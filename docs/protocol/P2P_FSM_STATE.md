# Which FSM state the live join is in — read from the k3s gameplay pod's log

This answers one standing question: **where is the join actually sitting** when a
live client dials the deployed gameplay host. It is a reading of one run's pod
log, not a new trace of the binary; the state names, addresses and gates are
`P2P_CONNECT_FSM.md`'s, and the log only decides which of them is current.

Measured 2026-10-10 (UTC) from the `mgo2` namespace of the cluster the
`deploy/` tree reconciles, with the client's own end of the run reported from
the player's side (it raised `0B09`). The commands that produced the server
half are at the end, so the reading can be redone against any later run.

| | |
| --- | --- |
| pod | `mgo2-gameplay-1-564885fdf5-dp7vd` (ns `mgo2`), started `2026-10-09T23:26:01Z` |
| image | `ghcr.io/miguelripoll23/mgo2-server/mgo2-gameplay-server:d9ccb633…` (ArgoCD-pinned) |
| port / room | `5731`, lobby `Free Battle` (10), match 189, map 4 |
| joiner | `89.129.16.203:5730`, peer id `2`, counter base `0xb712cbf1`, character 2 `PhilDunphy23`, roster slot 0 |
| host | character 26 `server-5731` |

---

## 1. The answer

**The join spent its whole wait in the client's connect FSM (`0xaa1140`,
`ctx+0x70`) state 2** — *"await the module's data phase"* — with its transport
session (`session+4`) at **state 6, reply accepted**, the session key installed,
and never at **state 8**. States 3 and 4 are entered only from `8`, so neither
was reached, and neither was the connect FSM's terminal success value **6**. The
gate it stood in front of is the reliable-record-window test at `0x268f18` →
`0x26ab58` (`P2P_CONNECT_FSM.md` §7.1b).

**The run then ended in state 5**, the post-failure wait, which is entered only
after a failure path has destroyed the session and sent `0x4322`. The client
raised **`0B09`** (dialog 2825, "Unable to connect to host") — state 5's
channel-`0x2b` exit, not its `0B08` timeout exit. So the state field at the end
is 5, with the "over" marker `ctx+0x84` set, and `0B09` is the *verdict* that
wait reports, not the cause of the failure (§6).

In the server's own words: the join was **established and keyed, the roster was
served, the recorded host stream ran, every client record was answered — and the
transport still never reported the data phase, so the wait expired into the
polite `0B09` failure**. The two halves of that answer have since been corrected
against the captures — its body length (`PeerAcknowledgementUtils`) and its
timing (`PeerAnswerSchedulerService`, `P2P_CONNECT_FSM.md` §7.1e) — so the next
live join is the measurement that closes this run's reading.

The state fields, and what in the run says so:

