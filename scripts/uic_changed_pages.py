#!/usr/bin/env python3
"""
MOD-0286 DL-3 — UI uyum seçicisi: bir değişikliğin dokunduğu sayfalarda DL-2'nin otomatik maddelerini koşar ve sonucu
kayıtlı tabanla (tests/ui-compliance-baseline.tsv) karşılaştırır.

    python3 scripts/uic_changed_pages.py --base <ref>        CI kipi: <ref>...HEAD farkının dokunduğu sayfalar
    python3 scripts/uic_changed_pages.py --self-test         kendi kendine test (geçici bir depoda, depo dışında)
    python3 scripts/uic_changed_pages.py --check-baseline    tabanı biçim kurallarıyla okur, CI başına satır ve eşleşme
    python3 scripts/uic_changed_pages.py --draft-baseline    bütün sayfalar, başlıklı TSV — yalnız ilk taban için, yerelde

Dört kip birbirini dışlar; kip yoksa ya da birden çoksa çıkış 2. CI kipi hiçbir koşulda --draft-baseline'a düşmez;
bütün sayfaları koşturan bir bayrak, ortam değişkeni ya da değer yoktur.

CI kipinin çıkışı: 0 = taban dışında kırmızı yok; 1 = en az bir `YENİ KIRMIZI`; 2 = araç hatası (git hatası, eksik
base, bilinmeyen durum harfi, okunamayan ya da bozuk taban, okunamayan ya da biçimi bozuk arama kaydı JSON'u,
sınıflandırılamayan doğrulayıcı çıktısı). Araç hatası hiçbir zaman "0 değişen sayfa" olarak okunmaz.

Kaynak: MOD-0286 paketi (execution/domains/platform-shared-services/module-packs/
MOD-0286-ui-design-system-page-pattern-governance.md), "DL-3 UI uyum CI bağlantısı" bölüm 1-2, CI-1…CI-15 tablosu ve
"DL-3 satır kontrol desenleri" kod bloğu. Satır kontrollerinin desenleri çalışma anında o kod bloğundan okunur; bu
dosyadaki OWN_PATTERNS yalnız --self-test'in karşılaştırma kopyasıdır.

Okuma: sayfa dosyaları HEAD'den (git ls-tree / cat-file), doğrulayıcı (CI-3) ve resx denetçisi (CI-14) diskten okur;
bu yüzden CI ve taslak kipleri frontend/Diten.Web altında çalışma ağacı HEAD'le aynı değilse durur (çıkış 2).
Yalnız Python standart kütüphanesi ve git. Depoya hiçbir dosya yazmaz; depo modülü import etmez.
"""
from __future__ import annotations

import argparse
import collections
import json
import os
import re
import subprocess
import sys
import tempfile
import time
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WEB = "frontend/Diten.Web/"
PACK = "execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md"
PATTERN_HEADING = "### DL-3 satır kontrol desenleri"
BASELINE = "tests/ui-compliance-baseline.tsv"
HEADER = ["page", "check", "instance", "count", "reason", "fu", "owner"]
VERIFIER = ".antigravity/scripts/verify_datatable_page.py"
RESX_CHECKER = ".antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py"
SEARCH = {"en": WEB + "wwwroot/assets/json/platform-search.en.json", "tr": WEB + "wwwroot/assets/json/platform-search.tr.json"}
PAGEHELP_CATALOG = WEB + "Services/Help/PageHelpCatalog.cs"
TREES = (WEB + "Views/", WEB + "wwwroot/assets/js/", WEB + "Resources/")
LANGS = ("en", "tr", "fr", "es", "zh", "ar", "ru")
NON_EN = ("tr", "fr", "es", "zh", "ar", "ru")
NOT_PAGE_AREAS = ("Shared", "Archive")
VERIFIER_TIMEOUT = 120
CHECKER_TIMEOUT = 120

# The selector's own copy of the pack's "DL-3 satır kontrol desenleri" block — used ONLY by --self-test to prove that
# the block it reads at run time says the same thing (pack part 1, rev 12, F-24). The run itself uses the pack's block.
OWN_PATTERNS = {
    "CI-2": r"spinner-border",
    "CI-4": r"createCrudTable\(",
    "CI-5": r"DataTable\(\s*\{|new DataTable\([^,()]+,\s*\{",
    "CI-6v": r'style="',
    "CI-6j": r"\.style\.",
    "CI-7": r"\balert\(|Swal\.fire|toastr\.",
    "CI-8": r"document\.cookie|Bearer|access_token",
    "CI-9p": r"api/personalization",
    "CI-10": r"colReorder",
}
# pattern key -> the `check` written in the baseline and in the output (CI-6v / CI-6j / CI-9p are keys, never checks)
KEY_CHECK = {"CI-2": "CI-2", "CI-4": "CI-4", "CI-5": "CI-5", "CI-6v": "CI-6", "CI-6j": "CI-6", "CI-7": "CI-7",
             "CI-8": "CI-8", "CI-9p": "CI-9", "CI-10": "CI-10"}
# pattern key -> the files of the check's "Runs on" cell
KEY_SCOPE = {"CI-2": "views", "CI-4": "js", "CI-5": "both", "CI-6v": "views", "CI-6j": "js", "CI-7": "js", "CI-8": "js",
             "CI-9p": "js", "CI-10": "js"}
LINE_CHECKS = ("CI-2", "CI-4", "CI-5", "CI-6", "CI-7", "CI-8", "CI-10")   # CI-9 has a line half (api/personalization)

FU4_NAME = "personalizationClient sends tenant header only for tenant users"
CORE_FILE_CHECKS = {"Index.cshtml exists", "_Filter.cshtml exists", "_DataTable.cshtml exists", "_IndexL10n.cshtml exists",
                    "index.js exists", "index.l10n.js exists", "dt-defaults.js exists", "backbone-custom.css exists"}
NOT_MEASURED_STOP = "doğrulayıcı eksik dosyada durdu"
FOLDER_LEVEL_VIEWS = ("_ViewStart.cshtml", "_ViewImports.cshtml")
FU18_LINE = "eşlenmemiş sayfa JS'i, sayfa kontrolü yok (FU-18)"

# Owner decision pss-mod0286-taban-01 (taban-karar, 2026-10-27): reason, fu and owner of the first baseline
REASON = "sahip kararı pss-mod0286-taban-01 (ilk taban, 2026-10-27)"
# Owner decision tek-seviye-borc-01 (2026-10-31): reason of the FU-16 rows; their owners are in the decision record, not here
SINGLE_REASON = "sahip kararı tek-seviye-borc-01 (tek seviye sayfalar, 2026-10-31)"
UNKNOWN_OWNER = "belirsiz — sahip belirleyecek"
AREA_OWNER = {}
for _owner, _areas in (
        ("commercial-suite", "CRM Account"),
        ("pharmacovigilance", "Pharmacovigilance"),
        ("portfolio-delivery", "PPM DeliveryExecutionManagement DemandIdeas DecompositionTreeBuilder"),
        ("human-capital-management", "HCM HumanCapital TalentEcosystem TimeEntry Organization"),
        ("supply-chain-execution", "Inventory Expiry Quarantine Recall Traceability SupplyChain Manufacturing"),
        ("master-data-management", "MDM MasterData MasterDataManagement"),
        ("procurement", "Procurement"),
        ("management-governance", "Governance ManagementGovernance"),
        ("enterprise-strategy-business-performance", "EnterpriseStrategyBusinessPerformance"),
        ("developer-enablement", "DevEnablement"),
        ("platform-shared-services", "Platform Home Profile Tasks WorkCenterNext WorkingCalendar Meetings DocumentManagement ESignature")):
    for _a in _areas.split():
        AREA_OWNER[_a] = _owner
# The two `bilgi:` rows, with the pack's text (pack DL-3 part 2, lines 416-418)
BILGI_ROWS = [
    ("DevEnablement/GoldenReferenceSlim", "bilgi: RC-6 AddNewGoldenReferenceSlim", "-", "-",
     REASON + "; paket gerekçesi: RC-6 §2 modül anahtarları sayfa başına ölçülemez, FU-12 bilgi satırıdır (DL-3 CI tablosu dışında kalanlar)",
     "FU-12", "developer-enablement"),
    ("DevEnablement/GoldenReferenceCompact", "bilgi: reference not ready (DEV-0001 status: in-progress)", "-", "-",
     REASON + "; paket gerekçesi: DEV-0001 status in-progress, Golden Compact referansı kabul edilmedi (FU-15)",
     "FU-15", "developer-enablement"),
]

# the verifier's gaps contract, read fail-closed as .antigravity/scripts/list_screen_touch_hook.py:62 reads it
GAP_LINE = re.compile(r"^(\d+)\) \S")
OK_LINE = re.compile(r"^GAPS-OK: [1-9]\d* kontrol, 0 sapma$")
SHELL_WARNING = re.compile(r"^\[shell\] WARNING: the shell file itself lacks .+ — reported below as a red 'List shell file carries …' check$")
INFO_LINES = (
    re.compile(r"^\[shell\] \S+ calls _ListShell — markers resolved from the model setup \(.*\) and \S.*$"),
    SHELL_WARNING,
    re.compile(r"^\[factory\] \S+ calls DitenDataTable\.createList \(dataMode=.*\) — \d+/\d+ mechanics verified in \S.*$"),
    re.compile(r"^\[pack\] \S+ declares data_mode: \S.*$"),
)
TITLE_CASE_LINE = re.compile(r"^\[X\] (\S+): '([^']+)' should be Title Case \(UI action label\), got: .*$")

GIT_ENV_EXTRA: dict[str, str] = {}   # set only by --self-test (isolates the throw-away repositories from user config)


class ToolError(Exception):
    """Exit 2: the tool could not measure. Never read as a pass or as '0 changed pages'."""


# ------------------------------------------------------------------------------------------------------------- git
def git(root, *args: str, input: bytes | None = None) -> bytes:
    env = dict(os.environ, GIT_OPTIONAL_LOCKS="0", **GIT_ENV_EXTRA)
    try:
        done = subprocess.run(["git", "-c", "core.quotepath=off", "-C", str(root), *args], input=input,
                              capture_output=True, env=env)
    except OSError as e:
        raise ToolError(f"git başlatılamadı ({type(e).__name__})")
    if done.returncode != 0:
        raise ToolError(f"git {' '.join(args[:2])} → çıkış {done.returncode}: "
                        f"{done.stderr.decode('utf-8', errors='replace').strip()[:200]}")
    return done.stdout


def decode_path(raw: bytes) -> str:
    try:
        return raw.decode("utf-8")
    except UnicodeDecodeError:
        raise ToolError("git UTF-8 olmayan bir yol verdi")


