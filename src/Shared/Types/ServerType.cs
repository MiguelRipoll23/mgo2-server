namespace Mgo2Server.Shared.Types;

/// <summary>Role of a TCP server, which selects the command set it dispatches.</summary>
public enum ServerType
{
    /// <summary>First connection point: delivers the lobby list and the news.</summary>
    Gate,

    /// <summary>Character creation, deletion, selection and session validation.</summary>
    Account,

    /// <summary>Gameplay lobby: room management, player sessions and match statistics.</summary>
    GameplayLobby,
}
