# Survival host design

## Goal

Add a gameplay container named `mgo2-survival-host-1` that publishes one
dedicated game named `SURVIVAL_HOST` in the Free Battle lobby on UDP port
`60190`. This port was selected at random after checking the tracked deployment
configuration and local UDP bindings; both the container listener and Compose
host mapping use `60190`. Existing gameplay containers keep their
`server-{port}` game names.

## Ownership and flow

The survival game-lobby process remains the owner of `TryAssignWaitingAsync`.
It owns the live sessions that receive match-found pushes, while the gameplay
host only publishes its dedicated room. `EventMatchmakingService` invokes the
lobby's assignment service after it persists a new Survival pairing. The
existing assignment ticker remains as recovery for a host that appears later or
a pairing that was not observed during a restart. The scripted opponent path
must not issue a second assignment attempt for the same pairing.

`CreateGameHandler` asks for assignment only when the created player room is
named `SURVIVAL_HOST`; other games do not trigger that work. The gameplay
server's own room creation does not call assignment from its process. Its
heartbeat makes the host visible through the shared database, and the pairing
trigger or existing ticker finds it. No new gRPC traffic is needed: assignment
and session pushes stay in the process that already owns them.

## Host configuration

The gameplay server accepts an optional `GAMEPLAY_SERVER_GAME_NAME` setting.
When absent, it retains the existing `server-{port}` name. The new Compose
service sets it to `SURVIVAL_HOST`, points at `Free Battle`, and uses UDP port
`60190` for both listener and published mapping. The name sets room subtype to
the Survival selector; the existing startup path marks the gameplay character's
host settings as dedicated, so the room satisfies the name, subtype, and
dedicated-host eligibility rules.

## Scope and verification

Touchpoints are `ServerOptions` and its environment binding,
`GameplayServer.MatchService`, `EventMatchmakingService`,
`SurvivalTestOpponentService`, `CreateGameHandler`, `compose.yaml`, and
`compose.dev.yaml`. Compose development inherits the gameplay image build for
the new service. No Kubernetes service or new gRPC protocol is introduced.
Verify Compose configuration, build affected projects, and run the existing
protocol-codec suite; do not add behavioral tests outside the repository's
codec-only test policy.
