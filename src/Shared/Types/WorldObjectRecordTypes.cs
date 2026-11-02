namespace Mgo2Server.Shared.Types;

/// <summary>
/// The two streams of the world-object channel: what a dedicated host states about
/// the objects standing on the stage and about the devices placed on it.
/// </summary>
/// <remarks>
/// <para>
/// A world-object record travels under an identifier whose low byte is the stream's:
/// <c>0x1a50</c> / <c>0x1a5a</c> in the first round's generation, <c>0x1250</c> /
/// <c>0x125a</c> in the sibling one, and <c>0x9a50</c> / <c>0x9a5a</c> with the payload
/// class bit (<c>MatchRecordGenerationUtils</c>).
/// The replay console's parser splits them the same way, by low <c>vid = 0x250</c> /
/// <c>0x25a</c> (<c>docs/analysis/replay-console/rpdt.js</c>;
/// <c>docs/protocol/RPDT_REPLAY_RECORDS.md</c>).
/// </para>
/// <para>
/// <b>The stream is what tells two records of the same length apart.</b> An item box and
/// a planted bomb both write a six-byte head, and only the stream says which object the
/// head names: <see cref="Object"/> is the things lying in the world — the two item boxes
/// and whatever a player drops — and <see cref="Device"/> the devices placed by a player,
/// the bomb among them. A host that writes a box under the device stream, or a bomb under
/// the object stream, describes an object the client cannot place.
/// </para>
/// </remarks>
public enum WorldObjectStream : byte
{
    /// <summary>Things lying in the world: the item boxes and dropped items.</summary>
    Object = 0x50,

    /// <summary>Placed devices: the bomb, the magazine trap, the SOP destabilizer.</summary>
    Device = 0x5a,
}

/// <summary>
/// Which record of an event a body is. One event is up to four records written in this
/// order in the same tick, and the six-byte <see cref="Head"/> of five zero bytes is the
/// filler that only says an object is still there.
/// </summary>
/// <remarks>
/// Measured over the twelve replays the replay console's parser was checked against, and
/// against the capture's own opening events, whose four bodies are a head
/// (<c>00 74 01 00 00 00</c>), an item (<c>01 8c 00 01 00 00</c>), a ten-byte position
/// (<c>02 b4 fb 08 07 72 06 00 00 00</c>) and a nine-byte tail
/// (<c>03 00 00 00 21 00 21 00 21</c>).
/// </remarks>
public enum WorldObjectRecordType : byte
{
    /// <summary>The event's head: the event code, the object's number, its id and a slot.</summary>
    Head = 0x00,

    /// <summary>
    /// The object's item: the item byte and four bytes behind it, the fifth of which is
    /// the removal reason when the event is <see cref="WorldObjectEvent.Removed"/>.
    /// </summary>
    Item = 0x01,

    /// <summary>
    /// The object's position: three signed sixteens, x and z in tenths and y in
    /// millimetres, then three zero bytes.
    /// </summary>
    Position = 0x02,

    /// <summary>The event's tail: three zero bytes and the family's own repeated values.</summary>
    Tail = 0x03,
}

/// <summary>
/// The event code in a world-object head: what happened to the object the record names.
/// </summary>
/// <remarks>
/// <para>
/// Read off the replay console's parser, which was measured over twelve replays
/// (<c>rpdt.js</c>, "objects <c>0x1250</c> ... and <c>0x125a</c>"), and off the console's
/// own drawing of the events, which reads the same codes over further replays
/// (<c>docs/analysis/replay-console/app.js</c>, <c>buildItemBoxes</c>).
/// </para>
/// <para>
/// <b>Which field carries the player depends on the event.</b> <see cref="Taken"/> names
/// its taker in the head's slot byte, <see cref="Dropped"/> packs its dropper into the
/// object id as <c>slot &lt;&lt; 3</c>, and <see cref="Removed"/> names its defuser in the
/// item record's second byte. A reader that looks for a slot in the same place on every
/// event reads the wrong player.
/// </para>
/// </remarks>
public enum WorldObjectEvent : byte
{
    /// <summary>
    /// A box set out afresh: its number is 1 or 2 and its item the stream's own. Each
    /// round opens with one per box, which is what tells a round's boxes from the filler.
    /// </summary>
    Placed = 0x74,

    /// <summary>Picked up: the head's slot byte names the player who took it.</summary>
    Taken = 0x83,

