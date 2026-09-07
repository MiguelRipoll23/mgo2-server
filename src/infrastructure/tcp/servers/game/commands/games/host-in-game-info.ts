import { inject, injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

// In-game settings edit (0x43c0 → 0x43c1). The host edits its game in place
// from the lobby-side screen; the packet is a strict subset of the 968-byte
// 0x4310 settings struct — name, comment, password flag, password, stance —
// and stops there.
//
// THE STANCE OFFSET IS NOT THE ONE 0x4310 USES: here it lands at 0xA1, where
// in 0x4310 the same offset means `dedicated`. Reusing the other packet's
// offset would write the wrong field.
const NAME_OFFSET = 0x00;
const NAME_LENGTH = 16;
const COMMENT_OFFSET = 0x10;
const COMMENT_LENGTH = 128;
const PASSWORD_FLAG_OFFSET = 0x90;
const PASSWORD_OFFSET = 0x91;
const PASSWORD_LENGTH = 16;
const STANCE_OFFSET = 0xa1;

@injectable()
@GameCommandHandler(0x43c0)
export class HostInGameInfoHandler implements ICommandHandler {
  constructor(private gameService = inject(GameService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const gameId = session.gameId;
    const characterId = session.characterId;
    const game = gameId !== null ? await this.gameService.findById(gameId) : null;

    if (game !== null && characterId !== null && game.host_id === characterId) {
      const bytes = packet.payload;
      const name = readField(bytes, NAME_OFFSET, NAME_LENGTH);
      const comment = readField(bytes, COMMENT_OFFSET, COMMENT_LENGTH);
      const passwordEnabled =
        bytes.length > PASSWORD_FLAG_OFFSET && bytes[PASSWORD_FLAG_OFFSET] !== 0;
      const password = passwordEnabled
        ? readField(bytes, PASSWORD_OFFSET, PASSWORD_LENGTH)
        : "";
      const stance = bytes.length > STANCE_OFFSET ? bytes[STANCE_OFFSET] : 0;

      await this.gameService.update(game.id, {
        name: name || game.name,
        comment,
        password,
        stance,
      });
    }

    await sendResult(session, 0x43c1, RESULT_NONE);
  }
}

/** Reads a fixed-width, NUL-terminated ISO-8859-1 field. */
function readField(bytes: Uint8Array, offset: number, max: number): string {
  if (offset >= bytes.length) return "";
  const limit = Math.min(offset + max, bytes.length);
  let end = offset;
  while (end < limit && bytes[end] !== 0) end++;
  let text = "";
  for (let i = offset; i < end; i++) text += String.fromCharCode(bytes[i]);
  return text;
}
