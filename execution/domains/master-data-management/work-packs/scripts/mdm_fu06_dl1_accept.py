#!/usr/bin/env python3
"""
MDM-FU06-DL1 — tek koşuluk kabul kanıtı (MOD-0290-FU06 §17 AC-1…AC-9: §18 testler, on iki sabotaj, E3 yükseltme yolu).

Kule yazar; işçi DEĞİŞTİRMEZ, yalnız koşar. Tek koşu, run_record altında, temiz ağaçta (aynı dizin iki kez verilir):

    python3 scripts/run_record.py run --out docs/records/audits/2026-11/mod0290-fu06-dl1/<yeni koşu dizini> -- \
        python3 execution/domains/master-data-management/work-packs/scripts/mdm_fu06_dl1_accept.py \
        --base-worktree <repo dışı mutlak yol> --out-dir docs/records/audits/2026-11/mod0290-fu06-dl1/<aynı dizin>

Neden: scripts/acceptance_record.py `check` her 'geçti' satırının kanıtını TEK run_record koşusunun artefaktı olarak ister
(CT-FIX-05 F3). Testler (e2-run), sabotajlar (SABOTAJ.md, elle) ve E3 (e3-run-3) bugüne dek ayrı koşulardı; bu betik
hepsini aynı koşuda ölçer ve her birini --out-dir altına yazar.

--out-dir         run_record'un --out dizini (aynı yol; run_record onu önceden açar). Depo-göreli, normal yol,
                  docs/records/audits/2026-11/mod0290-fu06-dl1/ altında (E3 betiğinin --out-dir kuralı); içinde yalnız
                  run_record'un stdout.txt / stderr.txt dosyaları olabilir.
--base-worktree   E3 betiğine aynen geçer (mdm_fu06_dl1_e3.py başlığı: repo DIŞINDA, taban commit'te açık worktree).

Sıra (her dotnet komutundan ve E3'ten önce yük kapısı: 1 dakikalık yük > 12 → 60 sn bekle, yeniden ölç; 15 dakikada
inmezse çıkış 4). Sabotajda kapı dosya değişmeden ÖNCE koşar; bekleme sırasında ağaç sabotajlı kalmaz.
  0  diff-scope.json        AC-7: `git diff --name-only <merge-base origin/main HEAD> HEAD` (yerel origin/main, fetch yok)
                            ve `ModuleVersion: "1.4.0"` kaynak sayımı (AC-6)
  1  tests.trx · tests.log · tests-summary.json             filtre FILTER, 28 / 28 yeşil (taban). Yeşil değilse sabotaj
                                                            ve E3 koşmaz (kırmızı tabanda sabotaj ölçümü anlamsız)
  2  sabotage-<harf>.trx · sabotage-<harf>.log · sabotage-summary.json
                            SABOTAJ.md tablosu birebir (dosya, çapa, değişiklik, beklenen kırmızı). Her biri: `git status`
                            yalnız --out-dir; çapa tam bir kez (değilse çıkış 2, dosyaya dokunulmadan); yük kapısı;
                            değiştir; test; dosyanın önceki baytlarına geri al ve baytları yeniden okuyarak karşılaştır;
                            `git status --porcelain` yalnız --out-dir altı; TRX'teki Failed test kümesi = beklenen küme ve
                            toplam 28. Derleme hatası ya da TRX'siz koşu kırmızı sayılmaz (o sabotaj tutmadı, çıkış 1).
  3  tests-final.trx · tests-final.log · tests-final-summary.json    28 / 28 yeşil
  4  e3/ (mdm_fu06_dl1_e3.py --out-dir <out-dir>/e3: e3/stdout.txt, e3/stderr.txt, E3'ün süzülmüş günlük kuyrukları)
     · e3-summary.json      çıkış ve son SONUÇ / ÖZET satırı. E3_* sırları ortamdan alt sürece aynen geçer; bu betik
                            yalnız var olduklarını ölçer, değerlerini okumaz, yazmaz. dotnet test'e E3_* geçmez.
  5  summary.json           her AC (AC-1…AC-9) için sonuç, artefakt ve test / satır. Kabul listesi `<out-dir>/summary.json`
                            dosyasını satırsız gösterebilir (dosya AC kimliğini taşır) ya da TRX'in satırını gösterir.

Çıkış kodları:
    0  KABUL KANITI YEŞİL — AC-1…AC-9'un her ölçümü tuttu
    1  KIRMIZI — ölçülen bir beklenti tutmadı (taban ya da son koşu 28 / 28 değil, sabotajda beklenen ≠ görülen ya da
       derleme hatası, E3 YEŞİL değil, AC-7 kapsam dışı yol) — adıyla yazılır
    2  DURUR — ölçülemedi: çapa tam bir kez yok, geri alma doğrulanamadı, --out-dir dışında değişiklik, dotnet ya da E3
       başlatılamadı / zaman aşımı, E3 çıkışı 2, 3 ya da 5, sinyal, beklenmeyen hata
    4  BEKLER — 1 dakikalık yük 15 dakika boyunca 12 üstünde (bu betikte ya da E3'te)
    5  kullanım ya da ortam eksik (--out-dir, E3_* değişkeni, dotnet, mongod)
Her yolda (hata, sinyal, erken çıkış) sabotaj geri alınır: try/finally + atexit + SIGTERM/SIGINT/SIGHUP. Çalışan alt
süreç önce SIGTERM alır (E3'ün Y6 temizliği koşsun), süre dolunca SIGKILL. SIGKILL ile öldürülen bu betik geri alamaz:
o zaman `git status` elle ölçülür ve `git checkout -- <dosya>` kule tarafından yapılır.

Ölçüm sınırı: betik kule tarafından koşulmadan önce yalnız `python3 -m py_compile` ile denetlendi; dotnet ve E3
çalıştırılmadı. Çapalar Expected HEAD'teki kaynak okumasıdır (SABOTAJ.md kod commit'i ca654fa79). TRX `testName` biçimi
(Ad.Alanı.Sınıf.Yöntem[(argümanlar)]) xUnit varsayılanıdır; karşılaştırma Sınıf.Yöntem düzeyindedir (theory bir kez
sayılır). AC-7'de docs/ ve execution/ altı (kanıt, kayıt, onay ve betik dosyaları) kod kapsamı dışında sayılır; kod
yolları paket §5 listesine eşit olmalı — bu gevşetme bağımsız incelemede okunur. Kayıt, sabotajın kodu ölçtüğünü değil
yalnız bu koşuda bu sonuçların alındığını gösterir.
"""
from __future__ import annotations

