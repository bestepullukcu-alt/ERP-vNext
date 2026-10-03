#!/usr/bin/env python3
"""CANDIDATE — NOT ACTIVE. MVP6 evidence kit v1.2: requests to the lane supervisor (k00_supervisor.py).

Usage: k00_ctl.py SOCKET launch <svc>
       k00_ctl.py SOCKET run k07 | scan | seal
       k00_ctl.py SOCKET run harness <file relative to the harness dir> [-- <args...>]
       k00_ctl.py SOCKET reveal <actor-label>      (interactive: the supervisor writes a one-time code to THIS terminal,
                                                    you type it here, then the supervisor writes the password to this
                                                    terminal; nothing comes back over the socket)
       k00_ctl.py SOCKET pids | ping | shutdown | abort
Prints the one-line JSON reply. Exit 0 only if the reply says ok.
v1.2 (F2): `run` takes a task name, not a command line; the supervisor builds the command and decides which values the
task receives. The terminal checks for `reveal` are enforced by the supervisor (peer credentials + tty), not here.
"""
import json, socket, sys


def ask(sock, req, timeout=None):
    s = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    s.settimeout(timeout)
    s.connect(sock)
    s.sendall((json.dumps(req) + "\n").encode())
    reply = s.makefile().readline().strip()
    s.close()
    return reply, json.loads(reply or "{}")


def main():
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)
    sock, op, rest = sys.argv[1], sys.argv[2], sys.argv[3:]
    if op == "launch" and len(rest) == 1:
        req = {"op": "launch", "svc": rest[0]}
    elif op == "run" and rest[:1] in (["k07"], ["scan"], ["seal"]) and len(rest) == 1:
        req = {"op": "run", "task": rest[0]}
    elif op == "run" and rest[:1] == ["harness"] and len(rest) >= 2:
        args = rest[2:]
        if args[:1] == ["--"]:
            args = args[1:]
        req = {"op": "run", "task": "harness", "file": rest[1], "args": args}
    elif op == "reveal" and len(rest) == 1:
        reply, res = ask(sock, {"op": "reveal-arm", "actor": rest[0]}, timeout=10)
        if not res.get("ok"):
            print(reply); sys.exit(1)
        with open("/dev/tty") as tty:
            sys.stderr.write("type the 6-digit code the supervisor just wrote to this terminal: "); sys.stderr.flush()
            code = tty.readline().strip()
        req = {"op": "reveal", "actor": rest[0], "code": code}
    elif op in ("pids", "ping", "shutdown", "abort") and not rest:
        req = {"op": op}
    else:
        raise SystemExit(__doc__)
    reply, res = ask(sock, req, timeout=10 if op in ("ping", "pids", "reveal") else None)
    print(reply)
    sys.exit(0 if res.get("ok") else 1)


main()