class Tree:
    """The blobs of one commit under frontend/Diten.Web/ (git ls-tree), read on demand (git cat-file --batch)."""

    def __init__(self, root, rev: str):
        self.root, self.rev = root, rev
        self.sha: dict[str, str] = {}
        for item in git(root, "ls-tree", "-r", "-z", rev, "--", WEB.rstrip("/")).split(b"\0"):
            if not item:
                continue
            meta, _, path = item.partition(b"\t")
            parts = meta.split()
            if len(parts) != 3:
                raise ToolError("git ls-tree çıktısı okunamadı")
            if parts[1] == b"blob":
                self.sha[decode_path(path)] = parts[2].decode()
        self.dirs: set[str] = set()
        for p in self.sha:
            i = p.find("/")
            while i != -1:
                self.dirs.add(p[:i + 1])
                i = p.find("/", i + 1)
        self.parents = {p.rsplit("/", 1)[0] + "/" for p in self.sha}   # the folders that hold a file directly
        self.cache: dict[str, bytes] = {}

    def has(self, path: str) -> bool:
        return path in self.sha

    def has_dir(self, prefix: str) -> bool:
        return prefix in self.dirs

    def has_direct(self, prefix: str) -> bool:
        return prefix in self.parents

    def under(self, prefix: str) -> list[str]:
        return sorted(p for p in self.sha if p.startswith(prefix))

    def direct(self, prefix: str) -> list[str]:
        return sorted(p for p in self.sha if p.startswith(prefix) and "/" not in p[len(prefix):])

    def has_page(self, page: str) -> bool:
        """A page exists at this commit: {Area}/{Module} = any file under its folder; {Module} = a file directly in it
        (its subfolders are other pages, pack part 1, rev 15)."""
        vdir = f"{WEB}Views/{page}/"
        return self.has_dir(vdir) if "/" in page else self.has_direct(vdir)

    def prefetch(self, paths) -> None:
        want = sorted({self.sha[p] for p in paths if p in self.sha and p not in self.cache})
        if not want:
            return
        data = git(self.root, "cat-file", "--batch", input=("\n".join(want) + "\n").encode())
        blobs, pos = {}, 0
        for sha in want:
            nl = data.find(b"\n", pos)
            header = data[pos:nl].split() if nl != -1 else []
            if len(header) != 3 or header[1] != b"blob" or header[0].decode() != sha:
                raise ToolError("git cat-file --batch çıktısı okunamadı")
            size = int(header[2])
            blobs[sha] = data[nl + 1:nl + 1 + size]
            pos = nl + 1 + size + 1
        for p in paths:
            if p in self.sha and self.sha[p] in blobs:
                self.cache[p] = blobs[self.sha[p]]

    def read(self, path: str) -> bytes | None:
        if path not in self.sha:
            return None
        if path not in self.cache:
            self.prefetch([path])
        return self.cache[path]


def text_of(data: bytes) -> str:
    return data.decode("utf-8", errors="replace")


def parse_name_status(raw: bytes) -> list[tuple[str, list[str]]]:
    """`git diff --name-status -z` -> [(letter, [path] | [old, new])]; a status outside A C D M R T is a tool error."""
    items = raw.split(b"\0")
    if items and items[-1] == b"":
        items.pop()
    out, i = [], 0
    while i < len(items):
        status = items[i].decode("ascii", errors="replace")
        i += 1
        if not re.fullmatch(r"[ACDMRT][0-9]{0,3}", status):
            raise ToolError(f"git diff bilinmeyen durum harfi verdi: '{status[:8]}'")
        n = 2 if status[0] in "RC" else 1
        if i + n > len(items):
            raise ToolError("git diff çıktısı eksik")
        out.append((status[0], [decode_path(items[i + k]) for k in range(n)]))
        i += n
    return out


# ------------------------------------------------------------------------------------------------- pattern block
def parse_pattern_block(text: str) -> dict[str, str]:
    """The pack's "DL-3 satır kontrol desenleri" code block: every line `<key><TAB><raw pattern>`, the pattern byte for
    byte after the one tab. Exactly the nine keys, each once, each compiling with re.compile and no flags."""
    lines = text.replace("\r\n", "\n").split("\n")
    heads = [i for i, l in enumerate(lines) if l.strip() == PATTERN_HEADING]
    if len(heads) != 1:
        raise ToolError(f"paket: '{PATTERN_HEADING}' başlığı {len(heads)} kez var, 1 olmalı")
    start = next((i for i in range(heads[0] + 1, len(lines)) if lines[i] == "```text"), None)
    if start is None:
        raise ToolError("paket: desen başlığından sonra ```text kod bloğu yok")
    end = next((i for i in range(start + 1, len(lines)) if lines[i] == "```"), None)
    if end is None:
        raise ToolError("paket: desen kod bloğu kapanmıyor")
    patterns: dict[str, str] = {}
    for line in lines[start + 1:end]:
        key, tab, pattern = line.partition("\t")
        if not tab or not key or not pattern or "\t" in pattern:
            raise ToolError(f"paket: desen satırı '<anahtar><TAB><desen>' değil: '{line[:40]}'")
        if key in patterns:
            raise ToolError(f"paket: desen anahtarı iki kez var: {key}")
        try:
            re.compile(pattern)
        except re.error as e:
            raise ToolError(f"paket: {key} deseni derlenmiyor ({e})")
        patterns[key] = pattern
    if set(patterns) != set(OWN_PATTERNS):
        raise ToolError(f"paket: desen anahtarları beklenen dokuz anahtar değil (eksik {sorted(set(OWN_PATTERNS) - set(patterns))}, "
                        f"fazla {sorted(set(patterns) - set(OWN_PATTERNS))})")
    return patterns


def load_patterns(root) -> dict[str, re.Pattern]:
    try:
        text = (Path(root) / PACK).read_text(encoding="utf-8")
    except OSError:
        raise ToolError(f"paket okunamadı: {PACK}")
    return {k: re.compile(v) for k, v in parse_pattern_block(text).items()}


# ------------------------------------------------------------------------------------------------------ baseline
PATH_PART = r"[^:\t\s]+"


def _norm_ok(path: str) -> bool:
    return not path.startswith("/") and "//" not in path and ".." not in path.split("/") and not path.endswith("/")


def instance_problem(page: str, check: str, instance: str, count: str) -> str | None:
    """The form of one row's instance and count (pack Validation Rules, DL-3 part 2); None when well formed."""
    line_count = re.fullmatch(r"[1-9][0-9]*", count) is not None
    if check.startswith("bilgi: "):
        return None if instance == "-" and count == "-" else "bilgi: satırında instance ve count '-' olmalı"
    area_module = re.fullmatch(r"[^/\s]+/[^/\s]+", page) is not None
    single = re.fullmatch(r"[^/\s]+", page) is not None and page not in NOT_PAGE_AREAS   # {Module}, FU-16 (rev 15)
    page_key = area_module or single
    vdir, jdir = f"Views/{page}/", f"wwwroot/assets/js/{page}/"

    def path_in(p: str, prefixes, suffixes) -> bool:
        # a single-level page's instance is a file directly in its folder, no further '/' (pack Validation Rules, F-34)
        return (re.fullmatch(PATH_PART, p) is not None and _norm_ok(p)
                and any(p.startswith(x) and not (single and "/" in p[len(x):]) for x in prefixes)
                and any(p.endswith(s) for s in suffixes))
    if check == "CI-3":
        ok = page and area_module and instance == instance.strip() and instance != "-" and " — " not in instance
        return None if ok and count == "-" else "CI-3 instance doğrulayıcı kontrol adı, count '-' olmalı"
    if check in ("CI-2", "CI-4", "CI-5", "CI-6", "CI-7", "CI-8", "CI-10") or (check == "CI-9" and ":" not in instance):
        scope = {"CI-2": ([vdir], [".cshtml"]), "CI-5": ([vdir, jdir], [".cshtml", ".js"]),
                 "CI-6": ([vdir, jdir], [".cshtml", ".js"])}.get(check, ([jdir], [".js"]))
        if not page_key or not path_in(instance, *scope):
            return f"{check} satır kontrolü instance'ı sayfanın dosya yolu değil"
        return None if line_count else f"{check} satır kontrolünde count 1 ya da büyük bir tam sayı olmalı (işaretsiz, baştaki sıfırsız)"
    if count != "-":
        return f"{check} satırında count '-' olmalı"
    lang = "(?:" + "|".join(LANGS) + ")"
    if check == "CI-1":
        m = re.fullmatch(r"(.+):yok:Layout", instance)
        ok = page_key and m and path_in(m.group(1), [vdir], [".cshtml"]) and not m.group(1).split("/")[-1].startswith("_")
    elif check == "CI-9":
        m = re.fullmatch(r"(.+):yok:savedView", instance)
        ok = page_key and m and m.group(1) == jdir + "index.js"
    elif check == "CI-11":
        m = re.fullmatch(r"(.+):yok:GetAllStrings", instance)
        ok = page_key and m and path_in(m.group(1), [vdir], ["/_IndexL10n.cshtml", "/_L10n.cshtml"])
    elif check == "CI-12":
        ok = page.startswith("Platform/") and area_module and re.fullmatch(r"platform-search\.(?:en|tr)\.json:yok", instance)
    elif check == "CI-13":
        ok = (page.startswith("Resources/Views/") and _norm_ok(page) and re.fullmatch(r"[^\s]+", page)
              and re.fullmatch(lang + r":(?:dosya-yok|anahtar-yok:\S.*)", instance))
    elif check == "CI-14":
        ok = page == "Resources/SharedResource" and re.fullmatch(lang + r":(?:missing|equal|title-case):\S.*", instance)
    elif check == "CI-15":
        ok = (re.fullmatch(r"PageHelp/[^/\s]+", page)
              and re.fullmatch(lang + r":(?:dosya-yok|Purpose-Audience|related_total|range_ok|seq_diff|title_diff|Related\.\S+)", instance))
    else:
        return f"tanımsız kontrol {check}"
    return None if ok else f"{check} instance biçimi bozuk"


def parse_baseline(text: str) -> list[dict]:
    """tests/ui-compliance-baseline.tsv read with the pack's Validation Rules; any violation is a tool error (exit 2)."""
    if "\r" in text:
        raise ToolError("taban: CR karakteri var (yalnız LF)")
    lines = text.split("\n")
    if lines and lines[-1] == "":
        lines.pop()
    if not lines or lines[0].split("\t") != HEADER:
        raise ToolError(f"taban: başlık birebir '{chr(92)}t'.join({HEADER}) değil")
    rows, seen = [], set()
    for n, line in enumerate(lines[1:], start=2):
        cells = line.split("\t")
        if len(cells) != len(HEADER):
            raise ToolError(f"taban satır {n}: {len(cells)} hücre, {len(HEADER)} olmalı")
        if any(c.strip() == "" for c in cells):
            raise ToolError(f"taban satır {n}: boş hücre")
        row = dict(zip(HEADER, cells))
        if not (re.fullmatch(r"CI-(?:[1-9]|1[0-5])", row["check"]) or re.fullmatch(r"bilgi: \S.*", row["check"])):
            raise ToolError(f"taban satır {n}: check '{row['check'][:30]}' CI-1…CI-15 ya da 'bilgi: …' değil")
        bad = instance_problem(row["page"], row["check"], row["instance"], row["count"])
        if bad:
            raise ToolError(f"taban satır {n}: {bad}")
        if not re.fullmatch(r"FU-[1-9][0-9]*", row["fu"]):
            raise ToolError(f"taban satır {n}: fu '{row['fu'][:20]}' FU-<n> biçiminde değil")
        key = (row["page"], row["check"], row["instance"])
        if key in seen:
            raise ToolError(f"taban satır {n}: page + check + instance tekrarı ({' · '.join(key)[:80]})")
        seen.add(key)
        row["line"] = n
        rows.append(row)
    return rows


def read_baseline(root) -> list[dict]:
    try:
        data = (Path(root) / BASELINE).read_bytes()
    except OSError:
        raise ToolError(f"taban okunamadı: {BASELINE}")
    try:
        return parse_baseline(data.decode("utf-8"))
    except UnicodeDecodeError:
        raise ToolError("taban UTF-8 değil")


# ---------------------------------------------------------------------------------------------- shared helpers
def owner_of(page: str, check: str, instance: str) -> tuple[str, str]:
    if "/" not in page:      # a single-level page: AREA_OWNER does not apply, the owner decides (pack part 2, rev 15)
        return "FU-16", UNKNOWN_OWNER
    if check == "CI-3" and instance == FU4_NAME:
        return "FU-4", "developer-enablement"
    if check == "CI-13":
        area = page.split("/")[2] if len(page.split("/")) > 2 else None
    elif check in ("CI-14", "CI-15"):
        area = None
    else:
        area = page.split("/")[0]
    return "FU-14", AREA_OWNER.get(area, UNKNOWN_OWNER)


