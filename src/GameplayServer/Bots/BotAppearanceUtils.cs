using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The looks this host's bots play as: appearance blocks measured from the
/// recorded dedicated host's own bot loadouts.
/// </summary>
/// <remarks>
/// <para>
/// <b>They are captured bytes, not a generated body.</b> <c>pcapng1</c> sends ten
/// loadouts for its ten bots at t+488.7, and the trailing
/// <see cref="Rooms.LoadoutFramesUtils.AppearanceLength"/> bytes of each are that
/// bot's face, gear and colours in the network block's order. One block per bot is
/// stored here, so a bot is announced with a body the game wrote rather than with
/// the reference host's single face repeated for the whole population.
/// </para>
/// <para>
/// The gender travels in the same block (field 12), which is why no separate table
/// exists: a bot whose block says female is created with a female appearance row,
/// and the row is what the settings burst and the lobby read.
/// </para>
/// <para>
/// The blocks are the bots' own and not the players': they are the records the
/// reference wrote for slots its roster names <c>BOT-01</c>…<c>BOT-10</c>, and no
/// player entry in that capture holds one of them.
/// </para>
/// </remarks>
public static class BotAppearanceUtils
{
    /// <summary>
    /// Number of distinct looks, which is how many bots a room cycles through
    /// before a look repeats.
    /// </summary>
    public static int Count => Blocks.Length;

    /// <summary>
    /// The looks, in the capture's own order, each the trailing twenty-three bytes
    /// of one bot loadout: head, chest, hand, waist, foot, accessory 1,
    /// accessory 2, the six colours, gender, face, upper, lower, face paint, the
    /// two garment colours, voice, pitch and the two accessory colours.
    /// </summary>
    private static readonly string[] BlockHex =
    [
        "1cc1a15e40666d000d00140100010b16001606080e0005",
        "23c32f64b0667215170005080106101600160510060000",
        "224430573d66f2190000170600060f160006090b15000f",
        "1d4731563d666602010000040005121600111209030000",
        "2ac2a1593b66661b0800190000050d160013040c190000",
        "25c13356406c66031100000601050d1600180e120e0000",
        "254fa1623d6f66190b0001020103111600160116190700",
        "1c44315640f46a000000000500020e1600070f0e160001",
        "2a4d31583c66f3150400170a00000d160007080d060003",
        "844435563967660b0000000d01050b1600040b12150200",
    ];

    private static readonly byte[][] Blocks = [.. BlockHex.Select(Convert.FromHexString)];

    /// <summary>
    /// Writes one bot's look into an appearance row.
    /// </summary>
    /// <remarks>
    /// The bot's number chooses the block, counting from one: the first bot of the
    /// process plays as the first captured look, and the population cycles through
    /// them in order. The number is the bot's identity number and not its room
    /// slot, so a bot keeps its face when the room gives it another slot.
    /// </remarks>
    /// <param name="appearance">The appearance row to write.</param>
    /// <param name="number">The bot's identity number, counting from one.</param>
    public static void Apply(CharacterAppearance appearance, int number)
    {
        ArgumentNullException.ThrowIfNull(appearance);

        var block = BlockFor(number);
        appearance.Head = block[0];
        appearance.Chest = block[1];
        appearance.Hands = block[2];
        appearance.Waist = block[3];
        appearance.Feet = block[4];
        appearance.Accessory1 = block[5];
        appearance.Accessory2 = block[6];
        appearance.HeadColor = block[7];
        appearance.ChestColor = block[8];
        appearance.HandsColor = block[9];
        appearance.WaistColor = block[10];
        appearance.FeetColor = block[11];
        appearance.Gender = block[12];
        appearance.Face = block[13];
        appearance.Upper = block[14];
        appearance.Lower = block[15];
        appearance.FacePaint = block[16];
        appearance.UpperColor = block[17];
        appearance.LowerColor = block[18];
        appearance.Voice = block[19];
        appearance.Pitch = block[20];
        appearance.Accessory1Color = block[21];
        appearance.Accessory2Color = block[22];
    }

    /// <summary>The block a bot's number is written with.</summary>
    /// <param name="number">The bot's identity number, counting from one.</param>
    /// <returns>The appearance block.</returns>
    private static byte[] BlockFor(int number) =>
        Blocks[(int)((uint)(number - 1) % Blocks.Length)];
}
