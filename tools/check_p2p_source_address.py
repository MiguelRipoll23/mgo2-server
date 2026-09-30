#!/usr/bin/env python3
"""Measure the source address the gameplay pod observes for a UDP datagram.

The gameplay host files a peer session under the exact "address:port" a datagram
arrived from, so whether the pod sees the sender's real endpoint is the whole
question. A datagram the pod cannot decode is logged with the source it saw:

    Undecodable datagram from {RemoteAddress}

That makes the address measurable without speaking the protocol: send anything,
read the line back, compare it with the address the socket actually bound to.

Usage:
    python tools/check_p2p_source_address.py <target-host:port> [--local-port N]

Exit status is 0 when the pod observed the source port the socket bound to,
which is what `externalTrafficPolicy: Local` has to buy, and 1 when it did not.
"""
from __future__ import annotations

import argparse
import os
import pathlib
import re
import socket
import subprocess
import sys
import time

NAMESPACE = "mgo2"
POD_SELECTOR = "app=mgo2-gameplay-1"
UNREADABLE = re.compile(r"Undecodable datagram from (?P<address>[0-9.]+):(?P<port>\d+)")

KUBECONFIG = pathlib.Path("/tmp/k3s.yaml")


def pod_logs(since_seconds: int) -> str:
    """Returns the gameplay pod's log for the last `since_seconds` seconds."""
    environment = dict(os.environ)
    environment["KUBECONFIG"] = str(KUBECONFIG)
    result = subprocess.run(
        [
            "kubectl", "-n", NAMESPACE, "logs", "-l", POD_SELECTOR,
            f"--since={since_seconds}s", "--tail=500",
        ],
        capture_output=True,
        text=True,
        env=environment,
    )
    if result.returncode != 0:
        print(f"kubectl logs failed: {result.stderr.strip()}", file=sys.stderr)
        return ""
    return result.stdout


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("target", help="host:port to send the probe to")
    parser.add_argument("--local-port", type=int, default=0, help="source port to bind")
    parser.add_argument("--timeout", type=int, default=15, help="seconds to wait for the log line")
    arguments = parser.parse_args()

    host, _, port_text = arguments.target.rpartition(":")
    port = int(port_text)

    probe = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    probe.bind(("0.0.0.0", arguments.local_port))
    probe.settimeout(2.0)
    local_port = probe.getsockname()[1]
    print(f"probe bound to 0.0.0.0:{local_port}, sending to {host}:{port}", flush=True)

    sent_at = time.time()
    probe.sendto(b"mgo2-source-address-probe", (host, port))

    # The window starts at the send, so the only sender in it is this probe and
    # any "Undecodable" line in it is ours. Offsets into the log would be wrong:
    # the pod logs its account heartbeat on a timer, so the text shifts under
    # the slice between two reads.
    observed = None
    while time.time() - sent_at < arguments.timeout:
        elapsed = int(time.time() - sent_at) + 1
        for match in UNREADABLE.finditer(pod_logs(elapsed)):
            observed = (match.group("address"), int(match.group("port")))
            break
        if observed is not None:
            break
        time.sleep(1)

    probe.close()

    if observed is None:
        print("FAIL: the pod logged no datagram at all; the path is not reaching it")
        return 1

    seen_address, seen_port = observed
    print(f"pod observed source {seen_address}:{seen_port}")
    if seen_port == local_port:
        print(f"PASS: the pod sees this probe's source port, so the source survived the path")
        return 0
    print(
        f"FAIL: the socket sent from port {local_port} but the pod saw {seen_port}, "
        f"so the source was rewritten in transit"
    )
    return 1


if __name__ == "__main__":
    sys.exit(main())
