using Mgo2Server.GameLobbyServer.Commands.Game.Rooms;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the rules a pushed host-settings block has to pass. What the push stores
/// is what a later create-room request builds a room out of, so a block that fails
/// a rule has to be refused before it is kept rather than after it is used.
/// </summary>
[Trait("Category", "GameLobby")]
public sealed class HostSettingsValidationTests
{
    [Fact]
    public void A_block_the_screen_could_have_produced_is_stored()
    {
        Assert.True(HostSettingsValidationUtils.IsAcceptable(Settings()));
    }

    [Fact]
    public void A_reserved_role_without_the_dedicated_flag_is_refused()
    {
        // The name would sit in the lobby as a room named for a role it may never
        // hold, so the room is refused before it can be created from the block.
        Assert.False(HostSettingsValidationUtils.IsAcceptable(Settings(name: "SURVIVAL_HOST")));
        Assert.False(HostSettingsValidationUtils.IsAcceptable(Settings(name: "tournament_host")));

        // The flag alone is enough, and a room named for nothing needs neither.
        Assert.True(HostSettingsValidationUtils.IsAcceptable(
            Settings(name: "SURVIVAL_HOST", dedicated: true)));
    }

    [Fact]
    public void A_mode_the_screen_cannot_name_is_refused()
    {
        Assert.False(HostSettingsValidationUtils.IsAcceptable(Settings(lobbySubtype: 0)));
        Assert.False(HostSettingsValidationUtils.IsAcceptable(Settings(lobbySubtype: 10)));
        Assert.True(HostSettingsValidationUtils.IsAcceptable(Settings(lobbySubtype: 1)));
        Assert.True(HostSettingsValidationUtils.IsAcceptable(Settings(lobbySubtype: 9)));
    }

    [Fact]
    public void A_player_count_the_room_cannot_hold_is_refused()
    {
        Assert.False(HostSettingsValidationUtils.IsAcceptable(Settings(maximumPlayers: 0)));
        Assert.False(HostSettingsValidationUtils.IsAcceptable(Settings(maximumPlayers: 18)));
        Assert.True(HostSettingsValidationUtils.IsAcceptable(Settings(maximumPlayers: 1)));

        // A dedicated host consumes the seventeenth slot of a full 8-vs-8 match.
        Assert.True(HostSettingsValidationUtils.IsAcceptable(Settings(maximumPlayers: 17)));
    }

    [Fact]
    public void A_room_with_nothing_to_play_is_refused()
    {
        Assert.False(HostSettingsValidationUtils.IsAcceptable(Settings(withRotation: false)));
    }

    /// <summary>A block as the settings screen would push one.</summary>
    /// <param name="name">Room name the block carries.</param>
    /// <param name="dedicated">Whether the block says the room is dedicated.</param>
    /// <param name="lobbySubtype">Mode the block names.</param>
    /// <param name="maximumPlayers">Players the block asks the room to hold.</param>
    /// <param name="withRotation">Whether the block names a game.</param>
    private static CharacterHostSettings Settings(
        string name = "MY ROOM",
        bool dedicated = false,
        short lobbySubtype = 1,
        short maximumPlayers = 16,
        bool withRotation = true) =>
        new()
        {
            Name = name,
            Dedicated = dedicated,
            SettingsLobbySubtype = lobbySubtype,
            MaxPlayers = maximumPlayers,
            RotationRules = [withRotation ? (short)4 : (short)0, .. new short[15]],
            RotationMaps = [withRotation ? (short)1 : (short)0, .. new short[15]],
            RotationFlags = new short[16],
        };
}
