#!/usr/bin/env python3
"""Second measurement for MOD-0286-FU01: script-tag prefixes, loaders, dangling ref, callers, per-page costs, anchors, order. Read only."""
import collections, importlib.util, json, pathlib, re, shutil, subprocess, sys, tempfile, time
sys.dont_write_bytecode = True
ROOT = "/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01"
SC = "/private/tmp/claude-501/-Users-alitufanoglu-ERP-vNext/05ce4e4b-9875-47e7-8842-ee9372026895/scratchpad/"
spec = importlib.util.spec_from_file_location("uic", ROOT + "/scripts/uic_changed_pages.py")
uic = importlib.util.module_from_spec(spec); sys.modules["uic"] = uic; spec.loader.exec_module(uic)
WEB = uic.WEB
M = json.load(open(SC + "fu01_measure.json", encoding="utf-8"))
run = uic.Run(ROOT, "draft"); tree = run.head
views = sorted(p for p in tree.sha if p.startswith(WEB + "Views/") and p.endswith(".cshtml"))
tree.prefetch(views)
STRIP = re.compile(r"@\*.*?\*@|<!--.*?-->", re.S)
OLD = re.compile(r"<script\b[^>]*?\bsrc\s*=\s*[\"']\s*(?:@Url\.Content\(\s*[\"'])?[^\"']*?(assets/js/[^\"'?]+?\.js)", re.I | re.S)
NEW = re.compile(r"<script\b[^>]*?\ssrc\s*=\s*[\"']\s*(?:@Url\.Content\(\s*[\"'])?[^\"']*?(assets/js/[^\"'?]+?\.js)", re.I | re.S)
PFX = re.compile(r"<script\b[^>]*?\ssrc\s*=\s*[\"']\s*(.{0,30}?)assets/js/", re.I | re.S)
hist = collections.Counter(); old = new = 0; commented = 0; datasrc = []
for v in views:
    raw = uic.text_of(tree.cache[v]); t = STRIP.sub("", raw)
    old += len(OLD.findall(t)); new += len(NEW.findall(t))
    for m in PFX.finditer(t):
        hist[m.group(1)] += 1
    commented += len(NEW.findall(raw)) - len(NEW.findall(t))
    for m in re.finditer(r"data-src\s*=\s*[\"'][^\"']*assets/js/[^\"']*", t):
        datasrc.append((v, m.group(0)))
print("capture counts old vs \\s-anchored", old, new, "commented-out script tags", commented, "data-src", datasrc)
print("prefix histogram", dict(hist))
dv = [v[len(WEB):] for v in views if "strategy-initiatives-api.js" in uic.text_of(tree.cache[v])]
print("dangling ref views", dv)
for v in views:
    raw = uic.text_of(tree.cache[v])
    if "strategy-initiatives-api.js" in raw:
        for i, l in enumerate(raw.split("\n"), 1):
            if "strategy-initiatives-api.js" in l:
                print("   ", v[len(WEB):], i, l.strip()[:150])
comp_call = [v for v in views if re.search(r"Components/_[A-Za-z]+", uic.text_of(tree.cache[v])) and "EnterpriseStrategyBusinessPerformance" in v]
print("ESBP views with text 'Components/_X':", len(comp_call), "outside Components", len([v for v in comp_call if "/Components/" not in v]))
allcall = [v for v in views if re.search(r"Components/_[A-Za-z]+", uic.text_of(tree.cache[v]))]
print("all views with text 'Components/_X':", len(allcall), collections.Counter(v[len(WEB):].split("/")[1] for v in allcall))
shell = [v for v in views if "_SubdomainShell" in uic.text_of(tree.cache[v])]
print("views naming _SubdomainShell:", len(shell), "outside Components:", len([v for v in shell if "/Components/" not in v]))
scr = [v for v in views if re.search(r"Components/_Scripts", uic.text_of(tree.cache[v]))]
print("views naming Components/_Scripts:", len(scr))
# per-page cost of the checks on the 22 pages (today's evaluate_page, own files; adding JS reads are cheap)
pages = sorted({pg for v in M["refs"].values() for pg in v if pg not in ("Shared", "Archive")})
print("pages", len(pages))
tot = 0
for page in pages:
    t = time.monotonic(); r = run.evaluate_page(page); d = time.monotonic() - t; tot += d
    print("   %-52s %.3f s views=%d list=%s" % (page, d, len([p for p in (tree.under if '/' in page else tree.direct)(f"{WEB}Views/{page}/") if p.endswith('.cshtml')]), run.either_has(f"{WEB}Views/{page}/_DataTable.cshtml")))
