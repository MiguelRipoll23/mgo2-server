import { z } from "@hono/zod-openapi";

const uint8Field = (description: string, defaultValue: number) =>
  z.number().int().min(0).max(0xff).default(defaultValue).openapi({
    example: defaultValue,
    description,
  });

export const flashNewsBroadcastRequestSchema = z.object({
  message: z.string().min(1).max(255).openapi({
    example: "Survival (EU) begins in 30 minutes.",
    description: "Ticker text sent to active game-lobby clients.",
  }),
  unknown1: uint8Field(
    "Byte 1 of the 2-byte field at the start of the 0x4A50 payload (purpose unknown)",
    0x00,
  ),
  unknown2: uint8Field(
    "Byte 2 of the 2-byte field at the start of the 0x4A50 payload (purpose unknown)",
    0x00,
  ),
  unknown5: uint8Field(
    "Byte 5 of the 0x4A50 payload (purpose unknown)",
    0x01,
  ),
  unknown6: uint8Field(
    "Byte 6 of the 0x4A50 payload (purpose unknown)",
    0x00,
  ),
}).openapi("FlashNewsBroadcastRequest");

export const flashNewsEmergencyRequestSchema = z.object({
  maintenanceTime: uint8Field(
    "Maintenance start time encoded in byte 6 of the 0x4A50 payload. " +
      "The exact encoding formula is not yet known.",
    0x00,
  ),
  unknown1: uint8Field(
    "Byte 1 of the 2-byte field at the start of the 0x4A50 payload (purpose unknown)",
    0x00,
  ),
  unknown2: uint8Field(
    "Byte 2 of the 2-byte field at the start of the 0x4A50 payload (purpose unknown)",
    0x00,
  ),
  unknown5: uint8Field(
    "Byte 5 of the 0x4A50 payload (purpose unknown)",
    0x01,
  ),
}).openapi("FlashNewsEmergencyRequest");
