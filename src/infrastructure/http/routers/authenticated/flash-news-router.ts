import { inject, injectable } from "@needle-di/core";
import { createRoute, OpenAPIHono } from "@hono/zod-openapi";
import {
  flashNewsBroadcastRequestSchema,
  flashNewsEmergencyRequestSchema,
} from "../../schemas/flash-news-schemas.ts";
import {
  FlashNewsService,
  FlashNewsSubcommand,
} from "../../../tcp/services/flash-news-service.ts";

const security = [{ bearerAuth: [] }];

@injectable()
export class FlashNewsRouter {
  private app: OpenAPIHono;

  constructor(private flashNewsService = inject(FlashNewsService)) {
    this.app = new OpenAPIHono();
    this.setRoutes();
  }

  public getRouter(): OpenAPIHono {
    return this.app;
  }

  private setRoutes(): void {
    this.registerBroadcastRoute();
    this.registerEmergencyRoute();
  }

  private registerBroadcastRoute(): void {
    this.app.openapi(
      createRoute({
        method: "post",
        path: "/broadcast",
        tags: ["Flash News"],
        summary: "Broadcast a server message",
        description:
          "Sends a 0x4A50 ticker packet with subcommand 0x4A68 to every active player. " +
          "The message text is displayed in the client's ticker.",
        security,
        request: {
          body: {
            content: {
              "application/json": { schema: flashNewsBroadcastRequestSchema },
            },
          },
        },
        responses: {
          202: {
            description: "Message accepted for broadcast",
          },
        },
      }),
      async (c) => {
        const { unknown6, ...rest } = c.req.valid("json");
        await this.flashNewsService.broadcast({
          ...rest,
          subcommandHex: FlashNewsSubcommand.SERVER_MESSAGE,
          maintenanceTime: unknown6,
        });
        return c.body(null, 202);
      },
    );
  }

  private registerEmergencyRoute(): void {
    this.app.openapi(
      createRoute({
        method: "post",
        path: "/emergency",
        tags: ["Flash News"],
        summary: "Broadcast an emergency maintenance notice",
        description:
          "Sends a 0x4A50 ticker packet with subcommand 0x4A71 to every active player. " +
          "The client ignores the message text and displays a built-in emergency maintenance screen.",
        security,
        request: {
          body: {
            content: {
              "application/json": { schema: flashNewsEmergencyRequestSchema },
            },
          },
        },
        responses: {
          202: {
            description: "Emergency notice accepted for broadcast",
          },
        },
      }),
      async (c) => {
        const body = c.req.valid("json");
        await this.flashNewsService.broadcast({
          ...body,
          subcommandHex: FlashNewsSubcommand.EMERGENCY_MAINTENANCE,
        });
        return c.body(null, 202);
      },
    );
  }

}
