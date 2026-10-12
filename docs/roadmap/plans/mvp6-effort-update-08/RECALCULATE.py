#!/usr/bin/env python3
"""MVP6 effort update 08 — reproduces every numeric output of this folder.

Inputs: update-07 EFFORT.tsv (frozen baseline ledger) and the cited estimate files.
ROOT = repository root (env ERP_ROOT, else four levels above this file). Outputs go next to this file.
"""
import csv
import os
from pathlib import Path

OUT = Path(__file__).resolve().parent
ROOT = Path(os.environ["ERP_ROOT"]) if os.environ.get("ERP_ROOT") else OUT.parents[3]
BASE = ROOT / "docs/roadmap/plans/mvp6-effort-shipment-ct-update-07/EFFORT.tsv"
KEYS = ("optimistic_hours", "most_likely_hours", "pessimistic_hours")

A12 = "docs/records/audits/2026-09/mvp6-ct-disposition-a12-ver02-2026-09-26.md"
R2MAP = "docs/records/audits/2026-09/mvp6-shipment-root-r2-ct-disposition-01/EFFORT-ROW-MAPPING.tsv"
LOADS_CT = "docs/records/audits/2026-09/mvp6-ct-disposition-q25-q32-2026-09-26.md"
LOADS_EST = "docs/roadmap/plans/mvp6-loads-remaining-scope-estimate-01/REPORT.md"
RET_EFF = "docs/roadmap/plans/mvp6-ui-pack-drafts-01/returns/EFFORT.md"
CLA_EFF = "docs/roadmap/plans/mvp6-ui-pack-drafts-01/claims/EFFORT.md"
SR_EFF = "docs/roadmap/plans/mvp6-self-registration-prep-01/EFFORT.md"
SR_CT = "docs/records/audits/2026-09/mvp6-ct-disposition-q46-q47-q49-2026-09-26.md; docs/records/audits/2026-09/mvp6-ct-disposition-q53-q54-2026-09-26.md"

# CT-accepted baseline: CT audit 2026-09-25 line 54 ("CT-accepted share under Rev2 rules 1,128 h = 39.7%").
ACCEPTED_BASE_ML = 1128.0


def read(path):
    with path.open(newline="", encoding="utf-8") as handle:
        return list(csv.DictReader(handle, delimiter="\t"))


def write(name, fields, rows):
    with (OUT / name).open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, delimiter="\t", lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def fmt(value):
    return f"{round(value, 1):g}"


def totals(rows):
    result = {state: [0.0, 0.0, 0.0] for state in ("DELIVERED", "REMAINING")}
    for row in rows:
        for index, key in enumerate(KEYS):
            result[row["state"]][index] += float(row[key])
    return result


# ---------------------------------------------------------------- frozen baseline ledger
rows = []
fields = None
for original in read(BASE):
    row = dict(original)
    fields = fields or list(row)
    rid = row["id"]
    if rid == "0183-6-DELIVERED":
        row.update(optimistic_hours="32", most_likely_hours="48", pessimistic_hours="68",
                   deliverable=row["deliverable"] + " + kabul edilmiş Root R2 browser regression (A12 runtime VER-02)",
                   evidence_or_scope=f"{R2MAP}; {A12}",
                   reason_boundary="0183-R2-04 root real-Auth browser allocation 2/4/8 closed by the independent A12 VER-02 browser run (A08/A09 transition/POD regression, detail, 16 durable PNG) accepted by CT 2026-09-26; broader UI rows stay in remaining")
    elif rid == "0183-6-REMAINING":
        row.update(optimistic_hours="12", most_likely_hours="20", pessimistic_hours="36",
                   evidence_or_scope=f"{A12}",
                   reason_boundary="After the R2-04 transfer the reserve keeps broader UI/browser acceptance (A01, A02, A04–A07, A10, A13, A14, A15), PNG for those rows and composed runtime; A12 itself has no separate O/M/P and receives no further credit")
    elif rid == "0185-2-DELIVERED":
        row.update(optimistic_hours="16.8", most_likely_hours="24", pessimistic_hours="32.8",
                   deliverable=row["deliverable"] + " + Loads 3.1.0 root seam disposition/candidate/publication",
                   evidence_or_scope=f"{LOADS_CT}; {LOADS_EST}",
                   reason_boundary="0185-2-REMAINING 4.8/8/12.8 transferred: the Loads estimate maps root-seam disposition/candidate/repin work (M 8) onto this reserve; 3.1.0 publication + guard binding CT ACCEPTED after independent VER Q32")
    elif rid == "0185-2-REMAINING":
        row.update(optimistic_hours="0", most_likely_hours="0", pessimistic_hours="0",
                   evidence_or_scope=f"{LOADS_CT}",
                   reason_boundary="Reserve consumed by the accepted root seam work; multi-Shipment root grouping policy stays an open owner boundary and is listed as UNESTIMATED (0185-RS-08); estimate pessimistic excess 3.2 stays unallocated uncertainty")
    rows.append(row)

