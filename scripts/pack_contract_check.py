#!/usr/bin/env python3
"""EES planlama zinciri — kısa paket standardı için YALNIZ UYARAN kontrol (.ees/standartlar/paket-standardi.md).

    python3 scripts/pack_contract_check.py [--base REF] [--root DIR] [--all] [DOSYA ...]

Ne okur: künyesinde `pack_standard: kisa-v1` olan modül paketleri (execution/domains/*/module-packs/*.md) ve süreç dosyaları
(execution/domains/*/is-surecleri/SUR-*.md). Künyesinde bu alan olmayan paket HİÇ okunmaz (eski paketler muaf; ayrı liste yok),
tek istisna: bu değişiklikte YENİ eklenen paket alanı taşımıyorsa tek uyarı (U0).
Hangi dosyalar: DOSYA verildiyse onlar; --all ise ağaçtaki hepsi; --base REF ise REF...HEAD arasında eklenen ya da değişen;
hiçbiri yoksa çalışma ağacındaki değişiklikler.

Uyarı kodları: U0 alan unutuldu · U1 süreç bağı · U2 plan bağı · U3 boyut · U4 çok dilim · U5 bölüm başlıkları ·
U6 kabul ölçütü ve dilim · U7 geçmiş sözleşmede · U8 süreç dosyası. Boyutta 1 KB = 1024 bayt.

Çıkış kodu HER KOŞULDA 0'dır: bu bir kapı değil, danışmadır; okunamayan dosya, bozuk künye, git hatası, yanlış argüman da uyarı
olur, kırmızı vermez. Kabul ölçütü ve dilim tablosu ikinci bir okuyucuyla değil, var olan okuyucularla okunur
(scripts/acceptance_record.py parse_pack ve scripts/pack_approval.py slices). Testin VARLIĞINI bu araç ölçmez: o teslimde
scripts/ac_coverage.py report'un işidir. CI'a bağlı değildir.
"""

from __future__ import annotations

import argparse
import glob
import importlib.util
import os
import re
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
KB = 1024

COMMON_MAX = 8 * KB          # künye + bölüm 1, 2, 3, 7 — hedef (uyarı sınırı da bu)
SLICE_WARN = 10 * KB         # bir dilimin satırları; hedef 6 KB, uyarı yalnız bunun üstünde
SLICE_COUNT_MAX = 8
PROCESS_MAX_BYTES = 9 * KB
PROCESS_MAX_LINES = 90
PROCESS_MAX_STEPS, PROCESS_MAX_EXC, PROCESS_MAX_E2E = 12, 12, 4

STANDARD_HEADINGS = ["## 1. Ne ve neden", "## 2. Kapsam", "## 3. Bağlar", "## 4. Sözleşme",
                     "## 5. Acceptance Criteria", "## 6. Teslim dilimleri", "## 7. Açık kararlar"]
COMMON_SECTIONS = ("## 1. Ne ve neden", "## 2. Kapsam", "## 3. Bağlar", "## 7. Açık kararlar")
CONDITIONAL_NAMES = ("para", "zaman", "veri-degisikligi", "sozlesme-degisikligi", "yuksek-hacim", "disa-aktarma", "gozlem",
                     "hassas-veri", "ekran", "durum-akisi", "rota", "dis-sistem", "mevzuat")
TEST_KINDS = ("birim", "entegrasyon", "sözleşme", "uçtan uca", "görünüm", "canlı tur")
PROCESS_HEADINGS = ("## Roller", "## Adımlar", "## İş kuralları", "## İstisnalar", "## Uçtan uca senaryolar")

# pack_readiness.py'deki desenin yedeği: bu araç onu ÖNCE oradan yükler (aynı desen, ikinci kopya değil).
_HISTORY_FALLBACK = re.compile(r"(?i)(revizyon|revision|history|geçmiş|gecmis|changelog|değişiklik (kaydı|günlüğü)|"
                               r"version notes|sürüm notları|reconciliation|uzlaştırma|review findings|inceleme bulguları)")

