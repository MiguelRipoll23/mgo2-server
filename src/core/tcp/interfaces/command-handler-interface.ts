import type { TcpSession } from "../types/session-type.ts";
import type { Packet } from "../types/packet-type.ts";

export interface ICommandHandler {
  handle(session: TcpSession, packet: Packet): Promise<void>;
  // deno-lint-ignore no-explicit-any
  setLogger?(log: any): void;
}
