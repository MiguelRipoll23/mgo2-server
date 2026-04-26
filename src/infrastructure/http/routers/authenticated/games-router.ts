import { inject, injectable } from "@needle-di/core";
import { createRoute, OpenAPIHono } from "@hono/zod-openapi";
import {
  gameErrorSchema,
  gameParamsSchema,
  gamePatchRequestSchema,
  gameRequestSchema,
  gamesListResponseSchema,
  gameResponseSchema,
} from "../../schemas/games-schemas.ts";
import { GamesHttpService } from "../../adapters/games-http-adapter.ts";

const security = [{ bearerAuth: [] }];

@injectable()
export class GamesRouter {
  private app: OpenAPIHono;

  constructor(private gamesHttpService = inject(GamesHttpService)) {
    this.app = new OpenAPIHono();
    this.setRoutes();
  }

  public getRouter(): OpenAPIHono {
    return this.app;
  }

  private setRoutes(): void {
    this.registerListGamesRoute();
    this.registerGetGameRoute();
    this.registerCreateGameRoute();
    this.registerPatchGameRoute();
    this.registerDeleteGameRoute();
  }

  private registerListGamesRoute(): void {
    this.app.openapi(
      createRoute({
        method: "get",
        path: "/",
        tags: ["Games"],
        summary: "List all games",
        description: "Returns a list of all active game rooms ordered by ID",
        security,
        responses: {
          200: {
            description: "A list of all active game rooms",
            content: { "application/json": { schema: gamesListResponseSchema } },
          },
        },
      }),
      async (c) => {
        const items = await this.gamesHttpService.findAll();
        return c.json(items, 200);
      },
    );
  }

  private registerGetGameRoute(): void {
    this.app.openapi(
      createRoute({
        method: "get",
        path: "/:id",
        tags: ["Games"],
        summary: "Get a game by ID",
        description: "Returns a single game room by its numeric identifier",
        security,
        request: { params: gameParamsSchema },
        responses: {
          200: {
            description: "The requested game room",
            content: { "application/json": { schema: gameResponseSchema } },
          },
          404: {
            description: "Not found",
            content: { "application/json": { schema: gameErrorSchema } },
          },
        },
      }),
      async (c) => {
        const { id } = c.req.valid("param");
        const item = await this.gamesHttpService.findById(id);
        return c.json(item, 200);
      },
    );
  }

  private registerCreateGameRoute(): void {
    this.app.openapi(
      createRoute({
        method: "post",
        path: "/",
        tags: ["Games"],
        summary: "Create a game",
        description: "Creates a new game room and returns the created resource",
        security,
        request: { body: { content: { "application/json": { schema: gameRequestSchema } } } },
        responses: {
          201: {
            description: "The newly created game room",
            content: { "application/json": { schema: gameResponseSchema } },
          },
        },
      }),
      async (c) => {
        const body = c.req.valid("json");
        const created = await this.gamesHttpService.create(body);
        return c.json(created, 201);
      },
    );
  }

  private registerPatchGameRoute(): void {
    this.app.openapi(
      createRoute({
        method: "patch",
        path: "/:id",
        tags: ["Games"],
        summary: "Update a game",
        description: "Partially updates an existing game room by its numeric identifier",
        security,
        request: {
          params: gameParamsSchema,
          body: { content: { "application/json": { schema: gamePatchRequestSchema } } },
        },
        responses: {
          200: {
            description: "The updated game room",
            content: { "application/json": { schema: gameResponseSchema } },
          },
          404: {
            description: "Not found",
            content: { "application/json": { schema: gameErrorSchema } },
          },
        },
      }),
      async (c) => {
        const { id } = c.req.valid("param");
        const body = c.req.valid("json");
        const updated = await this.gamesHttpService.update(id, body);
        return c.json(updated, 200);
      },
    );
  }

  private registerDeleteGameRoute(): void {
    this.app.openapi(
      createRoute({
        method: "delete",
        path: "/:id",
        tags: ["Games"],
        summary: "Delete a game",
        description: "Permanently removes a game room by its numeric identifier",
        security,
        request: { params: gameParamsSchema },
        responses: {
          204: { description: "Game successfully deleted" },
          404: {
            description: "Not found",
            content: { "application/json": { schema: gameErrorSchema } },
          },
        },
      }),
      async (c) => {
        const { id } = c.req.valid("param");
        await this.gamesHttpService.remove(id);
        return c.body(null, 204);
      },
    );
  }
}
