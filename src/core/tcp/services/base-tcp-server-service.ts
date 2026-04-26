import type { ServerType } from "../types/server-type.ts";
import type { TcpSession } from "../types/session-type.ts";
import { commandRegistry } from "./command-registry-service.ts";
import { decodePacket, encodeErrorPacket } from "./packet-codec-service.ts";
import { sendPacket as sendPacketHelper } from "../utils/session-helpers-util.ts";
import {
  logTcpConnection,
  logTcpDisconnection,
  logTcpInPacket,
  writeLoggedBytes,
} from "../utils/traffic-logger-util.ts";
import { HEADER_SIZE } from "../types/packet-type.ts";
import type { Packet } from "../types/packet-type.ts";

/** Command ID sent by a client to request disconnection. */
const COMMAND_DISCONNECT = 0x0003;

/** Command ID used for keep-alive / ping exchanges. */
const COMMAND_KEEPALIVE = 0x0005;

/**
 * Upper 16 bits of the XOR key (0x5A7085AF), used to peek at the command field
 * before full packet decryption.
 */
const XOR_COMMAND_MASK = (0x5a7085af >>> 16) & 0xffff;

/**
 * Lower 16 bits of the XOR key (0x5A7085AF), used to peek at the payloadLength
 * field before full packet decryption.
 */
const XOR_PAYLOAD_LENGTH_MASK = 0x5a7085af & 0xffff;

function readU16Be(buffer: Uint8Array, offset: number): number {
  return (buffer[offset] << 8) | buffer[offset + 1];
}

function isConnectionClosedError(error: unknown): boolean {
  return (
    error instanceof Deno.errors.ConnectionReset ||
    error instanceof Deno.errors.BrokenPipe ||
    error instanceof Deno.errors.ConnectionAborted ||
    error instanceof Deno.errors.BadResource
  );
}

function formatCommand(command: number): string {
  return `0x${command.toString(16).padStart(4, "0")}`;
}

function formatGateSessionState(session: TcpSession): string {
  const authOk = session.userId !== null;
  const userId = session.userId ?? "none";
  const characterId = session.characterId ?? "none";
  const lobbyId = session.lobbyId ?? "none";
  const gameId = session.gameId ?? "none";
  return `auth=${authOk ? "ok" : "missing"} userId=${userId} characterId=${characterId} lobbyId=${lobbyId} gameId=${gameId}`;
}

import { createLogger } from "../utils/logger-util.ts";

export abstract class BaseTcpServer {
  protected abstract readonly serverType: ServerType;
  protected abstract readonly port: number;

  protected get logPrefix(): string {
    return `tcp:${this.serverType}`;
  }

  private _log: ReturnType<typeof createLogger> | null = null;
  protected get log(): ReturnType<typeof createLogger> {
    if (!this._log) {
      this._log = createLogger(this.logPrefix);
    }
    return this._log;
  }

  private listener: Deno.TcpListener | null = null;
  private readonly sessions = new Map<string, TcpSession>();

  /** Start listening for incoming TCP connections. */
  async start(): Promise<void> {
    this.listener = Deno.listen({ port: this.port }) as Deno.TcpListener;
    this.log.info(`Listening on port ${this.port}`);
    for await (const connection of this.listener) {
      void this.handleConnection(connection as Deno.TcpConn);
    }
  }

  /** Stop accepting new connections. Existing sessions are left to finish. */
  stop(): void {
    this.listener?.close();
    this.listener = null;
  }

  // ---------------------------------------------------------------------------
  // Connection lifecycle
  // ---------------------------------------------------------------------------

  private async handleConnection(connection: Deno.TcpConn): Promise<void> {
    const netAddress = connection.remoteAddr as Deno.NetAddr;
    const remoteAddress = `${netAddress.hostname}:${netAddress.port}`;

    const session: TcpSession = {
      serverType: this.serverType,
      logPrefix: this.logPrefix,
      connection,
      remoteAddress,
      // Default sequence numbers inferred from network dumps:
      // - client (incoming) first sequence = 0
      // - server (outgoing) first sequence = 1
      sequenceIn: 0,
      sequenceOut: 1,
      userId: null,
      characterId: null,
      lobbyId: null,
      gameId: null,
    };

    this.sessions.set(remoteAddress, session);
    logTcpConnection(this.logPrefix, remoteAddress);
    this.onSessionCreated(session);

    try {
      await this.runReadLoop(session);
    } catch (error) {
      if (!isConnectionClosedError(error)) {
        this.log.error(`Error for ${remoteAddress}:`, error);
      }
    } finally {
      this.sessions.delete(remoteAddress);
      this.onSessionDestroyed(session);
      logTcpDisconnection(this.logPrefix, remoteAddress);

      try {
        connection.close();
      } catch {
        // Already closed — ignore.
      }
    }
  }