import argparse
import atexit
import contextlib
import hashlib
import json
import os
import posixpath
import re
import shutil
import signal
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]
EVIDENCE_PREFIX = "docs/records/audits/2026-11/mod0290-fu06-dl1/"
E3_SCRIPT = "execution/domains/master-data-management/work-packs/scripts/mdm_fu06_dl1_e3.py"
TEST_PROJECT = "services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Diten.MdmService.Application.Tests.csproj"
FILTER = "FullyQualifiedName~ModuleRegistration|FullyQualifiedName~CompositionRootGuardTests"
MONGOD = "/opt/homebrew/bin/mongod"
EXPECTED_TOTAL = 28
COMPOSITION_CLASS = "CompositionRootGuardTests"
COMPOSITION_COUNT = 2           # SABOTAJ.md: "CompositionRootGuardTests'in iki testi"
LOAD_LIMIT = 12.0
DOTNET_TIMEOUT = 1800
DOTNET_GRACE = 30
E3_TIMEOUT = 3 * 3600
E3_GRACE = 600                  # SIGTERM'den sonra E3'ün Y6 temizliğine verilen süre
SECRET_VARS = ("E3_JWT_SECRET", "E3_INTERNAL_API_KEY", "E3_MDM_REGISTRATION_ID", "E3_MDM_REGISTRATION_SECRET")  # = e3 SECRET_VARS
RUN_RECORD_FILES = {"stdout.txt", "stderr.txt"}
BUILD_ERROR = re.compile(r"\b(?:error|hata)\s+(?:CS|MSB|NU|NETSDK)\d+", re.I)

PROVIDER = "services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs"
CONTRACT = "services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductMaster/ProductMasterContract.cs"
CONTROLLER = "services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/ProductMasterController.cs"
MODULE_VERSION = 'ModuleVersion: "1.4.0",'

# paket §5 (Repo Scope) — AC-7'nin kod yolları; docs/ ve execution/ kod dışı sayılır (başlıktaki sınır)
REPO_SCOPE = (
    PROVIDER,
    "services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs",
    "services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductMasterReadKeyPlatformPathTests.cs",
    TEST_PROJECT,
)
NON_CODE_PREFIXES = ("docs/", "execution/")
FORBIDDEN_PREFIXES = ("services/Diten.AuthService", "services/Diten.Platform", "frontend/", "gateway/")  # Auth, Platform, Web, Gateway

# SABOTAJ.md kısaltmaları → Sınıf.Yöntem
_PROVIDER_TESTS = "ProductItemSkuMasterManifestProviderTests"
_PATH_TESTS = "ProductMasterReadKeyPlatformPathTests"
TESTS = {
    "T1": f"{_PROVIDER_TESTS}.Product_master_read_is_a_non_ui_view_action_and_not_an_api_permission",
    "T2": f"{_PROVIDER_TESTS}.Declares_exact_unique_canonical_permission_contract_and_matches_runtime_enforcement",
    "T3": f"{_PATH_TESTS}.Production_manifest_syncs_the_read_key_with_the_module_owner_and_tenant_scope",
    "T4-b": f"{_PATH_TESTS}.Every_key_synced_under_the_module_is_in_the_entitled_read_set",
    "GP": f"{_PROVIDER_TESTS}.Global_product_declares_exact_lifecycle_actions_without_approval_or_rejection_actions",
    "ID": f"{_PROVIDER_TESTS}.Declares_exact_identity_route_and_non_baseline_flags",
}
CODE_OF = {name: code for code, name in TESTS.items()}

# Sağlayıcı:121 — satırın tamamı (girinti ve satır sonu dahil) çapadır
READ_LINE = ('                        new ModuleManifestAction("READ_PRODUCT_MASTER", "Read Product Master", ProductMasterRead, '
             '"View", 60, IsDangerous: false, IsToolbarAction: false, IsRowAction: false)\n')


@dataclass(frozen=True)
class Edit:
    path: str
    anchor: str     # dosyada tam bir kez
    old: str        # çapada tam bir kez; old == anchor ve new == "" → satır silinir
    new: str


@dataclass(frozen=True)
class Sabotage:
    letter: str
    what: str
    edits: tuple
    expected: tuple


