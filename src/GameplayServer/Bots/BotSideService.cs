using Mgo2Server.GameplayServer.Rooms;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The slots the room's bots hold and the sides they hold them on.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a table and not the population.</b> The lobby-slot arrays have to name
/// a bot's slot and side, and the arrays are read by services the population
/// itself depends on — perception reads the arrays to tell a teammate from an
/// enemy. Making the reader depend on the population would close that into a
/// cycle, so the two facts the arrays need live in this small table, the population
/// writes them and the arrays read them.
/// </para>
/// <para>
/// It holds nothing else: no name, no role, no decision. A reader that wants to
/// know who a bot is reads the roster, which is where the bot's entry is.
/// </para>
/// </remarks>
public sealed class BotSideService
{
    private readonly Dictionary<byte, short?> sides = [];
    private readonly Lock gate = new();

    /// <summary>States the side a bot's slot holds.</summary>
    /// <param name="sessionSlot">Key in the room's 24-entry player table.</param>
    /// <param name="side">Side the bot stands on, or <c>null</c> while none is resolved.</param>
    public void Set(byte sessionSlot, short? side)
    {
        lock (gate)
        {
            sides[sessionSlot] = side;
        }
    }

    /// <summary>Forgets a slot, because its bot is gone.</summary>
    /// <param name="sessionSlot">Key the bot held.</param>
    public void Remove(byte sessionSlot)
    {
        lock (gate)
        {
            sides.Remove(sessionSlot);
        }
    }

    /// <summary>Drops every slot and side, because the room has emptied.</summary>
    public void Reset()
    {
        lock (gate)
        {
            sides.Clear();
        }
    }

    /// <summary>The slots and sides, as the lobby-slot arrays name them.</summary>
    public IReadOnlyList<LobbyTeamSlot> TeamSlots
    {
        get
        {
            lock (gate)
            {
                return
                [
                    .. sides
                        .OrderBy(entry => entry.Key)
                        .Select(entry => new LobbyTeamSlot(entry.Key, entry.Value)),
                ];
            }
        }
    }
}
