import { container } from "../../../container.ts";
import type { IPeerCommandHandler } from "../interfaces/peer-command-handler-interface.ts";

export type PeerHandlerConstructor = new () => IPeerCommandHandler;

class PeerCommandRegistry {
  private readonly handlers = new Map<number, PeerHandlerConstructor>();

  register(messageType: number, constructor: PeerHandlerConstructor): void {
    this.handlers.set(messageType, constructor);
  }

  resolve(messageType: number): IPeerCommandHandler | null {
    const instance = this.handlers.get(messageType);
    return instance ? container.get(instance) : null;
  }

  has(messageType: number): boolean {
    return this.handlers.has(messageType);
  }
}

export const peerCommandRegistry = new PeerCommandRegistry();

/** Registers a class as a UDP peer message handler. */
export function PeerCommandHandler(messageType: number) {
  return function <T extends PeerHandlerConstructor>(
    target: T,
    _context: ClassDecoratorContext,
  ) {
    peerCommandRegistry.register(messageType, target);
  };
}