print("evaluate 22 pages total s", round(tot, 2))
for pp in (["EnterpriseStrategyBusinessPerformance", "EnterpriseStrategyBusinessPerformance/Components", "DeliveryExecutionManagement"],
           ["Traceability/LotMigration", "Traceability/LotMovements", "Traceability/LotTrace", "Traceability/Lots", "Traceability/Serials"]):
    t = time.monotonic()
    for page in pp:
        run.evaluate_page(page)
    print("several-file pages", pp[0], "...", round(time.monotonic() - t, 3), "s")
# anchors
for rel, pat in (("wwwroot/assets/js/pages/enterprise-strategy/modal-form-utils.js", r"\.style\."),
                 ("wwwroot/assets/js/pages/enterprise-strategy/goals.js", r"\.style\."),
                 ("wwwroot/assets/js/Platform/PersonReferences/person-reference-picker.js", r"\balert\(|Swal\.fire|toastr\."),
                 ("wwwroot/assets/js/Traceability/trace-ui.js", r"\balert\(|Swal\.fire|toastr\."),
                 ("wwwroot/assets/js/pages/decomposition/decompositionPage.js", r"\balert\(|Swal\.fire|toastr\.")):
    raw = uic.text_of(tree.read(WEB + rel)); hits = [i for i, l in enumerate(raw.split("\n"), 1) if re.search(pat, l)]
    print("anchor", rel, pat, "n=", sum(1 for _ in re.finditer(pat, raw)), "lines", hits[:6])
for rel in ("wwwroot/assets/js/Platform/PersonReferences/person-reference-picker.js", "wwwroot/assets/js/pages/decomposition/decompositionPage.js", "wwwroot/assets/js/CRM/cycle-dates.js", "wwwroot/assets/js/Meetings/series/index.js"):
    print("refs", rel, M["refs"][rel])
for v in views:
    raw = uic.text_of(tree.cache[v])
    if "decompositionPage.js" in raw or "person-reference-picker.js" in raw:
        for i, l in enumerate(raw.split("\n"), 1):
            if "decompositionPage.js" in l or "person-reference-picker.js" in l:
                print("   tag", v[len(WEB):], i, l.strip()[:160])
# CRM/Accounts regression target
acc = [v for v in views if v.startswith(WEB + "Views/CRM/Accounts/")]
hits = [v for v in acc for m in NEW.finditer(STRIP.sub("", uic.text_of(tree.cache[v]))) if WEB + "wwwroot/" + m.group(1) in {WEB + k for k in M["cls"]}]
print("CRM/Accounts views", len(acc), "referencing class files", len(hits))
# order evidence: a row appended on a scratch copy of the baseline, today's reader
d = tempfile.mkdtemp(prefix="fu01-order-", dir=SC)
(pathlib.Path(d) / "tests").mkdir()
base = (pathlib.Path(ROOT) / uic.BASELINE).read_text(encoding="utf-8")
print("--check-baseline on unchanged copy:")
(pathlib.Path(d) / uic.BASELINE).write_text(base, encoding="utf-8")
c, o, e = uic.execute(d, "check"); print("  exit", c, o[-1] if o else "", e)
row = "\t".join(["Meetings/Series", "CI-4", "wwwroot/assets/js/Meetings/series/index.js", "1", "sahip kararı x (2026-10-31)", "FU-19", "platform-shared-services"]) + "\n"
(pathlib.Path(d) / uic.BASELINE).write_text(base + row, encoding="utf-8")
c, o, e = uic.execute(d, "check"); print("  with one reference row: exit", c, e)
shutil.rmtree(d)
