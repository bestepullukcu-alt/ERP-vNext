#!/usr/bin/env python3
"""MVP6 effort update 09 (Q94) — reproduces every numeric output of this folder.

Method of update 08 unchanged. Inputs (read-only, hash-checked): update-08 EFFORT.tsv (frozen 2,842 h ledger, 106 rows),
update-08 FORECAST.tsv, Q78 ESTIMATE.tsv, Q68 ESTIMATE.tsv, Q77a README (A-07). ROOT = env ERP_ROOT (else four levels up).
OUT = env Q94_OUT (else this file's folder).
"""
import csv, hashlib, json, os
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = Path(os.environ["ERP_ROOT"]) if os.environ.get("ERP_ROOT") else HERE.parents[3]
OUT = Path(os.environ["Q94_OUT"]) if os.environ.get("Q94_OUT") else HERE
KEYS = ("optimistic_hours", "most_likely_hours", "pessimistic_hours")
U08 = "docs/roadmap/plans/mvp6-effort-update-08/"
EXPECT = {
    U08 + "EFFORT.tsv": None, U08 + "FORECAST.tsv": None, U08 + "SHA256SUMS": "3928b36d07206d7db2f2d6f8296ce3b9494d5dfb7252b8de57a417396400e5bb",
    "docs/roadmap/plans/mvp6-ui-scope-190-192-01/ESTIMATE.tsv": "03669f33abb67a6be2ebdd1ae4e17fa26a7566d9190dc5d551f4969076a109e3",
    "docs/roadmap/plans/mvp6-loads-uptake-prep-01/ESTIMATE.tsv": None,
    "docs/records/audits/2026-09/mvp6-loads-uptake-draft-01/README.md": None,
}
Q78E = "docs/roadmap/plans/mvp6-ui-scope-190-192-01/ESTIMATE.tsv"
Q78D = "docs/records/decisions/2026-09/mvp6-sop-capacity-ui-scope-owner-decision-01.md"
Q68E = "docs/roadmap/plans/mvp6-loads-uptake-prep-01/ESTIMATE.tsv"
Q68V = "docs/records/audits/2026-09/mvp6-ct-verdicts-q68-q69-q70-2026-09-26.md"
Q77R = "docs/records/audits/2026-09/mvp6-loads-uptake-draft-01/README.md"
Q77V = "docs/records/audits/2026-09/mvp6-ct-verdicts-q64a-q77a-q78-2026-09-26.md"

def sha(p): return hashlib.sha256((ROOT / p).read_bytes()).hexdigest()
for p, h in EXPECT.items():
    if h: assert sha(p) == h, p

def read(p):
    with (ROOT / p).open(newline="", encoding="utf-8") as f: return list(csv.DictReader(f, delimiter="\t"))
def write(name, fields, rows):
    with (OUT / name).open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields, delimiter="\t", lineterminator="\n"); w.writeheader(); w.writerows(rows)
def fmt(v): return f"{round(v, 1):g}"
def totals(rows):
    r = {s: [0.0, 0.0, 0.0] for s in ("DELIVERED", "REMAINING")}
    for x in rows:
        for i, k in enumerate(KEYS): r[x["state"]][i] += float(x[k])
    return r

frozen = read(U08 + "EFFORT.tsv"); fc08 = read(U08 + "FORECAST.tsv")
assert len(frozen) == 106
b = totals(frozen)
assert [round(x, 1) for x in b["DELIVERED"]] == [1069.0, 1464.0, 1916.6] and [round(x, 1) for x in b["REMAINING"]] == [822.0, 1378.0, 2571.2]
assert round(totals(frozen + fc08)["DELIVERED"][1] + totals(frozen + fc08)["REMAINING"][1], 1) == 2954.0
row = {r["id"]: r for r in frozen}

# ---- Q78 option A estimates, checked against the source file
est = {(r["module"].replace("MOD-", ""), r["option"], r["category"]): r for r in read(Q78E)}
def omp(r): return [float(r["O_h"]), float(r["M_h"]), float(r["P_h"])]
def rem(rid): return [float(row[rid][k]) for k in KEYS]
def delta(new, old): return [n - o for n, o in zip(new, old)]
fc9 = []
def fc(fid, module, cat, deliv, state, v, source, boundary):
    fc9.append({"id": fid, "module": module, "category": cat, "deliverable": deliv, "state": state,
                "optimistic_hours": fmt(v[0]), "most_likely_hours": fmt(v[1]), "pessimistic_hours": fmt(v[2]), "source": source, "boundary": boundary})
