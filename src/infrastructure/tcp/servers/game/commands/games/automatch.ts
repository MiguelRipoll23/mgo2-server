import { injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import {
  RESULT_NONE,
  RESULT_AUTOMATCH_CANNOT_START,
  RESULT_AUTOMATCH_NOT_OPEN,
} from "../../../../../../core/constants/error-codes-constants.ts";

// Automatching: start (0x43e0) and cancel (0x43e2) of a ranked search.
//
// 0x43e0 is NOT a status fetch — it is "start automatching", sent on confirm
// from state 4 of the screen's state machine (0x93CD58). Its single u8 is the
// rule filter; 11 is the "do not specify rules" sentinel, deliberately outside
// the 0-10 range the label mapper accepts (0x93B610). The client's own error
// table settles the semantics: a timeout on this slot prints "Unable to START
// automatching" (0x93CDD4) where 0x43e2's prints "Unable to cancel".
//
// Answering result 0 is a commitment: it sets the client's "loaded" flag and
// registers push channel 60 (0x93CF04), after which the client sends nothing
// and waits ~20 minutes for the matchmaker's 0x43e4 push. This server has no
// matchmaker, so valid requests are refused with AUTOMATCH_NOT_OPEN and the
// client prints the sentence Konami wrote for that case, instead of stalling.
const START_AUTOMATCH = 0x43e0;
const START_AUTOMATCH_RESULT = 0x43e1;
const CANCEL_AUTOMATCH = 0x43e2;
const CANCEL_AUTOMATCH_RESULT = 0x43e3;

// "Do not specify rules" — disc string 925, the default selection.
const ANY_RULE = 11;

// Menu rows carry rule ids 0-5 plus the sentinel; rules 6 (BOMB) and 7 (team
// sneaking) have no row in this build, so a value outside this set is a
// mis-parse, not a selection.
const RULE_FILTERS = new Set([0, 1, 2, 3, 4, 5, ANY_RULE]);

@injectable()
@GameCommandHandler(START_AUTOMATCH)
export class StartAutomatchHandler implements ICommandHandler {
  async handle(session: TcpSession, packet: Packet): Promise<void> {
    // A malformed request is CAN_NOT_START; a well-formed one is refused
    // because there is no matchmaker to ever look at the player.
    const rule = packet.payload.length >= 1 ? packet.payload[0] : -1;
    if (!RULE_FILTERS.has(rule)) {
      await sendResult(
        session,
        START_AUTOMATCH_RESULT,
        RESULT_AUTOMATCH_CANNOT_START,
      );
      return;
    }

    await sendResult(session, START_AUTOMATCH_RESULT, RESULT_AUTOMATCH_NOT_OPEN);
  }
}

@injectable()
@GameCommandHandler(CANCEL_AUTOMATCH)
export class CancelAutomatchHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    // Explicit four-byte zero rather than an empty payload — an empty one
    // leaves the client reading four bytes of stale receive buffer as its
    // result (the trap the reference records for 0x4399).
    await sendResult(session, CANCEL_AUTOMATCH_RESULT, RESULT_NONE);
  }
}
