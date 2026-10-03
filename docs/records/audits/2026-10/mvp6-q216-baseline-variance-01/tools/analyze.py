#!/usr/bin/env python3
"""Q216 analysis: reads the N .trx files and run logs; prints/writes per-test variance and run totals.
usage: analyze.py <scratch> [<outdir>]"""
import sys, os, glob, csv, re, json, xml.etree.ElementTree as ET
from collections import defaultdict, Counter
S = sys.argv[1]; OUT = sys.argv[2] if len(sys.argv) > 2 else None
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
runs = sorted(glob.glob(S + "/results/run*/run*.trx"))
per = defaultdict(dict); msg = defaultdict(Counter); dur = defaultdict(dict); totals = []
meta = {l.split("\t")[0]: l.rstrip("\n").split("\t") for l in open(S + "/runs.tsv").read().splitlines()[1:]}
for f in runs:
    tag = os.path.basename(f)[:-4]; r = ET.parse(f).getroot(); c = Counter()
    for u in r.findall(".//t:UnitTestResult", ns):
        n = u.get("testName").replace("Diten.SupplyChainService.Tests.", ""); o = u.get("outcome")
        if tag in per[n]: n = n + " #2 (same display name, second theory row)"
        per[n][tag] = o; c[o] += 1; dur[n][tag] = u.get("duration")
        if o != "Passed":
            m = u.find(".//t:Message", ns); st = u.find(".//t:StackTrace", ns)
            where = ""
            if st is not None and st.text:
                mm = re.search(r"in (/[^:]+):line (\d+)", st.text); where = (" @ " + mm.group(1).split("Diten.SupplyChainService.Tests/")[-1] + ":" + mm.group(2)) if mm else ""
            msg[n][(" ".join((m.text or "").split())[:240] if m is not None else "(no message)") + where] += 1
    cnt = r.find(".//t:Counters", ns).attrib
    log = open(f"{S}/logs/{tag}.log", errors="replace").read()
    hang = re.findall(r"(?i)(was aborted|hang dump|test host process crashed)", log)
    totals.append([tag, meta.get(tag, [""] * 9)[1], meta.get(tag, [""] * 9)[2], meta.get(tag, [""] * 9)[3], cnt.get("total"), cnt.get("passed"), cnt.get("failed"), str(int(cnt.get("total")) - int(cnt.get("executed", cnt.get("total")))), "yes" if hang else "no", meta.get(tag, [""] * 9)[5], meta.get(tag, [""] * 9)[4], ",".join(sorted(n for n in per if per[n].get(tag) not in ("Passed", None)))])
tags = [os.path.basename(f)[:-4] for f in runs]
var = []
for n in sorted(per):
    o = [per[n].get(t, "MISSING") for t in tags]
    if any(x != "Passed" for x in o):
        var.append([n, sum(x == "Passed" for x in o), sum(x == "Failed" for x in o), sum(x in ("Timeout", "Aborted", "MISSING", "NotExecuted") for x in o), " ".join("P" if x == "Passed" else "F" if x == "Failed" else "T" for x in o), " || ".join(f"{k} (x{v})" for k, v in msg[n].most_common(3)), " ".join((dur[n].get(t) or "-")[3:10] for t in tags)])
print("runs", len(runs), "tests", len(per))
for t in totals: print("\t".join(t))
for v in var: print("\t".join(map(str, v)))
if OUT:
    def w(name, hdr, rows):
        with open(os.path.join(OUT, name), "w", newline="") as fh:
            cw = csv.writer(fh, delimiter="\t", lineterminator="\n"); cw.writerow(hdr); cw.writerows(rows)
    w("RUN-TOTALS.tsv", ["run", "start_local", "end_local", "wall_seconds", "total", "passed", "failed", "not_executed", "hang_or_abort_seen", "run_capped", "dotnet_exit", "non_green_tests"], totals)
    w("VARIANCE.tsv", ["test", "passes", "failures", "timeouts", "per_run_outcome_1_to_N", "assertion_when_failed", "per_run_duration_mm:ss.f"], var)
    json.dump({n: per[n] for n in per}, open(os.path.join(OUT, "evidence", "per-test-outcomes.json"), "w"), indent=0, sort_keys=True)
