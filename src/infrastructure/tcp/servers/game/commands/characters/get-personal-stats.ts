import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { CharacterStatsService } from "../../../../../../modules/character/character-stats-service.ts";
import type { CharacterStats } from "../../../../../../db/schema.ts";

// Matches echo's STATS_BUFFER_SIZE
const STATS_BUFFER_SIZE = 1024;

// Offsets within the 0x4103 header packet
const EXP_OFFSET = 0x20;
const CLAN_TAG_OFFSET = 0x230;  // byte: clan status, then 15-byte clan tag string
const CLAN_TRAILER_OFFSET = 0x240; // int32: 0x00000100
const COMMENT_OFFSET = 0x29D;

// Aggregate stats block size per mode (18 int32 fields × 4 bytes)
const MODE_BLOCK_SIZE = 72;

@injectable()
@GameCommandHandler(0x4102)
export class GetPersonalStatsHandler implements ICommandHandler {
  constructor(
    private characterService = inject(CharacterService),
    private statsService = inject(CharacterStatsService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const reader = new PacketReader(packet.payload);
    const targetCharacterId = reader.readUint32();

    const [character, stats, clanInfo] = await Promise.all([
      targetCharacterId > 0 ? this.characterService.findById(targetCharacterId) : null,
      targetCharacterId > 0 ? this.statsService.findByCharacterId(targetCharacterId) : null,
      targetCharacterId > 0 ? this.characterService.getClanInfo(targetCharacterId) : null,
    ]);

    // ── Packet 0x4103: header (experience, comment) ──────────────────────────
    const header = new PacketWriter();
    header.writeUint32(0);                                    // 0x00: reserved
    header.writeUint32(targetCharacterId);                    // 0x04: character id
    header.writeFixedString(character?.name ?? "", 16);       // 0x08: name
    header.writePadding(EXP_OFFSET - header.size);            // pad to 0x20
    header.writeUint32(character?.experience ?? 0);           // 0x20: experience
    header.writePadding(CLAN_TAG_OFFSET - header.size);       // pad to 0x230
    // Clan tag: status byte + ";clanName" (15 bytes)
    const hasClan = clanInfo != null;
    const clanTag = hasClan ? `;${clanInfo!.clanName}` : "";
    header.writeUint8(hasClan ? 0x12 : 0x00);                 // 0x230: clan status
    header.writeFixedString(clanTag, 15);                     // 0x231: clan tag
    header.writePadding(CLAN_TRAILER_OFFSET - header.size);   // pad to 0x240
    header.writeUint32(0x00000100);                           // 0x240: clan trailer
    header.writePadding(COMMENT_OFFSET - header.size);        // pad to 0x29D
    header.writeFixedString(character?.comment ?? "", 128);   // 0x29D: comment
    header.writePadding(STATS_BUFFER_SIZE - header.size);

    await sendPacket(session, 0x4103, header.build());

    // ── Packet 0x4105: per-mode stats ────────────────────────────────────────
    const modePacket = new PacketWriter();
    modePacket.writePadding(80);
    writeAggregateBlock(modePacket, stats);
    for (let mode = 0; mode <= 10; mode++) {
      writeModeBlock(modePacket, stats, mode, this.statsService);
    }
    modePacket.writePadding(STATS_BUFFER_SIZE - modePacket.size);

    await sendPacket(session, 0x4105, modePacket.build());

    // ── Packet 0x4105 (second): extended stats with total score ──────────────
    const extended = new PacketWriter();
    extended.writeUint32(0);
    extended.writeUint32(1);
    extended.writePadding(844);
    extended.writeUint32(stats?.score ?? 0);                  // total reward/score
    extended.writePadding(STATS_BUFFER_SIZE - extended.size);

    await sendPacket(session, 0x4105, extended.build());

    // ── Packet 0x4107: additional stats ──────────────────────────────────────
    const additional = new PacketWriter();
    additional.writePadding(20);
    additional.writeUint32(stats?.stuns_received ?? 0);
    additional.writePadding(STATS_BUFFER_SIZE - additional.size);

    await sendPacket(session, 0x4107, additional.build());
  }
}

function writeAggregateBlock(writer: PacketWriter, stats: CharacterStats | null): void {
  if (!stats) {
    writer.writePadding(MODE_BLOCK_SIZE);
    return;
  }
  writer.writeUint32(stats.kills);
  writer.writeUint32(stats.deaths);
  writer.writeUint32(stats.lock_kills);
  writer.writeUint32(stats.score);
  writer.writeUint32(stats.stuns);
  writer.writeUint32(stats.stuns_received);
  writer.writeUint32(stats.headshot_kills);
  writer.writeUint32(stats.headshot_deaths);
  writer.writeUint32(stats.headshot_stuns);
  writer.writeUint32(stats.headshot_stuns_received);
  writer.writeUint32(stats.lock_stuns);
  writer.writeUint32(stats.lock_deaths);
  writer.writeUint32(stats.lock_stuns_received);
  writer.writeUint32(stats.score);
  writer.writeUint32(stats.rounds);
  writer.writeUint32(0);
  writer.writeUint32(stats.wins);
  writer.writeUint32(stats.time);
}

function writeModeBlock(
  writer: PacketWriter,
  stats: CharacterStats | null,
  mode: number,
  service: CharacterStatsService,
): void {
  if (!stats) {
    writer.writePadding(MODE_BLOCK_SIZE);
    return;
  }

  try {
    const m = service.getModeStats(stats, mode);
    writer.writeUint32(m.kills ?? 0);
    writer.writeUint32(m.deaths ?? 0);
    writer.writeUint32(m.lockKills ?? 0);
    writer.writeUint32(m.score ?? 0);
    writer.writeUint32(m.stuns ?? 0);
    writer.writeUint32(m.stunsRec ?? 0);
    writer.writeUint32(m.hsKills ?? 0);
    writer.writeUint32(m.hsDeaths ?? 0);
    writer.writeUint32(m.hsStuns ?? 0);
    writer.writeUint32(m.hsStunsRec ?? 0);
    writer.writeUint32(m.lockStuns ?? 0);
    writer.writeUint32(m.lockDeaths ?? 0);
    writer.writeUint32(m.lockStunsRec ?? 0);
    writer.writeUint32(m.score ?? 0);
    writer.writeUint32(m.rounds ?? 0);
    writer.writeUint32(0);
    writer.writeUint32(m.wins ?? 0);
    writer.writeUint32(m.time ?? 0);
  } catch {
    writer.writePadding(MODE_BLOCK_SIZE);
  }
}
