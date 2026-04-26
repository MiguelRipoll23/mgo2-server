import { commandRegistry } from "../services/command-registry-service.ts";
import type { HandlerConstructor } from "../services/command-registry-service.ts";

/** Registers a class as a Game server command handler. */
export function GameCommandHandler(commandId: number) {
  return function <T extends HandlerConstructor>(
    target: T,
    _context: ClassDecoratorContext,
  ) {
    commandRegistry.register("game", commandId, target);
  };
}
