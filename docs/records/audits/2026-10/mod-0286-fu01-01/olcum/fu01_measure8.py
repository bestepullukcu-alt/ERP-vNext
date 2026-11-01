#!/usr/bin/env python3
"""MOD-0286-FU01 SB-14: her anahtar için bir karakterlik bozulma, derlenebilir kalır ve adı geçen öz test biçiminin sonucunu değiştirir.
Desenleri paketten okur, kopya tutmaz; depoya yazmaz. Depo kökünden koşulur."""
import re

PACK = "execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md"
text = open(PACK, encoding="utf-8").read()
RAW = {k: re.search(rf"^{k}\t(.*)$", text, re.M).group(1) for k in ("STRIP", "SCRIPT", "VARIABLE")}


def classify(tag, raw):
    t = re.sub(raw["STRIP"], "", tag, flags=re.S)
    script = re.compile(raw["SCRIPT"], re.I | re.S)
    caps = script.findall(t)
    var = re.compile(raw["VARIABLE"], re.I | re.S).search(script.sub("", t)) is not None
    return caps, var


CASES = (
    ("STRIP", r"@\*.*?\*@", r"@\#.*?\*@", "FU01-15", '@* <script src="~/assets/js/a/x.js"></script> *@'),
    ("SCRIPT", r"@Url\.Content", r"@Urx\.Content", "FU01-13", '<script src="@Url.Content("~/assets/js/a/x.js")"></script>'),
    ("VARIABLE", r"[^>]*?assets/js/|", r"[^>]*?assets/jz/|", "FU01-62", '<script src="../assets/js/a/x.js"></script>'),
    ("VARIABLE", r"(?<!@)@(?!@)", r"(?<!@)#(?!@)", "FU01-19", '<script src="@x"></script>'),
    ("VARIABLE", r"[^>]*?assets/js/|", r"[^>]*?assets/jz/|", "FU01-18 (fark vermez)", '<script src="~/assets/js/@x.js"></script>'),
    ("VARIABLE", r"(?<!@)@(?!@)", r"(?<!@)#(?!@)", "FU01-18 (fark vermez)", '<script src="~/assets/js/@x.js"></script>'),
)
for key, old, new, case, tag in CASES:
    assert RAW[key].count(old) == 1, (key, old)
    mutated = dict(RAW)
    mutated[key] = RAW[key].replace(old, new)
    re.compile(mutated[key])
    print(f"{key}\t{case}\tdoğru={classify(tag, RAW)}\tbozuk={classify(tag, mutated)}")
