import type { ICommandHandler } from "../interfaces/command-handler-interface.ts";
import type { ServerType } from "../types/server-type.ts";
import { container } from "../../../container.ts";

export type HandlerConstructor = new () => ICommandHandler;

class CommandRegistry {
  private readonly handlers = new Map<string, HandlerConstructor>();

  register(
    serverType: ServerType,
    commandId: number,
    constructor: HandlerConstructor,
  ): void {
    this.handlers.set(`${serverType}:${commandId}`, constructor);
  }

  resolve(serverType: ServerType, commandId: number): ICommandHandler | null {
    const instance = this.handlers.get(`${serverType}:${commandId}`);
    return instance ? container.get(instance) : null;
  }

  has(serverType: ServerType, commandId: number): boolean {
    return this.handlers.has(`${serverType}:${commandId}`);
  }
}

export const commandRegistry = new CommandRegistry();