def registry_items(data: bytes, label: str) -> list[tuple[str, dict]]:
    """[(JSON path navigation/<group> | suggestions/<group>, item)]; a file that is not JSON or breaks the §2 shape
    (`{"navigation": {<group>: [items]}, "suggestions": {<group>: [items]}}`, a string url in every item) -> exit 2."""
    try:
        doc = json.loads(data.decode("utf-8"))
    except (UnicodeDecodeError, ValueError):
        raise ToolError(f"{label} JSON olarak okunamıyor")
    if not isinstance(doc, dict) or set(doc) != {"navigation", "suggestions"}:
        raise ToolError(f"{label} üst seviye biçimi {{navigation, suggestions}} değil")
    out = []
    for top in ("navigation", "suggestions"):
        groups = doc[top]
        if not isinstance(groups, dict):
            raise ToolError(f"{label} {top} bir nesne değil")
        for group, items in groups.items():
            if not isinstance(items, list):
                raise ToolError(f"{label} {top}/{group} bir liste değil")
            for item in items:
                if not isinstance(item, dict) or not isinstance(item.get("url"), str):
                    raise ToolError(f"{label} {top}/{group} içinde url'si metin olmayan öğe")
                out.append((f"{top}/{group}", item))
    return out


def registry_keys(data: bytes | None, label: str) -> collections.Counter:
    """The multiset of keys (JSON path, canonical item) of one version of one registry file; an absent file is empty."""
    if data is None:
        return collections.Counter()
    return collections.Counter((path, json.dumps(item, sort_keys=True, ensure_ascii=False, separators=(",", ":")))
                               for path, item in registry_items(data, label))


def resx_keys(data: bytes, label: str) -> set[str]:
    try:
        root = ET.fromstring(data)
    except ET.ParseError:
        raise ToolError(f"{label} XML olarak okunamıyor")
    return {d.get("name") for d in root.findall("data") if d.get("name")}


def rc24_sets(text: str) -> tuple[set[str], list[tuple[str, str]]]:
    """RC-24's set building (uic-auto.sh RC-24 block) in Python: the keys (`grep -o '<data name="[^"]*"'`) and the
    key/value pairs read the way its awk reads them (value on the same line, else on the next line)."""
    lines = text.split("\n")
    keys: set[str] = set()
    for line in lines:
        keys.update(re.findall(r'<data name="([^"]*)"', line))
    pairs, i = [], 0
    while i < len(lines):
        line = lines[i]
        if '<data name="' in line:
            m = re.search(r'name="[^"]*"', line)
            k = m.group(0)[6:-1]
            if "<value>" not in line and i + 1 < len(lines):
                i += 1
                line = lines[i]
            v = re.search(r"<value>.*</value>", line)
            pairs.append((k, v.group(0)[7:-8] if v else ""))
        i += 1
    return keys, pairs


