using Mgo2Server.GameLobbyServer.Commands.Game.Chat;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the in-game chat framing that the reference server reads out of the ELF.
/// <para>
/// A <c>0x4400</c> is <c>{u8 kind, u8 channel digit, text, NUL}</c> and the
/// <c>0x4401</c> the client displays is <c>{u32 speaker, channel digit, text, NUL}</c>.
/// The digit is the channel — the client computes it as <c>digit - '0'</c> and takes
/// the text from the byte behind it — so relaying <c>kind</c> instead, or leaving the
/// digit inside the text, shifts the string one byte and the line renders under a
/// channel and a first character the sender never typed.
/// </para>
/// </summary>
[Trait("Category", "GameLobby")]
public sealed class ChatPayloadTests
{
    /// <summary>Speaker character id the replies are attributed to.</summary>
    private const int Speaker = 3;

    [Fact]
    public void A_public_send_is_decoded_into_its_channel_digit_and_text()
    {
        // Captured from the client as `00 30 68 69 ...`, typed "hi".
        var request = ChatPayloadBuilder.ParseRequest(Request(0x00, (byte)'0', "hi"));

        Assert.NotNull(request);
        Assert.Equal((byte)'0', request.ChannelDigit);
        Assert.Equal("hi", request.Text);
    }

    [Fact]
    public void A_team_send_carries_the_kind_the_captures_paired_with_it()
    {
        // `/team team` arrived as `01 31 74 65 61 6d ...`.
        var request = ChatPayloadBuilder.ParseRequest(Request(0x01, (byte)'1', "team"));

        Assert.NotNull(request);
        Assert.Equal((byte)'1', request.ChannelDigit);
        Assert.Equal("team", request.Text);
    }

    [Fact]
    public void The_reply_puts_the_digit_at_the_byte_the_client_reads_the_channel_from()
    {
        var request = ChatPayloadBuilder.ParseRequest(Request(0x00, (byte)'0', "hi"));

        var reply = ChatPayloadBuilder.BuildReply(Speaker, Assert.IsType<ChatRequest>(request));

        // {u32 speaker}{'0'}{h}{i}{NUL} - eight bytes, the digit at offset 4.
        Assert.Equal([0x00, 0x00, 0x00, 0x03, (byte)'0', (byte)'h', (byte)'i', 0x00], reply);
    }

    [Fact]
    public void The_reply_relays_the_digit_and_not_the_kind_where_they_diverge()
    {
        // Channel 2 is the first the captures never reached: the send path pairs it
        // with kind 0, so a reply built from the kind would open the string with a
        // byte the client reads as channel -0x30.
        var request = ChatPayloadBuilder.ParseRequest(Request(0x00, (byte)'2', "hi"));

        var reply = ChatPayloadBuilder.BuildReply(Speaker, Assert.IsType<ChatRequest>(request));

        Assert.Equal((byte)'2', reply[4]);
        Assert.Equal([(byte)'h', (byte)'i', 0x00], reply[5..]);
    }

    [Fact]
    public void The_reply_is_terminated_rather_than_padded_to_a_width()
    {
        var request = ChatPayloadBuilder.ParseRequest(Request(0x00, (byte)'0', "hello"));

        var reply = ChatPayloadBuilder.BuildReply(Speaker, Assert.IsType<ChatRequest>(request));

        // Four of speaker, one of digit, five of text, one terminator.
        Assert.Equal(11, reply.Length);
        Assert.Equal(0x00, reply[^1]);
    }

    [Fact]
    public void A_message_is_truncated_to_the_127_bytes_the_client_can_display()
    {
        var request = ChatPayloadBuilder.ParseRequest(Request(0x00, (byte)'0', new string('a', 200)));

        Assert.NotNull(request);
        Assert.Equal(ChatPayloadBuilder.MaximumTextLength, request.Text.Length);
    }

    [Fact]
    public void A_payload_too_short_to_hold_a_kind_and_a_digit_is_dropped()
    {
        Assert.Null(ChatPayloadBuilder.ParseRequest([]));
        Assert.Null(ChatPayloadBuilder.ParseRequest([0x00]));
    }

    [Fact]
    public void A_channel_prefix_left_in_the_text_is_stripped()
    {
        // The client strips these itself, so this only guards against a sender that did not.
        var request = ChatPayloadBuilder.ParseRequest(Request(0x00, (byte)'0', "/all hi"));

        Assert.NotNull(request);
        Assert.Equal("hi", request.Text);
    }

    /// <summary>Builds a 129-byte request around the fields the client writes.</summary>
    /// <param name="kind">Coarse public/team flag.</param>
    /// <param name="channelDigit">ASCII channel digit.</param>
    /// <param name="text">Message body.</param>
    private static byte[] Request(byte kind, byte channelDigit, string text)
    {
        var payload = new byte[129];
        payload[0] = kind;
        payload[1] = channelDigit;
        for (var index = 0; index < text.Length && 2 + index < payload.Length; index++)
        {
            payload[2 + index] = (byte)text[index];
        }

        return payload;
    }
}
