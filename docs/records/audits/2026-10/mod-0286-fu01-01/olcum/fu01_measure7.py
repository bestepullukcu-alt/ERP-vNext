#!/usr/bin/env python3
"""FU01-60..66 measured against the pack's own part 2 block (read from the pack file, no copy). Read only."""
import re
PACK = "/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md"
text = open(PACK, encoding="utf-8").read()
raw = {k: re.search(rf"^{k}\t(.*)$", text, re.M).group(1) for k in ("STRIP", "SCRIPT", "VARIABLE")}
STRIP, SCRIPT, VARIABLE = re.compile(raw["STRIP"], re.S), re.compile(raw["SCRIPT"], re.I | re.S), re.compile(raw["VARIABLE"], re.I | re.S)
def classify(tag):
    t = STRIP.sub("", tag)
    return SCRIPT.findall(t), VARIABLE.search(SCRIPT.sub("", t)) is not None
for name, tag in (("FU01-60", '<script src="/assets/js/a/x.js"></script>'),
                  ("FU01-61", '<script src="@Url.Content(Model.X)"></script>'),
                  ("FU01-62", '<script src="../assets/js/a/x.js"></script>'),
                  ("FU01-63", '<script src="http://cdn.x/assets/js/lib.js"></script>'),
                  ("FU01-64", '<script src="//cdn.x/assets/js/lib.js"></script>'),
                  ("FU01-65 review form", '<script src="~/assets/js/@@x.js"></script>'),
                  ("FU01-65 @@x.js", '<script src="@@x.js"></script>'),
                  ("FU01-65 vendor @@", '<script src="~/assets/vendor/libs/@@a/b.js"></script>'),
                  ("FU01-66", '<script src="@Url.Content("~/assets/vendor/a.js")"></script>')):
    caps, var = classify(tag)
    print(f"{name:22} captures={caps} variable={var}   {tag}")
