#!/usr/bin/env python3
import csv
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
BASE = ROOT / "docs/roadmap/plans/mvp6-effort-carrier-e2e-update-05/EFFORT.tsv"
OUT = Path(__file__).resolve().parent


def read_rows():
    with BASE.open(newline="", encoding="utf-8") as handle:
        return list(csv.DictReader(handle, delimiter="\t"))


def number(value):
    return float(value)


def totals(rows):
    result = {state: [0.0, 0.0, 0.0] for state in ("DELIVERED", "REMAINING")}
    for row in rows:
        for index, key in enumerate(("optimistic_hours", "most_likely_hours", "pessimistic_hours")):
            result[row["state"]][index] += number(row[key])
    return result


rows = read_rows()
fields = list(rows[0])
result = []

for row in rows:
    row = dict(row)
    row_id = row["id"]
    if row_id == "0183-5-DELIVERED":
        row.update(
            deliverable="Tamamlanan exact source/composition, gateway route ve module integration kapanışı",
            optimistic_hours="21",
            most_likely_hours="36",
            pessimistic_hours="55",
            evidence_or_scope="docs/records/audits/2026-09/mvp6-shipment-integration-rework-independent-ver-01/SOP-22.md",
            reason_boundary="Existing integration reserve transferred after independent 351/351 source closure, Release builds and 24/24 route verification; real-Auth/browser remains Test/VER",
        )
    elif row_id == "0183-5-REMAINING":
        row.update(
            optimistic_hours="0",
            most_likely_hours="0",
            pessimistic_hours="0",
            evidence_or_scope="docs/records/audits/2026-09/mvp6-shipment-integration-rework-independent-ver-01/SOP-22.md",
            reason_boundary="Exact integration/source/build/route reserve transferred to delivered; no real-Auth, browser, PNG or full-module credit",
        )
    elif row_id == "0183-6-REMAINING":
        row.update(
            evidence_or_scope="docs/records/audits/2026-09/mvp6-shipment-integration-rework-independent-ver-01/SOP-22.md",
            reason_boundary="Independent build/route and bounded unauthenticated runtime passed; real-Auth Shipment HTTP, browser, permissions, tenant/LE, persistence actions and PNG remain OPEN",
        )
    elif row_id == "0184-6-REMAINING":
        row.update(
            evidence_or_scope="docs/records/audits/2026-09/mvp6-carrier-real-auth-ct-review-01/SOP-22.md",
            reason_boundary="CT verdict is PARTIAL; mandatory durable PNG remains OPEN, so the co-mingled final Test/VER reserve is retained without invented allocation",
        )
    elif row_id == "0185-1-REMAINING":
        row.update(
            deliverable="UI exact pack delta, decision binding and Phase 1.5 dispatch closure",
            optimistic_hours="4",
            most_likely_hours="8",
            pessimistic_hours="16",
            evidence_or_scope="docs/roadmap/plans/mvp6-loads-ui-scope-01/EFFORT.md",
            reason_boundary="Replaces the prior design reserve; no completed-work credit",
        )
    elif row_id == "0185-4-REMAINING":
        row.update(
            id="0185-4-UI-REMAINING",
            deliverable="List/create Slim UI, repeatable editor, seven languages and module tests",
            optimistic_hours="24",
            most_likely_hours="40",
            pessimistic_hours="64",
            evidence_or_scope="docs/roadmap/plans/mvp6-loads-ui-scope-01/EFFORT.md",
            reason_boundary="Bounded first UI slice only; transition/detail/lookup and root-driven UI remain unestimated scope",
        )
    elif row_id == "0185-5-REMAINING":
        ui = dict(row)
        ui.update(
            id="0185-5-UI-REMAINING",
            deliverable="UI Gateway/navigation/permission/L10n/personalization integration",
            optimistic_hours="8",
            most_likely_hours="16",
            pessimistic_hours="28",
            evidence_or_scope="docs/roadmap/plans/mvp6-loads-ui-scope-01/EFFORT.md",
            reason_boundary="UI portion of the replacement integration estimate",
        )
        live = dict(row)
        live.update(
            id="0185-5-LIVE-REMAINING",
            deliverable="Live-reference and root integration outside the first UI slice",
            optimistic_hours="12",
            most_likely_hours="20",
            pessimistic_hours="32",
            evidence_or_scope="docs/roadmap/plans/mvp6-loads-ui-scope-01/EFFORT.md",
            reason_boundary="Retains live/root integration in the full MVP denominator; exact transition UI awaits root decision",
        )
        result.extend((ui, live))
        continue
    elif row_id == "0185-6-REMAINING":
        ui = dict(row)
        ui.update(
            id="0185-6-UI-REMAINING",
            deliverable="Real-Auth browser, persistence and restart independent UI verification",
            optimistic_hours="12",
            most_likely_hours="20",
            pessimistic_hours="36",
            evidence_or_scope="docs/roadmap/plans/mvp6-loads-ui-scope-01/EFFORT.md",
            reason_boundary="Verification for the bounded first UI slice",
        )
        live = dict(row)
        live.update(
            id="0185-6-LIVE-REMAINING",
            deliverable="Cross-module/live acceptance and narrow defect closure",
            optimistic_hours="8",
            most_likely_hours="16",
            pessimistic_hours="28",
            evidence_or_scope="docs/roadmap/plans/mvp6-loads-ui-scope-01/EFFORT.md",
            reason_boundary="Retains downstream/live acceptance beyond the first UI slice",
        )
        result.extend((ui, live))
        continue
    result.append(row)

