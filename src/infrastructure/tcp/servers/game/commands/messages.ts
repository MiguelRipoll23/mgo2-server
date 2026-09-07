import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../core/tcp/utils/packet-builder-util.ts";
import { sendPacket, sendResult } from "../../../../../core/tcp/utils/session-helpers-util.ts";
import {
  RESULT_NONE,
  RESULT_MAIL_NOT_FOUND,
  RESULT_MAIL_RECIPIENT_UNKNOWN,
  RESULT_MAIL_RECIPIENT_FULL,
} from "../../../../../core/constants/error-codes-constants.ts";
import { ClanService } from "../../../../../modules/clan/clan-service.ts";
import {
  MailService,
  MAILBOX_MAX,
  type MailEntry,
  type RecipientOutcome,
} from "../../../../../modules/mail/mail-service.ts";
import { CharacterService } from "../../../../../modules/character/character-service.ts";

// The mailbox family (0x48xx), stored in the mail table. The client reads the
// mailbox as soon as it joins a game lobby, and shares ONE wait slot (0x55)
// across the whole family — replies must stay in order and in shape.
//
// The 0x4820 request's selector does NOT choose a tab: every tab asks with
// 0x0f, and the inbox/sent split is driven by the category byte on each
// 0x4822 entry (0 = Inbox, 1 = Sent, 3 = Announcements). Selector 0x10 is the
// clan-applications box, where a leader approves with 0x4b30/0x4b32.

const GET_MESSAGES = 0x4820;
const GET_MESSAGES_START = 0x4821;
const GET_MESSAGES_DATA = 0x4822;
const GET_MESSAGES_END = 0x4823;

const SEND_MESSAGE = 0x4800;
const SEND_MESSAGE_RESULT = 0x4801;

const READ_MESSAGE = 0x4840;
const READ_MESSAGE_RESULT = 0x4841;

const DELETE_MESSAGE = 0x4880;
const DELETE_MESSAGE_RESULT = 0x4881;

// Mailbox selectors, named after the reference servers.
const MAIL = 0x0f;
const CLAN_APPLICATIONS = 0x10;

// Wire categories (see header comment — 2 is dead in this build).
const CATEGORY_INBOX = 0;
const CATEGORY_SENT = 1;
const CATEGORY_ANNOUNCEMENT = 3;

// Bit 0 of the 0x4801 flags byte MUST be set: clear, the parser re-sends the
// entire letter as a 969-byte 0x4860 and re-waits on the same slot.
const SEND_RESULT_FLAGS = 0x01;

const NAME_LENGTH = 16;
const SEND_MAX_RECIPIENTS = 8;
const SUBJECT_LENGTH = 128;
const BODY_LENGTH = 708;
const READ_BODY_LENGTH = 708;

// Wire offsets in the 967-byte 0x4800: count @0, names @1, subject, body,
// then the destination byte — 3 means "to the Game Master" (no recipients).
const SEND_RECIPIENTS_OFFSET = 1;
const SUBJECT_OFFSET = SEND_RECIPIENTS_OFFSET + SEND_MAX_RECIPIENTS * NAME_LENGTH;
const BODY_OFFSET = SUBJECT_OFFSET + SUBJECT_LENGTH;
const GM_DESTINATION_OFFSET = BODY_OFFSET + BODY_LENGTH;
const GM_DESTINATION = 3;

// 0x4822 entry: {u8 category, u8 index, u8 nameCount, char[128], char[128],
// u32, u8 x3} = 266 bytes. The parser has NO loop — exactly one record per
// packet, the rest of the payload discarded.
const ENTRY_NAME_COUNT = 1;

function readFixed(payload: Uint8Array, offset: number, max: number): string {
  let end = Math.min(offset + max, payload.length);
  let text = "";
  for (let i = offset; i < end; i++) {
    if (payload[i] === 0) {
      end = i;
      break;
    }
  }
  for (let i = offset; i < end; i++) {
    text += String.fromCharCode(payload[i]);
  }
  return text.trim();
}

// ── Send (0x4800 → 0x4801) ──────────────────────────────────────────────────