# ------------------------------------------------------------------------------------------------------- the run
class Run:
    def __init__(self, root, mode: str, base: str | None = None):
        self.root, self.mode = Path(root), mode
        self.out: list[str] = []
        self.new_red = 0
        self.patterns = load_patterns(self.root)
        clean = git(self.root, "status", "--porcelain=v1", "-z", "--untracked-files=all", "--", WEB.rstrip("/"))
        if clean.strip(b"\0"):
            raise ToolError("frontend/Diten.Web altında çalışma ağacı HEAD'le aynı değil — doğrulayıcı diskten okur, "
                            "ölçüm HEAD'e bağlanamaz")
        self.head = Tree(self.root, "HEAD")
        self.base = None
        if base is not None:
            if base.startswith("-") or not base.strip():
                raise ToolError(f"base '{base[:40]}' bir ref değil")
            commit = git(self.root, "rev-parse", "--verify", "--quiet", f"{base}^{{commit}}").decode().strip()
            if not re.fullmatch(r"[0-9a-f]{40}", commit):
                raise ToolError(f"base '{base[:40]}' bir commit'e çözülmedi")
            self.base_ref = base
            self.base = Tree(self.root, commit)
        self.rows: dict[tuple[str, str], dict[str, dict]] = collections.defaultdict(dict)
        if mode == "ci":
            for row in read_baseline(self.root):
                self.rows[(row["page"], row["check"])][row["instance"]] = row

    # ----- reading at HEAD and at the base
    def either_has(self, path: str) -> bool:
        return self.head.has(path) or (self.base is not None and self.base.has(path))

    def either_has_dir(self, prefix: str) -> bool:
        return self.head.has_dir(prefix) or (self.base is not None and self.base.has_dir(prefix))

    def either_has_direct(self, prefix: str) -> bool:
        return self.head.has_direct(prefix) or (self.base is not None and self.base.has_direct(prefix))

    # ----- the checks of one page; results are ("abs", check, reds, passes, measured), ("line", check, counts) or
    # ("note", line) — a line printed as it is, never a red, never a baseline row
    def evaluate_page(self, page: str, added_partials=()) -> list[tuple]:
        two_level = "/" in page
        area, module = page.split("/", 1) if two_level else (None, page)
        vdir, jdir = f"{WEB}Views/{page}/", f"{WEB}wwwroot/assets/js/{page}/"
        files = self.head.under if two_level else self.head.direct    # a single-level page: only files directly in its folder
        views = [p for p in files(vdir) if p.endswith(".cshtml")]
        js = [p for p in files(jdir) if p.endswith(".js")]
        self.head.prefetch(views + js)
        rel = lambda p: p[len(WEB):]
        results = []
        reds, passes = set(), []
        for p in views:
            if p.rsplit("/", 1)[-1].startswith("_"):
                continue
            if b'Layout = "' in self.head.read(p):
                passes.append(rel(p))
            else:
                reds.add(rel(p) + ":yok:Layout")
        results.append(("abs", "CI-1", reds, passes, True))

        def counts(keys_files) -> dict[str, int]:
            c = {}
            for key, files in keys_files:
                for p in files:
                    c[rel(p)] = sum(1 for _ in self.patterns[key].finditer(text_of(self.head.read(p))))
            return c
        scope = {"views": views, "js": js, "both": js + views}
        line = {k: ("line", KEY_CHECK[k], counts([(k, scope[KEY_SCOPE[k]])]))
                for k in ("CI-2", "CI-4", "CI-5", "CI-7", "CI-8", "CI-9p", "CI-10")}
        line["CI-6"] = ("line", "CI-6", counts([("CI-6v", views), ("CI-6j", js)]))
        list_page = self.either_has(vdir + "_DataTable.cshtml")
        ci3 = None
        if list_page:
            ci3 = (self.ci3(area, module, vdir) if two_level
                   else ("note", f"ölçülmedi {page} CI-3 - doğrulayıcı alansız sayfayı desteklemiyor (FU-17)"))
        ci9 = None
        if list_page:
            idx = jdir + "index.js"
            data = self.head.read(idx)
            if data is None or b"savedView" not in data:
                ci9 = ("abs", "CI-9", {rel(idx) + ":yok:savedView"}, [], True)
            else:
                ci9 = ("abs", "CI-9", set(), [rel(idx)], True)
        ci11 = None
        if added_partials:
            reds, passes = set(), []
            for p in sorted(added_partials):
                data = self.head.read(p)
                if data is None or b"GetAllStrings(true)" not in data:
                    reds.add(rel(p) + ":yok:GetAllStrings")
                else:
                    passes.append(rel(p))
            ci11 = ("abs", "CI-11", reds, passes, True)
        ci12 = self.ci12(module) if area == "Platform" and self.either_has(vdir + "Index.cshtml") else None
        for r in (line["CI-2"], ci3, line["CI-4"], line["CI-5"], line["CI-6"], line["CI-7"], line["CI-8"], ci9,
                  line["CI-9p"], line["CI-10"], ci11, ci12):
            if r is not None:
                results.append(r)
        return results

    def ci3(self, area: str, module: str, vdir: str) -> tuple:
        reference = None
        if self.either_has(vdir + "_CreateEditOffcanvas.cshtml"):
            reference = "slim"
        elif self.either_has(vdir + "_Form.cshtml"):
            reference = "compact"
        verifier = self.root / VERIFIER
        if not verifier.is_file():
            raise ToolError(f"doğrulayıcı yok: {VERIFIER}")
        argv = ([sys.executable, "-B", str(verifier), ".", "--area", area, "--module", module]
                + (["--reference", reference] if reference else []) + ["--format", "gaps", "--ok-line"])
        try:
            done = subprocess.run(argv, cwd=self.root, capture_output=True, timeout=VERIFIER_TIMEOUT)
        except subprocess.TimeoutExpired:
            raise ToolError(f"doğrulayıcı zaman aşımı ({area}/{module}, {VERIFIER_TIMEOUT} s)")
        except OSError as e:
            raise ToolError(f"doğrulayıcı başlatılamadı ({type(e).__name__})")
        where = f"{area}/{module}, çıkış {done.returncode}"
        if done.stderr.strip():
            raise ToolError(f"doğrulayıcı hata akışına yazdı ({where})")
        lines = [l for l in done.stdout.decode("utf-8", errors="replace").splitlines() if l.strip()]
        gaps = [l for l in lines if GAP_LINE.match(l)]
        oks = [i for i, l in enumerate(lines) if OK_LINE.match(l)]
        other = [l for l in lines if not GAP_LINE.match(l) and not OK_LINE.match(l) and not any(p.match(l) for p in INFO_LINES)]
        if other:
            raise ToolError(f"doğrulayıcı çıktısı sınıflandırılamadı ({where}): '{other[0][:60]}'")
        if not gaps and any(SHELL_WARNING.match(l) for l in lines):
            raise ToolError(f"doğrulayıcı kabuk uyarısı verdi ama numaralı satır yok ({where})")
        if done.returncode == 0 and not gaps:
            if oks == [len(lines) - 1]:
                return ("abs", "CI-3", set(), [lines[-1]], True)
            raise ToolError(f"doğrulayıcı başarı satırı vermedi ({where})")
        numbers = [int(GAP_LINE.match(l).group(1)) for l in gaps]
        if done.returncode != 1 or oks or numbers != list(range(1, len(gaps) + 1)):
            raise ToolError(f"doğrulayıcı çıkışı ile çıktısı uyuşmuyor ({where}, {len(gaps)} numaralı satır)")
        reds = set()
        for l in gaps:
            name = l.split(") ", 1)[1].split(" — ", 1)[0].rstrip()
            if not name or "\t" in name or name in reds:
                raise ToolError(f"doğrulayıcı satırından tekil bir kontrol adı okunamadı ({where}): '{l[:60]}'")
            reds.add(name)
        stopped = bool(reds & CORE_FILE_CHECKS)
        return ("abs", "CI-3", reds, [], not stopped)

    def ci12(self, module: str) -> tuple:
        reds, passes = set(), []
        for lang in ("en", "tr"):
            data = self.head.read(SEARCH[lang])
            ok = data is not None and any(path.startswith("navigation/") and item["url"] == f"/Platform/{module}"
                                          for path, item in registry_items(data, SEARCH[lang]))
            name = f"platform-search.{lang}.json"
            if ok:
                passes.append(name)
            else:
                reds.add(name + ":yok")
        return ("abs", "CI-12", reds, passes, True)

    def evaluate_resx(self, base: str) -> list[tuple] | None:
        """CI-13 for one resx base (relative to frontend/Diten.Web/); None when no language file is left at HEAD."""
        present = {l: WEB + f"{base}.{l}.resx" for l in LANGS if self.head.has(WEB + f"{base}.{l}.resx")}
        if not present:
            return None
        required = ("en", "tr") if base.startswith("Resources/Views/Platform/") else LANGS
        reds, bad_langs = set(), set()
        for l in required:
            if l not in present:
                reds.add(f"{l}:dosya-yok")
                bad_langs.add(l)
        if "en" in present:
            self.head.prefetch(present.values())
            en = resx_keys(self.head.read(present["en"]), present["en"])
            for l, p in present.items():
                if l == "en":
                    continue
                for k in sorted(en - resx_keys(self.head.read(p), p)):
                    reds.add(f"{l}:anahtar-yok:{k}")
                    bad_langs.add(l)
        passes = [l for l in LANGS if l in present and l not in bad_langs]
        return [("abs", "CI-13", reds, passes, True)]

    def evaluate_shared_resource(self) -> list[tuple]:
        """CI-14: the checker's 'should be Title Case' lines (RC-22, both contexts) and the RC-24 key sets."""
        reds = set()
        for context in ("tenant", "platform"):
            checker = self.root / RESX_CHECKER
            if not checker.is_file():
                raise ToolError(f"resx denetçisi yok: {RESX_CHECKER}")
            try:
                done = subprocess.run([sys.executable, "-B", str(checker), ".", "--context", context], cwd=self.root,
                                      stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=CHECKER_TIMEOUT)
            except (subprocess.TimeoutExpired, OSError) as e:
                raise ToolError(f"resx denetçisi koşmadı ({context}, {type(e).__name__})")
            text = done.stdout.decode("utf-8", errors="replace")
            if done.returncode not in (0, 1) or not text.strip():
                raise ToolError(f"resx denetçisi araç hatası ({context}, çıkış {done.returncode})")
            lines = [l for l in text.splitlines() if "should be Title Case" in l]
            parsed = [TITLE_CASE_LINE.match(l) for l in lines]
            if not all(parsed):
                raise ToolError(f"resx denetçisinin Title Case satırı okunamadı ({context})")
            for m in parsed:
                reds.add(f"{m.group(1)}:title-case:{m.group(2)}")
        files = {l: WEB + f"Resources/SharedResource.{l}.resx" for l in LANGS}
        self.head.prefetch(files.values())
        if self.head.read(files["en"]) is None:
            raise ToolError("Resources/SharedResource.en.resx HEAD'de yok — anahtar kümesi kurulamaz")
        sets = {l: rc24_sets(text_of(self.head.read(p))) if self.head.read(p) is not None else (set(), [])
                for l, p in files.items()}
        en_keys, en_pairs = sets["en"]
        en_values = collections.defaultdict(set)
        for k, v in en_pairs:
            en_values[k].add(v)
        for l in NON_EN:
            keys, pairs = sets[l]
            for k in en_keys - keys:
                reds.add(f"{l}:missing:{k}")
            for k, v in pairs:
                if v != "" and v in en_values.get(k, ()):
                    reds.add(f"{l}:equal:{k}")
        passes = [l for l in NON_EN if not any(r.startswith(l + ":") for r in reds)]
        return [("abs", "CI-14", reds, passes, True)]

    def evaluate_pagehelp(self) -> dict[str, list[tuple]]:
        """CI-15: every code with Help.{CODE}.Purpose in en, RC-21 and RC-23 acceptance per code and language."""
        files = {l: WEB + f"Resources/PageHelpResource.{l}.resx" for l in LANGS}
        self.head.prefetch(list(files.values()) + [PAGEHELP_CATALOG])
        en = self.head.read(files["en"])
        catalog = self.head.read(PAGEHELP_CATALOG)
        if en is None or catalog is None:
            raise ToolError("PageHelpResource.en.resx ya da PageHelpCatalog.cs HEAD'de yok — kodlar okunamaz")
        codes = sorted(set(re.findall(r'name="Help\.([^".]+)\.Purpose"', text_of(en))))
        cat_lines = text_of(catalog).split("\n")
        texts = {l: text_of(self.head.read(p)) if self.head.read(p) is not None else None for l, p in files.items()}
        out = {}
        for code in codes:
            c = re.escape(code)
            related = [x for l in cat_lines if re.search(r'\["' + c + r'"\] = \[new\(', l)
                       for x in re.findall(r'new\("([A-Z0-9_-]*)"', l)]
            reds, passes = set(), []
            for lang in LANGS:
                text = texts[lang]
                if text is None:
                    reds.add(f"{lang}:dosya-yok")
                    continue
                lines = text.split("\n")
                count = lambda pattern: sum(1 for l in lines if re.search(pattern, l))
                mine = set()
                if count(r'name="Help\.' + c + r'\.(?:Purpose|Audience)"') != 2:
                    mine.add(f"{lang}:Purpose-Audience")
                for r in related:
                    if count(r'name="Help\.' + c + r'\.Related\.' + re.escape(r) + '"') != 1:
                        mine.add(f"{lang}:Related.{r}")
                if count(r'name="Help\.' + c + r'\.Related\.') != len(related):
                    mine.add(f"{lang}:related_total")
                steps = sorted({int(x) for x in re.findall(r'name="Help\.' + c + r'\.Step([0-9]+)"', text)})
                titles = sorted({int(x) for x in re.findall(r'name="Help\.' + c + r'\.Step([0-9]+)Title"', text)})
                k = len(steps)
                if not 1 <= k <= 5:
                    mine.add(f"{lang}:range_ok")
                if steps != list(range(1, k + 1)):
                    mine.add(f"{lang}:seq_diff")
                if steps != titles:
                    mine.add(f"{lang}:title_diff")
                reds |= mine
                if not mine:
                    passes.append(lang)
            out[f"PageHelp/{code}"] = [("abs", "CI-15", reds, passes, True)]
        return out

    # ----- comparison with the baseline (CI mode)
    def emit(self, page: str, results: list[tuple], closing: list[str]) -> None:
        for r in results:
            if r[0] == "note":
                self.out.append(r[1])
            elif r[0] == "abs":
                _, check, reds, passes, measured = r
                rows = {i: row for i, row in self.rows.get((page, check), {}).items() if check != "CI-9" or ":yok:" in i}
                for inst in sorted(reds):
                    if inst in rows:
                        self.out.append(f"TABAN {rows[inst]['fu']} {page} {check} {inst}")
                    else:
                        self.out.append(f"YENİ KIRMIZI {page} {check} {inst}")
                        self.new_red += 1
                for inst in passes:
                    self.out.append(f"PASS {page} {check} {inst}")
                for inst in sorted(set(rows) - set(reds)):
                    if measured:
                        closing.append(f"kapandı - satır silinmeli {page} {check} {inst}")
                    else:
                        self.out.append(f"ölçülmedi {page} {check} {inst} - {NOT_MEASURED_STOP}")
            else:
                _, check, counts = r
                rows = {i: row for i, row in self.rows.get((page, check), {}).items() if ":yok:" not in i}
                for path in sorted(counts):
                    n = counts[path]
                    b = int(rows[path]["count"]) if path in rows else 0
                    if n > b:
                        self.out.append(f"YENİ KIRMIZI {page} {check} {path} {n} +{n - b}")
                        self.new_red += 1
                    elif n == b and n > 0:
                        self.out.append(f"TABAN {rows[path]['fu']} {page} {check} {path} {n}")
                    elif 0 < n < b:
                        self.out.append(f"TABAN {rows[path]['fu']} {page} {check} {path} {n}")
                        closing.append(f"kapandı - satırı güncelle {b} -> {n} {page} {check} {path}")
                    elif n == 0 and b > 0:
                        closing.append(f"kapandı - satır silinmeli {page} {check} {path}")
                    else:
                        self.out.append(f"PASS {page} {check} {path} 0")
                for path in sorted(set(rows) - set(counts)):
                    closing.append(f"kapandı - satır silinmeli {page} {check} {path}")

    def removed(self, label: str, page: str) -> None:
        self.out.append(f"{label} {page}")
        for (p, check), rows in sorted(self.rows.items()):
            if p == page and not check.startswith("bilgi: "):
                for inst in sorted(rows):
                    self.out.append(f"kapandı - satır silinmeli {page} {check} {inst}")

    def bilgi(self, page: str) -> list[str]:
        return [f"{check} {page} {rows[i]['fu']} {rows[i]['owner']}" for (p, check), rows in sorted(self.rows.items())
                if p == page and check.startswith("bilgi: ") for i in sorted(rows)]

    # ----- CI mode
    def ci(self) -> int:
        diff = parse_name_status(git(self.root, "diff", "--no-color", "--name-status", "-z", "--find-renames",
                                     "--diff-filter=ACDMRT", f"{self.base_ref}...HEAD", "--"))
        pages: set[str] = set()
        registry_pages: set[str] = set()
        resx: set[str] = set()
        shared_resx = pagehelp = False
        registry_files: set[str] = set()
        shared: list[str] = []
        unmapped: list[str] = []
        added: dict[str, set[str]] = collections.defaultdict(set)
        for letter, paths in diff:
            for k, path in enumerate(paths):
                kind, value = self.map_path(path)
                if kind in ("page", "page_shared"):
                    pages.add(value)
                    is_new = letter == "A" or (letter in "RC" and k == 1)
                    if is_new and path.rsplit("/", 1)[-1] in ("_IndexL10n.cshtml", "_L10n.cshtml"):
                        added[value].add(path)
                    if kind == "page_shared" and path not in shared:
                        shared.append(path)
                elif kind == "fu18":
                    if path not in unmapped:
                        unmapped.append(path)
                elif kind == "resx":
                    resx.add(value)
                elif kind == "shared_resx":
                    shared_resx = True
                elif kind == "pagehelp":
                    pagehelp = True
                elif kind == "registry":
                    registry_files.add(path)
                elif kind == "shared":
                    if path not in shared:
                        shared.append(path)
        for path in shared:
            self.out.append(f"paylaşılan dosya, sayfa kontrolü yok {path}")
        for path in unmapped:
            self.out.append(f"{FU18_LINE} {path}")
        for path in sorted(registry_files):
            before = registry_keys(self.base.read(path), f"{path}@base")
            after = registry_keys(self.head.read(path), f"{path}@HEAD")
            changed = (before - after) + (after - before)
            for key in sorted(changed):
                url = json.loads(key[1])["url"]
                page = self.registry_page(url)
                if page is None:
                    self.out.append(f"kayıt sayfaya eşlenmedi {path} {key[0]} {key[1]}")
                else:
                    registry_pages.add(page)
        for page in sorted(pages | registry_pages):
            if not self.head.has_page(page):
                self.removed("sayfa kaldırıldı", page)
                continue
            closing: list[str] = []
            if page in pages:
                new_surface = self.base is not None and not self.base.has_page(page)
                results = self.evaluate_page(page, added.get(page, set()) if new_surface else ())
                self.emit(page, results, closing)
                self.out.extend(self.bilgi(page))
            else:
                self.emit(page, [self.ci12(page.split("/", 1)[1])], closing)   # a registry page is always Platform/{Module}
            self.out.extend(closing)
        for base in sorted(resx):
            results = self.evaluate_resx(base)
            if results is None:
                self.removed("kaynak kümesi kaldırıldı", base)
                continue
            closing = []
            self.emit(base, results, closing)
            self.out.extend(closing)
        if shared_resx:
            closing = []
            self.emit("Resources/SharedResource", self.evaluate_shared_resource(), closing)
            self.out.extend(closing)
        if pagehelp:
            for page, results in sorted(self.evaluate_pagehelp().items()):
                closing = []
                self.emit(page, results, closing)
                self.out.extend(closing)
        return 1 if self.new_red else 0

    def map_path(self, path: str) -> tuple[str, str | None]:
        if path in SEARCH.values():
            return "registry", path
        if path == PAGEHELP_CATALOG:
            return "pagehelp", None
        if not path.startswith(TREES):
            return "outside", None
        rel = path[len(WEB):]
        parts = rel.split("/")
        if parts[0] == "Views":
            if len(parts) >= 4 and parts[1] not in NOT_PAGE_AREAS:
                return "page", f"{parts[1]}/{parts[2]}"
            if len(parts) == 3 and parts[1] not in NOT_PAGE_AREAS:       # single-level page, any file type (rev 15)
                return ("page_shared" if parts[2] in FOLDER_LEVEL_VIEWS else "page"), parts[1]
            return "shared", path
        if parts[0] == "wwwroot":
            if len(parts) < 5 or parts[3] in NOT_PAGE_AREAS:
                return "shared", path
            top = parts[3]
            if top == "pages":                                           # FU-18 (1)
                return "fu18", path
            vtop = f"{WEB}Views/{top}/"
            if len(parts) >= 6:
                if self.either_has_dir(f"{vtop}{parts[4]}/"):
                    return "page", f"{top}/{parts[4]}"
                return ("fu18", path) if self.either_has_dir(vtop) else ("shared", path)   # FU-18 (2), (2b)
            if path.endswith(".js"):
                if self.either_has_direct(vtop):
                    return "page", top
                if self.either_has_dir(vtop):                            # FU-18 (3)
                    return "fu18", path
            return "shared", path
        if re.fullmatch(r"Resources/SharedResource\.[^/]*\.resx", rel):
            return "shared_resx", None
        if re.fullmatch(r"Resources/PageHelpResource\.[^/]*\.resx", rel):
            return "pagehelp", None
        if parts[0] == "Resources" and len(parts) >= 3 and parts[1] == "Views" and rel.endswith(".resx"):
            m = re.fullmatch(r"(.+)\.(" + "|".join(LANGS) + r")\.resx", rel)
            return "resx", (m.group(1) if m else rel[:-len(".resx")])
        return "shared", path

    def registry_page(self, url: str) -> str | None:
        if not url.startswith("/Platform/"):
            return None
        module = url[len("/Platform/"):].split("/", 1)[0]
        if not module or not (url == f"/Platform/{module}" or url.startswith(f"/Platform/{module}/")):
            return None
        return f"Platform/{module}" if self.either_has(f"{WEB}Views/Platform/{module}/Index.cshtml") else None

    # ----- draft mode
    def draft(self) -> list[tuple]:
        pages = sorted({("/".join(parts[1:3]) if len(parts) >= 4 else parts[1])
                        for parts in (p[len(WEB):].split("/") for p in self.head.sha if p.startswith(WEB + "Views/"))
                        if len(parts) >= 3 and parts[1] not in NOT_PAGE_AREAS})
        found: list[tuple[str, list[tuple]]] = [(page, self.evaluate_page(page)) for page in pages]
        bases = sorted({m.group(1) for p in self.head.sha
                        for m in [re.fullmatch(WEB + r"(Resources/Views/.+)\.(?:" + "|".join(LANGS) + r")\.resx", p)] if m})
        for base in bases:
            found.append((base, self.evaluate_resx(base) or []))
        found.append(("Resources/SharedResource", self.evaluate_shared_resource()))
        found.extend(sorted(self.evaluate_pagehelp().items()))
        rows = []
        for page, results in found:
            for r in results:
                reason = REASON if "/" in page else SINGLE_REASON      # FU-16 rows: decision tek-seviye-borc-01
                if r[0] == "abs":
                    for inst in r[2]:
                        rows.append((page, r[1], inst, "-") + (reason,) + owner_of(page, r[1], inst))
                elif r[0] == "line":
                    for path, n in r[2].items():
                        if n > 0:
                            rows.append((page, r[1], path, str(n)) + (reason,) + owner_of(page, r[1], path))
        rows.extend(BILGI_ROWS)
        order = lambda row: (row[0], int(row[1][3:]) if row[1].startswith("CI-") else 99, row[1], row[2])
        return sorted(rows, key=order)


