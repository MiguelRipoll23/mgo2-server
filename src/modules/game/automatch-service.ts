import { injectable } from "@needle-di/core";
import { CharacterService } from "../character/character-service.ts";

// The automatch queue and the periodic work that drives it, ported from the
// reference's Automatch/AutomatchPolicy. The wire shapes live in the
// automatch.ts handler; this class stays packet-free.

/** "Do not specify rules" — the sentinel outside the 0-10 label range. */
export const ANY_RULE = 11;

/**
 * Rule filters accepted from 0x43e0: every rule the stock client can offer —
 * 0-5 plus 7 (Team Sneaking, gated by the 0x4101 feature bit this server now
 * sets) — plus 6 (BOMB) and 8 (COOP), which stock clients hardcode off but a
 * patched client may send. Accepting them costs nothing on a stock client
 * (no menu row produces them) and keeps a patched one from being refused.
 */
export const RULE_FILTERS = new Set([0, 1, 2, 3, 4, 5, 6, 7, 8, ANY_RULE]);

/** Rules a wildcard search may be given — only ones stock clients can actually play. */
export const WILDCARD_RULES = [0, 1, 2, 3, 4, 5, 7];

/** The maps automatching may pick — the disc's five shipping stages. */
export const MAP_POOL = [2, 3, 4, 7, 12];

/** How long a searcher may sit in the queue before we drop them (backstop; the client gives up at ~20 min). */
export const MAX_WAIT_MS = 25 * 60 * 1000;

/** How long an elected host has to create the game before the group is told it failed. */
export const HOST_CREATE_TIMEOUT_MS = 45 * 1000;

/** Searcher lifecycle. */
export enum AutomatchState {
  SEARCHING = 0,
  AWAITING_HOST_CREATE = 1,
  MATCHED = 2,
}

export class Searcher {
  constructor(
    public readonly charaId: number,
    public readonly ruleFilter: number,
    public readonly experience: number,
    public readonly joinedAt: number,
    /** Whether the socket is still alive — reaped by the tick. */
    public active: boolean,
    public state: AutomatchState = AutomatchState.SEARCHING,
    public match: PendingMatch | null = null,
  ) {}

  /** The level the client will draw this searcher's window around. */
  level(): number {
    return levelOf(this.experience);
  }

  waited(): number {
    return Date.now() - this.joinedAt;
  }
}

/** A group told to form a game, and the host elected to create it. */
export class PendingMatch {
  gameId = 0;
  hostLost = false;

  constructor(
    public readonly hostCharaId: number,
    public readonly members: number[],
    public readonly electedAt: number = Date.now(),
    /** Any game the elected host already hosted when we chose them (ghost-row guard). */
    public readonly gameIdBefore: number = 0,
    /** The rotation rules for the match, entry 0 first. */
    public readonly rules: number[] = [],
    /** The map the match runs. */
    public readonly map: number = 0,
  ) {}
}

/** Level derived from experience — the same table CharacterService draws. */
export function levelOf(experience: number): number {
  return CharacterService.calculateLevel(experience);
}

/** Matchmaking policy: bands widen and requirements decay with wait time. */
export class AutomatchPolicy {
  constructor(
    public readonly minPlayers = 2,
    public readonly minPlayersStart = 12,
    public readonly minPlayersStepMs = 30_000,
    public readonly bandStart = 1,
    public readonly bandStepMs = 30_000,
    public readonly bandMax = 22,
    public readonly modeRelaxMs = 90_000,
  ) {}

  /** The level half-width for a searcher who has waited this long. */
  bandAfter(waitedMs: number): number {
    const steps = Math.floor(waitedMs / this.bandStepMs);
    return Math.min(this.bandStart + steps, this.bandMax);
  }

  /**
   * How many players a group needs after this wait: decays from
   * minPlayersStart to minPlayers, one per step.
   */
  requiredPlayersAfter(waitedMs: number): number {
    if (this.minPlayersStepMs === 0) return this.minPlayers;
    const steps = Math.floor(waitedMs / this.minPlayersStepMs);
    return Math.max(this.minPlayers, this.minPlayersStart - steps);
  }

  /** Whether a searcher who has waited this long accepts any mode. */
  acceptsAnyModeAfter(waitedMs: number): boolean {
    return waitedMs >= this.modeRelaxMs;
  }
}

