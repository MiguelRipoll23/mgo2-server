#!/usr/bin/env bash
#
# Reclaim the node's disk.
#
# Every commit that reaches a service publishes a multi-arch image to the node,
# and nothing that published one ever removes it. A node that starts clean is
# back to 83% full in a fortnight, and the thing that fills it is not a log
# file or a packet capture: it is the snapshot layers of every build the cluster
# has ever run. On a 58G card that is the difference between a deploy that fits
# and one that fails halfway.
#
# `.github/workflows/purge-images.yml` bounds the *registry*. This bounds the
# *node*, and neither substitutes for the other: a registry that still holds a
# version is what makes the prune here safe, because anything removed below can
# be pulled again from GHCR.
#
# What each step is allowed to remove is the whole care of this script:
#
#   `crictl rmi --prune` removes only images no container references. Every
#   image a running pod was started from is kept, so a rollout in flight is not
#   touched and a pod that restarts finds its image still on disk.
#
#   `docker builder prune --filter until=168h` keeps a week of build cache. An
#   unfiltered prune throws away cache a build published minutes ago and turns
#   the next one into a cold build, which on this node is minutes rather than
#   seconds. A week is also what GitHub expires its own caches on, so nothing is
#   kept here that has already stopped being useful to CI.
#
#   `docker image prune` is left unfiltered on purpose, so it removes only
#   *dangling* images — the untagged residue of a retag — and leaves every image
#   a stopped container still names. That matches what scripts/install-linux-macos.sh
#   already does, and an unattended nightly job has no business removing an
#   image some compose stack expects to start tomorrow.
#
# Rollback survives this. deploy/README.md describes a rollback as pointing
# `newTag` at an earlier commit, which works because every image the cluster has
# run is pinned in git and pullable from the registry. A pruned node makes that
# rollback re-pull its image first, which costs a pull and changes nothing else.

set -euo pipefail

readonly LOG_PREFIX="prune-images"

# The node's own filesystem, which is what a full card actually stops. /var is
# where the container stores live; naming it explicitly keeps the number this
# prints honest if anything else on the box changes.
readonly NODE_FS="/"

log() {
  echo "${LOG_PREFIX}: $*"
}

# Bytes free on the node filesystem, for the reclaimed figure at the end.
free_bytes() {
  df --output=avail -B1 "${NODE_FS}" | tail -n 1 | tr -d '[:space:]'
}

# k3s is what runs the cluster and docker is what runs the two services beside
# it, and a node is allowed to have only one of the two. Each step is skipped
# rather than failed when its runtime is absent, so this is safe to install on a
# node that has never run docker and on one that has never run k3s.
runtime_present() {
  command -v "$1" >/dev/null 2>&1
}

main() {
  if [ "$(id -u)" -ne 0 ]; then
    # Both stores are root-owned. A non-zero exit here is deliberate: a silent
    # no-op that reports "nothing to do" reads like a healthy node.
    echo "${LOG_PREFIX}: must run as root" >&2
    exit 1
  fi

  local before after
  before="$(free_bytes)"

  if runtime_present k3s && k3s crictl images >/dev/null 2>&1; then
    # No `-f` equivalent is wanted and none is offered: crictl prunes exactly
    # the unreferenced set and nothing else, so there is no broader mode to
    # accidentally reach for here.
    log "pruning k8s images no container references"
    # A deadline here is not a failure to paper over. The default timed out on
    # a card busy with a rollout, and a prune that gives up halfway has still
    # freed what it freed, so the timeout is raised rather than the failure
    # swallowed: a real error still exits non-zero.
    if ! k3s crictl --timeout 10m rmi --prune 2>&1 | tail -n 3; then
      echo "prune-images: k3s image prune did not complete" >&2
      exit 1
    fi
  else
    log "k3s not serving on this node, skipping the k8s image prune"
  fi

  if runtime_present docker && docker info >/dev/null 2>&1; then
    log "pruning build cache older than 168h"
    docker builder prune --force --filter until=168h

    log "pruning dangling images"
    docker image prune --force
  else
    log "docker not serving on this node, skipping the build cache prune"
  fi

  after="$(free_bytes)"
  log "reclaimed $(( (after - before) / 1024 / 1024 )) MiB, $(( after / 1024 / 1024 )) MiB free"
}

main "$@"