write("EFFORT.tsv", fields, rows)

# ---------------------------------------------------------------- forecast rows (not in the frozen baseline)
def fc(fid, module, category, deliverable, state, o, m, p, source, boundary):
    return {"id": fid, "module": module, "category": category, "deliverable": deliverable, "state": state,
            "optimistic_hours": fmt(o), "most_likely_hours": fmt(m), "pessimistic_hours": fmt(p),
            "source": source, "boundary": boundary}

forecast = [
    # Returns UI — Scope A replacement of rows 1/4/5/6 expressed as per-row deltas (net +13.6/+26/+34.4)
    fc("FC-0186-1", "0186", "Pack/tasarım", "UI pack revision, Phase 1.5, dispatch closure (delta vs 0186-1-REMAINING)", "REMAINING", 0.4, 2, 6.4, RET_EFF, "4/8/16 replaces 3.6/6/9.6; pack revision applied (Q46) but no split from Phase 1.5/dispatch closure, so no credit"),
    fc("FC-0186-4", "0186", "Frontend", "List/create/transition UI, 7 languages, module tests (delta vs 0186-4-REMAINING)", "REMAINING", 3.2, 4, -8, RET_EFF, "32/52/88 replaces 28.8/48/96"),
    fc("FC-0186-5", "0186", "Entegrasyon", "Shared UI integration 8/16/28 + backend uptake 6/10/16 (delta vs 0186-5-REMAINING)", "REMAINING", 2, 6, 12, RET_EFF, "14/26/44 replaces 12/20/32"),
    fc("FC-0186-6", "0186", "Test/VER", "Independent UI VER 14/24/40 + module E2E 6/10/16 (delta vs 0186-6-REMAINING)", "REMAINING", 8, 14, 24, RET_EFF, "20/34/56 replaces 12/20/32"),
    # Claims UI — net +6.8/+16/+14.4
    fc("FC-0187-1", "0187", "Pack/tasarım", "UI pack revision, Phase 1.5, dispatch closure (delta vs 0187-1-REMAINING)", "REMAINING", 0.4, 2, 6.4, CLA_EFF, "4/8/16 replaces 3.6/6/9.6; pack revision applied (Q38) but no split, so no credit"),
    fc("FC-0187-4", "0187", "Frontend", "List/create/transition UI with exact amounts (delta vs 0187-4-REMAINING)", "REMAINING", -3.6, -6, -28, CLA_EFF, "30/50/84 replaces 33.6/56/112"),
    fc("FC-0187-5", "0187", "Entegrasyon", "Shared UI integration 8/16/28 + backend uptake 6/10/16 (delta vs 0187-5-REMAINING)", "REMAINING", 2, 6, 12, CLA_EFF, "14/26/44 replaces 12/20/32"),
    fc("FC-0187-6", "0187", "Test/VER", "Independent UI VER 14/24/40 + module E2E 6/10/16 (delta vs 0187-6-REMAINING)", "REMAINING", 8, 14, 24, CLA_EFF, "20/34/56 replaces 12/20/32"),
    # Self-registration 38/70/127 (D2=A, D5=A)
    fc("FC-SR-PACK", "SHARED", "Pack/tasarım", "Self-registration pack/DCP sections (DCP-009 §21; 0183–0187)", "DELIVERED", 3, 5, 8, f"{SR_EFF}; {SR_CT}", "Applied with independent VER PASS (Q47/Q49, Q53/Q54); CT records give NO product credit, so not in CT-accepted"),
    fc("FC-SR-FOUND", "SHARED", "Backend", "Self-registration foundation + F-01…F-06", "REMAINING", 4, 8, 14, SR_EFF, "Overlay per SR-D4; applied at final integration"),
    fc("FC-SR-0183", "0183", "Backend", "ShipmentTrackingPod provider + M-01…M-08", "REMAINING", 4, 6, 10, SR_EFF, "Ships with the module UI (D4) as overlay"),
    fc("FC-SR-0184", "0184", "Backend", "CarrierManagement provider + both-direction tests", "REMAINING", 2, 4, 7, SR_EFF, "Adapts the unapproved candidate"),
    fc("FC-SR-0185", "0185", "Backend", "RoutingLoadPlanning provider + tests", "REMAINING", 2, 3, 6, SR_EFF, "Only after the Loads UI exists"),
    fc("FC-SR-0186", "0186", "Backend", "ReverseLogistics provider + tests (D5 ForTarget)", "REMAINING", 3, 5, 9, SR_EFF, "Ships with the Returns UI"),
    fc("FC-SR-0187", "0187", "Backend", "ClaimsManagement provider + tests", "REMAINING", 3, 5, 9, SR_EFF, "Ships with the Claims UI"),
    fc("FC-SR-GUARDS", "SHARED", "Test/VER", "Frontend guards W-01, W-02, W-04", "REMAINING", 4, 8, 14, SR_EFF, "Source-parsing guards"),
    fc("FC-SR-NAV", "SHARED", "Entegrasyon", "Nav keys 11 × 7 languages + l10n review", "REMAINING", 2, 4, 8, SR_EFF, "Possible overlap with Q33 shared-integration rows (nav) — not netted; see ASSUMPTIONS A7"),
    fc("FC-SR-R01", "SHARED", "Test/VER", "Reconcile-state R-01 Platform fixture test", "REMAINING", 3, 6, 12, SR_EFF, ""),
    fc("FC-SR-R0204", "SHARED", "Test/VER", "Runtime R-02…R-04", "REMAINING", 4, 8, 16, SR_EFF, "Native executor"),
    fc("FC-SR-VER", "SHARED", "Test/VER", "Self-registration independent VER", "REMAINING", 4, 8, 14, SR_EFF, ""),
]
fc_fields = ["id", "module", "category", "deliverable", "state", "optimistic_hours", "most_likely_hours", "pessimistic_hours", "source", "boundary"]
write("FORECAST.tsv", fc_fields, forecast)

