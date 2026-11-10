#!/usr/bin/env python3
"""Fourth measurement for MOD-0286-FU01: own-folder class pairs (F-1), Archive/root-only loads (F-9), uncaptured src forms (F-9),
and the exact refusal text today's reader gives for a sample of rows. Read only."""
import collections, importlib.util, json, re, subprocess, sys
sys.dont_write_bytecode = True
ROOT = "/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01"
SC = "/private/tmp/claude-501/-Users-alitufanoglu-ERP-vNext/05ce4e4b-9875-47e7-8842-ee9372026895/scratchpad/"
spec = importlib.util.spec_from_file_location("uic", ROOT + "/scripts/uic_changed_pages.py")
uic = importlib.util.module_from_spec(spec); sys.modules["uic"] = uic; spec.loader.exec_module(uic)
WEB = uic.WEB
print("HEAD", subprocess.run(["git", "-C", ROOT, "rev-parse", "--short=9", "HEAD"], capture_output=True, text=True).stdout.strip())
run = uic.Run(ROOT, "draft"); tree = run.head
M = json.load(open(SC + "fu01_measure.json", encoding="utf-8"))
views = sorted(p for p in tree.sha if p.startswith(WEB + "Views/") and p.endswith(".cshtml"))
tree.prefetch(views)
# F-1: class-file (file, page) pairs that lie under js/{page}/ as a prefix
under = collections.Counter(); deep = []
for rel, pages in M["refs"].items():
    for pg in pages:
        if pg in ("Shared", "Archive", "ROOT"):
            continue
        pre = f"wwwroot/assets/js/{pg}/"
        if rel.startswith(pre):
            under[pg] += 1
            deep.append((pg, rel, "/" in rel[len(pre):]))
print("class pairs with path under js/{page}/:", dict(under), "all deeper than direct:", all(d[2] for d in deep), "two-level pages among them:", [d for d in deep if "/" in d[0]])
# F-9: class files loaded only from Archive or a root view
STRIP = re.compile(r"@\*.*?\*@|<!--.*?-->", re.S)
SCRIPT = re.compile(r"<script\b[^>]*?\ssrc\s*=\s*[\"']\s*(?:@Url\.Content\(\s*[\"'])?[^\"']*?(assets/js/[^\"'?]+?\.js)", re.I | re.S)
ANY_SRC = re.compile(r"<script\b[^>]*?\ssrc\s*=\s*([\"'])(.*?)\1", re.I | re.S)
holders = collections.defaultdict(set)
form = collections.Counter(); odd = []
for v in views:
    parts = v[len(WEB):].split("/")
    kind = "Archive" if parts[1] == "Archive" else "Shared" if parts[1] == "Shared" else "ROOT" if len(parts) == 2 else "page"
    t = STRIP.sub("", uic.text_of(tree.cache[v]))
    for m in SCRIPT.finditer(t):
        holders[WEB + "wwwroot/" + m.group(1)].add(kind)
    for m in ANY_SRC.finditer(t):
        val = m.group(2)
        if "assets/js/" in val:
            form["assets/js"] += 1
        elif val.startswith(("http://", "https://", "//")):
            form["external"] += 1
        elif "@" in val:
            form["razor"] += 1; odd.append((v[len(WEB):], val[:70]))
        else:
            form["other"] += 1
    for m in re.finditer(r"<script\b[^>]*?\ssrc\s*=\s*(?![\"'])", t, re.I | re.S):
        form["unquoted"] += 1; odd.append((v[len(WEB):], t[m.start():m.end() + 40]))
cls = [WEB + k for k in M["cls"]]
print("class files by holder kinds:", collections.Counter(tuple(sorted(holders.get(p, ()))) for p in cls))
print("Archive-only or root-only class files:", [p[len(WEB):] for p in cls if holders.get(p) and holders[p] <= {"Archive", "ROOT"}])
print("script src forms:", dict(form))
for x in odd[:25]:
    print("   ", x)
any_attr = [(v[len(WEB):], m.group(0)[:80]) for v in views for m in re.finditer(r"<script\b[^>]*?\ssrc\s*=\s*[\"']?@[^\"'>\s]*", STRIP.sub("", uic.text_of(tree.cache[v])), re.I | re.S) if "assets/js/" not in m.group(0)]
print("src opening with @ and no assets/js/ in the value head:", len(any_attr))
for x in any_attr[:25]:
    print("   ", x)
# refusal texts of today's reader on the row shapes: which row reaches which text
for name, row in (("FU-14 outside", ("HCM/Employees", "CI-7", "wwwroot/assets/js/Platform/PersonReferences/person-reference-picker.js", "1")),
                  ("missing cell", ("Home", "CI-7", "wwwroot/assets/js/pages/systemStatus.js", ""))):
    print(name, uic.instance_problem(*row))
