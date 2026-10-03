#!/usr/bin/env python3
"""CANDIDATE — NOT ACTIVE. MVP6 evidence kit phase K03: lane port allocation and conflict check.

Slot S (1..9) gives one deterministic block, so parallel lanes (pilot limit: 2 product + 1 environment lane)
never share a port and a reviewer can recompute the plan:
  gateway 5000+100S, web +1, auth +56, platform +57, mdm +59, supplychain +61, sink +99 (must stay CLOSED)
  mongo   30994+1000S
Refused outright: 27017 (operational Mongo), 5000/5001 and 5011-5064 (operational service band, ports.md),
7000-7999 (dev tools), >=49152 (OS ephemeral range). Slot 0 would collide with the operational stack.

Usage: k03_ports.py --slot S --services "auth platform ..." --work W --evidence E [--check-only]
Writes W/ports.json and E/raw/PORTS.tsv + E/raw/ports-lsof-before.txt. Exit 1 on any conflict.
"""
import argparse, json, pathlib, socket, subprocess, sys

OFFSETS = {"gateway": 0, "web": 1, "auth": 56, "platform": 57, "mdm": 59, "supplychain": 61, "sink": 99}


def forbidden(port):
    if port == 27017:
        return "operational Mongo 27017"
    if port in (5000, 5001) or 5011 <= port <= 5064:
        return "operational service band (ports.md)"
    if 7000 <= port <= 7999:
        return "dev-tools band"
    if port >= 49152:
        return "OS ephemeral range"
    return None


def listening(port):
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM); s.settimeout(0.5)
    try:
        return s.connect_ex(("127.0.0.1", port)) == 0
    finally:
        s.close()


def bindable(port):
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    try:
        s.bind(("127.0.0.1", port)); return True
    except OSError:
        return False
    finally:
        s.close()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--slot", type=int, required=True)
    ap.add_argument("--services", required=True)
    ap.add_argument("--work", required=True)
    ap.add_argument("--evidence", required=True)
    ap.add_argument("--check-only", action="store_true", help="re-check an existing plan (used again by K11)")
    a = ap.parse_args()
    if not 1 <= a.slot <= 9:
        raise SystemExit("slot must be 1..9")
    base = 5000 + 100 * a.slot
    names = a.services.split() + ["sink"]
    unknown = [n for n in names if n not in OFFSETS]
    if unknown:
        raise SystemExit(f"unknown service(s): {unknown}")
    ports = {n: base + OFFSETS[n] for n in names}
    ports["mongo"] = 30994 + 1000 * a.slot

    rows, bad = [], []
    for name, port in sorted(ports.items(), key=lambda x: x[1]):
        why = forbidden(port)
        busy = listening(port)
        free = not busy and bindable(port)
        state = "FORBIDDEN:" + why if why else ("FREE" if free else "IN-USE")
        rows.append((name, port, state))
        if why or not free:
            bad.append((name, port, state))
    raw = pathlib.Path(a.evidence, "raw"); raw.mkdir(parents=True, exist_ok=True)
    tag = "recheck" if a.check_only else "before"
    lsof = subprocess.run(["lsof", "-nP"] + [f"-iTCP:{p}" for p in ports.values()] + ["-sTCP:LISTEN"],
                          capture_output=True, text=True)
    (raw / f"ports-lsof-{tag}.txt").write_text(lsof.stdout or "(no listeners)\n")
    with open(raw / f"PORTS-{tag}.tsv", "w") as f:
        f.write("role\tport\tstate\n")
        for r in rows:
            f.write("\t".join(map(str, r)) + "\n")
    if not a.check_only:
        pathlib.Path(a.work).mkdir(parents=True, exist_ok=True)
        pathlib.Path(a.work, "ports.json").write_text(json.dumps({"slot": a.slot, "ports": ports}, indent=2) + "\n")
    if bad:
        print("K03 FAIL:", "; ".join(f"{n}:{p} {s}" for n, p, s in bad)); sys.exit(1)
    print("K03 PASS:", " ".join(f"{n}={p}" for n, p in sorted(ports.items(), key=lambda x: x[1])))


if __name__ == "__main__":
    main()
