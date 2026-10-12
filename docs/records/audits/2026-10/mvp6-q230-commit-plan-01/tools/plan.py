#!/usr/bin/env python3
"""Q230 planner — READ-ONLY on the repo. Assigns every file behind the 665 porcelain entries to one commit
group, an EXCLUDE class or a HOLD class, by explicit rules. Writes only into argv[2]."""
import sys, os, re, json, csv
from collections import defaultdict, Counter, OrderedDict
SP, OUT = sys.argv[1], sys.argv[2]; os.chdir("/Users/natig/Projects/ERP-vNext-recovery")
d = json.load(open(SP + "/data.json")); ent = d["ent"]; files = d["files"]; info = d["info"]
OLD = "docs/roadmap/plans/mvp6-commit-plan-01/pathspec/"
old = {}
for f in sorted(os.listdir(OLD)):
    for p in open(OLD + f).read().splitlines():
        if p.strip(): old[p.strip()] = f[:3]
written = {}
for l in open("docs/records/audits/2026-10/mvp6-q202a-integration-write-01/WRITTEN-FILES.tsv").read().splitlines()[1:]:
    c = l.split("\t")
    if len(c) > 8 and c[8].startswith("OK"): written[c[1]] = c[2]
f02 = {x[0] for x in d["f02"]}
H1 = set(open(OLD + "H01-held-secret-review.txt").read().split())
D3 = ["mvp6-carrier-numericdate-rework-01/candidate-source.tar.gz", "mvp6-mod0186-http-composition-01/source-snapshot.tar.gz", "mvp6-mod0190-http-dev-01-r1/integration-source.tar.gz", "mvp6-shipment-carrier-predecessor-pin-01/carrier-predecessor-source.tar.gz", "mvp6-shipment-integration-rework-prep-01/BC-SOURCE-CLOSURE.tar.gz", "mvp6-shipment-pod-ui-exec-01/shipment-ui-source.tar.gz"]
D3 = {"docs/records/audits/2026-09/" + x for x in D3}
UC01A = {l.split("\t")[0] for l in open("docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/Q131-APPLICATION-STATE.tsv").read().splitlines()[1:] if "(a)" in l.split("\t")[5]}
A9, A10, DEC = "docs/records/audits/2026-09/", "docs/records/audits/2026-10/", "docs/records/decisions/"
QDEC = "docs/records/decisions/2026-09/mvp6-owner-decisions-q19-q15-q03b-q03c-q21-2026-09-27.md:53 (OD-Q03b)"
D16 = "docs/records/decisions/2026-09/mvp6-q101-fix-decisions-owner-decision-01.md"
# commit id -> (title scope)
def fam(folder):
    n = folder.lower()
    if re.match(r"mvp6-(q\d|ct-verdict|base-stack|ct-)", n): return "R7"
    if re.search(r"returns|claims|mod0186|mod0187|mod-0186|mod-0187", n): return "R4"
    if re.search(r"sandop|capacity|mod0190|mod0192|mod-0190|mod-0192|bc-successor", n): return "R5"
    if re.search(r"carrier|mod0184|mod-0184", n): return "R2"
    if re.search(r"loads|mod0185|mod-0185", n): return "R3"
    if re.search(r"shipment|a12|root|pod|mod0183|mod-0183", n): return "R1"
    return "R6"
