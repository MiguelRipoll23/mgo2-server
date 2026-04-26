import { OpenAPIHono } from "@hono/zod-openapi";
import { Scalar } from "@scalar/hono-api-reference";

export class OpenAPIService {
  public static configure(app: OpenAPIHono): void {
    app.openAPIRegistry.registerComponent("securitySchemes", "bearer", {
      type: "http",
      scheme: "bearer",
      bearerFormat: "JWT",
    });
  }

  public static setRoutes(app: OpenAPIHono): void {
    app.doc31("/.well-known/openapi", {
      openapi: "3.1.0",
      info: {
        version: "1.0.0",
        title: "MGO2 HTTP API",
        description: "MGO2 server HTTP API",
      },
    });

    app.get(
      "/",
      Scalar({
        url: "/.well-known/openapi",
        pageTitle: "MGO2 HTTP API",
        metaData: {
          title: "MGO2 HTTP API",
          description: "MGO2 server HTTP API",
          ogTitle: "MGO2 HTTP API",
          ogDescription: "MGO2 server HTTP API",
        },
        darkMode: true,
        defaultOpenAllTags: true,
        authentication: {
          preferredSecurityScheme: "bearer",
        },
        persistAuth: true,
      }),
    );
  }
}
