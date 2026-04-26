import { injectable } from "@needle-di/core";
import type { TcpSession } from "../../../core/tcp/types/session-type.ts";

@injectable()
export class ActiveGameSessionsService {
  private readonly sessions = new Set<TcpSession>();

  add(session: TcpSession): void {
    this.sessions.add(session);
  }

  remove(session: TcpSession): void {
    this.sessions.delete(session);
  }

  list(): TcpSession[] {
    return [...this.sessions];
  }
}
