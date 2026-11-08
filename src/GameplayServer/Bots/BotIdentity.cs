namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// One bot's identity: the name it is known by and the number the name carries.
/// </summary>
/// <remarks>
/// <para>
/// <b>The name is <c>BOT-</c> and a number and nothing else.</b> It is not a
/// player-like name, a role title or a random string: a bot that reads as a person
/// in the room list is a bot a player will talk to, and the recorded dedicated
/// hosts' own bots are named this way — the briefing capture's ready set names
/// <c>BOT-01</c>, <c>BOT-02</c> and <c>BOT-03</c> (<c>docs/GAMEPLAY_SERVER.md</c>,
/// the ready-set reading). This server's format is the same shape without the zero
/// padding, as the task states it.
/// </para>
/// <para>
/// The number is kept beside the name so a released name can be named in a log
/// without parsing the string back apart.
/// </para>
/// </remarks>
/// <param name="Name">The bot's character name, for its whole lifetime.</param>
/// <param name="Number">The sequence number the name was built from.</param>
public sealed record BotIdentity(string Name, int Number);
