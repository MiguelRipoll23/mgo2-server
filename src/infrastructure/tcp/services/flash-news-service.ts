import { inject, injectable } from "@needle-di/core";
import { PacketWriter } from "../../../core/tcp/utils/packet-builder-util.ts";
import { sendPacket } from "../../../core/tcp/utils/session-helpers-util.ts";
import { ActiveGameSessionsService } from "./active-game-sessions-service.ts";

const FLASH_NEWS_COMMAND = 0x4a50;
const PREFIX_PADDING_LENGTH = 7;

/** Known subcommand values embedded in bytes 3–4 of the 0x4A50 payload. */
export const FlashNewsSubcommand = {
  /** Standard server/ticker message — message text is displayed. */
  SERVER_MESSAGE: 0x4a68,
  /** Emergency maintenance notice — message text is ignored by the client;
   *  `maintenanceTime` encodes the start time via an unknown formula. */
  EMERGENCY_MAINTENANCE: 0x4a71,
} as const;

export type FlashNewsSubcommandValue = typeof FlashNewsSubcommand[keyof typeof FlashNewsSubcommand];

export interface FlashNewsAnnouncement {
  message?: string;
  unknown1?: number;
  unknown2?: number;
  /** Two-byte subcommand that controls client behaviour.
   *  High byte → payload byte 3, low byte → payload byte 4.
   *  @see FlashNewsSubcommand */
  subcommandHex?: number;
  unknown5?: number;
  /** Maintenance start time (subcommand 0x4A71 only).
   *  The exact encoding formula is not yet known. */
  maintenanceTime?: number;
}

export interface FlashNewsBroadcastResult {
  recipients: number;
  payloadLength: number;
}

@injectable()
export class FlashNewsService {
  constructor(
    private readonly activeGameSessionsService = inject(ActiveGameSessionsService),
  ) {}

  buildPayload({
    message = "",
    unknown1 = 0x00,
    unknown2 = 0x00,
    subcommandHex = FlashNewsSubcommand.SERVER_MESSAGE,
    unknown5 = 0x01,
    maintenanceTime = 0x00,
  }: FlashNewsAnnouncement): Uint8Array {
    const subcmdHigh = (subcommandHex >> 8) & 0xff;
    const subcmdLow = subcommandHex & 0xff;
    const writer = new PacketWriter();
    writer
      .writeUint8(unknown1)
      .writeUint8(unknown2)
      .writeUint8(subcmdHigh)
      .writeUint8(subcmdLow)
      .writeUint8(unknown5)
      .writeUint8(maintenanceTime)
      .writePadding(PREFIX_PADDING_LENGTH)
      .writeBytes(new TextEncoder().encode(message));
    return writer.build();
  }

  async broadcast(
    announcement: FlashNewsAnnouncement,
  ): Promise<FlashNewsBroadcastResult> {
    const payload = this.buildPayload(announcement);
    const sessions = this.activeGameSessionsService.list();

    await Promise.all(
      sessions.map((session) => sendPacket(session, FLASH_NEWS_COMMAND, payload)),
    );

    return {
      recipients: sessions.length,
      payloadLength: payload.length,
    };
  }
}