/** Callbacks the service needs into the server/game layer. */
export interface AutomatchHooks {
  /** The id of a game this character hosts in this lobby, or 0. */
  hostedGameId(lobbyId: number, charaId: number): Promise<number>;
  /** Renames a formed game and clears any password on it. */
  renameAndUnlock(gameId: number, name: string): Promise<void>;
}

@injectable()
export class AutomatchService {
  private readonly searchers = new Map<number, Searcher>();
  private readonly pending: PendingMatch[] = [];
  private readonly policy = new AutomatchPolicy();
  private gameNumber = 0;
  private hooks: AutomatchHooks | null = null;

  setLobby(lobbyId: number, hooks: AutomatchHooks): void {
    this.lobbyId = lobbyId;
    this.hooks = hooks;
  }

  private lobbyId = 0;

  /** The tick's formed matches, exposed for the ticker's push phase. */
  takeFormedMatches(): PendingMatch[] {
    const formed = this.formed;
    this.formed = [];
    return formed;
  }

  /** Matches whose host never created a game — the group gets 0x43f3. */
  takeFailedMatches(): PendingMatch[] {
    const failed = this.failed;
    this.failed = [];
    return failed;
  }

  private formed: PendingMatch[] = [];
  private failed: PendingMatch[] = [];

  /** Adds a searcher, replacing any earlier entry for the same character. */
  enqueue(charaId: number, ruleFilter: number, experience: number, active: boolean): void {
    this.searchers.set(charaId, new Searcher(charaId, ruleFilter, experience, Date.now(), active));
  }

  /**
   * Removes a searcher. The outcome is decided by the state at the moment
   * this runs, which makes the race with the tick decidable: if the tick has
   * already marked this searcher matched, the cancel is too late.
   */
  cancel(charaId: number): "CANCELLED" | "TOO_LATE" {
    const searcher = this.searchers.get(charaId);
    if (!searcher) return "CANCELLED";
    if (searcher.state !== AutomatchState.SEARCHING) return "TOO_LATE";
    this.searchers.delete(charaId);
    return "CANCELLED";
  }

  /** Marks a searcher's connection dead — reaped by the tick. */
  disconnect(charaId: number): void {
    const searcher = this.searchers.get(charaId);
    if (searcher) searcher.active = false;
  }

  size(): number {
    return this.searchers.size;
  }

  /**
   * How many more players THIS searcher needs, counted among those they can
   * actually reach. The recipient counts themselves, so `others = reachable - 1`.
   */
  playersNeeded(searcher: Searcher): number {
    let reachable = 0;
    for (const other of this.searchers.values()) {
      if (other.state !== AutomatchState.SEARCHING) continue;
      if (!other.active) continue;
      if (!this.windowsOverlap(searcher, other)) continue;
      if (!this.modesCompatible(searcher, other)) continue;
      reachable++;
    }
    const others = Math.max(0, reachable - 1);
    return Math.max(0, this.policy.requiredPlayersAfter(searcher.waited()) - others);
  }

  playersNeededOnArrival(): number {
    return Math.max(0, this.policy.requiredPlayersAfter(0));
  }

  /** This searcher's half-width right now — also what we send them. */
  band(searcher: Searcher): number {
    return this.policy.bandAfter(searcher.waited());
  }

  /**
   * One pass of the matchmaker: reap first (a dead searcher is never counted
   * into the figures survivors are shown), then release and form.
   */
  async tick(): Promise<void> {
    this.reap();
    await this.releaseCreatedMatches();
    this.formMatches();
  }

  /** Drops dead or over-waited searchers. */
  private reap(): void {
    const now = Date.now();
    for (const [charaId, searcher] of this.searchers) {
      if (!searcher.active || now - searcher.joinedAt > MAX_WAIT_MS) {
        this.searchers.delete(charaId);
        if (searcher.match && searcher.match.hostCharaId === charaId && searcher.match.gameId === 0) {
          searcher.match.hostLost = true;
        }
      }
    }
  }

  /** Whether two searchers' level windows share at least one level. */
  private windowsOverlap(a: Searcher, b: Searcher): boolean {
    const bandSum = this.policy.bandAfter(a.waited()) + this.policy.bandAfter(b.waited());
    return Math.abs(a.level() - b.level()) <= bandSum;
  }