for mod in ("0190", "0192"):
    p_new, f_new = omp(est[(mod, "A", "Pack/tasarım")]), omp(est[(mod, "A", "Frontend")])
    p_old, f_old = rem(f"{mod}-1-REMAINING"), rem(f"{mod}-4-REMAINING")
    fc(f"FC9-{mod}-1", mod, "Pack/tasarım", f"UI pack revision, Phase 1.5, dispatch closure (delta vs {mod}-1-REMAINING)", "REMAINING", delta(p_new, p_old),
       f"{Q78E}; {Q78D}", f"{'/'.join(fmt(x) for x in p_new)} replaces {'/'.join(fmt(x) for x in p_old)}; pack revision applied (Q81) and VER Q82 PASS, Phase 1.5 owner-approved (Q80), dispatch closure open; no split, so no credit (08 FC-0186-1/FC-0187-1 treatment)")
    fc(f"FC9-{mod}-4", mod, "Frontend", f"Bounded UI option A (delta vs {mod}-4-REMAINING)", "REMAINING", delta(f_new, f_old),
       f"{Q78E}; {Q78D}", f"{'/'.join(fmt(x) for x in f_new)} replaces {'/'.join(fmt(x) for x in f_old)}; draft overlay {'Q84' if mod == '0190' else 'Q88'} earns 0 (not writer-complete)")
# rows 5/6: Q78 integration and VER are "UI sub-items" of the existing reserves -> contained, no delta (ASSUMPTION A5)
for mod in ("0190", "0192"):
    for cat, rid in (("Entegrasyon", f"{mod}-5-REMAINING"), ("Test/VER", f"{mod}-6-REMAINING")):
        assert est[(mod, "A", cat)]["replaces_or_note"].startswith("UI sub-item of")

# ---- Loads producer uptake (Q68, CT ACCEPTED as preparation) + F-1 (Q77a A-07), forecast rows (Q68 README option 1)
e68 = {r["id"]: r for r in read(Q68E)}
def ev(i): return [float(e68[i]["optimistic_h"]), float(e68[i]["most_likely_h"]), float(e68[i]["pessimistic_h"])]
assert ev("TOTAL") == [11.5, 19.0, 35.0]
for i, cat, text in (("EST-01", "Backend", "Uptake writer code: presence-aware root read in queryLoads"),
                     ("EST-02", "Test/VER", "Uptake writer tests, evidence tooling, Loads regression"),
                     ("EST-03", "Test/VER", "Uptake independent runtime VER on Mac (LU-15)"),
                     ("EST-04", "Contract", "Repin evidence verifier to 3.1.0; LoadSummary parity (no contract edit)"),
                     ("EST-05", "Entegrasyon", "Writer isolated-env composition")):
    assert e68[i]["category"] == cat
    fc(f"FC9-0185-UP-{i[-2:]}", "0185", cat, text + f" ({i})", "REMAINING", ev(i), f"{Q68E}; {Q68V}",
       "0185-RS-05 was outside both ledgers (08 UNESTIMATED-SCOPE); Q68 option 1 (new forecast rows); Q77a draft covers writer part, 0 credit")
a07 = (ROOT / Q77R).read_text(encoding="utf-8")
assert "backend 0.5/1/2 h + tests 1/2/3 h = **+1.5/3/5 h**" in a07
fc("FC9-0185-F1-BE", "0185", "Backend", "F-1 fix: transition read through the root materializer (A-07)", "REMAINING", [0.5, 1, 2], f"{Q77R}; {Q77V}", "Agent estimate on top of Q68; owner decision F-1 fix with uptake")
fc("FC9-0185-F1-T", "0185", "Test/VER", "F-1 fix tests (A-07)", "REMAINING", [1, 2, 3], f"{Q77R}; {Q77V}", "Agent estimate on top of Q68")

fields = ["id", "module", "category", "deliverable", "state", "optimistic_hours", "most_likely_hours", "pessimistic_hours", "source", "boundary"]
write("FORECAST-09.tsv", fields, fc9)

# ---- accepted (CT) — portfolio 1140 from 08; per-module split as published on the Progress Board (A3)
ACC = {"0183": 152, "0184": 104, "0185": 144, "0186": 172, "0187": 200, "0190": 148, "0192": 220, "0147": 0, "0148": 0, "SHARED": 0}
assert sum(ACC.values()) == 1140
CREDITS_09 = []  # no line meets the 08 rule (see CREDIT-LINES.tsv)
acc_total = 1140 + sum(c[1] for c in CREDITS_09)

