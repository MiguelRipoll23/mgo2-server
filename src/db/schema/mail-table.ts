import { boolean, integer, pgTable, serial, timestamp, varchar } from "drizzle-orm/pg-core";
import { charactersTable } from "./characters-table.ts";
import { sql } from "drizzle-orm";

/**
 * The mailbox (0x4800/0x4820/0x4840/0x4880). One row is a single delivery seen
 * from both ends: the recipient's Inbox and the sender's Sent list are the same
 * row, with per-side read/deleted flags — the wire has no deletion or read
 * field on the 0x4822 entry, so both states are purely server-side filters.
 *
 * `sender_character_id` is NULL for system letters (Announcements, category 3
 * on the wire) and `recipient_character_id` is NULL for mail addressed to the
 * Game Master (the 0x4800 destination byte 3 — stored for admins, never
 * client-visible).
 */
export const mailTable = pgTable("mail", {
  id: serial("id").primaryKey(),
  sender_character_id: integer("sender_character_id").references(() => charactersTable.id),
  recipient_character_id: integer("recipient_character_id").references(() => charactersTable.id),
  sender_name: varchar("sender_name", { length: 16 }).notNull().default(""),
  recipient_name: varchar("recipient_name", { length: 16 }).notNull().default(""),
  subject: varchar("subject", { length: 128 }).notNull().default(""),
  body: varchar("body", { length: 708 }).notNull().default(""),
  recipient_read: boolean("recipient_read").notNull().default(false),
  recipient_deleted: boolean("recipient_deleted").notNull().default(false),
  sender_read: boolean("sender_read").notNull().default(false),
  sender_deleted: boolean("sender_deleted").notNull().default(false),
  sent_at: timestamp("sent_at").notNull().default(sql`now()`),
});

export type Mail = typeof mailTable.$inferSelect;
export type NewMail = typeof mailTable.$inferInsert;

/**
 * Mail addressed to the Game Master (0x4800 destination byte 3). The client
 * sends no recipient at all for these letters, so there is no delivery row —
 * this table is the administrative inbox, never served to a game client.
 */
export const gmMailTable = pgTable("gm_mail", {
  id: serial("id").primaryKey(),
  sender_character_id: integer("sender_character_id").references(() => charactersTable.id),
  sender_name: varchar("sender_name", { length: 16 }).notNull().default(""),
  subject: varchar("subject", { length: 128 }).notNull().default(""),
  body: varchar("body", { length: 708 }).notNull().default(""),
  sent_at: timestamp("sent_at").notNull().default(sql`now()`),
});

export type GmMail = typeof gmMailTable.$inferSelect;
export type NewGmMail = typeof gmMailTable.$inferInsert;
