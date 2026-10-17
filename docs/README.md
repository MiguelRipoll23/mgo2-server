# dev

Four different kinds of thing live here, and the distinction matters — some of it the running stack
depends on, some is documentation, and some is a tool you run once and forget.

## Documentation

| file | what |
| --- | --- |
| `SETUP.md` | Everything outside this repository that must be true before a client can play. **Start here.** |
| `PROTOCOL.md` | The TCP command protocol, command by command and byte by byte. |
| `STUN.md` | The UDP port check. Separate transport, separate thread, shares nothing with the lobby servers. |
| `CRYPTO.md` | Every cipher, key and hash, where each is applied, and how to obtain them. |
| `OBSERVED.md` | What was observed and verified against a real client, including the hypotheses that turned out wrong. Read before re-testing anything. |
| `GAMEPLAY_SERVER.md` | The UDP gameplay host: the join, the briefing gate, the round and the beat — and how much of what a client receives is still a replay of a capture. |
| `protocol/STATE_VECTOR.md` | The operations stream's opcode language and its 89-byte state vector: which of the eighty-nine bytes are constants of the layout, which are the room's own, and which are still unread. |
| `analysis/udp-protocol-patterns.md` | The gameplay channel read as a design: replication and per-stream acks, the phase's identifier pair, the two stop-and-wait handshakes, ticks and the host's clock, and why a missing state is a stall rather than an error. |
| `BACKLOG.md` | Deliberately deferred work, with enough context to pick each item up cold. |

## Required by the running stack

**Not developer scratch — `compose.yaml` mounts these, and the server does not work without them.**

| path | role |
| --- | --- |
| `http_probe.py` | Serves the HTTP and HTTPS endpoints (`probe-http`, `probe-https`), terminates TLS and proxies to the web service. Holds the TLS-1.0 and `SECLEVEL=0` settings the console needs. |
| `turnserver.conf` | coturn config for the `probe-stun` service — the STUN responder that answers the port check. |
| `www/` | Static documents only — the document root the probes mount read-only and serve. Changes are live without a restart; large files (the ~1.9 GB patch archives) are streamed, not read whole. |
| `tls/` | The certificate chain and its private keys. Deliberately **not** under `www/`: that directory is served, and keys have no business in a document root. Mounted separately at `/tls`. |

## Tools

`tools/` is everything you run by hand — nothing in it is mounted by the running stack.
**`tools/README.md`** is its guide, and documents in full the two that do real work against a
console: `build_patch_round.py` (builds the in-game 1.0 → 1.36 patch) and `testhk_editor.py`
(points the game at your server). Two entries below are the ones you need before a first run.

| path | role |
| --- | --- |
| `tools/seed.sql` | Inserts the lobby rows, a test account and a news item. **Required** — an empty `lobby` table is a silent dead end. Run once. |
| `tools/extract_keys.py` | Pulls the crypto constants out of your own copy of `MGO2.elf`, with offsets. Run once. |

## Diagnostics

Not normally running. Each exists because it answered a question once and would answer it again.

| path | role |
| --- | --- |
| `stun_selftest.py` | Asserts the STUN reply format against a running responder — coturn, these days. A regression there produces no error, just a hung game. |
| `upnp_probe.py` | Answers the client's UPnP discovery. The game carries its own IGD client. |
| `dnsmasq.conf` | A logging DNS server. **DNS is not needed to play** — this is for discovering the hostnames a different disc or region asks for. |
| `retired/` | Superseded implementations, kept because they document a working approach rather than because anything runs them. `stun_probe.py` is the hand-rolled STUN responder coturn replaced on 2026-07-21; run it standalone with `python stun_probe.py 3478 <ip> <secondary-ip>`. |

## About `tls/`

`ca-cert.pem` and `ca-key.pem` are a certificate authority generated for this project. The console
validates the server certificate against its own store, so the CA has to be installed into the
emulator (see `SETUP.md`), and the leaf must cover every hostname in the swap list — `ext.cnf` has
the SANs.

**The CA private key is committed deliberately, and that is a trade-off worth understanding.** It
means everyone using this repository trusts the same CA, whose key is public — so anyone could sign
a certificate that your emulator would accept. For a LAN game server that is not a meaningful
threat, and shipping it means setup is a file copy rather than an openssl session. Generate your own
if you would rather not: sign a leaf with `ext.cnf`'s SANs and install your CA instead.

`cert-expired.pem` and `cert-leaf-expired.pem` are a chain whose only defect is its validity window.
Swapping them in via `MGO2SERVER_TLS_CERT` makes the client report `070B` instead of `090B`, which is how
the certificate branch was identified in the first place.

## About the replay console scripts

`analysis/replay-console/` holds verbatim copies of the JavaScript the SaveMGO replay console
loads. `rpdt.js` is the authority for the `0b` settings payload's layout — the appearance field
order, the skill alignment and the rotation triples — which no capture here fixes on its own, so
`protocol/UDP_SETTINGS_PAYLOAD.md` cites the committed copy rather than a URL that can change
under it. Nothing in this repository loads these files; `analysis/replay-console/README.md` has
the hashes, the fetch commands and the write-ups that depend on them.

## About the other MGO2 servers

echo, mgo2-server and the Nomad servers are cited by these documents for comparison, but are **not
vendored in this repository** — consult them on GitHub if a comparison is genuinely needed. Nothing
here depends on them, and `CLAUDE.md` explains why they are not specifications.
