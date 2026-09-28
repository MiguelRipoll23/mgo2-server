# Host links: making a tailnet game joinable from outside it

Players run their own tailnet and the server is invited onto it by machine sharing, so the address
the server sees for a host is the host's **tailnet** address. A game hosted there is reachable by
everyone already on that tailnet and by nobody else. This is the one path that hands such a host a
way to publish the address the game is reachable at from the public internet.

**It is not a login link and grants nothing but one address write.** A code resolves to a character
and is used to overwrite that character's `character_connections.public_ip`; it cannot read an
account, change a password or reach any other route.

---

## What the server does

When a host creates a room (`0x4300`, `CreateGameHandler`), the lobby raises one chat line **as the
host's own character** — the speaker field is the host, so the room sees it as a message from them,
not as a staff notice — and only when all three of these hold:

| condition | where it comes from |
| --- | --- |
| an external page is configured | `EXTERNAL_SERVER_BASE_URL`, unset by default |
| the code can be resolved | `JWT_SECRET`, which the external page is expected to hold too |
| the game is tailnet-only | `ADVERTISED_ADDRESS` inside `100.64.0.0/10` |

The line is:

```
Go to https://placeholder.vercel.app/1234 to make your game available to join from other players outside your network
```

`EXTERNAL_SERVER_BASE_URL` supplies `https://placeholder.vercel.app` (a trailing slash is trimmed)
and the four digits are the code below. The line shares the client's 127-byte chat limit, so a base
address long enough to overrun it is logged and skipped rather than truncated into a link that
points nowhere.

Every other advertised address — a LAN address, a public one, an unset one — sends nothing, because
the game is already reachable without the page.

## Configuration

Both keys live in the deployment's `appsettings.json`, which every workload mounts and the
`mgo2-appsettings` ConfigMap carries. Changing one rolls the whole stack, the lobbies included —
see `deploy/README.md`, "Changing the ConfigMap".

```json
{
  "EXTERNAL_SERVER_BASE_URL": "https://placeholder.vercel.app",
  "JWT_SECRET": "..."
}
```

> **`JWT_SECRET` is the bearer-token signing key as well.** Handing it to the external page so it can
> reproduce codes also hands it the ability to forge API tokens. That is the trade the code
> derivation makes; a deployment that would rather not make it should key the links on a separate
> secret instead.

## The code

The exact function, so the page can reproduce it byte for byte:

1. `secret` is `JWT_SECRET` as UTF-8 bytes; `name` is the character's `characters.name` as UTF-8
   bytes. The name is used **exactly**, case included.
2. `digest = HMAC-SHA256(key = secret, message = name)`.
3. `value = ` the first four bytes of `digest`, read **big-endian**, as an unsigned 32-bit integer.
4. `code = ` `value mod 10000`, rendered as decimal with leading zeros to exactly four digits.

Pinned vectors, so an implementation can be checked before it is trusted:

| secret | name | code |
| --- | --- | --- |
| `secret` | `Snake` | `1068` |
| `secret` | `Raiden` | `5274` |
| `a-long-base64-secret` | `OCELOT` | `0150` |
| `secret` | `snake` | `6065` |

`HostLinkUtils` is the server's implementation and `HostLinkUtilsTests` carries the same vectors.

## Resolving a code to a character

The code is derived, never stored, so the page resolves it by recomputing. **Resolve only against
characters hosting a game right now** — the hosts of the `games` rows that are still published
(`updated_at` within the last hour, the server's own `GameStaleSeconds`), joined to
`characters.name`. That is the whole reason four digits are enough: a handful of concurrent hosts is
a handful of codes, where the whole character table would collide constantly and a code would stop
naming one character.

```
select g.host_id, c.name
from games g join characters c on c.id = g.host_id
where g.updated_at > now() - interval '1 hour'
```

Recompute the code for each name and keep the row it matches. **If two hosts match, refuse rather
than guess** — the write is a public address, and picking one of two is worse than asking the host
to try again once the other room is gone.

## The write

The public address is the only thing the link changes. The port the client listens on is already
correct in `character_connections` (`public_port`), because the client reports it itself; only the
address the server saw is wrong, since it saw the tailnet one.

```sql
update character_connections
set public_ip = :address, updated_at = now()
where character_id = :character_id
```

The page is the only thing that can know the address: it is the address the visiting browser
reached the page from, so the page should record what it observes rather than trust anything the
visitor sends. A character with no `character_connections` row has not registered a peer endpoint
yet and has nothing to update; that is answered the same way as a code that resolves to nothing.