PACK_RE = re.compile(r"(?:^|/)execution/domains/[^/]+/module-packs/[^/]+\.md$")
PROCESS_RE = re.compile(r"(?:^|/)execution/domains/[^/]+/is-surecleri/SUR-[^/]+\.md$")
FENCE = re.compile(r"^(`{3,}|~{3,})")
PLAN_OK = re.compile(r"^\S.*? · \S.*? · (MUST|SHOULD|COULD)$")
PLAN_NONE = re.compile(r"^plan satırı yok — \S")
PROCESS_REF = re.compile(r"^SUR-([A-Z][A-Z0-9]*)-(\d{2,})\b")
KUNYE_KEY = re.compile(r"^([A-Za-z_][A-Za-z0-9_]*):[ \t]*(.*?)[ \t]*$")
TABLE_SEP = re.compile(r"^\|(\s*:?-+:?\s*\|)+\s*$")
DL = re.compile(r"DL-\d+")
AC_LINE = re.compile(r"^- \[[ xX]\] AC-([1-9][0-9]*) \((zorunlu|isteğe bağlı)\) — (\S.*)$")
AC_DILIM = re.compile(r"· dilim: (DL-\d+)(?=\s|·|$)")
AC_TEST = re.compile(r"· test: (.+?)\s*$")


class Warn:
    def __init__(self, path: str, line: int, code: str, message: str):
        self.path, self.line, self.code, self.message = path, line, code, message

    def __repr__(self) -> str:
        return f"{self.path}:{self.line}: {self.code} {self.message}"


# --------------------------------------------------------------------------------------------------- yükleyiciler
def _load(name: str, path: Path):
    sys.dont_write_bytecode = True       # Codex turu 2 (bulgu 6): okuyucu yüklemesi önbellek dosyası yazmaz
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    sys.modules[name] = mod
    spec.loader.exec_module(mod)
    return mod


_READERS: dict = {}


def reader(name: str):
    """Var olan okuyucular: acceptance_record (kabul ölçütü), pack_approval (dilim tablosu), pack_readiness (geçmiş deseni)."""
    if name not in _READERS:
        _READERS[name] = _load(f"pcc_{name}", HERE / f"{name}.py")
    return _READERS[name]


def history_heading() -> re.Pattern:
    try:
        return reader("pack_readiness").HISTORY_HEADING
    except Exception:
        return _HISTORY_FALLBACK


# --------------------------------------------------------------------------------------------------- metin yardımcıları
def split_kunye(text: str) -> tuple[dict[str, str] | None, int, str | None]:
    """(alanlar, gövdenin ilk satır indeksi, sorun). Künye yoksa (None, 0, None); kapanmıyorsa (None, 0, sorun)."""
    lines = text.replace("\r\n", "\n").split("\n")
    if not lines or lines[0].strip() != "---":
        return None, 0, None
    end = next((i for i in range(1, len(lines)) if lines[i].strip() == "---"), None)
    if end is None:
        return None, 0, "künye '---' ile kapanmıyor"
    out: dict[str, str] = {}
    for l in lines[1:end]:
        m = KUNYE_KEY.match(l)
        if m and m.group(1) not in out and not l.lstrip().startswith("#"):
            out[m.group(1)] = m.group(2)
    return out, end + 1, None


def body_lines(text: str, start: int) -> list[tuple[int, str, bool]]:
    """Gövde satırları: (1 tabanlı satır no, satır, kod çiti içinde mi). Çit satırlarının kendisi çit içi sayılır."""
    out, fence = [], None
    for i, l in enumerate(text.replace("\r\n", "\n").split("\n")):
        if i < start:
            continue
        m = FENCE.match(l.strip())
        inside = fence is not None
        if fence is None and m:
            fence, inside = (m.group(1)[0], len(m.group(1))), True
        elif fence is not None and m and l.strip() == m.group(1) and m.group(1)[0] == fence[0] and len(m.group(1)) >= fence[1]:
            fence = None
        out.append((i + 1, l, inside))
    return out


def sections(lines: list[tuple[int, str, bool]]) -> list[tuple[str, int, list[tuple[int, str]]]]:
    """İkinci düzey (`## `) bölümler, çit dışında: (başlık, başlık satırı, [(satır no, satır)...])."""
    out: list[tuple[str, int, list[tuple[int, str]]]] = []
    for n, l, inside in lines:
        if not inside and re.match(r"^## ", l):
            out.append((l.rstrip(), n, []))
        elif out:
            out[-1][2].append((n, l))
    return out