_ALL5 = ("T1", "T2", "T3", "T4-b", "GP")
SABOTAGES = (
    Sabotage("a", "Sağlayıcı:121 — READ_PRODUCT_MASTER satırı silindi", (Edit(PROVIDER, READ_LINE, READ_LINE, ""),), _ALL5),
    Sabotage("b", "Sağlayıcı:121 — `ProductMasterRead,` → `\"mdm.product-master.reads\",`",
             (Edit(PROVIDER, READ_LINE, "ProductMasterRead,", '"mdm.product-master.reads",'),), _ALL5),
    Sabotage("b2", "ProductMasterContract.cs:19 (§5 dışı, geçici) — ReadPermission → \"mdm.product-master.sabotage\"",
             (Edit(CONTRACT, '    public const string ReadPermission = "mdm.product-master.read";\n',
                   '"mdm.product-master.read"', '"mdm.product-master.sabotage"'),), _ALL5),
    Sabotage("c", "Sağlayıcı:95 — RoutePath → \"/Platform/MasterDataManagement/GlobalProducts\"",
             (Edit(PROVIDER, '                    RoutePath: "/MasterDataManagement/GlobalProducts",\n',
                   '"/MasterDataManagement/GlobalProducts"', '"/Platform/MasterDataManagement/GlobalProducts"'),),
             ("T3", "ID")),
    Sabotage("d", "Sağlayıcı:121 + Sağlayıcı:269 — eylem satırı silindi; belge sonuna ApiPermissions (T3 yeşil kalır; "
                  "çapa `IsBaseline: false);` — belgenin son argümanı, Expected HEAD'te satır 270)",
             (Edit(PROVIDER, READ_LINE, READ_LINE, ""),
              Edit(PROVIDER, "            IsBaseline: false);\n", "IsBaseline: false);",
                   'IsBaseline: false,\n            ApiPermissions: [new ModuleManifestPermission("mdm.product-master.read", '
                   '"Read Product Master")]);')),
             ("T1", "T4-b", "GP")),
    Sabotage("e", "ProductMasterController.cs:24 (§5 dışı, geçici) — sınıf [HasPermission(...ReadPermission)] satırı silindi",
             (Edit(CONTROLLER, "[HasPermission(ProductMasterContract.ReadPermission)]\npublic sealed class ProductMasterController",
                   "[HasPermission(ProductMasterContract.ReadPermission)]\n", ""),), ("T2",)),
    Sabotage("f", "Sağlayıcı:121 — `\"View\", 60` → `\"Operator\", 60`",
             (Edit(PROVIDER, READ_LINE, '"View", 60', '"Operator", 60'),), ("T1",)),
    Sabotage("g", "Sağlayıcı:121 — `IsRowAction: false)` → `IsRowAction: true)`",
             (Edit(PROVIDER, READ_LINE, "IsRowAction: false)", "IsRowAction: true)"),), ("T1",)),
    Sabotage("h", "Sağlayıcı:121 — `IsToolbarAction: false` → `IsToolbarAction: true`",
             (Edit(PROVIDER, READ_LINE, "IsToolbarAction: false", "IsToolbarAction: true"),), ("T1",)),
    Sabotage("i", "Sağlayıcı:121 — `IsRowAction: false)` → `IsRowAction: false, PermissionOwnerModuleCode: \"brand-product-master\")`",
             (Edit(PROVIDER, READ_LINE, "IsRowAction: false)",
                   'IsRowAction: false, PermissionOwnerModuleCode: "brand-product-master")'),), ("T1", "T3", "T4-b")),
    Sabotage("j", "Sağlayıcı:121 — `IsDangerous: false` → `IsDangerous: true`",
             (Edit(PROVIDER, READ_LINE, "IsDangerous: false", "IsDangerous: true"),), ("T1",)),
    Sabotage("k", "Sağlayıcı:121 — `\"Read Product Master\"` → `\"Read Product Master Data\"`",
             (Edit(PROVIDER, READ_LINE, '"Read Product Master"', '"Read Product Master Data"'),), ("T1",)),
)


class Stop(Exception):
    """A planned stop with an exit code and a verdict line."""

    def __init__(self, code: int, verdict: str):
        super().__init__(verdict)
        self.code = code
        self.verdict = verdict


# ── state ──────────────────────────────────────────────────────────────────────────────────────────────────────────
OUT = ""                        # repo-relative --out-dir
OUT_ABS = ROOT
STATE: dict = {"signal": None, "loads": []}
PENDING: dict = {}              # path → bytes before the sabotage (geri alma bekleyen)
CHILD: list = [None]            # the running child process (dotnet / E3)


def emit(line: str) -> None:
    print(line, flush=True)


def rel(name: str) -> str:
    return f"{OUT}/{name}"


def write_json(name: str, obj) -> str:
    path = OUT_ABS / name
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    return rel(name)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


# ── small tools ───────────────────────────────────────────────────────────────────────────────────────────────────
def run(argv: list, timeout: int = 120) -> tuple:
    done = subprocess.run(argv, cwd=ROOT, capture_output=True, text=True, timeout=timeout,
                          env=dict(os.environ, GIT_OPTIONAL_LOCKS="0"))
    return done.returncode, (done.stdout or ""), (done.stderr or "")


def git(*args: str) -> str:
    code, out, _ = run(["git", "-C", str(ROOT), *args])
    if code != 0:
        raise Stop(2, f"DURUR — git {' '.join(args[:2])} çıkış {code}")
    return out


def load_1min() -> float:
    code, out, _ = run(["uptime"], timeout=10)
    m = re.search(r"load averages?:\s*([0-9]+[.,][0-9]+)", out)
    if code == 0 and m:
        return float(m.group(1).replace(",", "."))
    return os.getloadavg()[0]


def load_gate(where: str) -> float:
    # Card / prompt rule: above the limit, wait 60 s and measure again; stop only if it stays above for 15 minutes.
    for attempt in range(16):
        load = load_1min()
        emit(f"yük ({where}): 1 dakikalık {load:.2f} (eşik {LOAD_LIMIT:.0f})")
        if load <= LOAD_LIMIT:
            STATE["loads"].append({"adım": where, "yük_1dk": round(load, 2)})
            return load
        if attempt < 15:
            time.sleep(60)
    raise Stop(4, f"BEKLER — 1 dakikalık yük 15 dakika boyunca {LOAD_LIMIT:.0f} üstünde ({where})")


