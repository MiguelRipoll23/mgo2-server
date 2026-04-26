import { integer, pgTable, primaryKey, text, timestamp } from "drizzle-orm/pg-core";
import { lobbiesTable } from "./lobbies-table.ts";

export const lobbyInstanceCountsTable = pgTable("lobby_instance_counts", {
  instanceId: text("instance_id").notNull(),
  lobbyId: integer("lobby_id").notNull().references(() => lobbiesTable.id, {
    onDelete: "cascade",
  }),
  count: integer("count").notNull().default(0),
  updatedAt: timestamp("updated_at", { withTimezone: true }).notNull().defaultNow(),
}, (table) => [
  primaryKey({ columns: [table.instanceId, table.lobbyId] }),
]);

export type LobbyInstanceCount = typeof lobbyInstanceCountsTable.$inferSelect;
export type NewLobbyInstanceCount = typeof lobbyInstanceCountsTable.$inferInsert;
