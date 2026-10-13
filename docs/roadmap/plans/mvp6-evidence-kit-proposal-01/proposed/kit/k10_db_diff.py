#!/usr/bin/env python3
"""CANDIDATE — NOT ACTIVE. MVP6 evidence kit phase K10b: assert a DB before/after pair.

Usage: k10_db_diff.py before.json after.json --expect "sce_shipments=0 sce_shipment_audit=+1" [--state Status=Planned]
  counts: '0' = unchanged (zero-write negative case), '+N'/'-N' = exact delta, '@N' = exact absolute count.
Appends one row to E/raw/db-assertions.tsv (derived from the path of after.json) and exits 1 on mismatch.
Every mutation AND every negative case gets a pair (process guide §5).
"""
import argparse, json, pathlib, sys


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("before"); ap.add_argument("after")
    ap.add_argument("--expect", required=True)
    ap.add_argument("--state", nargs="*", default=[])
    a = ap.parse_args()
    b = json.loads(pathlib.Path(a.before).read_text()); f = json.loads(pathlib.Path(a.after).read_text())
    if b.get("db") != f.get("db") or b.get("port") != f.get("port"):
        raise SystemExit("before/after were taken from different databases or ports")
    bad = []
    for item in a.expect.split():
        name, want = item.split("=", 1)
        before, after = b["counts"].get(name), f["counts"].get(name)
        if before is None or after is None:
            bad.append(f"{name}: not captured"); continue
        delta = after - before
        if want == "0":
            ok = delta == 0
        elif want[0] in "+-":
            ok = delta == int(want)
        elif want[0] == "@":
            ok = after == int(want[1:])
        else:
            raise SystemExit(f"bad expectation {item!r}: use 0, +N, -N or @N")
        if not ok:
            bad.append(f"{name}: before={before} after={after} expected {want}")
    for item in a.state:
        k, v = item.split("=", 1)
        got = (f.get("state") or {}).get(k)
        if str(got) != v:
            bad.append(f"state {k}: {got!r} != {v!r}")
    out = pathlib.Path(a.after).parent / "db-assertions.tsv"
    new = not out.exists()
    with open(out, "a") as fh:
        if new:
            fh.write("before\tafter\texpect\tstate\tresult\tdetail\n")
        fh.write(f"{b.get('label')}\t{f.get('label')}\t{a.expect}\t{' '.join(a.state) or '-'}\t{'FAIL' if bad else 'PASS'}\t{'; '.join(bad) or '-'}\n")
    print(("K10 FAIL: " + "; ".join(bad)) if bad else f"K10 PASS: {f.get('label')}")
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
