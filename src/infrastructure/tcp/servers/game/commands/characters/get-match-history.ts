import { inject, injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { sendPacket, sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";
import { RoundReportService } from "../../../../../../modules/game/round-report-service.ts";

// Met-players history (0x4680 → START 0x4681, DATA 0x4682*, END 0x4683).
// Request is a u32 character id. Rows derive from round reports at query
// time — players who shared a game with the viewer, newest first.
//
// 25-byte record, live-confirmed in the reference: u32 Unix timestamp of the
// last encounter, u32 character id, 16-byte player name, and a trailing u8
// game-type label decoded by a 9-arm table that REJECTS anything outside
// 1..9 (0 renders a blank column). The label is the lobby subtype, which is
// a packet-specific table — not the 8-arm one 0x4582/0x4602 use.
const GET_MATCH_HISTORY = 0x4680;
const MATCH_HISTORY_START = 0x4681;
const MATCH_HISTORY_ENTRIES = 0x4682;
const MATCH_HISTORY_END = 0x4683;

const HISTORY_LIMIT = 64;
const HISTORY_ENTRIES_PER_PACKET = 40;

@injectable()
@GameCommandHandler(GET_MATCH_HISTORY)
export class GetMatchHistoryHandler implements ICommandHandler {
  constructor(private reportService = inject(RoundReportService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const characterId = packet.payload.length >= 4
      ? new DataView(packet.payload.buffer, packet.payload.byteOffset).getUint32(0, false)
      : 0;

    const met = characterId > 0
      ? await this.reportService.metPlayers(characterId, HISTORY_LIMIT)
      : [];

    await sendResult(session, MATCH_HISTORY_START, RESULT_NONE);

    for (let start = 0; start < met.length; start += HISTORY_ENTRIES_PER_PACKET) {
      const batch = met.slice(start, start + HISTORY_ENTRIES_PER_PACKET);
      const writer = new PacketWriter();
      for (const player of batch) {
        writer.writeUint32(player.lastMetEpochSeconds >>> 0);
        writer.writeUint32(player.charaId);
        writer.writeFixedString(player.name, 16);
        writer.writeUint8(gameTypeLabel(player.lobbySubtype));
      }
      await sendPacket(session, MATCH_HISTORY_ENTRIES, writer.build());
    }

    await sendResult(session, MATCH_HISTORY_END, RESULT_NONE);
  }
}

/**
 * The game-type label for a history row's trailing byte: the lobby subtype
 * itself where the client's 9-arm table agrees (1 Free Battle, 2
 * Automatching, 3 Tournament, 4 Survival, 5/6 Official, 7/8 Training), 0
 * for anything the table rejects.
 */
function gameTypeLabel(lobbySubtype: number): number {
  return lobbySubtype >= 1 && lobbySubtype <= 9 ? lobbySubtype : 0;
}
