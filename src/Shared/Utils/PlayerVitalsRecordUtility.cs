using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Encodes and decodes the gameplay-channel record that carries a character's
/// health and stamina.
/// </summary>
/// <remarks>
/// <para>
/// The record is two bytes — health, then stamina — and it is the easiest
/// thing in the capture to name, because both ends of its scale are visible in
/// one round. It opens <c>fa fa</c> (250, 250) and stays there between fights;
/// health then falls in steps and reaches <c>00</c>, after which it is back to
/// <c>fa</c>. Three deaths and three restores over the round, and stamina at
/// 250 in every one of the 3 269 records. So health is 0 when the character is
/// dead and <see cref="PlayerVitals.MaximumValue"/> when it is untouched, and
/// stamina is measured the same way with nothing else to say about it.
/// </para>
/// <para>
/// Only the first byte is measured. Health at <see cref="PlayerVitals.MaximumValue"/>
/// between fights and at zero on death is **[V]**, with three deaths and three
/// restores in the round; the second byte sitting at 250 throughout is **[I]**
/// stamina — inferred from being the byte that never moves, not observed.
/// </para>
/// <para>
/// It travels as a <b>tick record</b>, not a session record: its identifier is
/// below <see cref="Constants.UdpCommandConstants.TickRecordThreshold"/>, so
/// its length byte counts the attribute class as well and the record is one byte
/// shorter than that byte says. Two identifiers carry it — one per player —
/// and they differ by <c>0x800</c>, which is the same bit the recorded tick
/// stream uses for the second player.
/// </para>
/// <para>
/// The server does not send tick records yet, so nothing here dispatches this;
/// it is the codec for a record the capture measures, kept next to the measured
/// bytes rather than described in a document and retyped later.
/// </para>
/// </remarks>
public static class PlayerVitalsRecordUtility
{
    /// <summary>Identifier of the vitals record for the first player.</summary>
    public const ushort FirstPlayerType = 0x0080;

    /// <summary>Identifier of the vitals record for the second player.</summary>
    public const ushort SecondPlayerType = 0x0880;

    /// <summary>Attribute class the record travels under, in all four captured bodies.</summary>
    public const byte AttributeClass = 0x03;

    /// <summary>Length of the record's body: health, then stamina.</summary>
    public const int BodyLength = 2;

    /// <summary>Whether an identifier is one of the two that carry vitals.</summary>
    /// <param name="type">Record identifier.</param>
    /// <returns>True when the record is a vitals record.</returns>
    public static bool IsVitalsRecord(ushort type) => type is FirstPlayerType or SecondPlayerType;

    /// <summary>Builds the vitals record for one player.</summary>
    /// <param name="vitals">Health and stamina to report.</param>
    /// <param name="secondPlayer">
    /// Whether this is the second player's record rather than the first's.
    /// </param>
    /// <returns>The record, ready to be serialized into a content region.</returns>
    public static UdpMessage Build(PlayerVitals vitals, bool secondPlayer = false)
    {
        var clamped = vitals.Clamped();
        return new UdpMessage(
            secondPlayer ? SecondPlayerType : FirstPlayerType,
            (byte)(BodyLength + 1),
            AttributeClass,
            [(byte)clamped.Health, (byte)clamped.Stamina]);
    }

    /// <summary>Reads the health and stamina out of a record body.</summary>
    /// <param name="body">Record body.</param>
    /// <returns>
    /// The vitals, or <c>null</c> when the body is not two bytes long — which is
    /// how a record of another kind is told apart without guessing at its
    /// contents.
    /// </returns>
    public static PlayerVitals? Parse(ReadOnlySpan<byte> body) =>
        body.Length == BodyLength ? new PlayerVitals(body[0], body[1]) : null;
}