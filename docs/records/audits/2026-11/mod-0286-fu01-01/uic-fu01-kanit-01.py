#!/usr/bin/env python3
"""MOD-0286-FU01 kule kanıt betiği, sürüm 01 — Control Tower yazdı; işçi bu dosyayı değiştirmez, olduğu gibi koşar.
Ölçüt metni: execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md (AC-1...AC-13,
sabotaj tablosu SB-1...SB-14, SB-13 satırları, Failure Path). Sayılar ve çapalar bağımsız ölçümden okunur:
docs/records/audits/2026-10/mod-0286-fu01-01/uic-fu01-oracle-02.py (satirlar, ciftler, siniflar, gorunum, turetim, secici); paket
"evidence script re-finds by text" dediği yerleri (Index.cshtml:179, SB-6/7/9 etiketleri, SB-3/4 çapaları, T-1...T-7 ve SB-13 tablosu)
bu betik metinle bulur. Tablo ile türetilen fark ederse "not:" satırı yazılır, türetilen kullanılır.

Çağrı (depo kökünden):
  python3 -B <bu betik> <çıktı klasörü> <iş tam sha> --base <taban sha> --decision <sahip karar adı> --owners <sayfa<TAB>sahip TSV>
         [--ci-kaydi <dosya>] [--klon-dizini <üst dizin>] [--klon-var <klon>]
Kayıtlı koşu: python3 scripts/run_record.py run --out docs/records/audits/<yeni dizin> -- python3 -B <bu betik> <aynı dizin> <sha> ...
  (run_record çıktı dizinine stdout.txt ve stderr.txt'yi önceden yazar; betik bunlar dışında dosya bulursa durur.)
Yan kip (oracle sabotajı, işçi çalıştırmaz):  python3 -B <bu betik> <çıktı klasörü> <sha> --oracle-sabotaj [--klon-var <klon>]

Her madde için tek satır `AC-n geçti|kaldı <neden>`, sonda `zorunlu geçti N / M`; çıkış 0 yalnız hepsi geçince, 1 kalan varsa,
2 kullanım hatası, 3 yük nedeniyle durdu. Bir madde hata verirse o madde kaldı, betik sürer.
Sınırlar: sabotajlar geçici klonun geçici dalında (fu01-sabotaj), klonda `origin` kaldırılır, hiçbir şey push edilmez; klon çıktı
klasörünün DIŞINDA kurulur (varsayılan sistem geçici dizini): run_record çıktı dizinindeki her dosyayı özetler ve sembolik bağda
reddeder, 1,2 GB klon kaydı kullanılmaz kılar. Canlı depoya yazılmaz; silme yok (klon silinmez).
AC-12: CI işi bu betikte koşmaz; `--ci-kaydi` dosyası (run_id=, head_sha=, artefakt_sha256= satırları) verilirse tutarlılığı
denetlenir ve işin yerel eşdeğeri (`--base <taban>` çıkış 0) ölçülür; verilmezse madde kaldı.
"""
import argparse
import ast
import collections
import os
import re
import subprocess
import sys
import tempfile
import time

SELECTOR = "scripts/uic_changed_pages.py"
BASELINE = "tests/ui-compliance-baseline.tsv"
PACK = "execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md"
PARENT_PACK = "execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md"
WORKFLOW = ".github/workflows/ui-compliance.yml"
ORACLE_REL = "docs/records/audits/2026-10/mod-0286-fu01-01/uic-fu01-oracle-02.py"
WEB = "frontend/Diten.Web/"
JSDIR = WEB + "wwwroot/assets/js/"
JSI = "wwwroot/assets/js/"
BR = "fu01-sabotaj"
FOUR_MODES = {"--base", "--self-test", "--check-baseline", "--draft-baseline"}
GID = ["-c", "user.name=uic-fu01-kanit", "-c", "user.email=uic-fu01-kanit@localhost", "-c", "commit.gpgsign=false"]
ENV = dict(os.environ, PYTHONDONTWRITEBYTECODE="1", PYTHONUTF8="1", PYTHONIOENCODING="utf-8", GIT_OPTIONAL_LOCKS="0")
# Paketin 0504c96ea için yazdığı değerler: yalnız "not:" karşılaştırması içindir, ölçüt değildir.
TABLO = {"sınıf": 106, "çift": 113, "sayfa": 22, "satır": 22, "eşleşme": 92, "görünüm": 1530, "CRM/Accounts": 19,
         "kırmızı": {"SB-1": 1, "SB-2": 5, "SB-3": 2, "SB-4": 1, "SB-8": 2, "SB-9": 4},
         "çapa": {"wwwroot/assets/js/pages/enterprise-strategy/modal-form-utils.js": 113,
                  "wwwroot/assets/js/pages/enterprise-strategy/goals.js": 3372}, "SB-6 satır": 179}

ROOT = os.getcwd()
OUT = None
K = None
LOGF = None
ORACLE = None
W = B = None
SAY = [0]
Z = {}
ANCHOR = {}
NOTES = []


def log(s=""):
    print(s, flush=True)
    if LOGF:
        LOGF.write(s + "\n")
        LOGF.flush()


def yuk_bekle():
    t = time.time()
    while os.getloadavg()[0] > 12:
        if time.time() - t > 900:
            log("DUR: 1 dk yük ortalaması 15 dakikadır 12'nin üstünde; koşu başlatılmadı/sürdürülmedi")
            sys.exit(3)
        time.sleep(30)


class Res:
    def __init__(self, code, out, err, sn=0.0):
        self.code, self.out, self.err, self.sn = code, out, err, sn

    @property
    def lines(self):
        return self.out.split("\n")[:-1] if self.out.endswith("\n") else (self.out.split("\n") if self.out else [])

    @property
    def alll(self):
        e = self.err.split("\n")[:-1] if self.err.endswith("\n") else (self.err.split("\n") if self.err else [])
        return self.lines + e

    def has(self, line):
        return line in self.lines

    def count(self, line):
        return self.lines.count(line)

    def pre(self, prefix):
        return sum(1 for ln in self.lines if ln.startswith(prefix))


def sh(args, cwd, name=None, timeout=900, heavy=False):
    if heavy:
        yuk_bekle()
    t = time.time()
    try:
        p = subprocess.run(args, cwd=cwd, capture_output=True, timeout=timeout, env=ENV)
        r = Res(p.returncode, p.stdout.decode("utf-8", "replace"), p.stderr.decode("utf-8", "replace"))
    except subprocess.TimeoutExpired:
        r = Res(124, "", "zaman aşımı")
    r.sn = time.time() - t
    if name and OUT:
        SAY[0] += 1
        base = os.path.join(OUT, "%03d-%s" % (SAY[0], re.sub(r"[^A-Za-z0-9._-]", "_", name)))
        with open(base + ".stdout.txt", "w", encoding="utf-8") as fh:
            fh.write(r.out)
        with open(base + ".stderr.txt", "w", encoding="utf-8") as fh:
            fh.write(r.err)
        with open(base + ".exit.txt", "w", encoding="utf-8") as fh:
            fh.write("%d\n" % r.code)
    return r


def g(*a, ok=False):
    r = sh(["git", *GID, *a], K)
    if r.code and not ok:
        raise RuntimeError("git %s: %s" % (" ".join(a[:3]), r.err.strip()[:200]))
    return r.out.strip()


def head():
    return g("rev-parse", "HEAD")


def kcheckout(sha):
    g("checkout", "--quiet", "--force", "-B", BR, sha)


def kcommit(paths, msg):
    g("add", "--", *paths)
    g("commit", "--quiet", "-m", msg)
    return head()


def rd(rel, default=None):
    p = os.path.join(K, rel)
    if not os.path.exists(p):
        if default is not None:
            return default
        raise RuntimeError("dosya yok: " + rel)
    with open(p, "rb") as fh:
        return fh.read().decode("utf-8", "surrogateescape")


def wr(rel, text):
    p = os.path.join(K, rel)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    with open(p, "wb") as fh:
        fh.write(text.encode("utf-8", "surrogateescape"))


