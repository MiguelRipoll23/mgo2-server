# Survival host design

## Goal

Add a gameplay container named `mgo2-survival-host-1` that publishes its
dedicated game in the **Survival Hosts** lobby on UDP port `60190`, under the
display name `Survival Host 1`. The port was selected at random after checking
the tracked deployment configuration and local UDP bindings; both the container
listener and Compose host mapping use `60190`. The regular gameplay containers
keep their `server-{port}` game names.

## Host identity

A dedicated host is identified by the flag its host character carries and by
the mode of the room it publishes, never by a reserved name. The gameplay
server's startup path marks its character's host settings as dedicated, and
`MatchService` gives the room the mode of the lobby it publishes in. The
Survival Hosts lobby runs the Survival selector, so the room it hosts is a
Survival host. A deployment that wants a host for another mode publishes it in
that mode's lobby or names the mode in the room's own settings.

The `*_HOST` name convention is gone. A room named `SURVIVAL_HOST` or
`TOURNAMENT_HOST` means no more than any other name: the room list no longer
hides such names, and the join gate refuses a stranger from a dedicated room by
its flag and mode rather than by its name.

## Ownership and flow

The survival game-lobby process remains the owner of `TryAssignWaitingAsync`.
It owns the live sessions that receive match-found pushes, while the gameplay
host only publishes its dedicated room. `EventMatchmakingService` invokes the
lobby's assignment service after it persists a new Survival pairing. The
existing assignment ticker remains as recovery for a host that appears later or
a pairing that was not observed during a restart. The scripted opponent path
must not issue a second assignment attempt for the same pairing.

`CreateGameHandler` offers a newly created room to the waiting matches without
checking its name, because a name no longer says whether a room may host. The
gameplay server's own room creation does not call assignment from its process.
Its heartbeat makes the host visible through the shared database, and the
pairing trigger or existing ticker finds it.

A match that is cancelled hands its room back: the cancellation releases the
lease the assignment holds and settles any orphaned lease whose match is no
longer live, so a room is never left leased to a match that is over.

## Scope and verification

Touchpoints are `ServerOptions` and its environment binding,
`GameplayServer.MatchService`, `EventMatchmakingService`,
`EventHostEligibilityUtils`, `RoomCreationHandlers`, `RoomLifecycleHandlers`,
`RoomBrowserHandlers`, `compose.yaml`, and `compose.dev.yaml`. No Kubernetes
service or new gRPC protocol is introduced. Verify Compose configuration, build
affected projects, and run the existing protocol-codec suite; do not add
behavioral tests outside the repository's codec-only test policy.
