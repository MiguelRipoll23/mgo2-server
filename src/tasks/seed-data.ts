import { drizzle } from "drizzle-orm/node-postgres";
import { sql } from "drizzle-orm";
import { lobbiesTable, LobbyType } from "../db/schema.ts";

const databaseUrl = Deno.env.get("DATABASE_URL");

if (!databaseUrl) {
  console.error("DATABASE_URL environment variable is required");
  Deno.exit(1);
}

const db = drizzle(databaseUrl);

// Rows are inserted with explicit IDs so that lobby_game_types.id equals the
// game-protocol gameId value. This keeps lobbies.subtypeId consistent with
// the wire format used in hub.ts.
const gameTypeRows = [
  { id: 0, gameId: 0, name: "None" },
  { id: 1, gameId: 1, name: "Free Battle" },
  { id: 2, gameId: 2, name: "Automatching" },
  { id: 3, gameId: 3, name: "Tournament" },
  { id: 4, gameId: 4, name: "Survival" },
  { id: 5, gameId: 5, name: "Unknown" },
  { id: 6, gameId: 6, name: "Unknown" },
  { id: 7, gameId: 7, name: "Training" },
  { id: 10, gameId: 10, name: "Tournament Registration" },
];

const lobbyRows = [
  {
    id: 1,
    typeId: LobbyType.GATE,
    subtypeId: 0,
    name: "GATE",
    ipAddress: "0.0.0.0",
    port: 5731,
    playersCount: 0,
    beginnerOnly: false,
    expansionOnly: false,
    noHeadshot: false,
    replays: false,
  },
  {
    id: 2,
    typeId: LobbyType.ACCOUNT,
    name: "ACCOUNT",
    ipAddress: "0.0.0.0",
    port: 5732,
    playersCount: 0,
    beginnerOnly: false,
    expansionOnly: false,
    noHeadshot: false,
    replays: false,
    subtypeId: 0,
  },
  {
    id: 3,
    typeId: LobbyType.GAME,
    name: "Free Battle",
    ipAddress: "0.0.0.0",
    port: 5733,
    playersCount: 0,
    beginnerOnly: false,
    expansionOnly: false,
    noHeadshot: false,
    replays: false,
    subtypeId: 1,
  },
  {
    id: 8,
    typeId: LobbyType.GAME,
    name: "Basic Training",
    ipAddress: "0.0.0.0",
    port: 5737,
    playersCount: 0,
    beginnerOnly: false,
    expansionOnly: false,
    noHeadshot: false,
    replays: false,
    subtypeId: 7,
  },
  {
    id: 9,
    typeId: LobbyType.GAME,
    name: "Combat Training",
    ipAddress: "0.0.0.0",
    port: 5738,
    playersCount: 0,
    beginnerOnly: false,
    expansionOnly: false,
    noHeadshot: false,
    replays: false,
    subtypeId: 7,
  },
  {
    id: 10,
    typeId: LobbyType.GAME,
    name: "Survival",
    ipAddress: "0.0.0.0",
    port: 5735,
    playersCount: 0,
    beginnerOnly: false,
    expansionOnly: false,
    noHeadshot: false,
    replays: false,
    subtypeId: 4,
  },
  {
    id: 12,
    typeId: LobbyType.GAME,
    name: "Survival Hosts",
    ipAddress: "0.0.0.0",
    port: 5739,
    playersCount: 0,
    beginnerOnly: false,
    expansionOnly: false,
    noHeadshot: false,
    replays: false,
    subtypeId: 5,
  },
  {
    id: 13,
    typeId: LobbyType.GAME,
    name: "Replays",
    ipAddress: "0.0.0.0",
    port: 5734,
    playersCount: 0,
    beginnerOnly: false,
    expansionOnly: false,
    noHeadshot: false,
    replays: false,
    subtypeId: 7,
  },
  {
    id: 14,
    typeId: LobbyType.GAME,
    name: "Automatching",
    ipAddress: "0.0.0.0",
    port: 5740,
    playersCount: 0,
    beginnerOnly: false,
    expansionOnly: false,
    noHeadshot: false,
    replays: false,
    subtypeId: 2,
  },
  {
    id: 15,
    typeId: LobbyType.GAME,
    name: "Registration",
    ipAddress: "0.0.0.0",
    port: 5741,
    playersCount: 0,
    beginnerOnly: false,
    expansionOnly: false,
    noHeadshot: false,
    replays: false,
    subtypeId: 10,
  },
  {
    id: 16,
    typeId: LobbyType.GAME,
    name: "Tournament",
    ipAddress: "0.0.0.0",
    port: 5742,
    playersCount: 0,
    beginnerOnly: false,
    expansionOnly: false,
    noHeadshot: false,
    replays: false,
    subtypeId: 3,
  },
];

console.log("Seeding lobby_game_types and lobbies tables...");

await db.transaction(async (tx) => {
  await tx.delete(lobbiesTable);

  // Delete all game types via raw SQL to avoid FK order issues
  await tx.execute(sql`DELETE FROM lobby_game_types`);

  // Insert game types with explicit IDs so that id == gameId (the wire-format
  // enum value). Serial sequences are bypassed here intentionally.
  for (const row of gameTypeRows) {
    await tx.execute(
      sql`INSERT INTO lobby_game_types (id, game_id, name) VALUES (${row.id}, ${row.gameId}, ${row.name})`,
    );
  }

  // Advance the sequence past the highest explicit ID we inserted.
  await tx.execute(
    sql`SELECT setval('lobby_game_types_id_seq', (SELECT MAX(id) FROM lobby_game_types))`,
  );

  // Insert lobbies with explicit IDs and advance the sequence afterward.
  await tx.insert(lobbiesTable).values(lobbyRows);
  await tx.execute(
    sql`SELECT setval('lobbies_id_seq', (SELECT MAX(id) FROM lobbies))`,
  );
});

console.log("Done.");
console.log(`Game types inserted: ${gameTypeRows.length}`);
console.log(`Lobbies inserted: ${lobbyRows.length}`);
