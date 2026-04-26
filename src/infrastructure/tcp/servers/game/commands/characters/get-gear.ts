import { injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

// All unlockable gear item IDs (1-byte each), matching the reference protocol.
// Items with gear_slot > 255 from the shop catalog belong to other packet types
// (faces, character creation, etc.) and are NOT sent here.
export const GEAR_ITEMS = new Uint8Array([
  0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
  0x0b, 0x0c, 0x0d, 0x0e, 0x0f, 0x10, 0x11, 0x12, 0x13, 0x16, 0x17, 0x18,
  0x19, 0x1a, 0x1b, 0x1c, 0x1d, 0x1e, 0x1f, 0x20, 0x21, 0x22, 0x23, 0x24,
  0x25, 0x26, 0x28, 0x29, 0x2a, 0x2b, 0x2c, 0x2e, 0x2f, 0x30, 0x31, 0x32,
  0x33, 0x34, 0x35, 0x39, 0x3a, 0x3b, 0x3c, 0x3d, 0x3e, 0x3f, 0x40, 0x44,
  0x45, 0x46, 0x47, 0x48, 0x49, 0x4a, 0x4b, 0x4c, 0x4d, 0x4e, 0x4f, 0x50,
  0x51, 0x52, 0x53, 0x56, 0x57, 0x58, 0x59, 0x5a, 0x5b, 0x5c, 0x5d, 0x5e,
  0x5f, 0x60, 0x61, 0x62, 0x63, 0x64, 0x66, 0x67, 0x68, 0x69, 0x6a, 0x6b,
  0x6c, 0x6d, 0x6e, 0x6f, 0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77,
  0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a, 0x8b,
  0x8c, 0x8d, 0x8e, 0x8f, 0xa0, 0xa1, 0xa2, 0xb0, 0xc0, 0xc1, 0xc2, 0xc3,
  0xc4, 0xf0, 0xf1, 0xf2, 0xf3, 0xf4,
]);

// 35-byte weapon camos header:
//   bytes  0-9:  0x24 (White camo for MK.2)
//   byte   10:   0x08 (M4 Maroon)
//   byte   11:   0x1C (AK World Champion)
//   bytes 12-34: 0x00 (reserved)
const CAMO_HEADER = new Uint8Array(35);
CAMO_HEADER.fill(0x24, 0, 10);
CAMO_HEADER[10] = 0x08;
CAMO_HEADER[11] = 0x1c;

const GEAR_TERMINATOR = new Uint8Array(32).fill(0xff);

export function buildGearPayload(): Uint8Array {
  const writer = new PacketWriter();

  // 35-byte weapon camos header
  writer.writeBytes(CAMO_HEADER);

  // Item count (byte 35)
  writer.writeUint8(GEAR_ITEMS.length);

  // Items: 1-byte ID + 4-byte color bitmask (0xffffffff = all colors unlocked)
  for (const id of GEAR_ITEMS) {
    writer.writeUint8(id);
    writer.writeUint32(0xffffffff);
  }

  // 32-byte 0xFF terminator
  writer.writeBytes(GEAR_TERMINATOR);

  // 30 zero bytes + 0x3370 trailing short
  writer.writePadding(30);
  writer.writeUint16(0x3370);

  return writer.build();
}

// Pre-built payload — gear list is static, no per-character lookup needed.
export const GEAR_PAYLOAD = buildGearPayload();

@injectable()
@GameCommandHandler(0x4124)
export class GetGearHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendPacket(session, 0x4124, GEAR_PAYLOAD);
  }
}