OLDMAP = {"C01": "C01", "C02": "S1", "C03": "S2", "C04": "S3", "C05": "K1", "C06": "D1", "C07": "R1", "C08": "R2", "C09": "R3", "C10": "R4", "C11": "R5", "C12": "R6", "C13": "L1", "C14": "T1", "H01": "HOLD-H1"}
def assign(p, st):
    b = os.path.basename(p)
    # 1. exclusions (OD-Q03b: X1/X2 + TestResults)
    if p == "rework-results.json": return "EXCLUDE-X1", "stray model-result JSON at the repo root; " + QDEC
    if "/TestResults/" in p and not p.startswith("docs/"): return "EXCLUDE-X1", "test run output outside an evidence folder (D4: " + D16 + ":17); NOT git-ignored; " + QDEC
    if p == ".claude/settings.local.json": return "EXCLUDE-X2", "machine-local permission settings (tracked file, local-only change); " + QDEC
    if p.startswith(".claude/settings.local.json.bak"): return "EXCLUDE-X2", "backup copy of machine-local settings; same class as X2; NOT git-ignored"
    # 2. holds
    if p in H1: return "HOLD-H1", "secret-review hold (bearer/JWT or secret assignment) until the owner confirms; " + QDEC
    if p in f02: return "HOLD-F02", "archive contains the pre-Q117 Loads probe file(s) that carry the exposed signing-secret literal (Q101-F02); member sha256 matched by Q230; 'a secret must never enter a commit' (decision pack Q147 :71)"
    if p in D3: return "HOLD-D3", "one of the 6 unrecorded archives held from commit (D3: " + D16 + ":16)"
    if re.match(r"docs/records/audits/2026-09/[^/]+/overlay/", p): return "HOLD-D6", "file under an open overlay/ folder, held from commit (D6: " + D16 + ":19)"
    if p.startswith("docs/roadmap/plans/mvp6-process-pilot-01/"): return "G1", "pilot ledger folder (CT-QUEUE, MILESTONE-EVENTS and the other ledgers); other lanes append to it"
    if p in (".antigravity/agents/frontend-ui-ux.md", ".antigravity/workflows/test.md"): return "A1", "Q144 pre-Mac UI checklist patch (CT-QUEUE.tsv:198; VER Q145, CT-QUEUE.tsv:218)"
    if p in (".antigravity/workflows/dispatch-wp.md", "docs/guides/operations/control-tower-sop.md"): return "A1", "SOP v2.5 r2 + dispatch-wp.md applied by Q167 (CT-QUEUE.tsv:229, DONE CT ACCEPTED)"
    if p in UC01A: return "S9", "Platform test file byte-equal to the Q131 postimage, state (a) (mvp6-q201-integration-dryrun-01/Q131-APPLICATION-STATE.tsv)"
    # 3. earlier accepted prep plan (Q69) membership
    if p in old and old[p] != "H01": return OLDMAP[old[p]], "same commit family as in the Q69 prep plan (" + OLD + old[p] + "…)"
    # 4. Q202a-written files, by area
    if p in written:
        w = "written by Q202a (" + written[p] + "); WRITTEN-FILES.tsv"
        if p.startswith("services/Diten.SupplyChainService/"):
            if re.search(r"/(Returns|returns)/", p): return "S5", w
            if re.search(r"/(Claims|claims)/", p): return "S6", w
            if "/SandopPlans/" in p: return "S7", w
            if "/CapacityPlans/" in p: return "S8", w
            if re.search(r"/(Carriers|carriers)/", p): return "S1", w
            if re.search(r"/(Loads|loads)/", p): return "S2", w
            return "S3", w
        if p.startswith("services/"): return "S9", w
        if p.startswith(("frontend/", "gateway/")): return "S10", w
        if p.startswith("execution/"): return "K1", w
        return "S11", w
    # 5. remaining areas
    if p.startswith("execution/"): return "K1", "module pack / capability pack edited in the working tree"
    if p.startswith(DEC): return "D1", "owner decision record"
    if p.startswith(A9): return fam(p[len(A9):].split("/")[0]), "record folder, family by folder name (same keyword rule as the Q69 plan; Q-series and CT verdicts → R7)"
    if p.startswith(A10): return "R8", "record of 2026-10"
    if p.startswith("docs/roadmap/plans/mvp6-process-pilot-01/"): return "G1", "pilot ledger folder"
    if p.startswith(("docs/roadmap/", "docs/guides/")): return "L1", "plan / backlog / guide"
    if p.startswith("scripts/evidence-kit/"): return "T1", "evidence kit"
    if p.startswith("docs/analysis/contracts/") or p in ("docs/reference/architecture/docs-path-authority.json", "tests/architecture/TenantArchitecture.ArchitectureTests/DocsPathGuardTests.cs", ".antigravity/rules/docs-organization.md"): return "C01", "contract / docs-path guard authority"
    if p.startswith("services/Diten.SupplyChainService/"):
        if re.search(r"/(Carriers|carriers)/", p): return "S1", "SupplyChain source in the tree before Q202a (Carrier)"
        if re.search(r"/(Loads|loads)/", p): return "S2", "SupplyChain source in the tree before Q202a (Loads)"
        return "S3", "SupplyChain source in the tree before Q202a"
    return "UNCLASSIFIED", "no rule and no record found that places this path"

