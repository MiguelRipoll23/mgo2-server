import { injectable } from "@needle-di/core";

const LAUNCHER_SERVER = Deno.env.get("LAUNCHER_SERVER") ?? "http://mgo2pc.com";
const UPSTREAM_FETCH_TIMEOUT_MS = 3000;
const UPSTREAM_UNAVAILABLE_MESSAGE = "Request has timed out.";
const LOCAL_POLICY_FILE = "./static/policy.txt";

@injectable()
export class PolicyService {
  async getPolicy(): Promise<string> {
    let upstreamPolicy: string | null = null;
    try {
      const upstream = await fetch(
        `${LAUNCHER_SERVER}/jp/mgo2/policy/policy.txt`,
        {
          headers: {
            "user-agent": "Mozilla/5.0 (PLAYSTATION 3; 3.55)",
          },
          signal: AbortSignal.timeout(UPSTREAM_FETCH_TIMEOUT_MS),
        },
      );
      if (upstream.ok) {
        upstreamPolicy = await upstream.text();
      } else {
        console.log(
          "[http]",
          `Upstream policy request failed with status ${upstream.status}; using the fallback message.`,
        );
      }
    } catch (error) {
      console.log(
        "[http]",
        "Upstream policy request failed; using the fallback message.",
        error,
      );
    }

    const originalResponse = upstreamPolicy !== null &&
        upstreamPolicy.trim().length > 0
      ? upstreamPolicy.trimEnd()
      : UPSTREAM_UNAVAILABLE_MESSAGE;

    const localPolicy = await Deno.readTextFile(LOCAL_POLICY_FILE);
    return `${localPolicy.trimEnd()}\n${originalResponse}\n`;
  }
}
