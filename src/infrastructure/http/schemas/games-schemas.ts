import { z } from "@hono/zod-openapi";

export const gameResponseSchema = z.object({
  id: z.number().openapi({ example: 1, description: "Unique game identifier" }),
  hostId: z.number().openapi({ example: 1, description: "Character ID of the game host" }),
  lobbyId: z.number().openapi({ example: 1, description: "Lobby this game belongs to" }),
  name: z.string().openapi({ example: "My Game", description: "Display name of the game room" }),
  password: z.string().openapi({ example: "", description: "Password required to join (empty means open)" }),
  comment: z.string().openapi({ example: "", description: "Optional comment shown in the game list" }),
  maxPlayers: z.number().openapi({ example: 8, description: "Maximum number of players allowed" }),
  currentGame: z.number().openapi({ example: 0, description: "Index of the currently active game variant" }),
  games: z.string().openapi({ example: "[]", description: "JSON array of game variant data" }),
  stance: z.number().openapi({ example: 0, description: "Game stance setting" }),
  ping: z.number().openapi({ example: 0, description: "Reported ping of the host" }),
  common: z.string().openapi({ example: "{}", description: "JSON object with common game settings" }),
  rules: z.string().openapi({ example: "{}", description: "JSON object with game rule settings" }),
  status: z.number().openapi({ example: 0, description: "Current game status" }),
  createdAt: z.string().nullable().openapi({ example: "2024-01-01T00:00:00.000Z", description: "Timestamp when the game was created" }),
}).openapi("GameResponse");

export const gamesListResponseSchema = z.array(gameResponseSchema);

export const gameRequestSchema = z.object({
  hostId: z.number().int().positive().openapi({ example: 1, description: "Character ID of the game host" }),
  lobbyId: z.number().int().positive().openapi({ example: 1, description: "Lobby this game belongs to" }),
  name: z.string().min(1).max(16).openapi({ example: "My Game", description: "Display name of the game room" }),
  password: z.string().max(15).default("").openapi({ example: "", description: "Password required to join" }),
  comment: z.string().max(128).default("").openapi({ example: "", description: "Optional comment shown in the game list" }),
  maxPlayers: z.number().int().min(1).max(8).default(8).openapi({ example: 8 }),
  currentGame: z.number().int().default(0).openapi({ example: 0 }),
  games: z.string().default("[]").openapi({ example: "[]" }),
  stance: z.number().int().default(0).openapi({ example: 0 }),
  ping: z.number().int().default(0).openapi({ example: 0 }),
  common: z.string().default("{}").openapi({ example: "{}" }),
  rules: z.string().default("{}").openapi({ example: "{}" }),
  status: z.number().int().default(0).openapi({ example: 0 }),
});

export const gamePatchRequestSchema = gameRequestSchema.partial();

export const gameParamsSchema = z.object({
  id: z.coerce.number().int().positive(),
});

export const gameErrorSchema = z.object({
  error: z.string(),
});
