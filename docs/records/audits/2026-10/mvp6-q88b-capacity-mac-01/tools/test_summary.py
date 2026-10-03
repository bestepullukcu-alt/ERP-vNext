#!/usr/bin/env python3
"""Q84b: TEST-SUMMARY.tsv from the TRX files (tree A = BASE-STACK v2 baseline, tree B = + Capacity overlay v3 + L6 env).
Per suite: passed/failed/skipped/total for A and B; failures in B not failing in A = NEW (by fully qualified test name)."""
import pathlib, sys, xml.etree.ElementTree as ET
L = pathlib.Path.home() / "mvp6-env/q88b/logs/test"
NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
def load(trx):
    # every UnitTestResult row counts (theory cases can share a display name); failure names compared as a set
    r = ET.parse(trx).getroot()
    return [(u.get("testName"), u.get("outcome")) for u in r.iter(NS + "UnitTestResult")]
rows = ["suite\ttree\ttotal\tpassed\tfailed\tskipped\tnew_failures_vs_A\tnew_failure_names\tfixed_vs_A\ttrx"]
detail = []
for suite in ("web-tests", "supplychain-tests"):
    got = {t: load(L / f"{t}--{suite}.trx") for t in ("treeA", "treeB")}
    for t in ("treeA", "treeB"):
        r = got[t]; c = lambda o: sum(1 for _, v in r if v == o)
        failed = {k for k, v in r if v == "Failed"}
        a_failed = {k for k, v in got["treeA"] if v == "Failed"}
        new = sorted(failed - a_failed) if t == "treeB" else []
        fixed = sorted(a_failed - failed) if t == "treeB" else []
        skipped = len(r) - c("Passed") - c("Failed")
        rows.append(f"{suite}\t{t}\t{len(r)}\t{c('Passed')}\t{c('Failed')}\t{skipped}\t{len(new) if t == 'treeB' else '-'}\t{';'.join(new) or '-'}\t{len(fixed) if t == 'treeB' else '-'}\tlogs/test/{t}--{suite}.trx")
        for k in sorted(failed): detail.append(f"{suite}\t{t}\tFAILED\t{k}\t{'NEW (not failing on A)' if t == 'treeB' and k not in a_failed else ('also fails on A' if t == 'treeB' else 'baseline')}")
    only_b = sorted({k for k, _ in got["treeB"]} - {k for k, _ in got["treeA"]})
    detail.append(f"{suite}\ttreeB\tADDED-TESTS\t{len(only_b)}\ttests present in B only (overlay tests)")
out = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else L / "TEST-SUMMARY.tsv")
out.write_text("\n".join(rows) + "\n\n# failures and added tests\nsuite\ttree\tkind\ttest\tnote\n" + "\n".join(detail) + "\n")
print(out.read_text())