def selector(args, name, timeout=900, heavy=False):
    return sh(["python3", "-B", SELECTOR, *args], K, name, timeout, heavy)


def sel_base(base, name):
    return selector(["--base", base], name)


def ora(mode, sha, *extra):
    key = ("ora", mode, sha, extra)
    if key not in Z:
        nm = "oracle-%s-%s%s" % (mode, sha[:9], ("-" + extra[0].replace("/", "_")) if extra else "")
        Z[key] = sh(["python3", "-B", ORACLE, mode, sha, *extra], K, nm, 900, heavy=True)
    return Z[key]


def ora_ok(mode, sha, *extra):
    r = ora(mode, sha, *extra)
    if r.code != 0:
        raise RuntimeError("oracle %s çıkış %d: %s" % (mode, r.code, r.err.strip()[:160]))
    return r


def lazy(f):
    def w():
        k = "lazy-" + f.__name__
        if k not in Z:
            try:
                Z[k] = (True, f())
            except Exception as e:                       # noqa: BLE001
                Z[k] = (False, e)
        ok, v = Z[k]
        if ok:
            return v
        raise v
    w.__name__ = f.__name__
    return w


def note(label, tablo, got):
    if tablo != got:
        msg = "not: tablo %s = %s, türetilen %s = %s (türetilen kullanıldı)" % (label, tablo, label, got)
        if msg not in NOTES:
            NOTES.append(msg)
            log("  " + msg)


# --- bağımsız ölçümden türetilen değerler -------------------------------------------------------------------------
@lazy
def siniflar():
    r = ora_ok("siniflar", B)
    out = [tuple(ln.split("\t", 1)) for ln in r.lines]
    note("sınıf", TABLO["sınıf"], len(out))
    return out


@lazy
def ciftler():
    r = ora_ok("ciftler", B)
    out = [tuple(ln.split("\t", 1)) for ln in r.lines]
    note("çift", TABLO["çift"], len(out))
    return out


@lazy
def satirlar_base():
    r = ora_ok("satirlar", B)
    rows = [tuple(ln.split("\t")) for ln in r.lines]
    m = re.search(r"sınıf (\d+) çift (\d+) sayfa (\d+) satır (\d+) eşleşme (\d+)", r.err)
    if m:
        note("sayfa", TABLO["sayfa"], int(m.group(3)))
        note("satır", TABLO["satır"], int(m.group(4)))
        note("eşleşme", TABLO["eşleşme"], int(m.group(5)))
    return rows


@lazy
def turetim():
    r = ora_ok("turetim", B)
    rows = collections.defaultdict(list)
    for ln in r.lines:
        sb, page, check, inst, n, d = ln.split("\t")
        rows[sb].append((page, check, inst, int(n), int(d)))
    red = {}
    m = re.search(r"^kırmızı\t(.*)$", r.err, re.M)
    for part in (m.group(1).split() if m else []):
        k, v = part.split("=")
        red[k] = int(v)
    anchors = {}
    for mm in re.finditer(r"^çapa\t(\S+)\t(\d+)$", r.err, re.M):
        anchors[mm.group(1)] = int(mm.group(2))
    for k, v in TABLO["kırmızı"].items():
        note("kırmızı " + k, v, red.get(k))
    for k, v in TABLO["çapa"].items():
        note("çapa " + k.rsplit("/", 1)[1], v, anchors.get(k))
    return dict(rows), red, anchors


def red_lines(sb):
    return ["YENİ KIRMIZI %s %s %s %d +%d" % (p, c, i, n, d) for (p, c, i, n, d) in turetim()[0][sb] if d > 0]


@lazy
def gor_all():
    n = int(ora_ok("gorunum", B).out.strip())
    note("görünüm", TABLO["görünüm"], n)
    return n


@lazy
def gor_crm():
    n = int(ora_ok("gorunum", B, "CRM/Accounts").out.strip())
    note("CRM/Accounts görünüm", TABLO["CRM/Accounts"], n)
    return n


# --- madde denetim nesnesi ------------------------------------------------------------------------------------------
class Chk:
    def __init__(self, ad):
        self.ad, self.fails, self.n = ad, [], 0

    def ok(self, label, cond, detail=""):
        self.n += 1
        if cond:
            log("  %s tamam: %s" % (self.ad, label))
        else:
            self.fails.append(label + ((" [" + str(detail)[:140] + "]") if detail else ""))
            log("  %s FARK: %s %s" % (self.ad, label, str(detail)[:200]))
        return bool(cond)

    def eq(self, label, got, want):
        return self.ok("%s = %s" % (label, got), got == want, "beklenen %s" % (want,))

    def guard(self, label, fn):
        try:
            fn()
        except Exception as e:                           # noqa: BLE001
            self.n += 1
            self.fails.append("%s: hata %s: %s" % (label, type(e).__name__, str(e)[:120]))
            log("  %s HATA: %s %s: %s" % (self.ad, label, type(e).__name__, str(e)[:200]))


def memo(key, fn):
    k = "memo-" + key
    if k not in Z:
        try:
            Z[k] = (True, fn())
        except Exception as e:                           # noqa: BLE001
            Z[k] = (False, e)
    ok, v = Z[k]
    if ok:
        return v
    raise v


# --- klon üstünde sabotaj yardımcıları ---------------------------------------------------------------------------------
def jpath(x):
    return WEB + x if x.startswith("Views/") else JSDIR + x


def jinst(x):
    return JSI + x


def app(add):
    return lambda t: t + add


ALERT = "\nalert('x');\n"
ALERT_TEXT = "alert('x');"


def styled(rel):
    def fn(t):
        src = t.split("\n")
        i = next(i for i, s in enumerate(src) if re.search(r"\.style\.", s))
        src[i] += " a.style.b;"
        ANCHOR[rel] = i + 1
        return "\n".join(src)
    return fn


def tag_rx(jsrel):
    return re.compile(r"<script\b[^>]*" + re.escape(jsrel) + r"[^>]*>(?:\s*</script>)?", re.S | re.I)


def drop_tag(jsrel, where=None):
    def fn(t):
        ms = list(tag_rx(jsrel).finditer(t))
        if len(ms) != 1:
            raise RuntimeError("etiket %d kez bulundu: %s" % (len(ms), jsrel))
        s, e = ms[0].span()
        if where is not None:
            where.append(t[:s].count("\n") + 1)
        ls = t.rfind("\n", 0, s) + 1
        le = t.find("\n", e)
        le = len(t) if le < 0 else le + 1
        if t[ls:s].strip() == "" and t[e:le].strip() == "":
            s, e = ls, le
        return t[:s] + t[e:]
    return fn


def scen(tag, start, edits, msg=None):
    kcheckout(start)
    p = head()
    for rel, fn in edits:
        wr(rel, fn(rd(rel, default="")))
    s = kcommit([r for r, _ in edits], msg or tag)
    return p, s, sel_base(p, tag)


def revert(tag, s):
    g("revert", "--no-edit", s)
    return sel_base(s, tag + "-geri")


def expect(c, tag, r, code, present=(), once=(), absent=(), absent_pre=(), absent_has=()):
    c.ok("%s çıkış %s (beklenen %s)" % (tag, r.code, code), r.code == code)
    for ln in present:
        c.ok("%s satır: %s" % (tag, ln), r.has(ln))
    for ln in once:
        c.ok("%s satır bir kez: %s" % (tag, ln), r.count(ln) == 1, "sayı %d" % r.count(ln))
    for ln in absent:
        c.ok("%s satır yok: %s" % (tag, ln), not r.has(ln))
    for pre in absent_pre:
        c.ok("%s '%s' ile başlayan satır yok" % (tag, pre), r.pre(pre) == 0, "sayı %d" % r.pre(pre))
    for parts in absent_has:
        c.ok("%s şunları birlikte içeren satır yok: %s" % (tag, parts),
             not any(all(x in ln for x in parts) for ln in r.lines))


