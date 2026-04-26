import { inject, injectable } from "@needle-di/core";
import { OpenAPIHono } from "@hono/zod-openapi";
import { FilesService } from "../../services/files-service.ts";

@injectable()
export class FilesRouter {
  private app: OpenAPIHono;

  constructor(private filesService = inject(FilesService)) {
    this.app = new OpenAPIHono();
    this.setRoutes();
  }

  public getRouter(): OpenAPIHono {
    return this.app;
  }

  private setRoutes(): void {
    this.app.get("/*", (c) => {
      const filePath = c.req.path.replace(/^\/files\//, "");
      return this.filesService.getFile(filePath);
    });

    this.app.on("HEAD", "/*", (c) => {
      const filePath = c.req.path.replace(/^\/files\//, "");
      return this.filesService.getFile(filePath, "HEAD");
    });
  }
}
