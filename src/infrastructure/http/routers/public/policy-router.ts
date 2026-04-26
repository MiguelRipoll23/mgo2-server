import { inject, injectable } from "@needle-di/core";
import { createRoute, OpenAPIHono } from "@hono/zod-openapi";
import { PolicyService } from "../../services/policy-service.ts";

@injectable()
export class PolicyRouter {
  private app: OpenAPIHono;

  constructor(private policyService = inject(PolicyService)) {
    this.app = new OpenAPIHono();
    this.setRoutes();
  }

  public getRouter(): OpenAPIHono {
    return this.app;
  }

  private setRoutes(): void {
    this.app.openapi(
      createRoute({
        method: "get",
        path: "/policy.txt",
        tags: ["Game"],
        summary: "Policy document",
        description: "Returns the terms of service policy document",
        responses: {
          200: {
            description: "Policy text",
            content: { "text/html": { schema: { type: "string" } } },
          },
        },
      }),
      async () => {
        const body = await this.policyService.getPolicy();
        return new Response(body, {
          status: 200,
          headers: { "content-type": "text/html; charset=utf-8" },
        }) as never;
      },
    );
  }
}
