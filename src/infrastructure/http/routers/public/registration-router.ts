import { inject, injectable } from "@needle-di/core";
import { createRoute, OpenAPIHono } from "@hono/zod-openapi";
import {
  registerErrorSchema,
  registerRequestSchema,
  registerResponseSchema,
} from "../../schemas/register-schemas.ts";
import { RegistrationService } from "../../../../modules/auth/registration-service.ts";

@injectable()
export class RegistrationRouter {
  private app: OpenAPIHono;

  constructor(private registrationService = inject(RegistrationService)) {
    this.app = new OpenAPIHono();
    this.setRoutes();
  }

  public getRouter(): OpenAPIHono {
    return this.app;
  }

  private setRoutes(): void {
    this.app.openapi(
      createRoute({
        method: "post",
        path: "/register",
        tags: ["Account"],
        summary: "Register account",
        description: "Creates a new player account.",
        request: {
          body: {
            content: { "application/json": { schema: registerRequestSchema } },
          },
        },
        responses: {
          201: {
            description: "Account created",
            content: { "application/json": { schema: registerResponseSchema } },
          },
          409: {
            description: "Username already taken",
            content: { "application/json": { schema: registerErrorSchema } },
          },
        },
      }),
      async (context) => {
        const body = context.req.valid("json");
        const created = await this.registrationService.register(
          body.displayName,
          body.password,
        );
        return context.json(created, 201);
      },
    );
  }
}
