#!/usr/bin/env python3
"""MVP6 effort update 09b (Q138) — reproduces every number in this folder.

Method of 08/09/09a unchanged (Rev2: hours move only at writer complete, independent VER pass or CT decision, and only where the
event closes an existing ledger row or row part with an O/M/P split; 8 h = 1 person-day; figures M unless O/M/P shown).
Step 1 rebuilds 09a from the 08 frozen ledger + the 09a 2:1 split and ASSERTS it equals 09a BOARD-FIGURES.json and the 09a forecast
table. Step 2 applies the 09b transfers and the new fix-plan forecast lines. ROOT = env ERP_ROOT (else four levels up); OUT = env Q138_OUT.
"""
import csv, hashlib, json, os, copy
from pathlib import Path
HERE = Path(__file__).resolve().parent
ROOT = Path(os.environ["ERP_ROOT"]) if os.environ.get("ERP_ROOT") else HERE.parents[3]
OUT = Path(os.environ["Q138_OUT"]) if os.environ.get("Q138_OUT") else HERE
KEYS = ("optimistic_hours", "most_likely_hours", "pessimistic_hours")
P = "docs/roadmap/plans/"
EXPECT = {P + "mvp6-effort-update-08/SHA256SUMS": "3928b36d07206d7db2f2d6f8296ce3b9494d5dfb7252b8de57a417396400e5bb",
          P + "mvp6-effort-update-09/SHA256SUMS": "5718307d9df693c332196308c24625d91d047ab3f47736f4cf92833da99eed1e",
          P + "mvp6-effort-update-09a/SHA256SUMS": "b595619ef555b92f3a1afed8dc01697d60472bc6a28ccb7af798d91e3a3855df",
          P + "mvp6-effort-update-09a/BOARD-FIGURES.json": "54e72eaef88abdf6e838716a476c9a3f875ac48448457ee7f6e1bc3644d4de81",
          "docs/records/audits/2026-09/mvp6-q101-control-audit-01/FINDINGS.tsv": "8c37547d16fe8ea64533386d7c35727f9e0ddeac90f9ab830667343d7708d886",
          "docs/records/audits/2026-09/mvp6-q101b-control-audit-01/FINDINGS.tsv": None}
def sha(p): return hashlib.sha256((ROOT / p).read_bytes()).hexdigest()
for p, h in EXPECT.items():
    if h: assert sha(p) == h, p
def read(p):
    with (ROOT / p).open(newline="", encoding="utf-8") as f: return list(csv.DictReader(f, delimiter="\t"))
def fmt(v): return f"{round(v, 1):g}"
def f3(r): return [float(r[k]) for k in KEYS]
CAT = [("Pack/tasarım", "pack"), ("Contract", "contract"), ("Backend", "backend"), ("Frontend", "frontend"), ("Entegrasyon", "integration"), ("Test/VER", "test")]
MODS = ["0183", "0184", "0185", "0186", "0187", "0190", "0192", "0147", "0148", "SHARED"]

frozen08 = read(P + "mvp6-effort-update-08/EFFORT.tsv"); fc08 = read(P + "mvp6-effort-update-08/FORECAST.tsv"); fc9 = read(P + "mvp6-effort-update-09/FORECAST-09.tsv")
assert len(frozen08) == 106

# ---- Step 1: 09a split (2:1, (a) = round(row*2/3, 1), (b) = row - (a)); (a) credited for 0187/0190/0192, not 0186
def split(rows):
    out = []
    for r in rows:
        if r["id"] in ("0186-1-REMAINING", "0187-1-REMAINING", "0190-1-REMAINING", "0192-1-REMAINING"):
            v = f3(r); a = [round(x * 2 / 3, 1) for x in v]; b = [round(x - y, 1) for x, y in zip(v, a)]
            ra, rb = dict(r), dict(r)
            ra["id"] = r["id"] + "(a)"; rb["id"] = r["id"] + "(b)"
            for k, x, y in zip(KEYS, a, b): ra[k] = fmt(x); rb[k] = fmt(y)
            ra["state"] = "REMAINING" if r["module"] == "0186" else "DELIVERED"
            out += [ra, rb]
        else:
            out.append(dict(r))
    return out
frozen09a = split(frozen08); assert len(frozen09a) == 110
fcrows = [dict(r) for r in fc08 + fc9]
ACC09a = {"0183": 152, "0184": 104, "0185": 144, "0186": 172, "0187": 204, "0190": 153.3, "0192": 225.3, "0147": 0, "0148": 0, "SHARED": 0}

def totals(rows):
    t = {"DELIVERED": [0.0] * 3, "REMAINING": [0.0] * 3}
    for x in rows:
        for i, k in enumerate(KEYS): t[x["state"]][i] += float(x[k])
    return t
