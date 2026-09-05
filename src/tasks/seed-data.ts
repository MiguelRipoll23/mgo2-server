import { drizzle } from "drizzle-orm/node-postgres";
import { eq, sql } from "drizzle-orm";
import {
  characterConnectionsTable,
  charactersTable,
  gamePlayersTable,
  gamesTable,
  lobbiesTable,
  LobbyType,
  usersTable,
} from "../db/schema.ts";
import { CryptoService } from "../core/services/crypto-service.ts";

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

const npcSeedResult = await db.transaction(async (tx) => {
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

  // ── NPC account, character, and the standing P2P test match ──────────────
  // A second peer to test P2P against: log in as npc/npc, and a hosted game
  // named "P2P TESTING" is always up in the Free Battle lobby.
  const freeBattleLobbyId =
    lobbyRows.find((row) => row.name === "Free Battle")?.id ?? 3;

  // Account "npc" with password "npc" (stored as the MD5 the login flow
  // compares against).
  const npcPasswordHash = new CryptoService().md5Hex("npc");
  let [npcUser] = await tx
    .select()
    .from(usersTable)
    .where(eq(usersTable.display_name, "npc"))
    .limit(1);
  if (!npcUser) {
    [npcUser] = await tx
      .insert(usersTable)
      .values({ display_name: "npc", password: npcPasswordHash })
      .returning();
  }

  // Character "npc" parked in the Free Battle lobby.
  let [npcCharacter] = await tx
    .select()
    .from(charactersTable)
    .where(eq(charactersTable.name, "npc"))
    .limit(1);
  if (!npcCharacter) {
    [npcCharacter] = await tx
      .insert(charactersTable)
      .values({
        user_id: npcUser.id,
        name: "npc",
        lobby_id: freeBattleLobbyId,
        comment: "P2P test peer",
      })
      .returning();
  }

  // The standing match. Re-created on every seed run so its settings always
  // match this file. Joins require the host's registered 0x4700 endpoint, so
  // the NPC must actually be connected (in game, past check-session and its
  // 0x4700 push) for a P2P handoff to succeed — that is the thing being
  // tested.
  await tx.delete(gamesTable).where(eq(gamesTable.name, "P2P TESTING"));
  const [p2pGame] = await tx
    .insert(gamesTable)
    .values({
      host_id: npcCharacter.id,
      lobby_id: freeBattleLobbyId,
      name: "P2P TESTING",
      password: "",
      comment: "Standing match for P2P testing",
      max_players: 8,
      games: JSON.stringify([[1, 0, 0]]),
    })
    .returning();

  // Required FK row: the host is the game's first roster member. Pings,
  // round attribution and the details player list all read from here.
  await tx
    .insert(gamePlayersTable)
    .values({ game_id: p2pGame.id, character_id: npcCharacter.id })
    .onConflictDoNothing();

  // The host's P2P endpoint, required by the join handoff (0x4321): without
  // a row the join refuses with a generic error. Provisioned at loopback
  // port 5731 (external == internal) so a test client dials the server host
  // itself; a real client's 0x4700 push overwrites this row when the NPC
  // logs in for real.
  await tx
    .insert(characterConnectionsTable)
    .values({
      character_id: npcCharacter.id,
      public_ip: "127.0.0.1",
      public_port: 5731,
      private_ip: "127.0.0.1",
      private_port: 5731,
    })
    .onConflictDoUpdate({
      target: characterConnectionsTable.character_id,
      set: {
        public_ip: "127.0.0.1",
        public_port: 5731,
        private_ip: "127.0.0.1",
        private_port: 5731,
      },
    });

  return { gameId: p2pGame.id, characterId: npcCharacter.id };
});

console.log("Done.");
console.log(`Game types inserted: ${gameTypeRows.length}`);
console.log(`Lobbies inserted: ${lobbyRows.length}`);
console.log(
  `P2P test match: game ${npcSeedResult.gameId} "P2P TESTING" hosted by character ${npcSeedResult.characterId} "npc" in Free Battle`,
);
console.log("NPC account: npc / npc");
