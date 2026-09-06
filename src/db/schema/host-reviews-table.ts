import {
  index,
  integer,
  pgTable,
  primaryKey,
  smallint,
  timestamp,
} from "drizzle-orm/pg-core";
import { charactersTable } from "./characters-table.ts";
import { gamesTable } from "./games-table.ts";

/**
 * Host-rating votes (0x43c4) — the end-of-game star picker. One row per vote,
 * append-only, with the average computed at query time. Votes are history and
 * outlive the game, so they are NOT accumulated onto the game row (games are
 * deleted at teardown, exactly as round reports outlive games).
 *
 * One vote per player per game: the client offers the prompt once as the
 * player leaves, so a second arrival is a retry or a replay, not a second
 * opinion. The same key backs the 0x4321 join-reply rating gate, so the gate
 * and this constraint can never disagree.
 */
export const hostReviewsTable = pgTable(
  "host_reviews",
  {
    // Not a primary key: the composite (game_id, voter_character_id) key below
    // is the table's single primary key (PostgreSQL allows only one per table).
    id: integer("id").generatedByDefaultAsIdentity(),
    game_id: integer("game_id").notNull(),
    host_character_id: integer("host_character_id")
      .notNull()
      .references(() => charactersTable.id, { onDelete: "cascade" }),
    voter_character_id: integer("voter_character_id")
      .notNull()
      .references(() => charactersTable.id, { onDelete: "cascade" }),
    rating: smallint("rating").notNull(),
    reviewed_at: timestamp("reviewed_at", { withTimezone: true })
      .notNull()
      .defaultNow(),
  },
  (table) => [
    // One vote per player per game.
    primaryKey({ columns: [table.game_id, table.voter_character_id] }),
    // Matches the read pattern of both consumers: the star gauge and the
    // ranking board, which windows on the timestamp for its monthly half.
    index("host_reviews_host_idx").on(
      table.host_character_id,
      table.reviewed_at,
    ),
  ],
);

/**
 * Snapshots the roster at round start so a host's end-of-round report still
 * applies to a player who quit mid-round. Insert-only: rows accumulate
 * everyone who has ever been in the game, so the check is "was in the game
 * when a round started", roster or not.
 */
export const gameRoundsTable = pgTable(
  "game_rounds",
  {
    game_id: integer("game_id")
      .notNull()
      .references(() => gamesTable.id, { onDelete: "cascade" }),
    character_id: integer("character_id")
      .notNull()
      .references(() => charactersTable.id, { onDelete: "cascade" }),
  },
  (table) => [
    primaryKey({ columns: [table.game_id, table.character_id] }),
  ],
);

/**
 * The per-game roster with team slots and the host-reported ping for the
 * player-list and game-details screens. Replaces the in-memory player map as
 * the source of truth; rows are inserted on join/create and removed on
 * leave/host migration.
 */
export const gamePlayersTable = pgTable(
  "game_players",
  {
    game_id: integer("game_id")
      .notNull()
      .references(() => gamesTable.id, { onDelete: "cascade" }),
    character_id: integer("character_id")
      .notNull()
      .references(() => charactersTable.id, { onDelete: "cascade" }),
    team: smallint("team").notNull().default(0),
    ping: integer("ping").notNull().default(0),
    joined_at: timestamp("joined_at", { withTimezone: true })
      .notNull()
      .defaultNow(),
  },
  (table) => [
    primaryKey({ columns: [table.game_id, table.character_id] }),
  ],
);
