import {
  boolean,
  integer,
  pgTable,
  serial,
  varchar,
} from "drizzle-orm/pg-core";
import { lobbyGameTypesTable } from "./lobby-game-types-table.ts";

export enum LobbyType {
  GATE = 0,
  ACCOUNT = 1,
  GAME = 2,
}

export const lobbiesTable = pgTable("lobbies", {
  id: serial("id").primaryKey(),
  typeId: integer("type_id").notNull().$type<LobbyType>(),
  subtypeId: integer("subtype_id").notNull().references(
    () => lobbyGameTypesTable.id,
    { onDelete: "restrict", onUpdate: "cascade" },
  ),
  name: varchar("name", { length: 16 }).notNull(),
  ipAddress: varchar("ip_address", { length: 15 }).notNull(),
  port: integer("port").notNull().unique(),
  playersCount: integer("players_count").notNull().default(0),
  beginnerOnly: boolean("beginner_only").notNull().default(false),
  expansionOnly: boolean("expansion_only").notNull().default(false),
  noHeadshot: boolean("no_headshot").notNull().default(false),
  replaysOnly: boolean("replays_only").notNull().default(false),
});

export type Lobby = typeof lobbiesTable.$inferSelect;
export type NewLobby = typeof lobbiesTable.$inferInsert;
