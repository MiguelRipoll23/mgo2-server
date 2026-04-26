import { inject, injectable } from "@needle-di/core";
import { OpenAPIHono } from "@hono/zod-openapi";
import { AuthenticationRouter } from "./public/authentication-router.ts";
import { VersionRouter } from "./public/version-router.ts";
import { DataListRouter } from "./public/data-list-router.ts";
import { PolicyRouter } from "./public/policy-router.ts";
import { FilesRouter } from "./public/files-router.ts";
import { RegistrationRouter } from "./public/registration-router.ts";

@injectable()
export class PublicRouter {
  private app: OpenAPIHono;

  constructor(
    private authentication = inject(AuthenticationRouter),
    private versionRouter = inject(VersionRouter),
    private dataListRouter = inject(DataListRouter),
    private policyRouter = inject(PolicyRouter),
    private filesRouter = inject(FilesRouter),
    private registrationRouter = inject(RegistrationRouter),
  ) {
    this.app = new OpenAPIHono();
    this.setRoutes();
  }

  public getRouter(): OpenAPIHono {
    return this.app;
  }

  private setRoutes(): void {
    this.app.route("/account", this.registrationRouter.getRouter());
    this.app.route("/files", this.filesRouter.getRouter());
    this.app.route("/jp/mgo2/policy", this.policyRouter.getRouter());
    this.app.route("/jp/mgo2//patch", this.versionRouter.getRouter());
    this.app.route("/jp/mgo2/data", this.dataListRouter.getRouter());
    this.app.route(
      "/Z4qIOLmQBOj4NQo0uHx3q0mE51Fe/",
      this.authentication.getRouter(),
    );
  }
}
