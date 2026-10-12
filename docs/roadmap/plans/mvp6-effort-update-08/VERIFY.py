#!/usr/bin/env python3
import csv
import hashlib
import os
from pathlib import Path

OUT = Path(__file__).resolve().parent
ROOT = Path(os.environ["ERP_ROOT"]) if os.environ.get("ERP_ROOT") else OUT.parents[3]


def rows(name, base=OUT):
    with (base / name).open(newline="", encoding="utf-8") as handle:
        return list(csv.DictReader(handle, delimiter="\t"))


checks = []
sources = rows("SOURCES.tsv")
for src in sources:
    actual = hashlib.sha256((ROOT / src["path"]).read_bytes()).hexdigest()
    assert actual == src["sha256"], (src["path"], actual)
checks.append(f"PASS source hashes {len(sources)}/{len(sources)}")

effort = rows("EFFORT.tsv")
assert len(effort) == 106
checks.append("PASS frozen ledger rows 106")

prev = {r["id"]: r for r in rows("EFFORT.tsv", ROOT / "docs/roadmap/plans/mvp6-effort-shipment-ct-update-07")}
changed = {"0183-6-DELIVERED", "0183-6-REMAINING", "0185-2-DELIVERED", "0185-2-REMAINING"}
cur = {r["id"]: r for r in effort}
assert set(prev) == set(cur)
for key in prev:
    if key not in changed:
        assert cur[key] == prev[key], key
checks.append(f"PASS unchanged predecessor rows exact {len(prev) - len(changed)}/{len(prev) - len(changed)}")

KEYS = ("optimistic_hours", "most_likely_hours", "pessimistic_hours")
def tot(ledger, state):
    return [round(sum(float(r[k]) for r in ledger if r["state"] == state), 1) for k in KEYS]

for key in ("0183-6", "0185-2"):
    before = [sum(float(prev[f"{key}-{s}"][k]) for s in ("DELIVERED", "REMAINING")) for k in KEYS]
    after = [sum(float(cur[f"{key}-{s}"][k]) for s in ("DELIVERED", "REMAINING")) for k in KEYS]
    assert [round(x, 1) for x in before] == [round(x, 1) for x in after], key
checks.append("PASS changed rows are pure transfers (row-pair totals unchanged)")

assert tot(effort, "DELIVERED") == [1069.0, 1464.0, 1916.6]
assert tot(effort, "REMAINING") == [822.0, 1378.0, 2571.2]
checks.append("PASS frozen portfolio O/M/P: delivered + remaining = 1891/2842/4487.8")

forecast = rows("FORECAST.tsv")
fl = effort + forecast
d, r = tot(fl, "DELIVERED"), tot(fl, "REMAINING")
assert d[1] == 1469.0 and r[1] == 1485.0 and round(d[1] + r[1], 1) == 2954.0
checks.append("PASS forecast delivered + remaining = 2954 M")

for name, ledger in (("MODULE-SUMMARY.tsv", effort), ("FORECAST-SUMMARY.tsv", fl)):
    for s in rows(name):
        sel = ledger if s["scope"] == "portfolio" else [x for x in ledger if x["module"] == s["scope"]]
        dd = sum(float(x["most_likely_hours"]) for x in sel if x["state"] == "DELIVERED")
        rr = sum(float(x["most_likely_hours"]) for x in sel if x["state"] == "REMAINING")
        assert round(dd, 1) == float(s["delivered_most_likely"]) and round(rr, 1) == float(s["remaining_most_likely"])
        assert round(dd + rr, 1) == float(s["total_most_likely"]), (name, s["scope"])
checks.append("PASS per-module delivered + remaining = total (frozen and forecast)")

accepted = sum(float(a["delta_most_likely"]) for a in rows("ACCEPTED.tsv"))
assert accepted == 1140.0 and accepted <= 1464.0 and accepted <= d[1]
checks.append("PASS CT-accepted 1140 <= delivered (frozen 1464, forecast 1469)")

for rc in rows("ROW-CHANGES.tsv"):
    assert "docs/records/" in rc["controlling_evidence"] or rc["source_row"] == "portfolio", rc["source_row"]
checks.append("PASS every ROW-CHANGES row cites a CT record")

ids = {u["id"] for u in rows("UNESTIMATED-SCOPE.tsv")}
assert {"0185-RS-05", "0185-RS-06", "0185-RS-07", "UI-0190", "UI-0192", "SUP-0147-0148", "SH-KIT"} <= ids
checks.append(f"PASS unestimated scope listed {len(ids)} items")

(OUT / "VALIDATION.txt").write_text("\n".join(checks) + "\n", encoding="utf-8")
print("\n".join(checks))
