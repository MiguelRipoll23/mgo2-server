import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

import { injectable } from "@needle-di/core";

// Host reports a player left. The disconnect pair of 0x4340; the reply parser
// is the same {u32 result, u32 key} shape.
@injectable()
@GameCommandHandler(0x4342)
export class HostPlayerDisconnectedHandler implements ICommandHandler {
  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const reader = new PacketReader(packet.payload);
    const key = reader.remaining() >= 4 ? reader.readUint32() : 0;
    const payload = new PacketWriter()
      .writeUint32(RESULT_NONE)
      .writeUint32(key)
      .build();
    await sendPacket(session, 0x4343, payload);
  }
}

// Third peer-registration round-trip (P2P FSM state 0x217). Reached only after
// 0x4344 completes; unhandled, the FSM times out and disconnects the peer.
@injectable()
@GameCommandHandler(0x4346)
export class HostPlayerConnectFinishHandler implements ICommandHandler {
  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const reader = new PacketReader(packet.payload);
    const key = reader.remaining() >= 4 ? reader.readUint32() : 0;
    const payload = new PacketWriter()
      .writeUint32(RESULT_NONE)
      .writeUint32(key)
      .build();
    await sendPacket(session, 0x4347, payload);
  }
}

// Round end (0x43a2 is also answered in hub.ts — see the round-end handler
// there). Re-exported placeholder so both files' handlers stay discoverable.
export { RESULT_NONE };
