#!/usr/bin/env python3
"""MOD-0286-FU01 bağımsız ölçüm, sürüm 02 — Control Tower yazdı; iş paketi değiştirmez. Seçiciyi çağırmaz, içe aktarmaz.
Yalnız git ağacını, üst paketin "DL-3 satır kontrol desenleri" bloğunu, bu paketin Owned Objects bölüm 2 kod bloğunu ve
tests/ui-compliance-baseline.tsv dosyasını okur, depoya yazmaz. Depo kökünden koşulur:
  satirlar <commit>        FU-19 satırları: page, check, instance, count (sıralı TSV); özet stderr'e
  ciftler <commit>         (sayfa, dosya) çiftleri (sıralı TSV)
  siniflar <commit>        sınıf dosyaları: yüklenme türü (sayfa | birkac | shared | yok), yol
  gorunum <commit> [page]  görünüm sayısı: sayfa verilirse o sayfanın, verilmezse Views altındaki tüm .cshtml
  degisken <commit>        VARIABLE eşleşen görünümler (sayfa ya da -, görünüm); özet stderr'e
  turetim <commit>         sabotaj tablosunun türetilen değerleri (TSV: SB, page, check, instance, n, d); sayılar stderr'e
  secici <commit>          seçicinin FU01_REF_PATTERNS sabiti paket bloğuyla bayt bayt aynı mı (ast ile okur, içe aktarmaz)
"""
import ast
import re
import subprocess
import sys

PARENT = "execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md"
PACK = "execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md"
SELECTOR = "scripts/uic_changed_pages.py"
BASELINE = "tests/ui-compliance-baseline.tsv"
WEB = "frontend/Diten.Web/"
JSROOT = "wwwroot/assets/js/"
E = "wwwroot/assets/js/pages/enterprise-strategy/"
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


def raw_rules(commit):
    text = show(commit, PACK)
    return {k: re.search(rf"^{k}\t(.*)$", text, re.M).group(1) for k in ("STRIP", "SCRIPT", "VARIABLE")}


def rules(commit):
    raw = raw_rules(commit)
    return re.compile(raw["STRIP"], re.S), re.compile(raw["SCRIPT"], re.I | re.S), re.compile(raw["VARIABLE"], re.I | re.S)


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


def views(rels):
    return [r for r in rels if r.startswith("Views/") and r.endswith(".cshtml")]


def references(commit, rels):
    strip, script, _ = rules(commit)
    pages, shared = {}, set()
    for rel in views(rels):
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


def rows_of(commit, pats, classes, pages):
    rows = []
    for c in classes:
        if c not in pages:
            continue
        text = show(commit, WEB + c)
        for key, check in JS_KEYS.items():
            n = len(list(pats[key].finditer(text)))
            if n:
                rows.extend((p, check, c, str(n)) for p in pages[c])
    return sorted(rows)


def satirlar(commit):
    pats = patterns(commit)
    classes, pages, _ = measure(commit)
    rows = rows_of(commit, pats, classes, pages)
    for r in rows:
        print("\t".join(r))
    pairs = sum(len(pages.get(c, ())) for c in classes)
    owners = {p for c in classes for p in pages.get(c, ())}
    print(f"sınıf {len(classes)} çift {pairs} sayfa {len(owners)} satır {len(rows)} eşleşme {sum(int(r[3]) for r in rows)}", file=sys.stderr)


def gorunum(commit, page=None):
    vs = views(tree(commit))
    print(len(vs) if page is None else sum(1 for v in vs if page_of(v) == page))


def degisken(commit):
    strip, script, variable = rules(commit)
    hits = 0
    for rel in views(tree(commit)):
        text = script.sub("", strip.sub("", show(commit, WEB + rel)))
        if variable.search(text):
            hits += 1
            print(f"{page_of(rel) or '-'}\t{rel}")
    print(f"değişken görünüm {hits}", file=sys.stderr)


def budget(commit, page, check, inst):
    """Taban satırı: işin satırı işin tabanında yok sayılır, FU-19 sınıf dosyası satırı sayıya eşit kabul edilir."""
    for line in show(commit, BASELINE).split("\n"):
        cells = line.split("\t")
        if len(cells) >= 4 and cells[0] == page and cells[1] == check and cells[2] == inst:
            return int(cells[3])
    return None


