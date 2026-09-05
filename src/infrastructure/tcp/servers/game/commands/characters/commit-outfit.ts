import { injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { GEAR_PAYLOAD } from "./get-gear.ts";

// Outfit commit (0x4132, empty payload): fired after the 0x4130 updates when
// the outfit screen closes, and blocked on. The reply is NOT a result code —
// the parser reads a u32 entry count, then count × {u8 slot, u32 value} gear
// entries, then the fixed sixteen-pair trailer. The parser ZEROES the whole
// gear table before applying entries, so a count of 0 does not mean "no
// change", it means "forget every item you own" — and a nonzero first word
// would be read as a count, not an error, so there is no error shape here.
//
// The reply is therefore the same gear catalogue 0x4124 sends at connect,
// from the same constant source, which keeps the two writers honest.
@injectable()
@GameCommandHandler(0x4132)
export class CommitOutfitHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    if (session.characterId === null) {
      // The reply is a table, not a result code: staying silent stalls the
      // screen, but a session with no character cannot have reached it.
      return;
    }
    await sendPacket(session, 0x4133, GEAR_PAYLOAD);
  }
}