def cells(row: str) -> list[str]:
    s = row.strip()
    s = s[1:-1] if s.startswith("|") and s.endswith("|") else s
    return [c.strip() for c in s.split("|")]


def data_rows(body: list[tuple[int, str]]) -> list[tuple[int, str]]:
    """Tablo veri satırları: başlık satırı (ayraç satırından hemen önceki) ve ayraç satırı hariç."""
    rows = [(n, l) for n, l in body if l.strip().startswith("|")]
    sep = lambda s: bool(TABLE_SEP.match(s.strip().replace(" ", "")))
    return [(n, l) for i, (n, l) in enumerate(rows)
            if not sep(l) and not (i + 1 < len(rows) and sep(rows[i + 1][1]))]


def size(s: str) -> int:
    return len(s.encode("utf-8")) + 1


# --------------------------------------------------------------------------------------------------- paket kuralları
class Pack:
    def __init__(self, path: str, text: str):
        self.path, self.text = path, text
        self.kunye, self.body_start, self.kunye_problem = split_kunye(text)
        self.lines = body_lines(text, self.body_start) if self.kunye is not None else []
        self.sections = sections(self.lines)

    def section(self, heading: str):
        return next((s for s in self.sections if s[0] == heading), None)


def conditional_list(value: str | None) -> list[str] | None:
    if value is None:
        return None
    v = value.strip()
    if not (v.startswith("[") and v.endswith("]")):
        return None
    return [x.strip() for x in v[1:-1].split(",") if x.strip()]


def u1_process(p: Pack, root: str) -> list[Warn]:
    v = p.kunye.get("business_process")
    if v is None or not v.strip():
        return [Warn(p.path, 1, "U1", "künyede `business_process:` yok (SUR-… adım a-b, ya da yok — teknik: <gerekçe>, "
                                      "ya da yok — takip paketi: üst paketin süreci)")]
    v = v.strip()
    if v.startswith("yok — teknik:"):
        return [] if v[len("yok — teknik:"):].strip() else [Warn(p.path, 1, "U1", "`yok — teknik:` gerekçesiz")]
    if v.startswith("yok — takip paketi:"):
        return []
    m = PROCESS_REF.match(v)
    if not m:
        return [Warn(p.path, 1, "U1", f"`business_process:` biçimi tanınmadı: '{v[:50]}' "
                                      "(SUR-<ALAN>-<nn> adım a-b | yok — teknik: … | yok — takip paketi: …)")]
    sid = f"SUR-{m.group(1)}-{m.group(2)}"
    if not glob.glob(os.path.join(root, "execution", "domains", "*", "is-surecleri", f"{sid}-*.md")):
        return [Warn(p.path, 1, "U1", f"{sid} için süreç dosyası yok (execution/domains/*/is-surecleri/{sid}-*.md); "
                                      "süreç ona ilk atıf yapan paketle aynı PR'da gelmeli")]
    return []


def u2_plan(p: Pack, root: str) -> list[Warn]:
    v = p.kunye.get("plan_row")
    if v is None or not v.strip():
        return [Warn(p.path, 1, "U2", "künyede `plan_row:` yok (<sürüm> · <MVP kimliği> · MUST|SHOULD|COULD, ya da "
                                      "plan satırı yok — <neden>)")]
    if PLAN_OK.match(v.strip()) or PLAN_NONE.match(v.strip()):
        return []
    return [Warn(p.path, 1, "U2", f"`plan_row:` biçimi tanınmadı: '{v.strip()[:60]}'")]


def slice_sizes(p: Pack) -> dict[str, int]:
    """Dilim → o dilimin satırlarının baytı: bölüm 4 ve koşullu bölümlerin tablo satırları (son hücre DL-n) + bölüm 5'te
    `· dilim: DL-n` taşıyan ölçüt satırları."""
    out: dict[str, int] = {}
    for title, _n, body in p.sections:
        if title.startswith("## 4.") or title.startswith("## K-"):
            for _ln, l in data_rows(body):
                for d in set(DL.findall(cells(l)[-1] if cells(l) else "")):
                    out[d] = out.get(d, 0) + size(l)
        elif title == "## 5. Acceptance Criteria":
            for _ln, l in body:
                m = AC_LINE.match(l.strip())
                d = AC_DILIM.search(l) if m else None
                if d:
                    out[d.group(1)] = out.get(d.group(1), 0) + size(l)
    return out