def status_paths() -> list:
    """`git status --porcelain=v1 -z --untracked-files=all` paths (run_record.status_paths ile aynı okuma)."""
    items, paths, i = git("status", "--porcelain=v1", "-z", "--untracked-files=all").split("\0"), [], 0
    while i < len(items):
        item = items[i]
        if len(item) > 3:
            paths.append(item[3:])
            if item[0] in "RC":
                i += 1
                if i < len(items) and items[i]:
                    paths.append(items[i])
        i += 1
    return paths


def outside_out() -> list:
    return [p for p in status_paths() if not (p == OUT or p.startswith(OUT + "/"))]


def stop_child(grace: int) -> None:
    proc = CHILD[0]
    if proc is None or proc.poll() is not None:
        return
    proc.terminate()
    try:
        proc.wait(timeout=grace)
    except subprocess.TimeoutExpired:
        proc.kill()
        proc.wait()
    emit(f"alt süreç durduruldu: PID {proc.pid} çıkış {proc.returncode}")


def run_child(label: str, argv: list, env: dict, stdout_path: Path, stderr_path: Path | None,
              timeout: int, grace: int) -> int:
    with open(stdout_path, "wb") as out:
        with (open(stderr_path, "wb") if stderr_path is not None else contextlib.nullcontext()) as err:
            try:
                CHILD[0] = subprocess.Popen(argv, cwd=ROOT, env=env, stdout=out,
                                            stderr=err if stderr_path is not None else subprocess.STDOUT,
                                            stdin=subprocess.DEVNULL)
            except OSError as exc:
                raise Stop(2, f"DURUR — {label}: {argv[0]} başlatılamadı ({type(exc).__name__})")
            try:
                return CHILD[0].wait(timeout=timeout)
            except subprocess.TimeoutExpired:
                stop_child(grace)
                raise Stop(2, f"DURUR — {label}: {timeout} sn içinde bitmedi")
            except BaseException:
                stop_child(grace)
                raise
            finally:
                CHILD[0] = None


# ── TRX ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
def short_name(test_name: str) -> str:
    """Ad.Alanı.Sınıf.Yöntem(argümanlar) → Sınıf.Yöntem"""
    return ".".join(test_name.split("(", 1)[0].strip().split(".")[-2:])


def parse_trx(path: Path) -> dict | None:
    if not path.is_file():
        return None
    try:
        tree = ET.parse(str(path))
    except (ET.ParseError, OSError):
        return None
    results, counters = [], {}
    for el in tree.iter():
        tag = el.tag.rsplit("}", 1)[-1]
        if tag == "UnitTestResult":
            results.append({"ad": el.get("testName", ""), "sonuç": el.get("outcome", "")})
        elif tag == "Counters" and not counters:
            counters = {k: el.get(k) for k in ("total", "executed", "passed", "failed")}
    results.sort(key=lambda r: r["ad"])
    return {"tests": results, "counters": counters}


def outcome_of(results: list, name: str) -> str:
    """Passed only when every result of that Sınıf.Yöntem passed; 'yok' when the test did not run."""
    outs = [r["sonuç"] for r in results if short_name(r["ad"]) == name]
    if not outs:
        return "yok"
    return "Passed" if all(o == "Passed" for o in outs) else "Failed"


def dotnet_test(label: str, trx_name: str) -> dict:
    trx = OUT_ABS / trx_name
    log = OUT_ABS / (trx_name[:-len(".trx")] + ".log")
    if trx.exists() or log.exists():
        raise Stop(2, f"DURUR — {rel(trx_name)} zaten var; her koşu yeni dosyaya yazar")
    argv = ["dotnet", "test", TEST_PROJECT, "--filter", FILTER, "--logger", f"trx;LogFileName={trx}"]
    env = {k: v for k, v in os.environ.items() if not k.startswith("E3_")}
    env["DITEN_TEST_MONGOD"] = MONGOD
    emit(f"dotnet test ({label}) → {rel(trx_name)}")
    started = time.time()
    code = run_child(f"dotnet test ({label})", argv, env, log, None, DOTNET_TIMEOUT, DOTNET_GRACE)
    seconds = round(time.time() - started, 1)
    parsed = parse_trx(trx)
    text = log.read_text(encoding="utf-8", errors="replace") if log.exists() else ""
    results = parsed["tests"] if parsed else []
    failed = sorted({short_name(r["ad"]) for r in results if r["sonuç"] == "Failed"})
    summary = {
        "komut": ["DITEN_TEST_MONGOD=" + MONGOD] + argv,
        "dotnet_çıkış": code,
        "süre_sn": seconds,
        "trx": rel(trx_name) if trx.exists() else None,
        "günlük": rel(log.name),
        "trx_okundu": parsed is not None,
        "derleme_hatası": bool(BUILD_ERROR.search(text)),
        "toplam": len(results),
        "geçen": sum(1 for r in results if r["sonuç"] == "Passed"),
        "kalan": failed,
        "trx_sayaçları": parsed["counters"] if parsed else {},
        "adlandırılmış": {c: {"test": n, "sonuç": outcome_of(results, n)} for c, n in TESTS.items()},
        "testler": results,
    }
    comp = [r for r in results if short_name(r["ad"]).split(".")[0] == COMPOSITION_CLASS]
    summary["bileşim_bekçisi"] = {"sınıf": COMPOSITION_CLASS, "beklenen_sayı": COMPOSITION_COUNT, "sayı": len(comp),
                                  "hepsi_geçti": bool(comp) and all(r["sonuç"] == "Passed" for r in comp)}
    emit(f"  {label}: dotnet çıkış {code} · {summary['geçen']} / {summary['toplam']} geçti · kalan: "
         f"{', '.join(CODE_OF.get(n, n) for n in failed) or 'yok'}"
         + (" · DERLEME HATASI" if summary["derleme_hatası"] else "") + ("" if parsed else " · TRX yok"))
    return summary


