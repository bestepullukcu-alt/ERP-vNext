#!/usr/bin/env python3
"""Third measurement: draft at HEAD vs committed baseline; the workflow's path filter against the two trees. Read only."""
import collections, importlib.util, re, sys, time
sys.dont_write_bytecode = True
ROOT = "/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01"
spec = importlib.util.spec_from_file_location("uic", ROOT + "/scripts/uic_changed_pages.py")
uic = importlib.util.module_from_spec(spec); sys.modules["uic"] = uic; spec.loader.exec_module(uic)
t = time.monotonic()
c, out, err = uic.execute(ROOT, "draft")
print("draft exit", c, err, "wall s", round(time.monotonic() - t, 1), "lines", len(out))
hdr = out[0].split("\t")
draft = {tuple(l.split("\t")[:3]): l.split("\t")[3] for l in out[1:]}
base = {(r["page"], r["check"], r["instance"]): r["count"] for r in uic.read_baseline(ROOT)}
only_draft = sorted(k for k in draft if k not in base)
only_base = sorted(k for k in base if k not in draft)
higher = sorted(k for k in draft if k in base and draft[k].isdigit() and base[k].isdigit() and int(draft[k]) > int(base[k]))
lower = sorted(k for k in draft if k in base and draft[k].isdigit() and base[k].isdigit() and int(draft[k]) < int(base[k]))
print("reds outside baseline today (draft only):", len(only_draft))
for k in only_draft[:40]:
    print("   ", k, draft[k])
print("baseline rows not in draft (stale):", len(only_base))
for k in only_base[:15]:
    print("   ", k)
print("count above the row:", higher[:20], "count below the row:", len(lower), lower[:10])
pages22 = {"CRM/CycleCapacities", "CRM/CyclePeriods", "CRM/LegalProfiles", "CRM/SafetyTexts", "DecompositionTreeBuilder", "DeliveryExecutionManagement", "DemandIdeas",
           "EnterpriseStrategyBusinessPerformance", "EnterpriseStrategyBusinessPerformance/Components", "HCM/Employees", "ManagementGovernance", "ManagementGovernance/ProcessModeling",
           "Meetings/MeetingTypes", "Meetings/Series", "Organization/OrganizationUnits", "SupplyChain/DemandPlanning", "Traceability/LotMigration", "Traceability/LotMovements",
           "Traceability/LotTrace", "Traceability/Lots", "Traceability/Serials", "WorkCenterNext"}
print("draft-only reds on the 22 pages:", [k for k in only_draft if k[0] in pages22])
print("rows per owner on the 22 pages (current baseline):")
own = collections.defaultdict(collections.Counter)
for r in uic.read_baseline(ROOT):
    if r["page"] in pages22:
        own[r["page"]][r["owner"]] += 1
for p in sorted(own):
    print("   ", p, dict(own[p]))
print("pages with no baseline row:", sorted(pages22 - set(own)))
