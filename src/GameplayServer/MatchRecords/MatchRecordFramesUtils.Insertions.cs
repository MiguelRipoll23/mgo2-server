using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The insertions whose fourth byte is taken from a stream, and the conversion of a
/// builder's message into the codec's.
/// </summary>
/// <remarks>
/// A partial of <see cref="MatchRecordFramesUtils"/>, split out to keep each file under the
/// repository's size limit. These members live together because they are where a record's
/// fourth byte is decided: the value is taken where the record is built, as the image's
/// builders do, and the send path assigns nothing (<c>docs/GHIDRA.md</c>).
/// </remarks>
public static partial class MatchRecordFramesUtils
{
    /// <summary>A control-byte insertion.</summary>
    /// <param name="body">Control body to carry.</param>
    /// <param name="fourthByte">Value of the record's fourth byte.</param>
    /// <returns>The insertion record.</returns>
    public static RecordMessage ControlInsertion(byte[] body, byte fourthByte) =>
        new(UdpCommandConstants.InGameControl, body, fourthByte);

    /// <summary>
    /// A control-byte insertion whose value is taken now, where the record is built, from
    /// the room's own exchange stream.
    /// </summary>
    /// <remarks>
    /// <b>The control is counted on the room's exchange and not on the operations
    /// stream.</b> <c>0xd001</c>'s low twelve bits are <c>0x001</c>, which is the stream the
    /// roster run and the room's count travel on, and the capture's controls sit inside
    /// that sequence, between the roster records that share it.
    /// </remarks>
    /// <param name="body">Control body to carry.</param>
    /// <param name="sequences">The room's streams, which the value is taken from.</param>
    /// <returns>The insertion record.</returns>
    public static RecordMessage ControlInsertion(byte[] body, MatchRecordSequenceService sequences) =>
        new(UdpCommandConstants.InGameControl, body, sequences.Take(UdpCommandConstants.InGameControl));

    /// <summary>Converts one stream message into the codec's message type.</summary>
    /// <remarks>
    /// The record is built from its entry: the identifier is written through
    /// <see cref="RecordHeaderFieldService.Set"/>, which is the image's field setter, and the
    /// fourth byte the builder took travels with it (<see cref="RecordHeaderEntry.Build"/>).
    /// </remarks>
    /// <param name="message">Message to convert.</param>
    /// <returns>The message the frame builder serializes.</returns>
    public static UdpMessage ToUdpMessage(RecordMessage message) =>
        RecordHeaderEntry.Build(message.Type, message.Body, message.FourthByte);
}
