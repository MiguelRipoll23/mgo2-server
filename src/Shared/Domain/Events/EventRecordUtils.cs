using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Codec of the short event-push records: the result summary, the event and
/// roster states, the counter pair, the advance, and the two shapes the roster
/// commands share. The screen records — the start, the detail, its row updates
/// and the streamed list — live in <see cref="EventActiveEventUtils"/>.
/// <para>
/// The roster-state record carries no event identifier, so its layout is
/// separate from the event-state record that does, and the prefixed-value and
/// sequence-update shapes are the two ways a roster command answers.
/// </para>
/// </summary>
public static class EventRecordUtils
{
    /// <summary>Writes the event result summary.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="activeStateIdentifier">Active state the result belongs to.</param>
    /// <param name="sequence">Sequence of that active state.</param>
    /// <param name="eventIdentifier">Event that produced the result.</param>
    /// <param name="resultValues">Eight result values.</param>
    /// <param name="signedResult">Signed result.</param>
    /// <param name="tailA">First tail value.</param>
    /// <param name="tailB">Second tail value.</param>
    /// <param name="tailC">Third tail value.</param>
    public static void WriteEventResult(
        PacketWriter writer,
        int activeStateIdentifier,
        int sequence,
        int eventIdentifier,
        int[] resultValues,
        int signedResult,
        int tailA,
        int tailB,
        int tailC)
    {
        ArgumentNullException.ThrowIfNull(writer);
        RequireLength(resultValues, EventConstants.EventRowWordCount, "result values");

        var start = writer.Size;
        WritePrefix(writer, activeStateIdentifier, sequence);
        writer.WriteInt32(eventIdentifier);
        foreach (var value in resultValues)
        {
            writer.WriteInt32(value);
        }

        writer.WriteInt32(signedResult);
        writer.WriteInt32(tailA);
        writer.WriteInt32(tailB);
        writer.WriteInt32(tailC);

        AssertSize(writer, start, EventConstants.EventResultWireSize, "event result");
    }

    /// <summary>Writes an event-state record.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="activeStateIdentifier">Active state the record belongs to.</param>
    /// <param name="sequence">Sequence of that active state.</param>
    /// <param name="eventIdentifier">Event being described.</param>
    /// <param name="globalState">Global phase byte.</param>
    /// <param name="participantStates">Eight participant states.</param>
    public static void WriteEventState(
        PacketWriter writer,
        int activeStateIdentifier,
        int sequence,
        int eventIdentifier,
        int globalState,
        byte[] participantStates)
    {
        ArgumentNullException.ThrowIfNull(writer);
        RequireParticipants(participantStates);

        var start = writer.Size;
        WritePrefix(writer, activeStateIdentifier, sequence);
        writer.WriteInt32(eventIdentifier);
        writer.WriteUInt8(globalState);
        WriteParticipantStates(writer, participantStates);
        AssertSize(writer, start, EventConstants.EventStateWireSize, "event state");
    }

    /// <summary>Writes a roster-state record, which carries no event identifier.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="activeStateIdentifier">Active state the record belongs to.</param>
    /// <param name="sequence">Sequence of that active state.</param>
    /// <param name="globalState">Global phase byte.</param>
    /// <param name="participantStates">Eight participant states.</param>
    public static void WriteRosterState(
        PacketWriter writer,
        int activeStateIdentifier,
        int sequence,
        int globalState,
        byte[] participantStates)
    {
        ArgumentNullException.ThrowIfNull(writer);
        RequireParticipants(participantStates);

        var start = writer.Size;
        WritePrefix(writer, activeStateIdentifier, sequence);
        writer.WriteUInt8(globalState);
        WriteParticipantStates(writer, participantStates);
        AssertSize(writer, start, EventConstants.RosterStateWireSize, "roster state");
    }

    /// <summary>Writes a pair of counters against an event.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="eventIdentifier">Event being described.</param>
    /// <param name="firstValue">First counter.</param>
    /// <param name="secondValue">Second counter.</param>
    public static void WriteCounterPair(
        PacketWriter writer,
        int eventIdentifier,
        ushort firstValue,
        ushort secondValue)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var start = writer.Size;
        writer.WriteInt32(eventIdentifier);
        writer.WriteUInt16(firstValue);
        writer.WriteUInt16(secondValue);
        AssertSize(writer, start, EventConstants.CounterPairWireSize, "counter pair");
    }

    /// <summary>Writes an advance record.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="activeStateIdentifier">Active state the record belongs to.</param>
    /// <param name="sequence">Sequence of that active state.</param>
    /// <param name="value">Value being advanced to.</param>
    /// <param name="fieldA">First byte field.</param>
    /// <param name="fieldB">Second byte field.</param>
    public static void WriteAdvance(
        PacketWriter writer,
        int activeStateIdentifier,
        int sequence,
        int value,
        int fieldA,
        int fieldB)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var start = writer.Size;
        WritePrefix(writer, activeStateIdentifier, sequence);
        writer.WriteInt32(value);
        writer.WriteUInt8(fieldA);
        writer.WriteUInt8(fieldB);
        AssertSize(writer, start, EventConstants.AdvanceWireSize, "advance");
    }

    /// <summary>Writes a prefixed value, the shape several roster commands share.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="activeStateIdentifier">Active state the value belongs to.</param>
    /// <param name="sequence">Sequence of that active state.</param>
    /// <param name="value">Value carried.</param>
    public static void WritePrefixedValue(
        PacketWriter writer,
        int activeStateIdentifier,
        int sequence,
        int value)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var start = writer.Size;
        WritePrefix(writer, activeStateIdentifier, sequence);
        writer.WriteInt32(value);
        AssertSize(writer, start, EventConstants.PrefixedValueWireSize, "prefixed value");
    }

    /// <summary>Writes a sequence update.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="activeStateIdentifier">Active state the update belongs to.</param>
    /// <param name="currentSequence">Sequence the client currently holds.</param>
    /// <param name="newSequence">Sequence to move to.</param>
    public static void WriteSequenceUpdate(
        PacketWriter writer,
        int activeStateIdentifier,
        int currentSequence,
        int newSequence)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var start = writer.Size;
        WritePrefix(writer, activeStateIdentifier, currentSequence);
        writer.WriteUInt16(newSequence);
        AssertSize(writer, start, EventConstants.SequenceUpdateWireSize, "sequence update");
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