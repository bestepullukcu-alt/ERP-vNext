#!/usr/bin/env python3
"""Fifth measurement for MOD-0286-FU01: the VARIABLE pattern on fixtures and on all views; SB-13 row paths. Read only."""
import importlib.util, re, subprocess, sys
sys.dont_write_bytecode = True
ROOT = "/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01"
spec = importlib.util.spec_from_file_location("uic", ROOT + "/scripts/uic_changed_pages.py")
uic = importlib.util.module_from_spec(spec); sys.modules["uic"] = uic; spec.loader.exec_module(uic)
WEB = uic.WEB
print("HEAD", subprocess.run(["git", "-C", ROOT, "rev-parse", "--short=9", "HEAD"], capture_output=True, text=True).stdout.strip())
run = uic.Run(ROOT, "draft"); tree = run.head
STRIP = re.compile(r"@\*.*?\*@|<!--.*?-->", re.S)
SCRIPT = re.compile(r"<script\b[^>]*?\ssrc\s*=\s*[\"']\s*(?:@Url\.Content\(\s*[\"'])?[^\"']*?(assets/js/[^\"'?]+?\.js)", re.I | re.S)
VARIABLE = re.compile(r"<script\b[^>]*?\ssrc\s*=\s*[\"']?(?!\s*(?:https?:)?//)(?!\s*@Url\.Content\(\s*[\"'])[^\"'>\s]*?(?<!@)@(?!@)", re.I | re.S)
for name, s, want in (("@x", '<script src="@x"></script>', True), ("@Model.Js quoted single", "<script src='@Model.Js'></script>", True),
                      ("@x unquoted", "<script src=@x></script>", True), ("Url.Content var", '<script src="@Url.Content(Model.X)"></script>', True),
                      ("inside path", '<script src="~/assets/js/@x.js"></script>', True), ("Url.Content literal", '<script src="@Url.Content("~/assets/js/a.js")"></script>', False),
                      ("@@ vendor", '<script src="~/assets/vendor/libs/@@a/b.js"></script>', False), ("plain", '<script src="~/assets/js/a.js"></script>', False),
                      ("data-src", '<script data-src="@x"></script>', False), ("no src", '<script>var a = "@x";</script>', False)):
    print("  fixture %-26s match=%-5s want=%s" % (name, VARIABLE.search(s) is not None, want))
views = sorted(p for p in tree.sha if p.startswith(WEB + "Views/") and p.endswith(".cshtml"))
tree.prefetch(views)
hits = [(v[len(WEB):], m.group(0)[:80]) for v in views for m in VARIABLE.finditer(STRIP.sub("", uic.text_of(tree.cache[v])))]
print("VARIABLE over", len(views), "views:", len(hits), hits[:10])
caps = [c for v in views for c in SCRIPT.findall(STRIP.sub("", uic.text_of(tree.cache[v]))) if re.search(r"[@{(]", c)]
print("SCRIPT captures with @ { ( :", len(caps))
for p in ("wwwroot/assets/js/CRM/Accounts/details-contacts.js", "wwwroot/assets/js/Recall/cases.js", "wwwroot/assets/js/shared/diten-tags.js",
          "wwwroot/assets/js/dt-defaults.js", "wwwroot/assets/js/pages/systemStatus.js", "wwwroot/assets/js/pages/governance/permissions.js", "wwwroot/assets/js/pages/decomposition/decompositionPage.js"):
    print("  at HEAD", p, tree.has(WEB + p))
rows = {(r["page"], r["check"], r["instance"]) for r in uic.read_baseline(ROOT)}
for k in (("CRM/Accounts", "CI-7", "wwwroot/assets/js/CRM/Accounts/details-contacts.js"), ("Recall", "CI-7", "wwwroot/assets/js/Recall/cases.js"),
          ("HCM/Employees", "CI-4", "wwwroot/assets/js/CRM/Accounts/index.js"), ("DecompositionTreeBuilder", "CI-4", "wwwroot/assets/js/pages/decomposition/decompositionPage.js")):
    print("  row already in baseline", k, k in rows)

