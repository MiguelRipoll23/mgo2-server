import { DNS_PORT, DEFAULT_LOCAL_RESOLVED_DOMAINS } from "./dns-constants.ts";

const alternativeDnsHost = Deno.env.get("ALTERNATIVE_DNS_SERVER") ?? "8.8.8.8";
const alternativeDnsPort = parseInt(Deno.env.get("ALTERNATIVE_DNS_PORT") ?? "53", 10);
const listeningIp = Deno.env.get("LISTENING_IP") ?? "0.0.0.0";
const localResolvedDomains = (
  Deno.env.get("LOCAL_RESOLVED_DOMAINS") ?? DEFAULT_LOCAL_RESOLVED_DOMAINS.join(",")
)
  .split(",")
  .map((d) => d.trim().toLowerCase())
  .filter((d) => d.length > 0);

const FORWARD_TIMEOUT_MS = 5000;

const QTYPE_A = 1;
const QTYPE_ANY = 255;

const textDecoder = new TextDecoder();

/**
 * Returns true only if the domain exactly matches an entry in LOCAL_RESOLVED_DOMAINS.
 * Subdomains are not implicitly matched; they must be explicitly listed.
 */
function isLocalDomain(domain: string): boolean {
  return localResolvedDomains.includes(domain);
}

/**
 * Parses a DNS domain name starting at `startOffset` in `data`.
 * Returns the decoded domain string and the offset immediately after the name.
 */
function parseDomainName(
  data: Uint8Array,
  startOffset: number,
): { domain: string; nextOffset: number } {
  const labels: string[] = [];
  let offset = startOffset;

  while (offset < data.length) {
    const length = data[offset];

    if (length === 0) {
      offset++;
      break;
    }

    // DNS name compression pointer (top two bits set)
    if ((length & 0xc0) === 0xc0) {
      const pointer = ((length & 0x3f) << 8) | data[offset + 1];
      const { domain } = parseDomainName(data, pointer);
      if (domain) labels.push(domain);
      offset += 2;
      break;
    }

    offset++;
    labels.push(textDecoder.decode(data.slice(offset, offset + length)));
    offset += length;
  }

  return { domain: labels.join("."), nextOffset: offset };
}

/**
 * Parses the first question in a DNS query packet.
 * Returns the queried domain (lowercased) and QTYPE, or null on parse failure.
 */
function parseQuery(
  data: Uint8Array,
): { domain: string; qtype: number } | null {
  if (data.length < 12) return null;

  const { domain, nextOffset } = parseDomainName(data, 12);
  if (nextOffset + 1 >= data.length) return null;

  const qtype = (data[nextOffset] << 8) | data[nextOffset + 1];
  return { domain: domain.toLowerCase(), qtype };
}

/**
 * Builds a DNS A record response for the given query packet, resolving to `ip`.
 */
function buildAResponse(query: Uint8Array, ip: string): Uint8Array {
  const ipParts = ip.split(".").map(Number);

  // Walk past the QNAME (ends with 0x00), then skip QTYPE + QCLASS (4 bytes)
  let offset = 12;
  while (offset < query.length && query[offset] !== 0) {
    offset += query[offset] + 1;
  }
  offset++; // 0x00 terminator
  offset += 4; // QTYPE + QCLASS

  const questionLength = offset - 12;
  const response = new Uint8Array(12 + questionLength + 16);

  // --- Header ---
  response[0] = query[0];
  response[1] = query[1]; // Transaction ID (echo)
  response[2] = 0x85; // QR=1 AA=1 RD=1
  response[3] = 0x80; // RA=1
  response[4] = 0;
  response[5] = 1; // QDCOUNT = 1
  response[6] = 0;
  response[7] = 1; // ANCOUNT = 1
  response[8] = 0;
  response[9] = 0; // NSCOUNT = 0
  response[10] = 0;
  response[11] = 0; // ARCOUNT = 0

  // --- Question section (verbatim copy) ---
  response.set(query.slice(12, 12 + questionLength), 12);

  // --- Answer RR ---
  let cursor = 12 + questionLength;
  response[cursor++] = 0xc0;
  response[cursor++] = 0x0c; // Name: pointer → offset 12
  response[cursor++] = 0x00;
  response[cursor++] = 0x01; // TYPE: A
  response[cursor++] = 0x00;
  response[cursor++] = 0x01; // CLASS: IN
  response[cursor++] = 0x00;
  response[cursor++] = 0x00;
  response[cursor++] = 0x01;
  response[cursor++] = 0x2c; // TTL: 300 s
  response[cursor++] = 0x00;
  response[cursor++] = 0x04; // RDLENGTH: 4
  for (const part of ipParts) response[cursor++] = part;

  return response;
}

/**
 * Forwards a raw DNS query to the alternative DNS server and returns the raw response.
 * Returns null if the upstream does not reply within FORWARD_TIMEOUT_MS.
 */
async function forwardToAltDns(query: Uint8Array): Promise<Uint8Array | null> {
  const socket = Deno.listenDatagram({
    transport: "udp",
    hostname: "0.0.0.0",
    port: 0,
  });

  try {
    await socket.send(query, {
      transport: "udp",
      hostname: alternativeDnsHost,
      port: alternativeDnsPort,
    });

    const responsePromise = socket.receive();
    const timeoutPromise = new Promise<null>((resolve) => {
      setTimeout(() => resolve(null), FORWARD_TIMEOUT_MS);
    });

    const result = await Promise.race([responsePromise, timeoutPromise]);

    if (result === null) {
      return null;
    }

    const [response] = result;
    return response;
  } catch (error) {
    console.error("[dns] upstream UDP socket error:", error);
    return null;
  } finally {
    socket.close();
  }
}

type ResponseSender = (response: Uint8Array) => Promise<void>;

export class DnsServer {
  async start(): Promise<void> {
    const socket = Deno.listenDatagram({
      transport: "udp",
      hostname: "0.0.0.0",
      port: DNS_PORT,
    });

    console.log(`[dns] Listening on port ${DNS_PORT}`);

    while (true) {
      try {
        const [data, remote] = await socket.receive();
        await this.handleQuery(data, async (response) => {
          await socket.send(response, remote);
        });
      } catch (error) {
        console.error("[dns] listener UDP socket error:", error);
      }
    }
  }

  private async handleQuery(
    data: Uint8Array,
    send: ResponseSender,
  ): Promise<void> {
    const parsed = parseQuery(data);
    if (!parsed) return;

    const { domain, qtype } = parsed;

    if (isLocalDomain(domain) && (qtype === QTYPE_A || qtype === QTYPE_ANY)) {
      console.log(`[dns] Overriding "${domain}" locally -> ${listeningIp}`);
      await send(buildAResponse(data, listeningIp));
    } else {
      console.log(`[dns] Resolving "${domain}" via alternative DNS server ${alternativeDnsHost}:${alternativeDnsPort}`);
      const response = await forwardToAltDns(data);
      if (response) await send(response);
    }
  }
}
