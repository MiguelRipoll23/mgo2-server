import { integer, pgTable, serial, varchar } from "drizzle-orm/pg-core";

export const lobbyGameTypesTable = pgTable("lobby_game_types", {
  id: serial("id").primaryKey(),
  gameId: integer("game_id").notNull(),
  name: varchar("name", { length: 64 }).notNull(),
});

export type LobbyGameType = typeof lobbyGameTypesTable.$inferSelect;
export type NewLobbyGameType = typeof lobbyGameTypesTable.$inferInsert;
