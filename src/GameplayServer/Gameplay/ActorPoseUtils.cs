namespace Mgo2Server.GameplayServer.Gameplay;

/// <summary>
/// Reads and writes the stance and pose an actor's position record carries.
/// </summary>
/// <remarks>
/// <para>
/// <b>The head byte is a stance, not an identifier.</b> Bits five to seven name
/// the stance (<see cref="ActorStance"/>) and bit zero says the body is in play;
/// the six-byte aim body that shares the <c>0x57</c> opening with a CQC hold is
/// why a decoder must never read a bare <c>0x57</c> as a stance. Only a
/// position body is decoded here.
/// </para>
/// <para>
/// The heads are measured across <c>.pcapngs/actions/</c>: standing
/// <c>21 00</c>, walking <c>41 81</c>, crouching <c>61 00</c>, prone
/// <c>81 02</c>, crawling <c>A1 28</c>, knocked down <c>C1 81</c>, recovering
/// <c>E1 00</c> and lying <c>01 1C</c>. The animation field's own values are
/// <see cref="ActorAnimation"/>, measured on the capture that names each one.
/// </para>
/// </remarks>
public static class ActorPoseUtils
{
    /// <summary>Bit of the head byte that says the body is in play.</summary>
    public const byte ActiveBit = 0x01;

    /// <summary>Shift from a head byte to its stance.</summary>
    public const int StanceShift = 5;

    /// <summary>Bit of the pose byte that says the actor is armed.</summary>
    public const byte ArmedBit = 0x80;

    /// <summary>Bit of the pose byte that says the actor is acting.</summary>
    public const byte ActingBit = 0x01;

    /// <summary>Bits of the pose byte that carry the upper-body animation.</summary>
    public const byte AnimationMask = 0x7e;

    /// <summary>Length of the shortest body a pose can be read from.</summary>
    public const int MinimumBodyLength = 2;

    /// <summary>
    /// Reads the pose out of a position body.
    /// </summary>
    /// <param name="body">The position body, at least two bytes.</param>
    /// <returns>The pose, or <c>null</c> for a body too short to carry one.</returns>
    public static ActorPose? Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < MinimumBodyLength)
        {
            return null;
        }

        return new ActorPose(
            (ActorStance)(body[0] >> StanceShift),
            (body[0] & ActiveBit) != 0,
            (body[1] & ArmedBit) != 0,
            (body[1] & ActingBit) != 0,
            (ActorAnimation)(body[1] & AnimationMask));
    }

    /// <summary>Whether a stance is one the body is off its feet in.</summary>
    /// <param name="stance">Stance to test.</param>
    /// <returns><c>true</c> for lying and knocked down.</returns>
    public static bool IsDown(ActorStance stance) =>
        stance is ActorStance.Lying or ActorStance.KnockedDown;

    /// <summary>Whether a stance is one whose head is below a standing actor's.</summary>
    /// <param name="stance">Stance to test.</param>
    /// <returns><c>true</c> for crouching, prone, crawling and lying.</returns>
    public static bool IsLowered(ActorStance stance) =>
        stance is ActorStance.Crouching or ActorStance.Prone or ActorStance.Crawling
            or ActorStance.Lying;

    /// <summary>Writes the head byte for a stance.</summary>
    /// <param name="stance">Stance to write.</param>
    /// <param name="active">Whether the body is in play.</param>
    /// <returns>The head byte.</returns>
    public static byte HeadOf(ActorStance stance, bool active = true) =>
        (byte)(((byte)stance << StanceShift) | (active ? ActiveBit : 0));

    /// <summary>Writes the pose byte from its two flags and its animation field.</summary>
    /// <param name="armed">Whether the actor is armed.</param>
    /// <param name="acting">Whether the actor is acting.</param>
    /// <param name="animation">The upper-body animation, masked to its bits.</param>
    /// <returns>The pose byte.</returns>
    public static byte FlagsOf(
        bool armed,
        bool acting,
        ActorAnimation animation = ActorAnimation.None) =>
        (byte)((armed ? ArmedBit : 0)
            | (acting ? ActingBit : 0)
            | ((byte)animation & AnimationMask));
}
