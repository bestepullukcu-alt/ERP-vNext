#!/usr/bin/env python3
import csv
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
BASE = ROOT / "docs/roadmap/plans/mvp6-effort-baseline-reconcile-02/EFFORT.tsv"
OUT = Path(__file__).resolve().parent

with BASE.open(newline="", encoding="utf-8") as stream:
    rows = list(csv.DictReader(stream, delimiter="\t"))
fields = list(rows[0].keys())

for row in rows:
    if row["id"] == "0183-4-REMAINING":
        row.update({
            "id": "0183-4-DELIVERED",
            "deliverable": "Tamamlanan bounded list/detail/create/transition/POD UI ve yedi dil uygulaması",
            "state": "DELIVERED",
            "evidence_or_scope": "docs/records/audits/2026-09/mvp6-shipment-pod-ui-exec-01/SOP-22.md",
            "reason_boundary": "UI implementation and focused tests complete; composed build/runtime/browser/independent VER remain outside this row",
        })
    elif row["id"] == "S1-DELIVERED":
        row.update({
            "optimistic_hours": "51.6",
            "most_likely_hours": "72",
            "pessimistic_hours": "102.0",
            "evidence_or_scope": "docs/records/audits/2026-09/mvp6-carrier-numericdate-exec-01/SOP-22-DEV-HANDOFF.md",
            "reason_boundary": "Approved NumericDate two-file application and writer-complete; shared effort not repeated under Carrier",
        })
    elif row["id"] == "S1-REMAINING":
        row.update({
            "optimistic_hours": "0",
            "most_likely_hours": "0",
            "pessimistic_hours": "0",
            "evidence_or_scope": "docs/records/audits/2026-09/mvp6-carrier-numericdate-exec-01/SOP-22-DEV-HANDOFF.md",
            "reason_boundary": "NumericDate implementation/authority/application reserve transferred to delivered; no new estimate",
        })
    elif row["id"] == "S2-DELIVERED":
        row.update({
            "evidence_or_scope": "docs/records/audits/2026-09/mvp6-carrier-numericdate-exec-ver-01/SOP-22-VER.md",
            "reason_boundary": "F01/F02 independently closed; no extra numeric credit because the remaining row co-mingles final Auth issuance",
        })
    elif row["id"] == "S2-REMAINING":
        row.update({
            "deliverable": "Shared Auth login/refresh/re-resolution independent completion",
            "evidence_or_scope": "docs/records/audits/2026-09/mvp6-carrier-numericdate-exec-ver-01/SOP-22-VER.md",
            "reason_boundary": "F01/F02 closed; full 16h reserve retained because no reliable split isolates the uncompleted real Auth chain",
        })

with (OUT / "EFFORT.tsv").open("w", newline="", encoding="utf-8") as stream:
    writer = csv.DictWriter(stream, fieldnames=fields, delimiter="\t", lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)

def sums(filtered):
    result = {state: [0.0, 0.0, 0.0] for state in ("DELIVERED", "REMAINING")}
    for row in filtered:
        vals = [float(row["optimistic_hours"]), float(row["most_likely_hours"]), float(row["pessimistic_hours"])]
        bucket = result[row["state"]]
        for i, value in enumerate(vals):
            bucket[i] += value
    return result

module_order = []
for row in rows:
    if row["module"] not in module_order:
        module_order.append(row["module"])

categories = []
for row in rows:
    if row["category"] not in categories:
        categories.append(row["category"])

with (OUT / "MODULE-SUMMARY.tsv").open("w", newline="", encoding="utf-8") as stream:
    fieldnames = ["scope", "delivered_most_likely", "remaining_most_likely", "total_most_likely", "completion_percent"]
    writer = csv.DictWriter(stream, fieldnames=fieldnames, delimiter="\t", lineterminator="\n")
    writer.writeheader()
    for module in module_order + ["portfolio"]:
        selected = rows if module == "portfolio" else [r for r in rows if r["module"] == module]
        totals = sums(selected)
        delivered, remaining = totals["DELIVERED"][1], totals["REMAINING"][1]
        writer.writerow({
            "scope": module,
            "delivered_most_likely": f"{delivered:g}",
            "remaining_most_likely": f"{remaining:g}",
            "total_most_likely": f"{delivered + remaining:g}",
            "completion_percent": f"{100 * delivered / (delivered + remaining):.1f}",
        })

with (OUT / "MODULE-CATEGORY-PERCENT.tsv").open("w", newline="", encoding="utf-8") as stream:
    fieldnames = ["scope"] + categories + ["Overall"]
    writer = csv.DictWriter(stream, fieldnames=fieldnames, delimiter="\t", lineterminator="\n")
    writer.writeheader()
    for module in module_order + ["portfolio"]:
        scope_rows = rows if module == "portfolio" else [r for r in rows if r["module"] == module]
        result = {"scope": module}
        for category in categories:
            selected = [r for r in scope_rows if r["category"] == category]
            totals = sums(selected)
            delivered, remaining = totals["DELIVERED"][1], totals["REMAINING"][1]
            result[category] = f"{100 * delivered / (delivered + remaining):.1f}" if delivered + remaining else "N/A"
        totals = sums(scope_rows)
        delivered, remaining = totals["DELIVERED"][1], totals["REMAINING"][1]
        result["Overall"] = f"{100 * delivered / (delivered + remaining):.1f}"
        writer.writerow(result)

with (OUT / "RECALCULATION.tsv").open("w", newline="", encoding="utf-8") as stream:
    fieldnames = ["scope", "category", "delivered_optimistic", "delivered_most_likely", "delivered_pessimistic", "remaining_optimistic", "remaining_most_likely", "remaining_pessimistic", "most_likely_completion_percent"]
    writer = csv.DictWriter(stream, fieldnames=fieldnames, delimiter="\t", lineterminator="\n")
    writer.writeheader()
    for scope in module_order + ["portfolio"]:
        scope_rows = rows if scope == "portfolio" else [r for r in rows if r["module"] == scope]
        for category in categories + ["TOTAL"]:
            selected = scope_rows if category == "TOTAL" else [r for r in scope_rows if r["category"] == category]
            if not selected:
                continue
            totals = sums(selected)
            delivered, remaining = totals["DELIVERED"], totals["REMAINING"]
            denominator = delivered[1] + remaining[1]
            writer.writerow({
                "scope": scope,
                "category": category,
                "delivered_optimistic": f"{delivered[0]:g}",
                "delivered_most_likely": f"{delivered[1]:g}",
                "delivered_pessimistic": f"{delivered[2]:g}",
                "remaining_optimistic": f"{remaining[0]:g}",
                "remaining_most_likely": f"{remaining[1]:g}",
                "remaining_pessimistic": f"{remaining[2]:g}",
                "most_likely_completion_percent": f"{100 * delivered[1] / denominator:.1f}" if denominator else "N/A",
            })

assert len(rows) == 103
portfolio = sums(rows)
assert portfolio["DELIVERED"][1] == 1372
assert portfolio["REMAINING"][1] == 1432
assert portfolio["DELIVERED"][1] + portfolio["REMAINING"][1] == 2804
