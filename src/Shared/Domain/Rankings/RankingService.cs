namespace Mgo2Server.Shared.Domain.Rankings;

/// <summary>
/// The Rankings screens: a board is ordered by one quantity and a window of the
/// result is returned together with the size of the whole board, which is what
/// drives the client's scroll limit.
/// <para>
/// The clients sends <c>records</c> 1 when it wants its own standing and 100 for a
/// page, and it rejects the whole reply when the record count exceeds what it asked
/// for, so the window is clamped rather than trusted.
/// </para>
/// </summary>
/// <param name="boardService">Service that sources the board being ranked.</param>
public sealed class RankingService(RankingBoardService boardService)
{
    /// <summary>
    /// Largest window the client ever asks for, and the number it accepts. Also well
    /// inside the client's receive buffer.
    /// </summary>
    public const int MaximumRecords = 100;

    /// <summary>
    /// Player boards use term 0 and 1, clan boards 2 and 3; the odd member of each
    /// pair is the MONTH half of the toggle, which is why the test is on the low bit.
    /// </summary>
    private const int PeriodicTermBit = 1;

    /// <summary>A window of the player board, or the subject's own row when its identifier is non-zero.</summary>
    /// <param name="term">Period selector; the low bit asks for the current month.</param>
    /// <param name="rule">Game mode, used by the score board only.</param>
    /// <param name="key">Board selector.</param>
    /// <param name="from">Row offset of the window.</param>
    /// <param name="records">Rows the client asked for, or 1 for its own row.</param>
    /// <param name="characterIdentifier">Character whose own row is requested, or zero for a page.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<RankingPage> PlayersAsync(
        int term,
        int rule,
        int key,
        int from,
        int records,
        long characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        var board = await boardService.PlayerBoardAsync(key, rule, IsPeriodic(term), cancellationToken);
        return BuildPage(board, from, records, characterIdentifier);
    }

    /// <summary>A window of the clan board, or the subject's own row when its identifier is non-zero.</summary>
    /// <param name="term">Period selector; the low bit asks for the current month.</param>
    /// <param name="key">Board selector.</param>
    /// <param name="from">Row offset of the window.</param>
    /// <param name="records">Rows the client asked for, or 1 for its own row.</param>
    /// <param name="clanIdentifier">Clan whose own row is requested, or zero for a page.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<RankingPage> ClansAsync(
        int term,
        int key,
        int from,
        int records,
        long clanIdentifier,
        CancellationToken cancellationToken = default)
    {
        var board = await boardService.ClanBoardAsync(key, IsPeriodic(term), cancellationToken);
        return BuildPage(board, from, records, clanIdentifier);
    }

    /// <summary>Ranks the whole board, dense and one-based, then cuts the requested window out of it.</summary>
    /// <param name="board">Unranked subjects of the board.</param>
    /// <param name="from">Row offset of the window.</param>
    /// <param name="records">Rows the client asked for.</param>
    /// <param name="subject">Subject whose own row is requested, or zero for a page.</param>
    private static RankingPage BuildPage(
        IReadOnlyList<RankingBoardRow> board,
        int from,
        int records,
        long subject)
    {
        var ranked = board
            .OrderByDescending(row => row.Value)
            // Ties break on the identifier, so paging is stable across requests.
            .ThenBy(row => row.Identifier)
            .Select((row, index) => new RankingEntry(
                index + 1,
                row.Identifier,
                row.Name,
                unchecked((int)row.Value)))
            .ToList();

        var limit = Math.Clamp(records, 0, MaximumRecords);
        if (limit == 0)
        {
            return new RankingPage([], ranked.Count);
        }

        var entries = subject != 0
            ? ranked.Where(entry => entry.Identifier == subject).ToList()
            : ranked.Skip(Math.Max(from, 0)).Take(limit).ToList();

        return new RankingPage(entries, ranked.Count);
    }

    /// <summary>Whether the periodic half of the client's toggle was asked for.</summary>
    /// <param name="term">Period selector of the request.</param>
    private static bool IsPeriodic(int term) => (term & PeriodicTermBit) != 0;
}
