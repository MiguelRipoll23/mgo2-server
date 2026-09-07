import { SETTINGS_BLOCK_SIZE } from "./automatch.ts";

// The 204-byte settings block carried inside 0x43f1 — the elected host hands
// it straight to the create-game task, whose 0x4310 builder reads nothing
// else. Every field here becomes the created game. Ported from the
// reference's AutomatchSettingsBlock; see its javadoc for the field evidence.
//
// Tiers: the observed retail values (DM 10/50, TDM 5/4/25, SNE 7/4) replace
// the client defaults for those rules; everything else stays at the client
// default — a known-wrong fixed value beats an invented one, and the
// difference is one line per rule as soon as a game of that rule is observed.

const ROTATION = 0;
const ROTATION_ENTRIES = 16;
const ENTRY_BYTES = 3; // rule, map, flags

const MAX_PLAYERS = 66;
const MAX_PLAYERS_VALUE = 16;
const CURRENT_PLAYERS = 67;
const PLAYERS_AT_CREATE = 1; // the host is the only player when it creates
const BRIEFING_TIME = 68;
const BRIEFING_SECONDS = 2;
const LEVEL_LIMIT_TOLERANCE = 95;
const LEVEL_CAP = 22;
const TIMERS = 100; // 17 u32s
const UNIQUE_BLUE = 169;
const COMMON_A = 177;
const COMMON_A_VALUE = 0x24;
const TEAM_KILL_KICK = 182;
const TEAM_KILL_KICK_VALUE = 3;
const SNAKE = 189;
const AUTOMATCH_SNAKE = 3;
const UNKNOWN_72 = 72;
const UNKNOWN_72_VALUE = 0x02000000;
const UNKNOWN_179 = 179;
const UNKNOWN_179_VALUE = 0x20;

// Timer-array index of each rule's first slot, and how many slots it owns
// (DM has no rounds slot — its count is fixed at one).
const RULE_TIMERS: Record<number, number[]> = {
  0: [9, 2], // Deathmatch      — time, tickets
  1: [6, 3], // Team Deathmatch — time, rounds, tickets
  2: [4, 2], // Rescue          — time, rounds
  3: [2, 2], // Capture         — time, rounds
  4: [0, 2], // Sneaking        — time, rounds (+ SNAKE)
  5: [11, 2], // Base           — time, rounds
  6: [13, 2], // Bomb           — time, rounds. Not served
  7: [15, 2], // Team Sneaking  — time, rounds. Not served
};

// Values a real automatch game was observed to use, by rule id.
const AUTOMATCH_TIMERS: Record<number, number[]> = {
  0: [10, 50], // DM:  10 min, 50 tickets          (default 5/30)
  1: [5, 4, 25], // TDM: 5 min, 4 rounds, 25 tickets (default 3/4/15)
  4: [7, 4], // SNE: 7 min, 4 rounds              (default 8/4)
};

// Client-default timer array: SNE 8/4, CAP 4/4, RES 4/4, TDM 3/4/15, DM 5/30,
// BASE 5/4, BOMB 30/4, TSNE 10/4.
const DEFAULT_TIMERS = [8, 4, 4, 4, 4, 4, 3, 4, 15, 5, 30, 5, 4, 30, 4, 10, 4];

function putU32(block: Uint8Array, at: number, value: number): void {
  const view = new DataView(block.buffer);
  view.setUint32(at, value >>> 0, false);
}

function putU16(block: Uint8Array, at: number, value: number): void {
  const view = new DataView(block.buffer);
  view.setUint16(at, value, false);
}

function template(): Uint8Array {
  const block = new Uint8Array(SETTINGS_BLOCK_SIZE);
  block[MAX_PLAYERS] = MAX_PLAYERS_VALUE;
  putU32(block, BRIEFING_TIME, BRIEFING_SECONDS);
  block[LEVEL_LIMIT_TOLERANCE] = LEVEL_CAP;
  for (let i = 0; i < DEFAULT_TIMERS.length; i++) {
    putU32(block, TIMERS + i * 4, DEFAULT_TIMERS[i]);
  }
  block[UNIQUE_BLUE] = 1;
  block[COMMON_A] = COMMON_A_VALUE;
  putU16(block, TEAM_KILL_KICK, TEAM_KILL_KICK_VALUE);
  block[SNAKE] = AUTOMATCH_SNAKE;
  // The two still-unexplained fields, emitted as captured.
  putU32(block, UNKNOWN_72, UNKNOWN_72_VALUE);
  block[UNKNOWN_179] = UNKNOWN_179_VALUE;
  return block;
}

/**
 * Builds the block for a match: one rotation entry per distinct rule the
 * group asked for, contiguous from index 0; flags left 0 (the one value the
 * disc's ruleopt_bit whitelist permits for every rule). Map must be nonzero —
 * the client discards a zero-map entry.
 */
export function buildAutomatchSettingsBlock(rules: number[], map: number): Uint8Array {
  if (rules.length === 0) throw new Error("An automatch rotation needs at least one rule.");
  if (rules.length > ROTATION_ENTRIES) {
    throw new Error(`At most ${ROTATION_ENTRIES} rotation entries, got ${rules.length}`);
  }
  if (map === 0) throw new Error("Automatch map must not be 0.");

  const block = template();
  block[CURRENT_PLAYERS] = PLAYERS_AT_CREATE;

  for (let entry = 0; entry < rules.length; entry++) {
    const at = ROTATION + entry * ENTRY_BYTES;
    block[at] = rules[entry];
    block[at + 1] = map;
    block[at + 2] = 0; // per-rule "rule option"
    applyObservedTimers(block, rules[entry]);
  }
  return block;
}

/** Replaces a rule's client defaults with the observed values where we have them. */
function applyObservedTimers(block: Uint8Array, rule: number): void {
  if (rule === 4) block[SNAKE] = AUTOMATCH_SNAKE;
  const observed = AUTOMATCH_TIMERS[rule];
  const slots = RULE_TIMERS[rule];
  if (!observed || !slots) return;
  for (let i = 0; i < observed.length && i < slots[1]; i++) {
    putU32(block, TIMERS + slots[0] * 4 + i * 4, observed[i]);
  }
}
