using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Gameplay;

/// <summary>
/// One actor's position as a client writes it: the three coordinates at a tenth
/// of a stage unit and the heading, read from the sixteen-byte position record.
/// </summary>
/// <param name="X">Stage <c>x</c> at a tenth of a unit, at body offset <c>+6</c>.</param>
/// <param name="Y">Height at a tenth of a unit, at body offset <c>+8</c>.</param>
/// <param name="Z">Stage <c>z</c> at a tenth of a unit, at body offset <c>+10</c>.</param>
/// <param name="Heading">The heading, at body offset <c>+12</c>.</param>
public sealed record PlayerPosition(short X, short Y, short Z, short Heading)
{
    /// <summary>Attribute class the position record carries, which is also its kind.</summary>
    public const byte AttributeClass = 2;

    /// <summary>Length of the body the record carries.</summary>
    public const int BodyLength = 16;

    /// <summary>
    /// Reads a position out of a client's own position body.
    /// </summary>
    /// <remarks>
    /// The layout is a live client's, not a guess: at Blood Bath's red spawn a
    /// client writes <c>20 00 00 00 00 00 b9 f6 50 00 1b 08 ff 5f 00 00</c>, which
    /// is that stage's <c>x = -23750</c> and <c>z = 20750</c> and the stand height
    /// <c>800</c> at a tenth of a unit, and the spawn's own heading <c>24575</c>.
    /// </remarks>
    /// <param name="body">The sixteen-byte body.</param>
    /// <returns>The position it carries.</returns>
    public static PlayerPosition FromBody(ReadOnlySpan<byte> body) => new(
        ReadInt16(body, 6),
        ReadInt16(body, 8),
        ReadInt16(body, 10),
        ReadInt16(body, 12));

    /// <summary>Reads one signed sixteen-bit value little-endian.</summary>
    /// <param name="body">Body to read.</param>
    /// <param name="offset">Offset of the low byte.</param>
    /// <returns>The value.</returns>
    private static short ReadInt16(ReadOnlySpan<byte> body, int offset) =>
        (short)(body[offset] | (body[offset + 1] << 8));
}

/// <summary>
/// Holds the actor position each player last reported, keyed by the endpoint the
/// report arrived from.
/// </summary>
/// <remarks>
/// <para>
/// <b>A client's position is a tick record this host relays and used to forget.</b>
/// It travels under the position record of the player's own actor block, which is
/// passed to the room's other peers unchanged
/// (<see cref="GameplayServerService"/>) and, until now, kept nowhere. Holding it
/// is what lets a bot be written where a player actually is, which is the
/// arrangement the bot actors are being checked with: a bot at the player's own
/// point is on the map if the client shows anything at all.
/// </para>
/// <para>
/// Nothing here is written back to the peer. The value is the host's own reading
/// of a record the client wrote, like the vitals kept beside it
/// (<see cref="PlayerVitalsService"/>).
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed class PlayerPositionService(ILogger<PlayerPositionService> logger)
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, PlayerPosition> lastReported = [];

    /// <summary>
    /// Records a player's position, and reports whether it moved.
    /// </summary>
    /// <param name="remoteAddress">Endpoint the report came from.</param>
    /// <param name="position">Position the report carried.</param>
    /// <returns><c>true</c> when the value differs from the one held.</returns>
    public bool Note(string remoteAddress, PlayerPosition position)
    {
        ArgumentNullException.ThrowIfNull(remoteAddress);
        ArgumentNullException.ThrowIfNull(position);

        lock (gate)
        {
            if (lastReported.TryGetValue(remoteAddress, out var existing))
            {
                if (existing == position)
                {
                    return false;
                }

                lastReported[remoteAddress] = position;
                logger.LogDebug(
                    "{RemoteAddress} moved to {X},{Y},{Z} heading {Heading}, from {PreviousX},{PreviousY},{PreviousZ}",
                    remoteAddress,
                    position.X,
                    position.Y,
                    position.Z,
                    position.Heading,
                    existing.X,
                    existing.Y,
                    existing.Z);
                return true;
            }

            lastReported[remoteAddress] = position;
            logger.LogDebug(
                "{RemoteAddress} first reported a position: {X},{Y},{Z} heading {Heading}",
                remoteAddress,
                position.X,
                position.Y,
                position.Z,
                position.Heading);
            return true;
        }
    }

    /// <summary>
    /// The position a player last reported.
    /// </summary>
    /// <param name="remoteAddress">Endpoint the player is filed under.</param>
    /// <param name="position">The value held, when there is one.</param>
    /// <returns><c>true</c> when the player has reported a position.</returns>
    public bool TryGet(string remoteAddress, out PlayerPosition position)
    {
        lock (gate)
        {
            return lastReported.TryGetValue(remoteAddress, out position!);
        }
    }

    /// <summary>
    /// Drops a player's position, because the player has left the room.
    /// </summary>
    /// <param name="remoteAddress">Endpoint the player was filed under.</param>
    /// <returns><c>true</c> when the player had reported a value.</returns>
    public bool Forget(string remoteAddress)
    {
        lock (gate)
        {
            return lastReported.Remove(remoteAddress);
        }
    }
}
