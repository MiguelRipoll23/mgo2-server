# The room's clocks when it is empty, and when it holds one client

**Where this comes from.** The room's own rule, stated by the operator on
2026-10-15 and not measured by any capture held here:

* **A room that holds exactly one client freezes its clocks** — the briefing time
  and the round's time both stop advancing. Readiness still works in that state,
  so a solo client is never held by the freeze.
* **A solo client's readiness ends the briefing and starts a solo session** — the
  round opens on that report and is played by the one client.
* **A room that empties resets** — the time, the tickets and the current round
  start over. A leaver among others changes nothing; only the room emptying does.

**What the captures corroborate.** The solo-ready rule is `mgo2-started-briefing-then-solo-game`:
the client reports on `0x1ade` at t+173.438, the opening run follows at t+174.371
and the client's own records begin at t+181.578 — one client, one report, a round
that runs (`docs/analysis/briefing-start-comparison.md` §4.1 for the identifiers
it plays under). The freeze and the reset are **not** in the captures, and neither
is the client's side of either; both are stated here because they are the rule the
host is to follow.

## What is written

* **The reset on empty.** `MatchStartRunService.ResetWhenEmpty` is called from the
  session reaper's drop (`GameplayServerService.DropReapedPeer`), and it is the
  room's population that decides: a leaver among others returns `false` and
  nothing moves, while the last client out drops the round
  (`MatchRoundStateService.Reset`: the count back to zero, the clock and settings
  cleared, the result cleared), clears the host's score — which is what its
  tickets are counted from, `MatchResultService.TicketsReachedAsync` — clears the
  briefing states and begins a fresh briefing, countdown and deadline. The next
  client into the room therefore arrives at a first round rather than at the one
  that emptied.

## What is not written

* **The freeze at one client**, for both clocks. The design it needs is the same
  for each and is not yet in the code:
  * a clock that counts only the time the room held more than one client, for the
    round (`MatchRoundStateService.RemainingUnits` is a plain elapsed-time
    subtraction from `openedAt` today) and for the briefing's deadline
    (`MatchStartRunService.Briefing.cs` arms a plain `Task.Delay` for the room's
    briefing minutes, and `BriefingCountdownService.Remaining` likewise reads a
    start time);
  * one population source for both — `RoomRosterService.JoinerCount`, which is
    what `ResetWhenEmpty` already reads — so the freeze and the reset cannot
    disagree about who is in the room;
  * and the consequence for the beat, which writes the round's remaining units on
    every clock record (`MatchRecordBodiesUtils.RoundTimeLimitUnits`): a frozen
    round writes the same value until it unfreezes, which is what "frozen" means
    on the wire.
  Until it is written, a round with one client in it runs down on wall time and a
  briefing with one client in it reaches its deadline and opens its round, which
  is the behaviour the captures show for rooms of two or more.