def baseline_text(rows: list[tuple]) -> str:
    return "\t".join(HEADER) + "\n" + "".join("\t".join(r) + "\n" for r in rows)


def summary(rows: list[dict]) -> list[str]:
    n, m = collections.Counter(), collections.Counter()
    for r in rows:
        key = "bilgi" if r["check"].startswith("bilgi: ") else r["check"]
        n[key] += 1
        if re.fullmatch(r"[1-9][0-9]*", r["count"]):
            m[key] += int(r["count"])
    out = [f"{c} satır={n[c]} eşleşme={m[c]}" for c in [f"CI-{i}" for i in range(1, 16)] + ["bilgi"]]
    return out + [f"toplam satır={len(rows)} eşleşme={sum(m.values())}"]


def execute(root, mode: str, base: str | None = None) -> tuple[int, list[str], list[str]]:
    """One mode, in-process: (exit, stdout lines, stderr lines). ToolError -> exit 2."""
    t0 = time.monotonic()
    duration = lambda: f"süre: {time.monotonic() - t0:.1f} s"
    try:
        if mode == "ci":
            run = Run(root, "ci", base)
            code = run.ci()
            return code, run.out + [duration()], []
        if mode == "check":
            rows = read_baseline(root)
            return 0, [f"taban {BASELINE} geçerli"] + summary(rows), []
        if mode == "draft":
            run = Run(root, "draft")
            text = baseline_text(run.draft())
            parse_baseline(text)                       # the draft must itself be a well-formed baseline
            return 0, text.rstrip("\n").split("\n"), [duration()]
        raise ToolError(f"bilinmeyen kip {mode}")
    except ToolError as e:
        return 2, ([duration()] if mode == "ci" else []), [f"HATA: {e}"]


