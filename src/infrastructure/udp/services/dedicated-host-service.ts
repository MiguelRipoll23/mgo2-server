// DedicatedHostService — the standalone UDP p2p host on ONE port. It owns the
// socket and the decode pipeline, keeps a PeerSessionService for negotiated
// sessions, and dispatches decoded messages to @PeerCommandHandler handlers.
// Handshake acceptance and keep-alive ACKs are ordinary command handlers
// (see ./commands/) — this class stays transport-only.

import { encodeFrame, removeChainInPlace, unscrambleHeaderOnly, verifyTailDigest } from "../../../core/udp/utils/frame-crypto-util.ts";
import { decodeFrame, frameToLogBytes } from "../../../core/udp/utils/message-codec-util.ts";
import {
  UDP_PRE_HANDSHAKE_KEY,
  UDP_TAIL_DIGEST_KEY,
} from "../../../core/udp/constants/udp-crypto-keys-constants.ts";
import {
  UDP_ACK_BODY,
  UDP_ACK_FIRST_ATTEMPT,
  UDP_COUNTER_MASK,
  ackTypeOf,
} from "../../../core/udp/constants/udp-commands-constants.ts";
import {
  UDP_HOST_COUNTER_BASE,
  UDP_HOST_PEER_ID,
} from "../../../core/udp/constants/udp-host-identity-constants.ts";
import type { PeerContext } from "../../../core/udp/interfaces/peer-command-handler-interface.ts";
import type { PeerSession, UdpMessage } from "../../../core/udp/types/udp-type.ts";
import { peerCommandRegistry } from "../../../core/udp/services/peer-command-registry-service.ts";
import { PeerSessionService } from "../../../core/udp/services/peer-session-service.ts";
import { buildMessageFrame, messageOf } from "../../../core/udp/utils/frame-builder-util.ts";
import {
  formatUdpCounter,
  formatUdpMessageType,
  formatUdpU32,
  logUdpInPacket,
  logUdpOutPacket,
} from "../../../core/udp/utils/udp-logger-util.ts";
import { u32LE } from "../../../core/udp/utils/binary-util.ts";

export class DedicatedHostService {
  private readonly sessions = new PeerSessionService();
  private socket: Deno.DatagramConn | null = null;
  // The host is always the seeded NPC character (id 1) — the joiner's drain
  // gate checks the reply's peer_id against that character (§4 gate 1).
  private readonly hostPeerId = UDP_HOST_PEER_ID;
  private readonly hostCounterBase = UDP_HOST_COUNTER_BASE;

  constructor(
    private readonly port: number,
    private readonly hostname = "0.0.0.0",
  ) {}

  private get logPrefix(): string {
    return `udp:${this.port}`;
  }

  async start(): Promise<void> {
    this.socket = Deno.listenDatagram({
      port: this.port,
      hostname: this.hostname,
      transport: "udp",
    });
    this.sessions.start();
    console.info(`[${this.logPrefix}] listening on ${this.hostname}:${this.port}`);
    await this.receiveLoop();
  }

  stop(): void {
    this.sessions.stop();
    this.socket?.close();
    this.socket = null;
  }

  private async receiveLoop(): Promise<void> {
    while (this.socket) {
      let received: [Uint8Array, Deno.NetAddr];
      try {
        received = (await this.socket.receive()) as [Uint8Array, Deno.NetAddr];
      } catch (error) {
        // A dial-back send to an unreachable endpoint surfaces as an ICMP
        // port-unreachable on the NEXT receive (Windows WSAECONNRESET);
        // stop() surfaces as Interrupted while a receive() is pending.
        if (
          error instanceof Deno.errors.ConnectionReset ||
          error instanceof Deno.errors.Interrupted
        ) {
          continue;
        }
        throw error;
      }
      await this.handleDatagram(received[0], received[1] as Deno.NetAddr);
    }
  }

