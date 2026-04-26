import { inject, injectable } from "@needle-di/core";
import { createRoute, OpenAPIHono } from "@hono/zod-openapi";
import {
  loginRequestSchema,
  loginResponseSchema,
} from "../../schemas/authentication-schemas.ts";
import { AuthenticationService } from "../../../../modules/auth/authentication-service.ts";

@injectable()
export class AuthenticationRouter {
  private app: OpenAPIHono;

  constructor(private authenticationService = inject(AuthenticationService)) {
    this.app = new OpenAPIHono();
    this.setRoutes();
  }

  public getRouter(): OpenAPIHono {
    return this.app;
  }

  private setRoutes(): void {
    this.registerLoginRoute();
  }

  private registerLoginRoute(): void {
    this.app.openapi(
      createRoute({
        method: "post",
        path: `/`,
        tags: ["Game"],
        summary: "Login",
        description: "Authenticates a player and returns a session token",
        request: {
          body: {
            content: {
              "application/x-www-form-urlencoded": {
                schema: loginRequestSchema,
              },
            },
          },
        },
        responses: {
          200: {
            description: "Login result as plain text",
            content: { "text/html": { schema: loginResponseSchema } },
          },
        },
      }),
      async (context) => {
        const rawBody = await context.req.parseBody();
        const parsedBody = loginRequestSchema.safeParse(rawBody);

        if (!parsedBody.success) {
          return context.text("1,0,0,0000000000000000");
        }

        const body = parsedBody.data;
        const response = await this.authenticationService.login(
          body.name,
          body.passwd,
        );
        return context.text(response);
      },
    );
  }
}
