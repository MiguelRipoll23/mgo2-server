namespace Mgo2Server.Shared.Utils;

/// <summary>A parsed <c>0x1001</c> player-profile record.</summary>
/// <param name="Version">Record version byte.</param>
/// <param name="RecordLength">
/// Byte at offset 1 read back raw. It is the record's own length less seven, not
/// a record kind; <see cref="PlayerProfileRecordUtility.IsRosterRecord"/> is the
/// test over it.
/// </param>
/// <param name="RosterBaseField">Raw field at offset 4: 0xe2 plus the index, or zero for the host.</param>
/// <param name="RosterIndex">
/// Position of the player in the room roster, recovered from that field. It is
/// signed because the host's own slot is <c>-1</c>, written as a plain zero
/// rather than as one below the first player's.
    /// </param>
    /// <param name="sessionSlot">
    /// The player's slot in the host's 24-entry network player table (offsets 0x05
    /// and 0x07 of the roster entry), the byte the replay console's <c>rpdt.js</c>
    /// calls <c>slot</c>. It varies per player without following roster order and
    /// matches the member bytes in the <c>0x0261</c>/<c>0x0a61</c>
    /// rules-roster-shaped records, so it is not an index. The client binary names
    /// the field via the table at <c>0x26d5a8</c>; how the host selects it is
    /// unresolved. The live capture calls these "member bytes" and confirms they
    /// are slots in a 24-entry table.
    /// </param>
/// <param name="HasClanName">Whether a clan name was read alongside the name.</param>
/// <param name="CharacterId">The character id at offset 8, little-endian.</param>
/// <param name="AddressData">
/// The public and private IPv4 endpoint pairs, copied from the character's
/// handshake and carried as twelve bytes in the roster record.
/// </param>
/// <param name="Name">Character name.</param>
/// <param name="ClanName">Clan name.</param>
    public sealed record PlayerProfileRecord(
        byte Version,
        byte RecordLength,
        byte RosterBaseField,
        sbyte RosterIndex,
        byte SessionSlot,
        bool HasClanName,
        int CharacterId,
        byte[] AddressData,
        string Name,
        string ClanName)
    {
        /// <summary>
        /// The appearance block a join request carries, empty on a roster entry.
        /// </summary>
        /// <remarks>
        /// <b>It is the tail the client plays as, and the host re-broadcasts it.</b>
        /// <c>briefing-then-solo-game</c>'s 140-byte join request holds it at
        /// <c>0x10</c>, and the loadout the host writes for that player carries the same
        /// bytes as its trailing block, so a character whose saved row states no colours
        /// is still announced with the face it plays as
        /// (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §8.4). A roster entry puts its
        /// endpoint pairs at that offset and announces nothing here.
        /// </remarks>
        public byte[] AnnouncedAppearance { get; init; } = [];

        /// <summary>
        /// Clan id the record names, or zero when the player is in no clan. It is not
        /// the emblem: the client fetches that over TCP by this id.
        /// </summary>
        public int ClanIdentifier { get; init; }
    }