# --- sabotajlar (paket tablosu) --------------------------------------------------------------------------------------------
PICK = "Platform/PersonReferences/person-reference-picker.js"
TRACE = "Traceability/trace-ui.js"
MODAL = "pages/enterprise-strategy/modal-form-utils.js"
GOALS = "pages/enterprise-strategy/goals.js"
DECO = "pages/decomposition/decompositionPage.js"
CYCLE = "CRM/cycle-dates.js"
V_DECO = "Views/DecompositionTreeBuilder/Index.cshtml"
V_CYC_D = "Views/CRM/CyclePeriods/Details.cshtml"
V_CYC_I = "Views/CRM/CyclePeriods/Index.cshtml"
V_SERIALS = "Views/Traceability/Serials/Index.cshtml"
GOALS_TAG = '<script src="~/assets/js/%s"></script>' % GOALS


def sb_simple(tag, x, fn_edit):
    return scen(tag, W, [(jpath(x), fn_edit)])


def mk_sb(key, tag, x, fn_edit, with_revert=True):
    def run():
        d = dict(zip("PSR", sb_simple(tag, x, fn_edit)))
        if with_revert:
            d["rev"] = revert(tag, d["S"])
        return d
    return memo(key, run)


def sb1():
    return mk_sb("sb1", "SB-1", PICK, app(ALERT))


def sb2():
    return mk_sb("sb2", "SB-2", TRACE, app(ALERT))


def sb3():
    return mk_sb("sb3", "SB-3", MODAL, styled(jinst(MODAL)))


def sb4():
    return mk_sb("sb4", "SB-4", GOALS, styled(jinst(GOALS)))


def sb5():
    return mk_sb("sb5", "SB-5", GOALS, app("\n"), with_revert=False)


def sb6():
    def run():
        where = []
        d = dict(zip("PSR", scen("SB-6", W, [(WEB + V_DECO, drop_tag("assets/js/" + DECO, where))])))
        d["satır"] = where[0]
        note("SB-6 etiket satırı", TABLO["SB-6 satır"], where[0])
        d["rev"] = revert("SB-6", d["S"])
        return d
    return memo("sb6", run)


def sb6_next():
    def run():
        d = sb6()
        kcheckout(d["S"])
        wr(WEB + V_DECO, rd(WEB + V_DECO) + "\n")
        f = kcommit([WEB + V_DECO], "SB-6 sonrası aynı sayfaya dokunan commit")
        r = sel_base(d["S"], "SB-6-sonraki")
        cb = selector(["--check-baseline"], "SB-6-sonraki-check-baseline")
        return f, r, cb
    return memo("sb6n", run)


def sb7():
    def run():
        kcheckout(W)
        if not tag_rx("assets/js/" + CYCLE).search(rd(WEB + V_CYC_I)):
            raise RuntimeError("ön koşul: Index.cshtml etiketi taşımıyor (SB-7 'Index keeps it')")
        return dict(zip("PSR", scen("SB-7", W, [(WEB + V_CYC_D, drop_tag("assets/js/" + CYCLE))])))
    return memo("sb7", run)


def sb8():
    return mk_sb("sb8", "SB-8", V_CYC_I, app("\n" + GOALS_TAG + "\n"))


def sb9():
    def run():
        d = dict(zip("PSR", scen("SB-9", W, [(WEB + V_SERIALS, drop_tag("assets/js/" + TRACE)), (jpath(TRACE), app(ALERT))])))
        d["rev"] = revert("SB-9", d["S"])
        return d
    return memo("sb9", run)


def sb10(letter):
    x = "CRM/Accounts/index.js" if letter in "ab" else "Recall/cases.js"
    fn = app("\n") if letter == "a" else app(ALERT)
    return mk_sb("sb10" + letter, "SB-10" + letter, x, fn, with_revert=False)


def sb10a_parent():
    def run():
        return dict(zip("PSR", scen("SB-10a-taban", B, [(jpath("CRM/Accounts/index.js"), app("\n"))])))
    return memo("sb10ap", run)


def sb11_files():
    return [p for k, p in siniflar() if k in ("shared", "yok")]


def sb11():
    def run():
        files = sb11_files()
        if not files:
            raise RuntimeError("oracle siniflar: shared ya da yok dosyası yok")
        return dict(zip("PSR", scen("SB-11", W, [(WEB + p, app(ALERT)) for p in files])))
    return memo("sb11", run)


def sb12():
    return memo("sb12", lambda: dict(zip("PSR", scen("SB-12", W, [(WEB + V_CYC_I, app("\n@* " + GOALS_TAG + " *@\n"))]))))


def ac1_run():
    def run():
        files = [p for _, p in siniflar()]
        return dict(zip("PSR", scen("AC-1", W, [(WEB + p, app("\n")) for p in files], "106 sınıf dosyasına boş satır")))
    return memo("ac1", run)


# --- AC-1 ... AC-13 -------------------------------------------------------------------------------------------------------
def ac1(c):
    d = ac1_run()
    r = d["R"]
    pairs = ciftler()
    c.eq("çıkış", r.code, 0)
    c.eq("YENİ KIRMIZI satırı", r.pre("YENİ KIRMIZI "), 0)
    cnt = collections.Counter()
    for ln in r.lines:
        m = re.match(r"^PASS (\S+) CI-4 (\S+?)(?: \d+)?$", ln) or re.match(r"^TABAN FU-19 (\S+) CI-4 (\S+) \d+$", ln)
        if m:
            cnt[(m.group(1), m.group(2))] += 1
    bad = [p for p in pairs if cnt[p] != 1]
    c.ok("%d (sayfa, dosya) çiftinin her birinde tam bir CI-4 PASS ya da TABAN FU-19 satırı" % len(pairs), not bad,
         "uymayan %d, ilk: %s" % (len(bad), bad[:2]))
    c.eq("TABAN FU-19 satırı", r.pre("TABAN FU-19 "), len(satirlar_base()))
    c.eq("(FU-18) içeren satır", sum(1 for ln in r.lines if "(FU-18)" in ln), 0)


def ac2(c):
    files = sb11_files()
    d = sb11()
    r = d["R"]
    kinds = {p: k for k, p in siniflar()}
    c.ok("oracle siniflar shared + yok = %d dosya" % len(files), len(files) > 0)
    pre = "eşlenmemiş sayfa JS'i, "
    texts = {"yok": pre + "hiçbir görünüm yüklemiyor, sayfa kontrolü yok (MOD-0286-FU01) ",
             "shared": pre + "yalnız Shared görünümü yüklüyor, sayfa kontrolü yok (MOD-0286-FU01) "}
    expect(c, "SB-11", r, 0, present=[texts[kinds[p]] + WEB + p for p in files], absent_pre=["YENİ KIRMIZI "])
    c.eq("'MOD-0286-FU01' içeren satır", sum(1 for ln in r.lines if "MOD-0286-FU01" in ln), len(files))
    chk = [ln for ln in r.lines if re.match(r"^(PASS|TABAN|YENİ KIRMIZI) ", ln) and any(p in ln for p in files)]
    c.eq("bu dosyaları bir kontrol satırında anan satır", len(chk), 0)


