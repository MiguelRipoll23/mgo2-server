using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Codec of the active-event screen records: the start record the client
/// restores its live-event state from, the detail and its row updates, and the
/// streamed list the event screens open with. The short push records — the
/// result, the states, the counter pair and the roster shapes — live in
/// <see cref="EventRecordUtils"/>.
/// <para>
/// Several records carry a variable number of per-item state bytes and never
/// repeat their count on the wire, so a writer cannot know how many to emit and
/// a reader cannot know how many to expect. Each such writer takes the states
/// explicitly and asserts the size that follows from them.
/// </para>
/// </summary>
public static class EventActiveEventUtils
{
    /// <summary>
    /// Writes the active-event start record. The client clears its event caches
    /// on the snapshot reply that precedes it, so this is what restores the live
    /// event state afterwards.
    /// </summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="activeStateIdentifier">Active state the record belongs to; never zero.</param>
    /// <param name="sequence">Sequence of that active state.</param>
    /// <param name="eventIdentifier">Event being started; never zero.</param>
    /// <param name="globalState">Global phase byte.</param>
    /// <param name="participantStates">Eight participant states.</param>
    /// <param name="hostEnvironment">Environment the event runs with.</param>
    /// <param name="baseTimeSeconds">Absolute event base time.</param>
    /// <param name="flagByte">Flag byte whose meaning is not established.</param>
    /// <param name="detailByte">Detail byte whose meaning is not established.</param>
    public static void WriteActiveEventState(
        PacketWriter writer,
        int activeStateIdentifier,
        int sequence,
        int eventIdentifier,
        int globalState,
        byte[] participantStates,
        EventHostEnvironment hostEnvironment,
        int baseTimeSeconds,
        int flagByte,
        int detailByte)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(hostEnvironment);
        RequireParticipants(participantStates);

        var start = writer.Size;
        WritePrefix(writer, activeStateIdentifier, sequence);
        writer.WriteInt32(eventIdentifier);
        writer.WriteUInt8(globalState);
        WriteParticipantStates(writer, participantStates);
        EventHostEnvironmentUtils.Write(writer, hostEnvironment);
        writer.WriteInt32(baseTimeSeconds);
        writer.WriteUInt8(flagByte);
        writer.WriteUInt8(detailByte);