def u3_size(p: Pack, root: str) -> list[Warn]:
    w = []
    common = len("\n".join(p.text.replace("\r\n", "\n").split("\n")[:p.body_start]).encode("utf-8"))
    for title, _n, body in p.sections:
        if title in COMMON_SECTIONS:
            common += size(title) + sum(size(l) for _ln, l in body)
    if common > COMMON_MAX:
        w.append(Warn(p.path, 1, "U3", f"ortak kısım (künye + bölüm 1, 2, 3, 7) {common} bayt > {COMMON_MAX} (8 KB)"))
    for d, n in sorted(slice_sizes(p).items()):
        if n > SLICE_WARN:
            w.append(Warn(p.path, 1, "U3", f"{d} satırları {n} bayt > {SLICE_WARN} (10 KB; hedef 6 KB): "
                                           "dilimi bölmeyi ya da ölçütleri sadeleştirmeyi düşün"))
    return w


def slice_table_ids(p: Pack) -> list[str]:
    s = p.section("## 6. Teslim dilimleri")
    if not s:
        return []
    return [cells(l)[0] for _n, l in data_rows(s[2]) if cells(l) and DL.fullmatch(cells(l)[0])]


def u4_slices(p: Pack, root: str) -> list[Warn]:
    ids = slice_table_ids(p)
    if len(ids) > SLICE_COUNT_MAX:
        return [Warn(p.path, 1, "U4", f"{len(ids)} dilim > {SLICE_COUNT_MAX}: takip paketine bölmeyi düşün")]
    return []


def u5_sections(p: Pack, root: str) -> list[Warn]:
    w, titles = [], [(t, n) for t, n, _b in p.sections]
    found = [t for t, _n in titles]
    for want in STANDARD_HEADINGS:
        if want not in found:
            near = next((t for t in found if t.split(" ", 2)[:2] == want.split(" ", 2)[:2]), None)
            w.append(Warn(p.path, 1, "U5", f"başlık yok ya da farklı: `{want}`" + (f" (bulunan: `{near}`)" if near else "")))
    std_found = [t for t in found if t in STANDARD_HEADINGS]
    if std_found != [h for h in STANDARD_HEADINGS if h in std_found]:
        w.append(Warn(p.path, 1, "U5", "yedi çekirdek bölümün sırası standarttakinden farklı"))
    for t, n in titles:
        if t not in STANDARD_HEADINGS and not t.startswith("## K-"):
            w.append(Warn(p.path, n, "U5", f"standartta olmayan ikinci düzey başlık: `{t}` (yalnız yedi çekirdek bölüm ve `## K-<ad>.`)"))
    conds = conditional_list(p.kunye.get("conditional"))
    if conds is None:
        w.append(Warn(p.path, 1, "U5", "künyede `conditional: [...]` yok ya da liste biçiminde değil (hiçbiri yoksa `[]`)"))
        conds = []
    k_found = [(re.match(r"^## K-([a-z0-9-]+)\.", t), n) for t, n in titles if t.startswith("## K-")]
    k_names = []
    for m, n in k_found:
        if not m:
            w.append(Warn(p.path, n, "U5", "koşullu bölüm başlığı `## K-<ad>. <Ad>` biçiminde değil"))
        else:
            k_names.append(m.group(1))
    for c in conds:
        if c not in CONDITIONAL_NAMES:
            w.append(Warn(p.path, 1, "U5", f"`conditional:` listesinde bilinmeyen bölüm adı: {c}"))
        elif c not in k_names:
            w.append(Warn(p.path, 1, "U5", f"`conditional:` listesindeki `{c}` için `## K-{c}.` başlığı yok"))
    for k in k_names:
        if k not in conds:
            w.append(Warn(p.path, 1, "U5", f"`## K-{k}.` bölümü var ama künyedeki `conditional:` listesinde yok"))
    if [k for k in k_names if k in conds] != [c for c in conds if c in k_names]:
        w.append(Warn(p.path, 1, "U5", "koşullu bölümlerin sırası `conditional:` listesindekinden farklı"))
    return w


