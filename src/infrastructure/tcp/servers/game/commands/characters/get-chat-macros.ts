import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

const MACRO_TEXT_LENGTH = 64;
const MACROS_PER_TYPE = 12;
const MACRO_PAYLOAD_SIZE = 0x301;

function buildChatMacrosPayload(
  macros: Array<{ type: number; idx: number; text: string }>,
  type: number,
): Uint8Array {
  const writer = new PacketWriter();
  writer.writeUint8(type);

  const typeMacros = macros
    .filter((m) => m.type === type)
    .sort((a, b) => a.idx - b.idx);

  for (let i = 0; i < MACROS_PER_TYPE; i++) {
    const macro = typeMacros.find((m) => m.idx === i);
    writer.writeFixedString(macro?.text ?? "", MACRO_TEXT_LENGTH);
  }

  const padding = MACRO_PAYLOAD_SIZE - 1 - MACROS_PER_TYPE * MACRO_TEXT_LENGTH;
  writer.writePadding(padding);

  return writer.build();
}

@injectable()
@GameCommandHandler(0x411a)
export class GetChatMacrosHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId === null) {
      await sendPacket(session, 0x4121);
      return;
    }

    const macros = await this.characterService.getChatMacros(characterId);
    const defaultMacros: Array<{ type: number; idx: number; text: string }> =
      [];
    for (let typem = 0; typem < 2; typem++) {
      for (let index = 0; index < MACROS_PER_TYPE; index++) {
        const existing = macros.find(
          (m) => m.type === typem && m.idx === index,
        );
        defaultMacros.push({
          type: typem,
          idx: index,
          text: existing?.text ?? "",
        });
      }
    }

    await sendPacket(session, 0x4121, buildChatMacrosPayload(defaultMacros, 0));
    await sendPacket(session, 0x4121, buildChatMacrosPayload(defaultMacros, 1));
  }
}