    /// <summary>
    /// Let go where the carrier stood: the object id is the dropper's slot shifted left
    /// three, and the position record is his hip.
    /// </summary>
    Dropped = 0x64,

    /// <summary>
    /// A device put down where the player stood: a magazine trap, a planted bomb, a
    /// placed SOP destabilizer. The bomb's plant is within 3 s of the round's own plant
    /// flag in every recorded case.
    /// </summary>
    SetDown = 0x65,

    /// <summary>
    /// Gone, with the reason in the item record's fifth byte
    /// (<see cref="WorldObjectRemovalReason"/>). The bomb's own ending.
    /// </summary>
    Removed = 0xa0,

    /// <summary>
    /// A placed device gone, with the reason in the item record's fifth byte
    /// (<see cref="WorldObjectRemovalReason"/>). Measured over 395 replays, 123 traps in
    /// 25 of them: the record keeps the placer's object id and the device's number.
    /// </summary>
    DeviceRemoved = 0xa5,
}

/// <summary>
/// The reason a removal gives for an object's end, in its item record's fifth byte.
/// </summary>
/// <remarks>
/// <b>Both removal codes carry one, and each stream codes its own endings.</b> A bomb's
/// ending is <see cref="WorldObjectEvent.Removed"/>: <c>1</c> detonated 60.0 s after the
/// plant, <c>3</c> defused by the player named in the item record's second byte. A placed
/// device's is <see cref="WorldObjectEvent.DeviceRemoved"/>: <c>1</c> went off,
/// <c>3</c> destroyed — a player firing with his aim point on it in 26 of 42 measured
/// cases — and <c>0</c> taken away with no effect, either picked up or cleared a few
/// seconds after its owner's death. Measured over 395 replays for the devices, and over
/// the bomb's own records for the other.
/// </remarks>
public enum WorldObjectRemovalReason : byte
{
    /// <summary>Taken away with no effect, as a trap its owner or another player collects.</summary>
    TakenAway = 0x00,

    /// <summary>
    /// It went off: the bomb detonating after its fuse, or a trap whose hit lands in the
    /// same second and is credited to its owner.
    /// </summary>
    WentOff = 0x01,

    /// <summary>
    /// Destroyed by a player: the bomb's defuse, or a trap shot out or caught in a
    /// grenade. The item record's second byte names the defuser on a bomb and nobody on a
    /// trap — the record does not say who ended a device.
    /// </summary>
    Destroyed = 0x03,
}

/// <summary>
/// The item byte a world-object head or item record carries: which item the box holds, or
/// which device was placed.
/// </summary>
/// <remarks>
/// <para>
/// <b>These are not the game's item-pool indices.</b> The pool table the replay console
/// carries names other things at the same offsets — <c>BOOK</c> at <c>0x45</c>,
/// <c>CRITICAL KNIFE</c> at <c>0x71</c>, <c>KARAMBIT</c> at <c>0x8c</c> — so a reader must
/// not resolve a world-object item through it. Two entries do agree, the bomb at
/// <c>0x2e</c> and the base buster at <c>0x44</c>, which is why the table was mistaken
/// for the pool's in the first place.
/// </para>
/// <para>
/// What the rest name is measured from behaviour rather than from a name table: carriers
/// of <see cref="DrumCan"/> spend their time in the drum can's own motion states and
/// carriers of <see cref="Envg"/> fight as normal, over 17 replays, and the magazine and
/// the destabilizer are named by their placement events
/// (<c>rpdt.js</c>, <c>app.js</c>). The client's own module for the boxes is
/// <c>object/itembox_mng.cc</c> and its online half <c>online/other/online_itembox.cc</c>
/// (<c>itemname</c>, <c>second_itemname</c>), both named in the native port's
/// strings — the table these bytes belong to.
/// </para>
/// </remarks>
public enum WorldObjectItem : byte
{
    /// <summary>The bomb a Bomb round plays around; <c>0x2e</c>, and the pool agrees.</summary>
    TimeBomb = 0x2e,

    /// <summary>The SOP destabilizer Base's attackers plant; <c>0x44</c>, the pool's Base Buster.</summary>
    BaseBuster = 0x44,

    /// <summary>The magazine set down as a trap; the pool's <c>BOOK</c> slot.</summary>
    Magazine = 0x45,

    /// <summary>The drum can, a box a player hides in; <c>0x71</c>.</summary>
    DrumCan = 0x71,

    /// <summary>The ENVG, the small box of Team Sneaking's defenders; <c>0x8c</c>.</summary>
    Envg = 0x8c,
}
