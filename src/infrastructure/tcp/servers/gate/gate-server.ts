import { BaseTcpServer } from "../../../../core/tcp/services/base-tcp-server-service.ts";
import "./commands/get-news.ts";
import "./commands/get-lobby-list.ts";

export class GateServer extends BaseTcpServer {
  protected readonly serverType = "gate" as const;
  protected readonly port: number;

  constructor(port: number) {
    super();
    this.port = port;
  }
}
