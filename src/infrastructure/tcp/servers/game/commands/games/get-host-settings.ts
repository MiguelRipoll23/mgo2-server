import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

const HOST_SETTINGS_RESPONSE_SIZE = 128;

@injectable()
@GameCommandHandler(0x4304)
export class GetHostSettingsHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const characterId = session.characterId ?? 0;
    const settingsList =
      characterId > 0
        ? await this.characterService.getHostSettings(characterId)
        : [];

    const writer = new PacketWriter();
    if (settingsList.length > 0) {
      const settingsBytes = new TextEncoder().encode(settingsList[0].settings);
      const paddedSize = Math.max(
        HOST_SETTINGS_RESPONSE_SIZE,
        settingsBytes.length + 4,
      );
      writer.writeUint32(settingsList[0].type);
      writer.writeBytes(settingsBytes.slice(0, paddedSize - 4));
      writer.writePadding(Math.max(0, paddedSize - 4 - settingsBytes.length));
    } else {
      writer.writePadding(HOST_SETTINGS_RESPONSE_SIZE);
    }

    await sendPacket(session, 0x4305, writer.build());
  }
}

@injectable()
@GameCommandHandler(0x4310)
export class CheckHostSettingsHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId !== null && packet.payload.length >= 4) {
      const reader = new PacketReader(packet.payload);
      const settingsType = reader.readUint32();
      const remainingBytes = reader.readBytes(packet.payload.length - 4);
      const settingsJson = JSON.stringify(Array.from(remainingBytes));
      await this.characterService.updateHostSettings(
        characterId,
        settingsType,
        settingsJson,
      );
    }
    await sendPacket(session, 0x4311, null);
  }
}