def ac3(c):
    def parent():
        kcheckout(B)
        p = head()
        wr(jpath(PICK), rd(jpath(PICK)) + ALERT)
        kcommit([jpath(PICK)], "SB-1 taban seçicide")
        r = sel_base(p, "SB-1-taban")
        expect(c, "SB-1 (taban seçici ve taban)", r, 0,
               present=["eşlenmemiş sayfa JS'i, sayfa kontrolü yok (FU-18) " + WEB + jinst(PICK)])
    c.guard("SB-1 taban", parent)
    red = None
    try:
        red = turetim()[1]
    except Exception as e:                               # noqa: BLE001
        c.fails.append("turetim: " + str(e)[:100])

    def one(label, fn, key, extra=None):
        def go():
            d = fn()
            lines = red_lines(key)
            c.ok("%s: oracle'a göre kırmızı satırı var" % label, len(lines) > 0, lines)
            expect(c, label, d["R"], 1, once=lines, present=(extra or {}).get("present", ()),
                   absent_has=(extra or {}).get("absent_has", ()))
            c.eq("%s YENİ KIRMIZI satırı (oracle kırmızı)" % label, d["R"].pre("YENİ KIRMIZI "), red[key] if red else None)
            expect(c, label + " geri alma", d["rev"], 0)
        c.guard(label, go)
    one("SB-1", sb1, "SB-1")
    one("SB-2", sb2, "SB-2")
    one("SB-3", sb3, "SB-3")
    one("SB-4", sb4, "SB-4")
    one("SB-8", sb8, "SB-8", {"present": ["başvuru eklendi CRM/CyclePeriods " + jinst(GOALS)]})
    one("SB-9", sb9, "SB-9", {"present": ["başvuru kalktı Traceability/Serials " + jinst(TRACE)],
                                "absent_has": [(" Traceability/Serials CI-7 " + jinst(TRACE),)]})

    def anchors():
        _, _, anch = turetim()
        for x in (MODAL, GOALS):
            got = ANCHOR.get(jinst(x))
            c.eq("çapa %s (betik metinle buldu, oracle çapa)" % x.rsplit("/", 1)[1], got, anch.get(jinst(x)))
    c.guard("çapalar", anchors)


def ac4(c):
    def sb5_chk():
        d = sb5()
        rows = [r for r in turetim()[0]["SB-5"]]
        expect(c, "SB-5", d["R"], 0, present=["TABAN FU-19 %s %s %s %d" % (p, ch, i, n) for (p, ch, i, n, _) in rows],
               absent_pre=["YENİ KIRMIZI"])
    c.guard("SB-5", sb5_chk)

    def sb6_chk():
        d = sb6()
        p, ch, i, n, _ = turetim()[0]["SB-6"][0]
        expect(c, "SB-6", d["R"], 0,
               present=["başvuru kalktı %s %s" % (p, i), "kapandı - satır silinmeli %s %s %s" % (p, ch, i)],
               absent=["TABAN FU-19 %s %s %s %d" % (p, ch, i, n)])
        expect(c, "SB-6 geri alma", d["rev"], 0,
               present=["başvuru eklendi %s %s" % (p, i), "TABAN FU-19 %s %s %s %d" % (p, ch, i, n)])
        f, r, cb = sb6_next()
        expect(c, "SB-6 sonraki commit", r, 0, present=["kapandı - satır silinmeli %s %s %s" % (p, ch, i)])
        c.eq("SB-6 sonrası --check-baseline çıkış", cb.code, 2)
    c.guard("SB-6", sb6_chk)

    def sb7_chk():
        d = sb7()
        expect(c, "SB-7", d["R"], 0, present=["PASS CRM/CyclePeriods CI-4 %s 0" % jinst(CYCLE)], absent_pre=["başvuru kalktı"])
    c.guard("SB-7", sb7_chk)

    def sb12_chk():
        d = sb12()
        expect(c, "SB-12", d["R"], 0, absent_pre=["başvuru eklendi"], absent_has=[("CRM/CyclePeriods", "goals.js")])
    c.guard("SB-12", sb12_chk)
    c.guard("FP-1 araç hatası", lambda: fp1(c))
    c.guard("FP-2 sınıf değişimi", lambda: fp2(c))


def fp1(c):
    """Failure Path 'Tool error' (F-18): tabanın taban dosyası okunamaz / base commit değil -> çıkış 2, asla '0 sayfa'."""
    kcheckout(W)
    g("rm", "--quiet", "--", BASELINE)
    g("commit", "--quiet", "-m", "taban dosyası yok")
    dsha = head()
    g("checkout", "--force", W, "--", BASELINE)
    wr(jpath("CRM/Accounts/index.js"), rd(jpath("CRM/Accounts/index.js")) + "\n")
    kcommit([BASELINE, jpath("CRM/Accounts/index.js")], "taban geri, bir sayfa değişti")
    r = sel_base(dsha, "FP-1a")
    c.eq("FP-1a taban dosyası tabanda yok: çıkış", r.code, 2)
    c.ok("FP-1a hata iletisi var, '0 sayfa' sonucu değil", bool(r.err.strip()) and r.pre("PASS ") + r.pre("TABAN ") == 0, r.err[:100])
    blob = g("rev-parse", "%s:%s" % (W, SELECTOR))
    r2 = sel_base(blob, "FP-1b")
    c.eq("FP-1b base bir commit değil: çıkış", r2.code, 2)


def fp2(c):
    """Failure Path 'Class changes under a row' (F-18): yeni Views/{X}/{Sub}/ sınıf dosyasını sayfanın kendi dosyası yapar."""
    kcheckout(W)
    cand = []
    for (page, check, inst, n) in satirlar_base():
        m = re.match(r"^wwwroot/assets/js/([^/]+)/([^/]+)/[^/]+\.js$", inst)
        if m and m.group(1) != "pages" and not os.path.isdir(os.path.join(K, WEB, "Views", m.group(1), m.group(2))):
            cand.append((page, check, inst, int(n), m.group(1), m.group(2)))
    if not cand:
        raise RuntimeError("satırı olan ve görünüm klasörü olmayan JS alt klasörü dosyası yok (oracle satirlar)")
    page0, _, inst, _, x, sub = sorted(cand)[0]
    rows = [(ch, n) for (pg, ch, ii, n, _, _) in cand if ii == inst and pg == page0]
    newpage = "%s/%s" % (x, sub)
    old_view = None
    vdir = os.path.join(K, WEB, "Views", *page0.split("/"))
    for f in sorted(os.listdir(vdir)) if os.path.isdir(vdir) else []:
        if f.endswith(".cshtml") and "assets/js/" + inst.split("assets/js/", 1)[1] in rd(os.path.join(WEB, "Views", *page0.split("/"), f)):
            old_view = os.path.join(WEB, "Views", *page0.split("/"), f)
            break
    if not old_view:
        raise RuntimeError("%s sayfasının J'yi anan görünümü bulunamadı" % page0)
    new_view = "%sViews/%s/%s/Index.cshtml" % (WEB, x, sub)
    p, s, r = scen("FP-2", W, [(new_view, lambda t: '@{\n    Layout = "_LayoutTenantShell";\n}\n'), (old_view, app("\n"))])
    present = ["YENİ KIRMIZI %s %s %s %d +%d" % (newpage, ch, inst, n, n) for ch, n in rows]
    present += ["kapandı - satır silinmeli %s %s %s" % (page0, ch, inst) for ch, _ in rows]
    expect(c, "FP-2 (%s -> %s)" % (page0, newpage), r, 1, present=present)


def ac5(c):
    def a():
        pa, pw = sb10a_parent(), sb10("a")
        drop = ("süre:", "başvuru taraması", "okunan görünüm metni")
        f = lambda r: [ln for ln in r.lines if not ln.startswith(drop)]       # noqa: E731
        c.eq("SB-10a taban çıkış", pa["R"].code, 0)
        c.eq("SB-10a iş çıkış", pw["R"].code, 0)
        c.ok("SB-10a stdout iş = taban (süre, başvuru taraması, okunan görünüm metni satırları atıldı)", f(pa["R"]) == f(pw["R"]),
             "satır %d / %d" % (len(f(pa["R"])), len(f(pw["R"]))))
    c.guard("SB-10a", a)
    for letter, page, x in (("b", "CRM/Accounts", "CRM/Accounts/index.js"), ("c", "Recall", "Recall/cases.js")):
        def go(letter=letter, page=page, x=x):
            d = sb10(letter)
            row = [r for r in turetim()[0]["SB-10"] if r[0] == page][0]
            expect(c, "SB-10" + letter, d["R"], 1, present=["YENİ KIRMIZI %s %s %s %d +%d" % (row[0], row[1], row[2], row[3], row[4])])
        c.guard("SB-10" + letter, go)


