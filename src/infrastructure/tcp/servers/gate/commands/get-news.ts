import { injectable, inject } from "@needle-di/core";
import type { ICommandHandler } from "../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../core/tcp/types/packet-type.ts";
import { GateCommandHandler } from "../../../../../core/tcp/decorators/gate-command-handler-decorator.ts";
import { PacketWriter } from "../../../../../core/tcp/utils/packet-builder-util.ts";
import {
  sendPacket,
  sendStartEndPacket,
  sendError,
} from "../../../../../core/tcp/utils/session-helpers-util.ts";
import { NewsService } from "../../../../../modules/news/news-service.ts";
import { ERROR_GENERAL } from "../../../../../core/constants/error-codes-constants.ts";
import { MAX_PAYLOAD_LENGTH } from "../../../../../core/tcp/types/packet-type.ts";
import type { NewsItem } from "../../../../../db/schema.ts";
import { createLogger } from "../../../../../core/tcp/utils/logger-util.ts";

type Logger = ReturnType<typeof createLogger>;

const NEWS_TOPIC_LENGTH = 128;

function buildNewsItemPayload(newsItem: NewsItem): Uint8Array {
  const writer = new PacketWriter();
  const messageBytes = new TextEncoder().encode(newsItem.message);
  writer.writeUint32(newsItem.id);
  writer.writeUint8(newsItem.important ? 1 : 0);
  writer.writeUint32(newsItem.time);
  writer.writeFixedString(newsItem.topic, NEWS_TOPIC_LENGTH);
  writer.writeBytes(messageBytes);
  writer.writeUint8(0);
  return writer.build();
}

@injectable()
@GateCommandHandler(0x2008)
export class GetNewsHandler implements ICommandHandler {
  private log: Logger | null = null;

  constructor(private newsService = inject(NewsService)) {}

  setLogger(log: Logger): void {
    this.log = log;
  }

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    try {
      await sendStartEndPacket(session, 0x2009);

      const newsItems = await this.newsService.findAll();
      for (const newsItem of newsItems) {
        const payload = buildNewsItemPayload(newsItem);
        if (payload.length > MAX_PAYLOAD_LENGTH) {
          throw new Error(
            `news item ${newsItem.id} exceeds max payload size (${payload.length} > ${MAX_PAYLOAD_LENGTH})`,
          );
        }
        await sendPacket(session, 0x200a, payload);
      }
      await sendStartEndPacket(session, 0x200b);
    } catch (error) {
      this.log?.error("getNews error:", error);
      await sendError(session, 0x2009, ERROR_GENERAL);
    }
  }
}