rows = []; st_of = {}
for s_, p in ent:
    for f in files[p]: st_of[f] = s_.strip() or "M"
for e, fl in files.items():
    for f in fl:
        g, why = assign(f, st_of.get(f)); rows.append([f, st_of.get(f, "??"), g, why, e])
# ---- chunk large record/plan groups by folder so each commit stays reviewable as a name list
def folder_key(p):
    parts = p.split("/")
    if p.startswith(("docs/records/audits/", )): return "/".join(parts[:5]) if len(parts) > 5 else "/".join(parts[:4]) + "/(files at month level)"
    if p.startswith("docs/roadmap/plans/"): return "/".join(parts[:4]) if len(parts) > 4 else "docs/roadmap/plans/(single files)"
    return "/".join(parts[:3])
LIMIT = 450
grp = defaultdict(list)
for r in rows: grp[r[2]].append(r)
for g in [k for k in grp if k[0] in "RL" and len(grp[k]) > LIMIT]:
    fold = OrderedDict()
    for r in sorted(grp[g], key=lambda r: r[0]): fold.setdefault(folder_key(r[0]), []).append(r)
    chunk, n, idx = [], 0, 0
    def flush():
        global idx
        for r in chunk: r[2] = g + "abcdefghijklmnop"[idx]
        idx += 1
    for k, fl in fold.items():
        if chunk and n + len(fl) > LIMIT: flush(); chunk, n = [], 0
        chunk += fl; n += len(fl)
    if chunk: flush()
