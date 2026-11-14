namespace Mgo2Server.GameplayServer.Gameplay;

/// <summary>
/// The stance an actor's position record states in the three bits above its
/// active bit.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured over <c>.pcapngs/actions/</c>, where every one of the seven
/// values appears.</b> The head byte is <c>stance &lt;&lt; 5</c> with bit zero
/// set on a body in play: <c>0x21</c> standing, <c>0x41</c> walking,
/// <c>0x61</c> crouching, <c>0x81</c> prone, <c>0xA1</c> crawling, <c>0xC1</c>
/// knocked down, <c>0xE1</c> recovering and <c>0x01</c> lying. A spawn
/// placement writes <c>0x20</c>, which is <see cref="Standing"/> with that bit
/// clear.
/// </para>
/// <para>
/// The thirty-two-byte down body keeps the same head and pose byte at <c>+0</c>
/// and <c>+1</c>, then carries a frame byte and <c>0x0B</c> before the counter at
/// <c>+4</c>. Deaths write <c>22 1A</c> or <c>21 1A</c> with vitals at zero; a
/// living knock-out writes <c>A1 18</c> with health intact. It is written by
/// <c>BotPositionBodiesUtils.BuildDown</c>; only its first two bytes are a pose,
/// which <see cref="ActorPoseUtils.Decode"/> already reads.
/// </para>
/// </remarks>
public enum ActorStance : byte
{
    /// <summary>
    /// On the floor with no power of its own: the knife stun, the sleep device
    /// and the beat before a body gets up all write <c>0x01</c>.
    /// </summary>
    Lying,

    /// <summary>On its feet and still: <c>0x21</c>.</summary>
    Standing,

    /// <summary>On its feet and moving: <c>0x41</c>.</summary>
    Walking,

    /// <summary>Squatting: <c>0x61</c>.</summary>
    Crouching,

    /// <summary>
    /// Flat on its front but moving or aiming: <c>0x81</c>. The forward roll
    /// writes <c>81 81</c> for nine records and comes up on <c>21 01</c>; the
    /// cardboard box's crawl writes the crawling head instead.
    /// </summary>
    Prone,

    /// <summary>Dragging itself along the floor: <c>0xA1</c>.</summary>
    Crawling,

    /// <summary>
    /// Put down by a blow: <c>0xC1</c>, the capture's <c>c1 81</c> head. It
    /// lasts as long as the hit reaction or the stun behind it. The evasive
    /// back-step and side flip write it too — <c>c1 01</c> for one beat, then
    /// <c>c1 81</c> for about 1.4 s — so the stance alone does not say a body was
    /// hit; the damage record does.
    /// </summary>
    KnockedDown,

    /// <summary>Getting back up: <c>0xE1</c>.</summary>
    Recovering,
}
