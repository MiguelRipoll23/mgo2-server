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

* **The briefing half of the freeze** (`join-stall-after-mode2.md` §17). A room of
  one is not timed: the count its client is sent is held at the room's whole
  briefing time and no deadline is armed, so the briefing never runs out and no
  round opens on it. What ends a solo briefing is the other half of the rule below
  — the client's own ready (`MatchStartRunService.OnJoinReport` arms the count, and
  `ArmBriefingDeadline` is armed only once the room has a second player).
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

* **The freeze at one client**, for the round's own clock — the briefing's is
  written, above. The design it needs is:
  * a clock that counts only the time the room held more than one client, for the
    round (`MatchRoundStateService.RemainingUnits` is a plain elapsed-time
    subtraction from `openedAt` today; the briefing's deadline was the same shape
    of `Task.Delay` and no longer runs for a room of one at all);
  * one population source for both — `RoomRosterService.JoinerCount`, which is
    what `ResetWhenEmpty` already reads — so the freeze and the reset cannot
    disagree about who is in the room;
  * and the consequence for the beat, which writes the round's remaining units on
    every clock record (`MatchRecordBodiesUtils.RoundTimeLimitUnits`): a frozen
    round writes the same value until it unfreezes, which is what "frozen" means
    on the wire.
  Until it is written, a round with one client in it — the round a solo client
  starts by readying — runs down on wall time, which is the behaviour the captures
  show for rooms of two or more and not the freeze the rule asks for.
