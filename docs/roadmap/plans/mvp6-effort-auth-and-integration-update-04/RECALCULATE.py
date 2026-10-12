#!/usr/bin/env python3
import csv
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
BASE = ROOT / "docs/roadmap/plans/mvp6-effort-progress-update-03/EFFORT.tsv"
OUT = Path(__file__).resolve().parent

with BASE.open(newline="", encoding="utf-8") as stream:
    rows = list(csv.DictReader(stream, delimiter="\t"))
fields = list(rows[0])

for row in rows:
    if row["id"] == "S2-DELIVERED":
        row.update({
            "optimistic_hours": "24.6",
            "most_likely_hours": "36",
            "pessimistic_hours": "57",
            "evidence_or_scope": "docs/records/audits/2026-09/mvp6-carrier-auth-chain-recovery-01/SOP-22-VER.md",
            "reason_boundary": "Fresh Auth login, refresh, LE re-resolution, zero/multiple/inactive/revoked scope and refusal/timeout close the exact remaining S2 delivery; prior NumericDate implementation credit is not repeated",
        })
    elif row["id"] == "S2-REMAINING":
        row.update({
            "optimistic_hours": "0",
            "most_likely_hours": "0",
            "pessimistic_hours": "0",
            "evidence_or_scope": "docs/records/audits/2026-09/mvp6-carrier-auth-chain-recovery-01/SOP-22-VER.md",
            "reason_boundary": "Exact login/refresh/re-resolution reserve transferred to delivered; inherited MFA/forced-password and broader tuples were not relabeled as fresh runs",
        })

with (OUT / "EFFORT.tsv").open("w", newline="", encoding="utf-8") as stream:
    writer = csv.DictWriter(stream, fieldnames=fields, delimiter="\t", lineterminator="\n")
    writer.writeheader(); writer.writerows(rows)

def sums(selected):
    result = {state: [0.0, 0.0, 0.0] for state in ("DELIVERED", "REMAINING")}
    for row in selected:
        vals = [float(row["optimistic_hours"]), float(row["most_likely_hours"]), float(row["pessimistic_hours"])]
        for i, value in enumerate(vals): result[row["state"]][i] += value
    return result

modules=[]; categories=[]
for row in rows:
    if row["module"] not in modules: modules.append(row["module"])
    if row["category"] not in categories: categories.append(row["category"])

with (OUT / "MODULE-SUMMARY.tsv").open("w", newline="", encoding="utf-8") as stream:
    names=["scope","delivered_most_likely","remaining_most_likely","total_most_likely","completion_percent"]
    w=csv.DictWriter(stream,fieldnames=names,delimiter="\t",lineterminator="\n"); w.writeheader()
    for module in modules+["portfolio"]:
        selected=rows if module=="portfolio" else [r for r in rows if r["module"]==module]
        t=sums(selected); d=t["DELIVERED"][1]; rem=t["REMAINING"][1]
        w.writerow(dict(scope=module,delivered_most_likely=f"{d:g}",remaining_most_likely=f"{rem:g}",total_most_likely=f"{d+rem:g}",completion_percent=f"{100*d/(d+rem):.1f}"))

with (OUT / "MODULE-CATEGORY-PERCENT.tsv").open("w", newline="", encoding="utf-8") as stream:
    names=["scope"]+categories+["Overall"]
    w=csv.DictWriter(stream,fieldnames=names,delimiter="\t",lineterminator="\n"); w.writeheader()
    for module in modules+["portfolio"]:
        scope_rows=rows if module=="portfolio" else [r for r in rows if r["module"]==module]
        result={"scope":module}
        for category in categories:
            t=sums([r for r in scope_rows if r["category"]==category]); d=t["DELIVERED"][1]; rem=t["REMAINING"][1]
            result[category]=f"{100*d/(d+rem):.1f}" if d+rem else "N/A"
        t=sums(scope_rows); d=t["DELIVERED"][1]; rem=t["REMAINING"][1]; result["Overall"]=f"{100*d/(d+rem):.1f}"
        w.writerow(result)

with (OUT / "RECALCULATION.tsv").open("w", newline="", encoding="utf-8") as stream:
    names=["scope","category","delivered_optimistic","delivered_most_likely","delivered_pessimistic","remaining_optimistic","remaining_most_likely","remaining_pessimistic","most_likely_completion_percent"]
    w=csv.DictWriter(stream,fieldnames=names,delimiter="\t",lineterminator="\n"); w.writeheader()
    for scope in modules+["portfolio"]:
        scope_rows=rows if scope=="portfolio" else [r for r in rows if r["module"]==scope]
        for category in categories+["TOTAL"]:
            selected=scope_rows if category=="TOTAL" else [r for r in scope_rows if r["category"]==category]
            if not selected: continue
            t=sums(selected); d=t["DELIVERED"]; rem=t["REMAINING"]; denominator=d[1]+rem[1]
            w.writerow(dict(scope=scope,category=category,delivered_optimistic=f"{d[0]:g}",delivered_most_likely=f"{d[1]:g}",delivered_pessimistic=f"{d[2]:g}",remaining_optimistic=f"{rem[0]:g}",remaining_most_likely=f"{rem[1]:g}",remaining_pessimistic=f"{rem[2]:g}",most_likely_completion_percent=f"{100*d[1]/denominator:.1f}" if denominator else "N/A"))

assert len(rows)==103
t=sums(rows)
assert t["DELIVERED"][1]==1388 and t["REMAINING"][1]==1416 and t["DELIVERED"][1]+t["REMAINING"][1]==2804
