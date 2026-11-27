using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The stage's result: which side took it, and the two state writes that
/// publish it 21.133 s behind the round's result.
/// </summary>
/// <remarks>
/// <para>
/// <b>The writes are the result screen's, not the round's.</b> The round's
/// result is <c>02 02 02 03 03</c> at t+163.597; the stage's own fields follow
/// at t+184.730 as <c>02 04 02 01 02</c> and <c>02 07 01 02</c>. The same pair
/// is written 21.136 s behind the same result in
/// <c>swap-spawnpoint-side-between-rounds-enabled.pcapng</c>, so the wait is
/// the screen's and not a clock this host has.
/// </para>
/// <para>
/// <b>The first write's two bytes are the winning side's raw team and its
/// rounds won.</b> Both captures are team 1's 2–0, so offset 4 carries
/// <c>1, 2</c>, and offset 7 carries the stage's own total, 2, because that
/// stage was a sweep. A game whose rounds were split is decided on the higher
/// total score and its rounds are stated the same way; the stage total is then
/// more than the winner's. The older <c>pcapng1</c> writes a team-0 win as
/// <c>02 04 03 00 02 02</c>, three bytes from offset 4, with the total there; the
/// reference versions differ in the total's byte, and this writer follows the
/// t+184.730 capture. A <c>02</c> offset is a byte offset into the state block:
/// offset 4 is the block's bytes 2–3, which the <c>00</c> vector carries as its
/// bytes 0–1 because the vector starts at block byte 2.
/// </para>
/// </remarks>
public sealed partial class MatchResultService
{
    /// <summary>
    /// Offset of the winning side in the stage result's state block — the block's
    /// byte 2, which is vector byte 0 of the <c>00</c> record's data.
    /// </summary>
    private const int StageWinnerIndex = 4;

    /// <summary>Offset of the stage's total rounds in the stage result's state block.</summary>
    private const int StageTotalIndex = 7;

    /// <summary>
    /// The stage result the round that was awarded last decided, or <c>null</c>
    /// for a level split or a room that has emptied.
    /// </summary>
    private StageResult? stageResult;

    /// <summary>
    /// The stage result the last awarded round decided, or <c>null</c>.
    /// </summary>
    public StageResult? LastStageResult => stageResult;

    /// <summary>
    /// The side a decided stage belongs to, the rounds it won and the rounds the
    /// stage played.
    /// </summary>
    /// <param name="WinnerTeam">Raw team value the stage's result names.</param>
    /// <param name="WinningRounds">Rounds the winning side took.</param>
    /// <param name="StageRounds">Rounds the stage as a whole played.</param>
    public sealed record StageResult(byte WinnerTeam, byte WinningRounds, byte StageRounds);

    /// <summary>
    /// Writes the stage's result to a peer once its result screens have been up
    /// for <see cref="MatchScreenTimingsUtils.StageResultMilliseconds"/>.
    /// </summary>
    /// <remarks>
    /// <b>The stage end is the guard.</b> A room that emptied, a round that
    /// closed afterwards, or a new open moves the version or clears the stage
    /// end, and the wait then writes nothing. The two records take consecutive
    /// values of the round's operations stream where they are sent, so whatever
    /// the beat wrote during the screen is numbered in front of them.
    /// </remarks>
    /// <param name="context">Context the stage result is written through.</param>
    /// <param name="generation">Identifier set the stage played under.</param>
    /// <param name="stageEndVersion">The stage end this write belongs to.</param>
    public async Task WriteStageResultAsync(
        PeerContext context,
        MatchRecordGenerationUtils generation,
        int stageEndVersion)
    {
        var result = stageResult;
        if (result is null)
        {
            return;
        }

        try
        {
            await Task.Delay(MatchScreenTimingsUtils.StageResultMilliseconds);

            if (!MatchRecordPeerUtils.IsAlive(context.Session)
                || !roundState.IsStageEnding
                || roundState.StageEndVersion != stageEndVersion
                || roundState.IsOpen)
            {
                return;
            }

            var ordinals = sequences.Take(generation.Operations, 2);
            var records = new MatchRecordFramesUtils.RecordMessage[]
            {
                new(
                    generation.Operations,
                    MatchRecordBodiesUtils.StateWrite(
                        StageWinnerIndex,
                        result.WinnerTeam,
                        result.WinningRounds),
                    ordinals[0]),
                new(
                    generation.Operations,
                    MatchRecordBodiesUtils.StateWrite(StageTotalIndex, result.StageRounds),
                    ordinals[1]),
            };

            await MatchRecordSendUtils.SendAsync(context, records);
            logger.LogInformation(
                "UDP {LocalPort}: writing the stage result to {RemoteAddress}: "
                + "state[{WinnerOffset}] = {WinnerTeam}, state[5] = {WinnerRounds}, "
                + "state[{TotalOffset}] = {TotalRounds}",
                context.LocalPort,
                context.Remote,
                StageWinnerIndex,
                result.WinnerTeam,
                result.WinningRounds,
                StageTotalIndex,
                result.StageRounds);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "UDP {LocalPort}: the stage result was not written to {RemoteAddress}",
                context.LocalPort,
                context.Remote);
        }
    }
}
