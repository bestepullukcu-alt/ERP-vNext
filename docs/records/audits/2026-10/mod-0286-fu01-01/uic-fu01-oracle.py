#!/usr/bin/env python3
"""MOD-0286-FU01 AC-7 bağımsız ölçüm — Control Tower yazdı; iş paketi değiştirmez. Seçiciyi çağırmaz, içe aktarmaz.
Yalnız git ağacını, üst paketin "DL-3 satır kontrol desenleri" bloğunu ve bu paketin Owned Objects bölüm 2 STRIP / SCRIPT
satırlarını okur, depoya yazmaz. Depo kökünden koşulur:
  satirlar <commit>   FU-19 satırları: page, check, instance, count (sıralı TSV); özet stderr'e
  ciftler <commit>    (sayfa, dosya) çiftleri (sıralı TSV)
  siniflar <commit>   sınıf dosyaları: yüklenme türü (sayfa | birkac | shared | yok), yol
"""
import re
import subprocess
import sys

PARENT = "execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md"
PACK = "execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md"
WEB = "frontend/Diten.Web/"
JSROOT = "wwwroot/assets/js/"
NOT_PAGE = {"Shared", "Archive"}
JS_KEYS = {"CI-4": "CI-4", "CI-5": "CI-5", "CI-6j": "CI-6", "CI-7": "CI-7", "CI-8": "CI-8", "CI-9p": "CI-9", "CI-10": "CI-10"}


def git(*args):
    return subprocess.run(["git", *args], capture_output=True, check=True).stdout


def show(commit, path):
    return git("show", f"{commit}:{path}").decode("utf-8", errors="replace")


def patterns(commit):
    text = show(commit, PARENT)
    start = text.index("```text\n", text.index("### DL-3 satır kontrol desenleri")) + len("```text\n")
    out = {}
    for line in text[start:text.index("```", start)].split("\n"):
        if line:
            key, raw = line.split("\t", 1)
            out[key] = re.compile(raw)
    return out


def rules(commit):
    text = show(commit, PACK)
    strip = re.search(r"^STRIP\t(.*)$", text, re.M).group(1)
    script = re.search(r"^SCRIPT\t(.*)$", text, re.M).group(1)
    return re.compile(strip, re.S), re.compile(script, re.I | re.S)


def tree(commit):
    names = git("ls-tree", "-r", "--name-only", commit, WEB + "Views", WEB + JSROOT).decode().split("\n")
    return [p[len(WEB):] for p in names if p.startswith(WEB)]


def class_files(rels):
    any_view, direct, subs = set(), set(), {}
    for rel in rels:
        parts = rel.split("/")
        if rel.startswith("Views/") and len(parts) >= 3 and parts[1] not in NOT_PAGE:
            any_view.add(parts[1])
            if len(parts) == 3:
                direct.add(parts[1])
            else:
                subs.setdefault(parts[1], set()).add(parts[2])
    out = []
    for rel in rels:
        if not (rel.startswith(JSROOT) and rel.endswith(".js")):
            continue
        parts = rel[len(JSROOT):].split("/")
        top = parts[0]
        if top == "pages":
            out.append(rel)
        elif top in any_view:
            if len(parts) == 2:
                if top not in direct:
                    out.append(rel)
            elif parts[1] not in subs.get(top, set()):
                out.append(rel)
    return sorted(out)


def page_of(rel):
    parts = rel.split("/")
    if len(parts) < 3 or parts[1] in NOT_PAGE:
        return None
    return parts[1] if len(parts) == 3 else f"{parts[1]}/{parts[2]}"


def references(commit, rels):
    strip, script = rules(commit)
    pages, shared = {}, set()
    for rel in rels:
        if not (rel.startswith("Views/") and rel.endswith(".cshtml")):
            continue
        text = strip.sub("", show(commit, WEB + rel))
        page = page_of(rel)
        for m in script.finditer(text):
            target = "wwwroot/" + m.group(1)
            if page:
                pages.setdefault(target, set()).add(page)
            else:
                shared.add(target)
    return pages, shared


def measure(commit):
    rels = tree(commit)
    classes = class_files(rels)
    pages, shared = references(commit, rels)
    return classes, pages, shared


def siniflar(commit):
    classes, pages, shared = measure(commit)
    for c in classes:
        n = len(pages.get(c, ()))
        kind = "sayfa" if n == 1 else "birkac" if n > 1 else "shared" if c in shared else "yok"
        print(f"{kind}\t{c}")


def ciftler(commit):
    classes, pages, _ = measure(commit)
    for c in classes:
        for p in sorted(pages.get(c, ())):
            print(f"{p}\t{c}")


def satirlar(commit):
    pats = patterns(commit)
    classes, pages, _ = measure(commit)
    rows = []
    for c in classes:
        if c not in pages:
            continue
        text = show(commit, WEB + c)
        for key, check in JS_KEYS.items():
            n = len(list(pats[key].finditer(text)))
            if n:
                rows.extend((p, check, c, str(n)) for p in pages[c])
    for r in sorted(rows):
        print("\t".join(r))
    pairs = sum(len(pages.get(c, ())) for c in classes)
    owners = {p for c in classes for p in pages.get(c, ())}
    print(f"sınıf {len(classes)} çift {pairs} sayfa {len(owners)} satır {len(rows)} eşleşme {sum(int(r[3]) for r in rows)}", file=sys.stderr)


{"satirlar": satirlar, "ciftler": ciftler, "siniflar": siniflar}[sys.argv[1]](sys.argv[2])