def figures(ledger, acc):
    out = {}
    for m in MODS + ["portfolio"]:
        sel = ledger if m == "portfolio" else [r for r in ledger if r["module"] == m]
        v = totals(sel); d, r = v["DELIVERED"][1], v["REMAINING"][1]
        cats = {}
        for c, key in CAT:
            cv = totals([x for x in sel if x["category"] == c]); cd, cr = cv["DELIVERED"][1], cv["REMAINING"][1]
            cats[key] = round(100 * cd / (cd + cr), 1) if abs(cd + cr) > 1e-9 else None
        out[m] = {"accepted": round(sum(acc.values()), 1) if m == "portfolio" else acc[m], "delivered": round(d, 1), "remaining": round(r, 1),
                  "total": round(d + r, 1), "categories": cats, "omp_delivered": [round(x, 1) for x in v["DELIVERED"]], "omp_remaining": [round(x, 1) for x in v["REMAINING"]]}
    return out
def num(v): return None if v is None else (int(v) if float(v).is_integer() else v)
def board(fig):
    def one(x): return {k: num(x[k]) for k in ("accepted", "delivered", "remaining", "total")} | {"categories": {k: num(v) for k, v in x["categories"].items()}}
    return {"summary": one(fig["portfolio"]), "modules": {m: one(fig[m]) for m in MODS}}

F9a = figures(frozen09a, ACC09a); G9a = figures(frozen09a + fcrows, ACC09a)
assert board(F9a) == json.loads((ROOT / P / "mvp6-effort-update-09a/BOARD-FIGURES.json").read_text()), "09a frozen not reproduced"
assert G9a["portfolio"]["total"] == 2952 and G9a["portfolio"]["delivered"] == 1483.6 and G9a["portfolio"]["accepted"] == 1154.6
assert (G9a["0187"]["delivered"], G9a["0187"]["total"], G9a["0186"]["total"], G9a["SHARED"]["delivered"], G9a["SHARED"]["total"]) == (204, 335, 309, 113, 319)
assert F9a["portfolio"]["omp_delivered"] == [1077.8, 1478.6, 1940.0]

# ---- Step 2: 09b transfers (row id -> view(s)); accepted moves only where a CT decision ACCEPTS the work (A2)
TRANSFERS = [  # (line, row id, ledger 'frozen' or 'forecast', accepted?)
    ("CL9b-01", "0186-1-REMAINING(a)", "frozen", True),
    ("CL9b-02", "0187-1-REMAINING(b)", "frozen", False), ("CL9b-02", "FC-0187-1", "forecast", False),
    ("CL9b-03", "0187-4-REMAINING", "frozen", False), ("CL9b-03", "FC-0187-4", "forecast", False),
    ("CL9b-04", "FC-SR-0187", "forecast", False),
]
frozen09b = copy.deepcopy(frozen09a); fc09b = copy.deepcopy(fcrows)
ACC09b = dict(ACC09a); moved = []
for line, rid, view, acc in TRANSFERS:
    pool = frozen09b if view == "frozen" else fc09b
    hit = [r for r in pool if r["id"] == rid]; assert len(hit) == 1, rid
    r = hit[0]; assert r["state"] == "REMAINING", rid
    r["state"] = "DELIVERED"; v = f3(r); moved.append((line, rid, view, v, acc, r["module"], r["category"]))
    if acc: ACC09b[r["module"]] = round(ACC09b[r["module"]] + v[1], 1)

# ---- New forecast lines: Q101 fix plan F01…F16 (80/145/244), closed only if Q101b (CT ACCEPTED audit, D-6) says CLOSED
q101 = {r["id"].replace("Q101-", ""): r for r in read("docs/records/audits/2026-09/mvp6-q101-control-audit-01/FINDINGS.tsv")}
q101b = {r["id"]: r for r in read("docs/records/audits/2026-09/mvp6-q101b-control-audit-01/FINDINGS.tsv") if r["id"].startswith("F")}
MAP = {  # module = the single module the finding names, else SHARED; category = the artefact the fix changes (A5)
 "F01": ("SHARED", "Entegrasyon"), "F02": ("0185", "Test/VER"), "F03": ("0185", "Backend"), "F04": ("SHARED", "Test/VER"),
 "F05": ("SHARED", "Pack/tasarım"), "F06": ("SHARED", "Frontend"), "F07": ("SHARED", "Entegrasyon"), "F08": ("0185", "Backend"),
 "F09": ("SHARED", "Backend"), "F10": ("SHARED", "Test/VER"), "F11": ("SHARED", "Test/VER"), "F12": ("SHARED", "Test/VER"),
 "F13": ("SHARED", "Frontend"), "F14": ("SHARED", "Entegrasyon"), "F15": ("SHARED", "Test/VER"), "F16": ("SHARED", "Pack/tasarım")}