# ---------------------------------------------------------------- accepted ledger (portfolio level)
accepted = [
    {"item": "baseline", "delta_most_likely": fmt(ACCEPTED_BASE_ML), "ledger": "frozen+forecast", "evidence": "docs/records/audits/2026-09/mvp6-ct-audit-2026-09-25.md line 54 (CT-accepted share under Rev2 rules)"},
    {"item": "0183-R2-04 root browser regression (A12 VER-02)", "delta_most_likely": "4", "ledger": "frozen+forecast", "evidence": A12},
    {"item": "0185-2 Loads root seam / 3.1.0 publication", "delta_most_likely": "8", "ledger": "frozen+forecast", "evidence": LOADS_CT},
    {"item": "FC-SR-PACK self-registration pack/DCP sections", "delta_most_likely": "0", "ledger": "forecast", "evidence": "CT: no product credit for pack text (" + SR_CT + ")"},
]
write("ACCEPTED.tsv", ["item", "delta_most_likely", "ledger", "evidence"], accepted)
accepted_ml = sum(float(a["delta_most_likely"]) for a in accepted)

# ---------------------------------------------------------------- summaries
modules, categories = [], []
for row in rows + forecast:
    if row["module"] not in modules:
        modules.append(row["module"])
    if row["category"] not in categories:
        categories.append(row["category"])

