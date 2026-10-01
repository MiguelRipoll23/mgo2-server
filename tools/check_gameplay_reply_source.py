#!/usr/bin/env python3
"""Check the host rule that pins the gameplay reply's source address and port.

The gameplay Service is `externalTrafficPolicy: Local`, so the pod sees the
console's real endpoint. The reply path is the part that does not follow: the
pod's socket is bound but never connected, so Linux picks an ephemeral source
port per send, flannel's `--random-fully` masquerade randomizes it further, and
the console discards every reply that does not arrive from the exact `ip:port`
it dialled. It re-handshakes forever and logs nothing on either side.

A host nftables rule pins that reply back to what the console dialled. This
checks the rule is present, correct, and loaded at boot -- the failure mode for
all three is a join that hangs with no error anywhere, so nothing else notices.

Usage:
    python tools/check_gameplay_reply_source.py [--ssh HOST]

Exit status is 0 when the rule is correct and persistent, 1 when it is not.
"""
from __future__ import annotations

import argparse
import pathlib
import re
import subprocess
import sys

RULE_FILE = pathlib.Path("/etc/nftables.d-mgo2-gameplay.nft")
UNIT_FILE = pathlib.Path("/etc/systemd/system/mgo2-gameplay-nft.service")
TABLE = "ip mgo2_gameplay"

# The rule the console's session filter needs. The destination port is the
# selector because it is the port the console dialled; the source port is
# ephemeral per send and conditioning on it is the bug this replaces.
EXPECTED_RULE = re.compile(
    r"ip saddr (?P<pod_cidr>[\d./]+)\s+"
    r"ip daddr (?P<client>[\d.]+)\s+"
    r"udp dport (?P<dport>\d+)\s+"
    r"snat to (?P<snatted>[\d.]+):(?P<sport>\d+)"
)

# Conditioning on the source port matches only the case where the pod happens to
# emit from the gameplay port, and lets every other reply through to the
# randomising masquerade. The console then rejects those, silently.
SOURCE_PORT_MATCH = re.compile(r"udp sport \d+")

REQUIRED_UNIT_DIRECTIVES = (
    "WantedBy=multi-user.target",
    "ExecStart=",
    "Before=k3s.service",
)


def run(ssh_host: str | None, *args: str) -> subprocess.CompletedProcess[str]:
    """Runs a command, over ssh when a host is given."""
    command = ["ssh", ssh_host, *args] if ssh_host else list(args)
    return subprocess.run(command, capture_output=True, text=True)


def check_rule_file(ssh_host: str | None) -> str | None:
    """Returns the single rule the file declares, or why it is wrong."""
    result = run(ssh_host, f"sudo cat {RULE_FILE.as_posix()}")
    if result.returncode != 0:
        return f"{RULE_FILE} is not readable: {result.stderr.strip()}"

    rules = [line.strip() for line in result.stdout.splitlines() if "snat to" in line]
    if len(rules) != 1:
        return (
            f"{RULE_FILE} declares {len(rules)} snat rules, expected exactly 1. "
            "More than one means an edit was reloaded without deleting the table "
            "first, because `nft -f` adds to an existing table."
        )

    rule = rules[0]
    if SOURCE_PORT_MATCH.search(rule):
        return (
            f"{RULE_FILE} matches on the source port, which is ephemeral per send: {rule}"
        )

    if not EXPECTED_RULE.search(rule):
        return f"{RULE_FILE} does not match the shape the console needs: {rule}"

    return None


def check_loaded(ssh_host: str | None) -> str | None:
    """Returns why the live table is wrong, or None when it is correct."""
    result = run(ssh_host, f"sudo nft list table {TABLE}")
    if result.returncode != 0:
        return f"table {TABLE} is not loaded: {result.stderr.strip()}"

    rules = [line.strip() for line in result.stdout.splitlines() if "snat to" in line]
    if len(rules) != 1:
        return f"table {TABLE} holds {len(rules)} snat rules, expected exactly 1"

    if SOURCE_PORT_MATCH.search(rules[0]):
        return f"the live rule matches on the ephemeral source port: {rules[0]}"

    if not EXPECTED_RULE.search(rules[0]):
        return f"the live rule is not the expected shape: {rules[0]}"

    return None


def check_persistence(ssh_host: str | None) -> str | None:
    """Returns why the rule is lost on reboot, or None when it survives."""
    result = run(ssh_host, f"sudo cat {UNIT_FILE.as_posix()}")
    if result.returncode != 0:
        return (
            f"{UNIT_FILE} is missing, so the rule is lost on reboot. Without it the "
            "next restart silently reintroduces a join that hangs with no error."
        )

    for directive in REQUIRED_UNIT_DIRECTIVES:
        if directive not in result.stdout:
            return f"{UNIT_FILE} is missing {directive!r}"

    enabled = run(ssh_host, "sudo systemctl is-enabled mgo2-gameplay-nft.service")
    if enabled.stdout.strip() != "enabled":
        return f"mgo2-gameplay-nft.service is {enabled.stdout.strip()}, expected enabled"

    return None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--ssh", help="ssh host to check, default is this machine")
    arguments = parser.parse_args()

    failures = [
        failure
        for failure in (
            check_rule_file(arguments.ssh),
            check_loaded(arguments.ssh),
            check_persistence(arguments.ssh),
        )
        if failure
    ]

    for failure in failures:
        print(f"FAIL: {failure}")
    if failures:
        return 1

    print("PASS: the gameplay reply source is pinned and survives a reboot")
    return 0


if __name__ == "__main__":
    sys.exit(main())