  private async handleDatagram(wire: Uint8Array, remote: Deno.NetAddr): Promise<void> {
    if (remote.transport !== "udp") return;
    const remoteAddress = `${remote.hostname}:${remote.port}`;
    const work = wire.slice();

    // Classify by digest key BEFORE unchaining (§5.3): pre-keyed (state < 8)
    // frames verify with the bare constant; session-keyed with K ^ const.
    const session = this.sessions.get(remoteAddress);
    const preCounter = this.tryUnscrambleWith(work, UDP_TAIL_DIGEST_KEY);
    let keyedCounter: number | null = null;
    let sessionKey = 0;
    if (preCounter === null && session) {
      sessionKey = (session.counterBase ^ this.hostCounterBase) >>> 0;
      keyedCounter = this.tryUnscrambleWith(work, (sessionKey ^ UDP_TAIL_DIGEST_KEY) >>> 0);
    }

    if (preCounter !== null) {
      this.unchain(work, preCounter, UDP_PRE_HANDSHAKE_KEY);
      await this.dispatchPreKeyed(work, preCounter, remote, remoteAddress);
      return;
    }

    if (keyedCounter !== null && session) {
      this.unchain(work, keyedCounter, sessionKey);
      await this.dispatchKeyed(work, keyedCounter, sessionKey, session, remote);
      return;
    }

    console.warn(`[${this.logPrefix}] IN undecodable from ${remoteAddress}`);
  }

  /**
   * Unscramble + verify on a copy, then apply the same unscramble to `work`.
   * Returns the hdr counter, or null when the digest gate fails. The buffer is
   * left header-unscrambled with the chain still ON (the digest gate state).
   */
  private tryUnscrambleWith(work: Uint8Array, digestKey: number): number | null {
    const copy = work.slice();
    const counter = unscrambleHeaderOnly(copy);
    if (!verifyTailDigest(copy, digestKey)) return null;
    unscrambleHeaderOnly(work);
    return counter;
  }

  private unchain(work: Uint8Array, counter: number, key: number): void {
    removeChainInPlace(work, counter, key);
  }

  /** Pre-keyed dispatch (state < 8): handshake, keep-alive, and session pings. */
  private async dispatchPreKeyed(
    decoded: Uint8Array,
    counter: number,
    remote: Deno.NetAddr,
    remoteAddress: string,
  ): Promise<void> {
    const frame = decodeFrame(decoded);
    const first = frame.messages[0];
    const tag = `${frame.compressed ? "lzss " : ""}ctr=${formatUdpCounter(counter)}`;

    // Unknown peer sending a handshake: provision the session from the
    // handshake body, then hand the message to its handler (which replies).
    if (
      !this.sessions.get(remoteAddress) && first !== undefined &&
      decoded.length === 44 && first.type === 0x1000
    ) {
      this.provisionSession(decoded, remote, remoteAddress);
    }

    if (!this.sessions.get(remoteAddress)) {
      // Still unknown (not a handshake) — log the frame and drop it.
      const note = first ? ` type=${formatUdpMessageType(first.type)}` : "";
      logUdpInPacket(this.logPrefix, `pre ${tag}${note}`, frameToLogBytes(frame));
      return;
    }

    logUdpInPacket(this.logPrefix, `pre ${tag}`, frameToLogBytes(frame));
    const isHandshake = decoded.length === 44 && first !== undefined && first.type === 0x1000;
    for (const message of frame.messages) {
      await this.dispatchMessage(message, remote);
    }
    // The joiner's counter is one stream across the pre-keyed and keyed
    // phases (capture: keep-alive 0x0001, then record 0x8002). Ack the
    // keep-alives so the cumulative ACK window stays contiguous — the
    // handshake itself is answered by the handshake reply instead.
    const session = this.sessions.get(remoteAddress);
    // Track the joiner's pre-keyed frames in the cumulative ACK window, but
    // do not transmit an ACK yet — pre-state-8 reliable-class traffic is
    // unverified territory; the first keyed ACK covers everything ≤ it.
    if (!isHandshake && session) this.trackInbound(session, counter);
  }

