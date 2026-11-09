#!/usr/bin/env python3
"""
MDM-0220-DL1 — tek koşuluk kabul kanıtı (MOD-0220 §22 AC-T1…AC-T5; talimat MDM-0220-DL1-P v1.1, kart S-83, kule betiği).

Kule yazar; işçi DEĞİŞTİRMEZ, KOPYALAMAZ, yerine başka betik koşmaz. Tek koşu, run_record altında, temiz ağaçta (aynı
dizin iki kez verilir):

    DITEN_TEST_MONGOD=/opt/homebrew/bin/mongod python3 scripts/run_record.py run \
        --out docs/records/audits/2026-11/mdm-0220-dl1/accept-run-1 -- \
        python3 execution/domains/master-data-management/work-packs/scripts/mdm_0220_dl1_accept.py \
        --out-dir docs/records/audits/2026-11/mdm-0220-dl1/accept-run-1

Neden: scripts/acceptance_record.py `check` her 'geçti' satırının kanıtını TEK run_record koşusunun artefaktı olarak ister
(CT-FIX-05 F3). Bu betik testleri, sabotajları, Api derlemesini, AC-T5 taramasını ve AC-T4 regresyon dumanını aynı koşuda
ölçer ve her birini --out-dir altına yazar (emsal: mdm_fu06_dl1_accept.py).

--out-dir   run_record'un --out dizini (aynı yol; run_record onu önceden açar). Depo-göreli, normal yol,
            docs/records/audits/2026-11/mdm-0220-dl1/ altında; içinde yalnız run_record'un stdout.txt / stderr.txt
            dosyaları olabilir.

Sıra (her dotnet komutundan önce yük kapısı: 1 dakikalık yük > 12 → 60 sn bekle, yeniden ölç; 15 dakikada inmezse
çıkış 4). Sabotajda kapı dosya değişmeden ÖNCE koşar; iki projeli sabotajda dosya her proje için ayrı değiştirilir ve
geri alınır, bekleme sırasında ağaç sabotajlı kalmaz.
  0  diff-scope.json · mongod-before.txt
        Kapsam farkı taban..HEAD. Taban = bu betiği son değiştiren commit'ten sonra LegalEntityModels.cs'e dokunan EN ESKİ
        commit'in ebeveyni (talimat: "kod commit'inin ebeveyni (= Expected HEAD)"); Expected HEAD betiğin kendisini
        içerdiği için betiğe yazılamaz (döngü). Kapı: değişen her yol İzinli (LegalEntityModels.cs,
        services/Diten.MdmService/tests/**, docs/records/audits/2026-11/mdm-0220-dl1/**); src altında yalnız
        LegalEntityModels.cs; tests altında silinen / değişen yok (--diff-filter=DM boş); betik tabanda var ve taban..HEAD
        arasında değişmedi. Talimat v1.1'in Expected HEAD'i (f304329e8) tabanın atası mı, bilgi olarak yazılır.
  1  tests-app.trx/.log · tests-api.trx/.log · tests-summary.json
        DOĞRULA 1–2 süzgeci (FullyQualifiedName~LegalEntity, -c Debug), DITEN_TEST_MONGOD=/opt/homebrew/bin/mongod.
        Yeşil: dotnet çıkış 0, TRX okundu, derleme hatası yok, Failed yok, NotExecuted 0, adı verilen her test Passed,
        zorunlu sınıflar koştu ve geçti (Application: LegalEntityLookupApprovalStatusTests,
        LegalEntityReferenceValidationTests, LegalEntitiesControllerPermissionTests; Api: LegalEntityLookupValidationHttpTests,
        LegalEntityLookupConsumerReadFormTests).
  1b anchors.json — S1a…S6'nın bütün çapaları dosyaya dokunmadan denetlenir; biri tam bir kez değilse
        çıkış 2 "DURUR — çapa yok". Çapalar tamamsa ve taban yeşil değilse sabotaj koşmaz: çıkış 1.
  2  sabotage-<S>-<app|api>.trx/.log · sabotage-summary.json
        Talimat 8–14 birebir (dosya, değişiklik, proje(ler), beklenen kırmızı). Her (sabotaj, proje): `git status` yalnız
        --out-dir; çapa tam bir kez; yük kapısı; değiştir; test; dosyanın önceki baytlarına geri al, baytları yeniden
        okuyarak karşılaştır; `git diff --exit-code -- <dosya>` çıkış 0; `git status --porcelain` yalnız --out-dir altı.
        Tuttu: TRX okundu, derleme hatası yok, toplam = o projenin taban toplamı, adı verilen testler EN AZ Failed
        kümesinde (talimat: "en az"; fazladan kırmızı yazılır, kaldırmaz); S1a'da theory satırları (DRAFT, SUBMITTED,
        REJECTED) satır düzeyinde Failed. Derleme hatası ya da TRX'siz koşu kırmızı sayılmaz (o sabotaj tutmadı).
        S2, S5, S6 yalnız Api'de koşar (Application.Tests'in derlenmemesi beklenir; talimat).
  3  tests-final-app.trx/.log · tests-final-api.trx/.log · tests-final-summary.json — 1 ile aynı süzgeç; yeşil ve
        toplamlar tabanla aynı. known-failures-app.txt · known-failures-api.txt — `python3 scripts/known_failures.py check
        <son TRX> --suite mdm-app|mdm-api` (çıkış 0 beklenir).
  4  build-api.log · ac-t5-grep.txt · ac-t5-summary.json — AC-T5: `dotnet build` MdmService Api (-c Debug) çıkış 0 ve
        derleme hatası yok; `git grep -n "LegalEntityLookupDto" -- services/ frontend/ tests/` çıktısında tek yapım yeri
        LegalEntityModels.cs içindeki ToLookupDto (yapım: `new LegalEntityLookupDto`, aynı satırda hedef türlü `new(`, ya
        da DTO döndüren yöntem bildiriminden sonraki iki satırda `new(`).
  5  smoke-<ad>.trx/.log · ac-t4-smoke-summary.json — AC-T4 regresyon dumanı: talimat DOĞRULA 5'teki dört tüketici
        komutu, test dosyası değişmeden. "regresyon dumanı; uyumluluk kanıtı değildir" — kabul şartı DEĞİL, bilgi;
        kırmızısı çıkış kodunu değiştirmez (işçi talimatın durma koşuluna göre raporlar).
  6  mongod-after.txt · summary.json — AC başına sonuç, artefakt ve test / satır; KAPSAM kapısı; duman bilgisi.

Çıkış kodları:
    0  KABUL KANITI YEŞİL — AC-T1…AC-T5 geçti ve KAPSAM kapısı tuttu
    1  KIRMIZI — ölçülen bir beklenti tutmadı (taban / son koşu yeşil değil, bir sabotaj tutmadı, derleme, AC-T5
       taraması, known_failures, KAPSAM) — adıyla yazılır
    2  DURUR — ölçülemedi: çapa yok (tam bir kez değil), geri alma doğrulanamadı, --out-dir dışında değişiklik, dotnet
       başlatılamadı / zaman aşımı, sinyal, beklenmeyen hata
    4  BEKLER — 1 dakikalık yük 15 dakika boyunca 12 üstünde
    5  kullanım ya da ortam eksik (--out-dir, dotnet, mongod, DITEN_TEST_MONGOD farklı değerde)
Her yolda (hata, sinyal, erken çıkış) sabotaj geri alınır: try/finally + atexit + SIGTERM/SIGINT/SIGHUP. Çalışan alt
süreç önce SIGTERM alır, süre dolunca SIGKILL. SIGKILL ile öldürülen bu betik geri alamaz: o zaman `git status` elle
ölçülür ve `git checkout -- <dosya>` kule tarafından yapılır.

KIRMIZI TABAN (beklenen): betik kod yazılmadan önce yazıldı. Bugünkü ağaçta (talimat Expected HEAD, kod ve üç yeni test
dosyası yok) koşulursa: KAPSAM kapısı tutmaz (tabandan sonra LegalEntityModels.cs commit'i yok), taban testleri yeşil
değildir (yeni test sınıfları "yok"), çapa ön denetimi S2…S6'da LegalEntityModels.cs içinde `string ApprovalStatus);` /
`e.ApprovalStatus.ToString().ToUpperInvariant());` çapasını bulamaz ve betik "DURUR — çapa yok" ile ÇIKIŞ 2 verir;
sabotaj, son koşu, derleme ve duman koşmaz. Kırmızı tabanın beklenen sonucu budur.

Ölçüm sınırı: betik kule tarafından yalnız `python3 -m py_compile` ile denetlendi; dotnet ve betik koşulmadı. Çapalar
talimatın tarif ettiği değişikliğin (üye son konumda, `e.ApprovalStatus.ToString().ToUpperInvariant()` eşlemesi
`e.IsReferenceable,` satırından hemen sonra) mevcut dosyanın girintisiyle (üye 4, eşleme argümanı 12 boşluk) yazılmış
hâlidir; işçi başka biçimde yazarsa çapa tutmaz ve betik çıkış 2 verir (kule çapayı düzeltir, betik ve sha256 yenilenir).
TRX `testName` biçimi (Ad.Alanı.Sınıf.Yöntem[(argümanlar)]) xUnit varsayılanıdır; karşılaştırma Sınıf.Yöntem düzeyindedir,
S1a satır denetimi argüman metninde büyük/küçük harf duyarsız sözcük aramasıdır. Süzgeç `~LegalEntity`
LegalEntitiesControllerPermissionTests'ten yalnız adında "legalentity" geçen yöntemi seçer (VSTest `~` büyük/küçük harf
duyarsız; "LegalEntities" "LegalEntity" içermez); betik o sınıfın koştuğunu ve geçtiğini ölçer, kaç yöntemin koştuğunu
yazar. Kayıt, sabotajın kodu ölçtüğünü değil yalnız bu koşuda bu sonuçların alındığını gösterir (bağımsız inceleme).
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
SCRIPT = "execution/domains/master-data-management/work-packs/scripts/mdm_0220_dl1_accept.py"
EVIDENCE_PREFIX = "docs/records/audits/2026-11/mdm-0220-dl1/"
PROMPT_EXPECTED_HEAD = "f304329e88600a6d3f9d7c486a6c740497b8b52d"   # talimat v1.1 Expected HEAD (yalnız bilgi)
MONGOD = "/opt/homebrew/bin/mongod"
FILTER = "FullyQualifiedName~LegalEntity"
LOAD_LIMIT = 12.0
DOTNET_TIMEOUT = 1800
DOTNET_GRACE = 30
RUN_RECORD_FILES = {"stdout.txt", "stderr.txt"}
BUILD_ERROR = re.compile(r"\b(?:error|hata)\s+(?:CS|MSB|NU|NETSDK)\d+", re.I)

MDM = "services/Diten.MdmService"
SRC_PREFIX = f"{MDM}/src/"
TESTS_PREFIX = f"{MDM}/tests/"
MODELS = f"{MDM}/src/Diten.MdmService.Application/Features/LegalEntity/LegalEntityModels.cs"
HANDLER = f"{MDM}/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/QueryHandlers/ValidateLegalEntityReferenceHandler.cs"
API_SRC = f"{MDM}/src/Diten.MdmService.Api/Diten.MdmService.Api.csproj"
PROJECTS = {
    "app": f"{MDM}/tests/Diten.MdmService.Application.Tests/Diten.MdmService.Application.Tests.csproj",
    "api": f"{MDM}/tests/Diten.MdmService.Api.Tests/Diten.MdmService.Api.Tests.csproj",
}
SUITES = {"app": "mdm-app", "api": "mdm-api"}
ALLOWED_FILES = (MODELS,)
ALLOWED_PREFIXES = (TESTS_PREFIX, EVIDENCE_PREFIX)

# talimat NASIL — adlar sözleşmedir
APP_NEW = "LegalEntityLookupApprovalStatusTests"
APP_EXISTING = "LegalEntityReferenceValidationTests"
APP_PERM = "LegalEntitiesControllerPermissionTests"
API_HTTP = "LegalEntityLookupValidationHttpTests"
API_READ = "LegalEntityLookupConsumerReadFormTests"
TESTS = {
    "A1": ("app", f"{APP_NEW}.Active_entity_returns_approval_status_in_upper_case"),
    "A2": ("app", f"{APP_NEW}.Non_active_entity_is_404_even_when_approved"),
    "A3": ("app", f"{APP_NEW}.Active_rejected_entity_is_200_and_referenceable"),
    "A4": ("app", f"{APP_NEW}.Contract_example_deserializes_unchanged"),
    "E1": ("app", f"{APP_EXISTING}.Active_same_tenant_legal_entity_is_referenceable"),
    "H1": ("api", f"{API_HTTP}.Lookup_validation_carries_approval_status_over_http"),
    "H2": ("api", f"{API_HTTP}.Not_found_answers_are_unchanged_for_missing_deleted_other_tenant_and_non_active_approved"),
    "H3": ("api", f"{API_HTTP}.Active_rejected_is_200_and_referenceable_over_http"),
    "H4": ("api", f"{API_HTTP}.Lookup_validation_json_has_exactly_seven_properties"),
    "H5": ("api", f"{API_HTTP}.Lookup_list_items_have_the_same_seven_properties"),
    "R1": ("api", f"{API_READ}.Lookup_validation_body_reads_in_both_consumer_forms"),
    "R2": ("api", f"{API_READ}.Lookup_list_item_reads_in_both_consumer_forms"),
}
CODE_OF = {name: code for code, (_, name) in TESTS.items()}
REQUIRED_CLASSES = {"app": (APP_NEW, APP_EXISTING, APP_PERM), "api": (API_HTTP, API_READ)}
NON_APPROVED_ROWS = ("DRAFT", "SUBMITTED", "REJECTED")

# talimat DOĞRULA 5 — AC-T4 regresyon dumanı (değişiklik yok, yalnız koş)
SMOKE = (
    ("manufacturing", "services/Diten.ManufacturingService/tests/Diten.ManufacturingService.Tests/Diten.ManufacturingService.Tests.csproj",
     "FullyQualifiedName~MdmLegalEntityReferenceValidatorTests"),
    ("supplychain", "services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj",
     "FullyQualifiedName~LegalEntityScopeValidatorTests"),
    ("planning", "services/Diten.PlanningService/tests/Diten.PlanningService.Cycles.Tests/Diten.PlanningService.Cycles.Tests.csproj",
     "FullyQualifiedName~MrpUpstreamReaderTests|FullyQualifiedName~LiveHistoryImportReferencesTests"),
    ("web", "frontend/Diten.Web.Tests/Diten.Web.Tests.csproj", "FullyQualifiedName~CompanySelectionTests"),
)
SMOKE_NOT_RUN = {
    "Platform MdmLegalEntityReferenceValidatorTests": "koşmadı — §22.6a Platform sahibinin (§22.4 AC-T4 listesinde yok)",
    "CRM (parser testi yok), Inventory, HCM, SafetyStock": "koşmadı — §22.6a sahibinin",
    "MdmService tam mdm-app / mdm-api": "yerelde koşmadı — PR CI'ında",
}


@dataclass(frozen=True)
class Edit:
    path: str
    anchor: str     # dosyada tam bir kez
    old: str        # çapada tam bir kez
    new: str


@dataclass(frozen=True)
class Sabotage:
    key: str
    what: str
    edits: tuple
    projects: tuple
    expected: tuple          # TESTS kodları — en az Failed kümesinde
    rows: tuple = ()         # ((kod, (satır sözcüğü, …)), …) — theory satırları en az Failed
    acs: tuple = ()


# Çapalar: ValidateLegalEntityReferenceHandler.cs:20 (Expected HEAD'te bugün var); LegalEntityModels.cs çapaları talimat
# NE (1)'deki değişiklikten sonraki hâldir (bugün yok — kırmızı taban).
HANDLER_LINE = "        if (entity is null || entity.OperationalStatus != LegalEntityOperationalStatus.Active)\n"
DTO_HEAD = "public sealed record LegalEntityLookupDto(\n    Guid LegalEntityId,\n    string Code,\n"
DTO_TAIL = "    bool Referenceable,\n    string ApprovalStatus);\n"
MAP_TAIL = "            e.IsReferenceable,\n            e.ApprovalStatus.ToString().ToUpperInvariant());\n"

SABOTAGES = (
    Sabotage("S1a", "ValidateLegalEntityReferenceHandler.cs:20 — koşula `|| entity.ApprovalStatus != LegalEntityApprovalStatus.Approved`",
             (Edit(HANDLER, HANDLER_LINE, "LegalEntityOperationalStatus.Active)",
                   "LegalEntityOperationalStatus.Active || entity.ApprovalStatus != LegalEntityApprovalStatus.Approved)"),),
             ("app", "api"), ("A1", "A3", "E1", "H1", "H3"),
             rows=(("A1", NON_APPROVED_ROWS), ("H1", NON_APPROVED_ROWS)), acs=("AC-T2",)),
    Sabotage("S1b", "ValidateLegalEntityReferenceHandler.cs:20 — `entity is null || (OperationalStatus != Active && "
                    "ApprovalStatus != Approved)`",
             (Edit(HANDLER, HANDLER_LINE, "entity is null || entity.OperationalStatus != LegalEntityOperationalStatus.Active",
                   "entity is null || (entity.OperationalStatus != LegalEntityOperationalStatus.Active && "
                   "entity.ApprovalStatus != LegalEntityApprovalStatus.Approved)"),),
             ("app", "api"), ("A2", "H2"), acs=("AC-T2",)),
    Sabotage("S2", "LegalEntityModels.cs — ApprovalStatus üyesi ve eşlemesi çıkarıldı (yalnız Api)",
             (Edit(MODELS, DTO_TAIL, ",\n    string ApprovalStatus);", ");"),
              Edit(MODELS, MAP_TAIL, ",\n            e.ApprovalStatus.ToString().ToUpperInvariant());", ");")),
             ("api",), ("H4", "H5", "H1", "R1", "R2"), acs=("AC-T3",)),
    Sabotage("S3", "LegalEntityModels.cs — üyeye `[property: JsonPropertyName(\"approvalState\")]`",
             (Edit(MODELS, DTO_TAIL, "    string ApprovalStatus);",
                   '    [property: System.Text.Json.Serialization.JsonPropertyName("approvalState")] string ApprovalStatus);'),),
             ("app", "api"), ("H4", "H5", "H1", "A4", "R1", "R2"), acs=("AC-T3",)),
    Sabotage("S4", "LegalEntityModels.cs — ApprovalStatus eşlemesinde ToUpperInvariant kaldırıldı",
             (Edit(MODELS, MAP_TAIL, "e.ApprovalStatus.ToString().ToUpperInvariant());", "e.ApprovalStatus.ToString());"),),
             ("app", "api"), ("A1", "H1"), acs=("AC-T1",)),
    Sabotage("S5", "LegalEntityModels.cs — `string Code` → `string EntityCode` (konumsal; eşleme aynı; yalnız Api)",
             (Edit(MODELS, DTO_HEAD, "    string Code,\n", "    string EntityCode,\n"),),
             ("api",), ("R1", "R2", "H4", "H5"), acs=("AC-T3", "AC-T4")),
    Sabotage("S6", "LegalEntityModels.cs — `bool Referenceable` → `string Referenceable`, eşleme "
                   "`e.IsReferenceable ? \"true\" : \"false\"` (yalnız Api)",
             (Edit(MODELS, DTO_TAIL, "bool Referenceable,", "string Referenceable,"),
              Edit(MODELS, MAP_TAIL, "e.IsReferenceable,", 'e.IsReferenceable ? "true" : "false",')),
             ("api",), ("R1", "R2", "H4", "H5"), acs=("AC-T3", "AC-T4")),
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
CHILD: list = [None]            # the running child process (dotnet)


def emit(line: str) -> None:
    print(line, flush=True)


def rel(name: str) -> str:
    return f"{OUT}/{name}"


def write_json(name: str, obj) -> str:
    (OUT_ABS / name).write_text(json.dumps(obj, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    return rel(name)


def write_text(name: str, text: str) -> str:
    (OUT_ABS / name).write_text(text, encoding="utf-8")
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


def git_code(*args: str) -> int:
    return run(["git", "-C", str(ROOT), *args])[0]


def load_1min() -> float:
    code, out, _ = run(["uptime"], timeout=10)
    m = re.search(r"load averages?:\s*([0-9]+[.,][0-9]+)", out)
    if code == 0 and m:
        return float(m.group(1).replace(",", "."))
    return os.getloadavg()[0]


def load_gate(where: str) -> float:
    # Talimat: eşik üstünde 60 sn bekle ve yeniden ölç; 15 dakika boyunca inmezse dur.
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


def mongod_snapshot(name: str) -> None:
    code, out, _ = run(["ps", "-axo", "pid,lstart,command"], timeout=30)
    lines = [l for l in out.splitlines() if "mongod" in l and "grep" not in l] if code == 0 else []
    write_text(name, "".join(l + "\n" for l in lines) if lines else f"(mongod süreci yok · ps çıkış {code})\n")


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


def run_child(label: str, argv: list, env: dict, log_path: Path, timeout: int, grace: int) -> int:
    with open(log_path, "wb") as out:
        try:
            CHILD[0] = subprocess.Popen(argv, cwd=ROOT, env=env, stdout=out, stderr=subprocess.STDOUT,
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


def child_env() -> dict:
    env = dict(os.environ)
    env["DITEN_TEST_MONGOD"] = MONGOD
    return env


# ── TRX ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
def short_name(test_name: str) -> str:
    """Ad.Alanı.Sınıf.Yöntem(argümanlar) → Sınıf.Yöntem"""
    return ".".join(test_name.split("(", 1)[0].strip().split(".")[-2:])


def args_of(test_name: str) -> str:
    return test_name.split("(", 1)[1] if "(" in test_name else ""


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
            counters = {k: el.get(k) for k in ("total", "executed", "passed", "failed", "notExecuted")}
    results.sort(key=lambda r: r["ad"])
    return {"tests": results, "counters": counters}


def outcome_of(results: list, name: str) -> str:
    """Passed only when every result of that Sınıf.Yöntem passed; 'yok' when the test did not run."""
    outs = [r["sonuç"] for r in results if short_name(r["ad"]) == name]
    if not outs:
        return "yok"
    return "Passed" if all(o == "Passed" for o in outs) else "Failed"


def run_dotnet(label: str, argv_tail: list, trx_name: str) -> dict:
    """dotnet test <argv_tail> --logger trx → summary (no project-specific checks)."""
    trx = OUT_ABS / trx_name
    log = OUT_ABS / (trx_name[:-len(".trx")] + ".log")
    if trx.exists() or log.exists():
        raise Stop(2, f"DURUR — {rel(trx_name)} zaten var; her koşu yeni dosyaya yazar")
    argv = ["dotnet", "test", *argv_tail, "--logger", f"trx;LogFileName={trx}"]
    shown = ["DITEN_TEST_MONGOD=" + MONGOD, "dotnet", "test", *argv_tail, "--logger", f"trx;LogFileName={rel(trx_name)}"]
    emit(f"dotnet test ({label}) → {rel(trx_name)}")
    started = time.time()
    code = run_child(f"dotnet test ({label})", argv, child_env(), log, DOTNET_TIMEOUT, DOTNET_GRACE)
    parsed = parse_trx(trx)
    text = log.read_text(encoding="utf-8", errors="replace") if log.exists() else ""
    results = parsed["tests"] if parsed else []
    failed = sorted({short_name(r["ad"]) for r in results if r["sonuç"] not in ("Passed", "NotExecuted")})
    summary = {
        "komut": shown,
        "dotnet_çıkış": code,
        "süre_sn": round(time.time() - started, 1),
        "trx": rel(trx_name) if trx.exists() else None,
        "günlük": rel(log.name),
        "trx_okundu": parsed is not None,
        "derleme_hatası": bool(BUILD_ERROR.search(text)),
        "toplam": len(results),
        "geçen": sum(1 for r in results if r["sonuç"] == "Passed"),
        "not_executed": sum(1 for r in results if r["sonuç"] == "NotExecuted"),
        "kalan": failed,
        "trx_sayaçları": parsed["counters"] if parsed else {},
        "testler": results,
    }
    emit(f"  {label}: dotnet çıkış {code} · {summary['geçen']} / {summary['toplam']} geçti · NotExecuted "
         f"{summary['not_executed']} · kalan: {', '.join(CODE_OF.get(n, n) for n in failed) or 'yok'}"
         + (" · DERLEME HATASI" if summary["derleme_hatası"] else "") + ("" if parsed else " · TRX yok"))
    return summary


def project_test(pkey: str, label: str, trx_name: str) -> dict:
    summary = run_dotnet(label, [PROJECTS[pkey], "-c", "Debug", "--filter", FILTER], trx_name)
    results = summary["testler"]
    summary["proje"] = PROJECTS[pkey]
    summary["süzgeç"] = FILTER
    summary["adlandırılmış"] = {c: {"test": n, "sonuç": outcome_of(results, n)} for c, (p, n) in TESTS.items() if p == pkey}
    classes = {}
    for cls in REQUIRED_CLASSES[pkey]:
        rows = [r for r in results if short_name(r["ad"]).split(".")[0] == cls]
        classes[cls] = {"satır": len(rows), "yöntemler": sorted({short_name(r["ad"]).split(".", 1)[1] for r in rows}),
                        "hepsi_geçti": bool(rows) and all(r["sonuç"] == "Passed" for r in rows)}
    summary["zorunlu_sınıflar"] = classes
    return summary


def green(s: dict | None, base_total: int | None = None) -> bool:
    return bool(s and s["dotnet_çıkış"] == 0 and s["trx_okundu"] and not s["derleme_hatası"] and s["toplam"] > 0
                and not s["kalan"] and s["not_executed"] == 0
                and all(v["sonuç"] == "Passed" for v in s["adlandırılmış"].values())
                and all(v["hepsi_geçti"] for v in s["zorunlu_sınıflar"].values())
                and (base_total is None or s["toplam"] == base_total))


def test_step(key: str, label: str, prefix: str) -> bool:
    out = {}
    STATE[key] = out
    base = STATE.get("tests") if key != "tests" else None
    for p in PROJECTS:
        load = load_gate(f"{label} {p}")
        s = project_test(p, f"{label} {p}", f"{prefix}-{p}.trx")
        s["yük_1dk"] = round(load, 2)
        s["yeşil"] = green(s, base[p]["toplam"] if base else None)
        out[p] = s
        emit(f"  {label} {p}: {'YEŞİL' if s['yeşil'] else 'KIRMIZI'}")
    write_json(f"{prefix}-summary.json", {"süzgeç": FILTER, "projeler": out,
                                          "yeşil": all(s["yeşil"] for s in out.values())})
    return all(s["yeşil"] for s in out.values())


# ── sabotage ──────────────────────────────────────────────────────────────────────────────────────────────────────
def plan_edits(sab: Sabotage) -> tuple:
    """Every anchor is checked before any byte is written: a missing anchor stops with nothing changed."""
    originals, texts, notes = {}, {}, []
    for e in sab.edits:
        if e.path not in texts:
            raw = (ROOT / e.path).read_bytes()
            try:
                texts[e.path] = raw.decode("utf-8")
            except UnicodeDecodeError:
                raise Stop(2, f"DURUR — sabotaj {sab.key}: {e.path} UTF-8 değil")
            originals[e.path] = raw
        original_text = originals[e.path].decode("utf-8")
        current = texts[e.path]
        for where, text in (("dosyada", original_text), ("önceki değişiklikten sonra", current)):
            n = text.count(e.anchor)
            if n != 1:
                raise Stop(2, f"DURUR — çapa yok: sabotaj {sab.key}, {e.path} {where} {n} kez "
                              f"(tam 1 olmalı): {e.anchor.strip()[:90]!r}")
        if e.anchor.count(e.old) != 1:
            raise Stop(2, f"DURUR — çapa yok: sabotaj {sab.key}, değişen metin çapada {e.anchor.count(e.old)} kez (tam 1)")
        line = original_text.count("\n", 0, original_text.index(e.anchor)) + 1
        texts[e.path] = current.replace(e.anchor, e.anchor.replace(e.old, e.new, 1), 1)
        notes.append({"dosya": e.path, "çapa_satırı": line, "çapa": e.anchor.rstrip("\n"),
                      "değişiklik": {"eski": e.old, "yeni": e.new}})
    modified = {p: t.encode("utf-8") for p, t in texts.items()}
    for p in modified:
        if modified[p] == originals[p]:
            raise Stop(2, f"DURUR — sabotaj {sab.key}: {p} değişmedi")
    return originals, modified, notes


def anchor_preflight() -> None:
    entries, problems = [], []
    for sab in SABOTAGES:
        try:
            _, modified, notes = plan_edits(sab)
            entries.append({"sabotaj": sab.key, "çapa": "tam bir kez", "değişiklikler": notes})
        except Stop as stop:
            entries.append({"sabotaj": sab.key, "çapa": "YOK", "neden": stop.verdict})
            problems.append(stop.verdict)
    write_json("anchors.json", {"ölçülen": len(entries), "tamam": not problems, "sabotajlar": entries})
    emit(f"çapa ön denetimi: {len(SABOTAGES) - len(problems)} / {len(SABOTAGES)} tamam")
    if problems:
        raise Stop(2, problems[0] + (f" (+{len(problems) - 1} sabotajda daha; anchors.json)" if len(problems) > 1 else ""))


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


def sabotage_project(sab: Sabotage, pkey: str) -> dict:
    stray = outside_out()
    if stray:
        raise Stop(2, f"DURUR — sabotaj {sab.key} {pkey} öncesi --out-dir dışında değişiklik: {stray[0][:80]}")
    originals, modified, notes = plan_edits(sab)
    load = load_gate(f"sabotaj {sab.key} {pkey}")
    entry = {"proje": PROJECTS[pkey], "değişiklikler": notes, "yük_1dk": round(load, 2),
             "dosyalar": {p: {"önce_sha256": sha256(originals[p]), "sabotajlı_sha256": sha256(modified[p])} for p in modified}}
    try:
        for path, data in modified.items():
            PENDING[path] = originals[path]
            (ROOT / path).write_bytes(data)
        result = run_dotnet(f"sabotaj {sab.key} {pkey}", [PROJECTS[pkey], "-c", "Debug", "--filter", FILTER],
                            f"sabotage-{sab.key}-{pkey}.trx")
    finally:
        left = restore_all()
    if left:
        raise Stop(2, f"DURUR — sabotaj {sab.key} {pkey}: geri alma doğrulanamadı ({', '.join(left)})")
    diffs = {p: git_code("diff", "--quiet", "--exit-code", "--", p) for p in modified}
    stray = outside_out()
    entry["geri_alma"] = {"bayt_eşit": True, "git_diff_exit_code": diffs, "git_status_out_dir_dışı": stray}
    if stray or any(diffs.values()):
        raise Stop(2, f"DURUR — sabotaj {sab.key} {pkey} geri alındıktan sonra fark var: "
                      f"{(stray or [p for p, c in diffs.items() if c])[0][:80]}")
    base_total = STATE["tests"][pkey]["toplam"]
    expected = sorted(TESTS[c][1] for c in sab.expected if TESTS[c][0] == pkey)
    seen = result["kalan"]
    valid = result["trx_okundu"] and not result["derleme_hatası"] and result["toplam"] == base_total
    missing = sorted(set(expected) - set(seen))
    missing_rows = []
    for code, tokens in sab.rows:
        if TESTS[code][0] != pkey:
            continue
        name = TESTS[code][1]
        failed_args = [args_of(r["ad"]) for r in result["testler"]
                       if short_name(r["ad"]) == name and r["sonuç"] not in ("Passed", "NotExecuted")]
        for tok in tokens:
            if not any(re.search(rf"\b{tok}\b", a, re.I) for a in failed_args):
                missing_rows.append(f"{name}[{tok}]")
    held = bool(valid and not missing and not missing_rows)
    entry.update({
        "trx": result["trx"], "günlük": result["günlük"], "dotnet_çıkış": result["dotnet_çıkış"],
        "derleme_hatası": result["derleme_hatası"], "trx_okundu": result["trx_okundu"],
        "geçen": result["geçen"], "toplam": result["toplam"], "taban_toplamı": base_total,
        "beklenen_testler": expected, "görülen_testler": seen,
        "eksik": missing, "eksik_satırlar": missing_rows, "fazladan_kırmızı": sorted(set(seen) - set(expected)),
        "tuttu": held,
    })
    if not valid:
        entry["not"] = "derleme hatası, TRX'siz koşu ya da toplam taban toplamından farklı — kırmızı sayılmaz"
    emit(f"  sabotaj {sab.key} {pkey}: beklenen {', '.join(CODE_OF[n] for n in expected)} · görülen "
         f"{', '.join(CODE_OF.get(n, n) for n in seen) or 'yok'} · {result['geçen']} / {result['toplam']} · "
         f"{'TUTTU' if held else 'TUTMADI'}")
    return entry


def sabotage_step() -> None:
    entries = []
    STATE["sabotage"] = entries
    try:
        for sab in SABOTAGES:
            emit(f"== sabotaj {sab.key}: {sab.what}")
            entry = {"sabotaj": sab.key, "tanım": sab.what, "ac": list(sab.acs),
                     "beklenen": [TESTS[c][1] for c in sab.expected],
                     "beklenen_satırlar": {TESTS[c][1]: list(t) for c, t in sab.rows}, "projeler": {}}
            entries.append(entry)
            for p in sab.projects:
                entry["projeler"][p] = sabotage_project(sab, p)
            entry["tuttu"] = all(v["tuttu"] for v in entry["projeler"].values()) and len(entry["projeler"]) == len(sab.projects)
    finally:
        write_json("sabotage-summary.json", {
            "kaynak": "execution/domains/master-data-management/work-packs/prompts/MDM-0220-DL1-v1.1.md DOĞRULA 8–14 (S1a…S6)",
            "süzgeç": FILTER, "ölçüt": "adı verilen testler en az Failed kümesinde; toplam = taban toplamı; derleme hatası kırmızı sayılmaz",
            "ölçülen": sum(1 for e in entries if "tuttu" in e), "tanımlı": len(SABOTAGES),
            "hepsi_tuttu": len(entries) == len(SABOTAGES) and all(e.get("tuttu") for e in entries),
            "sabotajlar": entries,
        })


# ── scope ─────────────────────────────────────────────────────────────────────────────────────────────────────────
def allowed(path: str) -> bool:
    return path in ALLOWED_FILES or path.startswith(ALLOWED_PREFIXES)


def scope_step() -> None:
    head = git("rev-parse", "HEAD").strip()
    script_commit = git("log", "-1", "--format=%H", "--", SCRIPT).strip()
    code_commits = (git("log", "--reverse", "--format=%H", f"{script_commit}..HEAD", "--", MODELS).split()
                    if script_commit else [])
    base = git("rev-parse", f"{code_commits[0]}^").strip() if code_commits else ""
    names, dm, commits, script_at_base, script_same, prompt_head_ancestor = [], [], [], False, False, None
    if base:
        names = [p for p in git("diff", "--name-only", base, "HEAD").splitlines() if p]
        dm = [p for p in git("diff", "--diff-filter=DM", "--name-only", base, "HEAD", "--", TESTS_PREFIX).splitlines() if p]
        commits = [l for l in git("log", "--format=%H %s", f"{base}..HEAD").splitlines() if l]
        script_at_base = git_code("cat-file", "-e", f"{base}:{SCRIPT}") == 0
        script_same = git_code("diff", "--quiet", base, "HEAD", "--", SCRIPT) == 0
        prompt_head_ancestor = git_code("merge-base", "--is-ancestor", PROMPT_EXPECTED_HEAD, base) == 0
    outside = [p for p in names if not allowed(p)]
    src = [p for p in names if p.startswith(SRC_PREFIX)]
    ok = bool(base) and not outside and src == [MODELS] and not dm and script_at_base and script_same
    scope = {
        "head": head, "betiği_son_değiştiren_commit": script_commit or None,
        "kod_commitleri": code_commits, "taban": base or None,
        "taban_kuralı": "betiği son değiştiren commit'ten sonra LegalEntityModels.cs'e dokunan en eski commit'in ebeveyni "
                        "(talimat: kod commit'inin ebeveyni = Expected HEAD)",
        "talimat_v1_1_expected_head": PROMPT_EXPECTED_HEAD, "talimat_head_tabanın_atası": prompt_head_ancestor,
        "commitler": commits, "değişen_yollar": names,
        "izinli": {"dosyalar": list(ALLOWED_FILES), "önekler": list(ALLOWED_PREFIXES)},
        "izinli_dışı": outside, "src_değişen": src, "tests_silinen_ya_da_değişen": dm,
        "betik_tabanda_var": script_at_base, "betik_taban_head_aynı": script_same,
        "geçti": ok,
    }
    STATE["scope"] = scope
    write_json("diff-scope.json", scope)
    emit(f"kapsam: taban {base[:12] or 'YOK'} · {len(names)} yol · izinli dışı {len(outside)} · src {src or '[]'} · "
         f"tests D/M {len(dm)} · {'TUTTU' if ok else 'TUTMADI'}")


# ── known failures ─────────────────────────────────────────────────────────────────────────────────────────────────
def known_failures_step() -> None:
    out = {}
    STATE["known_failures"] = out
    for p, suite in SUITES.items():
        trx = (STATE.get("final") or {}).get(p, {}).get("trx")
        name = f"known-failures-{p}.txt"
        if not trx:
            out[p] = {"suite": suite, "çıkış": None, "çıktı": None, "not": "son TRX yok"}
            continue
        argv = [sys.executable, "scripts/known_failures.py", "check", trx, "--suite", suite]
        code, so, se = run(argv, timeout=600)
        write_text(name, so + (("\n--- stderr ---\n" + se) if se else ""))
        out[p] = {"komut": ["python3", "scripts/known_failures.py", "check", trx, "--suite", suite],
                  "suite": suite, "çıkış": code, "çıktı": rel(name)}
        emit(f"known_failures {suite}: çıkış {code}")


# ── AC-T5 ─────────────────────────────────────────────────────────────────────────────────────────────────────────
NEW_TYPED = re.compile(r"\bnew\s+LegalEntityLookupDto\b")
NEW_TARGET = re.compile(r"\bnew\s*\(")
RETURNS = re.compile(r"\bLegalEntityLookupDto\s+(\w+)\s*\(")
GREP_LINE = re.compile(r"^(.*?):(\d+):(.*)$")


def scan_constructions(lines: list) -> list:
    sites, cache = [], {}
    for raw in lines:
        m = GREP_LINE.match(raw)
        if not m:
            continue
        path, n, text = m.group(1), int(m.group(2)), m.group(3)
        kind, method = None, None
        if NEW_TYPED.search(text):
            kind = "new LegalEntityLookupDto(...)"
        elif NEW_TARGET.search(text):
            kind = "aynı satırda hedef türlü new(...)"
        else:
            r = RETURNS.search(text)
            if r:
                if path not in cache:
                    cache[path] = (ROOT / path).read_text(encoding="utf-8", errors="replace").splitlines()
                follow = cache[path][n:n + 2]
                if any(NEW_TARGET.search(f) or NEW_TYPED.search(f) for f in follow):
                    kind, method = "DTO döndüren yöntem → new(...)", r.group(1)
        if kind:
            sites.append({"dosya": path, "satır": n, "tür": kind, "yöntem": method, "metin": text.strip()})
    return sites


def ac_t5_step() -> None:
    load = load_gate("AC-T5 derleme")
    log = OUT_ABS / "build-api.log"
    if log.exists():
        raise Stop(2, f"DURUR — {rel('build-api.log')} zaten var")
    argv = ["dotnet", "build", API_SRC, "-c", "Debug"]
    emit(f"dotnet build → {rel('build-api.log')}")
    started = time.time()
    code = run_child("dotnet build", argv, child_env(), log, DOTNET_TIMEOUT, DOTNET_GRACE)
    text = log.read_text(encoding="utf-8", errors="replace")
    build = {"komut": argv, "çıkış": code, "süre_sn": round(time.time() - started, 1), "yük_1dk": round(load, 2),
             "günlük": rel("build-api.log"), "derleme_hatası": bool(BUILD_ERROR.search(text))}
    build["geçti"] = code == 0 and not build["derleme_hatası"]
    gcode, gout, _ = run(["git", "-C", str(ROOT), "grep", "-n", "LegalEntityLookupDto", "--", "services/", "frontend/", "tests/"])
    write_text("ac-t5-grep.txt", gout)
    lines = [l for l in gout.splitlines() if l]
    sites = scan_constructions(lines)
    scan_ok = (gcode == 0 and len(sites) == 1 and sites[0]["dosya"] == MODELS and sites[0]["yöntem"] == "ToLookupDto")
    summary = {"derleme": build,
               "tarama": {"komut": 'git grep -n "LegalEntityLookupDto" -- services/ frontend/ tests/', "çıkış": gcode,
                          "çıktı": rel("ac-t5-grep.txt"), "satır_sayısı": len(lines), "yapım_yerleri": sites,
                          "beklenen": f"tek yapım yeri {MODELS} ToLookupDto", "geçti": scan_ok}}
    summary["geçti"] = build["geçti"] and scan_ok
    STATE["ac_t5"] = summary
    write_json("ac-t5-summary.json", summary)
    where = ", ".join(s["dosya"].rsplit("/", 1)[-1] + ":" + str(s["satır"]) for s in sites) or "yok"
    emit(f"AC-T5: derleme çıkış {code}{' · DERLEME HATASI' if build['derleme_hatası'] else ''} · yapım yeri "
         f"{len(sites)} ({where}) · {'GEÇTİ' if summary['geçti'] else 'KALDI'}")


# ── AC-T4 regresyon dumanı ─────────────────────────────────────────────────────────────────────────────────────────
def smoke_step() -> None:
    runs = {}
    STATE["smoke"] = runs
    try:
        for name, project, flt in SMOKE:
            load = load_gate(f"duman {name}")
            s = run_dotnet(f"duman {name}", [project, "--filter", flt], f"smoke-{name}.trx")
            s["yük_1dk"] = round(load, 2)
            s["proje"], s["süzgeç"] = project, flt
            s["yeşil"] = (s["dotnet_çıkış"] == 0 and s["trx_okundu"] and not s["derleme_hatası"] and s["toplam"] > 0
                          and not s["kalan"])
            runs[name] = s
    finally:
        write_json("ac-t4-smoke-summary.json", {
            "başlık": "AC-T4 (MDM WP kısmı) — regresyon dumanı; uyumluluk kanıtı değildir",
            "not": "Bu testler kendi gövdelerini kurar, MdmService'i çağırmaz; DTO bozulsa da yeşil kalır. Kabul şartı "
                   "değildir (kabul şartı LegalEntityLookupConsumerReadFormTests ve S5 / S6). Test dosyası değişmeden koşuldu.",
            "koşular": runs, "hepsi_yeşil": len(runs) == len(SMOKE) and all(s["yeşil"] for s in runs.values()),
            "koşmayanlar": SMOKE_NOT_RUN,
        })


# ── AC summary ────────────────────────────────────────────────────────────────────────────────────────────────────
def verdict_of(measured: bool, ok: bool) -> str:
    return "geçti" if measured and ok else ("kaldı" if measured else "ölçülmedi")


def tests_part(codes: tuple) -> tuple:
    runs = (("tests", "tests"), ("final", "tests-final"))
    measured = all(STATE.get(k) and len(STATE[k]) == len(PROJECTS) for k, _ in runs)
    ok = measured and all(STATE[k][TESTS[c][0]]["yeşil"] and STATE[k][TESTS[c][0]]["adlandırılmış"][c]["sonuç"] == "Passed"
                          for k, _ in runs for c in codes)
    evidence = [{"artefakt": rel(f"{prefix}-{TESTS[c][0]}.trx"), "test": TESTS[c][1], "özet": rel(f"{prefix}-summary.json")}
                for _, prefix in runs for c in codes]
    return measured, ok, evidence


def sabotage_part(ac: str) -> tuple:
    keys = [s.key for s in SABOTAGES if ac in s.acs]
    if not keys:
        return True, True, []
    entries = {e["sabotaj"]: e for e in (STATE.get("sabotage") or []) if "tuttu" in e}
    measured = all(k in entries for k in keys)
    ok = measured and all(entries[k]["tuttu"] for k in keys)
    evidence = [{"artefakt": rel("sabotage-summary.json"), "satır": f"sabotajlar[{k}].tuttu"}]
    for s in SABOTAGES:
        if s.key in keys:
            evidence += [{"artefakt": rel(f"sabotage-{s.key}-{p}.trx"), "test": "outcome=Failed: " + ", ".join(
                TESTS[c][1] for c in s.expected if TESTS[c][0] == p)} for p in s.projects]
    return measured, ok, evidence


def ac_entry(ac: str, criterion: str, codes: tuple, extra: tuple = None) -> dict:
    tm, tok, tev = tests_part(codes)
    sm, sok, sev = sabotage_part(ac)
    measured, ok, ev = tm and sm, tok and sok, tev + sev
    if extra is not None:
        em, eok, eev = extra
        measured, ok, ev = measured and em, ok and eok, ev + eev
    return {"ölçüt": criterion, "sonuç": verdict_of(measured, ok), "kanıt": ev}


def ac_table() -> dict:
    acs = {
        "AC-T1": ac_entry("AC-T1", "Active varlıkta approvalStatus = ApprovalStatus büyük harfle, dört değer (işleyici + "
                                   "HTTP); S4 kırmızı", ("A1", "H1")),
        "AC-T2": ac_entry("AC-T2", "404 aynen (kayıt yok, silinmiş, başka kiracı, beş Active-dışı × Approved); Active + "
                                   "Rejected 200 ve referenceable; var olan LegalEntityReferenceValidationTests değişmeden "
                                   "geçer; S1a / S1b kırmızı", ("A2", "A3", "E1", "H2", "H3")),
        "AC-T3": ac_entry("AC-T3", "200 JSON yedi özellik (ad, tür), /lookup öğeleri aynı; sözleşme örneği okunur; tüketici "
                                   "okuma biçimi testi; S2 / S3 / S5 / S6 kırmızı", ("A4", "H4", "H5", "R1", "R2")),
    }
    kf = STATE.get("known_failures") or {}
    kf_measured = len(kf) == len(SUITES) and all(v.get("çıkış") is not None for v in kf.values())
    kf_ok = kf_measured and all(v["çıkış"] == 0 for v in kf.values())
    kf_ev = [{"artefakt": v["çıktı"], "satır": "ÖZET"} for v in kf.values() if v.get("çıktı")]
    acs["AC-T4"] = ac_entry("AC-T4", "MDM WP kısmı (kabul şartı): LegalEntityLookupConsumerReadFormTests yeşil ve S5 / S6 "
                                     "kırmızı; var olan MdmService testleri süzgeçte değişmeden geçer (taban ve son koşu "
                                     "yeşil, LegalEntityReferenceValidationTests ve LegalEntitiesControllerPermissionTests "
                                     "koştu ve geçti); known_failures check mdm-app / mdm-api çıkış 0",
                            ("R1", "R2"), (kf_measured, kf_ok, kf_ev))
    t5 = STATE.get("ac_t5")
    acs["AC-T5"] = {"ölçüt": "dotnet build MdmService Api çıkış 0; git grep LegalEntityLookupDto taramasında tek yapım "
                             "yeri ToLookupDto",
                    "sonuç": verdict_of(t5 is not None, bool(t5 and t5["geçti"])),
                    "kanıt": [{"artefakt": rel("build-api.log"), "satır": "çıkış (ac-t5-summary.json derleme.çıkış)"},
                              {"artefakt": rel("ac-t5-grep.txt"), "satır": "tümü"},
                              {"artefakt": rel("ac-t5-summary.json"), "satır": "tarama.yapım_yerleri"}]}
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
    if os.environ.get("DITEN_TEST_MONGOD") not in (None, "", MONGOD):
        return f"DITEN_TEST_MONGOD {MONGOD} değil"
    if shutil.which("dotnet") is None:
        return "dotnet bulunamadı"
    if not os.access(MONGOD, os.X_OK):
        return f"{MONGOD} yok ya da çalıştırılamıyor"
    return None


def on_signal(signum, _frame):
    if STATE["signal"] is not None:
        return
    STATE["signal"] = signum
    raise Stop(2, f"DURUR — sinyal {signum}")


def main() -> int:
    global OUT, OUT_ABS
    ap = argparse.ArgumentParser(prog="mdm_0220_dl1_accept.py",
                                 description="MDM-0220-DL1 tek koşuluk kabul kanıtı (MOD-0220 §22 AC-T1…AC-T5)")
    ap.add_argument("--out-dir", required=True)
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
    script_sha = sha256((ROOT / SCRIPT).read_bytes())
    try:
        emit(f"MDM-0220-DL1 kabul kanıtı · betik {SCRIPT} (sha256 {script_sha}) · repo HEAD "
             + git("rev-parse", "HEAD").strip() + " · --out-dir " + OUT)
        stray = outside_out()
        if stray:
            raise Stop(2, f"DURUR — başlangıçta --out-dir dışında değişiklik ({len(stray)} yol, ilk: {stray[0][:80]})")
        mongod_snapshot("mongod-before.txt")
        scope_step()
        emit("== testler (taban)")
        base_green = test_step("tests", "taban", "tests")
        anchor_preflight()
        if not base_green:
            raise Stop(1, "KIRMIZI — taban koşusu yeşil değil; sabotaj, son koşu, derleme ve duman koşulmadı")
        sabotage_step()
        emit("== testler (son)")
        test_step("final", "son", "tests-final")
        known_failures_step()
        emit("== AC-T5")
        ac_t5_step()
        emit("== AC-T4 regresyon dumanı (uyumluluk kanıtı değildir)")
        smoke_step()
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
    with contextlib.suppress(Exception):
        mongod_snapshot("mongod-after.txt")
    acs = ac_table()
    scope = STATE.get("scope")
    gates = {"KAPSAM": {"ölçüt": "taban..HEAD yalnız İzinli yollar; src yalnız LegalEntityModels.cs; tests D/M yok; betik "
                                 "tabanda var ve değişmedi",
                        "sonuç": verdict_of(scope is not None, bool(scope and scope["geçti"])),
                        "kanıt": [{"artefakt": rel("diff-scope.json"), "satır": "geçti"}]}}
    failed = [k for k, v in list(acs.items()) + list(gates.items()) if v["sonuç"] != "geçti"]
    if code == 0 and failed:
        code, verdict = 1, "KIRMIZI — " + ", ".join(f"{k} {dict(acs, **gates)[k]['sonuç']}" for k in failed)
    verdict = verdict or "KABUL KANITI YEŞİL — AC-T1…AC-T5"
    smoke = STATE.get("smoke")
    write_json("summary.json", {
        "betik": SCRIPT, "betik_sha256": script_sha,
        "out_dir": OUT, "çıkış": code, "sonuç": verdict,
        "yük": STATE["loads"],
        "adımlar": {
            "kapsam": rel("diff-scope.json") if scope else None,
            "taban": ({"özet": rel("tests-summary.json"), "yeşil": {p: s["yeşil"] for p, s in STATE["tests"].items()}}
                      if STATE.get("tests") else None),
            "çapa_ön_denetimi": rel("anchors.json") if (OUT_ABS / "anchors.json").exists() else None,
            "sabotaj": ({"özet": rel("sabotage-summary.json"),
                         "ölçülen": sum(1 for e in STATE["sabotage"] if "tuttu" in e),
                         "tuttu": sum(1 for e in STATE["sabotage"] if e.get("tuttu"))}
                        if STATE.get("sabotage") is not None else None),
            "son": ({"özet": rel("tests-final-summary.json"), "yeşil": {p: s["yeşil"] for p, s in STATE["final"].items()}}
                    if STATE.get("final") else None),
            "known_failures": STATE.get("known_failures"),
            "ac_t5": rel("ac-t5-summary.json") if STATE.get("ac_t5") else None,
            "mongod": {"önce": rel("mongod-before.txt"), "sonra": rel("mongod-after.txt")},
        },
        "ac": acs,
        "kapılar": gates,
        "ac_t4_regresyon_dumanı": {
            "not": "regresyon dumanı; uyumluluk kanıtı değildir — kabul şartı değil, çıkış kodunu değiştirmez",
            "özet": rel("ac-t4-smoke-summary.json") if smoke is not None else None,
            "sonuç": ("ölçülmedi" if smoke is None else
                      {n: ("yeşil" if s["yeşil"] else "kırmızı") for n, s in smoke.items()}),
        },
        "sınır": "Kayıt yalnız bu koşuda bu sonuçların alındığını gösterir; sabotajın ölçütü ölçtüğünü ya da çapaların "
                 "talimatın tarif ettiği değişikliği seçtiğini kanıtlamaz (bağımsız inceleme).",
    })
    emit("ÖZET: " + ", ".join(f"{k}={'GEÇTİ' if v['sonuç'] == 'geçti' else v['sonuç'].upper()}"
                              for k, v in list(acs.items()) + list(gates.items())))
    emit("SONUÇ: " + verdict)
    return code


if __name__ == "__main__":
    sys.exit(main())
