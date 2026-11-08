namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The tactical archetypes a bot can play.
/// </summary>
/// <remarks>
/// <para>
/// <b>They are AI behaviours, not MGO2 character classes.</b> The game's own
/// skills and classes are the client's (and the lobby's rows hold them); these
/// are what a bot's decision weights lean towards — a sniper holds and engages at
/// range, a recon walks and watches. Nothing here names a skill a character
/// carries or claims a class a player is shown.
/// </para>
/// <para>
/// The five are the names the task states; the game's own bot code is organised
/// differently (<c>docs/strings/codepaths.txt</c> carries the port's
/// <c>pl_bot_loc_tdm_*</c>, <c>pl_bot_mis_sup_*</c> and <c>pl_bot_mis_sne_*</c>
/// families, whose suffixes are weapon classes rather than archetypes), and that
/// is further evidence to keep these a design choice rather than a claim about
/// the game's own AI.
/// </para>
/// </remarks>
public enum BotRole
{
    /// <summary>Aggressive frontline combat and objective pressure.</summary>
    Assault,

    /// <summary>Team assistance, covering fire and defensive support.</summary>
    Support,

    /// <summary>Long-range engagements and overwatch.</summary>
    Sniper,

    /// <summary>Scouting, enemy detection and information gathering.</summary>
    Recon,

    /// <summary>Stealth, flanking and close-range engagements.</summary>
    Infiltrator,
}