# ------------------------------------------------------------------------------------------------------ self-test
def self_test() -> int:
    """Every case runs the selector against a throw-away git repository outside the repository (tempfile)."""
    import shutil
    global GIT_ENV_EXTRA
    saved_env = dict(GIT_ENV_EXTRA)
    GIT_ENV_EXTRA = {"GIT_CONFIG_GLOBAL": os.devnull, "GIT_CONFIG_SYSTEM": os.devnull, "GIT_AUTHOR_NAME": "uic-self-test",
                     "GIT_AUTHOR_EMAIL": "uic-self-test@localhost", "GIT_COMMITTER_NAME": "uic-self-test",
                     "GIT_COMMITTER_EMAIL": "uic-self-test@localhost"}
    cases: list[tuple[str, bool]] = []
    tmp = tempfile.TemporaryDirectory(prefix="uic-self-test-")
    counter = [0]
    W = WEB
    stub = (
        "import sys, pathlib\n"
        "a = sys.argv[sys.argv.index('--area') + 1]; m = sys.argv[sys.argv.index('--module') + 1]\n"
        "d = pathlib.Path(sys.argv[1]) / 'frontend/Diten.Web/Views' / a / m\n"
        "miss = [n for n in ('Index.cshtml', '_Filter.cshtml', '_DataTable.cshtml', '_IndexL10n.cshtml') if not (d / n).is_file()]\n"
        "if miss:\n"
        "    for i, n in enumerate(miss, 1): print(f'{i}) {n} exists — Missing: {d / n}')\n"
        "    sys.exit(1)\n"
        "t = (d / 'Index.cshtml').read_text(encoding='utf-8')\n"
        "if 'GARBAGE' in t: print('beklenmeyen bir satır'); sys.exit(0)\n"
        "g = [x for k, x in (('GAP-OLD', 'Old gap — ayrıntı'), ('GAP-NEW', 'New gap — ayrıntı')) if k in t]\n"
        "for i, x in enumerate(g, 1): print(f'{i}) {x}')\n"
        "if g: sys.exit(1)\n"
        "print('[pack] execution/x.md declares data_mode: client')\n"
        "print('GAPS-OK: 5 kontrol, 0 sapma')\n")
    layout = '@{ Layout = "_LayoutTenantShell"; }\n'

    def resx(pairs):
        return ('<?xml version="1.0" encoding="utf-8"?>\n<root>\n'
                + "".join(f'  <data name="{k}" xml:space="preserve"><value>{v}</value></data>\n' for k, v in pairs) + "</root>\n")
    reg = {"navigation": {"G": [{"name": "Tenants", "url": "/Platform/Tenants"}]},
           "suggestions": {"S": [{"name": "Tenants", "url": "/Platform/Tenants", "k": 1}]}}
    base_rows = [
        ("Ar/ListA", "CI-3", "Old gap", "-"), ("Ar/ListA", "CI-5", "wwwroot/assets/js/Ar/ListA/index.js", "1"),
        ("Ar/Plain", "CI-6", "Views/Ar/Plain/Index.cshtml", "2"),
        ("Platform/Other", "CI-12", "platform-search.en.json:yok", "-"),
        ("Platform/Other", "CI-12", "platform-search.tr.json:yok", "-"), ("Ar/ListA", "bilgi: deneme satırı", "-", "-")]
    baseline = baseline_text([r + ("r", "FU-12" if r[1].startswith("bilgi") else "FU-14", "o") for r in base_rows])
    help_pairs = [("Help.ABC.Purpose", "p"), ("Help.ABC.Audience", "a"), ("Help.ABC.Step1", "s"), ("Help.ABC.Step1Title", "t")]
    files = {
        W + "Views/Ar/ListA/Index.cshtml": layout + "@* GAP-OLD *@\n",
        W + "Views/Ar/ListA/_DataTable.cshtml": "<table></table>\n",
        W + "Views/Ar/ListA/_Filter.cshtml": "<div></div>\n",
        W + "Views/Ar/ListA/_IndexL10n.cshtml": "<script></script>\n",
        W + "Views/Ar/ListA/_CreateEditOffcanvas.cshtml": "<form></form>\n",
        W + "wwwroot/assets/js/Ar/ListA/index.js": "var cfg = { savedView: true };\nvar a = DataTable({ x: 1 });\n",
        W + "Views/Ar/Plain/Index.cshtml": layout + '<span style="a"></span><b style="b"></b>\n',
        W + "wwwroot/assets/js/Ar/Plain/main.js": "x = 1;\n",
        W + "Views/Platform/Tenants/Index.cshtml": layout,
        W + "Views/Platform/Other/Index.cshtml": layout,
        W + "Views/Shared/_X.cshtml": "<i></i>\n",
        W + "wwwroot/assets/js/shared.js": "s = 1;\n",
        SEARCH["en"]: json.dumps(reg, indent=2) + "\n",
        SEARCH["tr"]: json.dumps(reg, indent=2) + "\n",
        PAGEHELP_CATALOG: "// katalog\n",
        BASELINE: baseline,
        VERIFIER: stub,
    }
    for l in LANGS:
        files[W + f"Resources/Views/Ar/ListA/ListAIndex.{l}.resx"] = resx([("A", f"{l}-a"), ("B", f"{l}-b")])
        files[W + f"Resources/SharedResource.{l}.resx"] = resx([("K1", f"{l}-one"), ("K2", f"{l}-two")])
        files[W + f"Resources/PageHelpResource.{l}.resx"] = resx(help_pairs)
    files[PACK] = (ROOT / PACK).read_text(encoding="utf-8")
    files[RESX_CHECKER] = (ROOT / RESX_CHECKER).read_text(encoding="utf-8")

    def write(root, rel, text):
        p = Path(root) / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(text, encoding="utf-8")

    def tg(root, *args):
        return git(root, *args).decode().strip()

    def repo(extra=None):
        counter[0] += 1
        root = Path(tmp.name) / f"r{counter[0]}"
        root.mkdir()
        tg(root, "init", "-q", "-b", "main")
        for rel, text in {**files, **(extra or {})}.items():
            write(root, rel, text)
        tg(root, "add", "-A")
        tg(root, "-c", "commit.gpgsign=false", "commit", "-q", "-m", "taban")
        return root, tg(root, "rev-parse", "HEAD")

    def commit(root, msg="değişiklik"):
        tg(root, "add", "-A")
        tg(root, "-c", "commit.gpgsign=false", "commit", "-q", "-m", msg)
        return tg(root, "rev-parse", "HEAD")

    def edit(root, rel, fn):
        p = Path(root) / rel
        p.write_text(fn(p.read_text(encoding="utf-8")), encoding="utf-8")

    def ci(root, base):
        return execute(root, "ci", base)

    def has(lines, text):
        return any(text == l or l.startswith(text) for l in lines)

    J = W + "wwwroot/assets/js/Ar/ListA/index.js"
    PL = W + "Views/Ar/Plain/Index.cshtml"
    try:
        # --- three exits and the duration line
        r, b0 = repo()
        code, out, _ = ci(r, "HEAD")
        cases.append(("--base HEAD (empty diff) -> exit 0, the only line is the duration 'süre: … s'",
                      code == 0 and len(out) == 1 and re.fullmatch(r"süre: [0-9]+\.[0-9] s", out[0]) is not None))
        edit(r, W + "wwwroot/assets/js/Ar/Plain/main.js", lambda t: t + "y = 2;\n")
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("exit 0: a changed page whose only reds are baseline rows -> TABAN with its FU, PASS lines, exit 0",
                      code == 0 and has(out, "TABAN FU-14 Ar/Plain CI-6 Views/Ar/Plain/Index.cshtml 2")
                      and has(out, "PASS Ar/Plain CI-1 Views/Ar/Plain/Index.cshtml")
                      and has(out, "PASS Ar/Plain CI-4 wwwroot/assets/js/Ar/Plain/main.js 0")))
        code, out, err = ci(r, "0" * 40)
        cases.append(("exit 2: a missing base ref -> tool error, never '0 changed pages'", code == 2 and bool(err)))
        code, out, err = ci(r, "--all")
        cases.append(("exit 2: a base that is an option ('--all') is refused — no all-pages value", code == 2))
        # --- the match count (part 2)
        for label, fn, expect_code, expect in (
                ("a second match on the line of the baselined match -> +1",
                 lambda t: t.replace("var a = DataTable({ x: 1 });", "var a = DataTable({ x: 1 }); new DataTable(q, {});"),
                 1, "YENİ KIRMIZI Ar/ListA CI-5 wwwroot/assets/js/Ar/ListA/index.js 2 +1"),
                ("a byte copy of the violating line on a new line -> +1",
                 lambda t: t + "var a = DataTable({ x: 1 });\n", 1, "YENİ KIRMIZI Ar/ListA CI-5 wwwroot/assets/js/Ar/ListA/index.js 2 +1"),
                ("a line inserted above the violation -> unchanged, TABAN",
                 lambda t: "// üst satır\n" + t, 0, "TABAN FU-14 Ar/ListA CI-5 wwwroot/assets/js/Ar/ListA/index.js 1"),
                ("a re-indented violating line -> unchanged, TABAN",
                 lambda t: t.replace("var a = DataTable", "        var a = DataTable"), 0,
                 "TABAN FU-14 Ar/ListA CI-5 wwwroot/assets/js/Ar/ListA/index.js 1"),
                ("the violation moved inside the file -> unchanged, TABAN",
                 lambda t: "var a = DataTable({ x: 1 });\nvar cfg = { savedView: true };\n", 0,
                 "TABAN FU-14 Ar/ListA CI-5 wwwroot/assets/js/Ar/ListA/index.js 1"),
                ("the violating line edited without a new match -> unchanged, TABAN",
                 lambda t: t.replace("{ x: 1 }", "{ x: 2, z: 3 }"), 0, "TABAN FU-14 Ar/ListA CI-5 wwwroot/assets/js/Ar/ListA/index.js 1"),
                ("one violation removed and one added in the same file (the written limit) -> TABAN",
                 lambda t: "var cfg = { savedView: true };\nvar b = new DataTable(el, { y: 1 });\n", 0,
                 "TABAN FU-14 Ar/ListA CI-5 wwwroot/assets/js/Ar/ListA/index.js 1")):
            r, b0 = repo()
            edit(r, J, fn)
            commit(r)
            code, out, _ = ci(r, b0)
            cases.append((f"count: {label}", code == expect_code and has(out, expect)))
        r, b0 = repo()
        edit(r, J, lambda t: t.replace("var a = DataTable({ x: 1 });", "var a = DataTable({ x: 1 }); new DataTable(y,\noptions);"))
        prep = commit(r)
        code1, out1, _ = ci(r, b0)
        edit(r, J, lambda t: t.replace("\noptions);", "\n{});"))
        commit(r)
        code2, out2, _ = ci(r, prep)
        cases.append(("count: a match completed on its continuation line -> +1 (the preparatory commit itself stays TABAN)",
                      code1 == 0 and has(out1, "TABAN FU-14 Ar/ListA CI-5 wwwroot/assets/js/Ar/ListA/index.js 1")
                      and code2 == 1 and has(out2, "YENİ KIRMIZI Ar/ListA CI-5 wwwroot/assets/js/Ar/ListA/index.js 2 +1")))
        r, b0 = repo()
        edit(r, PL, lambda t: t.replace('<b style="b"></b>', "<b></b>"))
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("count fell (2 -> 1) -> TABAN 1 and 'kapandı - satırı güncelle 2 -> 1', exit 0",
                      code == 0 and has(out, "TABAN FU-14 Ar/Plain CI-6 Views/Ar/Plain/Index.cshtml 1")
                      and has(out, "kapandı - satırı güncelle 2 -> 1 Ar/Plain CI-6 Views/Ar/Plain/Index.cshtml")))
        r, b0 = repo()
        edit(r, PL, lambda t: t.replace(' style="a"', "").replace(' style="b"', ""))
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("count fell to 0 -> 'kapandı - satır silinmeli', exit 0",
                      code == 0 and has(out, "kapandı - satır silinmeli Ar/Plain CI-6 Views/Ar/Plain/Index.cshtml")))
        # --- the mapping
        r, b0 = repo()
        tg(r, "rm", "-q", W + "Views/Ar/ListA/_Filter.cshtml")
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("mapping: a deleted path selects its page; the verifier stops -> YENİ KIRMIZI '_Filter.cshtml exists', "
                      "the page's other CI-3 row 'ölçülmedi', never 'kapandı'",
                      code == 1 and has(out, "YENİ KIRMIZI Ar/ListA CI-3 _Filter.cshtml exists")
                      and has(out, f"ölçülmedi Ar/ListA CI-3 Old gap - {NOT_MEASURED_STOP}")
                      and not any("kapandı" in l and " CI-3 " in l for l in out)))
        r, b0 = repo()
        (Path(r) / W / "Views/Ar/NewP").mkdir(parents=True)
        tg(r, "mv", PL, W + "Views/Ar/NewP/Index.cshtml")
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("mapping: a rename maps both paths — the old page removed ('sayfa kaldırıldı', its rows 'kapandı - "
                      "satır silinmeli'), the new page checked as added (YENİ KIRMIZI)",
                      code == 1 and has(out, "sayfa kaldırıldı Ar/Plain")
                      and has(out, "kapandı - satır silinmeli Ar/Plain CI-6 Views/Ar/Plain/Index.cshtml")
                      and has(out, "YENİ KIRMIZI Ar/NewP CI-6 Views/Ar/NewP/Index.cshtml 2 +2")))

        def reg_case(label, lang, fn, expect_code, expects, absent=()):
            r, b0 = repo()
            doc = json.loads((Path(r) / SEARCH[lang]).read_text(encoding="utf-8"))
            fn(doc)
            write(r, SEARCH[lang], json.dumps(doc, ensure_ascii=False, indent=2) + "\n")
            commit(r)
            code, out, _ = ci(r, b0)
            cases.append((f"registry: {label}", code == expect_code and all(has(out, e) for e in expects)
                          and not any(has(out, a) for a in absent)))
        reg_case("an entry added (Platform/Other, tr) -> the page selected; en still red (TABAN), tr closed",
                 "tr", lambda d: d["navigation"]["G"].append({"name": "O", "url": "/Platform/Other"}), 0,
                 ["TABAN FU-14 Platform/Other CI-12 platform-search.en.json:yok", "PASS Platform/Other CI-12 platform-search.tr.json",
                  "kapandı - satır silinmeli Platform/Other CI-12 platform-search.tr.json:yok"])
        reg_case("both entries of a page removed (tr) -> YENİ KIRMIZI Platform/Tenants CI-12 platform-search.tr.json:yok",
                 "tr", lambda d: (d["navigation"]["G"].clear(), d["suggestions"]["S"].clear()), 1,
                 ["YENİ KIRMIZI Platform/Tenants CI-12 platform-search.tr.json:yok"])
        reg_case("only the navigation item removed, the suggestions item kept -> page selected, YENİ KIRMIZI",
                 "tr", lambda d: d["navigation"]["G"].clear(), 1, ["YENİ KIRMIZI Platform/Tenants CI-12 platform-search.tr.json:yok"])
        reg_case("an item object edited (keywords) -> the page selected", "en",
                 lambda d: d["navigation"]["G"][0].update(keywords=["x"]), 0,
                 ["PASS Platform/Tenants CI-12 platform-search.en.json", "PASS Platform/Tenants CI-12 platform-search.tr.json"])
        reg_case("a group renamed -> the page selected", "en", lambda d: d["navigation"].update(G2=d["navigation"].pop("G")), 0,
                 ["PASS Platform/Tenants CI-12 platform-search.en.json"])
        reg_case("an entry whose url maps to no page -> 'kayıt sayfaya eşlenmedi'", "en",
                 lambda d: d["navigation"]["G"].append({"url": "/Platform/Nope"}), 0, [f"kayıt sayfaya eşlenmedi {SEARCH['en']} navigation/G"])
        reg_case("a page selected only through the registry runs CI-12 only", "en",
                 lambda d: d["navigation"]["G"][0].update(name="X"), 0, ["PASS Platform/Tenants CI-12"],
                 absent=["PASS Platform/Tenants CI-1 "])
        for label, text in (("not JSON", "{"), ("without 'suggestions'", json.dumps({"navigation": {}})),
                            ("an item without a string url", json.dumps({"navigation": {"G": [{"url": 1}]}, "suggestions": {}}))):
            r, b0 = repo()
            write(r, SEARCH["en"], text)
            commit(r)
            code, out, err = ci(r, b0)
            cases.append((f"exit 2: a registry file {label} -> tool error", code == 2 and bool(err)))
        r, b0 = repo()
        write(r, W + "wwwroot/assets/js/shared.js", "s = 2;\n")
        write(r, W + "Views/Shared/_X.cshtml", "<i>2</i>\n")
        write(r, W + "wwwroot/assets/js/Zz/Nope/a.js", "z = 1;\n")
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("mapping: a shared file and a JS file with no Views page -> 'paylaşılan dosya, sayfa kontrolü yok', exit 0",
                      code == 0 and has(out, f"paylaşılan dosya, sayfa kontrolü yok {W}wwwroot/assets/js/shared.js")
                      and has(out, f"paylaşılan dosya, sayfa kontrolü yok {W}Views/Shared/_X.cshtml")
                      and has(out, f"paylaşılan dosya, sayfa kontrolü yok {W}wwwroot/assets/js/Zz/Nope/a.js")))
        # --- CI-11: new surface only (rev 14)
        r, b0 = repo()
        write(r, W + "Views/Ar/Fresh/_IndexL10n.cshtml", "<script>@Localizer.GetAllStrings(false)</script>\n")
        write(r, W + "Views/Ar/Fresh2/_L10n.cshtml", "<script>@Localizer.GetAllStrings(true)</script>\n")
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("CI-11: a partial added to a new surface without GetAllStrings(true) -> YENİ KIRMIZI; with it -> PASS",
                      code == 1 and has(out, "YENİ KIRMIZI Ar/Fresh CI-11 Views/Ar/Fresh/_IndexL10n.cshtml:yok:GetAllStrings")
                      and has(out, "PASS Ar/Fresh2 CI-11 Views/Ar/Fresh2/_L10n.cshtml")))
        r, b0 = repo()
        tg(r, "rm", "-q", W + "Views/Ar/ListA/_IndexL10n.cshtml")
        sab = commit(r)
        tg(r, "-c", "commit.gpgsign=false", "revert", "--no-edit", "HEAD")
        code, out, _ = ci(r, sab)
        cases.append(("CI-11: a partial re-added to a page that has files at the base (AC-18 (c) revert) -> no CI-11, exit 0",
                      code == 0 and not any(" CI-11 " in l for l in out) and has(out, "TABAN FU-14 Ar/ListA CI-3 Old gap")))
        # --- the verifier's output read fail-closed
        r, b0 = repo()
        edit(r, W + "Views/Ar/ListA/Index.cshtml", lambda t: t + "@* GARBAGE *@\n")
        commit(r)
        code, out, err = ci(r, b0)
        cases.append(("exit 2: verifier output the selector cannot classify -> tool error", code == 2 and any("sınıflandırılamadı" in e for e in err)))
        r, b0 = repo()
        edit(r, W + "Views/Ar/ListA/Index.cshtml", lambda t: t + "@* GAP-NEW *@\n")
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("CI-3: a new verifier line on a page with a baseline row -> YENİ KIRMIZI, the old row TABAN",
                      code == 1 and has(out, "YENİ KIRMIZI Ar/ListA CI-3 New gap") and has(out, "TABAN FU-14 Ar/ListA CI-3 Old gap")
                      and has(out, "bilgi: deneme satırı Ar/ListA FU-12 o")))
        # --- resx (CI-13), SharedResource (CI-14), page help (CI-15)
        r, b0 = repo()
        tg(r, "rm", "-q", W + "Resources/Views/Ar/ListA/ListAIndex.tr.resx")
        write(r, W + "Resources/SharedResource.fr.resx", resx([("K1", "fr-one")]))
        write(r, W + "Resources/PageHelpResource.fr.resx", resx(help_pairs[:3]))
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("CI-13 / CI-14 / CI-15: a language file deleted, a SharedResource key removed, a step title removed -> "
                      "each a named YENİ KIRMIZI",
                      code == 1 and has(out, "YENİ KIRMIZI Resources/Views/Ar/ListA/ListAIndex CI-13 tr:dosya-yok")
                      and has(out, "YENİ KIRMIZI Resources/SharedResource CI-14 fr:missing:K2")
                      and has(out, "YENİ KIRMIZI PageHelp/ABC CI-15 fr:title_diff")))
        # --- the baseline reader
        good = parse_baseline(baseline)
        cases.append(("baseline reader: the fixture baseline reads (6 rows)", len(good) == 6))
        row = "Ar/ListA\tCI-5\twwwroot/assets/js/Ar/ListA/index.js\t1\tr\tFU-14\to"
        head = "\t".join(HEADER) + "\n"
        for label, text in (
                ("an empty cell", head + row.replace("\tr\t", "\t\t") + "\n"),
                ("a duplicate page + check + instance", head + row + "\n" + row.replace("\t1\t", "\t2\t") + "\n"),
                ("a malformed instance (a line number)", head + row.replace("index.js", "index.js:12") + "\n"),
                ("a malformed instance (another page's file)", head + row.replace("Ar/ListA/index.js", "Ar/Plain/main.js") + "\n"),
                ("a line-check count 0", head + row.replace("\t1\t", "\t0\t") + "\n"),
                ("a line-check count that is not an integer", head + row.replace("\t1\t", "\tx\t") + "\n"),
                ("a line-check count with a leading zero", head + row.replace("\t1\t", "\t01\t") + "\n"),
                ("a line-check count with a sign", head + row.replace("\t1\t", "\t+1\t") + "\n"),
                ("another row's count other than '-'", head + "Platform/Other\tCI-12\tplatform-search.en.json:yok\t1\tr\tFU-14\to\n"),
                ("a malformed FU id", head + row.replace("FU-14", "FU14") + "\n"),
                ("a check outside CI-1…CI-15", head + row.replace("CI-5", "CI-16") + "\n"),
                ("a pattern key written as a check (CI-6v)", head + row.replace("CI-5", "CI-6v") + "\n"),
                ("a header that is not exact", head.replace("owner", "sahip") + row + "\n"),
                ("a row with a missing cell", head + row.rsplit("\t", 1)[0] + "\n")):
            try:
                parse_baseline(text)
                ok = False
            except ToolError:
                ok = True
            cases.append((f"baseline reader: {label} -> exit 2", ok))
        r, b0 = repo()
        write(r, BASELINE, baseline + "Ar/ListA\tCI-5\twwwroot/assets/js/Ar/ListA/index.js\t3\tr\tFU-14\to\n")
        edit(r, W + "wwwroot/assets/js/Ar/Plain/main.js", lambda t: t + "y = 2;\n")
        commit(r)
        code, out, err = ci(r, b0)
        cases.append(("exit 2: CI mode on a malformed baseline (duplicate key) -> tool error", code == 2 and any("tekrar" in e for e in err)))
        # --- git: a status letter outside ACDMRT, a directory that is not a repository
        for raw in (b"X\0a\0", b"U\0a\0", b"R100\0a\0"):
            try:
                parse_name_status(raw)
                ok = False
            except ToolError:
                ok = True
            cases.append((f"exit 2: git diff output {raw!r} (unknown letter / missing path) -> tool error", ok))
        cases.append(("git diff output 'R086 old new' and 'D path' are read with both rename paths",
                      parse_name_status(b"R086\0a/x\0b/x\0D\0c\0") == [("R", ["a/x", "b/x"]), ("D", ["c"])]))
        notrepo = Path(tmp.name) / "not-a-repo"
        notrepo.mkdir()
        write(notrepo, PACK, files[PACK])
        code, out, err = execute(notrepo, "ci", "HEAD")
        cases.append(("exit 2: not a git repository -> tool error", code == 2))
        # --- the pattern block (rev 12, F-24)
        try:
            block = parse_pattern_block((ROOT / PACK).read_text(encoding="utf-8"))
            ok = block == OWN_PATTERNS and all(re.compile(v) for v in block.values())
        except ToolError:
            ok = False
        cases.append(("pattern block: every key of the pack's block present, compiling with re.compile, and the selector's own "
                      "pattern for each key the same string", ok))
        block_text = f"{PATTERN_HEADING}\n\n```text\n" + "".join(f"{k}\t{v}\n" for k, v in OWN_PATTERNS.items()) + "```\n"
        for label, text in (("a missing key", block_text.replace("CI-10\tcolReorder\n", "")),
                            ("a pattern that does not compile", block_text.replace("createCrudTable\\(", "createCrudTable(")),
                            ("a key written twice", block_text.replace("CI-10\tcolReorder\n", "CI-10\tcolReorder\nCI-10\tcolReorder\n"))):
            try:
                parse_pattern_block(text)
                ok = False
            except ToolError:
                ok = True
            cases.append((f"pattern block: {label} -> refused", ok))
        changed = parse_pattern_block(block_text.replace("document\\.cookie", "document.cookie"))
        cases.append(("pattern block: a different string for a key -> the self-test comparison fails", changed != OWN_PATTERNS))
        # --- the draft and the modes
        r, b0 = repo()
        code, out, err = execute(r, "draft")
        rows = parse_baseline("\n".join(out) + "\n") if code == 0 else []
        keys = {(x["page"], x["check"], x["instance"], x["count"]) for x in rows}
        cases.append(("draft: the fixture's reds as rows (CI-5 count 1, CI-6 count 2, CI-3 'Old gap', CI-12 en + tr) plus the "
                      "two bilgi rows; no CI-11 row",
                      code == 0 and ("Ar/ListA", "CI-5", "wwwroot/assets/js/Ar/ListA/index.js", "1") in keys
                      and ("Ar/Plain", "CI-6", "Views/Ar/Plain/Index.cshtml", "2") in keys and ("Ar/ListA", "CI-3", "Old gap", "-") in keys
                      and ("Platform/Other", "CI-12", "platform-search.tr.json:yok", "-") in keys
                      and sum(1 for x in rows if x["check"].startswith("bilgi: ")) == 2 and not any(x["check"] == "CI-11" for x in rows)))
        code, out, err = execute(r, "check")
        cases.append(("--check-baseline on a well-formed baseline -> exit 0 with per-CI rows and matches",
                      code == 0 and "CI-5 satır=1 eşleşme=1" in out and "CI-6 satır=1 eşleşme=2" in out))
        write(r, W + "Views/Ar/ListA/Index.cshtml", "kirli\n")
        code, out, err = ci(r, "HEAD")
        cases.append(("exit 2: a working tree that is not HEAD under frontend/Diten.Web -> tool error", code == 2))
        write(r, W + "Views/Ar/ListA/Index.cshtml", files[W + "Views/Ar/ListA/Index.cshtml"])
        for argv in ([], ["--base", "HEAD", "--draft-baseline"], ["--self-test", "--check-baseline"], ["--base"], ["--all"]):
            done = subprocess.run([sys.executable, "-B", str(Path(__file__).resolve())] + argv, cwd=r, capture_output=True)
            cases.append((f"modes: {argv or 'no mode'} -> exit 2", done.returncode == 2))
        # --- single-level pages (pack rev 15, AC-21 (g)): every case a real run on a throw-away repository
        M_V, M_J, FU18 = W + "Views/Mod/Index.cshtml", W + "wwwroot/assets/js/Mod/app.js", FU18_LINE + " "
        SHARED_LINE = "paylaşılan dosya, sayfa kontrolü yok "
        mod = {M_V: layout, W + "Views/Mod/ModIndex.cs": "// marker\n", M_J: "a = 1;\n"}

        def no_check_lines(lines):
            return not any(re.search(r" CI-[0-9]+ ", l) for l in lines)

        r, b0 = repo(mod)
        edit(r, M_V, lambda t: t + '<span class="spinner-border"></span>\n')
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("AC-21g01 a view file directly in the folder -> page {Module}, a new match is YENİ KIRMIZI, not a shared file",
                      code == 1 and "YENİ KIRMIZI Mod CI-2 Views/Mod/Index.cshtml 1 +1" in out and not any(SHARED_LINE in l for l in out)))
        r, b0 = repo(mod)
        edit(r, W + "Views/Mod/ModIndex.cs", lambda t: t + "// 2\n")
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("AC-21g01 a non-.cshtml file directly in the folder selects the page and no check reads it",
                      code == 0 and "PASS Mod CI-1 Views/Mod/Index.cshtml" in out and not any("ModIndex.cs" in l for l in out)))
        r, b0 = repo(mod)
        edit(r, M_J, lambda t: t + "alert('x');\n")
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("AC-21g02 JS directly in the folder, a direct view at HEAD and at the base -> page {Module}, YENİ KIRMIZI",
                      code == 1 and "YENİ KIRMIZI Mod CI-7 wwwroot/assets/js/Mod/app.js 1 +1" in out and not any(FU18 in l for l in out)))
        r, b0 = repo(mod)
        tg(r, "rm", "-q", M_V, W + "Views/Mod/ModIndex.cs")
        edit(r, M_J, lambda t: t + "b = 2;\n")
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("AC-21g02 JS directly in the folder, the direct view only at the base -> the JS still selects page {Module}",
                      code == 0 and "sayfa kaldırıldı Mod" in out and not any(FU18 in l or SHARED_LINE in l for l in out)))
        r, b0 = repo({W + "Views/Sub1/Inner/Index.cshtml": layout, W + "wwwroot/assets/js/Sub1/helper.js": "a = 1;\n"})
        edit(r, W + "wwwroot/assets/js/Sub1/helper.js", lambda t: t + "alert('x');\n")
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("AC-21g03 JS directly in the folder, no direct view at HEAD or at the base -> its FU-18 line, no page",
                      code == 0 and f"{FU18}{W}wwwroot/assets/js/Sub1/helper.js" in out and no_check_lines(out)))
        r, b0 = repo({W + "Views/Sub1/Index.cshtml": layout, W + "Views/Sub1/Inner/Index.cshtml": layout,
                      W + "wwwroot/assets/js/Sub1/helper.js": "a = 1;\n"})
        tg(r, "rm", "-q", W + "Views/Sub1/Index.cshtml")
        edit(r, W + "wwwroot/assets/js/Sub1/helper.js", lambda t: t + "b = 2;\n")
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("AC-21g03 JS directly in the folder, the direct view only at the base, a subfolder page stays -> page, not FU-18",
                      code == 0 and "sayfa kaldırıldı Sub1" in out and not any(FU18 in l for l in out)))
        both = {**mod, W + "Views/Mod/Sub/Index.cshtml": layout + '<span class="spinner-border"></span>\n',
                W + "wwwroot/assets/js/Mod/Sub/index.js": "alert('x');\n"}
        r, b0 = repo(both)
        edit(r, M_J, lambda t: t + "b = 2;\n")
        commit(r)
        code1, out1, _ = ci(r, b0)
        r, b0 = repo(both)
        edit(r, W + "Views/Mod/Sub/Index.cshtml", lambda t: t + '<span class="spinner-border"></span>\n')
        commit(r)
        code2, out2, _ = ci(r, b0)
        cases.append(("AC-21g04 one folder, a single-level and a two-level page -> each reads only its own files",
                      code1 == 0 and "PASS Mod CI-4 wwwroot/assets/js/Mod/app.js 0" in out1 and not any("Mod/Sub" in l for l in out1)
                      and code2 == 1 and "YENİ KIRMIZI Mod/Sub CI-2 Views/Mod/Sub/Index.cshtml 2 +2" in out2
                      and not any(l.startswith(("PASS Mod ", "YENİ KIRMIZI Mod ")) for l in out2)))
        na = [W + "Views/Shared/_Y.cshtml", W + "Views/Archive/z.cshtml", W + "wwwroot/assets/js/Shared/x.js",
              W + "wwwroot/assets/js/Archive/y.js", W + "wwwroot/assets/js/Shared/Sub/x.js"]
        r, b0 = repo({p: "alert('x'); spinner-border\n" for p in na})
        for p in na:
            edit(r, p, lambda t: t + "alert('y');\n")
        commit(r)
        code, out, _ = ci(r, b0)
        dcode, dout, _ = execute(r, "draft")
        cases.append(("AC-21g05 NOT_PAGE_AREAS (Shared, Archive): views and JS are the shared line, never a page, never FU-18, no draft row",
                      code == 0 and all(f"{SHARED_LINE}{p}" in out for p in na) and no_check_lines(out)
                      and not any(FU18 in l for l in out)
                      and dcode == 0 and not any(l.split("\t")[0] in NOT_PAGE_AREAS for l in dout)))
        pg = [W + "wwwroot/assets/js/pages/foo/a.js", W + "wwwroot/assets/js/pages/b.js"]
        r, b0 = repo({p: "alert('x');\n" for p in pg})
        for p in pg:
            edit(r, p, lambda t: t + "c = 3;\n")
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("AC-21g06 pages/** -> its FU-18 line for each path, not the shared line, no page",
                      code == 0 and all(f"{FU18}{p}" in out for p in pg) and no_check_lines(out) and not any(SHARED_LINE in l for l in out)))
        deep = [W + "wwwroot/assets/js/Mod/fixtures/a.js", W + "wwwroot/assets/js/Mod/fixtures/more/b.js"]
        r, b0 = repo({**mod, **{p: "alert('x');\n" for p in deep}})
        for p in deep:
            edit(r, p, lambda t: t + "c = 3;\n")
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("AC-21g07 js/{Module}/{Sub}/ with no view folder -> its FU-18 line, the single-level page is not selected",
                      code == 0 and all(f"{FU18}{p}" in out for p in deep) and no_check_lines(out)))
        area_js = [W + "wwwroot/assets/js/Ar/direct.js", W + "wwwroot/assets/js/Ar/NoPage/x.js"]
        r, b0 = repo({p: "alert('x');\n" for p in area_js})
        for p in area_js:
            edit(r, p, lambda t: t + "c = 3;\n")
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("AC-21g08 an area folder holding only subfolders: JS directly in it and in a subfolder with no view -> FU-18 lines",
                      code == 0 and all(f"{FU18}{p}" in out for p in area_js) and no_check_lines(out) and not any(SHARED_LINE in l for l in out)))
        for name in FOLDER_LEVEL_VIEWS:
            p = W + f"Views/Mod/{name}"
            r, b0 = repo({**mod, p: layout})
            edit(r, p, lambda t: t + "@* 2 *@\n")
            commit(r)
            code, out, _ = ci(r, b0)
            cases.append((f"AC-21g09 a folder-level {name} -> a file of page {{Module}} (its checks run) and the shared-file line",
                          code == 0 and f"{SHARED_LINE}{p}" in out and f"PASS Mod CI-2 Views/Mod/{name} 0" in out
                          and not any(f" CI-1 Views/Mod/{name}" in l for l in out)))
        root_view = W + "Views/_ViewStart.cshtml"
        r, b0 = repo({root_view: layout})
        edit(r, root_view, lambda t: t + "@* 2 *@\n")
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("AC-21g09 the root Views/_ViewStart.cshtml stays shared only", code == 0 and f"{SHARED_LINE}{root_view}" in out and no_check_lines(out)))

        def fu16(*rows):
            return baseline + "".join("\t".join(x) + "\tr\tFU-16\to\n" for x in rows)
        spin = {**mod, M_V: '<span class="spinner-border"></span>\n'}
        r, b0 = repo({**spin, BASELINE: fu16(("Mod", "CI-1", "Views/Mod/Index.cshtml:yok:Layout", "-"), ("Mod", "CI-2", "Views/Mod/Index.cshtml", "1"))})
        code1, out1, _ = execute(r, "check")
        edit(r, M_J, lambda t: t + "b = 2;\n")
        commit(r)
        code2, out2, _ = ci(r, b0)
        cases.append(("AC-21g10 baseline rows with a page key {Module} are accepted and matched as TABAN FU-16",
                      code1 == 0 and "CI-2 satır=1 eşleşme=1" in out1 and code2 == 0
                      and "TABAN FU-16 Mod CI-2 Views/Mod/Index.cshtml 1" in out2 and "TABAN FU-16 Mod CI-1 Views/Mod/Index.cshtml:yok:Layout" in out2))

        def check_row(cells):
            root, _ = repo({BASELINE: baseline + "\t".join(cells) + "\n"})
            return execute(root, "check")[0]
        for label, cells in (("an empty key", ("", "CI-2", "Views/Tasks/Create.cshtml", "1", "r", "FU-16", "o")),
                             ("a key with a space", ("Foo Bar", "CI-2", "Views/Foo Bar/x.cshtml", "1", "r", "FU-16", "o")),
                             ("a three-part key", ("A/B/C", "CI-2", "Views/A/B/C/x.cshtml", "1", "r", "FU-16", "o")),
                             ("a key in NOT_PAGE_AREAS", ("Shared", "CI-2", "Views/Shared/x.cshtml", "1", "r", "FU-16", "o")),
                             ("a CI-3 row with a single-level key", ("Mod", "CI-3", "x exists", "-", "r", "FU-16", "o"))):
            cases.append((f"AC-21g11 {label} -> exit 2", check_row(cells) == 2))
        cases.append(("AC-21g12 a single-level CI-2 row whose instance lies in a subfolder -> exit 2",
                      check_row(("Mod", "CI-2", "Views/Mod/Report/x.cshtml", "1", "r", "FU-16", "o")) == 2))
        cases.append(("AC-21g12 a single-level CI-1 row whose instance lies in a subfolder -> exit 2",
                      check_row(("Mod", "CI-1", "Views/Mod/Report/Index.cshtml:yok:Layout", "-", "r", "FU-16", "o")) == 2))
        partial = W + "Views/Ar/_IndexL10n.cshtml"
        r, b0 = repo()
        write(r, W + "Views/Ar/Index.cshtml", layout)
        write(r, partial, "<script></script>\n")
        commit(r)
        code1, out1, _ = ci(r, b0)
        edit(r, partial, lambda t: "<script>@Localizer.GetAllStrings(true)</script>\n")
        s1 = commit(r)
        code2, out2, _ = ci(r, b0)
        edit(r, partial, lambda t: "<script></script>\n")
        commit(r)
        code3, out3, _ = ci(r, s1)
        cases.append(("AC-21g13 a single-level new surface in a folder holding only subfolder pages -> CI-11 runs; at a later base it does not",
                      code1 == 1 and "YENİ KIRMIZI Ar CI-11 Views/Ar/_IndexL10n.cshtml:yok:GetAllStrings" in out1
                      and code2 == 0 and "PASS Ar CI-11 Views/Ar/_IndexL10n.cshtml" in out2
                      and code3 == 0 and not any(" CI-11 " in l for l in out3)))
        r, b0 = repo({**mod, W + "Views/Mod/Sub/Index.cshtml": layout,
                      BASELINE: fu16(("Mod", "CI-4", "wwwroot/assets/js/Mod/app.js", "1"))})
        tg(r, "rm", "-q", M_V, W + "Views/Mod/ModIndex.cs")
        commit(r)
        code, out, _ = ci(r, b0)
        cases.append(("AC-21g14 a single-level page removed while a subfolder page stays -> 'sayfa kaldırıldı', its rows closed, "
                      "the subfolder page untouched",
                      code == 0 and "sayfa kaldırıldı Mod" in out
                      and "kapandı - satır silinmeli Mod CI-4 wwwroot/assets/js/Mod/app.js" in out and not any("Mod/Sub" in l for l in out)))
        r, b0 = repo({**mod, W + "Views/Mod/_DataTable.cshtml": "<table></table>\n",
                      W + "wwwroot/assets/js/Mod/index.js": "x = 1;\n"})
        edit(r, M_J, lambda t: t + "b = 2;\n")
        commit(r)
        code, out, _ = ci(r, b0)
        dcode, dout, _ = execute(r, "draft")
        cases.append(("FU-17: a single-level list page -> one 'ölçülmedi' CI-3 line, the verifier not run, never red, no draft row; "
                      "CI-9 savedView red as YENİ KIRMIZI",
                      code == 1 and out.count("ölçülmedi Mod CI-3 - doğrulayıcı alansız sayfayı desteklemiyor (FU-17)") == 1
                      and "YENİ KIRMIZI Mod CI-9 wwwroot/assets/js/Mod/index.js:yok:savedView" in out
                      and sum(1 for l in out if l.startswith("YENİ KIRMIZI ")) == 1
                      and dcode == 0 and not any(l.startswith("Mod\tCI-3\t") for l in dout)))
        r, b0 = repo({W + "Views/Account/Login.cshtml": "<i></i>\n"})
        dcode, dout, _ = execute(r, "draft")
        cases.append(("draft: a single-level row has FU-16, the unknown owner (no AREA_OWNER) and the decision's reason; two-level rows unchanged",
                      dcode == 0 and "\t".join(("Account", "CI-1", "Views/Account/Login.cshtml:yok:Layout", "-", SINGLE_REASON, "FU-16", UNKNOWN_OWNER)) in dout
                      and any(l.startswith("Ar/ListA\tCI-5\t") and f"\t{REASON}\tFU-14\t" in l for l in dout)))
        # --- the selector writes nothing in the repository it measures
        clean = all(not git(Path(tmp.name) / f"r{i}", "status", "--porcelain", "--untracked-files=all").strip()
                    and not list((Path(tmp.name) / f"r{i}").rglob("__pycache__")) for i in range(1, counter[0] + 1))
        cases.append(("no write: every throw-away repository is clean after the runs (no file, no __pycache__)", clean))
    except Exception as e:   # a crash is a failed self-test, never a pass
        cases.append((f"self-test crashed: {type(e).__name__}: {str(e)[:200]}", False))
    finally:
        GIT_ENV_EXTRA = saved_env
        shutil.rmtree(tmp.name, ignore_errors=True)
    failed = 0
    for name, ok in cases:
        print(("PASS " if ok else "FAIL ") + name)
        failed += not ok
    print(f"self-test: {len(cases) - failed}/{len(cases)} passed")
    return 1 if failed or not cases else 0


def main() -> int:
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(prog="uic_changed_pages.py", description="MOD-0286 DL-3 UI uyum seçicisi")
    modes = ap.add_mutually_exclusive_group(required=True)
    modes.add_argument("--base", metavar="REF", help="CI kipi: <REF>...HEAD farkının dokunduğu sayfalar")
    modes.add_argument("--self-test", action="store_true", help="kendi kendine test (geçici depo)")
    modes.add_argument("--check-baseline", action="store_true", help="tabanı biçim kurallarıyla oku")
    modes.add_argument("--draft-baseline", action="store_true", help="bütün sayfalar, başlıklı TSV (yalnız yerelde)")
    a = ap.parse_args()                                      # no mode, two modes, an unknown option -> exit 2
    if a.self_test:
        return self_test()
    mode = "ci" if a.base is not None else "check" if a.check_baseline else "draft"
    code, out, err = execute(ROOT, mode, a.base)
    for line in err:
        print(line, file=sys.stderr)
    if out:
        print("\n".join(out))
    return code


if __name__ == "__main__":
    sys.exit(main())
