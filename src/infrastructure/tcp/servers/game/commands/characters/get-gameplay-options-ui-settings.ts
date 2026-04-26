import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

const DEFAULT_GAMEPLAY_OPTIONS =
  '{"onlineStatusMode":0,"emailFriendsOnly":false,"receiveNotices":true,"receiveInvites":true,"normalViewVerticalInvert":false,"normalViewHorizontalInvert":false,"normalViewSpeed":5,"shoulderViewVerticalInvert":false,"shoulderViewHorizontalInvert":false,"shoulderViewSpeed":5,"firstViewVerticalInvert":false,"firstViewHorizontalInvert":false,"firstViewSpeed":5,"firstViewPlayerDirection":true,"viewChangeSpeed":5,"firstViewMemory":false,"radarLockNorth":false,"radarFloorHide":false,"hudDisplaySize":0,"hudHideNameTags":false,"lockOnEnabled":false,"weaponSwitchMode":2,"weaponSwitchA":0,"weaponSwitchB":1,"weaponSwitchC":2,"weaponSwitchNow":0,"weaponSwitchBefore":1,"itemSwitchMode":2,"codec1Name":"","codec1a":1,"codec1b":3,"codec1c":4,"codec1d":2,"codec2Name":"","codec2a":10,"codec2b":12,"codec2c":13,"codec2d":11,"codec3Name":"","codec3a":14,"codec3b":16,"codec3c":17,"codec3d":15,"codec4Name":"","codec4a":5,"codec4b":7,"codec4c":8,"codec4d":6,"voiceChatRecognitionLevel":5,"voiceChatVolume":5,"headsetVolume":5,"bgmVolume":10}';

const GAMEPLAY_UI_SETTINGS_FIXED = new Uint8Array([
  0x01, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x10, 0x11, 0x10, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
  0x00, 0x00,
]);

function parseGameplayOptions(json: string): Record<string, unknown> {
  try {
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return JSON.parse(DEFAULT_GAMEPLAY_OPTIONS) as Record<string, unknown>;
  }
}