@injectable()
@GameCommandHandler(SEND_MESSAGE)
export class SendMessageHandler implements ICommandHandler {
  constructor(
    private mailService = inject(MailService),
    private characterService = inject(CharacterService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId === null) {
      await sendResult(session, SEND_MESSAGE_RESULT, RESULT_MAIL_RECIPIENT_UNKNOWN);
      return;
    }

    const payload = packet.payload;
    if (payload.length < BODY_OFFSET + BODY_LENGTH) {
      await sendResult(session, SEND_MESSAGE_RESULT, RESULT_MAIL_RECIPIENT_UNKNOWN);
      return;
    }

    const sender = await this.characterService.findById(characterId);
    const senderName = sender?.name ?? "";
    const subject = readFixed(payload, SUBJECT_OFFSET, SUBJECT_LENGTH);
    const body = readFixed(payload, BODY_OFFSET, BODY_LENGTH);

    // TO: GAME MASTER — destination byte 3, no recipients on the wire. Stored
    // to the admin inbox; answered success like any other letter.
    const destination = payload.length > GM_DESTINATION_OFFSET
      ? payload[GM_DESTINATION_OFFSET]
      : 0;
    if (destination === GM_DESTINATION) {
      await this.mailService.sendGameMasterMail(characterId, senderName, subject, body);
      await sendSuccess(session);
      return;
    }

    const count = Math.min(payload[0] ?? 0, SEND_MAX_RECIPIENTS);
    const failed: RecipientOutcome[] = [];
    for (let slot = 0; slot < count; slot++) {
      const name = readFixed(
        payload,
        SEND_RECIPIENTS_OFFSET + slot * NAME_LENGTH,
        NAME_LENGTH,
      );
      if (name.length === 0) continue;
      const outcome = await this.mailService.send(
        characterId,
        senderName,
        name,
        subject,
        body,
      );
      if (outcome.failureCode !== undefined) failed.push(outcome);
    }

    if (failed.length === 0) {
      await sendSuccess(session);
      return;
    }

    // Nonzero status makes the client read the per-recipient list that follows.
    // Unknown recipients go in the list too — success would claim a letter
    // that was never sent. Priority mirrors the reference: blocked > full >
    // unknown (we have no block system, so only the last two occur).
    const hasFull = failed.some((f) => f.failureCode === RESULT_MAIL_RECIPIENT_FULL);
    const status = hasFull
      ? RESULT_MAIL_RECIPIENT_FULL
      : RESULT_MAIL_RECIPIENT_UNKNOWN;

    const writer = new PacketWriter()
      .writeUint32(status >>> 0)
      .writeUint8(SEND_RESULT_FLAGS)
      .writeUint32(failed.length);
    for (const outcome of failed) {
      writer.writeFixedString(outcome.name, NAME_LENGTH);
      writer.writeUint32((outcome.failureCode ?? RESULT_MAIL_RECIPIENT_UNKNOWN) >>> 0);
    }
    await sendPacket(session, SEND_MESSAGE_RESULT, writer.build());
  }
}

async function sendSuccess(session: TcpSession): Promise<void> {
  const writer = new PacketWriter()
    .writeUint32(RESULT_NONE)
    .writeUint8(SEND_RESULT_FLAGS);
  await sendPacket(session, SEND_MESSAGE_RESULT, writer.build());
}

// ── List (0x4820 → 0x4821, 0x4822*, 0x4823) ─────────────────────────────────

