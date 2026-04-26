import { commandRegistry } from "../services/command-registry-service.ts";
import type { HandlerConstructor } from "../services/command-registry-service.ts";

/** Registers a class as an Account server command handler. */
export function AccountCommandHandler(commandId: number) {
  return function <T extends HandlerConstructor>(
    target: T,
    _context: ClassDecoratorContext,
  ) {
    commandRegistry.register("account", commandId, target);
  };
}
