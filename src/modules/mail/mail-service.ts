import { injectable, inject } from "@needle-di/core";
import { and, desc, eq, sql } from "drizzle-orm";
import { DatabaseService } from "../../core/services/database-service.ts";
import { charactersTable, gmMailTable, mailTable } from "../../db/schema.ts";
import type { Mail } from "../../db/schema.ts";

/** One listed letter, as the mail handlers render it. */
export interface MailEntry {
  id: number;
  /** Position within the queried list — the wire's per-category index. */
  index: number;
  /** The other end of the letter: sender for the inbox, recipient for Sent. */
  counterparty: string;
  subject: string;
  body: string;
  /** Unix seconds — the wire's u32 timestamp. */
  sentAtEpoch: number;
  read: boolean;
  /** A letter with no sender character is from the system (Announcements). */
  systemSender: boolean;
}

/** Result of a fan-out send, per recipient — drives the 0x4801 failure list. */
export interface RecipientOutcome {
  name: string;
  /** undefined = delivered; otherwise the official failure code (-801/-802). */
  failureCode?: number;
}

/** The client's own per-category cap; a 17th inbox letter would silently vanish. */
export const MAILBOX_MAX = 16;

@injectable()
export class MailService {
  constructor(private readonly dbs = inject(DatabaseService)) {}

  private get db() {
    return this.dbs.get();
  }

  /** Delivers one letter to a named recipient. Returns the outcome row. */
  async send(
    senderCharacterId: number,
    senderName: string,
    recipientName: string,
    subject: string,
    body: string,
  ): Promise<RecipientOutcome> {
    const matches = await this.db
      .select({ id: charactersTable.id })
      .from(charactersTable)
      .where(eq(charactersTable.name, recipientName))
      .limit(1);

    const target = matches[0];
    if (!target) {
      return { name: recipientName, failureCode: -801 }; // RECIPIENT_UNKNOWN
    }

    const size = await this.mailboxSize(target.id);
    if (size >= MAILBOX_MAX) {
      return { name: recipientName, failureCode: -802 }; // RECIPIENT_MAILBOX_FULL
    }

    await this.db.insert(mailTable).values({
      sender_character_id: senderCharacterId,
      recipient_character_id: target.id,
      sender_name: senderName,
      recipient_name: recipientName,
      subject,
      body,
    });
    return { name: recipientName };
  }

  /** Undeleted inbox size — the send-side refusal check. */
  async mailboxSize(characterId: number): Promise<number> {
    const rows = await this.db
      .select({ count: sql<number>`count(*)::int` })
      .from(mailTable)
      .where(
        and(
          eq(mailTable.recipient_character_id, characterId),
          eq(mailTable.recipient_deleted, false),
        ),
      );
    return rows[0]?.count ?? 0;
  }

  /** Mail addressed to the Game Master (destination byte 3) — admin inbox only. */
  async sendGameMasterMail(
    senderCharacterId: number,
    senderName: string,
    subject: string,
    body: string,
  ): Promise<void> {
    await this.db.insert(gmMailTable).values({
      sender_character_id: senderCharacterId,
      sender_name: senderName,
      subject,
      body,
    });
  }

  /** The recipient's inbox, newest first, indexed 0..n. */
  async mailbox(characterId: number, limit = MAILBOX_MAX): Promise<MailEntry[]> {
    const rows = await this.db
      .select()
      .from(mailTable)
      .where(
        and(
          eq(mailTable.recipient_character_id, characterId),
          eq(mailTable.recipient_deleted, false),
        ),
      )
      .orderBy(desc(mailTable.sent_at), desc(mailTable.id))
      .limit(limit);
    return rows.map((row, index) => toEntry(row, index, row.sender_name, false));
  }

  /** The sender's Sent list, newest first, indexed 0..n. */
  async sentMail(characterId: number, limit = MAILBOX_MAX): Promise<MailEntry[]> {
    const rows = await this.db
      .select()
      .from(mailTable)
      .where(
        and(
          eq(mailTable.sender_character_id, characterId),
          eq(mailTable.sender_deleted, false),
        ),
      )
      .orderBy(desc(mailTable.sent_at), desc(mailTable.id))
      .limit(limit);
    return rows.map((row, index) => toEntry(row, index, row.recipient_name, true));
  }

  /** Opening is the only mark-as-read signal the protocol has — per side. */
  async markRead(mailId: number, sent: boolean): Promise<void> {
    await this.db
      .update(mailTable)
      .set(sent ? { sender_read: true } : { recipient_read: true })
      .where(eq(mailTable.id, mailId));
  }

  /** The letter leaves the asking end's list only — one row, two flags. */
  async delete(mailId: number, sent: boolean): Promise<void> {
    await this.db
      .update(mailTable)
      .set(sent ? { sender_deleted: true } : { recipient_deleted: true })
      .where(eq(mailTable.id, mailId));
  }

  /** Whether the GM mailbox has any letters (used by tests/tooling). */
  async gmMailCount(): Promise<number> {
    const rows = await this.db
      .select({ count: sql<number>`count(*)::int` })
      .from(gmMailTable);
    return rows[0]?.count ?? 0;
  }
}

function toEntry(row: Mail, index: number, counterparty: string, sent: boolean): MailEntry {
  return {
    id: row.id,
    index,
    counterparty,
    subject: row.subject,
    body: row.body,
    sentAtEpoch: Math.floor((row.sent_at?.getTime() ?? 0) / 1000),
    read: sent ? row.sender_read : row.recipient_read,
    systemSender: row.sender_character_id === null,
  };
}
