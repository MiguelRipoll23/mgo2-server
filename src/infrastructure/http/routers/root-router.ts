import { inject, injectable } from "@needle-di/core";
import { OpenAPIHono } from "@hono/zod-openapi";
import { PublicRouter } from "./public-router.ts";
import { AuthenticatedRouter } from "./authenticated-router.ts";

@injectable()
export class RootRouter {
  private app: OpenAPIHono;

  constructor(
    private publicRouter = inject(PublicRouter),
    private authenticatedRouter = inject(AuthenticatedRouter),
  ) {
    this.app = new OpenAPIHono();
    this.setRoutes();
  }

  public getRouter(): OpenAPIHono {
    return this.app;
  }

  private setRoutes(): void {
    this.app.route("/", this.publicRouter.getRouter());
    this.app.route("/", this.authenticatedRouter.getRouter());
  }
}
