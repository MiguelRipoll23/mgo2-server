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
import struct
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


def same_node() -> bool:
    """True when the gameplay pod runs on the machine this probe is sent from."""
    result = subprocess.run(
        [
            "kubectl", "-n", NAMESPACE, "get", "pods", "-l", POD_SELECTOR,
            "-o", "jsonpath={.items[0].status.hostIP}",
        ],
        capture_output=True,
        text=True,
        env={**os.environ, "KUBECONFIG": str(KUBECONFIG)},
    )
    if result.returncode != 0 or not result.stdout.strip():
        return False
    host_ip = result.stdout.strip()
    addresses = subprocess.run(
        ["ip", "-4", "-o", "addr", "show"], capture_output=True, text=True
    )
    return any(f" {host_ip}/" in line for line in addresses.stdout.splitlines())


def checksum(data: bytes) -> int:
    """The standard internet checksum over a header's 16-bit words."""
    if len(data) % 2:
        data += b"\x00"
    total = sum(struct.unpack(f"!{len(data) // 2}H", data))
    while total >> 16:
        total = (total & 0xFFFF) + (total >> 16)
    return (~total) & 0xFFFF


def send_spoofed(source: str, destination: str, port: int, payload: bytes) -> None:
    """Sends a UDP datagram with a source address this host does not own.

    The reply is not wanted and never read. The point is only that the packet
    reaches the gameplay pod with a source the node does not own, which is what
    a remote console looks like to kube-proxy.
    """
    source_binary = socket.inet_aton(source)
    destination_binary = socket.inet_aton(destination)
    source_port = 45099

    udp_length = 8 + len(payload)
    udp_header = struct.pack("!HHHH", source_port, port, udp_length, 0)
    udp = udp_header + payload
    # A zero UDP checksum is legal over IPv4 and saves carrying the pseudo-header.
    total_length = 20 + udp_length
    header = struct.pack(
        "!BBHHHBBH4s4s",
        0x45, 0, total_length, 0, 0, 64, socket.IPPROTO_UDP, 0,
        source_binary, destination_binary,
    )
    header = header[:10] + struct.pack("!H", checksum(header)) + header[12:]

    raw = socket.socket(socket.AF_INET, socket.SOCK_RAW, socket.IPPROTO_RAW)
    raw.setsockopt(socket.IPPROTO_IP, socket.IP_HDRINCL, 1)
    raw.sendto(header + udp, (destination, 0))
    raw.close()
    print(f"spoofed source {source}:{source_port} -> {destination}:{port}", flush=True)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("target", help="host:port to send the probe to")
    parser.add_argument("--local-port", type=int, default=0, help="source port to bind")
    parser.add_argument("--timeout", type=int, default=15, help="seconds to wait for the log line")
    parser.add_argument(
        "--spoof-source",
        help="send from this address the host does not own, which is how a remote "
             "console looks to kube-proxy; needs root",
    )
    arguments = parser.parse_args()

    host, _, port_text = arguments.target.rpartition(":")
    port = int(port_text)

    if arguments.spoof_source:
        send_spoofed(
            arguments.spoof_source, host, port, b"mgo2-source-address-probe"
        )
        sent_at = time.time()
        observed = None
        while time.time() - sent_at < arguments.timeout:
            elapsed = int(time.time() - sent_at) + 1
            matches = list(UNREADABLE.finditer(pod_logs(elapsed)))
            if matches:
                match = matches[-1]
                observed = (match.group("address"), int(match.group("port")))
                break
            time.sleep(1)
        if observed is None:
            print("FAIL: the pod logged no datagram at all; the path is not reaching it")
            return 1
        seen_address, seen_port = observed
        print(f"pod observed source {seen_address}:{seen_port}")
        if seen_address == arguments.spoof_source and seen_port == 45099:
            print("PASS: a source the node does not own survived to the pod unrewritten")
            return 0
        print(
            f"FAIL: sent from {arguments.spoof_source}:45099 but the pod saw "
            f"{seen_address}:{seen_port}, so the source was rewritten in transit"
        )
        return 1

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
    # the slice between two reads. Take the LAST match, not the first: a line
    # from an earlier probe can still fall inside the window.
    observed = None
    while time.time() - sent_at < arguments.timeout:
        elapsed = int(time.time() - sent_at) + 1
        matches = list(UNREADABLE.finditer(pod_logs(elapsed)))
        if matches:
            match = matches[-1]
            observed = (match.group("address"), int(match.group("port")))
            break
        time.sleep(1)

    probe.close()

    if observed is None:
        print("FAIL: the pod logged no datagram at all; the path is not reaching it")
        return 1

    seen_address, seen_port = observed
    print(f"pod observed source {seen_address}:{seen_port}")

    # A probe sent from the node hosting the pod is masqueraded whatever the
    # Service says, because kube-proxy marks LOCAL-sourced traffic on the way to
    # a Local service:
    #
    #   masquerade LOCAL traffic ... -m addrtype --src-type LOCAL -j KUBE-MARK-MASQ
    #
    # So a rewritten source here says nothing about what a remote console sees,
    # and reporting PASS or FAIL would be a guess. Run this from another machine.
    if same_node():
        print(
            "INCONCLUSIVE: this probe left from the node hosting the gameplay pod, so it "
            "matched the --src-type LOCAL masquerade rule. That is expected under "
            "externalTrafficPolicy: Local and says nothing about a remote console. "
            "Run this from the console's network to get a verdict."
        )
        return 2

    if seen_port == local_port:
        print("PASS: the pod sees this probe's source port, so the source survived the path")
        return 0
    print(
        f"FAIL: the socket sent from port {local_port} but the pod saw {seen_port}, "
        f"so the source was rewritten in transit"
    )
    return 1


if __name__ == "__main__":
    sys.exit(main())