def green(summary: dict | None) -> bool:
    return bool(summary and summary["dotnet_çıkış"] == 0 and summary["trx_okundu"]
                and summary["toplam"] == EXPECTED_TOTAL and summary["geçen"] == EXPECTED_TOTAL and not summary["kalan"]
                and all(v["sonuç"] == "Passed" for v in summary["adlandırılmış"].values())
                and summary["bileşim_bekçisi"]["sayı"] == COMPOSITION_COUNT and summary["bileşim_bekçisi"]["hepsi_geçti"])


def test_step(key: str, label: str, trx_name: str, summary_name: str) -> dict:
    load = load_gate(label)
    summary = dotnet_test(label, trx_name)
    summary["yük_1dk"] = round(load, 2)
    summary["yeşil"] = green(summary)
    STATE[key] = summary
    write_json(summary_name, summary)
    emit(f"  {label}: {'YEŞİL' if summary['yeşil'] else 'KIRMIZI'} ({EXPECTED_TOTAL} / {EXPECTED_TOTAL} beklenir)")
    return summary


# ── sabotage ──────────────────────────────────────────────────────────────────────────────────────────────────────
def plan_edits(sab: Sabotage) -> tuple:
    """Every anchor is checked before any byte is written: a wrong anchor stops with nothing changed."""
    originals, texts, notes = {}, {}, []
    for e in sab.edits:
        if e.path not in texts:
            raw = (ROOT / e.path).read_bytes()
            try:
                texts[e.path] = raw.decode("utf-8")
            except UnicodeDecodeError:
                raise Stop(2, f"DURUR — sabotaj {sab.letter}: {e.path} UTF-8 değil")
            originals[e.path] = raw
        original_text = originals[e.path].decode("utf-8")
        current = texts[e.path]
        for where, text in (("dosyada", original_text), ("önceki değişiklikten sonra", current)):
            n = text.count(e.anchor)
            if n != 1:
                raise Stop(2, f"DURUR — sabotaj {sab.letter}: çapa {e.path} {where} {n} kez var, tam 1 olmalı")
        if e.anchor.count(e.old) != 1:
            raise Stop(2, f"DURUR — sabotaj {sab.letter}: değişen metin çapada {e.anchor.count(e.old)} kez var, tam 1 olmalı")
        line = original_text.count("\n", 0, original_text.index(e.anchor)) + 1
        texts[e.path] = current.replace(e.anchor, e.anchor.replace(e.old, e.new, 1), 1)
        notes.append({"dosya": e.path, "çapa_satırı": line, "çapa": e.anchor.rstrip("\n"),
                      "değişiklik": "satır silindi" if (e.old == e.anchor and not e.new) else {"eski": e.old, "yeni": e.new}})
    modified = {p: t.encode("utf-8") for p, t in texts.items()}
    for p in modified:
        if modified[p] == originals[p]:
            raise Stop(2, f"DURUR — sabotaj {sab.letter}: {p} değişmedi")
    return originals, modified, notes


def restore_all() -> list:
    """Write every pending file back to its bytes before the sabotage and read them back. Idempotent."""
    problems = []
    for path, original in list(PENDING.items()):
        try:
            (ROOT / path).write_bytes(original)
            ok = (ROOT / path).read_bytes() == original
        except OSError:
            ok = False
        if ok:
            del PENDING[path]
            emit(f"  geri alındı: {path} (sha256 {sha256(original)[:12]}, bayt eşit)")
        else:
            problems.append(path)
            emit(f"UYARI: geri alınamadı: {path} — kule elle `git checkout -- {path}` yapar")
    return problems


def run_sabotage(sab: Sabotage) -> dict:
    emit(f"== sabotaj {sab.letter}: {sab.what}")
    stray = outside_out()
    if stray:
        raise Stop(2, f"DURUR — sabotaj {sab.letter} öncesi --out-dir dışında değişiklik: {stray[0][:80]}")
    originals, modified, notes = plan_edits(sab)
    load = load_gate(f"sabotaj {sab.letter}")
    entry = {"harf": sab.letter, "tanım": sab.what, "değişiklikler": notes,
             "dosyalar": {p: {"önce_sha256": sha256(originals[p]), "sabotajlı_sha256": sha256(modified[p])} for p in modified},
             "yük_1dk": round(load, 2)}
    try:
        for path, data in modified.items():
            PENDING[path] = originals[path]
            (ROOT / path).write_bytes(data)
        result = dotnet_test(f"sabotaj {sab.letter}", f"sabotage-{sab.letter}.trx")
    finally:
        left = restore_all()
    if left:
        raise Stop(2, f"DURUR — sabotaj {sab.letter}: geri alma doğrulanamadı ({', '.join(left)})")
    stray = outside_out()
    entry["geri_alma"] = {"bayt_eşit": True, "git_status_out_dir_dışı": stray}
    if stray:
        raise Stop(2, f"DURUR — sabotaj {sab.letter} geri alındıktan sonra --out-dir dışında değişiklik: {stray[0][:80]}")
    expected = sorted(TESTS[c] for c in sab.expected)
    seen = result["kalan"]
    red = result["trx_okundu"] and result["toplam"] > 0 and not result["derleme_hatası"]
    equal = bool(red and seen == expected and result["toplam"] == EXPECTED_TOTAL)
    entry.update({
        "trx": result["trx"], "günlük": result["günlük"], "dotnet_çıkış": result["dotnet_çıkış"],
        "derleme_hatası": result["derleme_hatası"], "trx_okundu": result["trx_okundu"],
        "geçen": result["geçen"], "toplam": result["toplam"],
        "beklenen": list(sab.expected), "beklenen_testler": expected,
        "görülen": [CODE_OF.get(n, n) for n in seen], "görülen_testler": seen,
        "beklenmeyen": sorted(set(seen) - set(expected)), "eksik": sorted(set(expected) - set(seen)),
        "eşit": equal,
    })
    if not red:
        entry["not"] = "derleme hatası ya da TRX'siz koşu — kırmızı sayılmaz"
    emit(f"  sabotaj {sab.letter}: beklenen {', '.join(sab.expected)} · görülen {', '.join(entry['görülen']) or 'yok'} · "
         f"{result['geçen']} / {result['toplam']} · {'EŞİT' if equal else 'EŞİT DEĞİL'}")
    return entry