by = defaultdict(list)
for r in rows: by[r[2]].append(r[0])
TITLE = {
 "C01": ("docs(scm)", "publish MVP6 contracts and annexes with the docs-path guard authority", "Contracts (2 changed YAMLs, 8 annexes), docs-path-authority.json, DocsPathGuardTests.cs, the docs-organization.md candidate rule and the sealed inputs the authority pins by SHA-256. Same file set as C01 of the Q69 prep plan.", "Q69 (plan), Q62/Q72-Q76 publication WPs as recorded in the folders"),
 "S1": ("feat(supply-chain)", "add MOD-0184 Carrier bounded source", "Carrier feature source, tests and probes as they stand in the working tree.", "MOD-0184 WPs; Q202a for files it wrote"),
 "S2": ("feat(supply-chain)", "add MOD-0185 Loads bounded source", "Loads feature source, tests and probes. The three Loads probes are the Q117 versions (no static secret).", "MOD-0185 WPs; Q117; Q202a"),
 "S3": ("feat(supply-chain)", "add MOD-0183 shipment root read, module registration and service composition", "Shipment root read, ModuleRegistration, Program.cs (Carrier + Load composition only), .csproj, appsettings, shared test setup.", "MOD-0183 WPs; Q202a; Program.cs is the held pre-integration composition (Q209 open)"),
 "S5": ("feat(supply-chain)", "add MOD-0186 Returns bounded core (not composed)", "Returns source, tests and probes. Not registered in Program.cs.", "Q202a; verified by Q210"),
 "S6": ("feat(supply-chain)", "add MOD-0187 Claims bounded core (not composed)", "Claims source, tests and probes. Not registered in Program.cs.", "Q202a; verified by Q211"),
 "S7": ("feat(supply-chain)", "add MOD-0190 S&OP bounded core (not composed)", "S&OP source and tests (the 38 owned paths). Not registered in Program.cs.", "Q202a; verified by Q212"),
 "S8": ("feat(supply-chain)", "add MOD-0192 Capacity bounded core (not composed)", "Capacity source and tests. Not registered in Program.cs.", "Q202a; verified by Q213"),
 "S9": ("feat(platform)", "add service-to-service scope resolution and lane-configurable test Mongo URIs", "Auth, MDM, Platform, HCM, TEP and CRM service files from the Auth-22, A12-360, Q117, Q121 and Q131 layers.", "Q202a (BASE L2/L3, Q117, Q121, Q131)"),
 "S10": ("feat(web)", "add Supply Chain shipment UI, shared resources and gateway route tests", "frontend/ and gateway test files written by Q202a. ocelot.json itself is not in this commit (held seam).", "Q202a"),
 "S11": ("chore(repo)", "apply BASE-STACK v2 root files: AGENTS.md, scripts, test-env helper", "AGENTS.md, smoke/watch scripts, scripts/test-env/mvp6-test-mongo-env.sh.", "Q202a; Q131"),
 "A1": ("docs(governance)", "apply SOP v2.5 r2, dispatch-wp workflow and the pre-Mac UI checklist patch", ".antigravity/ agent and workflow edits plus the SOP text.", "Q144/Q145, Q167"),
 "K1": ("docs(scm)", "update supply-chain module packs, DCP-009 and CRM packs to the applied state", "Module packs MOD-0183…0192, DCP-009 and the commercial-suite packs written by Q202a.", "pack-apply WPs; Q202a"),
 "D1": ("docs(records)", "add MVP6 owner decision records (2026-09)", "Owner decision records.", "as named in each file"),
 "G1": ("docs(pilot)", "add MVP6 process-pilot ledgers and lane prompts", "CT-QUEUE.tsv, MILESTONE-EVENTS.tsv and the other pilot ledger files. Commit LAST: other lanes append to these files.", "all pilot WPs"),
 "T1": ("chore(tooling)", "install MVP6 evidence kit v1.2", "scripts/evidence-kit/, the kit guide and its install records. Same set as C14 of the Q69 plan.", "Q24a"),
}
FAM = {"R1": "Shipment / MOD-0183", "R2": "Carrier / MOD-0184", "R3": "Loads / MOD-0185", "R4": "Returns / MOD-0186 and Claims / MOD-0187", "R5": "S&OP / Capacity (MOD-0190/0192) and BC successor", "R6": "cross-module, publication/guard and integration", "R7": "Q-series work packages, CT verdicts and BASE-STACK (2026-09)", "R8": "work packages and CT verdicts (2026-10)", "L1": "plans, backlog and guides"}
def meta(g):
    if g in TITLE: return TITLE[g]
    base = g[:2]; wps = sorted({m.group(1).upper() for f in by[g] for m in [re.search(r"mvp6-(q\d+[a-z]?)-", f)] if m}, key=lambda x: (int(re.sub(r"\D", "", x)), x))
    folders = sorted({folder_key(f).split("/")[-1] for f in by[g]})
    scope = "docs(records)" if base[0] == "R" else "docs(plans)"
    part = (" (part " + g[2:] + ")") if len(g) > 2 else ""
    return (scope, "add MVP6 " + ("records: " if base[0] == "R" else "") + FAM[base] + part, f"{len(folders)} folders/items, first '{folders[0]}', last '{folders[-1]}'.", (", ".join(wps) if wps else "as named in each folder"))
ORDER = ["C01", "S1", "S2", "S3", "S5", "S6", "S7", "S8", "S9", "S10", "S11", "A1", "K1", "D1"] + sorted([g for g in by if g[0] == "R"], key=lambda g: (g[:2], g)) + sorted([g for g in by if g[0] == "L"]) + ["T1", "G1"]
ORDER = [g for g in ORDER if g in by]
def burden(fl):
    t = [f for f in fl if not info[f]["binary"]]; b = [f for f in fl if info[f]["binary"]]
    add = sum(info[f]["add"] if "add" in info[f] else info[f]["lines"] for f in t); rem = sum(info[f].get("rem", 0) for f in t)
    return dict(files=len(fl), text=len(t), binary=len(b), add=add, rem=rem, mb=sum(info[f]["bytes"] for f in fl) / 1e6, big=sum(1 for f in fl if info[f]["bytes"] > 1048576), mod=sum(1 for f in fl if st_of[f] == "M"))
def w(name, hdr, rws):
    with open(os.path.join(OUT, name), "w", newline="", encoding="utf-8") as fh:
        cw = csv.writer(fh, delimiter="\t", lineterminator="\n"); cw.writerow(hdr); cw.writerows(rws)
