import type { Packet } from "./packet-type.ts";
import type { ServerType } from "./server-type.ts";

export interface TcpSession {
  serverType: ServerType;
  logPrefix: string;
  connection: Deno.TcpConn;
  remoteAddress: string;
  sequenceIn: number;
  sequenceOut: number;
  userId: number | null;
  characterId: number | null;
  lobbyId: number | null;
  gameId: number | null;
}

export type CommandHandler = (session: TcpSession, packet: Packet) => Promise<void>;
