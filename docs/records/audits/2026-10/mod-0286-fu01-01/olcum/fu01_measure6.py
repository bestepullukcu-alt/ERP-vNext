#!/usr/bin/env python3
"""Sixth measurement for MOD-0286-FU01: tightened SCRIPT and VARIABLE on fixtures and over all views; pairs compared with the first run. Read only."""
import collections, importlib.util, json, re, subprocess, sys
sys.dont_write_bytecode = True
ROOT = "/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01"
SC = "/private/tmp/claude-501/-Users-alitufanoglu-ERP-vNext/05ce4e4b-9875-47e7-8842-ee9372026895/scratchpad/"
spec = importlib.util.spec_from_file_location("uic", ROOT + "/scripts/uic_changed_pages.py")
uic = importlib.util.module_from_spec(spec); sys.modules["uic"] = uic; spec.loader.exec_module(uic)
WEB = uic.WEB
print("HEAD", subprocess.run(["git", "-C", ROOT, "rev-parse", "--short=9", "HEAD"], capture_output=True, text=True).stdout.strip())
pack = open(ROOT + "/execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md", encoding="utf-8").read()
STRIP = re.compile(r"@\*.*?\*@|<!--.*?-->", re.S)
SCRIPT_S = r"""<script\b[^>]*?\ssrc\s*=\s*["']\s*(?:@Url\.Content\(\s*["'])?(?:~?/)?(assets/js/[^"'?#@{(\s]+?\.js)(?=["'?#])"""
VARIABLE_S = r"""<script\b[^>]*?\ssrc\s*=\s*(?!["']?\s*(?:https?:)?//)(?:[^>]*?assets/js/|(?!["']\s*@Url\.Content\(\s*["'][^"']*["']\s*\)\s*["'])["']?[^"'>\s]*?(?<!@)@(?!@))"""
SCRIPT, VARIABLE = re.compile(SCRIPT_S, re.I | re.S), re.compile(VARIABLE_S, re.I | re.S)
print("pack holds SCRIPT string:", SCRIPT_S in pack, "VARIABLE string:", VARIABLE_S in pack)
def classify(s):
    t = STRIP.sub("", s)
    caps = SCRIPT.findall(t)
    rest = SCRIPT.sub("", t)
    return caps, VARIABLE.search(rest) is not None
FIX = [
    ('plain', '<script src="~/assets/js/a/x.js"></script>', ["assets/js/a/x.js"], False),
    ('leading slash', '<script src="/assets/js/a/x.js"></script>', ["assets/js/a/x.js"], False),
    ('asp-append', '<script src="~/assets/js/a/x.js" asp-append-version="true"></script>', ["assets/js/a/x.js"], False),
    ('Url.Content literal', '<script src="@Url.Content("~/assets/js/a/x.js")"></script>', ["assets/js/a/x.js"], False),
    ('?v=1', '<script src="~/assets/js/a/x.js?v=1"></script>', ["assets/js/a/x.js"], False),
    ('?v=@ver', '<script src="~/assets/js/a/x.js?v=@ver"></script>', ["assets/js/a/x.js"], False),
    ('x.min.js', '<script src="~/assets/js/a/x.min.js"></script>', ["assets/js/a/x.min.js"], False),
    ('@x', '<script src="@x"></script>', [], True),
    ("'@Model.Js'", "<script src='@Model.Js'></script>", [], True),
    ('=@x', '<script src=@x></script>', [], True),
    ('Url.Content var', '<script src="@Url.Content(Model.X)"></script>', [], True),
    ('@ in path', '<script src="~/assets/js/@x.js"></script>', [], True),
    ('Url.Content concat', '<script src="@Url.Content("~/assets/js/" + name + ".js")"></script>', [], True),
    ('unquoted', '<script src=~/assets/js/a/x.js></script>', [], True),
    ('../', '<script src="../assets/js/a/x.js"></script>', [], True),
    ('data.json', '<script src="~/assets/js/a/data.json"></script>', [], True),
    ('https cdn', '<script src="https://cdn.x/assets/js/lib.js"></script>', [], False),
    ('// cdn', '<script src="//cdn.x/assets/js/lib.js"></script>', [], False),
    ('cdn version', '<script src="https://cdn.x/npm/xlsx@0.18/a.js"></script>', [], False),
    ('@@ vendor', '<script src="~/assets/vendor/libs/@@a/b.js"></script>', [], False),
    ('vendor Url.Content', '<script src="@Url.Content("~/assets/vendor/a.js")"></script>', [], False),
    ('vendor plain', '<script src="~/assets/vendor/a.js" asp-append-version="true"></script>', [], False),
    ('data-src', '<script data-src="@x"></script>', [], False),
    ('inline', '<script>var a = "@x"; var b = "assets/js/z.js";</script>', [], False),
    ('commented', '@* <script src="~/assets/js/a/x.js"></script> *@', [], False),
]
bad = 0
for name, s, wc, wv in FIX:
    c, v = classify(s)
    ok = c == wc and v == wv
    bad += not ok
    print("  %-20s caps=%-28s var=%-5s %s" % (name, c, v, "ok" if ok else "WRONG"))
print("fixtures wrong:", bad, "of", len(FIX))
run = uic.Run(ROOT, "draft"); tree = run.head
views = sorted(p for p in tree.sha if p.startswith(WEB + "Views/") and p.endswith(".cshtml"))
tree.prefetch(views)
def page_of(v):
    parts = v[len(WEB):].split("/")
    if parts[1] in uic.NOT_PAGE_AREAS:
        return parts[1]
    if len(parts) >= 4:
        return "/".join(parts[1:3])
    return parts[1] if len(parts) == 3 else "ROOT"
n = 0; pairs = set(); hits = []; holders = collections.defaultdict(set)
for v in views:
    t = STRIP.sub("", uic.text_of(tree.cache[v]))
    for c in SCRIPT.findall(t):
        n += 1
        holders[WEB + "wwwroot/" + c].add(page_of(v))
    for m in VARIABLE.finditer(SCRIPT.sub("", t)):
        hits.append((v[len(WEB):], m.group(0)[:90]))
print("views", len(views), "captures", n, "VARIABLE after SCRIPT removed", len(hits), hits[:15])
M = json.load(open(SC + "fu01_measure.json", encoding="utf-8"))
old = {(k, p) for k, v in M["refs"].items() for p in v if p not in ("Shared", "Archive", "ROOT")}
cls = [WEB + k for k in M["cls"]]
new = {(p[len(WEB):], g) for p in cls for g in holders.get(p, ()) if g not in ("Shared", "Archive", "ROOT")}
print("class files still class:", all(run.map_path(p)[0] == "fu18" for p in cls), "pairs old", len(old), "new", len(new), "same", old == new)
print("classes", collections.Counter(("none" if not holders.get(p) else "shared-only" if holders[p] <= {"Shared"} else "page") for p in cls))
