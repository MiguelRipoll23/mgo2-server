namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// One field of the entry a record is built on, as the image's selector names it.
/// </summary>
/// <remarks>
/// The numbers are the image's <c>P2P_SetRecordHeaderField</c> selectors, not this host's
/// ordering: a call site that reads <c>P2P_SetRecordHeaderField(handle, 3, id)</c> in the
/// binary is <see cref="Identifier"/> here.
/// </remarks>
public enum RecordHeaderField
{
    /// <summary>
    /// Selector 0: the low nibble of the byte at <c>+0xa</c>, with the high nibble left as
    /// it was. Bit 0 of the nibble is the sequenced bit <c>P2P_SendRecord</c> tests.
    /// </summary>
    SequenceFlags = 0,

    /// <summary>Selector 1: the byte at <c>+0xd</c>, refused at <c>0x100</c> and above.</summary>
    Channel = 1,

    /// <summary>Selector 2: the byte at <c>+9</c>, refused at <c>0x100</c> and above.</summary>
    State = 2,

    /// <summary>Selector 3: the halfword at <c>+4</c>, refused at <c>0x10000</c> and above.</summary>
    Identifier = 3,
}

/// <summary>
/// The image's <c>P2P_SetRecordHeaderField</c> (<c>0x26b2b0</c>): writes one header field of
/// the entry a record is being built on, and answers the code the image answers.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a field writer and not an answer path.</b> The decompiled body resolves the
/// handle and writes exactly one field: nothing is sent, and neither the write counter at
/// <c>+0xb</c> nor the acknowledged base at <c>+0xc</c> is touched — an earlier reading of
/// this function had it advancing the sequence, which is what the send path does and this
/// does not. Its senders stamp a freshly built record with it and then hand the record to
/// <c>P2P_SendRecord</c>, which carries the header out unchanged.
/// </para>
/// <para>
/// <b>The refusals are the image's own, and they are width checks.</b> Selectors 1 and 2
/// take a byte and are refused at <c>0x100</c>; selector 3 takes the sixteen-bit identifier
/// and is refused at <c>0x10000</c>; an unknown selector is refused, and so is a handle
/// that resolves to nothing (<c>0xffffffff</c> — the image's unresolved-handle answer).
/// A refused write leaves the field as it was, so a caller that ignores the code gets an
/// unstamped record rather than a truncated one.
/// </para>
/// <para>
/// <b>Selector 0 reads before it writes.</b> The body takes the byte, keeps <c>&amp; 0xf0</c>,
/// masks the value to four bits and stores the pair — so the field is four bits wide and the
/// upper half of the byte belongs to whatever else writes it. Nothing here masks the value
/// for the other three selectors: they refuse instead, which is the difference the image
/// makes between a nibble field and a byte field.
/// </para>
/// </remarks>
public static class RecordHeaderFieldService
{
    /// <summary>The image's success answer.</summary>
    public const int Success = 0;

    /// <summary>The image's refusal answer, <c>0xffffffff</c>.</summary>
    public const int Failure = -1;

    /// <summary>
    /// Writes one field of an entry and answers the image's code.
    /// </summary>
    /// <param name="entry">Entry the record is built on; null stands for a handle that resolves to nothing.</param>
    /// <param name="field">Field to write.</param>
    /// <param name="value">Value to write, checked against the field's width.</param>
    /// <returns><see cref="Success"/>, or <see cref="Failure"/> when the image refuses the write.</returns>
    public static int Set(RecordHeaderEntry? entry, RecordHeaderField field, uint value)
    {
        if (entry is null)
        {
            return Failure;
        }

        switch (field)
        {
            // Selector 0 masks instead of refusing: the field is a nibble by construction.
            // Both halves are narrowed to a byte before they are joined, which is the width
            // the image writes: the high nibble as it was, the low four bits of the value.
            case RecordHeaderField.SequenceFlags:
                var written = (byte)(value & 0x0f);
                entry.SequenceFlags = (byte)((byte)(entry.SequenceFlags & 0xf0) | written);
                return Success;

            case RecordHeaderField.Channel:
                if (value >= 0x100)
                {
                    return Failure;
                }

                entry.Channel = (byte)value;
                return Success;

            case RecordHeaderField.State:
                if (value >= 0x100)
                {
                    return Failure;
                }

                entry.State = (byte)value;
                return Success;

            case RecordHeaderField.Identifier:
                if (value >= 0x10000)
                {
                    return Failure;
                }

                entry.Identifier = (ushort)value;
                return Success;

            default:
                return Failure;
        }
    }
}
