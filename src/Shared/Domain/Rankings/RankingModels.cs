namespace Mgo2Server.Shared.Domain.Rankings;

/// <summary>
/// One row of a board.
/// <para>
/// Rank is one-based and dense. Value is already in the units the client renders:
/// scaled to 8.8 fixed point for the rating boards, a plain count everywhere else.
/// </para>
/// </summary>
/// <param name="Rank">Position of the row on the whole board.</param>
/// <param name="Identifier">Character identifier, or clan identifier on the clan board.</param>
/// <param name="Name">Name of the character or clan.</param>
/// <param name="Value">Quantity the board sorts by.</param>
public sealed record RankingEntry(int Rank, int Identifier, string Name, int Value);

/// <summary>
/// A window of a board plus the size of the whole board, which is what drives the
/// client's scroll limit and its row count.
/// </summary>
/// <param name="Entries">Rows in the requested window.</param>
/// <param name="Total">Rows on the whole board.</param>
public sealed record RankingPage(IReadOnlyList<RankingEntry> Entries, int Total);

/// <summary>
/// One unranked subject of a board, as the board query produces it. The rank is
/// assigned later, over the whole board, which is why it is not carried here.
/// </summary>
/// <param name="Identifier">Character identifier, or clan identifier on the clan board.</param>
/// <param name="Name">Name of the character or clan.</param>
/// <param name="Value">Quantity the board sorts by, in the client's units.</param>
public readonly record struct RankingBoardRow(int Identifier, string Name, long Value);