@injectable()
@GameCommandHandler(GET_MESSAGES)
export class GetMessagesHandler implements ICommandHandler {
  constructor(
    private mailService = inject(MailService),
    private clanService = inject(ClanService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const reader = new PacketReader(packet.payload);
    const mailbox = reader.remaining() > 0 ? reader.readUint8() : MAIL;
    const characterId = session.characterId;

    // Both start and end carry a result code, not a count — the client counts
    // the entry records itself.
    await sendResult(session, GET_MESSAGES_START, RESULT_NONE);

    let entries: Array<{ category: number; letter: MailEntry }> = [];

    if (mailbox === MAIL && characterId !== null) {
      const received = await this.mailService.mailbox(characterId, MAILBOX_MAX);
      const sent = await this.mailService.sentMail(characterId, MAILBOX_MAX);
      // A letter with no sender character is from the system — Announcements.
      for (const letter of received) {
        entries.push({
          category: letter.systemSender ? CATEGORY_ANNOUNCEMENT : CATEGORY_INBOX,
          letter,
        });
      }
      for (const letter of sent) {
        entries.push({ category: CATEGORY_SENT, letter });
      }
    } else if (mailbox === CLAN_APPLICATIONS && characterId !== null) {
      // Clan applications are MESSAGES, not roster rows: the leader judges
      // each one from this box with 0x4b30 (accept) / 0x4b32 (decline).
      const membership = await this.clanService.findMembershipByCharacterId(characterId);
      if (membership?.isLeader) {
        const applications = await this.clanService.applicantsFor(membership.clanId);
        entries = applications.map((application) => [
          {
            category: CATEGORY_INBOX,
            letter: {
              id: -application.characterId,
              index: 0,
              counterparty: application.name,
              subject: "",
              body: "",
              sentAtEpoch: Math.floor(application.appliedAt.getTime() / 1000),
              read: false,
              systemSender: false,
            } satisfies MailEntry,
          },
        ]).flat();
        entries = entries.map((entry, index) => ({
          ...entry,
          letter: { ...entry.letter, index },
        }));
      }
    }

    for (const entry of entries) {
      const writer = new PacketWriter()
        .writeUint8(entry.category)
        .writeUint8(entry.letter.index)
        .writeUint8(ENTRY_NAME_COUNT)
        .writeFixedString(entry.letter.counterparty, SUBJECT_LENGTH)
        .writeFixedString(entry.letter.subject, SUBJECT_LENGTH)
        .writeUint32(entry.letter.sentAtEpoch >>> 0)
        .writePadding(2)
        .writeUint8(entry.letter.read ? 1 : 0);
      await sendPacket(session, GET_MESSAGES_DATA, writer.build());
    }

    await sendResult(session, GET_MESSAGES_END, RESULT_NONE);
  }
}

// ── Read (0x4840 → 0x4841) and Delete (0x4880 → 0x4881) ─────────────────────

// 0x4841 is {u32 result} + the 708-byte body = 712 bytes. The body reader
// bound-checks the 1023-byte receive buffer rather than the payload, so a
// SHORT success reply copies stale buffer into the mail object — a nonzero
// result skips the read entirely, which is why "not found" is an error here.

@injectable()
@GameCommandHandler(READ_MESSAGE)
export class GetMessageContentsHandler implements ICommandHandler {
  constructor(private mailService = inject(MailService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId === null) {
      await sendResult(session, READ_MESSAGE_RESULT, RESULT_MAIL_NOT_FOUND);
      return;
    }

    const { category, index } = parseCategoryIndex(packet.payload);
    const letter = await listLetter(
      this.mailService,
      characterId,
      category,
      index,
    );

    if (!letter) {
      // "Unable to locate designated mail." — the client has had a sentence
      // for exactly this since the original service.
      await sendResult(session, READ_MESSAGE_RESULT, RESULT_MAIL_NOT_FOUND);
      return;
    }

    // Opening is the only mark-as-read signal the protocol has.
    await this.mailService.markRead(letter.id, category === CATEGORY_SENT);

    const writer = new PacketWriter()
      .writeUint32(RESULT_NONE)
      .writeFixedString(letter.body, READ_BODY_LENGTH);
    await sendPacket(session, READ_MESSAGE_RESULT, writer.build());
  }
}

function parseCategoryIndex(payload: Uint8Array): { category: number; index: number } {
  return {
    category: payload.length >= 1 ? (payload[0] << 24 >> 24) : 0,
    index: payload.length >= 2 ? payload[1] : 0,
  };
}

async function listLetter(
  mailService: MailService,
  characterId: number,
  category: number,
  index: number,
): Promise<MailEntry | null> {
  const sent = category === CATEGORY_SENT;
  const list = sent
    ? await mailService.sentMail(characterId, MAILBOX_MAX)
    : await mailService.mailbox(characterId, MAILBOX_MAX);
  return index < list.length ? list[index] : null;
}

@injectable()
@GameCommandHandler(DELETE_MESSAGE)
export class DeleteMessageHandler implements ICommandHandler {
  constructor(private mailService = inject(MailService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId === null) {
      await sendResult(session, DELETE_MESSAGE_RESULT, RESULT_MAIL_NOT_FOUND);
      return;
    }

    const { category, index } = parseCategoryIndex(packet.payload);
    const letter = await listLetter(this.mailService, characterId, category, index);

    if (!letter) {
      // Nothing was removed, so success would claim a deletion that did not
      // happen — the letter stayed in the list the player was just shown.
      await sendResult(session, DELETE_MESSAGE_RESULT, RESULT_MAIL_NOT_FOUND);
      return;
    }

    // The letter leaves the asking end's list only: one row is a single
    // delivery seen from both ends, each with its own flags.
    await this.mailService.delete(letter.id, category === CATEGORY_SENT);
    await sendResult(session, DELETE_MESSAGE_RESULT, RESULT_NONE);
  }
}
