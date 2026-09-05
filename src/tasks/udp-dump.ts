// Minimal UDP echo of raw bytes: binds a UDP socket and prints every
// datagram it receives as a byte array (hex + decimal), with the sender's
// address. Useful for watching what a client actually puts on the wire —
// e.g. P2P probes — without any protocol handling.
//
// Port comes from UDP_PORT (default 5731); bind address from UDP_HOSTNAME
// (default 0.0.0.0).

const port = Number(Deno.env.get("UDP_PORT") ?? 5731);
const hostname = Deno.env.get("UDP_HOSTNAME") ?? "0.0.0.0";

const socket = Deno.listenDatagram({
  port,
  hostname,
  transport: "udp",
});

console.log(`[udp] listening on ${hostname}:${port}`);

while (true) {
  const [data, remote] = await socket.receive();
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

  console.log(`[udp] ${sender} (${bytes.length} bytes)`);
  console.log(`[udp]   hex: ${hex}`);
  console.log(`[udp]  dec : [${decimal}]`);
}
