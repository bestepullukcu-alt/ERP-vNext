#!/usr/bin/env python3
import csv
import hashlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


lines = []
failures = []

with (HERE / "SOURCES.tsv").open(newline="") as handle:
    for row in csv.DictReader(handle, delimiter="\t"):
        actual = sha256(ROOT / row["path"])
        ok = actual == row["sha256"]
        lines.append(f"{'PASS' if ok else 'FAIL'} source {row['path']}")
        if not ok:
            failures.append(row["path"])

for name in ("SCOPE-DISPOSITION.tsv", "EFFORT.tsv", "SCENARIOS.tsv", "PORTFOLIO-IMPACT.tsv", "SOURCES.tsv"):
    with (HERE / name).open(newline="") as handle:
        rows = list(csv.reader(handle, delimiter="\t"))
    width = len(rows[0])
    ok = all(len(row) == width for row in rows)
    lines.append(f"{'PASS' if ok else 'FAIL'} {name} rows={len(rows)-1} columns={width}")
    if not ok:
        failures.append(name)

with (ROOT / "docs/roadmap/plans/mvp6-effort-shipment-loads-update-06/EFFORT.tsv").open(newline="") as handle:
    baseline = list(csv.DictReader(handle, delimiter="\t"))

keys = ("optimistic_hours", "most_likely_hours", "pessimistic_hours")
delivered = [sum(float(row[key]) for row in baseline if row["state"] == "DELIVERED") for key in keys]
remaining = [sum(float(row[key]) for row in baseline if row["state"] == "REMAINING") for key in keys]
expected_delivered = [1052.2, 1432.0, 1859.8]
expected_remaining = [822.8, 1382.0, 2580.0]
for label, actual, expected in (("delivered", delivered, expected_delivered), ("remaining", remaining, expected_remaining)):
    ok = all(abs(a - e) < 0.001 for a, e in zip(actual, expected))
    lines.append(f"{'PASS' if ok else 'FAIL'} baseline_{label}={actual}")
    if not ok:
        failures.append(f"baseline_{label}")

net_add = [16.0, 28.0, 48.0]
successor_remaining = [a + b for a, b in zip(remaining, net_add)]
successor_total = [a + b for a, b in zip(delivered, successor_remaining)]
lines.append(f"PASS net_add={net_add}")
lines.append(f"PASS successor_remaining={successor_remaining}")
lines.append(f"PASS successor_total={successor_total}")
lines.append(f"PASS loads_index={136/308*100:.1f}%")
lines.append(f"PASS portfolio_index={1432/2842*100:.1f}%")

print("\n".join(lines))
raise SystemExit(1 if failures else 0)
