import { injectable, inject } from "@needle-di/core";
import { GameService } from "../../../modules/game/game-service.ts";
import type { Game, NewGame } from "../../../db/schema.ts";
import { ServerError } from "../../../core/errors/server-error.ts";

export interface GameResponse {
  id: number;
  hostId: number;
  lobbyId: number;
  name: string;
  password: string;
  comment: string;
  maxPlayers: number;
  currentGame: number;
  games: string;
  stance: number;
  ping: number;
  common: string;
  rules: string;
  status: number;
  createdAt: string | null;
}

export interface GameCreateInput {
  hostId: number;
  lobbyId: number;
  name: string;
  password?: string;
  comment?: string;
  maxPlayers?: number;
  currentGame?: number;
  games?: string;
  stance?: number;
  ping?: number;
  common?: string;
  rules?: string;
  status?: number;
}

@injectable()
export class GamesHttpService {
  constructor(private readonly gameService = inject(GameService)) {}

  async findAll(): Promise<GameResponse[]> {
    const rows = await this.gameService.findAll();
    return rows.map((r) => this.toGameResponse(r));
  }

  async findById(id: number): Promise<GameResponse> {
    const game = await this.gameService.findById(id);
    if (!game) {
      throw new ServerError("NOT_FOUND", "Game not found", 404);
    }
    return this.toGameResponse(game);
  }

  async create(data: GameCreateInput): Promise<GameResponse> {
    const created = await this.gameService.create(this.fromGameInput(data) as NewGame);
    return this.toGameResponse(created);
  }

  async update(id: number, data: Partial<GameCreateInput>): Promise<GameResponse> {
    const updated = await this.gameService.update(id, this.fromGameInput(data));
    if (!updated) {
      throw new ServerError("NOT_FOUND", "Game not found", 404);
    }
    return this.toGameResponse(updated);
  }

  async remove(id: number): Promise<void> {
    const game = await this.gameService.findById(id);
    if (!game) {
      throw new ServerError("NOT_FOUND", "Game not found", 404);
    }
    await this.gameService.delete(id);
  }

  private toGameResponse(game: Game): GameResponse {
    return {
      id: game.id,
      hostId: game.host_id,
      lobbyId: game.lobby_id,
      name: game.name,
      password: game.password,
      comment: game.comment,
      maxPlayers: game.max_players,
      currentGame: game.current_game,
      games: game.games,
      stance: game.stance,
      ping: game.ping,
      common: game.common,
      rules: game.rules,
      status: game.status,
      createdAt: game.created_at ? game.created_at.toISOString() : null,
    };
  }

  private fromGameInput(data: Partial<GameCreateInput>): Partial<Game> {
    const result: Partial<Game> = {};
    if (data.hostId !== undefined) result.host_id = data.hostId;
    if (data.lobbyId !== undefined) result.lobby_id = data.lobbyId;
    if (data.name !== undefined) result.name = data.name;
    if (data.password !== undefined) result.password = data.password;
    if (data.comment !== undefined) result.comment = data.comment;
    if (data.maxPlayers !== undefined) result.max_players = data.maxPlayers;
    if (data.currentGame !== undefined) result.current_game = data.currentGame;
    if (data.games !== undefined) result.games = data.games;
    if (data.stance !== undefined) result.stance = data.stance;
    if (data.ping !== undefined) result.ping = data.ping;
    if (data.common !== undefined) result.common = data.common;
    if (data.rules !== undefined) result.rules = data.rules;
    if (data.status !== undefined) result.status = data.status;
    return result;
  }
}