# SB-13 satırları paketten metinle okunur
def sb13_table():
    text = rd_commit(W, PACK)
    sec = text[text.index("**8. Baseline reader"):text.index("**9. CI")]
    tt = {}
    for m in re.finditer(r"T-(\d)\s+`([^`]+)`", sec):
        tt.setdefault("T-" + m.group(1), m.group(2))
    e = re.search(r"`<e>` = `([^`]+)`", text).group(1)
    rr = re.search(r"`reason` is `([^`]+)` and `owner` is `([^`]+)`", text)
    rows = []
    for ln in text.split("\n"):
        if re.match(r"^\| R-\w+ \|", ln):
            cells = [x.strip() for x in ln.strip().strip("|").split("|")]
            rid, page, check, inst, count, fu, res = cells[:7]
            inst = inst.strip("`").replace("<e>", e)
            if res.startswith("Accepted"):
                want = None
            elif re.match(r"^T-\d", res):
                want = tt[res[:3]]
            else:
                want = re.search(r"`([^`]+)`", res).group(1)
            rows.append((rid, "\t".join([page, check, inst, count, rr.group(1), fu, rr.group(2)]), want))
    if len(rows) < 17 or len(tt) < 7:
        raise RuntimeError("SB-13 tablosu ya da T-1..T-7 metinleri okunamadı (%d satır, %d metin)" % (len(rows), len(tt)))
    return rows


def rd_commit(sha, path):
    r = sh(["git", "show", "%s:%s" % (sha, path)], K)
    if r.code:
        raise RuntimeError("git show %s:%s: %s" % (sha[:9], path, r.err.strip()[:100]))
    return r.out


def ac6(c):
    rows = sb13_table()
    kcheckout(W)
    base_txt = rd(BASELINE)
    n = base_txt.count("\n") + (0 if base_txt.endswith("\n") else 1) + 1
    prefix = "HATA: taban satır %d: " % n

    def one(rid, row, want):
        kcheckout(W)
        wr(BASELINE, base_txt + ("" if base_txt.endswith("\n") else "\n") + row + "\n")
        r = selector(["--check-baseline"], "AC-6-" + rid)
        if want is None:
            c.ok("%s kabul: çıkış %s" % (rid, r.code), r.code == 0)
        else:
            c.ok("%s: çıkış %s, '%s%s'" % (rid, r.code, prefix, want), r.code == 2 and (prefix + want) in r.alll,
                 [ln for ln in r.alll if ln.startswith("HATA")][:2])
        if rid in ("R-1", "R-5", "R-10"):
            kcheckout(W)
            p = head()
            wr(BASELINE, base_txt + ("" if base_txt.endswith("\n") else "\n") + row + "\n")
            kcommit([BASELINE], "SB-13 " + rid)
            r2 = sel_base(p, "AC-6-%s-base" % rid)
            c.ok("%s --base <ata>: çıkış %s, '%s%s'" % (rid, r2.code, prefix, want), r2.code == 2 and (prefix + want) in r2.alll,
                 [ln for ln in r2.alll if ln.startswith("HATA")][:2])
    for rid, row, want in rows:
        c.guard(rid, lambda rid=rid, row=row, want=want: one(rid, row, want))
    kcheckout(W)
    r = selector(["--check-baseline"], "AC-6-temiz")
    c.eq("değişmemiş taban --check-baseline çıkış", r.code, 0)


def parse_rows(lines, fu=None, start=0):
    out = []
    for ln in lines[start:]:
        cells = ln.split("\t")
        if len(cells) >= 7 and (fu is None or cells[5] == fu):
            out.append(tuple(cells[:4]))
    return sorted(out)


def diff_rows(a, b):
    ca, cb = collections.Counter(a), collections.Counter(b)
    return ["- %s" % "\t".join(k) for k in (ca - cb).elements()] + ["+ %s" % "\t".join(k) for k in (cb - ca).elements()]


def plant(rows):
    ex = rows + [("Zzz/Plant", "CI-7", "wwwroot/assets/js/Zzz/plant.js", "1")]
    low = None
    for i, r in enumerate(rows):
        if int(r[3]) >= 2:
            low = rows[:i] + [(r[0], r[1], r[2], str(int(r[3]) - 1))] + rows[i + 1:]
            break
    if low is None and rows:
        r = rows[0]
        low = [(r[0], r[1], r[2], str(int(r[3]) - 1))] + rows[1:]
    return ex, low


def ac7(c):
    kcheckout(W)
    orows = sorted(tuple(ln.split("\t")) for ln in ora_ok("satirlar", W).lines)
    base_rows = parse_rows(rd(BASELINE).split("\n")[1:], "FU-19")
    d = diff_rows(orows, base_rows)
    c.ok("oracle satirlar %d satır = tabandaki FU-19 satırları %d (iki yönlü)" % (len(orows), len(base_rows)), not d, d[:3])
    dr = selector(["--draft-baseline"], "AC-7-draft", 1800, heavy=True)
    c.eq("--draft-baseline çıkış", dr.code, 0)
    drows = parse_rows(dr.lines[1:], "FU-19")
    d2 = diff_rows(orows, drows)
    c.ok("oracle satirlar = --draft-baseline FU-19 satırları %d (iki yönlü)" % len(drows), not d2, d2[:3])
    ref = base_rows if base_rows else orows
    if not base_rows:
        log("  AC-7 not: tabanda FU-19 satırı yok; ekilen sabotaj oracle satırlarının kopyası üzerinde yapıldı")
    ex, low = plant(ref)
    dd = diff_rows(orows, ex)
    c.ok("ekilmiş fazla satır karşılaştırmayı bozar", bool(dd), "fark: %s" % dd[:2])
    c.ok("ekilmiş düşürülmüş sayı karşılaştırmayı bozar", low is not None and bool(diff_rows(orows, low)),
         "fark: %s" % (diff_rows(orows, low)[:2] if low else "satır yok"))


def ac8(c):
    owners_path, decision = X["owners"], X["decision"]
    kcheckout(W)
    ns = g("diff", "--numstat", B, "--", BASELINE).split("\t")
    added, deleted = (int(ns[0]), int(ns[1])) if len(ns) >= 3 else (0, 0)
    c.eq("tabana eklenen satır (oracle satirlar <taban>)", added, len(satirlar_base()))
    c.eq("tabandan silinen ya da değişen satır", deleted, 0)
    own = {}
    for ln in open(owners_path, encoding="utf-8").read().split("\n"):
        cells = ln.split("\t")
        if len(cells) >= 2 and cells[0].strip() and not ln.startswith("#") and cells[0] != "page":
            own[cells[0]] = cells[1].strip()
    lines = rd(BASELINE).split("\n")[1:]
    fu19 = [ln.split("\t") for ln in lines if ln.split("\t")[5:6] == ["FU-19"]]
    c.eq("FU-19 satırı", len(fu19), len(satirlar_base()))
    c.eq("FU-19 satırında taban-01 / tek-seviye-borc-01 / belirsiz", sum(1 for ln in lines if ln.split("\t")[5:6] == ["FU-19"]
                                                                          and re.search(r"taban-01|tek-seviye-borc-01|belirsiz", ln)), 0)
    rx = re.compile(r"^sahip kararı %s \(\d{4}-\d{2}-\d{2}\)$" % re.escape(decision))
    c.eq("reason'ı kararı adlandırmayan FU-19 satırı", sum(1 for r in fu19 if not rx.match(r[4])), 0)
    c.eq("owner'ı eşlemeyle (sayfa -> sahip) uyuşmayan ya da eşlemede olmayan FU-19 satırı",
         sum(1 for r in fu19 if own.get(r[0]) is None or own[r[0]] != r[6]), 0)
    cb = collections.Counter(ln.split("\t")[5] for ln in g("show", "%s:%s" % (B, BASELINE)).split("\n")[1:] if ln)
    cw = collections.Counter(ln.split("\t")[5] for ln in lines if ln)
    cw.pop("FU-19", None)
    cb.pop("FU-19", None)
    c.ok("FU-19 dışındaki fu sayıları değişmedi: %s" % dict(cb), cb == cw, dict(cw))


