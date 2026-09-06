const ports = (Deno.env.get("UDP_PORTS") ?? "3578,11181")
  .split(",")
  .map((part) => Number(part.trim()))
  .filter((port) => Number.isInteger(port) && port > 0);
const hostname = Deno.env.get("UDP_HOSTNAME") ?? "0.0.0.0";

const sockets = ports.map((port) => ({
  port,
  socket: Deno.listenDatagram({ port, hostname, transport: "udp" }),
}));

for (const { port } of sockets) {
  console.log(`[udp] listening on ${hostname}:${port}`);
}

while (sockets.length > 0) {
  const result = await Promise.race(
    sockets.map(async ({ port, socket }) => ({
      port,
      data: (await socket.receive()) as [Uint8Array, Deno.NetAddr],
    })),
  );

  const [data, remote] = result.data;
  const bytes = Array.from(new Uint8Array(data));

  const hex = bytes
    .map((byte) => byte.toString(16).padStart(2, "0"))
    .join(" ");
  const decimal = bytes.join(", ");

  // The address union includes UnixAddr; a UDP transport socket only ever
  // yields the network variant.
  const sender = remote.transport === "udp"
    ? `${remote.hostname}:${remote.port}`
    : "unix";

  console.log(`[udp:${result.port}] ${sender} (${bytes.length} bytes)`);
  console.log(`[udp:${result.port}]   hex: ${hex}`);
  console.log(`[udp:${result.port}]  dec : [${decimal}]`);
}