| field | value | what in the run says so |
| --- | --- | --- |
| transport session (`session+4`) | **6** (reply accepted) — not 8 | every client frame after the opening verifies with the **session** key (`passed the session tail-digest check`), and the key is stored only on the accept path behind all three gates (§7.1b). Nothing in the run is data-phase traffic. |
| connect FSM (`ctx+0x70`) during the wait | **2** (await the module's data phase) | state 2 is the state that ticks the session and waits for `8`; no client frame after the profile is anything but the unanswered one-byte `0x9001` series, and the session stayed alive for the 92 s of it. |
| connect FSM at the end | **5** (post-failure wait), `ctx+0x84 = 1` | reached only after the countdown expired, the session was destroyed and `0x4322` was sent; the client's `0B09` is that state's channel-`0x2b` exit. The failure branch out of state 2 — not its `8` exit — is what took the run there. |

States 0 and 1 (the TCP `0x4320` request and the `0x4321` reply) are **not in
this log** — they belong to the game-lobby server's TCP side. That a UDP
datagram arrived at all is what says they completed.

## 2. Timeline — every line that is not a frame dump

```
23:26:14  host account/character ready; endpoint 89.129.16.203:5731 registered for character 26
23:26:15  created match 189 in lobby 10 on map 4
23:30:49  IN 44 B, passed the pre-key tail-digest check at counter 0
23:30:49  provisional session: peer=0x2, counter base=0xb712cbf1
23:30:49  dispatched pre-keyed 0x1000 (28 B) → AcceptHandshakeHandler
23:30:49  OUT pre-keyed 0x5000 at counter 0 (opening keep-alive)
23:30:49  OUT pre-keyed 0x1000 at counter 1 (handshake reply)
23:30:49  session with peer=0x2 established; opening exchange pre-keyed
23:30:49  IN 100 B, passed the session tail-digest check at counter 32769 (0x8001)
          → keyed 0x5000 keep-alive + keyed 0x1001 profile (140 B), ordinal 0
23:30:49  roster entry registered: character 2, slot 0; roster response, repeat, trailer 0x5001 sent
23:30:49  post-join burst scheduled (+2.52 s); recorded host stream started (19 sparse steps)
23:30:51  post-join burst + follow-up 0xd001
23:30:54  IN 17 B at counter 2 → 0x9001, 1 B body, ordinal 1  → answered 0xd001 (1 B, ordinal 1)
23:30:59  counter 3 → ordinal 2  → answered
   …        one 17 B 0x9001 every ~5 s, ordinals 3 … 17, each answered on the same tick
23:32:21  counter 19 → ordinal 18 → answered; last client datagram of the run
          ⤷ client side, same instant: state 2's countdown expires → destroy the
            session → TCP 0x4322 → state 5 → channel 0x2b completes → raise 0B09
23:33:21  steady host stream stopped: the peer is idle
23:33:29  peer disconnected: quiet for more than 60 seconds; session reaped, roster entry removed
```

The client sent **20 datagrams in total**: the pre-key handshake, one keyed frame
carrying the keep-alive and the profile, and 18 one-byte `0x9001` records whose
fourth bytes run `1` … `18` (`0x12`). Nothing else. The host's frames reached
counter `3474`.

## 3. What the run shows, and what it does not

**Shown [V]:**

* **The handshake was accepted by the client.** The client's very next frame
  (counter 1, keyed) verifies with the session key derived from the reply's
  counter base, which the accept path stores. So gates 1–3 passed and
  `flags |= 2 | 4` — transport state 6.
* **Every client record was answered, in the shape the captures give.** 18
  `0x9001` in, 18 answers out; each answer is `0xd001` with **one** body byte,
  the same fourth byte as the record it answers, sent on the same tick (the
  `PeerAcknowledgementUtils` correction, §7.1c).
* **No promotion to the data phase.** Nothing on the wire belongs to it: no tick
  records, no position/vitals records, and — the loud one — **not a single answer
  from the client**. The reference joiner answers the host's roster as
  `0xd001`/`0x5001` by byte 0/1/2 (§7.1c, `t+4.226`); this client answered
  nothing in 92 s while receiving the roster, the burst and the stream.
* **The end was the failure branch, not a disconnect.** Exactly one pre-key
  handshake arrived, so no re-dial happened, and the client's datagrams stop at
  23:32:21 — the instant state 2's countdown expires and the session is
  destroyed. No frame follows, and none could: the session that sent them is
  gone. The client then sat in state 5 until channel `0x2b` completed and
  `0B09` was raised, which is the failure path reached in full
  (`P2P_CONNECT_FSM.md` §3, §6).

**Not shown [U]:**

* Why the window never drained *now that the answer shape is right*. The
  remaining suspect is the client's receive half: a peer that accepts the host's
  records answers them, and this one answers none, so its `[0xc]` (§7.1b) has
  nothing to move it while its `[0xb]` climbs to 18. 18 is still inside the 24
  the window allows, so the limit was not what stopped it. The answers this run
  was served arrived in the millisecond the record did; the reference never
  answers faster than 13 ms (§7.1e). `PeerAnswerSchedulerService` now writes
  them 300 ms after the frame, which is the half of this the captures could
  settle.
* Whether the client answers the host's records at all once the window is
  moving. Nothing in this run did — no `0xd001` and no `0x5001` came back — and
  no promotion was reached, so the two cannot be told apart from here.
* Which countdown expired. States 2, 3 and 4 all end an expired wait the same
  way — session destroyed, `0x4322`, state 5 — so `0B09` alone does not separate
  them. State 2 is the reading because states 3 and 4 are entered only from the
  session's `8`, and nothing in the run looks like `8` was ever reported: no
  data-phase traffic, and a client that answered none of the host's records.
* The countdown arithmetic. The earlier run's single budget (`6000 + 4500`,
  §7.1b) measured ~39 s from the dial to `0B09`; this one measured **92 s**
  (23:30:49 → 23:32:21) with no re-dial in between and the same `0B09` exit.
  92 s is ~2.4× 39 s, so either the re-arm repeats under a condition §7.1b does
  not spell out, or the countdown's tick base is not constant. The wait that
  this state's budget measures is therefore not pinned by a single run.

## 4. Reproducing the reading

```bash
export KUBECONFIG=/tmp/k3s.yaml
POD=$(kubectl -n mgo2 get pods -l app=mgo2-gameplay-1 -o name | head -1)
kubectl -n mgo2 logs "$POD" > /tmp/gameplay.log

# the states: keyed frames and the accept, then the record cadence
grep -E "passed the (pre-key|session) tail-digest|established|invoking|answering" /tmp/gameplay.log
grep -E "passed the (pre-key|session) tail-digest check at counter" /tmp/gameplay.log | tail -1

# which states are NOT ours: no data-phase traffic and no client answers
grep -oE "invoking [A-Za-z]+ for 0x[0-9a-f]+" /tmp/gameplay.log | sort | uniq -c
```

`0B09` itself cannot appear in the pod log: it is raised in the client, after
the client has destroyed the session and sent `0x4322`. From the server, a
state-2 run that ends in `0B09` therefore looks exactly like this one, and the
client's `0B09` is what confirms that state 5 was entered. What the log *can*
rule out is the two other endings: the client answering the host's records (the
promotion path, which would mean state 8 and then FSM states 3 → 4 → 6) and a
second pre-key handshake (a re-dial). Neither appears in `/tmp/gameplay.log`.