  /** Grouped by mode first, relaxed with time — wildcards fit everyone. */
  private modesCompatible(a: Searcher, b: Searcher): boolean {
    if (a.ruleFilter === ANY_RULE || b.ruleFilter === ANY_RULE) return true;
    if (a.ruleFilter === b.ruleFilter) return true;
    return this.policy.acceptsAnyModeAfter(a.waited()) ||
      this.policy.acceptsAnyModeAfter(b.waited());
  }

  /**
   * Elects a host for every group large enough to play. Groups form ACROSS
   * rule filters (a rotation carries several modes); the longest-waiting
   * searcher anchors the group and everyone whose window overlaps joins it.
   */
  private formMatches(): void {
    const formed: PendingMatch[] = [];
    const queued = [...this.searchers.values()]
      .filter((s) => s.state === AutomatchState.SEARCHING && s.active)
      .sort((a, b) => a.joinedAt - b.joinedAt);

    if (queued.length < this.policy.minPlayers) return;

    const anchor = queued[0];
    const anchorRequirement = this.policy.requiredPlayersAfter(anchor.waited());
    const waiting = queued.filter(
      (s) => this.windowsOverlap(anchor, s) && this.modesCompatible(anchor, s),
    );
    if (waiting.length < anchorRequirement) return;

    // Longest-waiting first, skipping anyone who already hosts here —
    // electing them would make their existing game look like the new one.
    const host = waiting.find(
      (s) => !this.pendingHosts().has(s.charaId),
    );
    if (!host) return;

    // Every distinct mode the group asked for, in join order; wildcards
    // contribute nothing. An all-wildcard group picks one at random.
    const rules: number[] = [];
    for (const s of waiting) {
      if (s.ruleFilter !== ANY_RULE && !rules.includes(s.ruleFilter)) rules.push(s.ruleFilter);
    }
    if (rules.length === 0) {
      rules.push(WILDCARD_RULES[Math.floor(Math.random() * WILDCARD_RULES.length)]);
    }

    const map = MAP_POOL[Math.floor(Math.random() * MAP_POOL.length)];
    const match = new PendingMatch(
      host.charaId,
      waiting.map((s) => s.charaId),
      Date.now(),
      0,
      rules,
      map,
    );

    for (const s of waiting) {
      s.state = AutomatchState.AWAITING_HOST_CREATE;
      s.match = match;
    }
    this.pending.push(match);
    this.formed.push(match);
  }

  private pendingHosts(): Set<number> {
    return new Set(this.pending.map((m) => m.hostCharaId));
  }

  /**
   * Releases groups whose host has produced a game; gives up on those whose
   * host has not. 0x43f2 must not go out before the host's 0x4310/0x4316 have
   * landed — waiting for the row guarantees that.
   */
  private async releaseCreatedMatches(): Promise<void> {
    for (const match of [...this.pending]) {
      if (match.gameId === 0 && !match.hostLost && this.hooks) {
        const hosted = await this.hooks.hostedGameId(this.lobbyId, match.hostCharaId);
        if (hosted !== 0 && hosted !== match.gameIdBefore) match.gameId = hosted;
      }

      if (match.gameId !== 0) {
        const name = `AUTOMATCH${++this.gameNumber}`.slice(0, 16);
        try {
          await this.hooks?.renameAndUnlock(match.gameId, name);
        } catch {
          // Cosmetic; never worth stranding a formed match over.
        }
        for (const charaId of match.members) {
          const searcher = this.searchers.get(charaId);
          if (searcher) searcher.state = AutomatchState.MATCHED;
          this.searchers.delete(charaId);
        }
        this.pending.splice(this.pending.indexOf(match), 1);
        continue;
      }

      const expired = Date.now() - match.electedAt > HOST_CREATE_TIMEOUT_MS;
      if (match.hostLost || expired) {
        this.failed.push(match);
        for (const charaId of match.members) this.searchers.delete(charaId);
        this.pending.splice(this.pending.indexOf(match), 1);
      }
    }
  }

  /** Told by create-game that a game now exists, so the tick need not wait. */
  gameCreated(hostCharaId: number, gameId: number): void {
    for (const match of this.pending) {
      if (match.hostCharaId === hostCharaId && match.gameId === 0) {
        match.gameId = gameId;
      }
    }
  }

  /** Snapshot accessors for the handler layer. */
  get(charaId: number): Searcher | undefined {
    return this.searchers.get(charaId);
  }

  searchersSnapshot(): Searcher[] {
    return [...this.searchers.values()];
  }
}
