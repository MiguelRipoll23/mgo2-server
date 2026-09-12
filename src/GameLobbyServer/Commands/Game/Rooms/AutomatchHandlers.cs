using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Automatch;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Rooms;

/// <summary>Starts an automatch search.</summary>
/// <param name="automatchService">Queue the searcher joins.</param>
/// <param name="characterService">Service that owns the character records.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class StartAutomatchHandler(
    AutomatchService automatchService,
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is not { } characterIdentifier)
        {
            await sessionHelper.SendResultAsync(session, CommandConstants.StartAutomatchResult, ErrorCodeConstants.ResultInvalidSession, cancellationToken);
            return;
        }

        var rule = packet.Payload.Length >= 1 ? packet.Payload[0] : -1;
        if (!AutomatchConstants.RuleFilters.Contains(rule))
        {
            // Not a value any menu row can produce, so queueing it would search
            // for a rule the player did not choose.
            await sessionHelper.SendResultAsync(session, CommandConstants.StartAutomatchResult, ErrorCodeConstants.ResultAutomatchCannotStart, cancellationToken);
            return;
        }

        var character = await characterService.FindByIdAsync(characterIdentifier, cancellationToken);
        automatchService.Enqueue(characterIdentifier, rule, character?.Experience ?? 0, true);

        // The starting band is the searcher's own window, and the shortfall is
        // the requirement minus one because they can always reach themselves.
        var searcher = automatchService.Find(characterIdentifier);
        var band = searcher is not null ? automatchService.Band(searcher) : 1;
        var needed = Math.Max(0, automatchService.PlayersNeededOnArrival() - 1);

        var writer = new PacketWriter();
        writer.WriteUInt32(ErrorCodeConstants.ResultNone);
        writer.WriteUInt8(band);
        writer.WriteUInt8(needed);
        await sessionHelper.SendPacketAsync(session, CommandConstants.StartAutomatchResult, writer.Build(), cancellationToken);
    }
}

/// <summary>Cancels an automatch search.</summary>
/// <param name="automatchService">Queue the searcher leaves.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class CancelAutomatchHandler(
    AutomatchService automatchService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is not { } characterIdentifier)
        {
            await sessionHelper.SendResultAsync(session, CommandConstants.CancelAutomatchResult, ErrorCodeConstants.ResultInvalidSession, cancellationToken);
            return;
        }

        var outcome = automatchService.Cancel(characterIdentifier);
        await sessionHelper.SendResultAsync(
            session,
            CommandConstants.CancelAutomatchResult,
            outcome == AutomatchCancelOutcome.TooLate
                ? ErrorCodeConstants.ResultAutomatchCancelTooLate
                : ErrorCodeConstants.ResultNone,
            cancellationToken);
    }
}

/// <summary>Writes the automatch pushes the ticker sends to a searcher.</summary>
public static class AutomatchPushWriter
{
    /// <summary>Number of population columns.</summary>
    private const int Columns = 23;

    /// <summary>Bytes the packed column array occupies.</summary>
    private const int ArrayBytes = 16;

    /// <summary>Highest column value the client's bar can render.</summary>
    private const int MaximumColumn = 15;

    /// <summary>
    /// Writes the search panel: a population histogram by player level, plus
    /// the searcher's band and shortfall.
    /// </summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="matchmaking">Population searching, by level.</param>
    /// <param name="inGame">Population playing, by level.</param>
    /// <param name="band">Level half-width shown to the searcher.</param>
    /// <param name="playersNeeded">Players the searcher still needs.</param>
    public static void WriteSearchPanel(
        PacketWriter writer,
        IReadOnlyList<int> matchmaking,
        IReadOnlyList<int> inGame,
        int band,
        int playersNeeded)
    {
        WriteColumns(writer, matchmaking);
        writer.WriteUInt8(Math.Clamp(band, 0, 22));
        writer.WriteUInt8(Math.Clamp(playersNeeded, 0, 0xff));
        writer.WriteUInt8(0);
        WriteColumns(writer, inGame);
        writer.WriteUInt8(0);
    }

    /// <summary>Writes the formed-match push.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="hostCharacterIdentifier">Character elected to create the room.</param>
    /// <param name="lobbyIdentifier">Lobby the match forms in.</param>
    /// <param name="lobbySubtype">Game type of the lobby.</param>
    /// <param name="rule">Rule of rotation entry zero.</param>
    /// <param name="settings">Settings block handed to the host.</param>
    public static void WriteMatchFound(
        PacketWriter writer,
        int hostCharacterIdentifier,
        int lobbyIdentifier,
        int lobbySubtype,
        int rule,
        byte[] settings)
    {
        writer.WriteUInt32((uint)hostCharacterIdentifier);
        writer.WriteUInt32((uint)lobbyIdentifier);
        writer.WriteUInt8(lobbySubtype);
        writer.WriteUInt8(rule);
        writer.WriteUInt32(0);
        writer.WriteUInt32(0);
        writer.WriteUInt8(0);
        writer.WriteBytes(settings);
    }

    /// <summary>Writes the push that releases a formed group with its room.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="gameIdentifier">Identifier of the created room.</param>
    public static void WriteMatchGame(PacketWriter writer, int gameIdentifier) =>
        writer.WriteUInt32((uint)gameIdentifier);

    /// <summary>Writes the push that tells a group its host never created the room.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="detail">Detail code carried with the failure.</param>
    public static void WriteMatchFailed(PacketWriter writer, int detail) =>
        writer.WriteUInt32((uint)detail);

    /// <summary>Packs the column counts two per byte, low nibble first.</summary>
    private static void WriteColumns(PacketWriter writer, IReadOnlyList<int> columns)
    {
        var packed = new byte[ArrayBytes];
        for (var column = 0; column < Columns; column++)
        {
            var value = Math.Clamp(column < columns.Count ? columns[column] : 0, 0, MaximumColumn);
            var index = column / 2;
            packed[index] |= (byte)(column % 2 == 0 ? value : value << 4);
        }

        writer.WriteBytes(packed);
    }
}
