#!/usr/bin/env python3
"""CANDIDATE — NOT ACTIVE. MVP6 evidence kit v1.1: one request to the lane supervisor (k00_supervisor.py).

Usage: k00_ctl.py SOCKET launch <svc>
       k00_ctl.py SOCKET run <label> [--cwd DIR] -- <argv...>
       k00_ctl.py SOCKET reveal <actor-label>        (prints ONE password to this terminal only; never redirect to a file)
       k00_ctl.py SOCKET pids | ping | shutdown
Prints the one-line JSON reply (a `reveal` reply prints only the password). Exit 0 only if the reply says ok.
"""
import json, socket, sys


def main():
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)
    sock, op, rest = sys.argv[1], sys.argv[2], sys.argv[3:]
    if op == "launch":
        req = {"op": "launch", "svc": rest[0]}
    elif op == "run":
        label, cwd = rest[0], None
        rest = rest[1:]
        if rest[:1] == ["--cwd"]:
            cwd, rest = rest[1], rest[2:]
        if rest[:1] == ["--"]:
            rest = rest[1:]
        req = {"op": "run", "label": label, "argv": rest, "cwd": cwd}
    elif op == "reveal":
        if not sys.stdout.isatty():
            raise SystemExit("reveal refuses to write to a pipe or file (v1.1 D4)")
        req = {"op": "reveal", "actor": rest[0]}
    elif op in ("pids", "ping", "shutdown"):
        req = {"op": op}
    else:
        raise SystemExit(f"unknown op {op}")
    s = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    s.connect(sock)
    s.sendall((json.dumps(req) + "\n").encode())
    reply = s.makefile().readline().strip()
    res = json.loads(reply)
    print(res.get("password") if op == "reveal" and res.get("ok") else reply)
    sys.exit(0 if res.get("ok") else 1)


main()