def ac9(c):
    gm, ga = gor_crm(), gor_all()
    a = sb10("a")
    expect(c, "SB-10a", a["R"], 0, present=["başvuru taraması yok", "okunan görünüm metni: HEAD %d, taban 0" % gm])
    d = ac1_run()
    rx = re.compile(r"^başvuru taraması: HEAD %d görünüm, taban %d görünüm, [0-9.]+ s$" % (ga, ga))
    c.ok("AC-1 koşusunda 'başvuru taraması: HEAD %d görünüm, taban %d görünüm, <s> s'" % (ga, ga), any(rx.match(ln) for ln in d["R"].lines))
    c.ok("AC-1 koşusunda 'süre:' satırı", d["R"].pre("süre: ") == 1)
    kcheckout(W)
    e = selector(["--base", "HEAD"], "AC-9-bos")
    c.ok("--base HEAD boş fark: çıkış %s, tek satır" % e.code,
         e.code == 0 and len(e.lines) == 1 and re.match(r"^süre: [0-9]+\.[0-9] s$", e.lines[0]) is not None, e.lines[:3])
    hw = selector(["--help"], "AC-9-help-is")
    kcheckout(B)
    hb = selector(["--help"], "AC-9-help-taban")
    fw = set(re.findall(r"--[a-z-]+", hw.out)) - {"--help"}
    fb = set(re.findall(r"--[a-z-]+", hb.out)) - {"--help"}
    c.ok("--help dört kip: %s" % sorted(fw), fw == FOUR_MODES and fb == FOUR_MODES, "iş %s taban %s" % (sorted(fw), sorted(fb)))


def self_test(sha, name):
    kcheckout(sha)
    return selector(["--self-test"], name, 3600, heavy=True)


def func_range(src, fname):
    for node in ast.walk(ast.parse(src)):
        if isinstance(node, ast.FunctionDef) and node.name == fname:
            return node.lineno, node.end_lineno
    raise RuntimeError(fname + " işlevi yok")


def diff_lines(base, work):
    out = sh(["git", "diff", "-U0", base, work, "--", SELECTOR], K).out
    rem, add = [], []
    o = n = 0
    for ln in out.split("\n"):
        m = re.match(r"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@", ln)
        if m:
            o, n = int(m.group(1)), int(m.group(3))
        elif ln.startswith("-") and not ln.startswith("---"):
            rem.append((o, ln[1:]))
            o += 1
        elif ln.startswith("+") and not ln.startswith("+++"):
            add.append((n, ln[1:]))
            n += 1
    return rem, add


def ac10(c):
    st = self_test(W, "AC-10-self-test")
    c.eq("--self-test çıkış", st.code, 0)
    c.eq("'FAIL ' satırı", st.pre("FAIL "), 0)
    missing = [i for i in range(1, 68) if st.pre("PASS FU01-%02d " % i) < 1]
    c.ok("FU01-01...FU01-67 her biri için en az bir 'PASS FU01-nn ' satırı", not missing, "eksik: %s" % missing[:12])
    base_src, work_src = sh(["git", "show", "%s:%s" % (B, SELECTOR)], K).out, sh(["git", "show", "%s:%s" % (W, SELECTOR)], K).out
    b0, b1 = func_range(base_src, "self_test")
    w0, w1 = func_range(work_src, "self_test")
    blines = base_src.split("\n")
    expected = sorted(ln for i, ln in enumerate(blines, 1) if b0 <= i <= b1 and (re.search(r"\bFU18 = ", ln) or "{FU18}" in ln))
    rem, add = diff_lines(B, W)
    rem_s = sorted(t for (i, t) in rem if b0 <= i <= b1)
    add_s = [t for (i, t) in add if w0 <= i <= w1]
    c.ok("self_test'te kaldırılan satırlar tam olarak tabanın FU18 tanımı ve olumlu savları (%d satır)" % len(expected), rem_s == expected,
         "kaldırılan %d" % len(rem_s))
    users = [t for t in add_s if "NO_VIEW_LINE" in t and not re.match(r"^\s*NO_VIEW_LINE\s*=", t)]
    c.ok("yerine konan satırlar NO_VIEW_LINE'ı anıyor (%d satır)" % len(users), len(users) >= max(0, len([1 for t in expected if "{FU18}" in t])))
    try:
        wt = ast.parse(work_src)
        env = {}
        for node in ast.walk(wt):
            if isinstance(node, ast.Assign):
                for t, v in ([(t, node.value) for t in node.targets if isinstance(t, ast.Name)] +
                             [(tt, vv) for t in node.targets if isinstance(t, ast.Tuple) and isinstance(node.value, ast.Tuple)
                              for tt, vv in zip(t.elts, node.value.elts) if isinstance(tt, ast.Name)]):
                    env.setdefault(t.id, []).append((node.lineno, v))

        def resolve(v, depth=0):
            if depth > 6:
                return None
            if isinstance(v, ast.Constant) and isinstance(v.value, str):
                return v.value
            if isinstance(v, ast.Name) and env.get(v.id):
                return resolve(env[v.id][-1][1], depth + 1)
            if isinstance(v, ast.BinOp) and isinstance(v.op, ast.Add):
                x, y = resolve(v.left, depth + 1), resolve(v.right, depth + 1)
                return None if x is None or y is None else x + y
            return None
        nv = env.get("NO_VIEW_LINE")
        val = resolve(nv[-1][1]) if nv else None
        seg = ast.get_source_segment(work_src, nv[-1][1]) if nv else None
        c.ok("NO_VIEW_LINE değeri 'hiçbir görünüm' içeriyor", bool(nv) and "hiçbir görünüm" in (val if val is not None else seg or ""), val or seg)
        fv = [(ln, v) for (ln, v) in env.get("FU18", []) if w0 <= ln <= w1]
        fval = resolve(fv[-1][1]) if fv else None
        c.ok("FU18 'eşlenmemiş sayfa JS'i, ' önekine döndü", fval is not None and fval.startswith("eşlenmemiş sayfa JS'i, ") and "FU-18" not in fval,
             fval if fval is not None else (ast.get_source_segment(work_src, fv[-1][1]) if fv else "FU18 tanımı yok"))
    except Exception as e:                               # noqa: BLE001
        c.fails.append("ast: " + str(e)[:100])
    names = lambda r: {re.sub(r"^(PASS|FAIL) ", "", ln) for ln in r.lines if re.match(r"^(PASS|FAIL) ", ln)}   # noqa: E731
    sb_ = st if W == B else self_test(B, "AC-10-self-test-taban")
    lost = sorted(names(sb_) - names(st))
    c.ok("tabanın hiçbir vaka adı eksik değil (%d ad)" % len(names(sb_)), not lost, lost[:2])


def ac11(c):
    d = [x for x in g("diff", "--name-only", B, W).split("\n") if x]
    c.ok("iş commit'inde yalnız iki Repo Scope dosyası: %s" % d, set(d) == {SELECTOR, BASELINE}, d)
    for p in (PARENT_PACK, WORKFLOW):
        c.ok("%s aynı blob" % p.rsplit("/", 1)[1], g("rev-parse", "%s:%s" % (B, p)) == g("rev-parse", "%s:%s" % (W, p)))
    c.ok("taban işin atası", sh(["git", "merge-base", "--is-ancestor", B, W], K).code == 0)


def ac12(c):
    kcheckout(W)
    r = sel_base(B, "AC-12-yerel")
    c.ok("işin yerel eşdeğeri (--base <taban>) çıkış %s" % r.code, r.code == 0)
    path = X["ci"]
    if not path:
        c.fails.append("CI kaydı verilmedi (--ci-kaydi): koşu kimliği, head SHA ve artefakt SHA-256 kayıtta olmalı; betik CI'ı koşmaz")
        return
    kv = dict(ln.split("=", 1) for ln in open(path, encoding="utf-8").read().split("\n") if "=" in ln)
    c.ok("CI kaydı: run_id sayı", re.fullmatch(r"\d+", kv.get("run_id", "")) is not None)
    c.ok("CI kaydı: head_sha iş commit'i", kv.get("head_sha") == W)
    c.ok("CI kaydı: artefakt_sha256 64 onaltılık", re.fullmatch(r"[0-9a-f]{64}", kv.get("artefakt_sha256", "")) is not None)


