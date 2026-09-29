using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>
/// The in-memory half of the character store: the test characters a simulation
/// fills a team with, mixed with the real rows so a lookup answers for either.
/// <para>
/// A test character is not a row. It has an identifier from the test range, a
/// name the marker prefix makes recognisable, and nothing else: no account, no
/// appearance, nothing anybody could sign in as. That is enough for a roster and
/// a snapshot, which carry the name and the identifier a screen renders.
/// </para>
/// <para>
/// The state is instance state guarded by one lock, the same shape the other
/// in-memory stores use, and it is gone when the process is. Every run resets
/// it first, so a second test does not build on the first one's characters.
/// </para>
/// </summary>
public sealed class CharacterMemoryService
{
    /// <summary>Experience a test character is shown with, roughly a mid-level rank.</summary>
    public const int TestExperience = 18_000;

    private readonly Lock gate = new();
    private readonly Dictionary<int, Character> characters = [];
    private int nextIdentifier = TestIdentifierUtils.FirstIdentifier;
    private int nextNumber = 1;

    /// <summary>Creates the next numbered test character.</summary>
    /// <returns>The character, held here under its test identifier.</returns>
    public Character Create()
    {
        lock (gate)
        {
            var character = new Character
            {
                // The identifier comes from the test range, so it can never
                // collide with a row's and a reader can tell the two apart.
                Identifier = nextIdentifier++,
                Name = TestPlayerNameUtils.ComposeName(nextNumber++),
                Experience = TestExperience,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            characters[character.Identifier] = character;
            return character;
        }
    }

    /// <summary>Returns one test character, when it is held here.</summary>
    /// <param name="characterIdentifier">Character to find.</param>
    public Character? Find(int characterIdentifier)
    {
        lock (gate)
        {
            return characters.GetValueOrDefault(characterIdentifier);
        }
    }

    /// <summary>Returns the test character one name belongs to, when it is held here.</summary>
    /// <param name="name">Name to look for.</param>
    public Character? FindByName(string name)
    {
        lock (gate)
        {
            return characters.Values.FirstOrDefault(character =>
                string.Equals(character.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Lists every test character held here, oldest first.</summary>
    public IReadOnlyList<Character> List()
    {
        lock (gate)
        {
            return [.. characters.Values.OrderBy(character => character.Identifier)];
        }
    }

    /// <summary>Whether an identifier names a test character held here.</summary>
    /// <param name="characterIdentifier">Identifier to test.</param>
    public bool Holds(int characterIdentifier)
    {
        lock (gate)
        {
            return characters.ContainsKey(characterIdentifier);
        }
    }

    /// <summary>Forgets every test character. Each run begins here.</summary>
    public void Reset()
    {
        lock (gate)
        {
            characters.Clear();

            // The numbers are not rewound, for the same reason the team store's
            // are not: a match row written for a test character outlives this
            // process's memory of it, and handing its identifier to a new
            // character would let that row pair it as if it had never left.
        }
    }
}