rows = result
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
    names = ["scope", "delivered_most_likely", "remaining_most_likely", "total_most_likely", "completion_percent"]
    writer = csv.DictWriter(handle, fieldnames=names, delimiter="\t", lineterminator="\n")
    writer.writeheader()
    for module in modules + ["portfolio"]:
        selected = rows if module == "portfolio" else [row for row in rows if row["module"] == module]
        value = totals(selected)
        delivered = value["DELIVERED"][1]
        remaining = value["REMAINING"][1]
        writer.writerow({
            "scope": module,
            "delivered_most_likely": f"{delivered:g}",
            "remaining_most_likely": f"{remaining:g}",
            "total_most_likely": f"{delivered + remaining:g}",
            "completion_percent": f"{100 * delivered / (delivered + remaining):.1f}",
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
            delivered = value["DELIVERED"][1]
            remaining = value["REMAINING"][1]
            output[category] = f"{100 * delivered / (delivered + remaining):.1f}" if delivered + remaining else "N/A"
        value = totals(selected)
        delivered = value["DELIVERED"][1]
        remaining = value["REMAINING"][1]
        output["Overall"] = f"{100 * delivered / (delivered + remaining):.1f}"
        writer.writerow(output)

with (OUT / "RECALCULATION.tsv").open("w", newline="", encoding="utf-8") as handle:
    names = ["scope", "category", "delivered_optimistic", "delivered_most_likely", "delivered_pessimistic", "remaining_optimistic", "remaining_most_likely", "remaining_pessimistic", "most_likely_completion_percent"]
    writer = csv.DictWriter(handle, fieldnames=names, delimiter="\t", lineterminator="\n")
    writer.writeheader()
    for scope in modules + ["portfolio"]:
        scoped = rows if scope == "portfolio" else [row for row in rows if row["module"] == scope]
        for category in categories + ["TOTAL"]:
            selected = scoped if category == "TOTAL" else [row for row in scoped if row["category"] == category]
            if not selected:
                continue
            value = totals(selected)
            delivered = value["DELIVERED"]
            remaining = value["REMAINING"]
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

assert len(rows) == 105
value = totals(rows)
assert value["DELIVERED"][1] == 1432
assert value["REMAINING"][1] == 1382
assert value["DELIVERED"][1] + value["REMAINING"][1] == 2814
