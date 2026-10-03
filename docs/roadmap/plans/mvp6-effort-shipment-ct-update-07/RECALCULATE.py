#!/usr/bin/env python3
import csv
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
BASE = ROOT / "docs/roadmap/plans/mvp6-effort-shipment-loads-update-06/EFFORT.tsv"
OUT = Path(__file__).resolve().parent


def read_rows(path):
    with path.open(newline="", encoding="utf-8") as handle:
        return list(csv.DictReader(handle, delimiter="\t"))


def totals(rows):
    result = {state: [0.0, 0.0, 0.0] for state in ("DELIVERED", "REMAINING")}
    for row in rows:
        for index, key in enumerate(("optimistic_hours", "most_likely_hours", "pessimistic_hours")):
            result[row["state"]][index] += float(row[key])
    return result


rows = []
fields = None
for original in read_rows(BASE):
    row = dict(original)
    fields = fields or list(row)
    row_id = row["id"]
    if row_id == "0183-3-DELIVERED":
        row.update(
            deliverable="Teslim edilmiş bounded çekirdek implementation + kabul edilmiş Root R2 mutation/persistence düzeltmesi",
            optimistic_hours="52",
            most_likely_hours="72",
            pessimistic_hours="96",
            evidence_or_scope="docs/records/audits/2026-09/mvp6-shipment-root-r2-ct-disposition-01/EFFORT-ROW-MAPPING.tsv",
            reason_boundary="Existing narrow-rework reserve transfer: Root R2 application/writer 4/8/16 closed; forecast allocation is not measured actual time",
        )
    elif row_id == "0183-3-REMAINING":
        row.update(
            optimistic_hours="5.6",
            most_likely_hours="8",
            pessimistic_hours="9.6",
            evidence_or_scope="docs/records/audits/2026-09/mvp6-shipment-root-r2-ct-disposition-01/EFFORT-ROW-MAPPING.tsv",
            reason_boundary="Root R2 application allocation 4/8/16 transferred to delivered; unrelated module-owned wiring reserve remains",
        )
    elif row_id == "0183-6-DELIVERED":
        row.update(
            deliverable="Tamamlanmış bounded bağımsız test/kabul çalışması + kabul edilmiş Root R2 runtime VER",
            optimistic_hours="30",
            most_likely_hours="44",
            pessimistic_hours="60",
            evidence_or_scope="docs/records/audits/2026-09/mvp6-shipment-root-r2-ct-disposition-01/EFFORT-ROW-MAPPING.tsv",
            reason_boundary="Existing Test/VER reserve transfer: independent Root R2 runtime VER 6/12/20 closed; browser row and PNG are not credited",
        )
    elif row_id == "0183-6-REMAINING":
        row.update(
            optimistic_hours="14",
            most_likely_hours="24",
            pessimistic_hours="44",
            evidence_or_scope="docs/records/audits/2026-09/mvp6-shipment-ui-functional-ct-consolidation-01/EFFORT-DISPOSITION.tsv",
            reason_boundary="After Root R2 runtime transfer, reserve retains 2/4/8 root-browser allocation plus broader UI/browser/PNG work; B01/B02 closure has no approved numeric split and receives no duplicate credit",
        )
    rows.append(row)

# The latest numeric baseline adds the mandatory transition UI estimate from the
# Loads remaining-scope disposition. Integration/VER portions overlap existing
# Loads reserves and are not added again.
transition = dict(rows[next(i for i, row in enumerate(rows) if row["id"] == "0185-4-UI-REMAINING")])
transition.update(
    id="0185-4-TRANSITION-REMAINING",
    deliverable="Mandatory Loads transition UI with exact lifecycle/root/replay/error behavior",
    optimistic_hours="16",
    most_likely_hours="28",
    pessimistic_hours="48",
    evidence_or_scope="docs/roadmap/plans/mvp6-loads-remaining-scope-estimate-01/EFFORT.tsv",
    reason_boundary="Non-overlapping mandatory frontend delivery; producer root implementation, optional detail and lookup remain unestimated",
)
insert_at = next(i for i, row in enumerate(rows) if row["id"] == "0185-4-UI-REMAINING") + 1
rows.insert(insert_at, transition)

