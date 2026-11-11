#!/usr/bin/env python3
"""Re-measure for MOD-0286-FU01 at HEAD: the selector's own FU-18 class, the reference map, debt, costs. Read only."""
import collections, importlib.util, json, re, subprocess, sys, time
sys.dont_write_bytecode = True
ROOT = "/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01"
OUT = "/private/tmp/claude-501/-Users-alitufanoglu-ERP-vNext/05ce4e4b-9875-47e7-8842-ee9372026895/scratchpad/fu01_measure.json"
spec = importlib.util.spec_from_file_location("uic", ROOT + "/scripts/uic_changed_pages.py")
uic = importlib.util.module_from_spec(spec); sys.modules["uic"] = uic; spec.loader.exec_module(uic)
WEB = uic.WEB
print("HEAD", subprocess.run(["git", "-C", ROOT, "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip())
t = time.monotonic(); run = uic.Run(ROOT, "draft"); print("Run init (tree listing, HEAD only) s", round(time.monotonic() - t, 2))
tree = run.head
all_js = sorted(p for p in tree.sha if p.startswith(WEB + "wwwroot/assets/js/"))
kinds = collections.Counter(); fu18 = []
for p in all_js:
    k, v = run.map_path(p); kinds[k] += 1
    if k == "fu18":
        fu18.append(p)
print("map_path kinds under js tree", dict(kinds), "fu18 .js", sum(p.endswith('.js') for p in fu18), "fu18 non-js", [p for p in fu18 if not p.endswith('.js')])
def grp(p):
    r = p[len(WEB + "wwwroot/assets/js/"):].split("/")
    if r[0] == "pages":
        return "pages"
    d = f"{WEB}Views/{r[0]}/"
    return "deeper" if tree.has_direct(d) else "area"
print("groups", collections.Counter(grp(p) for p in fu18))
views = sorted(p for p in tree.sha if p.startswith(WEB + "Views/") and p.endswith(".cshtml"))
print("views", len(views), "cshtml outside Views", [p for p in tree.sha if p.endswith('.cshtml') and not p.startswith(WEB + 'Views/')][:5])
t = time.monotonic(); tree.prefetch(views); print("prefetch all views s", round(time.monotonic() - t, 3), "bytes", sum(len(tree.cache[v]) for v in views))
STRIP = re.compile(r"@\*.*?\*@|<!--.*?-->", re.S)
SCRIPT = re.compile(r"<script\b[^>]*?\ssrc\s*=\s*[\"']\s*(?:@Url\.Content\(\s*[\"'])?[^\"']*?(assets/js/[^\"'?]+?\.js)", re.I | re.S)
def page_of(v):
    parts = v[len(WEB):].split("/")
    if parts[1] in uic.NOT_PAGE_AREAS:
        return parts[1]
    if len(parts) >= 4:
        return "/".join(parts[1:3])
    if len(parts) == 3:
        return parts[1]
    return "ROOT"
t = time.monotonic()
refs = collections.defaultdict(lambda: collections.defaultdict(set)); var = []
literal = 0; literal_in_tag = 0
for v in views:
    txt = STRIP.sub("", uic.text_of(tree.cache[v]))
    literal += len(re.findall(r"assets/js/", txt))
    for m in SCRIPT.finditer(txt):
        literal_in_tag += 1
        cap = m.group(1)
        if "@" in cap or "{" in cap:
            var.append((v, cap)); continue
        refs[WEB + "wwwroot/" + cap][page_of(v)].add(v)
print("strip+regex over all views s", round(time.monotonic() - t, 3))
print("literal assets/js/ after strip", literal, "captured by script rule", literal_in_tag, "variable captures", var)
leftover = []
for v in views:
    t2 = SCRIPT.sub("", STRIP.sub("", uic.text_of(tree.cache[v])))
    for m in re.finditer(r".{0,60}assets/js/.{0,50}", t2):
        leftover.append((v[len(WEB):], m.group(0).strip()))
print("uncaptured literal assets/js after strip:", len(leftover))
for x in leftover[:40]:
    print("   ", x)
dang = [p for p in refs if p not in tree.sha]
print("dangling script refs (not at HEAD, exact case):", len(dang))
for p in dang[:30]:
    print("   ", p[len(WEB):], {k: len(s) for k, s in refs[p].items()})
dclass = collections.Counter(run.map_path(p)[0] for p in dang)
print("dangling by class", dict(dclass))
cls = {}
for p in fu18:
    pg = set(refs.get(p, {})); real = pg - {"Shared", "Archive"}
    cls[p] = ("none" if not pg else "shared-only" if not real else "one" if len(real) == 1 and "Shared" not in pg else "one+shared" if len(real) == 1 else "several")
print("class total", collections.Counter(cls.values()))
print("class by group", {g: dict(collections.Counter(cls[p] for p in fu18 if grp(p) == g)) for g in ("pages", "deeper", "area")})
for p in fu18:
    if cls[p] != "one":
        print("  ", cls[p], p[len(WEB + 'wwwroot/assets/js/'):], {k: sorted(x[len(WEB + 'Views/'):] for x in s)[:6] for k, s in refs.get(p, {}).items()})
pairs = [(p, pg) for p in fu18 for pg in refs.get(p, {}) if pg not in ("Shared", "Archive")]
print("(file,page) pairs", len(pairs), "mapped files", len({p for p, _ in pairs}), "pages", len({g for _, g in pairs}))
print("pages by pairs", dict(collections.Counter(g for _, g in pairs).most_common(40)))
partial_only = [p for p in fu18 if cls[p] in ("one", "several") and all(v.rsplit('/', 1)[-1].startswith('_') for pg, s in refs[p].items() if pg not in ('Shared', 'Archive') for v in s)]
print("mapped files referenced only from a partial (_*.cshtml):", len(partial_only))
for p in partial_only:
    print("   ", p[len(WEB + 'wwwroot/assets/js/'):], {k: sorted(x[len(WEB + 'Views/'):] for x in s)[:3] for k, s in refs[p].items()})
comp = [v for v in views if re.search(r"EnterpriseStrategyBusinessPerformance/Components/", uic.text_of(tree.cache[v]))]
print("views naming ESBP/Components/ path", len(comp), "; outside Components:", len([v for v in comp if '/Components/' not in v]))
print("ESBP/Components files", len([v for v in views if '/EnterpriseStrategyBusinessPerformance/Components/' in v]), "pairs on it", len([1 for _, g in pairs if g == 'EnterpriseStrategyBusinessPerformance/Components']))
pat = uic.load_patterns(ROOT)
JS_KEYS = [("CI-4", "CI-4"), ("CI-5", "CI-5"), ("CI-6j", "CI-6"), ("CI-7", "CI-7"), ("CI-8", "CI-8"), ("CI-9p", "CI-9"), ("CI-10", "CI-10")]
tree.prefetch(fu18)
rows = []; filerows = []
for p in fu18:
    txt = uic.text_of(tree.read(p))
    for k, c in JS_KEYS:
        n = sum(1 for _ in pat[k].finditer(txt))
        if n:
            filerows.append((p[len(WEB):], c, n, cls[p]))
            for pg in sorted(refs.get(p, {})):
                if pg not in ("Shared", "Archive"):
                    rows.append((pg, c, p[len(WEB):], n))
print("file+check rows", len(filerows), "matches", sum(x[2] for x in filerows), "files", len({x[0] for x in filerows}))
print("by check file rows", {c: (sum(1 for x in filerows if x[1] == c), sum(x[2] for x in filerows if x[1] == c)) for _, c in JS_KEYS})
print("debt file rows on unmapped files", [x for x in filerows if x[3] in ('none', 'shared-only')])
print("page-keyed rows", len(rows), "matches", sum(r[3] for r in rows))
print("by check page rows", {c: (sum(1 for x in rows if x[1] == c), sum(x[3] for x in rows if x[1] == c)) for _, c in JS_KEYS})
print("by page", dict(collections.Counter(r[0] for r in rows)))
for r in sorted(rows):
    print("   ", r)
bl = uic.read_baseline(ROOT); print("baseline rows", len(bl))
keys = {(r['page'], r['check'], r['instance']) for r in bl}
fu18set = set(fu18)
print("rows already present", sum((r[0], r[1], r[2]) in keys for r in rows), "baseline rows naming a class file", sum(WEB + r['instance'].split(':')[0] in fu18set for r in bl))
print("pct on baseline %.2f" % (len(rows) / len(bl) * 100))
print("instance_problem today on a sample:", rows and uic.instance_problem(rows[0][0], rows[0][1], rows[0][2], str(rows[0][3])))
json.dump({"rows": rows, "cls": {p[len(WEB):]: c for p, c in cls.items()}, "refs": {p[len(WEB):]: {k: sorted(s) for k, s in v.items()} for p, v in refs.items() if p in fu18set}}, open(OUT, "w"), ensure_ascii=False, indent=0)