def dict_spans(src_text):
    src = src_text.encode("utf-8")
    starts = [0]
    for ln in src.split(b"\n"):
        starts.append(starts[-1] + len(ln) + 1)
    for node in ast.parse(src_text).body:
        if isinstance(node, ast.Assign) and any(isinstance(t, ast.Name) and t.id == "FU01_REF_PATTERNS" for t in node.targets) \
                and isinstance(node.value, ast.Dict):
            sp = {}
            for k, v in zip(node.value.keys, node.value.values):
                if isinstance(k, ast.Constant) and isinstance(v, ast.Constant) and isinstance(v.value, str):
                    sp[k.value] = (starts[v.lineno - 1] + v.col_offset, starts[v.end_lineno - 1] + v.end_col_offset, v.value)
            return sp, src
    return None, src


def mutate_dict(key, old, new):
    """F-28: değeri metin değiştirerek değil ast konumuyla bozar; yalnız o değer değişir, kaynak derlenir."""
    text = rd(SELECTOR)
    sp, src = dict_spans(text)
    if sp is None or key not in sp:
        raise RuntimeError("FU01_REF_PATTERNS sözlüğü yok ya da '%s' değeri düz dizge değil" % key)
    s, e, val = sp[key]
    if old not in val:
        raise RuntimeError("'%s' değerinde '%s' yok" % (key, old))
    nv = val.replace(old, new, 1)
    flags = {"STRIP": re.S, "SCRIPT": re.I | re.S, "VARIABLE": re.I | re.S}[key]
    re.compile(nv, flags)
    out = (src[:s] + repr(nv).encode("utf-8") + src[e:]).decode("utf-8")
    sp2, _ = dict_spans(out)
    if any(sp2[k][2] != (nv if k == key else sp[k][2]) for k in sp) or set(sp2) != set(sp):
        raise RuntimeError("bozma yalnız '%s' değerini değiştirmedi" % key)
    wr(SELECTOR, out)


MUTS = [("a", "STRIP", "@\\*.*?\\*@", "@\\#.*?\\*@", "FU01-15"),
        ("b", "SCRIPT", "@Url\\.Content", "@Urx\\.Content", "FU01-13"),
        ("c", "VARIABLE", "[^>]*?assets/js/|", "[^>]*?assets/jz/|", "FU01-62"),
        ("d", "VARIABLE", "(?<!@)@(?!@)", "(?<!@)#(?!@)", "FU01-19")]


def ac13(c):
    r = ora("secici", W)
    c.ok("oracle secici <iş>: 'aynı', çıkış %s" % r.code, r.code == 0 and r.lines == ["aynı"], r.lines[:2])
    for letter, key, old, new, case in MUTS:
        def go(letter=letter, key=key, old=old, new=new, case=case):
            kcheckout(W)
            mutate_dict(key, old, new)
            m = kcommit([SELECTOR], "SB-14" + letter)
            s = sh(["python3", "-B", ORACLE, "secici", m], K, "SB-14%s-secici" % letter)
            c.ok("SB-14%s secici çıkış %s, 'farklı<TAB>%s'" % (letter, s.code, key), s.code == 1 and s.has("farklı\t" + key), s.lines[:3])
            st = selector(["--self-test"], "SB-14%s-self-test" % letter, 3600, heavy=True)
            c.ok("SB-14%s --self-test çıkış %s (sıfırdan farklı)" % (letter, st.code), st.code != 0)
            c.ok("SB-14%s 'FAIL FU01-67 ' ile başlayan satır" % letter, st.pre("FAIL FU01-67 ") >= 1)
            c.ok("SB-14%s 'FAIL %s ' ile başlayan satır" % (letter, case), st.pre("FAIL %s " % case) >= 1)
            g("revert", "--no-edit", m)
            rv = sh(["python3", "-B", ORACLE, "secici", head()], K, "SB-14%s-secici-geri" % letter)
            c.ok("SB-14%s geri alma: 'aynı', çıkış %s" % (letter, rv.code), rv.code == 0 and rv.lines == ["aynı"], rv.lines[:2])
        c.guard("SB-14" + letter, go)

    def e():
        line = None
        lines = red_lines("SB-1")
        kcheckout(W)
        mutate_dict("SCRIPT", "(assets/js/", "(assets/jz/")
        m = kcommit([SELECTOR], "SB-14e dict bozuk")
        wr(jpath(PICK), rd(jpath(PICK)) + ALERT)
        kcommit([jpath(PICK)], "SB-14e SB-1 üstüne")
        rm_ = sel_base(m, "SB-14e-bozuk")
        log("  SB-14e bozuk koşu çıkışı %s (iddia edilmez)" % rm_.code)
        for line in lines:
            c.ok("SB-14e bozuk: satır yok: %s" % line, not rm_.has(line))
        kcheckout(W)
        p = head()
        wr(jpath(PICK), rd(jpath(PICK)) + ALERT)
        kcommit([jpath(PICK)], "SB-14e dict düzeltilmiş")
        rr = sel_base(p, "SB-14e-duzeltilmis")
        c.eq("SB-14e düzeltilmiş koşu çıkış", rr.code, 1)
        for line in lines:
            c.ok("SB-14e düzeltilmiş: satır var: %s" % line, rr.has(line))
    c.guard("SB-14e", e)


ACS = [("AC-1", ac1), ("AC-2", ac2), ("AC-3", ac3), ("AC-4", ac4), ("AC-5", ac5), ("AC-6", ac6), ("AC-7", ac7),
       ("AC-8", ac8), ("AC-9", ac9), ("AC-10", ac10), ("AC-11", ac11), ("AC-12", ac12), ("AC-13", ac13)]