def slice_hint(p: Pack) -> str:
    """Okunamayan dilim tablosunda hangi hücrenin kusurlu olduğunu söyler (okuyucu yalnız 'okunamadı' der)."""
    s = p.section("## 6. Teslim dilimleri")
    if not s:
        return " (bölüm 6 yok)"
    titles = {re.sub(r"^\d+(\.\d+)*\.?\s*", "", t[3:].strip()).lower() for t, _n, _b in p.sections}
    bad = []
    for _n, l in data_rows(s[2]):
        c = cells(l)
        if len(c) > 2:
            bad += [f"'{x}'" for x in (y.strip() for y in c[2].split(",")) if x.lower() not in titles]
        bad += [f"boş/—/yok hücre ({c[0]})" for x in c if re.fullmatch(r"(?i)\s*(-|—|tbd|todo|\.\.\.|…|yok|n/?a)?\s*", x)]
    return f"; kusurlu: {', '.join(bad[:4])}" if bad else ""


def u6_criteria(p: Pack, root: str) -> list[Warn]:
    w: list[Warn] = []
    idv = (p.kunye.get("id") or "").strip()
    try:
        crit, bad, old = reader("acceptance_record").parse_pack(p.text, idv)
    except Exception as e:
        return [Warn(p.path, 1, "U6", f"kabul ölçütü okuyucusu paketi okuyamadı ({type(e).__name__})")]
    for b in bad:
        if "künyede tek id" in b or "künyede tek status" in b:
            continue            # kimlik ve durum onay anının işi; taslak paket bunlarla uyarılmaz
        w.append(Warn(p.path, 1, "U6", f"kabul ölçütü okuyucusu: {b}"))
    if old and not bad:
        w.append(Warn(p.path, 1, "U6", "kabul ölçütü okuyucusu bölümde `AC-n (zorunlu|isteğe bağlı)` maddesi bulamadı"))
    try:
        slices = reader("pack_approval").slices(p.text)
    except Exception as e:
        slices = None
        w.append(Warn(p.path, 1, "U6", f"dilim tablosu okuyucusu hata verdi ({type(e).__name__})"))
    else:
        if slices is None:
            w.append(Warn(p.path, 1, "U6", "dilim tablosu eski onay aracının okuyucusuyla (pack_approval.slices) okunamıyor: "
                                           "başlık `## 6. Teslim dilimleri`, ilk sütun DL-n, üçüncü sütun paketin başlık adları, "
                                           "hücre boş / — / yok olmaz" + slice_hint(p)))
    sec = p.section("## 5. Acceptance Criteria")
    for n, l in (sec[2] if sec else []):
        m = AC_LINE.match(l.strip())
        if not m or m.group(2) != "zorunlu":
            continue
        d, t = AC_DILIM.search(l), AC_TEST.search(l)
        if not d:
            w.append(Warn(p.path, n, "U6", f"AC-{m.group(1)}: `· dilim: DL-n` eki yok"))
        elif slices is not None and d.group(1) not in slices:
            w.append(Warn(p.path, n, "U6", f"AC-{m.group(1)}: {d.group(1)} dilim tablosunda yok"))
        if not t:
            w.append(Warn(p.path, n, "U6", f"AC-{m.group(1)}: `· test: <tür>` eki yok"))
        elif t.group(1).strip() not in TEST_KINDS:
            w.append(Warn(p.path, n, "U6", f"AC-{m.group(1)}: test türü tanınmadı '{t.group(1)[:30]}' ({', '.join(TEST_KINDS)})"))
    # KP7: satırı olmayan TBD
    open_rows = data_rows(p.section("## 7. Açık kararlar")[2]) if p.section("## 7. Açık kararlar") else []
    if not open_rows:
        for title, _n, body in p.sections:
            if title == "## 7. Açık kararlar":
                continue
            for ln, l in body:
                if re.search(r"\bTBD\b", l):
                    w.append(Warn(p.path, ln, "U6", "`TBD` var ama bölüm 7'de açık karar satırı yok (KP7)"))
                    break
    return w


