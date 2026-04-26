import { inject, injectable } from "@needle-di/core";
import { createRoute, OpenAPIHono } from "@hono/zod-openapi";
import {
  newsErrorSchema,
  newsItemResponseSchema,
  newsListResponseSchema,
  newsParamsSchema,
  newsPatchRequestSchema,
  newsRequestSchema,
} from "../../schemas/news-schemas.ts";
import { NewsService } from "../../../../modules/news/news-service.ts";

const security = [{ bearerAuth: [] }];

@injectable()
export class NewsRouter {
  private app: OpenAPIHono;

  constructor(private newsService = inject(NewsService)) {
    this.app = new OpenAPIHono();
    this.setRoutes();
  }

  public getRouter(): OpenAPIHono {
    return this.app;
  }

  private setRoutes(): void {
    this.registerListNewsRoute();
    this.registerGetNewsRoute();
    this.registerCreateNewsRoute();
    this.registerPatchNewsRoute();
    this.registerDeleteNewsRoute();
  }

  private registerListNewsRoute(): void {
    this.app.openapi(
      createRoute({
        method: "get",
        path: "/",
        tags: ["News"],
        summary: "List all news items",
        description: "Returns a list of all news items ordered by most recent first",
        security,
        responses: {
          200: {
            description: "A list of all news items",
            content: { "application/json": { schema: newsListResponseSchema } },
          },
        },
      }),
      async (c) => {
        const items = await this.newsService.findAll();
        return c.json(items, 200);
      },
    );
  }

  private registerGetNewsRoute(): void {
    this.app.openapi(
      createRoute({
        method: "get",
        path: "/:id",
        tags: ["News"],
        summary: "Get a news item by ID",
        description: "Returns a single news item by its numeric identifier",
        security,
        request: { params: newsParamsSchema },
        responses: {
          200: {
            description: "The requested news item",
            content: { "application/json": { schema: newsItemResponseSchema } },
          },
          404: {
            description: "Not found",
            content: { "application/json": { schema: newsErrorSchema } },
          },
        },
      }),
      async (c) => {
        const { id } = c.req.valid("param");
        const item = await this.newsService.findById(id);
        return c.json(item, 200);
      },
    );
  }

  private registerCreateNewsRoute(): void {
    this.app.openapi(
      createRoute({
        method: "post",
        path: "/",
        tags: ["News"],
        summary: "Create a news item",
        description: "Creates a new news item and returns the created resource",
        security,
        request: { body: { content: { "application/json": { schema: newsRequestSchema } } } },
        responses: {
          201: {
            description: "The newly created news item",
            content: { "application/json": { schema: newsItemResponseSchema } },
          },
        },
      }),
      async (c) => {
        const body = c.req.valid("json");
        const created = await this.newsService.create(body);
        return c.json(created, 201);
      },
    );
  }

  private registerPatchNewsRoute(): void {
    this.app.openapi(
      createRoute({
        method: "patch",
        path: "/:id",
        tags: ["News"],
        summary: "Update a news item",
        description: "Partially updates an existing news item by its numeric identifier",
        security,
        request: {
          params: newsParamsSchema,
          body: { content: { "application/json": { schema: newsPatchRequestSchema } } },
        },
        responses: {
          200: {
            description: "Updated",
            content: { "application/json": { schema: newsItemResponseSchema } },
          },
          404: {
            description: "Not found",
            content: { "application/json": { schema: newsErrorSchema } },
          },
        },
      }),
      async (c) => {
        const { id } = c.req.valid("param");
        const body = c.req.valid("json");
        const updated = await this.newsService.update(id, body);
        return c.json(updated, 200);
      },
    );
  }

  private registerDeleteNewsRoute(): void {
    this.app.openapi(
      createRoute({
        method: "delete",
        path: "/:id",
        tags: ["News"],
        summary: "Delete a news item",
        description: "Permanently removes a news item by its numeric identifier",
        security,
        request: { params: newsParamsSchema },
        responses: {
          204: { description: "News item successfully deleted" },
          404: {
            description: "Not found",
            content: { "application/json": { schema: newsErrorSchema } },
          },
        },
      }),
      async (c) => {
        const { id } = c.req.valid("param");
        await this.newsService.remove(id);
        return c.body(null, 204);
      },
    );
  }
}
