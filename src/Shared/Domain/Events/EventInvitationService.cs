namespace Mgo2Server.Shared.Domain.Events;

/// <summary>One pending or accepted team invitation.</summary>
public sealed class EventInvitation
{
    /// <summary>Identifier the target answers with.</summary>
    public int Identifier { get; init; }

    /// <summary>Character that sent the invitation.</summary>
    public int LeaderCharacterIdentifier { get; init; }

    /// <summary>Character the invitation is addressed to.</summary>
    public int TargetCharacterIdentifier { get; init; }

    /// <summary>Team the invitation joins.</summary>
    public int TeamIdentifier { get; init; }

    /// <summary>Lobby the invitation was sent in.</summary>
    public int LobbyIdentifier { get; init; }

    /// <summary>Mode the invitation was sent with.</summary>
    public int Mode { get; init; }

    /// <summary>Name of the inviting character.</summary>
    public string LeaderName { get; init; } = string.Empty;

    /// <summary>Name of the invited character.</summary>
    public string TargetName { get; init; } = string.Empty;

    /// <summary>Second the invitation was created at.</summary>
    public long CreatedAt { get; init; }

    /// <summary>Second the invitation was accepted at, when it has been.</summary>
    public long AcceptedAt { get; set; }
}

/// <summary>
/// Owns the short-lived team invitations. They are instance state rather than a
/// table because every one of them names a live connection: a session exists in
/// exactly one lobby process, so an invitation has no meaning anywhere else, and
/// its whole lifetime is seconds. What is durable — the team and its roster —
/// still lives in the database.
/// <para>
/// The state is an instance field guarded by one lock, the same shape the
/// automatch queue uses, so two requests cannot create the same invitation twice
/// or accept one after it expired.
/// </para>
/// </summary>
public sealed class EventInvitationService
{
    /// <summary>Mode byte a Survival invitation carries.</summary>
    public const int SurvivalMode = 4;

    /// <summary>Answer byte meaning the target accepted.</summary>
    public const int ChoiceYes = 2;

    /// <summary>Answer byte meaning the target declined.</summary>
    public const int ChoiceNo = 4;

    /// <summary>Most targets one invitation request may name.</summary>
    public const int MaximumTargets = 3;

    /// <summary>Seconds a pending invitation stays answerable.</summary>
    public const long PendingLifetimeSeconds = 90;

    /// <summary>Seconds an accepted invitation reserves the target's slot.</summary>
    public const long AcceptedLifetimeSeconds = 300;

    private readonly Lock gate = new();
    private readonly Dictionary<int, EventInvitation> pending = [];
    private readonly Dictionary<int, EventInvitation> acceptedByTarget = [];
    private int nextIdentifier = 1;

    /// <summary>Creates a pending invitation, or returns null when it cannot exist.</summary>
    /// <param name="leaderCharacterIdentifier">Character inviting.</param>
    /// <param name="leaderName">Name of the inviting character.</param>
    /// <param name="targetCharacterIdentifier">Character invited.</param>
    /// <param name="targetName">Name of the invited character.</param>
    /// <param name="teamIdentifier">Team the invitation joins.</param>
    /// <param name="lobbyIdentifier">Lobby the invitation was sent in.</param>
    /// <param name="mode">Mode byte.</param>
    /// <param name="nowSeconds">Current second.</param>
    /// <returns>The invitation, or null when one already exists or the roster is full.</returns>
    public EventInvitation? Create(
        int leaderCharacterIdentifier,
        string leaderName,
        int targetCharacterIdentifier,
        string targetName,
        int teamIdentifier,
        int lobbyIdentifier,
        int mode,
        long nowSeconds)
    {
        if (leaderCharacterIdentifier == targetCharacterIdentifier)
        {
            return null;
        }

        lock (gate)
        {
            PurgeExpiredLocked(nowSeconds);

            if (acceptedByTarget.ContainsKey(targetCharacterIdentifier)
                || pending.Values.Any(existing =>
                    existing.LeaderCharacterIdentifier == leaderCharacterIdentifier
                    && existing.TargetCharacterIdentifier == targetCharacterIdentifier))
            {
                return null;
            }

            var invitation = new EventInvitation
            {
                Identifier = NextIdentifierLocked(),
                LeaderCharacterIdentifier = leaderCharacterIdentifier,
                TargetCharacterIdentifier = targetCharacterIdentifier,
                TeamIdentifier = teamIdentifier,
                LobbyIdentifier = lobbyIdentifier,
                Mode = mode,
                LeaderName = leaderName,
                TargetName = targetName,
                CreatedAt = nowSeconds,
            };
            pending[invitation.Identifier] = invitation;
            return invitation;
        }
    }

    /// <summary>Removes and returns the invitation a target is answering.</summary>
    /// <param name="identifier">Invitation identifier.</param>
    /// <param name="targetCharacterIdentifier">Character that must own it.</param>
    /// <param name="nowSeconds">Current second.</param>
    public EventInvitation? TakePending(int identifier, int targetCharacterIdentifier, long nowSeconds)
    {
        lock (gate)
        {
            PurgeExpiredLocked(nowSeconds);

            if (!pending.TryGetValue(identifier, out var invitation)
                || invitation.TargetCharacterIdentifier != targetCharacterIdentifier)
            {
                return null;
            }

            pending.Remove(identifier);
            return invitation;
        }
    }