def sabotage_step() -> None:
    entries = []
    STATE["sabotage"] = entries
    try:
        for sab in SABOTAGES:
            entries.append(run_sabotage(sab))
    finally:
        write_json("sabotage-summary.json", {
            "kaynak": f"{EVIDENCE_PREFIX}SABOTAJ.md (tablo a…k, birebir)",
            "filtre": FILTER, "beklenen_toplam": EXPECTED_TOTAL,
            "ölçülen": len(entries), "tanımlı": len(SABOTAGES),
            "hepsi_eşit": len(entries) == len(SABOTAGES) and all(e["eşit"] for e in entries),
            "sabotajlar": entries,
        })


# ── AC-6 / AC-7 source and scope ───────────────────────────────────────────────────────────────────────────────────
def scope_step() -> None:
    head = git("rev-parse", "HEAD").strip()
    code, out, _ = run(["git", "-C", str(ROOT), "merge-base", "origin/main", "HEAD"])
    base = out.strip() if code == 0 else ""
    names = [p for p in git("diff", "--name-only", base, "HEAD").splitlines() if p] if base else []
    code_paths = [p for p in names if not p.startswith(NON_CODE_PREFIXES)]
    outside = [p for p in code_paths if p not in REPO_SCOPE]
    forbidden = [p for p in names if p.startswith(FORBIDDEN_PREFIXES)]
    provider = (ROOT / PROVIDER).read_text(encoding="utf-8")
    version_count = provider.count(MODULE_VERSION)
    scope = {
        "head": head, "taban": base or None, "taban_kaynağı": "git merge-base origin/main HEAD (yerel origin/main, fetch yok)",
        "değişen_yollar": names, "kod_yolları": code_paths, "paket_5_kapsamı": list(REPO_SCOPE),
        "kod_dışı_önekler": list(NON_CODE_PREFIXES), "kapsam_dışı_kod_yolları": outside,
        "yasak_alanlar": list(FORBIDDEN_PREFIXES), "yasak_alanda_değişen": forbidden,
        "ac7_geçti": bool(base) and not outside and not forbidden,
        "module_version": {"dosya": PROVIDER, "aranan": MODULE_VERSION, "sayı": version_count},
    }
    STATE["scope"] = scope
    write_json("diff-scope.json", scope)
    emit(f"AC-7 kapsam: taban {base[:12] or 'YOK'} · {len(names)} yol · kod {len(code_paths)} · kapsam dışı kod "
         f"{len(outside)} · yasak alan {len(forbidden)} · ModuleVersion 1.4.0 sayısı {version_count}")


# ── E3 ────────────────────────────────────────────────────────────────────────────────────────────────────────────
def e3_step(base_worktree: str) -> None:
    e3_rel = rel("e3")
    e3_abs = ROOT / e3_rel
    if e3_abs.exists():
        raise Stop(2, f"DURUR — {e3_rel} zaten var")
    e3_abs.mkdir()
    load = load_gate("E3")
    argv = [sys.executable, E3_SCRIPT, "--base-worktree", base_worktree, "--out-dir", e3_rel]
    emit(f"E3 → {e3_rel}/stdout.txt")
    started = time.time()
    code = run_child("E3", argv, dict(os.environ), e3_abs / "stdout.txt", e3_abs / "stderr.txt", E3_TIMEOUT, E3_GRACE)
    lines = (e3_abs / "stdout.txt").read_text(encoding="utf-8", errors="replace").splitlines()
    results = [(n, l) for n, l in enumerate(lines, start=1) if l.startswith("SONUÇ: ")]
    summaries = [(n, l) for n, l in enumerate(lines, start=1) if l.startswith("ÖZET: ")]
    verdict = results[-1] if results else None
    summary = {
        "komut": [Path(sys.executable).name, E3_SCRIPT, "--base-worktree", base_worktree, "--out-dir", e3_rel],
        "sırlar": "E3_* ortamdan alt sürece geçti; değerler okunmadı, yazılmadı",
        "çıkış": code, "süre_sn": round(time.time() - started, 1), "yük_1dk": round(load, 2),
        "stdout": f"{e3_rel}/stdout.txt", "stderr": f"{e3_rel}/stderr.txt",
        "sonuç_satırı": {"satır": verdict[0], "metin": verdict[1]} if verdict else None,
        "özet_satırı": {"satır": summaries[-1][0], "metin": summaries[-1][1]} if summaries else None,
        "e3_dosyaları": sorted(p.name for p in e3_abs.iterdir()),
    }
    summary["yeşil"] = code == 0 and verdict is not None and verdict[1] == "SONUÇ: E3 YEŞİL"
    STATE["e3"] = summary
    write_json("e3-summary.json", summary)
    emit(f"E3: çıkış {code} · {verdict[1] if verdict else 'SONUÇ satırı yok'}")
    if code == 4:
        raise Stop(4, "BEKLER — E3 yük kapısı (çıkış 4)")
    if code in (2, 3, 5):
        raise Stop(2, f"DURUR — E3 çıkış {code}: {verdict[1] if verdict else 'SONUÇ satırı yok'}")