def u7_history(p: Pack, root: str) -> list[Warn]:
    pat, w = history_heading(), []
    for n, l, inside in p.lines:
        if not inside and re.match(r"^#{2,3} ", l) and pat.search(l):
            w.append(Warn(p.path, n, "U7", f"geçmiş/revizyon/inceleme başlığı sözleşmede: `{l.strip()[:50]}` (yeri kararlar/*.karar.md)"))
    return w


PACK_RULES = {"U1": u1_process, "U2": u2_plan, "U3": u3_size, "U4": u4_slices, "U5": u5_sections, "U6": u6_criteria,
              "U7": u7_history}


def check_pack(path: str, text: str, root: str = str(ROOT), added: bool = False, disabled: frozenset = frozenset()) -> list[Warn]:
    """Bir paket dosyasının uyarıları. `disabled`: sabotaj testi için kapatılan kural kodları."""
    p = Pack(path, text)
    if p.kunye_problem:           # künye kapanmıyorsa alan okunamaz; yalnız kısa standardı anan dosya uyarılır, eski paket muaf
        return [Warn(path, 1, "U5", p.kunye_problem)] if re.search(r"(?m)^pack_standard:\s*kisa-v1", text) else []
    standard = (p.kunye or {}).get("pack_standard", "").strip().strip("\"'")
    if p.kunye is None or standard != "kisa-v1":
        if added and "U0" not in disabled:
            return [Warn(path, 1, "U0", "yeni eklenen pakette künyede `pack_standard: kisa-v1` yok: yeni paket kısa standartla "
                                        "yazılır (eski biçimde kalması bilinçliyse yok sayılabilir)")]
        return []
    out: list[Warn] = []
    for code, fn in PACK_RULES.items():
        if code not in disabled:
            out += fn(p, root)
    return out


# --------------------------------------------------------------------------------------------------- süreç kuralları
def check_process(path: str, text: str, disabled: frozenset = frozenset()) -> list[Warn]:
    w: list[Warn] = []
    raw = text.replace("\r\n", "\n")
    nlines = raw.count("\n") + (0 if raw.endswith("\n") else 1)
    if "U3" not in disabled and (len(raw.encode("utf-8")) > PROCESS_MAX_BYTES or nlines > PROCESS_MAX_LINES):
        w.append(Warn(path, 1, "U3", f"süreç {len(raw.encode('utf-8'))} bayt / {nlines} satır "
                                     f"(sınır {PROCESS_MAX_BYTES} bayt ve {PROCESS_MAX_LINES} satır): iki süreç olabilir"))
    if "U8" in disabled:
        return w
    _kunye, start, _prob = split_kunye(raw)
    lines = body_lines(raw, start or 0)
    secs = {t: (n, b) for t, n, b in sections(lines)}
    for h in PROCESS_HEADINGS:
        if h not in secs:
            w.append(Warn(path, 1, "U8", f"süreç şablonunun `{h}` bölümü yok"))
    for h, label, cap in (("## Adımlar", "adım", PROCESS_MAX_STEPS), ("## İstisnalar", "istisna", PROCESS_MAX_EXC),
                          ("## Uçtan uca senaryolar", "senaryo", PROCESS_MAX_E2E)):
        if h in secs:
            n_rows = len(data_rows(secs[h][1]))
            if n_rows > cap:
                w.append(Warn(path, secs[h][0], "U8", f"{n_rows} {label} > {cap}: daha uzunsa iki süreçtir"))
    return w


# --------------------------------------------------------------------------------------------------- dosya seçimi
def git(root: str, *args: str) -> str | None:
    try:
        r = subprocess.run(["git", "-C", root, *args], capture_output=True, text=True, timeout=60)
    except Exception:
        return None
    return r.stdout if r.returncode == 0 else None


