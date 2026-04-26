import { injectable } from "@needle-di/core";

@injectable()
export class PolicyService {
  async getPolicy(): Promise<string> {
    return await Deno.readTextFile("./static/policy.txt");
  }
}