CAT = [("Pack/tasarım", "pack"), ("Contract", "contract"), ("Backend", "backend"), ("Frontend", "frontend"), ("Entegrasyon", "integration"), ("Test/VER", "test")]
MODS = ["0183", "0184", "0185", "0186", "0187", "0190", "0192", "0147", "0148", "SHARED"]
fc_all = frozen + fc08 + fc9

def figures(ledger):
    out = {}
    for m in MODS + ["portfolio"]:
        sel = ledger if m == "portfolio" else [r for r in ledger if r["module"] == m]
        v = totals(sel); d, r = v["DELIVERED"][1], v["REMAINING"][1]
        cats = {}
        for c, key in CAT:
            cv = totals([x for x in sel if x["category"] == c]); cd, cr = cv["DELIVERED"][1], cv["REMAINING"][1]
            cats[key] = round(100 * cd / (cd + cr), 1) if cd + cr else None
        out[m] = {"accepted": acc_total if m == "portfolio" else ACC[m], "delivered": round(d, 1), "remaining": round(r, 1), "total": round(d + r, 1),
                  "categories": cats, "omp_delivered": [round(x, 1) for x in v["DELIVERED"]], "omp_remaining": [round(x, 1) for x in v["REMAINING"]]}
    return out
F = figures(frozen); G = figures(fc_all); G08 = figures(frozen + fc08)

rows = []
for name, fig in (("frozen_baseline", F), ("forecast_08", G08), ("forecast_09", G)):
    for m in MODS + ["portfolio"]:
        x = fig[m]
        rows.append({"ledger": name, "scope": m, "accepted_ml": fmt(x["accepted"]), "delivered_ml": fmt(x["delivered"]), "remaining_ml": fmt(x["remaining"]), "total_ml": fmt(x["total"]),
                     "delivered_pct": f"{100 * x['delivered'] / x['total']:.1f}", "accepted_pct": f"{100 * x['accepted'] / x['total']:.1f}",
                     **{k: ("" if x["categories"][k] is None else f"{x['categories'][k]:.1f}") for _, k in CAT},
                     "delivered_omp": "/".join(fmt(v) for v in x["omp_delivered"]), "remaining_omp": "/".join(fmt(v) for v in x["omp_remaining"])})
write("MODULE-FIGURES.tsv", list(rows[0]), rows)

def num(v): return None if v is None else (int(v) if float(v).is_integer() else v)
def board(fig):
    def one(x): return {k: num(x[k]) for k in ("accepted", "delivered", "remaining", "total")} | {"categories": {k: num(v) for k, v in x["categories"].items()}}
    return {"summary": one(fig["portfolio"]), "modules": {m: one(fig[m]) for m in MODS}}
(OUT / "BOARD-FIGURES.json").write_text(json.dumps(board(F), indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

# ---- self-checks
assert F["portfolio"]["delivered"] == 1464 and F["portfolio"]["total"] == 2842 and F["portfolio"]["accepted"] == 1140
assert G08["portfolio"]["total"] == 2954
for m in MODS: assert abs(F[m]["delivered"] + F[m]["remaining"] - F[m]["total"]) < 1e-9 and F[m]["accepted"] <= F[m]["delivered"]
d = {m: round(G[m]["total"] - G08[m]["total"], 1) for m in MODS}
assert d["0190"] == -12 and d["0192"] == -12 and d["0185"] == 22 and all(v == 0 for k, v in d.items() if k not in ("0190", "0192", "0185"))
assert G["portfolio"]["total"] == 2952 and G["portfolio"]["delivered"] == 1469
print("frozen", F["portfolio"]["omp_delivered"], F["portfolio"]["omp_remaining"])
print("forecast08", G08["portfolio"]["omp_delivered"], G08["portfolio"]["omp_remaining"])
print("forecast09", G["portfolio"]["omp_delivered"], G["portfolio"]["omp_remaining"], "total", G["portfolio"]["total"])
print("deltas", d)
for m in ("0185", "0190", "0192"):
    print(m, "fc08", G08[m]["total"], G08[m]["omp_remaining"], "fc09", G[m]["total"], G[m]["omp_remaining"], G[m]["categories"])
print("portfolio cats frozen", F["portfolio"]["categories"], "fc09", G["portfolio"]["categories"])