def select(root: str, paths: list[str], base: str | None, everything: bool) -> tuple[list[tuple[str, bool]], list[Warn]]:
    """[(kökten göreli yol, yeni mi)], ön uyarılar."""
    pre: list[Warn] = []
    if paths:
        return [(os.path.relpath(os.path.abspath(p), root) if os.path.isabs(p) or os.path.exists(p) else p, False) for p in paths], pre
    if everything:
        found = glob.glob(os.path.join(root, "execution", "domains", "*", "module-packs", "*.md")) + \
            glob.glob(os.path.join(root, "execution", "domains", "*", "is-surecleri", "SUR-*.md"))
        return [(os.path.relpath(f, root), False) for f in sorted(found)], pre
    if base is not None:
        out = git(root, "diff", "--name-status", "--diff-filter=AM", f"{base}...HEAD") if base.strip() else None
        if out is None:
            pre.append(Warn("-", 1, "U0", f"taban '{base[:40]}' çözülemedi (taban yok): değişen dosyalar ölçülemedi, kontrol atlandı"))
            return [], pre
        res = []
        for l in out.splitlines():
            st, _, f = l.partition("\t")
            res.append((f, st.startswith("A")))
        return res, pre
    out = git(root, "status", "--porcelain", "--untracked-files=all")
    if out is None:
        pre.append(Warn("-", 1, "U0", "git durumu okunamadı: değişen dosyalar ölçülemedi, kontrol atlandı"))
        return [], pre
    res = []
    for l in out.splitlines():
        st, f = l[:2], l[3:].strip().strip('"')
        res.append((f, "A" in st or "?" in st))
    return res, pre


class Parser(argparse.ArgumentParser):
    def error(self, message):           # çıkış 0 kuralı: yanlış argüman da uyarıdır
        raise ValueError(message)


def run(argv: list[str]) -> tuple[list[Warn], int]:
    ap = Parser(prog="pack_contract_check.py", add_help=True)
    ap.add_argument("paths", nargs="*")
    ap.add_argument("--base")
    ap.add_argument("--root", default=str(ROOT))
    ap.add_argument("--all", action="store_true", dest="everything")
    a = ap.parse_args(argv)
    warns: list[Warn] = []
    files, pre = select(a.root, a.paths, a.base, a.everything)
    warns += pre
    read = 0
    for rel, added in files:
        rel = rel.replace(os.sep, "/")
        is_pack, is_proc = bool(PACK_RE.search(rel)), bool(PROCESS_RE.search(rel))
        if not (is_pack or is_proc):
            continue
        try:
            text = Path(os.path.join(a.root, rel)).read_text(encoding="utf-8")
        except (OSError, UnicodeDecodeError) as e:
            warns.append(Warn(rel, 1, "U5", f"dosya okunamadı ({type(e).__name__})"))
            continue
        read += 1
        warns += check_pack(rel, text, a.root, added) if is_pack else check_process(rel, text)
    return warns, read


def emit(w: Warn) -> str:
    if os.environ.get("GITHUB_ACTIONS") == "true":
        return f"::warning file={w.path},line={w.line}::{w.code} {w.message}"
    return f"uyarı {w.path}:{w.line}: {w.code} {w.message}"


def _say(line: str) -> None:
    """Codex turu 2 (bulgu 5): yazdırma da çıkış kodunu değiştiremez — kodlanamayan karakter yerine '?', kapalı boru sessiz."""
    try:
        print(line)
    except UnicodeEncodeError:
        enc = getattr(sys.stdout, "encoding", None) or "ascii"
        print(line.encode(enc, errors="replace").decode(enc, errors="replace"))


def main(argv: list[str] | None = None) -> int:
    sys.dont_write_bytecode = True
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(errors="replace")
        except (ValueError, OSError):
            pass
    try:
        warns, read = run(sys.argv[1:] if argv is None else argv)
    except SystemExit:
        warns, read = [], 0
    except BaseException as e:           # kontrol hiçbir koşulda kırmızı vermez
        warns, read = [Warn("-", 1, "U0", f"kontrol tamamlanamadı: {type(e).__name__}: {str(e)[:120]}")], 0
    try:
        for w in warns:
            _say(emit(w))
        _say(f"pack_contract_check: {read} dosya okundu, {len(warns)} uyarı (yalnız uyarı; çıkış 0)")
        sys.stdout.flush()
    except (BrokenPipeError, OSError, ValueError):
        try:                              # okuyan taraf kapandı: kalan çıktıyı at, çıkış yine 0
            devnull = os.open(os.devnull, os.O_WRONLY)
            os.dup2(devnull, sys.stdout.fileno())
        except (OSError, ValueError, AttributeError):
            pass
    return 0


if __name__ == "__main__":
    sys.exit(main())
