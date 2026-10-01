using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Utils;
using Xunit.Abstractions;

namespace Mgo2Server.Tests;

/// <summary>
/// Decodes the live frames of the 17:54 session, to find the one carrying the
/// join request.
/// </summary>
/// <remarks>
/// That session is the first on the build that registers the join-request tag
/// and logs an unhandled type as a warning. Neither fired, so whatever the
/// client sent is decoding to something the host already recognises, or to
/// nothing at all. The frames come in three sizes — 17-byte acknowledgements
/// and 100- and 106-byte payloads — and only one of them can be the join.
/// </remarks>
[Trait("Category", "Shared")]
public sealed class JoinerFramesTests(ITestOutputHelper output)
{
    /// <summary>Session key the gameplay server logged for that peer.</summary>
    private const uint SessionKey = 0x1d3f249c;

    private const string Frame100 =
        "7FC43ED47290360151DF462B97D445AB16E841BF6A648814471AA90AE08324AB922"
        + "436DBE2E597BDC706C6423838F0A443CAFFD8451DDBE285FEA2627242C4E8BD68"
        + "0666B10F3CED982433574F17857C5C6501C011EB0D81A1EC19E1BCEE52016B46C405";

    private const string Frame106 =
        "6B4DCB956DDDC341B012D3D5B627D0EB3735DEFF45B3F054E7AFDACBC14E936BB"
        + "2F1A39CC3B062FCE6D3750119ED6DE5621D721865EAA823A58931A2120D51295C"
        + "BD93A6D1C0A9ADBBF7BE976FA47633A3B69E8130348242C53F1B4A547F5B4C6D590"
        + "088B1CCD4AE3448";

    private void Report(string label, string hex)
    {
        var work = Convert.FromHexString(hex);
        var counter = FrameCryptoUtility.UnscrambleHeaderOnly(work);
        var digestOk = FrameCryptoUtility.VerifyTailDigest(work, SessionKey ^ UdpCryptoKeyConstants.TailDigestKey);
        FrameCryptoUtility.RemoveChainInPlace(work, counter, SessionKey);
        var frame = MessageCodecUtility.DecodeFrame(work);

        output.WriteLine($"--- {label} ---");
        output.WriteLine($"counter=0x{counter:x4} (seq={counter & UdpCommandConstants.CounterMask}) digest={digestOk} compressed={frame.Compressed}");
        output.WriteLine($"content={frame.Content.Length} bytes: {Convert.ToHexString(frame.Content)}");
        output.WriteLine($"messages={frame.Messages.Count}");
        foreach (var message in frame.Messages)
        {
            output.WriteLine($"  type=0x{message.Type:x4} len={message.Length} flags={message.Flags}");
            output.WriteLine($"    text={System.Text.Encoding.Latin1.GetString(message.Body).TrimEnd('\0')}");
        }
    }

    [Fact]
    public void The_100_byte_frame_decodes_to_something_the_host_answers()
    {
        Report("100 bytes", Frame100);
        Report("106 bytes", Frame106);
    }
}