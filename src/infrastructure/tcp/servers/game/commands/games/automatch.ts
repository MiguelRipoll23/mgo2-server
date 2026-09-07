import { inject, injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { sendPacket, sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import {
  RESULT_NONE,
  RESULT_AUTOMATCH_CANNOT_START,
  RESULT_AUTOMATCH_CANCEL_TOO_LATE,
  RESULT_AUTOMATCH_NOT_OPEN,
  RESULT_INVALID_SESSION,
} from "../../../../../../core/constants/error-codes-constants.ts";
import {
  AutomatchService,
  AutomatchState,
  ANY_RULE,
  RULE_FILTERS,
  type Searcher,
} from "../../../../../../modules/game/automatch-service.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { ActiveGameSessionsService } from "../../../../services/active-game-sessions-service.ts";

// Automatching: start (0x43e0), cancel (0x43e2) and the server→client pushes.
//
// 0x43e0 is NOT a status fetch — it is "start automatching", sent on confirm
// from state 4 of the screen's state machine (0x93CD58). Its single u8 is the
// rule filter; 11 is the "do not specify rules" sentinel, deliberately outside
// the 0-10 range the label mapper accepts (0x93B610).
//
// Answering result 0 is a commitment: it sets the client's "loaded" flag and
// registers push channel 60 (0x93CF04), after which the client sends nothing
// and waits ~20 minutes for the matchmaker's 0x43e4 push. This server now
// runs the matchmaker (AutomatchService + a 5-second ticker), so a valid
// request is enqueued and answered with the searcher's own band and the
// players-needed figure. An invalid one is refused with the official codes.

const START_AUTOMATCH = 0x43e0;
const START_AUTOMATCH_RESULT = 0x43e1;
const CANCEL_AUTOMATCH = 0x43e2;
const CANCEL_AUTOMATCH_RESULT = 0x43e3;

// ── S→C pushes (all unsolicited; only reach a client answered 0x43e1 = 0) ──

const SEARCH_PANEL = 0x43e4; // 36 bytes, event 42 — fire-and-forget, never stalls
const MATCH_FOUND = 0x43f1; // 223 bytes, event 44 — THE MATCH
const MATCH_GAME = 0x43f2; // 4 bytes, event 45 — releases the group; one-shot
const MATCH_FAILED = 0x43f3; // 4 bytes, event 46 ⇒ error 4945

const SEARCH_PANEL_SIZE = 36;
const SETTINGS_BLOCK_SIZE = 204;
const MATCH_FOUND_SIZE = 19 + SETTINGS_BLOCK_SIZE;

const COLUMNS = 23; // one per level 0-22
const ARRAY_BYTES = 16; // ceil(23/2); the client never reads the last 4 but the size is fixed
const MAX_COLUMN = 15; // bar is (A[i]+B[i])*64 px clamped to 960

@injectable()
@GameCommandHandler(START_AUTOMATCH)
export class StartAutomatchHandler implements ICommandHandler {
  constructor(
    private automatch = inject(AutomatchService),
    private characterService = inject(CharacterService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId === null) {
      await sendResult(session, START_AUTOMATCH_RESULT, RESULT_INVALID_SESSION);
      return;
    }

    const rule = packet.payload.length >= 1 ? packet.payload[0] : -1;
    if (!RULE_FILTERS.has(rule)) {
      // Not a value any menu row can produce — queueing it would search for a
      // rule the player did not choose.
      await sendResult(session, START_AUTOMATCH_RESULT, RESULT_AUTOMATCH_CANNOT_START);
      return;
    }

    const character = await this.characterService.findById(characterId);
    const experience = character?.experience ?? 0;

    this.automatch.enqueue(characterId, rule, experience, true);

    // The starting band — this searcher's own window, which widens with their
    // wait and is re-sent on every 0x43e4. The shortfall is minPlayers - 1:
    // they have just been enqueued and can always reach themselves.
    const searcher = this.automatch.get(characterId);
    const band = searcher ? this.automatch.band(searcher) : 1;
    const needed = Math.max(0, this.automatch.playersNeededOnArrival() - 1);

    const writer = new PacketWriter()
      .writeUint32(RESULT_NONE)
      .writeUint8(band)
      .writeUint8(needed);
    await sendPacket(session, START_AUTOMATCH_RESULT, writer.build());
  }
}

@injectable()
@GameCommandHandler(CANCEL_AUTOMATCH)
export class CancelAutomatchHandler implements ICommandHandler {
  constructor(private automatch = inject(AutomatchService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId === null) {
      await sendResult(session, CANCEL_AUTOMATCH_RESULT, RESULT_INVALID_SESSION);
      return;
    }

    const outcome = this.automatch.cancel(characterId);
    await sendResult(
      session,
      CANCEL_AUTOMATCH_RESULT,
      outcome === "TOO_LATE" ? RESULT_AUTOMATCH_CANCEL_TOO_LATE : RESULT_NONE,
    );
  }
}

// ── Push writers, shared with the ticker ────────────────────────────────────

/**
 * The search panel: a population histogram by player level, plus the band and
 * players needed. Fire-and-forget — nothing waits on it, no value it carries
 * changes control flow, and an all-zero payload simply renders a flat graph.
 */
export function writeSearchPanel(
  writer: PacketWriter,
  matching: number[],
  inGame: number[],
  band: number,
  playersNeeded: number,
): void {
  writeColumns(writer, matching);
  writer.writeUint8(clamp(band, 0, 22));
  writer.writeUint8(clamp(playersNeeded, 0, 0xff));
  writer.writeUint8(0); // event-42 argument; its only handler overwrites it
  writeColumns(writer, inGame);
  writer.writeUint8(0); // stored at status block +4, never read
}

/** Packs 23 column counts into 16 bytes, two per byte (low nibble = even column). */
function writeColumns(writer: PacketWriter, columns: number[]): void {
  const packed = new Uint8Array(ARRAY_BYTES);
  for (let column = 0; column < COLUMNS; column++) {
    const value = clamp(columns[column] ?? 0, 0, MAX_COLUMN);
    const index = Math.floor(column / 2);
    packed[index] |= column % 2 === 0 ? value : value << 4;
  }
  writer.writeBytes(packed);
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

/**
 * 0x43f1: 19 bytes of scalars then the 204-byte settings block. Goes to the
 * WHOLE group; every recipient compares the leading id against its own — the
 * match goes to state 12 and creates the game, everyone else parks at 18.
 */
export function writeMatchFound(
  writer: PacketWriter,
  hostCharaId: number,
  lobbyId: number,
  lobbySubtype: number,
  rule: number,
  settings: Uint8Array,
): void {
  writer.writeUint32(hostCharaId >>> 0);
  writer.writeUint32(lobbyId >>> 0);
  writer.writeUint8(lobbySubtype);
  writer.writeUint8(rule); // rotation entry 0's rule — what actually starts
  writer.writeUint32(0); // total/index pair, unread on subtype 2
  writer.writeUint32(0);
  writer.writeUint8(0); // rotation index: ALWAYS 0 (the client copies entry idx into entry 0)
  writer.writeBytes(settings);
}

export function writeMatchGame(writer: PacketWriter, gameId: number): void {
  writer.writeUint32(gameId >>> 0);
}

export function writeMatchFailed(writer: PacketWriter, detail: number): void {
  writer.writeUint32(detail >>> 0);
}

export {
  SEARCH_PANEL,
  MATCH_FOUND,
  MATCH_GAME,
  MATCH_FAILED,
  SEARCH_PANEL_SIZE,
  SETTINGS_BLOCK_SIZE,
  MATCH_FOUND_SIZE,
};

// Re-exported for the ticker in main.ts.
export type { Searcher };
export { AutomatchState };
