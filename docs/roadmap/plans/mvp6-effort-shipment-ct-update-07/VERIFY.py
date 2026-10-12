#!/usr/bin/env python3
import csv
import hashlib
from pathlib import Path

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]


def rows(name):
    with (OUT / name).open(newline="", encoding="utf-8") as handle:
        return list(csv.DictReader(handle, delimiter="\t"))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


expected = {
    "docs/roadmap/plans/mvp6-effort-shipment-loads-update-06/ARTIFACTS.sha256": "6f02cc694113d07e0af1951c33078226c4cc24c5755e80a773b3608704564d7d",
    "docs/roadmap/plans/mvp6-loads-remaining-scope-estimate-01/ARTIFACTS.sha256": "81e08bfd7203cca434fdc6b0e49be8440e0dcf7b72e75fbac354122b99b00377",
    "docs/roadmap/plans/mvp6-shipment-r2-effort-disposition-01/ARTIFACTS.sha256": "4bfeaf971540cfcfc69058eec2f13e98599f1e60fe18188681e140a35bd399cd",
    "docs/records/audits/2026-09/mvp6-shipment-root-r2-ct-disposition-01/ARTIFACTS.sha256": "95580a1593781cc7f8e77248c7a49871faf492ced1e98b448c3794ecc59002a7",
    "docs/records/audits/2026-09/mvp6-shipment-ui-functional-ct-consolidation-01/ARTIFACTS.sha256": "d16959c702db29456c2d1636d7f1aa2b96f8ef6a16cf039e52814cc443126395",
}
checks = []
for relative, wanted in expected.items():
    actual = sha(ROOT / relative)
    assert actual == wanted, (relative, actual, wanted)
checks.append("PASS controlling package hashes 5/5")

effort = rows("EFFORT.tsv")
assert len(effort) == 106
checks.append("PASS effort rows 106")

def total(state, key):
    return round(sum(float(row[key]) for row in effort if row["state"] == state), 1)

assert [total("DELIVERED", k) for k in ("optimistic_hours", "most_likely_hours", "pessimistic_hours")] == [1062.2, 1452.0, 1895.8]
assert [total("REMAINING", k) for k in ("optimistic_hours", "most_likely_hours", "pessimistic_hours")] == [828.8, 1390.0, 2592.0]
checks.append("PASS portfolio O/M/P delivered and remaining arithmetic")

summary = {row["scope"]: row for row in rows("MODULE-SUMMARY.tsv")}
assert summary["0183"] == {"scope": "0183", "delivered_most_likely": "248", "remaining_most_likely": "46", "total_most_likely": "294", "estimated_scope_index_percent": "84.4"}
assert summary["portfolio"]["total_most_likely"] == "2842"
assert summary["portfolio"]["estimated_scope_index_percent"] == "51.1"
checks.append("PASS Shipment and portfolio summary")

alloc = rows("EVIDENCE-ALLOCATION.tsv")
assert any(row["delivery_or_finding"].startswith("SHIP-UI-B01") and row["successor_numeric_treatment"] == "No additional numeric credit" for row in alloc)
assert any(row["delivery_or_finding"] == "Persistent PNG" and row["controlling_state"] == "OPEN" for row in alloc)
checks.append("PASS B01/B02 no-double-credit and PNG OPEN")

unestimated = rows("UNESTIMATED-SCOPE.tsv")
assert {row["id"] for row in unestimated} == {"0185-RS-05", "0185-RS-06", "0185-RS-07"}
checks.append("PASS Loads unestimated scope retained 3/3")

baseline = {row["id"]: row for row in csv.DictReader((ROOT / "docs/roadmap/plans/mvp6-effort-shipment-loads-update-06/EFFORT.tsv").open(), delimiter="\t")}
successor = {row["id"]: row for row in effort}
changed = {"0183-3-DELIVERED", "0183-3-REMAINING", "0183-6-DELIVERED", "0183-6-REMAINING"}
for key in baseline:
    if key not in changed:
        assert successor[key] == baseline[key], key
checks.append(f"PASS unaffected predecessor rows exact {len(baseline) - len(changed)}/{len(baseline) - len(changed)}")

(OUT / "VALIDATION.txt").write_text("\n".join(checks) + "\n", encoding="utf-8")

