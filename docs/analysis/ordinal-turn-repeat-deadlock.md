# The repeat at the turn, and why it killed the stream

What this answers: the change that handed `0xff` out again when a stream's counter
reached zero did not cross the turn — it stopped the stream dead — and the reader in
`MGO2.ELF` says exactly why. This note is the correction to the earlier reading that
made a repeat the price of crossing `0xff → 0x00`.

## The reader's rule

`read_queued_p2p_message` (`0x26b5c8`) decides whether a record is taken in three
lines:

```c
bVar7 = *(byte *)(iVar5 + 9);                        // the record's flags
if ((bVar7 & 5) == 5) {                              // 0x01 reliable | 0x04 carries a byte
    iVar6 = resolve_p2p_record_handle(puVar4[2]);
    if (*(char *)(iVar5 + 10) != *(char *)(iVar6 + 0xe)) goto LAB_0026b6e0;   // refuse
    *(char *)(iVar6 + 0xe) = *(char *)(iVar6 + 0xe) + '\x01';                 // accept
}
```

* `+0xa` of the record is its fourth byte; `+0xe` of the stream's handle is the byte
  the client expects next.
* The comparison is **equality**, and the expectation advances **by exactly one** on a
  match. There is no tolerance and no window: a record is taken only if its byte is the
  one the handle is holding.
* `_opd_FUN_0026b178` (the handle's constructor) writes `+9 = 0x80` and `+0xe = 0`, so
  a stream starts expecting zero.
* The refusal path lands on a label that clears the handle's own bookkeeping
  (`puVar4[2] = 0xffffffff`) — a refused record is not merely dropped, the stream's
  state is reset.

## The change as it was written

```csharp
if (value == 0x00 && lastHanded.TryGetValue(stream, out var handed) && handed == 0xFF)
{
    lastHanded[stream] = 0xFF;
    return 0xFF;
}
```

It returns `0xff` and leaves the counter at zero — the intent. But it also leaves
`lastHanded` at `0xff`, and the guard is `value == 0x00 && lastHanded == 0xFF`, which is
still true the next time. **The branch re-enters on every take.** The counter never
advances, so the stream hands out `0xff` for every record from then on, forever.

## What the client does with that, step by step

1. The stream reaches `0xff`; the client's expectation is `-1` (the byte is read as
   `char`, signed on this PowerPC ELF) and it takes the record, advancing to `0`.
2. The next record carries `0xff` again. The comparison is `0 != -1`, so it is
   **refused**, and the refusal resets the stream's handle.
3. Every record after it also carries `0xff`, so every one is refused the same way. The
   `0x00` the counter is holding is never written.
4. The stream therefore makes no progress at all, and the client force-closes the
   session after its own timeout.

That is not a turn that failed to cross. It is a stream that stopped moving while
holding a value it never sent, and the reader's equality test turns each later `0xff`
into another refusal.

## What the log showed

The run of 14:00, roster stream `0x001`: the client's acknowledgements run
`… 244 245 246 247 248 250 251 252 253 254 255` and then stop at 14:00:20. The
scoreboard and ticket records written behind it carried `0x00`, `0x01`, `0x02` — none
answered — while the operations stream on its own counter kept being answered either
side of the break. A single accepted `0xff`, then silence, is the shape this defect
predicts.

## The correct shape, if a repeat is ever wanted again

The guard has to be **one-shot**. The earlier form cleared it by setting
`lastHanded[stream] = 0x00` before returning `0xff`, so the next take saw
`value == 0x00 && lastHanded == 0x00`, did not re-enter, and handed out `0x00` — which
the client takes, because its expectation is now `0`.

What must never be done is leaving the guard in the state that fired it. A condition
that both returns early and leaves its own precondition standing is a loop, and on a
byte-wide counter a loop is a stream that never moves.

## What is still open

The turn itself is not established as harmful: the reference captures cross
`0xff → 0x00` directly more often than they repeat. What this note does establish is
that the repeat as written could only ever produce one accepted record and then a dead
stream, so the revert was right and the earlier reading that made a repeat mandatory is
superseded.