# --- yan kip: oracle sabotajı ------------------------------------------------------------------------------------------------
def oracle_sabotaj():
    log("ORACLE SABOTAJI: uic-fu01-oracle-02.py geçici klonda, %s ağacında" % W[:12])
    kcheckout(W)
    pack = rd_commit(W, PACK)
    raw = {k: re.search(r"^%s\t(.*)$" % k, pack, re.M).group(1) for k in ("STRIP", "SCRIPT", "VARIABLE")}
    base_rows = parse_rows(rd(BASELINE).split("\n")[1:], "FU-19")
    has_dict = dict_spans(rd(SELECTOR))[0] is not None
    kur = []
    if not base_rows:
        rows = sorted(tuple(ln.split("\t")) for ln in ora_ok("satirlar", W).lines)
        add = "".join("\t".join(r + ("sahip kararı oracle-sabotaj (2026-11-01)", "FU-19", "platform-shared-services")) + "\n" for r in rows)
        t = rd(BASELINE)
        wr(BASELINE, t + ("" if t.endswith("\n") else "\n") + add)
        kur.append(BASELINE)
        log("kur: tabana oracle satirlar çıktısından %d FU-19 satırı eklendi (bugünkü HEAD'de yok)" % len(rows))
    if not has_dict:
        wr(SELECTOR, rd(SELECTOR) + "\nFU01_REF_PATTERNS = {\n" + "".join("    %r: %r,\n" % (k, raw[k]) for k in ("STRIP", "SCRIPT", "VARIABLE")) + "}\n")
        kur.append(SELECTOR)
        log("kur: seçici kopyasına FU01_REF_PATTERNS sözlüğü eklendi (paket bloğundan; bugünkü HEAD'de yok)")
    k0 = kcommit(kur, "oracle sabotajı kurulum") if kur else head()
    results = []

    def cmp_state(sha, label):
        orows = sorted(tuple(ln.split("\t")) for ln in ora_ok("satirlar", sha).lines)
        brows = parse_rows(rd(BASELINE).split("\n")[1:], "FU-19")
        return diff_rows(orows, brows), len(orows), len(brows)

    d, no, nb = cmp_state(k0, "kontrol")
    log("KONTROL: oracle satirlar %d satır, tabandaki FU-19 %d satır, fark %d -> %s" % (no, nb, len(d), "eşit" if not d else "FARKLI"))
    sc = sh(["python3", "-B", ORACLE, "secici", k0], K, "kontrol-secici")
    log("KONTROL: oracle secici -> %s çıkış %d" % (sc.lines, sc.code))
    results.append(("kontrol", not d and sc.code == 0 and sc.lines == ["aynı"]))

    kcheckout(k0)
    t = rd(BASELINE)
    wr(BASELINE, t + "\t".join(["Zzz/Plant", "CI-7", "wwwroot/assets/js/Zzz/plant.js", "1", "sahip kararı oracle-sabotaj (2026-11-01)", "FU-19", "platform-shared-services"]) + "\n")
    sa = kcommit([BASELINE], "sabotaj a fazla FU-19 satırı")
    d, no, nb = cmp_state(sa, "a")
    log("(a) tabana fazladan FU-19 satırı: oracle %d, taban %d, fark %s -> %s" % (no, nb, d, "YAKALANDI (kırmızı)" if d else "kırmızı yok"))
    results.append(("a", bool(d)))

    kcheckout(k0)
    lines = rd(BASELINE).split("\n")
    tgt = next(i for i, ln in enumerate(lines) if i > 0 and ln.split("\t")[5:6] == ["FU-19"] and int(ln.split("\t")[3]) >= 2)
    cells = lines[tgt].split("\t")
    cells[3] = str(int(cells[3]) - 1)
    lines[tgt] = "\t".join(cells)
    wr(BASELINE, "\n".join(lines))
    sb_ = kcommit([BASELINE], "sabotaj b sayı düşür")
    d, no, nb = cmp_state(sb_, "b")
    log("(b) bir FU-19 sayısı düşürüldü (%s): fark %s -> %s" % (cells[:3], d, "YAKALANDI (kırmızı)" if d else "kırmızı yok"))
    results.append(("b", bool(d)))

    kcheckout(k0)
    mutate_dict("VARIABLE", "(?<!@)@(?!@)", "(?<!@)#(?!@)")
    sc_ = kcommit([SELECTOR], "sabotaj c sözlükte farklı desen")
    r = sh(["python3", "-B", ORACLE, "secici", sc_], K, "sabotaj-c-secici")
    log("(c) secici modunda VARIABLE deseni farklı sözlük: oracle secici -> %s çıkış %d -> %s" %
        (r.lines, r.code, "YAKALANDI (kırmızı)" if r.code == 1 and r.has("farklı\tVARIABLE") else "kırmızı yok"))
    results.append(("c", r.code == 1 and r.has("farklı\tVARIABLE")))

    kcheckout(k0)
    wr(WEB + V_DECO, drop_tag("assets/js/" + DECO)(rd(WEB + V_DECO)))
    sd = kcommit([WEB + V_DECO], "sabotaj d bir sayfadan etiket kaldır")
    d, no, nb = cmp_state(sd, "d")
    log("(d) oracle girdisi: DecompositionTreeBuilder görünümünden decompositionPage.js etiketi kaldırıldı: oracle %d, taban %d, fark %s -> %s"
        % (no, nb, d, "YAKALANDI (kırmızı)" if d else "kırmızı yok"))
    results.append(("d", bool(d)))
    log("ÖZET: " + ", ".join("%s=%s" % (k, ("eşit" if v else "FARKLI") if k == "kontrol" else ("kırmızı" if v else "KIRMIZI YOK")) for k, v in results))
    return 0 if all(v for _, v in results) else 1


# --- giriş -------------------------------------------------------------------------------------------------------------------
def main():
    global OUT, K, LOGF, ORACLE, W, B, X
    ap = argparse.ArgumentParser(prog="uic-fu01-kanit-01.py")
    ap.add_argument("out")
    ap.add_argument("work")
    ap.add_argument("--base")
    ap.add_argument("--decision")
    ap.add_argument("--owners")
    ap.add_argument("--ci-kaydi")
    ap.add_argument("--klon-dizini")
    ap.add_argument("--klon-var")
    ap.add_argument("--oracle-sabotaj", action="store_true")
    a = ap.parse_args()
    for s in (sys.stdout, sys.stderr):
        s.reconfigure(encoding="utf-8", errors="replace")
    if not os.path.isdir(a.out) or not re.fullmatch(r"[0-9a-f]{40}", a.work):
        print("kullanım: <var olan çıktı klasörü> <iş tam sha (40 onaltılık)> ...")
        return 2
    if not a.oracle_sabotaj and not (a.base and a.decision and a.owners and os.path.isfile(a.owners)):
        print("kullanım: --base <sha> --decision <ad> --owners <TSV dosyası> gerekli")
        return 2
    OUT = os.path.abspath(a.out)
    fazla = [f for f in os.listdir(OUT) if f not in ("stdout.txt", "stderr.txt")]
    if fazla:
        print("çıktı klasörü boş değil (%s ...); yeni boş klasör ver" % fazla[0])
        return 2
    ORACLE = os.path.join(ROOT, ORACLE_REL)
    if not os.path.isfile(ORACLE):
        print("oracle yok: " + ORACLE)
        return 2
    LOGF = open(os.path.join(OUT, "akis.txt"), "w", encoding="utf-8")
    X = {"decision": a.decision, "owners": os.path.abspath(a.owners) if a.owners else None, "ci": a.ci_kaydi}
    t0 = time.time()
    if a.klon_var:
        K = os.path.abspath(a.klon_var)
    else:
        yuk_bekle()
        parent = a.klon_dizini or tempfile.gettempdir()
        K = os.path.join(tempfile.mkdtemp(prefix="uic-fu01-klon-", dir=parent), "klon")
        r = sh(["git", "clone", "--quiet", "--shared", ROOT, K], parent, "klon")
        if r.code:
            log("klon kurulamadı: " + r.err[:200])
            return 2
        g("remote", "remove", "origin", ok=True)
    W = a.work
    log("klon: %s  iş: %s  taban: %s" % (K, W, a.base))
    if sh(["git", "cat-file", "-e", W + "^{commit}"], K).code:
        log("iş commit'i klonda yok")
        return 2
    if a.oracle_sabotaj:
        return oracle_sabotaj()
    B = g("rev-parse", a.base + "^{commit}", ok=True)
    if not re.fullmatch(r"[0-9a-f]{40}", B or ""):
        log("taban çözülemedi: " + a.base)
        return 2
    gecti = 0
    sonuc = []
    for ad, fn in ACS:
        log("== %s" % ad)
        c = Chk(ad)
        try:
            fn(c)
        except Exception as e:                           # noqa: BLE001
            c.fails.append("hata %s: %s" % (type(e).__name__, str(e)[:160]))
        if c.fails:
            line = "%s kaldı %s" % (ad, "; ".join(c.fails[:3]) + (" (+%d)" % (len(c.fails) - 3) if len(c.fails) > 3 else ""))
        else:
            gecti += 1
            line = "%s geçti %d denetim" % (ad, c.n)
        sonuc.append(line)
        log(line)
    log("== ÖZET (süre %.0f s)" % (time.time() - t0))
    for ln in sonuc:
        log(ln)
    for ln in NOTES:
        log(ln)
    with open(os.path.join(OUT, "sonuc.txt"), "w", encoding="utf-8") as fh:
        for ln in sonuc:
            ad, durum = ln.split(" ")[0], ln.split(" ")[1]
            fh.write("%s zorunlu %s\n" % (ad, durum))
    log("zorunlu geçti %d / %d" % (gecti, len(ACS)))
    return 0 if gecti == len(ACS) else 1


if __name__ == "__main__":
    sys.exit(main())
