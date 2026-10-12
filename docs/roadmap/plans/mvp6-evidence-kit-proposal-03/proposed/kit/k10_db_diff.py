#!/usr/bin/env python3
"""CANDIDATE — NOT ACTIVE. MVP6 evidence kit v1.2 phase K10b: assert a DB before/after pair.

Usage: k10_db_diff.py before.json after.json --expect "sce_shipment_audit=+1 sce_shipment_outbox=+2" [--state Status=Planned]
                      [--totals "sce_shipment_outbox=+2 ..."] [--allow-unlisted "reason"]
  counts: '0' = unchanged (zero-write negative case), '+N'/'-N' = exact delta, '@N' = exact absolute count.
v1.1 (G5): STRICT by default —
  * every counted collection NOT named in --expect must be unchanged (an omitted outbox delta now fails, as the A12
    VER-02 a09dup setup pair should have: outbox +2 went unasserted);
  * when both snapshots carry dbTotals (whole-database counts, k10_db_snapshot.js v1.1), every collection whose total
    changed must be named in --totals (or in --expect with the same delta); any other whole-DB change fails.
  --allow-unlisted "<reason>" relaxes the first rule only and records the reason in the assertion row.
v1.2 (F12): --totals-exempt "coll1,coll2" --exempt-reason "<why>" excludes named collections from the whole-DB rule for
  writes the lane does not control (e.g. asynchronous Platform background jobs, now enabled by D1). Both are required
  together, and both are recorded in the assertion row; exempted collections are still listed with their delta.
v1.1 (G3): refuses to add a row whose before or after file was already used by an earlier row, and refuses
  before == after, so attempts can never share (and silently overwrite) snapshot files.
Appends one row to E/raw/db-assertions.tsv (derived from the path of after.json) and exits 1 on mismatch.
Every mutation AND every negative case gets a pair (process guide §5).
"""
import argparse, csv, json, pathlib, sys


def parse(spec):
    out = {}
    for item in spec.split():
        name, want = item.split("=", 1)
        if not (want == "0" or want[0] in "+-@"):
            raise SystemExit(f"bad expectation {item!r}: use 0, +N, -N or @N")
        out[name] = want
    return out


def ok_delta(before, after, want):
    if want == "0":
        return after - before == 0
    if want[0] in "+-":
        return after - before == int(want)
    return after == int(want[1:])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("before"); ap.add_argument("after")
    ap.add_argument("--expect", required=True)
    ap.add_argument("--state", nargs="*", default=[])
    ap.add_argument("--totals", default="")
    ap.add_argument("--allow-unlisted", default="")
    ap.add_argument("--totals-exempt", default="")
    ap.add_argument("--exempt-reason", default="")
    a = ap.parse_args()
    bp, fp = pathlib.Path(a.before), pathlib.Path(a.after)
    if bp.resolve() == fp.resolve():
        raise SystemExit("before and after are the same file")
    out = fp.parent / "db-assertions.tsv"
    if out.exists():
        used = set()
        for r in csv.DictReader(open(out, newline=""), delimiter="\t"):
            used |= {r.get("before_file", ""), r.get("after_file", "")}
        clash = {bp.name, fp.name} & used
        if clash:
            raise SystemExit(f"G3: snapshot file(s) {sorted(clash)} already used by an earlier assertion — take new, attempt-suffixed snapshots")
    b = json.loads(bp.read_text()); f = json.loads(fp.read_text())
    if b.get("db") != f.get("db") or b.get("port") != f.get("port"):
        raise SystemExit("before/after were taken from different databases or ports")
    exp = parse(a.expect); tot = parse(a.totals) if a.totals else {}
    exempt = {x.strip() for x in a.totals_exempt.split(",") if x.strip()}
    if bool(exempt) != bool(a.exempt_reason.strip()):
        raise SystemExit("F12: --totals-exempt and --exempt-reason must be given together")
    exempt_seen = []
    bad = []
    for name, want in exp.items():
        before, after = b["counts"].get(name), f["counts"].get(name)
        if before is None or after is None:
            bad.append(f"{name}: not captured"); continue
        if not ok_delta(before, after, want):
            bad.append(f"{name}: before={before} after={after} expected {want}")
    unlisted = {k: f["counts"][k] - b["counts"].get(k, 0) for k in f.get("counts", {}) if k not in exp}
    moved = {k: d for k, d in unlisted.items() if d}
    if moved and not a.allow_unlisted:
        bad.append(f"unlisted counted collection(s) changed: {moved} (assert them or use --allow-unlisted with a reason)")
    bt, ft = b.get("dbTotals"), f.get("dbTotals")
    if bt is not None and ft is not None:
        for name in sorted(set(bt) | set(ft)):
            d = ft.get(name, 0) - bt.get(name, 0)
            if name in exempt:
                exempt_seen.append(f"{name}{d:+d}"); continue
            want = tot.get(name, exp.get(name))
            if want is None:
                if d:
                    bad.append(f"whole-DB {name}: delta {d:+d} not asserted (--totals)")
            elif not ok_delta(bt.get(name, 0), ft.get(name, 0), want):
                bad.append(f"whole-DB {name}: delta {d:+d} expected {want}")
    elif tot:
        bad.append("--totals given but a snapshot has no dbTotals (use k10_db_snapshot.js v1.1)")
    for item in a.state:
        k, v = item.split("=", 1)
        got = (f.get("state") or {}).get(k)
        if str(got) != v:
            bad.append(f"state {k}: {got!r} != {v!r}")
    new = not out.exists()
    with open(out, "a") as fh:
        if new:
            fh.write("before\tafter\tbefore_file\tafter_file\texpect\ttotals\tstate\tallow_unlisted\tresult\tdetail\ttotals_exempt\n")
        fh.write("\t".join([str(b.get("label")), str(f.get("label")), bp.name, fp.name, a.expect, a.totals or "-",
                            " ".join(a.state) or "-", a.allow_unlisted or "-", "FAIL" if bad else "PASS",
                            "; ".join(bad) or "-",
                            (f"{' '.join(exempt_seen) or ','.join(sorted(exempt))} ({a.exempt_reason})" if exempt else "-")]
                           ).replace("\n", " ") + "\n")
    print(("K10 FAIL: " + "; ".join(bad)) if bad else f"K10 PASS: {f.get('label')}")
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
