#!/usr/bin/env python3
"""Q216: join server-global failCommand episodes (episodes.tsv) to the five tests that arm one, by time window.
usage: join.py <scratch> <out.tsv>"""
import sys, glob, os, csv, xml.etree.ElementTree as ET
from datetime import datetime, timedelta
from collections import defaultdict, Counter
S, OUT = sys.argv[1:3]
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
P = lambda s: datetime.strptime(s[:23], "%Y-%m-%dT%H:%M:%S.%f")
CAND = {"Loads.LoadAtomicityTests.UnknownCommitResultRetriesAndCommitsExactlyOnce": "times:1 / 91",
        "Loads.LoadAtomicityTests.UnresolvedUnknownCommitReturns503WithoutPretendingRollbackOrSuccess": "times:3 / 91",
        "Returns.ReturnAtomicityTests.UnknownCommit_Unresolved503_AllOrNoneThenSameKeyRecovery": "times:5 / 91",
        "SandopPlans.SandopAtomicityTests.Unknown_commit_without_receipt_returns_unresolved_then_exact_key_recovers": "times:3 / 91",
        "SandopPlans.SandopAtomicityTests.Committed_write_concern_uncertainty_resolves_original_receipt": "times:1 / writeConcernError 91"}
tests = defaultdict(list)
for f in sorted(glob.glob(S + "/results/run*/run*.trx")):
    tag = os.path.basename(f)[:-4]
    for u in ET.parse(f).getroot().findall(".//t:UnitTestResult", ns):
        n = u.get("testName").replace("Diten.SupplyChainService.Tests.", "")
        if n in CAND:
            h, m, s = u.get("duration").split(":"); d = int(h) * 3600 + int(m) * 60 + float(s); e = P(u.get("endTime"))
            tests[tag].append((e - timedelta(seconds=d), e, n, d, u.get("outcome")))
eps = list(csv.reader(open(S + "/episodes.tsv"), delimiter="\t")); out = []
byrun = defaultdict(list)
for r in eps[1:]:
    if "NONE" in r[5]: byrun[r[0]].append(r)
for tag in sorted(byrun):
    E = sorted(byrun[tag], key=lambda r: r[1]); T = sorted(tests[tag])          # both in time order: one episode per arming test
    assert len(E) == len(T) == 5, (tag, len(E), len(T))
    for r, (a, b, n, d, o) in zip(E, T):
        arm = P("2026-10-02T" + r[1]); assert a - timedelta(seconds=1) <= arm <= b + timedelta(seconds=1), (tag, n, r[1])
        out.append([r[0], n, CAND.get(n, ""), r[4], r[6], r[7], r[8], "%.1f" % d, o])
w = csv.writer(open(OUT, "w", newline=""), delimiter="\t", lineterminator="\n")
w.writerow(["run", "test", "armed_as", "errorCode_in_log", "times_fired", "first_hit_local", "seconds_first_hit_to_off", "test_seconds", "outcome"]); w.writerows(out)
agg = defaultdict(list)
for r in out: agg[r[1]].append(r)
for k, v in sorted(agg.items()):
    print(k.split(".")[-1][:60], "| n=%d" % len(v), "| fired", dict(Counter(x[4] for x in v)), "| outcome", dict(Counter(x[8] for x in v)), "| secs", " ".join(x[7] for x in v))