  // ---------------------------------------------------------------------------
  // Read loop
  // ---------------------------------------------------------------------------

  private async runReadLoop(session: TcpSession): Promise<void> {
    let accumulated: Uint8Array<ArrayBuffer> = new Uint8Array(0);

    while (true) {
      const chunk = new Uint8Array(4096);
      const bytesRead = await session.connection.read(chunk);
      if (bytesRead === null) break; // Remote end closed the connection.

      const merged = new Uint8Array(accumulated.length + bytesRead);
      merged.set(accumulated);
      merged.set(chunk.subarray(0, bytesRead), accumulated.length);
      accumulated = merged;

      accumulated = await this.processAccumulatedBytes(session, accumulated);
    }
  }

  /**
   * Consumes complete packets from the accumulated byte buffer, dispatching
   * each one, and returns the remaining unconsumed bytes.
   */
  private async processAccumulatedBytes(
    session: TcpSession,
    accumulated: Uint8Array<ArrayBuffer>,
  ): Promise<Uint8Array<ArrayBuffer>> {
    while (accumulated.length >= HEADER_SIZE) {
      // Peek at the payload length before full XOR decryption.
      const rawPayloadLength = readU16Be(accumulated, 2);
      const payloadLength =
        (rawPayloadLength ^ XOR_PAYLOAD_LENGTH_MASK) & 0xffff;
      const totalPacketLength = HEADER_SIZE + payloadLength;

      if (accumulated.length < totalPacketLength) break;

      const packetBytes = accumulated.slice(0, totalPacketLength);
      accumulated = accumulated.slice(totalPacketLength);

      const packet = decodePacket(packetBytes);
      if (packet === null) {
        // Attempt to peek the XOR'd command field for logging purposes.
        const rawCommand = readU16Be(packetBytes, 0);
        const command = (rawCommand ^ XOR_COMMAND_MASK) & 0xffff;
        this.log.warn(
          `${formatCommand(command)} failed reason=decode from ${session.remoteAddress}`,
        );
        continue;
      }

      // Log the incoming decrypted packet.
      logTcpInPacket(session, packetBytes);

      session.sequenceIn++;

      const shouldContinue = await this.dispatchPacket(session, packet);
      if (!shouldContinue) break;
    }

    return accumulated;
  }

  // ---------------------------------------------------------------------------
  // Dispatch
  // ---------------------------------------------------------------------------

  /**
   * Routes a decoded packet to its handler.
   * Returns false when the connection should be closed (disconnect command).
   */
  private async dispatchPacket(
    session: TcpSession,
    packet: Packet,
  ): Promise<boolean> {
    const { command } = packet.header;

    if (command === COMMAND_DISCONNECT) {
      this.log.debug(
        `${formatCommand(command)} disconnect requested ${formatGateSessionState(session)}`,
      );
      return false;
    }

    if (command === COMMAND_KEEPALIVE) {
      await this.sendPacket(session, COMMAND_KEEPALIVE, null);
      this.log.debug(
        `${formatCommand(command)} keepalive ack ${formatGateSessionState(session)}`,
      );
      return true;
    }

    const handler = commandRegistry.resolve(this.serverType, command);
    if (!handler) {
      this.log.warn(
        `${formatCommand(command)} no-handler ${formatGateSessionState(session)}`,
      );
      return true;
    }

    if ("setLogger" in handler && typeof handler.setLogger === "function") {
      handler.setLogger(this.log);
    }

    this.log.debug(
      `${formatCommand(command)} processing ${formatGateSessionState(session)}`,
    );

    try {
      await handler.handle(session, packet);
      this.log.debug(
        `${formatCommand(command)} ok ${formatGateSessionState(session)}`,
      );
    } catch (error) {
      const reason = error instanceof Error ? error.message : String(error);
      this.log.debug(
        `${formatCommand(command)} failed reason=${reason} ${formatGateSessionState(session)}`,
      );
      throw error;
    }

    return true;
  }

  // ---------------------------------------------------------------------------
  // Send helpers
  // ---------------------------------------------------------------------------

  protected async sendPacket(
    session: TcpSession,
    commandId: number,
    payload: Uint8Array | null,
  ): Promise<void> {
    await sendPacketHelper(session, commandId, payload);
  }

  protected async sendError(
    session: TcpSession,
    commandId: number,
    errorCode: number,
  ): Promise<void> {
    const packetBuffer = encodeErrorPacket(
      commandId,
      errorCode,
      session.sequenceOut,
      this.logPrefix,
    );
    session.sequenceOut++;
    await writeLoggedBytes(session, packetBuffer);
  }

  // ---------------------------------------------------------------------------
  // Session lifecycle hooks (override in subclass as needed)
  // ---------------------------------------------------------------------------

  protected onSessionCreated(_session: TcpSession): void {}
  protected onSessionDestroyed(_session: TcpSession): void {}
}
