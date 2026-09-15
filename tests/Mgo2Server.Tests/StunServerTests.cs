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
    public async Task Answers_a_change_address_request_from_the_other_address_and_port()
    {
        var deployment = Deploy();

        try
        {
            // Test II of the console: change the address and the port.
            var (_, _, answeredFrom) = await ProbeAsync(
                deployment.Primary,
                StunMessageCodec.ChangeIpFlag | StunMessageCodec.ChangePortFlag,
                deployment.Cancellation.Token);

            Assert.Equal(deployment.Secondary, answeredFrom);
        }
        finally
        {
            await deployment.StopAsync();
        }
    }

    [Fact]
    public async Task Answers_a_change_port_request_from_the_alternate_port_of_the_same_address()
    {
        var deployment = Deploy();

        try
        {
            var (_, _, answeredFrom) = await ProbeAsync(
                deployment.Primary,
                StunMessageCodec.ChangePortFlag,
                deployment.Cancellation.Token);

            Assert.Equal(
                new IPEndPoint(deployment.Primary.Address, deployment.Primary.Port + 1),
                answeredFrom);
        }
        finally
        {
            await deployment.StopAsync();
        }
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
