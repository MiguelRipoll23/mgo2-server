import { injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

// The gear catalogue (0x4124 connect burst, echoed by 0x4133), per the
// reference's ELF-traced parser — the payload is exactly:
//
//   u32 count
//   count × { u8 item_id, u32 colour_mask }   (5 bytes each)
//   16 × { u8 item_id, u8 bit_index }         (32-byte colour-highlight tail)
//
// The old layout here (35-byte camo header, u8 count, 0xff terminator, 30
// zeros and a 0x3370 trailer) reads as a count of 0x24242424 to the client's
// parser and is wrong. There is no camo section in this packet.
//
// Two gates, both server-controlled (confirmed live in the reference):
//  - item ownership: an id absent from this packet is never listed in the
//    wardrobe, so serving every item is what "everything unlocked" means;
//  - colour availability: record mask & (1 << slot) per swatch. Colour bits
//    are PER-ITEM SLOTS — the client's catalogue (0x10506BC) maps each item's
//    slots to colour names, and slots beyond an item's count are skipped
//    before the mask is read. The correct all-unlocked mask is therefore each
//    item's real legal mask (1 << n) - 1, not 0xffffffff — which was harmless
//    and meaningless at once.
//
// The 67 ids below are exactly what the wardrobe can render: nine fixed
// {base, count} windows from the client's 9-arm table (0x9270AC), 67 ids with
// a maximum of 116. Everything else is a phantom id the wardrobe never asks
// for, so it is not sent. Five None ids (28, 68, 86, 102) are hardcoded
// always-available client-side and carry no colour records (mask 0).

// 21-slot camo family — 39 items.
const MASK_CAMO_21 = 0x1fffff;
const CAMO_21_ITEMS = [
  11, 22, 29, 30, 31, 32, 34, 35, 36, 37,
  57, 58, 59, 60, 61, 62,
  69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80,
  87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97,
];

// 10-slot solid-colour family — 6 items.
const MASK_SOLID_10 = 0x3ff;
const SOLID_10_ITEMS = [12, 13, 33, 38, 104, 108];

// 8-slot family (skips Green and Maroon) — 3 items.
const MASK_EIGHT = 0xff;
const EIGHT_ITEMS = [105, 109, 111];

// Lens families — goggles (6 slots) and eye wear (5 slots).
const MASK_LENS_6 = 0x3f;
const LENS_6_ITEMS = [103];
const MASK_LENS_5 = 0x1f;
const LENS_5_ITEMS = [106, 107];

// Scarf family — Shemagh (5 slots).
const MASK_SCARF_5 = 0x1f;
const SCARF_5_ITEMS = [113];

// Single-colour items (slot 0, the null colour name) — 10 items.
const MASK_SINGLE = 0x1;
const SINGLE_ITEMS = [46, 47, 48, 49, 50, 51, 112, 114, 115, 116];

// The one 24-slot item — widest in the game.
const MASK_SCARF_24 = 0xffffff;
const SCARF_24_ITEMS = [110];

// None entries: hardcoded always-available, no colour records at all.
const MASK_NONE = 0x0;
const NONE_ITEMS = [28, 68, 86, 102];

type GearEntry = { id: number; mask: number };

const GEAR_CATALOGUE: GearEntry[] = [
  ...CAMO_21_ITEMS.map((id) => ({ id, mask: MASK_CAMO_21 })),
  ...SOLID_10_ITEMS.map((id) => ({ id, mask: MASK_SOLID_10 })),
  ...EIGHT_ITEMS.map((id) => ({ id, mask: MASK_EIGHT })),
  ...LENS_6_ITEMS.map((id) => ({ id, mask: MASK_LENS_6 })),
  ...LENS_5_ITEMS.map((id) => ({ id, mask: MASK_LENS_5 })),
  ...SCARF_5_ITEMS.map((id) => ({ id, mask: MASK_SCARF_5 })),
  ...SCARF_24_ITEMS.map((id) => ({ id, mask: MASK_SCARF_24 })),
  ...SINGLE_ITEMS.map((id) => ({ id, mask: MASK_SINGLE })),
  ...NONE_ITEMS.map((id) => ({ id, mask: MASK_NONE })),
].sort((a, b) => a.id - b.id);

// The 32-byte tail: sixteen {item, bit} colour-highlight pairs. These GRANT
// NOTHING — the parser ORs a pair into record+16 only if the bit is already
// set in the colour mask, and +16 drives a wardrobe highlight, not
// availability. With everything unlocked there is nothing to highlight, so
// the slots keep the 0xff filler, which the parser provably skips (item 255
// exceeds its 128-entry bound). This makes the payload byte-identical to a
// character with no highlight rows.
const HIGHLIGHT_SLOTS = 16;

export function buildGearPayload(): Uint8Array {
  const writer = new PacketWriter();

  writer.writeUint32(GEAR_CATALOGUE.length);
  for (const { id, mask } of GEAR_CATALOGUE) {
    writer.writeUint8(id);
    writer.writeUint32(mask);
  }
  for (let slot = 0; slot < HIGHLIGHT_SLOTS; slot++) {
    writer.writeUint8(0xff);
    writer.writeUint8(0xff);
  }

  return writer.build();
}

// Pre-built payload — the catalogue is static, no per-character lookup needed.
export const GEAR_PAYLOAD = buildGearPayload();

@injectable()
@GameCommandHandler(0x4124)
export class GetGearHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendPacket(session, 0x4124, GEAR_PAYLOAD);
  }
}
