using System.Net;
using System.Net.Sockets;
using Mgo2Server.Stun;
using Mgo2Server.Stun.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mgo2Server.Tests;

/// <summary>
/// Exercises the responder over real sockets, on two loopback addresses, which is
/// what decides which socket an answer leaves from.
/// </summary>
[Trait("Category", "Stun")]
public sealed class StunServerTests
{
    private static readonly IPAddress PrimaryAddress = IPAddress.Parse("127.0.0.1");

    private static readonly IPAddress SecondaryAddress = IPAddress.Parse("127.0.0.2");

    /// <summary>How long a probe waits for an answer before it gives up.</summary>
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Answers_from_the_address_the_request_arrived_at()
    {
        var deployment = Deploy();

        try
        {
            var (transactionIdentifier, response, answeredFrom) =
                await ProbeAsync(deployment.Primary, changeRequestFlags: 0, deployment.Cancellation.Token);

            Assert.Equal(deployment.Primary, answeredFrom);
            Assert.Equal(expected: transactionIdentifier, response[4..StunMessageCodec.HeaderLength]);
            Assert.Equal(StunMessageCodec.BindingResponseType, (response[0] << 8) | response[1]);
            Assert.Equal(response.Length - StunMessageCodec.HeaderLength, (response[2] << 8) | response[3]);
        }
        finally
        {
            await deployment.StopAsync();
        }
    }

    [Fact]
    public async Task Does_not_answer_a_change_address_request()
    {
        var deployment = Deploy();

        try
        {
            // Test II of the console: change the address and the port. The
            // reference server leaves it unanswered rather than refusing it, and
            // this responder is matched to it.
            await AssertNoAnswerAsync(
                deployment.Primary,
                StunMessageCodec.ChangeIpFlag | StunMessageCodec.ChangePortFlag,
                deployment.Cancellation.Token);
        }
        finally
        {
            await deployment.StopAsync();
        }
    }

    [Fact]
    public async Task Does_not_answer_a_change_port_request()
    {
        var deployment = Deploy();

        try
        {
            await AssertNoAnswerAsync(
                deployment.Primary,
                StunMessageCodec.ChangePortFlag,
                deployment.Cancellation.Token);
        }
        finally
        {
            await deployment.StopAsync();
        }
    }

    [Fact]
    public async Task Answers_the_change_address_probe_the_console_sends_to_the_other_socket()
    {
        // The leg the classification rests on once CHANGE-REQUEST is dropped: Test
        // I prime is a plain request to CHANGED-ADDRESS, with no change asked for,
        // and it has to be answered from the second socket.
        var deployment = Deploy();

        try
        {
            var (_, response, answeredFrom) = await ProbeAsync(
                deployment.Secondary,
                changeRequestFlags: 0,
                deployment.Cancellation.Token);

            Assert.Equal(deployment.Secondary, answeredFrom);
            Assert.Equal(
                expected: StunMessageCodec.BindingResponseType,
                (response[0] << 8) | response[1]);

            // The console also requires the two sockets to report the same mapping,
            // and its mapped port is the port it sent from. Both probes therefore
            // have to leave from one socket, or the comparison would be measuring
            // the ephemeral ports this test happened to get.
            var (primaryMapped, secondaryMapped) = await MappedPortsFromOneSocketAsync(
                deployment,
                deployment.Cancellation.Token);

            Assert.Equal(primaryMapped, secondaryMapped);
        }
        finally
        {
            await deployment.StopAsync();
        }
    }

    /// <summary>
    /// Probes both sockets from a single client socket and reads back the port
    /// each answer names as the console's mapped port.
    /// </summary>
    /// <param name="deployment">The responder to probe.</param>
    /// <param name="cancellationToken">Token that stops the responder.</param>
    private static async Task<(int Primary, int Secondary)> MappedPortsFromOneSocketAsync(
        TestDeployment deployment,
        CancellationToken cancellationToken)
    {
        using var client = new UdpClient(new IPEndPoint(PrimaryAddress, 0));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);

        var ports = new List<int>();

