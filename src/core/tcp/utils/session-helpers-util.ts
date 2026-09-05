import { encodePacket, encodeErrorPacket } from "../services/packet-codec-service.ts";
import type { TcpSession } from "../types/session-type.ts";
import { writeLoggedBytes } from "./traffic-logger-util.ts";

function normalizePayload(
  payload?: Uint8Array | Uint8Array[] | null,
): Uint8Array {
  if (payload === null || payload === undefined) {
    return new Uint8Array(0);
  }

  if (!Array.isArray(payload)) {
    return payload;
  }

  const totalLength = payload.reduce((sum, chunk) => sum + chunk.length, 0);
  const merged = new Uint8Array(totalLength);
  let offset = 0;

  for (const chunk of payload) {
    merged.set(chunk, offset);
    offset += chunk.length;
  }

  return merged;
}

export async function sendPacket(
  session: TcpSession,
  commandId: number,
  payload?: Uint8Array | Uint8Array[] | null,
): Promise<void> {
  const bytes = encodePacket(
    commandId,
    normalizePayload(payload),
    session.sequenceOut,
    session.logPrefix,
  );
  session.sequenceOut++;
  await writeLoggedBytes(session, bytes);
}

export async function sendStartEndPacket(
  session: TcpSession,
  commandId: number,
): Promise<void> {
  await sendPacket(session, commandId, new Uint8Array(4));
}

/**
 * Sends an explicit {u32 result} payload — the shape most "result-style" reply
 * parsers actually read. An empty payload does NOT fail the client: its readers
 * bound-check the 1023-byte receive buffer rather than the payload length, so a
 * short reply is filled from stale buffer content (the failure mode behind
 * several "works but only sometimes" reports).
 */
export async function sendResult(
  session: TcpSession,
  commandId: number,
  result: number,
): Promise<void> {
  const payload = new Uint8Array(4);
  new DataView(payload.buffer).setUint32(0, result >>> 0, false);
  await sendPacket(session, commandId, payload);
}

export async function sendAck(session: TcpSession, commandId: number): Promise<void> {
  await sendPacket(session, commandId, null);
}

export async function sendError(
  session: TcpSession,
  commandId: number,
  errorCode: number,
): Promise<void> {
  const bytes = encodeErrorPacket(commandId, errorCode, session.sequenceOut, session.logPrefix);
  session.sequenceOut++;
  await writeLoggedBytes(session, bytes);
}
