import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

import { injectable } from "@needle-di/core";

// The host's per-peer P2P state machine issues THREE blocking
// register-with-server round-trips — 0x4340, 0x4344, 0x4346. All three reply
// parsers read {u32 result, u32 key} and bail on a short read, so an empty ack
// stalls the FSM until its 30-second deadline fires and it disconnects the
// peer. The key must be the host's own peer-table index, which is the leading
// u32 of the request — echoing the request's first word is correct for every
// phase.

async function replyWithEchoedKey(
  session: TcpSession,
  replyCommand: number,
  packet: Packet,
): Promise<void> {
  const reader = new PacketReader(packet.payload);
  const key = reader.remaining() >= 4 ? reader.readUint32() : 0;
  const payload = new PacketWriter()
    .writeUint32(RESULT_NONE)
    .writeUint32(key)
    .build();
  await sendPacket(session, replyCommand, payload);
}

// Host reports a player's P2P connection to it succeeded — sent only once the
// P2P link actually forms; its arrival is the first proof a join reached the host.
@injectable()
@GameCommandHandler(0x4340)
export class HostPlayerConnectedHandler implements ICommandHandler {
  async handle(session: TcpSession, packet: Packet): Promise<void> {
    await replyWithEchoedKey(session, 0x4341, packet);
  }
}

// Second peer-registration round-trip — not merely "set team".
@injectable()
@GameCommandHandler(0x4344)
export class HostSetPlayerTeamHandler implements ICommandHandler {
  async handle(session: TcpSession, packet: Packet): Promise<void> {
    await replyWithEchoedKey(session, 0x4345, packet);
  }
}
