namespace Mgo2Server.Shared.Domain.Automatch;

/// <summary>
/// The matchmaking half of the automatch service: it reaps the queue, decides
/// which searchers form a group, and releases a group once its host has created
/// the game. It shares the queue state of the service it is declared in.
/// </summary>
public sealed partial class AutomatchService
{
    /// <summary>Drops dead or over-waited searchers.</summary>
    private void Reap()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (characterIdentifier, searcher) in searchers.ToList())
        {
            if (searcher.Active && now - searcher.JoinedAt <= AutomatchConstants.MaximumWait)
            {
                continue;
            }

            searchers.Remove(characterIdentifier);
            if (searcher.Match is { GameIdentifier: 0 } match &&
                match.HostCharacterIdentifier == characterIdentifier)
            {
                match.HostLost = true;
            }
        }
    }

    /// <summary>Whether two searchers' level windows share at least one level.</summary>
    private bool WindowsOverlap(Searcher first, Searcher second)
    {
        var bandSum = policy.BandAfter(first.Waited) + policy.BandAfter(second.Waited);
        return Math.Abs(first.Level - second.Level) <= bandSum;
    }

    /// <summary>Grouped by mode first, relaxed with time — wildcards fit everyone.</summary>
    private bool ModesCompatible(Searcher first, Searcher second)
    {
        if (first.RuleFilter == AutomatchConstants.AnyRule || second.RuleFilter == AutomatchConstants.AnyRule)
        {
            return true;
        }

        if (first.RuleFilter == second.RuleFilter)
        {
            return true;
        }

        return policy.AcceptsAnyModeAfter(first.Waited) || policy.AcceptsAnyModeAfter(second.Waited);
    }

    /// <summary>
    /// Elects a host for every group large enough to play. Groups form across
    /// rule filters; the longest-waiting searcher anchors the group.
    /// </summary>
    private void FormMatches()
    {
        var queued = searchers.Values
            .Where(searcher => searcher.State == AutomatchState.Searching && searcher.Active)
            .OrderBy(searcher => searcher.JoinedAt)
            .ToList();

        if (queued.Count < policy.MinimumPlayers)
        {
            return;
        }

        var anchor = queued[0];
        var requirement = policy.RequiredPlayersAfter(anchor.Waited);
        var waiting = queued
            .Where(searcher => WindowsOverlap(anchor, searcher) && ModesCompatible(anchor, searcher))
            .ToList();

        if (waiting.Count < requirement)
        {
            return;
        }

        // Longest-waiting first, skipping anyone who already hosts here.
        var pendingHosts = pending.Select(match => match.HostCharacterIdentifier).ToHashSet();
        var host = waiting.FirstOrDefault(searcher => !pendingHosts.Contains(searcher.CharacterIdentifier));
        if (host is null)
        {
            return;
        }

        // Every distinct mode the group asked for, in join order; wildcards
        // contribute nothing. An all-wildcard group picks one at random.
        var rules = new List<int>();
        foreach (var searcher in waiting)
        {
            if (searcher.RuleFilter != AutomatchConstants.AnyRule && !rules.Contains(searcher.RuleFilter))
            {
                rules.Add(searcher.RuleFilter);
            }
        }

        if (rules.Count == 0)
        {
            rules.Add(AutomatchConstants.WildcardRules[Random.Shared.Next(AutomatchConstants.WildcardRules.Length)]);
        }

        var map = AutomatchConstants.MapPool[Random.Shared.Next(AutomatchConstants.MapPool.Length)];
        var match = new PendingMatch(
            host.CharacterIdentifier,
            [.. waiting.Select(searcher => searcher.CharacterIdentifier)],
            DateTimeOffset.UtcNow,
            0,
            rules,
            map);

        foreach (var searcher in waiting)
        {
            searcher.State = AutomatchState.AwaitingHostCreate;
            searcher.Match = match;
        }

        pending.Add(match);
        formed.Add(match);
    }

    /// <summary>
    /// Releases groups whose host produced a game and gives up on those whose
    /// host did not.
    /// </summary>
    private async Task ReleaseCreatedMatchesAsync(CancellationToken cancellationToken)
    {
        List<PendingMatch> snapshot;
        lock (gate)
        {
            snapshot = [.. pending];
        }

        foreach (var match in snapshot)
        {
            var hooksSnapshot = hooks;

            if (match.GameIdentifier == 0 && !match.HostLost && hooksSnapshot is not null)
            {
                var hosted = await hooksSnapshot.FindHostedGameIdentifierAsync(
                    lobbyIdentifier,
                    match.HostCharacterIdentifier,
                    cancellationToken);

                if (hosted != 0 && hosted != match.GameIdentifierBefore)
                {
                    match.GameIdentifier = hosted;
                }
            }

            if (match.GameIdentifier != 0)
            {
                await ReleaseMatchAsync(match, cancellationToken);
                continue;
            }

            var expired = DateTimeOffset.UtcNow - match.ElectedAt > AutomatchConstants.HostCreateTimeout;
            if (match.HostLost || expired)
            {
                FailMatch(match);
            }
        }
    }

    /// <summary>Names and unlocks the created game and retires its members.</summary>
    /// <param name="match">Group whose game exists.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task ReleaseMatchAsync(PendingMatch match, CancellationToken cancellationToken)
    {
        var name = $"AUTOMATCH{Interlocked.Increment(ref gameNumber)}";
        name = name.Length > 16 ? name[..16] : name;

        try
        {
            if (hooks is not null)
            {
                await hooks.RenameAndUnlockAsync(match.GameIdentifier, name, cancellationToken);
            }
        }
        catch
        {
            // Cosmetic; never worth stranding a formed match over.
        }

        lock (gate)
        {
            foreach (var characterIdentifier in match.Members)
            {
                if (searchers.TryGetValue(characterIdentifier, out var searcher))
                {
                    // Retire the searcher as matched but keep the entry: a
                    // cancel that arrives after the match formed must still be
                    // able to report TooLate. The reap drops it later, or a new
                    // search replaces it.
                    searcher.State = AutomatchState.Matched;
                }
            }

            pending.Remove(match);
        }
    }

    /// <summary>Reports a group whose host never created a game and retires its members.</summary>
    /// <param name="match">Group that failed.</param>
    private void FailMatch(PendingMatch match)
    {
        lock (gate)
        {
            failed.Add(match);
            foreach (var characterIdentifier in match.Members)
            {
                searchers.Remove(characterIdentifier);
            }

            pending.Remove(match);
        }
    }
}