def count(pats, key, text):
    return len(list(pats[key].finditer(text)))


def turetim(commit):
    pats = patterns(commit)
    key_of = {v: k for k, v in JS_KEYS.items()}
    classes, pages, _ = measure(commit)
    work = {(r[0], r[1], r[2]): int(r[3]) for r in rows_of(commit, pats, classes, pages)}
    out = []

    def line(sb, page, check, inst, text):
        n = count(pats, key_of[check], text)
        b = work.get((page, check, inst))
        if b is None:
            b = budget(commit, page, check, inst) or 0
        out.append((sb, page, check, inst, n, n - b))

    def alert(inst):
        return show(commit, WEB + inst) + "\nalert('x');\n"

    def styled(inst):
        src = show(commit, WEB + inst).split("\n")
        i = next(i for i, s in enumerate(src) if re.search(r"\.style\.", s))
        src[i] += " a.style.b;"
        print(f"çapa\t{inst}\t{i + 1}", file=sys.stderr)
        return "\n".join(src)

    pick = "wwwroot/assets/js/Platform/PersonReferences/person-reference-picker.js"
    for p in sorted(pages[pick]):
        line("SB-1", p, "CI-7", pick, alert(pick))
    trace = "wwwroot/assets/js/Traceability/trace-ui.js"
    for p in sorted(pages[trace]):
        line("SB-2", p, "CI-7", trace, alert(trace))
    modal = E + "modal-form-utils.js"
    for p in sorted(pages[modal]):
        line("SB-3", p, "CI-6", modal, styled(modal))
    goals = E + "goals.js"
    for p in sorted(pages[goals]):
        line("SB-4", p, "CI-6", goals, styled(goals))
    for p in sorted(pages[goals]):
        for check in ("CI-6", "CI-5"):
            line("SB-5", p, check, goals, show(commit, WEB + goals) + "\n")
    deco = "wwwroot/assets/js/pages/decomposition/decompositionPage.js"
    for p in sorted(pages[deco]):
        line("SB-6", p, "CI-7", deco, show(commit, WEB + deco))
    for key, check in JS_KEYS.items():
        if count(pats, key, show(commit, WEB + goals)):
            line("SB-8", "CRM/CyclePeriods", check, goals, show(commit, WEB + goals))
    for p in sorted(pages[trace] - {"Traceability/Serials"}):
        line("SB-9", p, "CI-7", trace, alert(trace))
    for inst, page in (("wwwroot/assets/js/CRM/Accounts/index.js", "CRM/Accounts"), ("wwwroot/assets/js/Recall/cases.js", "Recall")):
        line("SB-10", page, "CI-7", inst, alert(inst))
    for r in out:
        print("\t".join(map(str, r)))
    red = {}
    for sb, _, _, _, n, d in out:
        red[sb] = red.get(sb, 0) + (1 if d > 0 else 0)
    print("kırmızı\t" + " ".join(f"{k}={red[k]}" for k in ("SB-1", "SB-2", "SB-3", "SB-4", "SB-8", "SB-9")), file=sys.stderr)


def secici(commit):
    want = raw_rules(commit)
    tree_ = ast.parse(show(commit, SELECTOR))
    for node in tree_.body:
        if isinstance(node, ast.Assign) and any(isinstance(t, ast.Name) and t.id == "FU01_REF_PATTERNS" for t in node.targets):
            got = ast.literal_eval(node.value)
            if got == want:
                print("aynı")
                return
            for k in sorted(set(want) | set(got)):
                if want.get(k) != got.get(k):
                    print(f"farklı\t{k}")
            sys.exit(1)
    print("FU01_REF_PATTERNS yok")
    sys.exit(1)


{"satirlar": satirlar, "ciftler": ciftler, "siniflar": siniflar, "gorunum": gorunum, "degisken": degisken,
 "turetim": turetim, "secici": secici}[sys.argv[1]](*sys.argv[2:])