fp = []
for fid in sorted(MAP):
    o, m, p_ = (float(x) for x in q101[fid]["est_omp_h"].split("/")); st = q101b[fid]["status"]
    mod, cat = MAP[fid]
    fp.append({"id": "FP-" + fid, "module": mod, "category": cat, "deliverable": q101[fid]["item"], "state": "DELIVERED" if st == "CLOSED" else "REMAINING",
               "optimistic_hours": f"{o:g}", "most_likely_hours": f"{m:g}", "pessimistic_hours": f"{p_:g}", "q101b_status": st, "severity": q101[fid]["severity"]})
assert [round(sum(float(r[k]) for r in fp), 2) for k in KEYS] == [80.0, 145.0, 244.0]
fc09b += fp

F9b = figures(frozen09b, ACC09b); G9b = figures(frozen09b + fc09b, ACC09b)

# ---- checks
for fig, tot in ((F9b, 2842), (G9b, 3097)):
    assert fig["portfolio"]["total"] == tot
    assert round(sum(fig[m]["total"] for m in MODS), 1) == tot and round(sum(fig[m]["delivered"] for m in MODS), 1) == fig["portfolio"]["delivered"]
    for m in MODS: assert fig[m]["accepted"] <= fig[m]["delivered"] + 1e-9, m
    assert fig["portfolio"]["delivered"] >= fig["portfolio"]["accepted"]
assert round(sum(float(r["most_likely_hours"]) for r in frozen09b), 1) == 2842 and round(sum(float(r["most_likely_hours"]) for r in frozen09b + fc09b), 1) == 3097
for m in MODS: assert F9b[m]["total"] == F9a[m]["total"]  # frozen denominator unchanged

# ---- outputs
(OUT / "BOARD-FIGURES.json").write_text(json.dumps(board(F9b), indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
with (OUT / "FIX-PLAN-LINES.tsv").open("w", newline="", encoding="utf-8") as f:
    w = csv.DictWriter(f, fieldnames=list(fp[0]), delimiter="\t", lineterminator="\n"); w.writeheader(); w.writerows(fp)
rows = []
for name, a, b in (("frozen", F9a, F9b), ("forecast", G9a, G9b)):
    for m in MODS + ["portfolio"]:
        x, y = a[m], b[m]
        rows.append({"view": name, "scope": m, "total_09a": fmt(x["total"]), "total_09b": fmt(y["total"]),
                     "delivered_09a": fmt(x["delivered"]), "delivered_09b": fmt(y["delivered"]), "delivered_delta": fmt(y["delivered"] - x["delivered"]),
                     "accepted_09a": fmt(x["accepted"]), "accepted_09b": fmt(y["accepted"]), "accepted_delta": fmt(y["accepted"] - x["accepted"]),
                     "remaining_09a": fmt(x["remaining"]), "remaining_09b": fmt(y["remaining"]),
                     "delivered_pct_09a": f"{100 * x['delivered'] / x['total']:.1f}", "delivered_pct_09b": f"{100 * y['delivered'] / y['total']:.1f}",
                     "accepted_pct_09a": f"{100 * x['accepted'] / x['total']:.1f}", "accepted_pct_09b": f"{100 * y['accepted'] / y['total']:.1f}",
                     **{f"{k}_09a": ("" if x["categories"][k] is None else f"{x['categories'][k]:.1f}") for _, k in CAT},
                     **{f"{k}_09b": ("" if y["categories"][k] is None else f"{y['categories'][k]:.1f}") for _, k in CAT},
                     "delivered_omp_09b": "/".join(fmt(v) for v in y["omp_delivered"]), "remaining_omp_09b": "/".join(fmt(v) for v in y["omp_remaining"])})
with (OUT / "MODULE-FIGURES.tsv").open("w", newline="", encoding="utf-8") as f:
    w = csv.DictWriter(f, fieldnames=list(rows[0]), delimiter="\t", lineterminator="\n"); w.writeheader(); w.writerows(rows)
for m in moved: print("moved", m)
print("frozen 09a", F9a["portfolio"]["omp_delivered"], "09b", F9b["portfolio"]["omp_delivered"], F9b["portfolio"]["omp_remaining"])
print("forecast 09a", G9a["portfolio"]["omp_delivered"], "09b", G9b["portfolio"]["omp_delivered"], G9b["portfolio"]["omp_remaining"])
print("OK sums: frozen 2842, forecast 3097; per-module totals = overall; delivered >= accepted")