# ── AC summary ────────────────────────────────────────────────────────────────────────────────────────────────────
def verdict_of(measured: bool, ok: bool) -> str:
    return "geçti" if measured and ok else ("kaldı" if measured else "ölçülmedi")


def ac_test(code: str, criterion: str) -> dict:
    runs = [("tests", "tests.trx", "tests-summary.json"), ("final", "tests-final.trx", "tests-final-summary.json")]
    measured = all(STATE.get(k) for k, _, _ in runs)
    ok = measured and all(STATE[k]["adlandırılmış"][code]["sonuç"] == "Passed" and STATE[k]["yeşil"] for k, _, _ in runs)
    return {"ölçüt": criterion, "sonuç": verdict_of(measured, ok),
            "kanıt": [{"artefakt": rel(trx), "test": TESTS[code], "özet": rel(js)} for _, trx, js in runs]}


def ac_table() -> dict:
    acs = {
        "AC-1": ac_test("T1", "T1 yeşil: anahtar GLOBAL_PRODUCTS üzerinde arayüzsüz View eylemi, ApiPermissions boş"),
        "AC-2": ac_test("T2", "T2 yeşil: sınıf düzeyi öznitelik okunur, ProductMasterController listede, beyan 52 → 53"),
        "AC-3": ac_test("T3", "T3 yeşil: üretim işleyicisi üretim manifestiyle; Auth çağrısı (anahtar, modül sahibi, Tenant)"),
        "AC-4": ac_test("T4-b", "T4-b yeşil: modül sahibiyle eşitlenen küme plan okuma listesine eşit"),
    }
    sab = STATE.get("sabotage")
    sab_ok = bool(sab) and len(sab) == len(SABOTAGES) and all(e["eşit"] and not e["geri_alma"]["git_status_out_dir_dışı"]
                                                             for e in sab)
    acs["AC-5"] = {"ölçüt": "§18 sabotajlarının her biri adı geçen testte kırmızı; çapa, kırmızı test adı, geri alma sonrası "
                            "git status yalnız --out-dir",
                   "sonuç": verdict_of(sab is not None and len(sab) == len(SABOTAGES), sab_ok),
                   "kanıt": [{"artefakt": rel("sabotage-summary.json"), "satır": "sabotajlar[*].eşit"}]
                            + [{"artefakt": rel(f"sabotage-{s.letter}.trx"), "test": "outcome=Failed"} for s in SABOTAGES]}
    scope = STATE.get("scope")
    ac6 = ac_test("ID", "ModuleVersion 1.4.0; Declares_exact_identity_route_and_non_baseline_flags yeşil, sayfa sayısı 8")
    if scope is not None and scope["module_version"]["sayı"] != 1 and ac6["sonuç"] == "geçti":
        ac6["sonuç"] = "kaldı"
    ac6["kanıt"].append({"artefakt": rel("diff-scope.json"), "satır": "module_version.sayı == 1"})
    acs["AC-6"] = ac6
    acs["AC-7"] = {"ölçüt": "git diff kod yolları yalnız paket §5; Auth, Platform, Web, Gateway boş "
                            "(docs/ ve execution/ kod dışı — sınır)",
                   "sonuç": verdict_of(scope is not None and bool(scope["taban"]), bool(scope and scope["ac7_geçti"])),
                   "kanıt": [{"artefakt": rel("diff-scope.json"), "satır": "kapsam_dışı_kod_yolları, yasak_alanda_değişen"}]}
    t = STATE.get("tests")
    measured8 = t is not None
    ok8 = bool(t and t["bileşim_bekçisi"]["sayı"] == COMPOSITION_COUNT and t["bileşim_bekçisi"]["hepsi_geçti"]
               and t["adlandırılmış"]["T3"]["sonuç"] == "Passed")
    acs["AC-8"] = {"ölçüt": "CompositionRootGuardTests yeşil, T3 ile aynı süreçte (aynı dotnet test koşusu)",
                   "sonuç": verdict_of(measured8, ok8),
                   "kanıt": [{"artefakt": rel("tests.trx"), "test": f"{COMPOSITION_CLASS}.* ({COMPOSITION_COUNT}) + {TESTS['T3']}"},
                             {"artefakt": rel("tests-summary.json"), "satır": "bileşim_bekçisi"}]}
    e3 = STATE.get("e3")
    acs["AC-9"] = {"ölçüt": "§18 E3 yükseltme yolu (Y2–Y5), tek run_record koşusunun süzülmüş çıktısında; Y6 temizlik",
                   "sonuç": verdict_of(e3 is not None, bool(e3 and e3["yeşil"])),
                   "kanıt": [{"artefakt": rel("e3/stdout.txt"),
                              "satır": e3["sonuç_satırı"]["satır"] if e3 and e3["sonuç_satırı"] else None},
                             {"artefakt": rel("e3-summary.json"), "satır": "yeşil"}]}
    return acs