  /** Advance the cumulative window without transmitting (used for pre-keyed
   * frames and for frames whose only content is ACK entries — acks of acks
   * would ping-pong forever). */
  private trackInbound(session: PeerSession, counter: number): void {
    const seq = counter & UDP_COUNTER_MASK;
    if (seq > session.lastInSeq) session.lastInSeq = seq;
  }

  /** Session-keyed dispatch (state ≥ 8): every message to its handler. */
  private async dispatchKeyed(
    decoded: Uint8Array,
    counter: number,
    sessionKey: number,
    session: PeerSession,
    remote: Deno.NetAddr,
  ): Promise<void> {
    const frame = decodeFrame(decoded);
    session.lastSeenAt = Date.now();
    session.established = true;
    logUdpInPacket(
      this.logPrefix,
      `keyed ctr=${formatUdpCounter(counter)}${frame.compressed ? " lzss" : ""} K=${formatUdpU32(sessionKey)}`,
      frameToLogBytes(frame),
    );
    let ackEntries = 0;
    for (const message of frame.messages) {
      // An ACK entry (reliable-class type = 0x1000 | seq, len 1, body [0])
      // closes the matching outbound frame — mirror what FUN_0026e790 does on
      // the client: consume the pending slot, stop re-sending, never re-ack.
      // NOTE the length guard: the joiner's profile record shares the
      // reliable class (0x1001 = 0x1000|1) but is len 90 — it must reach its
      // handler, not the ACK consumer.
      const isAckEntry = (message.type & 0xf000) === 0x1000 &&
        message.length === 1 && message.body.length === 1 && message.body[0] === 0;
      if (isAckEntry) {
        const seq = message.type & 0x0fff;
        if (!session.seenAcks.has(message.type)) {
          session.seenAcks.add(message.type);
          console.debug(
            `[${this.logPrefix}] ACK for our seq ${seq} received (flags2=${message.flags})`,
          );
        }
        ackEntries++;
        continue;
      }
      await this.dispatchMessage(message, remote);
    }
    // Cumulative ACK (capture-proven: the joiner's single 0x1001 covers our
    // establish seq 0 AND the keep-alive mirror seq 1) — but never ack a
    // frame whose only content is ACK entries.
    if (ackEntries === frame.messages.length) {
      this.trackInbound(session, counter);
    } else {
      this.ackInbound(session, counter, remote);
    }
  }

  /**
   * Acknowledge the peer's frame — the mechanism decoded from MGO2.ELF:
   *
   * - Decoder enqueue path (0x266d48..0x266e04): every accepted keyed frame
   *   writes the received hdr counter into an outbound ACK envelope template
   *   (session+0x3c+4) alongside the frame's (offset, size) parse slots.
   * - Receiver pump FUN_0026e790: matches ACK entries (0x1000|seq, len 1,
   *   flags2 = attempt) against pending slots and frees them.
   * - The wire evidence: the joiner's control frames in the data phase are
   *   `type 0x1001 len 1 flags2 1..4 body [00]` — reliable-class 0x1000 | seq 1
   *   (our establish + keep-alive mirror both drew seq 1), flags2 escalating
   *   per re-send while unacknowledged.
   *
   * Every newly observed seq gets exactly one ACK (type 0x1000 | seq, len 1,
   * flags2 = 1, body [0]); duplicates (seq ≤ lastInSeq, or a re-sent reliable
   * record with the compression bit set) are not re-acked.
   */
  private ackInbound(session: PeerSession, counter: number, remote: Deno.NetAddr): void {
    const seq = counter & UDP_COUNTER_MASK;
    if (seq <= session.lastInSeq) return; // duplicate of an acked frame
    session.lastInSeq = seq;
    const attempt = UDP_ACK_FIRST_ATTEMPT;
    session.outCounter = (session.outCounter + 1) & 0xffff;
    const counterOut = session.outCounter;
    const plain = buildMessageFrame(counterOut, [
      messageOf(ackTypeOf(session.lastInSeq), UDP_ACK_BODY, attempt),
    ]);
    const key = session.established ? session.sessionKey : UDP_PRE_HANDSHAKE_KEY;
    const digestKey = session.established
      ? (session.sessionKey ^ UDP_TAIL_DIGEST_KEY) >>> 0
      : UDP_TAIL_DIGEST_KEY;
    const frame = encodeFrame(plain, counterOut, key, digestKey);
    logUdpOutPacket(
      this.logPrefix,
      `ack ctr=${formatUdpCounter(counterOut)} seq=${session.lastInSeq} attempt=${attempt}`,
      plain,
    );
    this.send(frame, remote);
  }

