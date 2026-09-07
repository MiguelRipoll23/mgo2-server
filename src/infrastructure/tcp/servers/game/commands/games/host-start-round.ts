import { inject, injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

// Start round, host side. Two client builds, two id pairings — both handled,
// each request answered on exactly the id its own build parses:
//
// 1.36 build (comradesean ELF scan): builds 0x43c8, parses 0x43c9.
// This repo's MGO2.ELF (capstone, PPC64 BE): li r4,0x43ca builder at 0xF0E260
// (payload = a single u8, stb into the request buffer) whose request slot 0x7f
// is completed by the 0x43cb waiter at 0xF0D4C0 (cmpwi 0x43cb in the reply
// dispatcher at 0xF04994; the waiter reads the first u32 of the reply as the
// result). So here the pairing is 0x43ca -> 0x43cb.
//
// One reply per request, never both: the client completes the request slot on
// the first reply and treats the second as unexpected (the double-0x4b71
// failure the reference documents).
const START_ROUND = 0x43c8;
const START_ROUND_RESULT = 0x43c9;
const START_ROUND_ALIAS = 0x43ca;
const START_ROUND_ALIAS_RESULT = 0x43cb;

@injectable()
@GameCommandHandler(START_ROUND)
@GameCommandHandler(START_ROUND_ALIAS)
export class HostStartRoundHandler implements ICommandHandler {
  constructor(private gameService = inject(GameService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    // Snapshot the roster: everyone in the game now "played this round",
    // which is what the attribution checks for the host's end-of-round
    // reports (0x4390/0x43a2/0x43a4) consult before applying stats to a
    // character — including one who quits mid-round.
    const gameId = session.gameId;
    if (gameId !== null) {
      await this.gameService.markRoundPlayers(gameId);
    }

    // Reply is {u32 result, u32 token}. The second word must be ZERO: the
    // client stores a nonzero value into profile+0x32F8 and republishes it to
    // every peer, where it gates the instructor-recognition prompt ("an
    // instructor is already saved"). It is not a round handle. The 0x43cb
    // waiter of this build visibly consumes only the first word (lwa of the
    // result); the second is unread but harmless.
    const payload = new PacketWriter()
      .writeUint32(RESULT_NONE)
      .writeUint32(0)
      .build();

    // Paired reply only: 0x43c8 -> 0x43c9 (1.36), 0x43ca -> 0x43cb (this ELF).
    const replyCommand = packet.header.command === START_ROUND_ALIAS
      ? START_ROUND_ALIAS_RESULT
      : START_ROUND_RESULT;
    await sendPacket(session, replyCommand, payload);
  }
}