with (OUT / "EFFORT.tsv").open("w", newline="", encoding="utf-8") as handle:
    writer = csv.DictWriter(handle, fieldnames=fields, delimiter="\t", lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)

modules = []
categories = []
for row in rows:
    if row["module"] not in modules:
        modules.append(row["module"])
    if row["category"] not in categories:
        categories.append(row["category"])

with (OUT / "MODULE-SUMMARY.tsv").open("w", newline="", encoding="utf-8") as handle:
    names = ["scope", "delivered_most_likely", "remaining_most_likely", "total_most_likely", "estimated_scope_index_percent"]
    writer = csv.DictWriter(handle, fieldnames=names, delimiter="\t", lineterminator="\n")
    writer.writeheader()
    for module in modules + ["portfolio"]:
        selected = rows if module == "portfolio" else [row for row in rows if row["module"] == module]
        value = totals(selected)
        delivered, remaining = value["DELIVERED"][1], value["REMAINING"][1]
        writer.writerow({
            "scope": module,
            "delivered_most_likely": f"{delivered:g}",
            "remaining_most_likely": f"{remaining:g}",
            "total_most_likely": f"{delivered + remaining:g}",
            "estimated_scope_index_percent": f"{100 * delivered / (delivered + remaining):.1f}",
        })

with (OUT / "MODULE-CATEGORY-PERCENT.tsv").open("w", newline="", encoding="utf-8") as handle:
    names = ["scope"] + categories + ["Overall"]
    writer = csv.DictWriter(handle, fieldnames=names, delimiter="\t", lineterminator="\n")
    writer.writeheader()
    for module in modules + ["portfolio"]:
        selected = rows if module == "portfolio" else [row for row in rows if row["module"] == module]
        output = {"scope": module}
        for category in categories:
            value = totals([row for row in selected if row["category"] == category])
            delivered, remaining = value["DELIVERED"][1], value["REMAINING"][1]
            output[category] = f"{100 * delivered / (delivered + remaining):.1f}" if delivered + remaining else "N/A"
        value = totals(selected)
        delivered, remaining = value["DELIVERED"][1], value["REMAINING"][1]
        output["Overall"] = f"{100 * delivered / (delivered + remaining):.1f}"
        writer.writerow(output)

with (OUT / "RECALCULATION.tsv").open("w", newline="", encoding="utf-8") as handle:
    names = ["scope", "category", "delivered_optimistic", "delivered_most_likely", "delivered_pessimistic", "remaining_optimistic", "remaining_most_likely", "remaining_pessimistic", "most_likely_estimated_scope_index_percent"]
    writer = csv.DictWriter(handle, fieldnames=names, delimiter="\t", lineterminator="\n")
    writer.writeheader()
    for scope in modules + ["portfolio"]:
        scoped = rows if scope == "portfolio" else [row for row in rows if row["module"] == scope]
        for category in categories + ["TOTAL"]:
            selected = scoped if category == "TOTAL" else [row for row in scoped if row["category"] == category]
            if not selected:
                continue
            value = totals(selected)
            delivered, remaining = value["DELIVERED"], value["REMAINING"]
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
                "most_likely_estimated_scope_index_percent": f"{100 * delivered[1] / denominator:.1f}" if denominator else "N/A",
            })

assert len(rows) == 106
value = totals(rows)
assert [round(v, 1) for v in value["DELIVERED"]] == [1062.2, 1452.0, 1895.8]
assert [round(v, 1) for v in value["REMAINING"]] == [828.8, 1390.0, 2592.0]
assert [round(a + b, 1) for a, b in zip(value["DELIVERED"], value["REMAINING"])] == [1891.0, 2842.0, 4487.8]
