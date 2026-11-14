namespace Mgo2Server.GameplayServer.Gameplay;

/// <summary>
/// The pose a position body carries: its stance, whether the actor is in play,
/// and the second byte's armed and acting bits with the animation field between
/// them.
/// </summary>
/// <remarks>
/// <b>The second byte is a pose byte before it is a flag byte.</b> <c>0x00</c>
/// is a body at ease, <c>0x81</c> one that is armed and acting — the capture's
/// walking, aiming and firing heads — and the bits between them are the
/// upper-body animation the client draws for a blow, a throw or a box being
/// carried (<see cref="ActorAnimation"/>). This record keeps that field as
/// <see cref="Animation"/> rather than pretending its spare bits are all flags.
/// </remarks>
/// <param name="Stance">The stance the head byte names.</param>
/// <param name="Active">Whether the head's active bit is set.</param>
/// <param name="Armed">Whether the second byte's <c>0x80</c> bit is set.</param>
/// <param name="Acting">Whether the second byte's <c>0x01</c> bit is set.</param>
/// <param name="Animation">
/// The second byte's bits <c>0x02</c> to <c>0x40</c>; an unnamed value keeps its
/// raw byte in this field.
/// </param>
public readonly record struct ActorPose(
    ActorStance Stance,
    bool Active,
    bool Armed,
    bool Acting,
    ActorAnimation Animation);
