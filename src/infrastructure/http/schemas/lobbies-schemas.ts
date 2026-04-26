import { z } from "@hono/zod-openapi";
import { LobbyType } from "../../../db/schema.ts";

export const lobbyTypeSchema = z.nativeEnum(LobbyType);

export const lobbyResponseSchema = z.object({
  id: z.number().openapi({
    example: 1,
    description: "Unique lobby identifier",
  }),
  typeId: lobbyTypeSchema.openapi({
    example: LobbyType.GAME,
    description: "Server type (0 = Gate, 1 = Account, 2 = Game)",
  }),
  subtypeId: z.number().openapi({
    example: 1,
    description: "Reference to the associated game type",
  }),
  name: z.string().openapi({
    example: "Lobby 1",
    description: "Display name of the lobby",
  }),
  ipAddress: z.string().openapi({
    example: "192.168.1.1",
    description: "IP address clients connect to",
  }),
  port: z.number().openapi({
    example: 5731,
    description: "TCP port clients connect to",
  }),
  playersCount: z.number().openapi({
    example: 0,
    description: "Current number of players in the lobby",
  }),
  beginnerOnly: z.boolean().openapi({
    example: false,
    description: "Whether the lobby is restricted to beginner players",
  }),
  expansionOnly: z.boolean().openapi({
    example: false,
    description: "Whether the lobby requires the expansion",
  }),
  noHeadshot: z.boolean().openapi({
    example: false,
    description: "Whether headshots are disabled in this lobby",
  }),
  replaysOnly: z.boolean().openapi({
    example: false,
    description: "Whether replay recording is enabled",
  }),
}).openapi("LobbyResponse");

export const lobbiesListResponseSchema = z.array(lobbyResponseSchema);

export const lobbyRequestSchema = z.object({
  typeId: lobbyTypeSchema.openapi({ example: LobbyType.GAME }),
  subtypeId: z.number().int().default(1).openapi({ example: 1 }),
  name: z.string().min(1).max(16).openapi({ example: "Lobby 1" }),
  ipAddress: z.ipv4().openapi({ example: "192.168.1.1" }),
  port: z.number().int().min(1).max(65535).openapi({ example: 5731 }),
  playersCount: z.number().int().min(0).default(0).openapi({ example: 0 }),
});

export const lobbyPatchRequestSchema = lobbyRequestSchema.partial();

export const lobbyParamsSchema = z.object({
  id: z.coerce.number().int().positive(),
});

export const lobbyErrorSchema = z.object({
  error: z.string(),
});
