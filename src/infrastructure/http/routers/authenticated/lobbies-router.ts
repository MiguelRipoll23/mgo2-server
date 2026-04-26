import { inject, injectable } from "@needle-di/core";
import { createRoute, OpenAPIHono } from "@hono/zod-openapi";
import {
  lobbyErrorSchema,
  lobbyParamsSchema,
  lobbyPatchRequestSchema,
  lobbyRequestSchema,
  lobbiesListResponseSchema,
  lobbyResponseSchema,
} from "../../schemas/lobbies-schemas.ts";
import { LobbyService } from "../../../../modules/lobby/lobby-service.ts";

const security = [{ bearerAuth: [] }];

@injectable()
export class LobbiesRouter {
  private app: OpenAPIHono;

  constructor(private lobbyService = inject(LobbyService)) {
    this.app = new OpenAPIHono();
    this.setRoutes();
  }

  public getRouter(): OpenAPIHono {
    return this.app;
  }

  private setRoutes(): void {
    this.registerListLobbiesRoute();
    this.registerGetLobbyRoute();
    this.registerCreateLobbyRoute();
    this.registerPatchLobbyRoute();
    this.registerDeleteLobbyRoute();
  }

  private registerListLobbiesRoute(): void {
    this.app.openapi(
      createRoute({
        method: "get",
        path: "/",
        tags: ["Lobbies"],
        summary: "List all lobbies",
        description: "Returns a list of all registered lobbies ordered by name",
        security,
        responses: {
          200: {
            description: "A list of all registered lobbies",
            content: { "application/json": { schema: lobbiesListResponseSchema } },
          },
        },
      }),
      async (c) => {
        const items = await this.lobbyService.findAll();
        return c.json(items, 200);
      },
    );
  }

  private registerGetLobbyRoute(): void {
    this.app.openapi(
      createRoute({
        method: "get",
        path: "/:id",
        tags: ["Lobbies"],
        summary: "Get a lobby by ID",
        description: "Returns a single lobby by its numeric identifier",
        security,
        request: { params: lobbyParamsSchema },
        responses: {
          200: {
            description: "The requested lobby",
            content: { "application/json": { schema: lobbyResponseSchema } },
          },
          404: {
            description: "Not found",
            content: { "application/json": { schema: lobbyErrorSchema } },
          },
        },
      }),
      async (c) => {
        const { id } = c.req.valid("param");
        const item = await this.lobbyService.findById(id);
        return c.json(item, 200);
      },
    );
  }

  private registerCreateLobbyRoute(): void {
    this.app.openapi(
      createRoute({
        method: "post",
        path: "/",
        tags: ["Lobbies"],
        summary: "Create a lobby",
        description: "Creates a new lobby and returns the created resource",
        security,
        request: { body: { content: { "application/json": { schema: lobbyRequestSchema } } } },
        responses: {
          201: {
            description: "The newly created lobby",
            content: { "application/json": { schema: lobbyResponseSchema } },
          },
        },
      }),
      async (c) => {
        const body = c.req.valid("json");
        const created = await this.lobbyService.create(body);
        return c.json(created, 201);
      },
    );
  }

  private registerPatchLobbyRoute(): void {
    this.app.openapi(
      createRoute({
        method: "patch",
        path: "/:id",
        tags: ["Lobbies"],
        summary: "Update a lobby",
        description: "Partially updates an existing lobby by its numeric identifier",
        security,
        request: {
          params: lobbyParamsSchema,
          body: { content: { "application/json": { schema: lobbyPatchRequestSchema } } },
        },
        responses: {
          200: {
            description: "Updated",
            content: { "application/json": { schema: lobbyResponseSchema } },
          },
          404: {
            description: "Not found",
            content: { "application/json": { schema: lobbyErrorSchema } },
          },
        },
      }),
      async (c) => {
        const { id } = c.req.valid("param");
        const body = c.req.valid("json");
        const updated = await this.lobbyService.update(id, body);
        return c.json(updated, 200);
      },
    );
  }

  private registerDeleteLobbyRoute(): void {
    this.app.openapi(
      createRoute({
        method: "delete",
        path: "/:id",
        tags: ["Lobbies"],
        summary: "Delete a lobby",
        description: "Permanently removes a lobby by its numeric identifier",
        security,
        request: { params: lobbyParamsSchema },
        responses: {
          204: { description: "Lobby successfully deleted" },
          404: {
            description: "Not found",
            content: { "application/json": { schema: lobbyErrorSchema } },
          },
        },
      }),
      async (c) => {
        const { id } = c.req.valid("param");
        await this.lobbyService.remove(id);
        return c.body(null, 204);
      },
    );
  }
}
