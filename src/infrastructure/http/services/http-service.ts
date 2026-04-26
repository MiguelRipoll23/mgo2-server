import { logger } from "hono/logger";
import { bodyLimit } from "hono/body-limit";
import { OpenAPIHono } from "@hono/zod-openapi";
import { inject, injectable } from "@needle-di/core";
import { RootRouter } from "../routers/root-router.ts";
import { ServerError } from "../../../core/errors/server-error.ts";
import { HTTP_PORT } from "../../../core/constants/ports-constants.ts";
import { ErrorHandlingService } from "../services/error-handling-service.ts";
import { OpenAPIService } from "../services/openapi-service.ts";

@injectable()
export class HTTPService {
  private app: OpenAPIHono;

  constructor(private rootRouter = inject(RootRouter)) {
    this.app = new OpenAPIHono();
    this.configure();
    this.setMiddlewares();
    this.setRoutes();
  }

  public listen(): void {
    Deno.serve(
      {
        port: HTTP_PORT,
        onListen: ({ port }: { port: number }) =>
          console.log(`[http] Listening on port ${port}`),
      },
      this.app.fetch,
    );
  }

  private configure(): void {
    ErrorHandlingService.configure(this.app);
    OpenAPIService.configure(this.app);
  }

  private setMiddlewares(): void {
    this.app.use("*", logger());
    this.setBodyLimitMiddleware();
  }

  private setBodyLimitMiddleware(): void {
    this.app.use(
      "*",
      bodyLimit({
        maxSize: 1024 * 1024,
        onError: () => {
          throw new ServerError(
            "BODY_SIZE_LIMIT_EXCEEDED",
            "Request body size limit exceeded",
            413,
          );
        },
      }),
    );
  }

  private setRoutes(): void {
    OpenAPIService.setRoutes(this.app);
    this.app.get("/favicon.ico", (c) =>
      c.redirect("https://favi.deno.dev/🦖.png", 302));
    this.app.route("/", this.rootRouter.getRouter());
  }
}