for g in ORDER: open(os.path.join(OUT, "pathspec", g + ".txt"), "w").write("\n".join(sorted(by[g])) + "\n")
# entry classification: one group per porcelain entry (majority), exceptions named
ec = []
for s_, p in ent:
    c = Counter(r[2] for r in rows if r[4] == p); main = c.most_common(1)[0][0]
    committable = [k for k in c if not k.startswith(("EXCLUDE", "HOLD", "UNCLASSIFIED"))]
    if committable: main = max(committable, key=lambda k: c[k])
    exc = "; ".join(f"{k}: {v}" for k, v in sorted(c.items()) if k != main)
    why = next(r[3] for r in rows if r[4] == p and r[2] == main)
    ec.append([p, s_.strip() or "M", len(files[p]), main, why, exc or "-"])
w("ENTRY-CLASSIFICATION.tsv", ["porcelain_path", "status", "files_behind_entry", "group", "reason", "files_of_this_entry_in_other_groups"], ec)
w("FILE-ASSIGNMENT.tsv", ["file", "status", "group", "reason", "porcelain_entry"], sorted(rows))
mn = [[r[0], r[2], info[r[0]]["bytes"], "no — appears in git status, so git add of its folder would stage it", r[3]] for r in sorted(rows, key=lambda r: (r[2], r[0])) if r[2].startswith(("EXCLUDE", "HOLD"))]
w("MUST-NOT-COMMIT.tsv", ["path", "class", "bytes", "git_ignored", "reason_and_record"], mn)
w("UNCLASSIFIED.tsv", ["path", "what_is_needed_to_decide"], [[r[0], r[3]] for r in rows if r[2] == "UNCLASSIFIED"])
B = {g: burden(by[g]) for g in by}
json.dump({"order": ORDER, "burden": B, "meta": {g: meta(g) for g in ORDER}, "counts": dict(Counter(r[2] for r in rows))}, open(SP + "/plan.json", "w"), indent=1)
# ---- COMMIT-PLAN command blocks
cp = []
for i, g in enumerate(ORDER, 1):
    sc, subj, body, wps = meta(g); b = B[g]
    cp.append(f"### {i}. `{g}` — {sc}: {subj}\n\n{b['files']} files · {b['mb']:.1f} MB · {b['binary']} binary · {b['big']} over 1 MB · {b['mod']} modified tracked, {b['files']-b['mod']} new\n\n```bash\nGIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/{g}.txt\n```\n\n```bash\ngit diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/{g}.txt)\n```\n\n```bash\ngit diff --cached --stat | tail -1\n```\n\n```bash\ngit diff --cached\n```\n\nOnly after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):\n\n```bash\ngit commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/{g}.txt\n```\n\nMessage (`messages/{g}.txt`):\n\n```text\n{sc}: {subj}\n\n{body}\nWork packages: {wps}.\n{b['files']} files from pathspec/{g}.txt (Q230 commit plan).\n```\n")
    os.makedirs(os.path.join(OUT, "messages"), exist_ok=True)
    open(os.path.join(OUT, "messages", g + ".txt"), "w").write(f"{sc}: {subj}\n\n{body}\nWork packages: {wps}.\n{b['files']} files from pathspec/{g}.txt (Q230 commit plan).\n")
open(os.path.join(OUT, "COMMIT-BLOCKS.md"), "w").write("\n".join(cp))
bt = ["| # | Commit | Files | Modified / new | Text lines to read (+ / −) | Binary files | MB | > 1 MB |", "|---:|---|---:|---|---:|---:|---:|---:|"]
T = Counter()
for i, g in enumerate(ORDER, 1):
    b = B[g]; bt.append(f"| {i} | `{g}` | {b['files']} | {b['mod']} / {b['files']-b['mod']} | +{b['add']:,} / −{b['rem']:,} | {b['binary']} | {b['mb']:.1f} | {b['big']} |")
    for k in ("files", "add", "rem", "binary", "big"): T[k] += b[k]
    T["mb"] += b["mb"]
bt.append(f"| | **Total committable** | **{T['files']}** | | **+{T['add']:,} / −{T['rem']:,}** | **{T['binary']}** | **{T['mb']:.1f}** | **{T['big']}** |")
open(os.path.join(OUT, "BURDEN-TABLE.md"), "w").write("\n".join(bt) + "\n")
print("\n".join(bt)); print(dict(Counter(r[2] for r in rows if r[2].startswith(("EXCLUDE", "HOLD", "UNCL")))))
print("entries", len(ec), Counter(e[3][:2] for e in ec).most_common(40))
