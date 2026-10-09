# Survival host design

## Goal

Add a gameplay container named `mgo2-survival-host-1` that publishes one
dedicated game named `SURVIVAL_HOST` in the Free Battle lobby on a UDP port
unused by the current Compose stack. Existing gameplay containers keep their
`server-{port}` game names.

## Ownership and flow

The survival game-lobby process remains the owner of `TryAssignWaitingAsync`.
It owns the live sessions that receive match-found pushes, while the gameplay
host only publishes its dedicated room. A newly persisted Survival pairing
immediately asks the lobby's assignment service to claim an eligible host. The
existing assignment ticker remains as recovery for a pairing or host that
appears during a restart.

Room creation asks for assignment only when the created room is named
`SURVIVAL_HOST`. Other games do not trigger that work. The gameplay host's
heartbeat makes its room visible through the shared database; no new gRPC
traffic is needed for this path, because assignment and session pushes stay in
the process that already owns them.

## Host configuration

The gameplay server accepts an optional game-name setting. When absent, it
retains the existing `server-{port}` name; the new Compose service sets it to
`SURVIVAL_HOST`, points at `Free Battle`, and uses the same newly selected UDP
port for the listener and published host mapping. The host name determines the
Survival room subtype so it qualifies for Survival assignments.

## Scope and verification

Only the Compose deployment is extended; Compose development inherits the new
service's existing gameplay image build. No Kubernetes service or new gRPC
protocol is introduced. Verify Compose configuration, build the affected
projects, and run the existing protocol-codec suite; do not add behavioral
tests outside the repository's codec-only test policy.