def summary(ledger, name, accepted_total):
    out = []
    for module in modules + ["portfolio"]:
        selected = ledger if module == "portfolio" else [r for r in ledger if r["module"] == module]
        v = totals(selected)
        d, r = v["DELIVERED"][1], v["REMAINING"][1]
        rec = {"scope": module, "delivered_most_likely": fmt(d), "remaining_most_likely": fmt(r),
               "total_most_likely": fmt(d + r), "delivered_index_percent": f"{100 * d / (d + r):.1f}"}
        rec["ct_accepted_most_likely"] = fmt(accepted_total) if module == "portfolio" else "not split per module"
        rec["ct_accepted_percent"] = f"{100 * accepted_total / (d + r):.1f}" if module == "portfolio" else "N/A"
        out.append(rec)
    write(name, ["scope", "delivered_most_likely", "remaining_most_likely", "total_most_likely", "delivered_index_percent", "ct_accepted_most_likely", "ct_accepted_percent"], out)
    return out

base_summary = summary(rows, "MODULE-SUMMARY.tsv", accepted_ml)
fc_ledger = rows + forecast
fc_summary = summary(fc_ledger, "FORECAST-SUMMARY.tsv", accepted_ml)

def category_table(ledger, name):
    out = []
    for module in modules + ["portfolio"]:
        selected = ledger if module == "portfolio" else [r for r in ledger if r["module"] == module]
        rec = {"scope": module}
        for category in categories:
            v = totals([r for r in selected if r["category"] == category])
            d, r = v["DELIVERED"][1], v["REMAINING"][1]
            rec[category] = f"{100 * d / (d + r):.1f}" if d + r else "N/A"
        v = totals(selected)
        rec["Overall"] = f"{100 * v['DELIVERED'][1] / (v['DELIVERED'][1] + v['REMAINING'][1]):.1f}"
        out.append(rec)
    write(name, ["scope"] + categories + ["Overall"], out)

category_table(rows, "MODULE-CATEGORY-PERCENT.tsv")
category_table(fc_ledger, "FORECAST-CATEGORY-PERCENT.tsv")

recalc = []
for ledger_name, ledger in (("frozen_baseline", rows), ("current_forecast", fc_ledger)):
    for scope in modules + ["portfolio"]:
        scoped = ledger if scope == "portfolio" else [r for r in ledger if r["module"] == scope]
        for category in categories + ["TOTAL"]:
            selected = scoped if category == "TOTAL" else [r for r in scoped if r["category"] == category]
            if not selected:
                continue
            v = totals(selected)
            d, r = v["DELIVERED"], v["REMAINING"]
            den = d[1] + r[1]
            recalc.append({"ledger": ledger_name, "scope": scope, "category": category,
                           "delivered_optimistic": fmt(d[0]), "delivered_most_likely": fmt(d[1]), "delivered_pessimistic": fmt(d[2]),
                           "remaining_optimistic": fmt(r[0]), "remaining_most_likely": fmt(r[1]), "remaining_pessimistic": fmt(r[2]),
                           "most_likely_index_percent": f"{100 * d[1] / den:.1f}" if den else "N/A"})
write("RECALCULATION.tsv", list(recalc[0]), recalc)

# ---------------------------------------------------------------- self-checks
assert len(rows) == 106
b = totals(rows)
assert [round(x, 1) for x in b["DELIVERED"]] == [1069.0, 1464.0, 1916.6], b
assert [round(x, 1) for x in b["REMAINING"]] == [822.0, 1378.0, 2571.2], b
assert [round(x + y, 1) for x, y in zip(b["DELIVERED"], b["REMAINING"])] == [1891.0, 2842.0, 4487.8]
f = totals(fc_ledger)
assert round(f["DELIVERED"][1] + f["REMAINING"][1], 1) == 2954.0, f
assert round(sum(float(r["most_likely_hours"]) for r in forecast if r["module"] == "0186" and r["id"].startswith("FC-0186")), 1) == 26
assert round(sum(float(r["most_likely_hours"]) for r in forecast if r["id"].startswith("FC-0187")), 1) == 16
assert [round(sum(float(r[k]) for r in forecast if r["id"].startswith("FC-SR")), 1) for k in KEYS] == [38.0, 70.0, 127.0]
assert accepted_ml == 1140.0 and accepted_ml <= b["DELIVERED"][1]
print("frozen", [round(x, 1) for x in b["DELIVERED"]], [round(x, 1) for x in b["REMAINING"]])
print("forecast", [round(x, 1) for x in f["DELIVERED"]], [round(x, 1) for x in f["REMAINING"]])
