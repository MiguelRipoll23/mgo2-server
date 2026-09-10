import type { PeerSession } from "../types/udp-type.ts";

/** Sessions idle longer than this are dropped (join retries far exceed it). */
const SESSION_IDLE_TIMEOUT_MS = 60_000;
const SWEEP_INTERVAL_MS = 10_000;

/** Owns and tracks negotiated p2p sessions, keyed by remote endpoint. */
export class PeerSessionService {
  private readonly sessions = new Map<string, PeerSession>();
  private sweepTimer: number | null = null;

  get(remoteAddress: string): PeerSession | null {
    return this.sessions.get(remoteAddress) ?? null;
  }

  put(session: PeerSession): void {
    this.sessions.set(session.remoteAddress, session);
  }

  delete(remoteAddress: string): void {
    this.sessions.delete(remoteAddress);
  }

  /** Re-key a session when a peer's source endpoint changes (rare). */
  rename(oldKey: string, session: PeerSession): void {
    if (oldKey !== session.remoteAddress) {
      this.sessions.delete(oldKey);
    }
    this.sessions.set(session.remoteAddress, session);
  }

  start(): void {
    if (this.sweepTimer !== null) return;
    this.sweepTimer = setInterval(() => this.sweep(), SWEEP_INTERVAL_MS) as unknown as number;
  }

  stop(): void {
    if (this.sweepTimer !== null) {
      clearInterval(this.sweepTimer);
      this.sweepTimer = null;
    }
    this.sessions.clear();
  }

  private sweep(): void {
    const now = Date.now();
    for (const [key, session] of this.sessions) {
      if (now - session.lastSeenAt > SESSION_IDLE_TIMEOUT_MS) {
        this.sessions.delete(key);
      }
    }
  }
}