function buildGameplayOptionsUiSettingsPayload(
  gameplayOptions: string,
): Uint8Array {
  const data = parseGameplayOptions(gameplayOptions);

  const onlineStatusMode = (data.onlineStatusMode as number) ?? 0;
  const emailFriendsOnly = (data.emailFriendsOnly as boolean) ?? false;
  const receiveNotices = (data.receiveNotices as boolean) ?? true;
  const receiveInvites = (data.receiveInvites as boolean) ?? true;

  const normalViewVerticalInvert =
    (data.normalViewVerticalInvert as boolean) ?? false;
  const normalViewHorizontalInvert =
    (data.normalViewHorizontalInvert as boolean) ?? false;
  const normalViewSpeed = (data.normalViewSpeed as number) ?? 5;
  const shoulderViewVerticalInvert =
    (data.shoulderViewVerticalInvert as boolean) ?? false;
  const shoulderViewHorizontalInvert =
    (data.shoulderViewHorizontalInvert as boolean) ?? false;
  const shoulderViewSpeed = (data.shoulderViewSpeed as number) ?? 5;
  const firstViewVerticalInvert =
    (data.firstViewVerticalInvert as boolean) ?? false;
  const firstViewHorizontalInvert =
    (data.firstViewHorizontalInvert as boolean) ?? false;
  const firstViewSpeed = (data.firstViewSpeed as number) ?? 5;
  const firstViewPlayerDirection =
    (data.firstViewPlayerDirection as boolean) ?? true;
  const viewChangeSpeed = ((data.viewChangeSpeed as number) ?? 5) - 1;
  const firstViewMemory = (data.firstViewMemory as boolean) ?? false;
  const radarLockNorth = (data.radarLockNorth as boolean) ?? false;
  const radarFloorHide = (data.radarFloorHide as boolean) ?? false;
  const hudDisplaySize = (data.hudDisplaySize as number) ?? 0;
  const hudHideNameTags = (data.hudHideNameTags as boolean) ?? false;
  const lockOnEnabled = (data.lockOnEnabled as boolean) ?? false;
  const bgmVolume = ((data.bgmVolume as number) ?? 10) + 1;

  const weaponSwitchMode = (data.weaponSwitchMode as number) ?? 2;
  const weaponSwitchA = (data.weaponSwitchA as number) ?? 0;
  const weaponSwitchB = (data.weaponSwitchB as number) ?? 1;
  const weaponSwitchC = (data.weaponSwitchC as number) ?? 2;
  const weaponSwitchNow = (data.weaponSwitchNow as number) ?? 0;
  const weaponSwitchBefore = (data.weaponSwitchBefore as number) ?? 1;
  const itemSwitchMode = (data.itemSwitchMode as number) ?? 2;

  const voiceChatRecognitionLevel =
    (data.voiceChatRecognitionLevel as number) ?? 5;
  const voiceChatVolume = (data.voiceChatVolume as number) ?? 5;
  const headsetVolume = (data.headsetVolume as number) ?? 5;

  const codec1Name = (data.codec1Name as string) ?? "";
  const codec1a = (data.codec1a as number) ?? 1;
  const codec1b = (data.codec1b as number) ?? 3;
  const codec1c = (data.codec1c as number) ?? 4;
  const codec1d = (data.codec1d as number) ?? 2;
  const codec2Name = (data.codec2Name as string) ?? "";
  const codec2a = (data.codec2a as number) ?? 10;
  const codec2b = (data.codec2b as number) ?? 12;
  const codec2c = (data.codec2c as number) ?? 13;
  const codec2d = (data.codec2d as number) ?? 11;
  const codec3Name = (data.codec3Name as string) ?? "";
  const codec3a = (data.codec3a as number) ?? 14;
  const codec3b = (data.codec3b as number) ?? 16;
  const codec3c = (data.codec3c as number) ?? 17;
  const codec3d = (data.codec3d as number) ?? 15;
  const codec4Name = (data.codec4Name as string) ?? "";
  const codec4a = (data.codec4a as number) ?? 5;
  const codec4b = (data.codec4b as number) ?? 7;
  const codec4c = (data.codec4c as number) ?? 8;
  const codec4d = (data.codec4d as number) ?? 6;

  const privacyA =
    1 | ((onlineStatusMode & 0b11) << 4) | (emailFriendsOnly ? 0b01000000 : 0);
  const privacyB = (receiveNotices ? 0b1 : 0) | (receiveInvites ? 0b10000 : 0);

  const normalView =
    (normalViewVerticalInvert ? 0b1 : 0) |
    (normalViewHorizontalInvert ? 0b10 : 0) |
    (((normalViewSpeed - 1) & 0b1111) << 4);

  const shoulderView =
    (shoulderViewVerticalInvert ? 0b1 : 0) |
    (shoulderViewHorizontalInvert ? 0b10 : 0) |
    (((shoulderViewSpeed - 1) & 0b1111) << 4);

  const firstView =
    (firstViewVerticalInvert ? 0b1 : 0) |
    (firstViewHorizontalInvert ? 0b10 : 0) |
    (((firstViewSpeed - 1) & 0b1111) << 4) |
    (firstViewPlayerDirection ? 0b100 : 0);

  const firstViewMemoryByte = firstViewMemory ? 0b10 : 0;
  const radar = (radarLockNorth ? 0b1 : 0) | (radarFloorHide ? 0b10000 : 0);
  const hudDisplay = (hudDisplaySize & 0b11) | (hudHideNameTags ? 0b10000 : 0);
  const lockOnAndBGM = (lockOnEnabled ? 0b1 : 0) | ((bgmVolume & 0b1111) << 4);

  const weaponSwitchAByte =
    (weaponSwitchA & 0b1111) | ((weaponSwitchB & 0b1111) << 4);
  const weaponSwitchBByte = weaponSwitchC & 0b1111;
  const weaponSwitchRecall =
    (weaponSwitchBefore & 0b1111) | ((weaponSwitchNow & 0b1111) << 4);
  const switchModes =
    (weaponSwitchMode & 0b1111) | ((itemSwitchMode & 0b1111) << 4);

  const voiceChatA = 1 | ((voiceChatRecognitionLevel & 0b1111) << 4);
  const voiceChatB =
    (voiceChatVolume & 0b1111) | ((headsetVolume & 0b1111) << 4);

  const writer = new PacketWriter();
  writer.writeUint8(privacyA);
  writer.writeUint8(normalView);
  writer.writeUint8(shoulderView);
  writer.writeUint8(firstView);
  writer.writeUint8(viewChangeSpeed);
  writer.writePadding(6);
  writer.writeUint8(switchModes);
  writer.writeUint8(0);
  writer.writeUint8(voiceChatA);
  writer.writeUint8(voiceChatB);
  writer.writeUint8(weaponSwitchAByte);
  writer.writeUint8(weaponSwitchBByte);
  writer.writeUint8(weaponSwitchRecall);
  writer.writeUint8(firstViewMemoryByte);
  writer.writeUint8(privacyB);
  writer.writeUint8(lockOnAndBGM);
  writer.writeUint8(radar);
  writer.writeUint8(hudDisplay);
  writer.writePadding(9);
  writer.writeUint8(codec1a);
  writer.writeUint8(codec1b);
  writer.writeUint8(codec1c);
  writer.writeUint8(codec1d);
  writer.writeUint8(codec2a);
  writer.writeUint8(codec2b);
  writer.writeUint8(codec2c);
  writer.writeUint8(codec2d);
  writer.writeUint8(codec3a);
  writer.writeUint8(codec3b);
  writer.writeUint8(codec3c);
  writer.writeUint8(codec3d);
  writer.writeUint8(codec4a);
  writer.writeUint8(codec4b);
  writer.writeUint8(codec4c);
  writer.writeUint8(codec4d);
  writer.writeFixedString(codec1Name, 64);
  writer.writeFixedString(codec2Name, 64);
  writer.writeFixedString(codec3Name, 64);
  writer.writeFixedString(codec4Name, 64);
  writer.writeBytes(GAMEPLAY_UI_SETTINGS_FIXED);

  return writer.build();
}

@injectable()
@GameCommandHandler(0x411b)
export class GetGameplayOptionsUiSettingsHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId === null) {
      await sendPacket(session, 0x4120);
      return;
    }

    const character = await this.characterService.findById(characterId);
    const gameplayOptions =
      character?.gameplay_options ?? DEFAULT_GAMEPLAY_OPTIONS;
    const payload = buildGameplayOptionsUiSettingsPayload(gameplayOptions);
    await sendPacket(session, 0x4120, payload);
  }
}
