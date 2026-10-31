#!/usr/bin/env python3
"""MOD-0286 AC-21 bağımsız ölçüm — Control Tower yazdı; iş paketi değiştirmez. Seçiciyi çağırmaz, içe aktarmaz.
Yalnız git ağacını ve paketin "DL-3 satır kontrol desenleri" kod bloğunu okur, depoya yazmaz. Depo kökünden koşulur:
  satirlar <commit>    tek seviye sayfaların kırmızıları: page, check, instance, count (sıralı TSV)
  liste <commit>       AC-21 (b)'nin dokunacağı dosyalar: <tür><TAB><depo köküne göre yol>; tür cshtml, js, fu18
  sahip <taban>        FU-16 satırlarında owner ve reason kararla aynı değilse satırı yazar (karar kaydı
                       docs/records/decisions/2026-10/pss-mod0286-tek-seviye-borc-01.md)
"""
import re
import subprocess
import sys

PACK = "execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md"
WEB = "frontend/Diten.Web/"
JSROOT = "wwwroot/assets/js/"
NOT_PAGE = {"Shared", "Archive"}
VIEW_KEYS = {"CI-2": "CI-2", "CI-5": "CI-5", "CI-6v": "CI-6"}
JS_KEYS = {"CI-4": "CI-4", "CI-5": "CI-5", "CI-6j": "CI-6", "CI-7": "CI-7", "CI-8": "CI-8", "CI-9p": "CI-9", "CI-10": "CI-10"}
REASON = "sahip kararı tek-seviye-borc-01 (tek seviye sayfalar, 2026-10-31)"
OWNER = {}
for owner, pages in (
        ("enterprise-strategy-business-performance", "EnterpriseStrategyBusinessPerformance"),
        ("portfolio-delivery", "DemandIdeas DeliveryExecutionManagement PPM DecompositionTreeBuilder"),
        ("management-governance", "ManagementGovernance Meetings"),
        ("platform-shared-services", "Account ESignature WorkCenterNext Tasks Home Profile Platform"),
        ("supply-chain-execution", "Recall Expiry Quarantine Inventory"),
        ("human-capital-management", "TimeEntry")):
    for page in pages.split():
        OWNER[page] = owner


def git(*args):
    return subprocess.run(["git", *args], capture_output=True, check=True).stdout


def show(commit, path):
    return git("show", f"{commit}:{path}").decode("utf-8", errors="replace")


def patterns(commit):
    text = show(commit, PACK)
    start = text.index("```text\n", text.index("### DL-3 satır kontrol desenleri")) + len("```text\n")
    out = {}
    for line in text[start:text.index("```", start)].split("\n"):
        if line:
            key, raw = line.split("\t", 1)
            out[key] = re.compile(raw)
    return out


def tree(commit):
    names = git("ls-tree", "-r", "--name-only", commit, WEB + "Views", WEB + JSROOT).decode().split("\n")
    return [p[len(WEB):] for p in names if p.startswith(WEB)]


def direct_views(rels):
    out = {}
    for rel in rels:
        parts = rel.split("/")
        if rel.startswith("Views/") and len(parts) == 3 and parts[1] not in NOT_PAGE:
            out.setdefault(parts[1], []).append(rel)
    return out


def satirlar(commit):
    pats, rels = patterns(commit), tree(commit)
    views = direct_views(rels)
    js = {}
    for rel in rels:
        parts = rel.split("/")
        if rel.startswith(JSROOT) and len(parts) == 5 and rel.endswith(".js"):
            js.setdefault(parts[3], []).append(rel)
    rows = []
    for page in views:
        for v in [x for x in views[page] if x.endswith(".cshtml")]:
            text = show(commit, WEB + v)
            if not v.rsplit("/", 1)[1].startswith("_") and 'Layout = "' not in text:
                rows.append((page, "CI-1", f"{v}:yok:Layout", "-"))
            for key, check in VIEW_KEYS.items():
                n = len(list(pats[key].finditer(text)))
                if n:
                    rows.append((page, check, v, str(n)))
        texts = {j: show(commit, WEB + j) for j in js.get(page, [])}
        for j, text in texts.items():
            for key, check in JS_KEYS.items():
                n = len(list(pats[key].finditer(text)))
                if n:
                    rows.append((page, check, j, str(n)))
        index = f"{JSROOT}{page}/index.js"
        if f"Views/{page}/_DataTable.cshtml" in views[page] and index in texts and "savedView" not in texts[index]:
            rows.append((page, "CI-9", f"{index}:yok:savedView", "-"))
    for r in sorted(rows):
        print("\t".join(r))


def liste(commit):
    rels = tree(commit)
    direct = direct_views(rels)
    any_view, subs = set(), {}
    for rel in rels:
        parts = rel.split("/")
        if rel.startswith("Views/") and len(parts) >= 3 and parts[1] not in NOT_PAGE:
            any_view.add(parts[1])
            if len(parts) >= 4:
                subs.setdefault(parts[1], set()).add(parts[2])
    for page, files in sorted(direct.items()):
        for f in sorted(files):
            if f.endswith(".cshtml"):
                print(f"cshtml\t{WEB}{f}")
    for rel in sorted(rels):
        if not (rel.startswith(JSROOT) and rel.endswith(".js")):
            continue
        parts = rel[len(JSROOT):].split("/")
        top = parts[0]
        if top == "pages":
            print(f"fu18\t{WEB}{rel}")
        elif top in any_view:
            if len(parts) == 2:
                print(f"{'js' if top in direct else 'fu18'}\t{WEB}{rel}")
            elif parts[1] not in subs.get(top, set()):
                print(f"fu18\t{WEB}{rel}")


def sahip(path):
    bad = 0
    for line in open(path, encoding="utf-8").read().split("\n")[1:]:
        cells = line.split("\t")
        if len(cells) == 7 and cells[5] == "FU-16":
            if cells[4] != REASON or OWNER.get(cells[0]) != cells[6]:
                print(line)
                bad += 1
    print(f"kararla aynı olmayan FU-16 satırı {bad}")


{"satirlar": satirlar, "liste": liste, "sahip": sahip}[sys.argv[1]](sys.argv[2])