        foreach (var destination in new[] { deployment.Primary, deployment.Secondary })
        {
            var transactionIdentifier = new byte[16];
            Random.Shared.NextBytes(transactionIdentifier);

            await client.SendAsync(
                BuildRequest(transactionIdentifier, changeRequestFlags: 0),
                destination,
                timeout.Token);

            var response = await client.ReceiveAsync(timeout.Token);
            var attributes = ReadAttributes(response.Buffer);

            ports.Add(ReadAddress(ValueOf(attributes, StunMessageCodec.MappedAddressType)).Port);
        }

        return (ports[0], ports[1]);
    }

    [Fact]
    public async Task Names_the_configured_address_while_serving_every_interface()
    {
        // The shape of the container: one address to name, which the responder does
        // not own, and datagrams handed over on whichever interface they arrive on.
        var deployment = Deploy(withSecondAddress: false);

        try
        {
            var consoleAddress = IPAddress.Parse("127.0.0.2");
            var (_, response, answeredFrom) = await ProbeAsync(
                deployment.Primary,
                changeRequestFlags: 0,
                deployment.Cancellation.Token,
                consoleAddress);

            // The answer leaves from the address it names, whatever the datagram
            // arrived on, and the console is told how it appears from outside.
            Assert.Equal(deployment.Primary, answeredFrom);

            var attributes = ReadAttributes(response);
            var mapped = ReadAddress(ValueOf(attributes, StunMessageCodec.MappedAddressType));
            var source = ReadAddress(ValueOf(attributes, StunMessageCodec.SourceAddressType));
            var changed = ReadAddress(ValueOf(attributes, StunMessageCodec.ChangedAddressType));

            Assert.Equal(consoleAddress, mapped.Address);
            Assert.Equal(deployment.Primary.Address, source.Address);
            Assert.Equal(deployment.Primary.Port, source.Port);

            // One address: the other address is the same one, and the port is the
            // move that stands in for it.
            Assert.Equal(deployment.Primary.Address, changed.Address);
            Assert.Equal(deployment.Primary.Port + 1, changed.Port);
        }
        finally
        {
            await deployment.StopAsync();
        }
    }

    /// <summary>Starts a responder on loopback and an ephemeral port.</summary>
    /// <param name="withSecondAddress">Whether the second address is served.</param>
    private static TestDeployment Deploy(bool withSecondAddress = true)
    {
        var port = ReservePort();

        var options = new StunServerOptions
        {
            Port = port,
            PrimaryAddress = PrimaryAddress.ToString(),
            SecondaryAddress = withSecondAddress ? SecondaryAddress.ToString() : null,
        };

        var cancellation = new CancellationTokenSource();
        var server = new StunServer(options, NullLogger<StunServer>.Instance);

        return new TestDeployment(
            new IPEndPoint(PrimaryAddress, port),
            new IPEndPoint(SecondaryAddress, port + 1),
            cancellation,
            server.RunAsync(cancellation.Token));
    }

    /// <summary>
    /// Sends one binding request and returns the transaction identifier it used,
    /// the answer and the endpoint the answer came from.
    /// </summary>
    /// <param name="destination">Address and port to send to.</param>
    /// <param name="changeRequestFlags">Change the request asks the responder to make.</param>
    /// <param name="cancellationToken">Token that stops the responder.</param>
    /// <param name="sourceAddress">Address the console sends from, loopback by default.</param>
    private static async Task<(byte[] TransactionIdentifier, byte[] Response, IPEndPoint AnsweredFrom)>
        ProbeAsync(
            IPEndPoint destination,
            int changeRequestFlags,
            CancellationToken cancellationToken,
            IPAddress? sourceAddress = null)
    {
        using var client = new UdpClient(new IPEndPoint(sourceAddress ?? PrimaryAddress, 0));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);

        var transactionIdentifier = new byte[16];
        Random.Shared.NextBytes(transactionIdentifier);

        await client.SendAsync(
            BuildRequest(transactionIdentifier, changeRequestFlags),
            destination,
            timeout.Token);

        var response = await client.ReceiveAsync(timeout.Token);

        return (transactionIdentifier, response.Buffer, response.RemoteEndPoint);
    }

    /// <summary>Builds a binding request the way the console does.</summary>
    /// <param name="transactionIdentifier">Transaction identifier of the request.</param>
    /// <param name="changeRequestFlags">Change the request asks the responder to make.</param>
    private static byte[] BuildRequest(byte[] transactionIdentifier, int changeRequestFlags)
    {
        var hasChangeRequest = changeRequestFlags != 0;
        var bodyLength = hasChangeRequest ? 8 : 0;

        var datagram = new byte[StunMessageCodec.HeaderLength + bodyLength];

        datagram[1] = 0x01; // A binding request.
        datagram[2] = (byte)(bodyLength >> 8);
        datagram[3] = (byte)bodyLength;
        transactionIdentifier.CopyTo(datagram, 4);

        if (hasChangeRequest)
        {
            datagram[StunMessageCodec.HeaderLength + 1] = (byte)StunMessageCodec.ChangeRequestType;
            datagram[StunMessageCodec.HeaderLength + 3] = 0x04;
            datagram[StunMessageCodec.HeaderLength + 7] = (byte)changeRequestFlags;
        }

        return datagram;
    }

    /// <summary>
    /// Asserts that a request is left unanswered: no reply arrives, and none is
    /// refused either. A 420 Error Response would satisfy neither the console nor
    /// this test, which is why the absence of any datagram is the contract.
    /// </summary>
    /// <param name="destination">Address and port to send to.</param>
    /// <param name="changeRequestFlags">Change the request asks the responder to make.</param>
    /// <param name="cancellationToken">Token that stops the responder.</param>
    private static async Task AssertNoAnswerAsync(
        IPEndPoint destination,
        int changeRequestFlags,
        CancellationToken cancellationToken)
    {
        using var client = new UdpClient(new IPEndPoint(PrimaryAddress, 0));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Long enough that a slow answer would still be caught as an answer, short
        // enough not to hold the suite up.
        timeout.CancelAfter(ProbeTimeout);

        var transactionIdentifier = new byte[16];
        Random.Shared.NextBytes(transactionIdentifier);

        await client.SendAsync(
            BuildRequest(transactionIdentifier, changeRequestFlags),
            destination,
            timeout.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await client.ReceiveAsync(timeout.Token));
    }

    /// <summary>Reads the attributes of a message as its type and value pairs.</summary>
    /// <param name="message">Message to read.</param>
    private static List<(int Type, byte[] Value)> ReadAttributes(byte[] message)
    {
        var bodyLength = (message[2] << 8) | message[3];
        var attributes = new List<(int Type, byte[] Value)>();

        var offset = StunMessageCodec.HeaderLength;
        while (offset + 4 <= StunMessageCodec.HeaderLength + bodyLength)
        {
            var attributeType = (message[offset] << 8) | message[offset + 1];
            var attributeLength = (message[offset + 2] << 8) | message[offset + 3];

            attributes.Add((attributeType, message[(offset + 4)..(offset + 4 + attributeLength)]));
            offset += 4 + attributeLength + (4 - attributeLength % 4) % 4;
        }

        return attributes;
    }

    /// <summary>Reads the value of one attribute of a message.</summary>
    /// <param name="attributes">Attributes of the message.</param>
    /// <param name="attributeType">Type of the attribute to read.</param>
    private static byte[] ValueOf(List<(int Type, byte[] Value)> attributes, int attributeType) =>
        attributes.Single(attribute => attribute.Type == attributeType).Value;

    /// <summary>Reads the address an address attribute value holds.</summary>
    /// <param name="value">Value to read.</param>
    private static IPEndPoint ReadAddress(byte[] value) =>
        new(new IPAddress(value[4..8]), (value[2] << 8) | value[3]);

    /// <summary>Reserves a UDP port by binding it and letting it go again.</summary>
    private static int ReservePort()
    {
        using var socket = new UdpClient(new IPEndPoint(PrimaryAddress, 0));

        return ((IPEndPoint)socket.Client.LocalEndPoint!).Port;
    }

    /// <summary>A responder running on loopback until it is stopped.</summary>
    /// <param name="Primary">Address and port the console dials.</param>
    /// <param name="Secondary">Second address and the alternate port.</param>
    /// <param name="Cancellation">Token that stops the responder.</param>
    /// <param name="Server">The receive loops of the responder.</param>
    private sealed record TestDeployment(
        IPEndPoint Primary,
        IPEndPoint Secondary,
        CancellationTokenSource Cancellation,
        Task Server)
    {
        /// <summary>Stops the responder and waits for its sockets to close.</summary>
        public async Task StopAsync()
        {
            await Cancellation.CancelAsync();
            await Server;
            Cancellation.Dispose();
        }
    }
}
