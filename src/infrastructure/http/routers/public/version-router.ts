import { inject, injectable } from "@needle-di/core";
import { createRoute, OpenAPIHono } from "@hono/zod-openapi";
import {
  checkVerRequestSchema,
  checkVerResponseSchema,
} from "../../schemas/checkver-schemas.ts";
import { VersionService } from "../../services/version-service.ts";

@injectable()
export class VersionRouter {
  private app: OpenAPIHono;

  constructor(private versionService = inject(VersionService)) {
    this.app = new OpenAPIHono();
    this.setRoutes();
  }

  public getRouter(): OpenAPIHono {
    return this.app;
  }

  private setRoutes(): void {
    this.registerCheckVerRoute();
    this.registerCheckVerRouteAlt();
  }

  private registerCheckVerRoute(): void {
    this.app.openapi(
      createRoute({
        method: "post",
        path: "/checkver.html",
        tags: ["Game"],
        summary: "Check version",
        description: "Returns the result of the version check",
        request: {
          body: {
            content: {
              "application/x-www-form-urlencoded": { schema: checkVerRequestSchema },
            },
          },
        },
        responses: {
          200: {
            description: "Version check result. 1 = up to date.",
            content: { "text/html": { schema: checkVerResponseSchema } },
          },
        },
      }),
      () => this.versionService.buildCheckVerResponse() as never,
    );
  }

  private registerCheckVerRouteAlt(): void {
    this.app.post("//checkver.html", () => this.versionService.buildCheckVerResponse());
  }
}
