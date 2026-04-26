import { DnsServer } from "../infrastructure/dns/dns-server.ts";

await new DnsServer().start();