  /**
   * Provision a session from a decoded handshake datagram: peer id and
   * counter base from the handshake body, session key K = peer ^ host base.
   */
  private provisionSession(decoded: Uint8Array, remote: Deno.NetAddr, remoteAddress: string): void {
    // Handshake layout (§4): after hdr [0..2), message header [2..6), the body
    // starts at 6: peerId u32 LE [6..10), counterBase u32 LE [10..14).
    const peerId = u32LE(decoded, 6);
    const counterBase = u32LE(decoded, 10);
    this.sessions.put({
      remoteAddress,
      dialBack: { hostname: remote.hostname, port: remote.port },
      counterBase,
      outCounter: 0,
      sessionKey: (counterBase ^ this.hostCounterBase) >>> 0,
      peerId,
      hostPeerId: this.hostPeerId,
      established: false,
      lastSeenAt: Date.now(),
      lastInSeq: 0,
      seenAcks: new Set(),
    });
  }

  /** Route one message to its @PeerCommandHandler handler. */
  private async dispatchMessage(
    message: UdpMessage,
    remote: Deno.NetAddr,
  ): Promise<void> {
    const session = this.sessions.get(`${remote.hostname}:${remote.port}`);
    if (!session) return;
    const handler = peerCommandRegistry.resolve(message.type);
    if (!handler) {
      console.debug(`[${this.logPrefix}] no-handler type=${formatUdpMessageType(message.type)}`);
      return;
    }
    const context: PeerContext = {
      session,
      message,
      remote: { hostname: remote.hostname, port: remote.port },
      localPort: this.port,
      send: (type: number, body: Uint8Array) =>
        Promise.resolve(this.sendMessage(session, type, body, remote)),
    };
    await handler.handle(context);
  }

  /** Send a message to a peer through the session's shared counter. */
  private sendMessage(
    session: PeerSession,
    messageType: number,
    body: Uint8Array,
    remote: Deno.NetAddr,
  ): void {
    const counter = session.outCounter;
    session.outCounter = (counter + 1) & 0xffff;
    const plain = buildMessageFrame(counter, [messageOf(messageType, body)]);
    const key = session.established ? session.sessionKey : UDP_PRE_HANDSHAKE_KEY;
    const digestKey = session.established
      ? (session.sessionKey ^ UDP_TAIL_DIGEST_KEY) >>> 0
      : UDP_TAIL_DIGEST_KEY;
    const frame = encodeFrame(plain, counter, key, digestKey);
    logUdpOutPacket(
      this.logPrefix,
      `msg ctr=${formatUdpCounter(counter)} type=${formatUdpMessageType(messageType)}${session.established ? "" : " pre"}`,
      plain,
    );
    this.send(frame, remote);
  }

  private send(data: Uint8Array, remote: Deno.NetAddr): void {
    this.socket?.send(data, remote).catch((error) => {
      console.warn(`[${this.logPrefix}] send to ${remote.hostname}:${remote.port} failed: ${error}`);
    });
  }
}