        AssertSize(writer, start, EventConstants.ActiveEventStateWireSize, "active event state");
    }

    /// <summary>Writes the event detail, whose length follows from its item states.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="activeStateIdentifier">Active state the detail belongs to; never zero.</param>
    /// <param name="sequence">Sequence of that active state.</param>
    /// <param name="eventIdentifier">Event being described.</param>
    /// <param name="globalState">Global phase byte.</param>
    /// <param name="uiContext">Screen context the detail is for.</param>
    /// <param name="detailFields">Nine u16 fields; the third must be the item-state count.</param>
    /// <param name="detailValue">Signed value carried before the item states.</param>
    /// <param name="itemStates">One state byte per list item.</param>
    /// <param name="flagBits">Flag bits carried after the item states.</param>
    /// <param name="detailByte">Detail byte whose meaning is not established.</param>
    /// <param name="trailingValue">Signed value carried after the detail byte.</param>
    /// <param name="signedValues">Eleven signed values.</param>
    /// <param name="metadataBytes">Ten metadata bytes.</param>
    /// <param name="trailingU16">Trailing u16.</param>
    public static void WriteEventDetail(
        PacketWriter writer,
        int activeStateIdentifier,
        int sequence,
        int eventIdentifier,
        int globalState,
        int uiContext,
        ushort[] detailFields,
        int detailValue,
        byte[] itemStates,
        int flagBits,
        int detailByte,
        int trailingValue,
        int[] signedValues,
        byte[] metadataBytes,
        ushort trailingU16)
    {
        ArgumentNullException.ThrowIfNull(writer);
        RequireLength(detailFields, EventConstants.EventDetailU16Count, "detail u16 fields");
        RequireLength(signedValues, EventConstants.EventDetailValueCount, "detail signed values");
        RequireLength(metadataBytes, EventConstants.EventDetailMetadataCount, "detail metadata");
        ArgumentNullException.ThrowIfNull(itemStates);

        // The client reads the count from the wire and then reads exactly that
        // many bytes, so a mismatch here would shift every later field.
        if (detailFields[2] != itemStates.Length)
        {
            throw new ArgumentException(
                "The third detail u16 field is the item-state count and must equal it.",
                nameof(detailFields));
        }

        var start = writer.Size;
        WritePrefix(writer, activeStateIdentifier, sequence);
        writer.WriteInt32(eventIdentifier);
        writer.WriteUInt8(globalState);
        writer.WriteUInt8(uiContext);
        foreach (var field in detailFields)
        {
            writer.WriteUInt16(field);
        }

        writer.WriteInt32(detailValue);
        writer.WriteBytes(itemStates);
        writer.WriteUInt8(flagBits);
        writer.WriteUInt8(detailByte);
        writer.WriteInt32(trailingValue);
        foreach (var value in signedValues)
        {
            writer.WriteInt32(value);
        }

        writer.WriteBytes(metadataBytes);
        writer.WriteUInt16(trailingU16);

        AssertSize(
            writer,
            start,
            EventConstants.EventDetailBaseWireSize + itemStates.Length,
            "event detail");
    }

    /// <summary>
    /// Writes a one-based row baseline. The row number is one-based here and
    /// zero-based in the update record, which is the difference the two commands
    /// exist to express.
    /// </summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="activeStateIdentifier">Active state the row belongs to.</param>
    /// <param name="sequence">Sequence of that active state.</param>
    /// <param name="eventIdentifier">Event being described.</param>
    /// <param name="globalState">Global phase byte.</param>
    /// <param name="uiContext">Screen context the row is for.</param>
    /// <param name="detailField">Detail u16 the row belongs to.</param>
    /// <param name="oneBasedRow">Row number, one-based.</param>
    /// <param name="bitsetRow">Eight bitset words.</param>
    /// <param name="itemStates">One state byte per list item.</param>
    public static void WriteRowResync(
        PacketWriter writer,
        int activeStateIdentifier,
        int sequence,
        int eventIdentifier,
        int globalState,
        int uiContext,
        ushort detailField,
        int oneBasedRow,
        int[] bitsetRow,
        byte[] itemStates)
    {
        ArgumentNullException.ThrowIfNull(writer);
        RequireLength(bitsetRow, EventConstants.EventRowWordCount, "bitset row");
        ArgumentNullException.ThrowIfNull(itemStates);
        if (oneBasedRow < 1 || oneBasedRow > EventConstants.EventRowWordCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(oneBasedRow),
                "A resynchronization row is numbered from one to eight.");
        }

        var start = writer.Size;
        WritePrefix(writer, activeStateIdentifier, sequence);
        writer.WriteInt32(eventIdentifier);
        writer.WriteUInt8(globalState);
        writer.WriteUInt8(uiContext);
        writer.WriteUInt16(detailField);
        writer.WriteUInt16(oneBasedRow);
        WriteBitsetRow(writer, bitsetRow);
        writer.WriteBytes(itemStates);

        AssertSize(
            writer,
            start,
            EventConstants.EventRowResyncBaseWireSize + itemStates.Length,
            "row resynchronization");
    }

    /// <summary>Writes a zero-based row update, which carries no state prefix.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="eventIdentifier">Event being described; never zero.</param>
    /// <param name="zeroBasedRow">Row number, zero-based.</param>
    /// <param name="bitsetRow">Eight bitset words.</param>
    /// <param name="itemStates">One state byte per list item.</param>
    public static void WriteRowUpdate(
        PacketWriter writer,
        int eventIdentifier,
        int zeroBasedRow,
        int[] bitsetRow,
        byte[] itemStates)
    {
        ArgumentNullException.ThrowIfNull(writer);
        RequireLength(bitsetRow, EventConstants.EventRowWordCount, "bitset row");
        ArgumentNullException.ThrowIfNull(itemStates);
        if (zeroBasedRow < 0 || zeroBasedRow >= EventConstants.EventRowWordCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(zeroBasedRow),
                "An update row is numbered from zero to seven.");
        }

        var start = writer.Size;
        writer.WriteInt32(eventIdentifier);
        writer.WriteUInt16(zeroBasedRow);
        WriteBitsetRow(writer, bitsetRow);
        writer.WriteBytes(itemStates);

        AssertSize(
            writer,
            start,
            EventConstants.EventRowUpdateBaseWireSize + itemStates.Length,
            "row update");
    }

    /// <summary>Writes the record that opens or closes a streamed event list.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="eventIdentifier">Event being listed; never zero.</param>
    public static void WriteEventListBoundary(PacketWriter writer, int eventIdentifier)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var start = writer.Size;
        writer.WriteInt32(eventIdentifier);
        AssertSize(writer, start, EventConstants.EventListBoundaryWireSize, "event list boundary");
    }

    private static void WritePrefix(PacketWriter writer, int activeStateIdentifier, int sequence)
    {
        if (activeStateIdentifier == 0)
        {
            throw new ArgumentException(
                "An active-event record needs a nonzero active-state identifier.",
                nameof(activeStateIdentifier));
        }

        writer.WriteInt32(activeStateIdentifier);
        writer.WriteUInt16(sequence);
    }

    private static void WriteParticipantStates(PacketWriter writer, byte[] states)
    {
        writer.WriteBytes(states);
    }

    private static void WriteBitsetRow(PacketWriter writer, int[] bitsetRow)
    {
        foreach (var word in bitsetRow)
        {
            writer.WriteInt32(word);
        }
    }

    private static void RequireParticipants(byte[] states)
    {
        ArgumentNullException.ThrowIfNull(states);
        if (states.Length != EventConstants.SnapshotParticipantCount)
        {
            throw new ArgumentException(
                $"An active-event record carries {EventConstants.SnapshotParticipantCount} participant states.",
                nameof(states));
        }
    }

    private static void RequireLength<T>(T[] values, int expected, string name)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length != expected)
        {
            throw new ArgumentException($"{name} must contain exactly {expected} values.", nameof(values));
        }
    }

    private static void AssertSize(PacketWriter writer, int start, int expected, string name)
    {
        var written = writer.Size - start;
        if (written != expected)
        {
            throw new InvalidOperationException(
                $"The {name} record is {written} bytes, expected {expected}.");
        }
    }
}