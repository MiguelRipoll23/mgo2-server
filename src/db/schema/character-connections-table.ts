import { integer, pgTable, timestamp, varchar } from "drizzle-orm/pg-core";
import { charactersTable } from "./characters-table.ts";

/**
 * The P2P endpoint a character's client listens on, registered by the
 * 0x4700 push and handed to joiners in the 0x4321 join reply. Public is the
 * address as seen by the server (taken from the socket, not the payload);
 * private is what the client reported for its LAN.
 *
 * DB-backed rather than in-memory so a seed can provision a standing
 * endpoint (e.g. the P2P TESTING host at loopback): a live 0x4700 push
 * upserts over it. One row per character — a client listens on one endpoint.
 */
export const characterConnectionsTable = pgTable("character_connections", {
  character_id: integer("character_id")
    .primaryKey()
    .references(() => charactersTable.id, { onDelete: "cascade" }),
  public_ip: varchar("public_ip", { length: 16 }).notNull(),
  public_port: integer("public_port").notNull(),
  private_ip: varchar("private_ip", { length: 16 }).notNull(),
  private_port: integer("private_port").notNull(),
  updated_at: timestamp("updated_at", { withTimezone: true })
    .notNull()
    .defaultNow(),
});

export type CharacterConnection = typeof characterConnectionsTable.$inferSelect;
export type NewCharacterConnection = typeof characterConnectionsTable.$inferInsert;