    /// <summary>Marks an invitation accepted, reserving the target's slot.</summary>
    /// <param name="invitation">Invitation that was accepted.</param>
    /// <param name="nowSeconds">Current second.</param>
    public void Accept(EventInvitation invitation, long nowSeconds)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        lock (gate)
        {
            // An accepted invitation is no longer answerable, so it leaves the
            // pending set even when the caller did not take it first. Otherwise it
            // would be counted twice by CountReservedForTeam.
            pending.Remove(invitation.Identifier);
            invitation.AcceptedAt = nowSeconds;
            acceptedByTarget[invitation.TargetCharacterIdentifier] = invitation;
        }
    }

    /// <summary>Returns the accepted invitation holding a target's slot, when one does.</summary>
    /// <param name="targetCharacterIdentifier">Character to look for.</param>
    /// <param name="nowSeconds">Current second.</param>
    public EventInvitation? FindAccepted(int targetCharacterIdentifier, long nowSeconds)
    {
        lock (gate)
        {
            PurgeExpiredLocked(nowSeconds);
            return acceptedByTarget.GetValueOrDefault(targetCharacterIdentifier);
        }
    }

    /// <summary>Removes the accepted reservation of one target.</summary>
    /// <param name="targetCharacterIdentifier">Character whose reservation is released.</param>
    public void ClearAccepted(int targetCharacterIdentifier)
    {
        lock (gate)
        {
            acceptedByTarget.Remove(targetCharacterIdentifier);
        }
    }

    /// <summary>Cancels a target's other pending invitations when one is answered.</summary>
    /// <param name="targetCharacterIdentifier">Character that answered.</param>
    /// <param name="answeredIdentifier">Invitation that was answered.</param>
    /// <param name="nowSeconds">Current second.</param>
    /// <returns>The cancelled invitations, so the sender can be told.</returns>
    public List<EventInvitation> CancelOthersForTarget(
        int targetCharacterIdentifier,
        int answeredIdentifier,
        long nowSeconds)
    {
        lock (gate)
        {
            PurgeExpiredLocked(nowSeconds);

            var cancelled = new List<EventInvitation>();
            foreach (var candidate in pending.Values.ToList())
            {
                if (candidate.TargetCharacterIdentifier == targetCharacterIdentifier
                    && candidate.Identifier != answeredIdentifier)
                {
                    pending.Remove(candidate.Identifier);
                    cancelled.Add(candidate);
                }
            }

            return cancelled;
        }
    }

    /// <summary>Counts pending and accepted invitations for one team.</summary>
    /// <param name="teamIdentifier">Team to count.</param>
    /// <param name="nowSeconds">Current second.</param>
    /// <returns>How many slots invitations already hold.</returns>
    public int CountReservedForTeam(int teamIdentifier, long nowSeconds)
    {
        lock (gate)
        {
            PurgeExpiredLocked(nowSeconds);

            var pendingCount = pending.Values.Count(invitation => invitation.TeamIdentifier == teamIdentifier);
            var acceptedCount = acceptedByTarget.Values.Count(invitation => invitation.TeamIdentifier == teamIdentifier);
            return pendingCount + acceptedCount;
        }
    }

    /// <summary>Removes every invitation belonging to one team.</summary>
    /// <param name="teamIdentifier">Team that ended.</param>
    public void RemoveForTeam(int teamIdentifier)
    {
        lock (gate)
        {
            foreach (var identifier in pending
                         .Where(pair => pair.Value.TeamIdentifier == teamIdentifier)
                         .Select(pair => pair.Key)
                         .ToList())
            {
                pending.Remove(identifier);
            }

            foreach (var target in acceptedByTarget
                         .Where(pair => pair.Value.TeamIdentifier == teamIdentifier)
                         .Select(pair => pair.Key)
                         .ToList())
            {
                acceptedByTarget.Remove(target);
            }
        }
    }

    /// <summary>Removes every invitation a character sent or received.</summary>
    /// <param name="characterIdentifier">Character that left.</param>
    public void RemoveForCharacter(int characterIdentifier)
    {
        lock (gate)
        {
            foreach (var identifier in pending
                         .Where(pair => pair.Value.LeaderCharacterIdentifier == characterIdentifier
                             || pair.Value.TargetCharacterIdentifier == characterIdentifier)
                         .Select(pair => pair.Key)
                         .ToList())
            {
                pending.Remove(identifier);
            }

            acceptedByTarget.Remove(characterIdentifier);
        }
    }

    private void PurgeExpiredLocked(long nowSeconds)
    {
        foreach (var identifier in pending
                     .Where(pair => nowSeconds - pair.Value.CreatedAt > PendingLifetimeSeconds)
                     .Select(pair => pair.Key)
                     .ToList())
        {
            pending.Remove(identifier);
        }

        foreach (var target in acceptedByTarget
                     .Where(pair => nowSeconds - Math.Max(pair.Value.AcceptedAt, pair.Value.CreatedAt)
                         > AcceptedLifetimeSeconds)
                     .Select(pair => pair.Key)
                     .ToList())
        {
            acceptedByTarget.Remove(target);
        }
    }

    private int NextIdentifierLocked()
    {
        var identifier = nextIdentifier++;
        if (identifier > 0)
        {
            return identifier;
        }

        nextIdentifier = 2;
        return 1;
    }
}
