import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_GENERAL, RESULT_INVALID_SESSION } from "../../../../../../core/constants/error-codes-constants.ts";

// The 0x4316 payload is ONE byte (a settings-type selector). Name, password,
// comment, rotation and rule/map arrive earlier via the Blowfish-encrypted
// 0x4310 push, which we persist per character — reading 163 bytes here would
// read past the payload into stale receive buffer.
const SETTINGS_TYPE_DEFAULT = 0;

@injectable()
@GameCommandHandler(0x4316)
export class CreateGameHandler implements ICommandHandler {
  constructor(
    private gameService = inject(GameService),
    private characterService = inject(CharacterService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const characterId = session.characterId;
    const lobbyId = session.lobbyId;

    if (characterId === null || lobbyId === null) {
      await sendPacket(
        session,
        0x4317,
        new PacketWriter().writeUint32(RESULT_INVALID_SESSION).build(),
      );
      return;
    }

    // Settings type selector from the 1-byte payload (0 when absent).
    const settingsType =
      packet.payload.length > 0 ? packet.payload[0] : SETTINGS_TYPE_DEFAULT;

    // Pull the stored 0x4310 blob for this character + settings type.
    const settingsRows = await this.characterService.getHostSettings(
      characterId,
    );
    const pushed =
      settingsRows.find((row) => row.type === settingsType) ??
        settingsRows[0] ??
        null;

    // 0x4310 blob layout: name[16] @0x00, comment[128] @0x10,
    // passwordEnabled u8 @0x90, password[16] @0x91.
    let name = "";
    let comment = "";
    let password = "";
    let maxPlayers = 8;
    let rotation: number[][] = [];
    if (pushed !== null) {
      const blob = decodeSettingsBlob(pushed.settings);
      if (blob) {
        name = blob.name;
        comment = blob.comment;
        password = blob.passwordEnabled ? blob.password : "";
        maxPlayers = blob.maxPlayers > 0 ? blob.maxPlayers : 8;
        rotation = blob.rotation;
      }
    }

    const game = await this.gameService.create({
      host_id: characterId,
      lobby_id: lobbyId,
      name: name || `Game_${characterId}`,
      password,
      comment,
      max_players: maxPlayers,
      games: JSON.stringify(rotation),
    });

    // The host is the game's first roster member — the roster row is what
    // carries its ping, team slot and round attribution.
    await this.gameService.addPlayer(game.id, characterId);

    session.gameId = game.id;

    // Reply is 8 bytes: {s32 result, u32 gameId}. The client's parser reads
    // the second u32 BEFORE testing the result, so a 4-byte "game id only"
    // reply puts the id in the result slot and breaks the screen.
    const writer = new PacketWriter()
      .writeUint32(RESULT_NONE_OK)
      .writeUint32(game.id);
    await sendPacket(session, 0x4317, writer.build());
  }
}

const RESULT_NONE_OK = 0;

/** Decodes the stored JSON 0x4310 settings blob. */
function decodeSettingsBlob(settingsJson: string): {
  name: string;
  comment: string;
  passwordEnabled: boolean;
  password: string;
  maxPlayers: number;
  rotation: number[][];
} | null {
  try {
    const raw = JSON.parse(settingsJson) as unknown;
    const bytes = Array.isArray(raw)
      ? Uint8Array.from(raw as number[])
      : null;
    if (!bytes || bytes.length < 0xf9) return null;
    return {
      name: readNulString(bytes, 0x00, 16),
      comment: readNulString(bytes, 0x10, 128),
      passwordEnabled: bytes[0x90] !== 0,
      password: readNulString(bytes, 0x91, 16),
      maxPlayers: bytes[0xe5],
      rotation: parseRotation(bytes),
    };
  } catch {
    return null;
  }
}

/** Reads a NUL-terminated ISO-8859-1 string from a fixed-width field. */
function readNulString(bytes: Uint8Array, offset: number, length: number): string {
  const chars: string[] = [];
  for (let i = 0; i < length && offset + i < bytes.length; i++) {
    const byte = bytes[offset + i];
    if (byte === 0) break;
    chars.push(String.fromCharCode(byte));
  }
  return chars.join("");
}

/**
 * Rotation: 16 interleaved {rule, map, flags} triples at 0xA3..0xD2. A
 * rule==0 && map==0 triple is the conventional terminator.
 */
function parseRotation(bytes: Uint8Array): number[][] {
  const rotation: number[][] = [];
  const base = 0xa3;
  for (let i = 0; i < 16 && base + i * 3 + 2 < bytes.length; i++) {
    const rule = bytes[base + i * 3];
    const map = bytes[base + i * 3 + 1];
    const flags = bytes[base + i * 3 + 2];
    if (rule === 0 && map === 0) break;
    rotation.push([rule, map, flags]);
  }
  return rotation;
}
