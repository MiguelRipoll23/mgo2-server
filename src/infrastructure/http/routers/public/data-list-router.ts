import { injectable } from "@needle-di/core";
import { createRoute, OpenAPIHono } from "@hono/zod-openapi";
import {
  dataListRequestSchema,
  dataListResponseSchema,
} from "../../schemas/data-list-schemas.ts";

@injectable()
export class DataListRouter {
  private app: OpenAPIHono;

  constructor() {
    this.app = new OpenAPIHono();
    this.setRoutes();
  }

  public getRouter(): OpenAPIHono {
    return this.app;
  }

  private setRoutes(): void {
    this.registerDataListRoute();
  }

  private registerDataListRoute(): void {
    this.app.openapi(
      createRoute({
        method: "post",
        path: "/datalist.html",
        tags: ["Game"],
        summary: "Data list",
        description: "Game updates list",
        request: {
          body: {
            content: {
              "application/x-www-form-urlencoded": {
                schema: dataListRequestSchema,
              },
            },
          },
        },
        responses: {
          200: {
            description: "Empty body — no data patches available",
            content: { "text/html": { schema: dataListResponseSchema } },
          },
        },
      }),
      (c) => c.text("", 200),
    );
  }
}
