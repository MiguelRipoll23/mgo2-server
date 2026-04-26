import * as HyperDX from "@hyperdx/node-opentelemetry";

export function init(): void {
  const apiKey = Deno.env.get("HDX_API_KEY");

  if (!apiKey) {
    return;
  }

  HyperDX.init({
    apiKey,
    disableStartupLogs: true,
    service: "mgo2-server",
  });
}