# ── main ──────────────────────────────────────────────────────────────────────────────────────────────────────────
def usage_problem(out: str) -> str | None:
    if (not out or out.startswith("/") or "\\" in out or posixpath.normpath(out) != out or ".." in out.split("/")
            or not out.startswith(EVIDENCE_PREFIX) or out.rstrip("/") == EVIDENCE_PREFIX.rstrip("/")):
        return f"--out-dir {EVIDENCE_PREFIX} altında normal, depo-göreli bir koşu dizini olmalı (run_record --out)"
    parts = out.split("/")
    for n in range(1, len(parts) + 1):
        if ROOT.joinpath(*parts[:n]).is_symlink():
            return f"--out-dir yolunda sembolik bağ: {'/'.join(parts[:n])}"
    if not (ROOT / out).is_dir():
        return "--out-dir var olan bir dizin olmalı (run_record onu açar)"
    extra = sorted(p.name for p in (ROOT / out).iterdir() if p.name not in RUN_RECORD_FILES)
    if extra:
        return f"--out-dir yeni değil (içinde {extra[0]}) — her koşu yeni bir dizine yazar"
    missing = [v for v in SECRET_VARS if not os.environ.get(v)]
    if missing:
        return "eksik ortam değişkeni: " + ", ".join(missing)
    if shutil.which("dotnet") is None:
        return "dotnet bulunamadı"
    if not os.access(MONGOD, os.X_OK):
        return f"{MONGOD} yok ya da çalıştırılamıyor"
    if not (ROOT / E3_SCRIPT).is_file():
        return f"{E3_SCRIPT} yok"
    return None


def on_signal(signum, _frame):
    if STATE["signal"] is not None:
        return
    STATE["signal"] = signum
    raise Stop(2, f"DURUR — sinyal {signum}")


def main() -> int:
    global OUT, OUT_ABS
    ap = argparse.ArgumentParser(prog="mdm_fu06_dl1_accept.py", description="MDM-FU06-DL1 tek koşuluk kabul kanıtı (AC-1…AC-9)")
    ap.add_argument("--out-dir", required=True)
    ap.add_argument("--base-worktree", required=True)
    args = ap.parse_args()

    problem = usage_problem(args.out_dir)
    if problem:
        emit("SONUÇ: kullanım — " + problem)
        return 5
    OUT = args.out_dir
    OUT_ABS = (ROOT / OUT).resolve()

    atexit.register(restore_all)
    for sig in (signal.SIGTERM, signal.SIGINT, signal.SIGHUP):
        signal.signal(sig, on_signal)

    code, verdict = 0, ""
    try:
        emit("MDM-FU06-DL1 kabul kanıtı · betik " + str(Path(__file__).resolve().relative_to(ROOT))
             + " · repo HEAD " + git("rev-parse", "HEAD").strip() + " · --out-dir " + OUT)
        stray = outside_out()
        if stray:
            raise Stop(2, f"DURUR — başlangıçta --out-dir dışında değişiklik ({len(stray)} yol, ilk: {stray[0][:80]})")
        scope_step()
        emit("== testler (taban)")
        if not test_step("tests", "taban", "tests.trx", "tests-summary.json")["yeşil"]:
            raise Stop(1, f"KIRMIZI — taban koşusu {EXPECTED_TOTAL} / {EXPECTED_TOTAL} yeşil değil; sabotaj, son koşu ve E3 koşulmadı")
        sabotage_step()
        emit("== testler (son)")
        test_step("final", "son", "tests-final.trx", "tests-final-summary.json")
        emit("== E3")
        e3_step(args.base_worktree)
    except Stop as stop:
        code, verdict = stop.code, stop.verdict
    except Exception as exc:  # noqa: BLE001 — any unexpected failure is a stop, never a pass
        code, verdict = 2, f"DURUR — beklenmeyen hata: {type(exc).__name__}"
    finally:
        for sig in (signal.SIGTERM, signal.SIGINT, signal.SIGHUP):
            signal.signal(sig, signal.SIG_IGN)       # geri alma bir sinyalle yarıda kalmaz
        left = restore_all()
    if left:
        code, verdict = 2, (verdict + " · " if verdict else "") + "DURUR — geri alınamadı: " + ", ".join(left)
    acs = ac_table()
    failed = [ac for ac, v in acs.items() if v["sonuç"] != "geçti"]
    if code == 0 and failed:
        code, verdict = 1, "KIRMIZI — " + ", ".join(f"{ac} {acs[ac]['sonuç']}" for ac in failed)
    verdict = verdict or "KABUL KANITI YEŞİL — AC-1…AC-9"
    write_json("summary.json", {
        "betik": Path(__file__).resolve().relative_to(ROOT).as_posix(),
        "out_dir": OUT, "çıkış": code, "sonuç": verdict,
        "yük": STATE["loads"],
        "adımlar": {
            "kapsam": rel("diff-scope.json") if STATE.get("scope") else None,
            "taban": {"özet": rel("tests-summary.json"), "yeşil": STATE["tests"]["yeşil"]} if STATE.get("tests") else None,
            "sabotaj": ({"özet": rel("sabotage-summary.json"), "ölçülen": len(STATE["sabotage"]),
                         "eşit": sum(1 for e in STATE["sabotage"] if e["eşit"])} if STATE.get("sabotage") is not None else None),
            "son": {"özet": rel("tests-final-summary.json"), "yeşil": STATE["final"]["yeşil"]} if STATE.get("final") else None,
            "e3": {"özet": rel("e3-summary.json"), "çıkış": STATE["e3"]["çıkış"], "yeşil": STATE["e3"]["yeşil"]} if STATE.get("e3") else None,
        },
        "ac": acs,
        "sınır": "Kayıt yalnız bu koşuda bu sonuçların alındığını gösterir; sabotajın ölçütü ölçtüğünü, E3 betiğinin "
                 "doğruluğunu ya da docs/ ve execution/ yollarının §18 kanıt dizini olduğunu kanıtlamaz (bağımsız inceleme).",
    })
    emit("ÖZET: " + ", ".join(f"{ac}={'GEÇTİ' if v['sonuç'] == 'geçti' else v['sonuç'].upper()}" for ac, v in acs.items()))
    emit("SONUÇ: " + verdict)
    return code


if __name__ == "__main__":
    sys.exit(main())
