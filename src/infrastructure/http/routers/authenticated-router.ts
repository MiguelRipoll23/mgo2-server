import { inject, injectable } from "@needle-di/core";
import { jwt } from "hono/jwt";
import { OpenAPIHono } from "@hono/zod-openapi";
import { NewsRouter } from "./authenticated/news-router.ts";
import { LobbiesRouter } from "./authenticated/lobbies-router.ts";
import { GamesRouter } from "./authenticated/games-router.ts";
import { FlashNewsRouter } from "./authenticated/flash-news-router.ts";

const jwtSecret = Deno.env.get("JWT_SECRET") ??
  (() => {
    throw new Error("JWT_SECRET environment variable is required");
  })();

@injectable()
export class AuthenticatedRouter {
  private app: OpenAPIHono;

  constructor(
    private newsRouter = inject(NewsRouter),
    private lobbiesRouter = inject(LobbiesRouter),
    private gamesRouter = inject(GamesRouter),
    private flashNewsRouter = inject(FlashNewsRouter),
  ) {
    this.app = new OpenAPIHono();
    this.setMiddlewares();
    this.setRoutes();
  }

  public getRouter(): OpenAPIHono {
    return this.app;
  }

  private setMiddlewares(): void {
    this.app.use("*", jwt({ secret: jwtSecret, alg: "HS256" }));
  }

  private setRoutes(): void {
    this.app.route("/news", this.newsRouter.getRouter());
    this.app.route("/flash-news", this.flashNewsRouter.getRouter());
    this.app.route("/lobbies", this.lobbiesRouter.getRouter());
    this.app.route("/games", this.gamesRouter.getRouter());
  }
}
