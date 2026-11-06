#!/usr/bin/env python3
"""
MDM-FU06-DL1 — E3 kanıt betiği (MOD-0290-FU06 §18 "Yerel yığın (E3): yükseltme yolu", Y1–Y6).

Kule yazar (S-83); işçi DEĞİŞTİRMEZ, yalnız koşar. Tek koşu, run_record altında, temiz ağaçta:

    python3 scripts/run_record.py run --out docs/records/audits/2026-11/mod0290-fu06-dl1/e3-run -- \
        python3 execution/domains/master-data-management/work-packs/scripts/mdm_fu06_dl1_e3.py \
        --base-worktree <repo dışı mutlak yol> --out-dir docs/records/audits/2026-11/mod0290-fu06-dl1/e3-run

--base-worktree   `git worktree add --detach <yol> $(git merge-base origin/main HEAD)` ile açılmış, repo DIŞINDA bir
                  worktree. Betik HEAD'ini, temizliğini ve taban koşulunu ölçer; orada yalnız MDM Api projesini derler;
                  Y6'da `git worktree remove` ile kaldırır (yalnız doğrulanmışsa).
--mongo-port      atılabilir mongod portu (varsayılan 37291; 27017–27021 yasak).
--out-dir         run_record'un --out dizini (aynı yol; run_record onu önceden açar). Y6'da geçici dizin silinmeden önce
                  Auth ve Platform günlüklerinin son 200 satırı, çıktı süzgecinden geçirilerek buraya yazılır
                  (<servis>-<sıra>.tail200.log). Verilmezse günlük kopyası yazılmaz ve bu çıktıda söylenir.

Sırlar YALNIZ ortamdan okunur, argv'ye girmez, çıktıya yazılmaz (değerleri çıktıda süzülür):
    E3_JWT_SECRET               → JwtSettings__Secret (Auth, Platform, MDM, Gateway)
    E3_INTERNAL_API_KEY         → Platform AuthService__InternalApiKey · Auth PlatformService__InternalApiKey ve
                                  InternalEventAuth__ApiKey · MDM PlatformRegistration__InternalApiKey; Y3b başlığı
    E3_MDM_REGISTRATION_ID      → Platform ModuleRegistrationCredentials__Mdm__Identifier ·
                                  MDM PlatformRegistration__ModuleRegistrationCredentialIdentifier
    E3_MDM_REGISTRATION_SECRET  → Platform ModuleRegistrationCredentials__Mdm__ActiveSecret ·
                                  MDM PlatformRegistration__ModuleRegistrationCredentialSecret
İsteğe bağlı: E3_KEEP_TEMP=1 → geçici dizin (günlükler, mongod verisi) silinmez; yolu yazılır, içeriği yazılmaz.

Çıkış kodları:
    0  E3 YEŞİL — Y1–Y6 her beklenti tuttu
    1  E3 KIRMIZI — ölçülen bir beklenti tutmadı (adıyla yazılır)
    2  KULE DURUR — önkoşul tutmadı (port, paylaşılan veritabanı, RabbitMQ, derleme, taban commit, kayıt kimliği)
    3  KULE DURUR, SAHİBE SORAR — kurulum yolunun bir adımı tutmadı (platform yöneticisi jetonu, kiracı, kullanıcı ve
       rol verme, adanmış roller, modules.max, kiracı girişi) ya da çalışma anında ölçülen iki nokta tutmadı (SuperAdmin
       rolü boş veritabanında, Platform iç anahtar ayarı); betik o noktada adıyla durur
    4  E3 BEKLER — 1 dakikalık yük 12'den büyük
    5  kullanım ya da ortam değişkeni eksik
Her yolda (hata, sinyal, erken çıkış) Y6 temizliği koşar: yalnız betiğin başlattığı süreçler, kayıtlı PID + başlangıç
zamanı eşleşirse durdurulur; ada göre süreç öldürülmez.

Ölçüm sınırı: servis ayar adları, uç yolları, günlük satırları ve koleksiyon adları kod okumasıdır (Expected HEAD
13ca54a01); betik kule tarafından koşulmadan önce derlenmedi, çalıştırılmadı. `kurulum()` sırası
docs/records/audits/2026-11/mdm-ct-02-dl1-inspection-01/E3-SETUP-PATH-MEASUREMENT.md §7'dir (kod okuması). Kurulumda
üretilen parolalar ve tek kullanımlık bağlantılar yalnız bellektedir; çıktıda süzülür, --out altına yazılmaz.
"""
from __future__ import annotations

import argparse
import base64
import json
import os
import re
import secrets
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]
PROVIDER = "services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs"
EVIDENCE_PREFIX = "docs/records/audits/2026-11/mod0290-fu06-dl1/"
KEY = "mdm.product-master.read"
ITEMS_READ = "mdm.items.read"
MODULE = "product-item-sku-master"
SCOPE_FORBIDDEN = "LEGAL_ENTITY_SCOPE_FORBIDDEN"
EQUALITY_ERROR = "complete active module descriptor set"
SYNC_APPLIED = "entitlement.sync.applied"
SELF_REGISTERED = "Module manifest self-registered with Platform"
REGISTRATION_SKIPPED = "Module self-registration skipped"
SHARED_MONGO_PORTS = range(27017, 27022)
SERVICE_PORTS = (5000, 5001, 5056, 5057, 5058, 5059, 5060)
LOAD_LIMIT = 12.0
RS_NAME = "rse3fu06"
DB_AUTH, DB_PLATFORM, DB_PLATFORM_JOBS, DB_MDM = "e3_auth", "e3_platform", "e3_platform_jobs", "e3_mdm"
SYNC_WAIT_SECONDS = 60
START_WAIT_SECONDS = 180
SECRET_VARS = ("E3_JWT_SECRET", "E3_INTERNAL_API_KEY", "E3_MDM_REGISTRATION_ID", "E3_MDM_REGISTRATION_SECRET")
# kurulum (E3-SETUP-PATH-MEASUREMENT §7; kod okuması)
PLATFORM_TENANT = "00000000-0000-0000-0000-000000000001"
PERMISSIONS_READY = "Permission auto-registration complete."
PLATFORM_ASSIGN = "platform.tenants.commercial.subscription.assign"
TOOL_PROJECT = "services/Diten.AuthService/tools/Diten.AuthService.FirstPlatformAdministrator"
TOOL_BINARY = "Diten.AuthService.FirstPlatformAdministrator"
SETUP_DOMAIN = "e3.example.test"
SETUP_PLAN = "STARTER"  # FREE değil: users.max 5, kiracı 1'in 6. kullanıcısı reddedilir
DEDICATED_ROLES = {"steward": "ProductDataSteward", "approver": "ProductIdentityApprover",
                   "retirement": "ProductIdentityRetirementSteward"}
GUID_RE = re.compile(r"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")

SERVICES = {
    # name: (port, project dir relative to a worktree root, binary name, health path or None)
    "auth": (5056, "services/Diten.AuthService/src/Diten.AuthService.Api", "Diten.AuthService.Api", "/health/live"),
    "platform": (5057, "services/Diten.Platform/src/Diten.Platform.API", "Diten.Platform.API", "/health/live"),
    "gateway": (5000, "gateway/Diten.ApiGateway", "Diten.ApiGateway", "/health/live"),
    "mdm": (5059, "services/Diten.MdmService/src/Diten.MdmService.Api", "Diten.MdmService.Api", "/health"),
}
BUILDS = (
    "services/Diten.AuthService/src/Diten.AuthService.Api/Diten.AuthService.Api.csproj",
    "services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj",
    "services/Diten.MdmService/src/Diten.MdmService.Api/Diten.MdmService.Api.csproj",
    "gateway/Diten.ApiGateway/Diten.ApiGateway.csproj",
    f"{TOOL_PROJECT}/{TOOL_BINARY}.csproj",
)
JWT_LIKE = re.compile(r"eyJ[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]*")


class Stop(Exception):
    """A planned stop with an exit code and a verdict line."""

    def __init__(self, code: int, verdict: str):
        super().__init__(verdict)
        self.code = code
        self.verdict = verdict


class OwnerStop(Stop):
    def __init__(self, items: list[str]):
        super().__init__(3, "KULE DURUR, SAHİBE SORAR — ölçülmemiş kurulum yolu: " + " · ".join(items))


# ── output: every line goes through the filter ────────────────────────────────────────────────────────────────────
_SECRETS: list[str] = []
RESULTS: list[tuple[str, bool, str]] = []


def scrub(line: str) -> str:
    for secret in _SECRETS:
        if secret:
            line = line.replace(secret, "[SIR]")
    line = JWT_LIKE.sub("[JETON]", line)
    if "authorization" in line.lower():
        line = "[Authorization içeren satır yazılmadı]"
    return line


def emit(line: str) -> None:
    print(scrub(line), flush=True)


def check(step: str, ok: bool, what: str) -> bool:
    RESULTS.append((step, ok, what))
    emit(f"KONTROL {step} {'GEÇTİ' if ok else 'KALDI'}: {what}")
    return ok


# ── small tools ───────────────────────────────────────────────────────────────────────────────────────────────────
def run(argv: list[str], cwd: Path | None = None, env: dict | None = None, timeout: int = 600) -> tuple[int, str]:
    done = subprocess.run(argv, cwd=cwd, env=env, capture_output=True, text=True, timeout=timeout)
    return done.returncode, (done.stdout or "") + (done.stderr or "")


def git(*args: str, cwd: Path = ROOT) -> str:
    code, out = run(["git", "-C", str(cwd), *args], timeout=120)
    if code != 0:
        raise Stop(2, f"KULE DURUR — git {' '.join(args[:2])} çıkış {code}")
    return out.strip()


def load_1min() -> float:
    code, out = run(["uptime"], timeout=10)
    m = re.search(r"load averages?:\s*([0-9]+[.,][0-9]+)", out)
    if code == 0 and m:
        return float(m.group(1).replace(",", "."))
    return os.getloadavg()[0]


def load_gate(where: str) -> None:
    # Card / prompt rule: above the limit, wait 60 s and measure again; stop only if it stays above for 15 minutes.
    for attempt in range(16):
        load = load_1min()
        emit(f"yük ({where}): 1 dakikalık {load:.2f} (eşik {LOAD_LIMIT:.0f})")
        if load <= LOAD_LIMIT:
            return
        if attempt < 15:
            time.sleep(60)
    raise Stop(4, f"E3 BEKLER — 1 dakikalık yük 15 dakika boyunca {LOAD_LIMIT:.0f} üstünde ({where})")


def listeners(port: int) -> list[str]:
    code, out = run(["lsof", "-nP", f"-iTCP:{port}", "-sTCP:LISTEN", "-t"], timeout=20)
    return [p for p in out.split() if p.isdigit()]


def start_time(pid: int) -> str:
    code, out = run(["ps", "-o", "lstart=", "-p", str(pid)], timeout=10)
    return out.strip() if code == 0 else ""


def http(method: str, url: str, headers: dict | None = None, body: dict | None = None, timeout: int = 30) -> tuple[int, bytes]:
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method, headers=dict(headers or {}))
    if data is not None:
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            return resp.status, resp.read()
    except urllib.error.HTTPError as e:
        return e.code, e.read() or b""
    except (urllib.error.URLError, OSError):
        return 0, b""


def json_of(raw: bytes):
    try:
        return json.loads(raw.decode("utf-8", errors="replace")) if raw else None
    except ValueError:
        return None


def field_ci(obj, name: str):
    if isinstance(obj, dict):
        for k, v in obj.items():
            if k.lower() == name.lower():
                return v
    return None


def data_of(raw: bytes):
    doc = json_of(raw)
    inner = field_ci(doc, "data")
    return inner if inner is not None else doc


def token_permissions(token: str) -> set[str]:
    try:
        part = token.split(".")[1]
        payload = json.loads(base64.urlsafe_b64decode(part + "=" * (-len(part) % 4)))
    except (IndexError, ValueError):
        return set()
    claim = payload.get("permission", [])
    return {claim} if isinstance(claim, str) else set(claim or [])


# ── processes: only what this script started, stopped by PID + start time ───────────────────────────────────────────
@dataclass
class Owned:
    name: str
    pid: int
    started: str
    log: Path
    proc: subprocess.Popen


@dataclass
class Ctx:
    tmp: Path
    base: str = ""
    base_worktree: Path | None = None
    base_verified: bool = False
    mongo_port: int = 37291
    vhost: str = ""
    vhost_created: bool = False
    owned: list[Owned] = field(default_factory=list)
    shared_dbs_before: list | None = None
    shared_mongo_reachable: bool = False
    env_secrets: dict = field(default_factory=dict)
    out_dir: Path | None = None


def start_process(ctx: Ctx, name: str, argv: list[str], cwd: Path, env: dict) -> Owned:
    log = ctx.tmp / f"{name}-{len(ctx.owned)}.log"
    handle = open(log, "wb")
    proc = subprocess.Popen(argv, cwd=cwd, env=env, stdout=handle, stderr=subprocess.STDOUT, stdin=subprocess.DEVNULL,
                            start_new_session=True)
    handle.close()
    time.sleep(0.5)
    owned = Owned(name, proc.pid, start_time(proc.pid), log, proc)
    ctx.owned.append(owned)
    emit(f"başlatıldı: {name} PID {owned.pid} başlangıç '{owned.started}'")
    return owned


def stop_process(owned: Owned) -> bool:
    if owned.proc.poll() is not None:
        emit(f"durdu (zaten): {owned.name} PID {owned.pid}")
        return True
    now = start_time(owned.pid)
    if not owned.started or now != owned.started:
        emit(f"DURDURULMADI: {owned.name} PID {owned.pid} başlangıç zamanı eşleşmiyor ('{now}' ≠ '{owned.started}')")
        return False
    os.kill(owned.pid, signal.SIGTERM)
    for _ in range(60):
        if owned.proc.poll() is not None:
            emit(f"durduruldu: {owned.name} PID {owned.pid}")
            return True
        time.sleep(0.5)
    if start_time(owned.pid) == owned.started:
        os.kill(owned.pid, signal.SIGKILL)
        owned.proc.wait(timeout=20)
        emit(f"durduruldu (SIGKILL): {owned.name} PID {owned.pid}")
        return True
    return False


def log_count(owned_list: list[Owned], name: str, *needles: str) -> int:
    count = 0
    for owned in owned_list:
        if owned.name != name or not owned.log.exists():
            continue
        for line in owned.log.read_text(encoding="utf-8", errors="replace").splitlines():
            if all(n.lower() in line.lower() for n in needles):
                count += 1
    return count


def service_owned(ctx: Ctx, name: str) -> Owned:
    alive = [o for o in ctx.owned if o.name == name and o.proc.poll() is None]
    if not alive:
        raise Stop(1, f"E3 KIRMIZI — {name} süreci çalışmıyor")
    return alive[-1]


def shared_connection_check(ctx: Ctx, step: str) -> None:
    """F-15: no process this script started holds a connection to the shared mongod ports."""
    bad = []
    for owned in ctx.owned:
        if owned.proc.poll() is not None or owned.name == "mongod":
            continue
        _, out = run(["lsof", "-a", "-nP", "-p", str(owned.pid), "-iTCP", "-sTCP:ESTABLISHED"], timeout=20)
        for port in re.findall(r"->[^ ]*:(\d+)", out):
            if int(port) in SHARED_MONGO_PORTS:
                bad.append(f"{owned.name}:{port}")
    if not check(step, not bad, f"F-15 betiğin servisleri paylaşılan mongod portlarına (27017–27021) bağlı değil{(' — ' + ', '.join(bad)) if bad else ''}"):
        raise Stop(2, "KULE DURUR — bir servis paylaşılan mongod'a bağlandı (F-15)")


# ── mongo helpers ───────────────────────────────────────────────────────────────────────────────────────────────────
def mongosh(port: int, js: str, db: str = "admin") -> tuple[int, str]:
    return run(["mongosh", "--quiet", "--port", str(port), db, "--eval", js], timeout=60)


def shared_databases() -> list | None:
    code, out = mongosh(27017, "JSON.stringify(db.adminCommand({listDatabases:1}).databases.map(d=>[d.name,d.sizeOnDisk]))")
    if code != 0:
        return None
    try:
        return json.loads(out.strip().splitlines()[-1])
    except (ValueError, IndexError):
        return None


def own_query(ctx: Ctx, db: str, js: str):
    code, out = mongosh(ctx.mongo_port, f"JSON.stringify({js})", db)
    if code != 0:
        raise Stop(1, f"E3 KIRMIZI — kendi mongod'unda sorgu çıkış {code} ({db})")
    try:
        return json.loads(out.strip().splitlines()[-1])
    except (ValueError, IndexError):
        raise Stop(1, f"E3 KIRMIZI — kendi mongod'unda sorgu okunamadı ({db})")


def platform_catalog(ctx: Ctx) -> tuple[str | None, int]:
    item = own_query(ctx, DB_PLATFORM,
                     f"db.platform_module_catalog.findOne({{ModuleCode:/^{MODULE}$/i, IsDeleted:false}},{{ModuleVersion:1,_id:0}})")
    actions = own_query(ctx, DB_PLATFORM,
                        f"db.platform_module_page_action_descriptors.countDocuments({{ModuleCode:/^{MODULE}$/i, "
                        f"PageCode:'GLOBAL_PRODUCTS', ActionCode:'READ_PRODUCT_MASTER', IsDeleted:false}})")
    return (item or {}).get("ModuleVersion"), int(actions or 0)


def auth_permission_row(ctx: Ctx):
    return own_query(ctx, DB_AUTH, f"db.permissions.findOne({{Key:'{KEY}'}},{{Module:1,Scope:1,IsDeleted:1,_id:0}})")


# ── preconditions ───────────────────────────────────────────────────────────────────────────────────────────────────
def preconditions(ctx: Ctx, base_worktree: Path) -> None:
    emit("== Önkoşullar (yığın açılmadan)")
    load_gate("başlangıç")

    busy = {p: listeners(p) for p in SERVICE_PORTS}
    emit("port listesi (başlangıç): " + ", ".join(f"{p}={'dolu' if v else 'boş'}" for p, v in busy.items()))
    if any(busy.values()):
        raise Stop(2, "KULE DURUR — servis portlarından biri dinleniyor; başka oturum olabilir, yeniden başlatılmaz")
    if ctx.mongo_port in SHARED_MONGO_PORTS or not (1024 < ctx.mongo_port < 65536) or listeners(ctx.mongo_port):
        raise Stop(2, f"KULE DURUR — atılabilir mongod portu {ctx.mongo_port} kullanılamaz (27017–27021 yasak ya da dolu)")

    ctx.shared_dbs_before = shared_databases()
    ctx.shared_mongo_reachable = ctx.shared_dbs_before is not None
    if ctx.shared_mongo_reachable:
        emit("paylaşılan mongod (27017) veritabanları (ad, boyut): " + json.dumps(ctx.shared_dbs_before, ensure_ascii=False))
    else:
        emit("paylaşılan mongod (27017): erişilemedi — sonda da aynı ölçülür")

    for tool in ("mongod", "mongosh", "rabbitmqctl", "lsof", "dotnet"):
        if shutil.which(tool) is None:
            raise Stop(2, f"KULE DURUR — '{tool}' bulunamadı")

    # base commit: git merge-base origin/main HEAD, and the pack's condition on it
    ctx.base = git("merge-base", "origin/main", "HEAD")
    head = git("rev-parse", "HEAD")
    emit(f"taban commit (merge-base origin/main HEAD): {ctx.base} · dal HEAD: {head}")
    base_provider = git("show", f"{ctx.base}:{PROVIDER}")
    cond = 'ModuleVersion: "1.3.0"' in base_provider and "READ_PRODUCT_MASTER" not in base_provider
    if not check("P-taban", cond, "taban commit'te sağlayıcı 1.3.0 ve READ_PRODUCT_MASTER yok"):
        raise Stop(2, "KULE DURUR — taban koşulu tutmadı (paket §18: kuleye dur)")

    real = base_worktree.resolve()
    if not base_worktree.is_absolute() or str(real).startswith(str(ROOT.resolve()) + os.sep) or real == ROOT.resolve():
        raise Stop(2, "KULE DURUR — --base-worktree repo dışında mutlak bir yol olmalı")
    their_common = Path(git("rev-parse", "--path-format=absolute", "--git-common-dir", cwd=real)).resolve()
    our_common = Path(git("rev-parse", "--path-format=absolute", "--git-common-dir")).resolve()
    if their_common != our_common:
        raise Stop(2, "KULE DURUR — --base-worktree bu deponun worktree'si değil")
    base_head = git("rev-parse", "HEAD", cwd=real)
    base_dirty = git("status", "--porcelain", cwd=real)
    if not check("P-taban-worktree", base_head == ctx.base and not base_dirty,
                 f"taban worktree HEAD {base_head[:12]} = taban, temiz"):
        raise Stop(2, "KULE DURUR — taban worktree yanlış commit'te ya da kirli")
    ctx.base_worktree, ctx.base_verified = real, True

    # builds: this branch's four services + the FirstPlatformAdministrator tool, then the base MDM in its worktree (load gate before each)
    for csproj in BUILDS:
        build(ROOT, csproj)
    build(real, BUILDS[2])

    # RabbitMQ: running, own vhost
    code, out = run(["rabbitmqctl", "-q", "list_vhosts"], timeout=60)
    if code != 0:
        raise Stop(2, "KULE DURUR — RabbitMQ çalışmıyor ya da rabbitmqctl erişilemiyor")
    ctx.vhost = f"e3fu06_{int(time.time())}"
    if ctx.vhost in out.split():
        raise Stop(2, "KULE DURUR — vhost adı zaten var")
    if run(["rabbitmqctl", "-q", "add_vhost", ctx.vhost], timeout=60)[0] != 0:
        raise Stop(2, "KULE DURUR — vhost eklenemedi")
    ctx.vhost_created = True
    if run(["rabbitmqctl", "-q", "set_permissions", "-p", ctx.vhost, "guest", ".*", ".*", ".*"], timeout=60)[0] != 0:
        raise Stop(2, "KULE DURUR — vhost izinleri verilemedi")
    emit(f"RabbitMQ vhost: {ctx.vhost} (guest izinli)")
    # MDM registration identity: Platform's ModuleRegistrationCredentials:Mdm and MDM's PlatformRegistration
    # credential come from the SAME two environment variables, so they match by construction; Y1 measures the push.
    emit("MDM kayıt kimliği: Platform ve MDM aynı iki ortam değişkeninden (E3_MDM_REGISTRATION_ID / _SECRET) alır")


def build(worktree: Path, csproj: str) -> None:
    load_gate(f"derleme {csproj.rsplit('/', 1)[-1]}")
    code, out = run(["dotnet", "build", str(worktree / csproj), "-c", "Debug", "--nologo", "-v", "q"], timeout=1800)
    errors = re.search(r"(\d+) Error\(s\)", out)
    where = "taban" if worktree != ROOT else "dal"
    if not check("P-derleme", code == 0, f"{where} {csproj.rsplit('/', 1)[-1]} derlendi (çıkış {code}, hata {errors.group(1) if errors else '?'})"):
        raise Stop(2, "KULE DURUR — derleme başarısız")


# ── the stack ───────────────────────────────────────────────────────────────────────────────────────────────────────
def start_mongod(ctx: Ctx) -> None:
    dbpath = ctx.tmp / "mongo-data"
    dbpath.mkdir()
    plain_env = {k: v for k, v in os.environ.items() if not k.startswith("E3_")}
    start_process(ctx, "mongod", [shutil.which("mongod"), "--replSet", RS_NAME, "--port", str(ctx.mongo_port),
                                  "--bind_ip", "127.0.0.1", "--dbpath", str(dbpath), "--quiet"], ctx.tmp, plain_env)
    for _ in range(60):
        if listeners(ctx.mongo_port):
            break
        time.sleep(1)
    init = f"rs.initiate({{_id:'{RS_NAME}',members:[{{_id:0,host:'127.0.0.1:{ctx.mongo_port}'}}]}})"
    mongosh(ctx.mongo_port, init)
    for _ in range(60):
        code, out = mongosh(ctx.mongo_port, "db.hello().isWritablePrimary")
        if code == 0 and out.strip().endswith("true"):
            emit(f"atılabilir mongod: port {ctx.mongo_port}, replSet {RS_NAME}, yeni dbpath (geçici dizin)")
            return
        time.sleep(1)
    raise Stop(2, "KULE DURUR — atılabilir mongod birincil olmadı")


def mongo_uri(ctx: Ctx) -> str:
    return f"mongodb://127.0.0.1:{ctx.mongo_port}/?replicaSet={RS_NAME}&directConnection=true"


def service_env(ctx: Ctx, name: str) -> dict:
    env = {k: v for k, v in os.environ.items() if not k.startswith("E3_") and k != "ASPNETCORE_URLS"}
    s = ctx.env_secrets
    uri = mongo_uri(ctx)
    env.update({"ASPNETCORE_ENVIRONMENT": "Development", "DOTNET_ENVIRONMENT": "Development",
                "JwtSettings__Secret": s["E3_JWT_SECRET"]})
    eventing = {"Eventing__Transport": "RabbitMQ", "Eventing__VirtualHost": ctx.vhost}
    # A missing X-Tenant-Id is a 400 "Missing Tenant", never a silent fall to the platform tenant (dev bypass)
    no_bypass = {"TenantResolution__DevBypassEnabled": "false"}
    # The setup path needs no e-mail; nothing goes to the shared Mailpit (localhost:1025)
    no_smtp = {"Smtp__Enabled": "false"}
    if name == "auth":
        env.update({"MongoDbSettings__ConnectionString": uri, "MongoDbSettings__DatabaseName": DB_AUTH,
                    "PlatformService__InternalApiKey": s["E3_INTERNAL_API_KEY"],
                    "InternalEventAuth__ApiKey": s["E3_INTERNAL_API_KEY"], **eventing, **no_bypass, **no_smtp})
    elif name == "platform":
        env.update({"MongoDbSettings__ConnectionString": uri, "MongoDbSettings__DatabaseName": DB_PLATFORM,
                    "BackgroundJobs__StorageDatabaseName": DB_PLATFORM_JOBS,
                    "AuthService__InternalApiKey": s["E3_INTERNAL_API_KEY"],
                    "ModuleRegistrationCredentials__Mdm__Identifier": s["E3_MDM_REGISTRATION_ID"],
                    "ModuleRegistrationCredentials__Mdm__ActiveSecret": s["E3_MDM_REGISTRATION_SECRET"],
                    "Eventing__WorkerEnabled": "true", **eventing, **no_smtp})
    elif name == "gateway":
        env.update(no_bypass)
    elif name == "mdm":
        env.update({"Mongo__ConnectionString": uri, "Mongo__DatabaseName": DB_MDM,
                    "PlatformRegistration__InternalApiKey": s["E3_INTERNAL_API_KEY"],
                    "PlatformRegistration__ModuleRegistrationCredentialIdentifier": s["E3_MDM_REGISTRATION_ID"],
                    "PlatformRegistration__ModuleRegistrationCredentialSecret": s["E3_MDM_REGISTRATION_SECRET"], **eventing})
    return env


def start_service(ctx: Ctx, name: str, worktree: Path) -> Owned:
    port, rel, binary, health = SERVICES[name]
    project = worktree / rel
    exe = project / "bin" / "Debug" / "net8.0" / binary
    argv = [str(exe)] if os.access(exe, os.X_OK) else ["dotnet", "exec", str(exe) + ".dll"]
    owned = start_process(ctx, name, argv + ["--urls", f"http://localhost:{port}"], project, service_env(ctx, name))
    deadline = time.time() + START_WAIT_SECONDS
    while time.time() < deadline:
        if owned.proc.poll() is not None:
            raise Stop(2, f"KULE DURUR — {name} açılışta durdu (çıkış {owned.proc.returncode})")
        if name != "mdm" and http("GET", f"http://localhost:{port}{health}", timeout=5)[0] == 200:
            emit(f"{name}: {health} 200")
            shared_connection_check(ctx, f"F-15/{name}")
            return owned
        if name == "mdm" and log_count([owned], "mdm", SELF_REGISTERED, MODULE) > 0:
            emit(f"mdm: günlükte '{SELF_REGISTERED} … ModuleCode={MODULE}'")
            shared_connection_check(ctx, "F-15/mdm")
            return owned
        if name == "mdm" and log_count([owned], "mdm", REGISTRATION_SKIPPED) > 0:
            raise Stop(2, "KULE DURUR — MDM kayıt kimliği yapılandırılmamış (registration skipped)")
        time.sleep(2)
    raise Stop(2, f"KULE DURUR — {name} {START_WAIT_SECONDS} sn içinde hazır olmadı")


# ── setup (E3-SETUP-PATH-MEASUREMENT §7, code reading): any step that does not hold stops by name ─────────────────────
@dataclass
class User:
    email: str
    password: str
    tenant: str = ""  # tenant guid, sent as X-Tenant-Id on the tenant login
    id: str = ""


@dataclass
class Setup:
    platform_admin_token: str
    tenant1: str
    tenant2: str
    t1: dict          # role -> User: "admin", "viewer", "nokey", "steward", "approver", "retirement"
    t2: dict          # role -> User: "admin", "viewer"
    platform_admin: User | None = None  # credentials kept in memory: the platform token lives 15 minutes


def remember_secret(value) -> None:
    if isinstance(value, str) and value:
        _SECRETS.append(value)


def new_password() -> str:
    """Upper + lower + digit + special, ≥ 16 (platform and tenant default policy: ≥ 10, upper, special)."""
    password = "Aa1!" + secrets.token_urlsafe(18)
    remember_secret(password)
    return password


def bearer(token: str) -> dict:
    return {"Authorization": f"Bearer {token}"}


def guid_of(value) -> str | None:
    if isinstance(value, dict):
        value = field_ci(value, "id")
    return value if isinstance(value, str) and GUID_RE.match(value) else None


def rows_of(value) -> list:
    if isinstance(value, list):
        return value
    for name in ("items", "data", "results"):
        inner = field_ci(value, name)
        if isinstance(inner, list):
            return inner
    return []


def mongo_tenant(tenant: str) -> str:
    """TenantId as written by Auth (standard UUID); the string form is matched too, nothing else."""
    return f"{{$in:[UUID('{tenant}'),'{tenant}']}}"


def wait_platform_permissions(ctx: Ctx) -> None:
    deadline = time.time() + START_WAIT_SECONDS
    while time.time() < deadline:
        if log_count(ctx.owned, "platform", PERMISSIONS_READY) > 0:
            emit(f"platform: günlükte '{PERMISSIONS_READY}'")
            return
        time.sleep(2)
    raise OwnerStop([f"Platform günlüğünde '{PERMISSIONS_READY}' {START_WAIT_SECONDS} sn içinde görülmedi "
                     "(platform.* izinleri SuperAdmin'e verilmedi)"])


def measure_superadmin(ctx: Ctx) -> None:
    """Runtime measurement (not read in DataSeeder): the platform tenant's SuperAdmin role exists on an empty database."""
    js = (f"db.roles.countDocuments({{Name:'SuperAdmin', TenantId:{mongo_tenant(PLATFORM_TENANT)}, "
          f"IsDeleted:{{$ne:true}}}})")
    deadline = time.time() + 30
    count = int(own_query(ctx, DB_AUTH, js) or 0)
    while not count and time.time() < deadline:
        time.sleep(3)
        count = int(own_query(ctx, DB_AUTH, js) or 0)
    emit(f"ÖLÇÜM (çalışma anı): Auth roles, platform kiracısında SuperAdmin sayısı {count}")
    if not count:
        raise OwnerStop(["SuperAdmin rolü boş veritabanında oluşmadı (DataSeeder satırı kod okumasıyla ölçülmemişti)"])


def measure_platform_internal_key(ctx: Ctx, email: str) -> None:
    """Runtime measurement: Platform compares the key this script gives it (AuthService__InternalApiKey) on its internal
    surface. The record the tool just wrote is Active, so a matching key answers 200; a 401 means the setting differs."""
    query = urllib.parse.urlencode({"email": email})
    status, _ = http("GET", f"http://localhost:5057/api/internal/platform-administrators/status?{query}",
                     headers={"X-Internal-Api-Key": ctx.env_secrets["E3_INTERNAL_API_KEY"]})
    emit(f"ÖLÇÜM (çalışma anı): Platform iç ucu platform-administrators/status, betiğin iç anahtarıyla → {status}")
    if status == 401:
        raise OwnerStop(["Platform iç anahtar ayar adı: Platform, AuthService__InternalApiKey ile verilen anahtarı kabul "
                         "etmedi (iç uç 401)"])
    if status != 200:
        raise OwnerStop([f"Platform yönetici kaydı iç uçta okunamadı (durum {status})"])


def first_platform_admin(ctx: Ctx) -> User:
    """FirstPlatformAdministrator tool → one-time link (read, then deleted) → reset-password."""
    admin = User(f"e3-platform-admin@{SETUP_DOMAIN}", new_password(), PLATFORM_TENANT)
    link = ctx.tmp / f"platform-admin-link-{secrets.token_hex(4)}"
    dll = ROOT / TOOL_PROJECT / "bin" / "Debug" / "net8.0" / f"{TOOL_BINARY}.dll"
    env = {k: v for k, v in os.environ.items() if not k.startswith(("E3_", "DITEN_"))}
    env.update({"DITEN_AUTH_MONGO_CONNECTION": mongo_uri(ctx), "DITEN_AUTH_MONGO_DATABASE": DB_AUTH,
                "DITEN_PLATFORM_MONGO_CONNECTION": mongo_uri(ctx), "DITEN_PLATFORM_MONGO_DATABASE": DB_PLATFORM,
                "JwtSettings__Secret": ctx.env_secrets["E3_JWT_SECRET"]})  # Auth's own secret: the link hash uses it
    query: dict = {}
    try:
        code, _ = run(["dotnet", "exec", str(dll), "--email", admin.email, "--frontend-base-url", "http://localhost:5001",
                       "--link-file", str(link)], cwd=ctx.tmp, env=env, timeout=300)
        emit(f"FirstPlatformAdministrator aracı: çıkış {code} (0 oluştu · 1 bağlantı · 2 kullanım · 3 reddedildi · 4 yarıda)")
        if code != 0:
            raise OwnerStop([f"FirstPlatformAdministrator aracı çıkış {code}"])
        if link.exists():
            query = urllib.parse.parse_qs(urllib.parse.urlsplit(link.read_text(encoding="utf-8").strip()).query)
    finally:
        if link.exists():
            link.unlink()
    token = (query.get("token") or [""])[0]
    if not token:
        raise OwnerStop(["FirstPlatformAdministrator bağlantı dosyasında token yok"])
    remember_secret(token)
    emit("bağlantı dosyası okundu ve silindi (içerik yazılmaz)")

    measure_platform_internal_key(ctx, admin.email)
    status, _ = http("POST", "http://localhost:5056/api/platform-auth/reset-password",
                     body={"email": admin.email, "token": token, "newPassword": admin.password})
    del token
    emit(f"platform yöneticisi parola belirleme (reset-password) → {status}")
    if status not in (200, 204):
        raise OwnerStop([f"platform yöneticisi parola belirleme (reset-password) → {status}"])
    return admin


def platform_login(admin: User) -> str:
    status, raw = http("POST", "http://localhost:5056/api/platform-auth/login",
                       body={"email": admin.email, "password": admin.password, "rememberMe": False})
    token = field_ci(data_of(raw), "accessToken") if status == 200 else None
    if not isinstance(token, str) or not token:
        raise OwnerStop([f"platform yöneticisi girişi jeton vermedi (durum {status})"])
    return token


def platform_token(setup: Setup) -> str:
    """Signs the platform administrator in again: the platform token lives 15 minutes (Y2a, Y4a, Y5 call it)."""
    setup.platform_admin_token = platform_login(setup.platform_admin)
    return setup.platform_admin_token


def starter_plan(token: str) -> str:
    status, raw = http("GET", "http://localhost:5057/api/platform/subscription-plans", headers=bearer(token))
    for row in rows_of(data_of(raw)) if status == 200 else []:
        if str(field_ci(row, "code") or "").upper() == SETUP_PLAN and guid_of(field_ci(row, "id")):
            return field_ci(row, "id")
    raise OwnerStop([f"{SETUP_PLAN} planı okunamadı (GET /api/platform/subscription-plans → {status})"])


def create_tenant(ctx: Ctx, token: str, plan_id: str, n: int) -> tuple[str, User]:
    """POST /api/admin/tenants → Admin/Viewer counted in Auth → tenant-admin-invited (internal key) → set-password."""
    admin = User(f"e3-t{n}-admin@{SETUP_DOMAIN}", new_password())
    first, last = "E3", f"Admin {n}"
    status, raw = http("POST", "http://localhost:5057/api/admin/tenants", headers=bearer(token),
                       body={"name": f"E3 Tenant {n}", "domain": f"e3-t{n}.example.test", "displayName": f"E3 Tenant {n}",
                             "planId": plan_id, "tenantType": 0,  # Customer = 0 (no string enum converter found)
                             "initialAdmin": {"firstName": first, "lastName": last, "email": admin.email}})
    tenant = guid_of(data_of(raw)) if status in (200, 201) else None
    emit(f"kiracı {n}: POST /api/admin/tenants → {status}")
    if not tenant:
        raise OwnerStop([f"kiracı {n} oluşturma (POST /api/admin/tenants → {status})"])
    admin.tenant = tenant

    roles = int(own_query(ctx, DB_AUTH, f"db.roles.countDocuments({{TenantId:{mongo_tenant(tenant)}, "
                                        f"Name:{{$in:['Admin','Viewer']}}, IsDeleted:{{$ne:true}}}})") or 0)
    emit(f"kiracı {n}: Auth'ta Admin + Viewer rol sayısı {roles} (beklenen 2)")
    if roles != 2:
        raise OwnerStop([f"kiracı {n}: tenant.activated sonrası Auth'ta Admin/Viewer {roles} (beklenen 2)"])

    status, raw = http("GET", f"http://localhost:5057/api/admin/tenants/{tenant}", headers=bearer(token))
    code = field_ci(data_of(raw), "code") if status == 200 else None
    status2, raw2 = http("GET", f"http://localhost:5057/api/admin/tenants/{tenant}/admin-users", headers=bearer(token))
    row = next((r for r in rows_of(data_of(raw2)) if status2 == 200
                and str(field_ci(r, "email") or "").lower() == admin.email), None)
    admin_user_id = guid_of(field_ci(row, "id")) if row else None
    if not isinstance(code, str) or not code or not admin_user_id:
        raise OwnerStop([f"kiracı {n}: kiracı kodu (durum {status}) ya da yönetici satırı (durum {status2}) okunamadı"])

    body = {"tenantId": tenant, "adminUserId": admin_user_id, "tenantCode": code, "tenantName": f"E3 Tenant {n}",
            "email": admin.email, "name": field_ci(row, "name") or f"{first} {last}", "trigger": "operator-invite"}
    for attempt in range(1, 4):
        status, raw = http("POST", "http://localhost:5056/internal/events/tenant-admin-invited",
                           headers={"X-Internal-Api-Key": ctx.env_secrets["E3_INTERNAL_API_KEY"]}, body=body)
        emit(f"kiracı {n}: tenant-admin-invited (deneme {attempt}) → {status}")
        if status != 409:
            break
        time.sleep(2)
    setup_token = field_ci(data_of(raw), "setupToken") if status == 200 else None
    if not isinstance(setup_token, str) or not setup_token:
        raise OwnerStop([f"kiracı {n}: ilk yönetici kurulum bağlantısı alınamadı (tenant-admin-invited → {status})"])
    remember_secret(setup_token)
    status, _ = http("POST", "http://localhost:5000/api/users/set-password",
                     body={"email": admin.email, "token": setup_token, "newPassword": admin.password})
    del setup_token
    emit(f"kiracı {n}: yönetici set-password → {status}")
    if status not in (200, 204):
        raise OwnerStop([f"kiracı {n}: yönetici parola belirleme (set-password → {status})"])
    return tenant, admin


def create_user(admin_token: str, tenant: str, n: int, label: str) -> User:
    user = User(f"e3-t{n}-{label}@{SETUP_DOMAIN}", new_password(), tenant)
    status, raw = http("POST", "http://localhost:5000/api/users", headers=bearer(admin_token),
                       body={"email": user.email, "password": user.password, "firstName": "E3", "lastName": f"T{n} {label}"})
    user.id = guid_of(data_of(raw)) if status in (200, 201) else ""
    emit(f"kiracı {n}: kullanıcı {label} POST /api/users → {status}")
    if not user.id:
        raise OwnerStop([f"kiracı {n}: kullanıcı {label} oluşturma (POST /api/users → {status})"])
    return user


def tenant_roles(admin_token: str, n: int) -> dict:
    status, raw = http("GET", "http://localhost:5000/api/roles", headers=bearer(admin_token))
    if status != 200:
        raise OwnerStop([f"kiracı {n}: rol listesi (GET /api/roles → {status})"])
    return {str(field_ci(r, "name") or "").lower(): guid_of(field_ci(r, "id"))
            for r in rows_of(data_of(raw)) if guid_of(field_ci(r, "id"))}


def assign_role(admin_token: str, n: int, label: str, user: User, roles: dict, role: str) -> None:
    role_id = roles.get(role.lower())
    if not role_id:
        raise OwnerStop([f"kiracı {n}: {role} rolü yok"])
    status, _ = http("POST", f"http://localhost:5000/api/users/{user.id}/roles", headers=bearer(admin_token),
                     body={"roleId": role_id})
    emit(f"kiracı {n}: {label} → {role} → {status}")
    if status not in (200, 201, 204):
        raise OwnerStop([f"kiracı {n}: {label} kullanıcısına {role} rolü verilemedi ({status})"])


def kurulum(ctx: Ctx) -> Setup:
    """E3-SETUP-PATH-MEASUREMENT §7: platform administrator → two tenants (STARTER) → users and roles. The three dedicated
    roles exist only after the entitlement sync, so they are assigned in Y2a (adanmis_roller)."""
    emit("== Kurulum — platform yöneticisi, iki kiracı, kullanıcılar ve roller")
    wait_platform_permissions(ctx)
    measure_superadmin(ctx)
    admin = first_platform_admin(ctx)
    token = platform_login(admin)
    if PLATFORM_ASSIGN not in token_permissions(token):
        raise OwnerStop([f"platform yöneticisi jetonunda {PLATFORM_ASSIGN} yok (SuperAdmin'e platform.* eşitlemesi eksik)"])
    emit(f"platform yöneticisi jetonu: {PLATFORM_ASSIGN} var")
    plan_id = starter_plan(token)
    tenant1, admin1 = create_tenant(ctx, token, plan_id, 1)
    tenant2, admin2 = create_tenant(ctx, token, plan_id, 2)
    t1, t2 = {"admin": admin1}, {"admin": admin2}
    for n, tenant, users, labels in ((1, tenant1, t1, ("viewer", "nokey", "steward", "approver", "retirement")),
                                     (2, tenant2, t2, ("viewer",))):
        admin_token = login(users["admin"])
        for label in labels:
            users[label] = create_user(admin_token, tenant, n, label)
        assign_role(admin_token, n, "viewer", users["viewer"], tenant_roles(admin_token, n), "Viewer")
        del admin_token  # admin is already Admin (invitation path); nokey stays without a role
    return Setup(platform_admin_token=token, tenant1=tenant1, tenant2=tenant2, t1=t1, t2=t2, platform_admin=admin)


def auth_log_tail(ctx: Ctx, tenant: str, limit: int = 25) -> list[str]:
    """The last Auth log lines about this tenant or the entitlement sync (printed only through emit's filter)."""
    lines: list[str] = []
    for owned in ctx.owned:
        if owned.name == "auth" and owned.log.exists():
            lines += [l for l in owned.log.read_text(encoding="utf-8", errors="replace").splitlines()
                      if tenant.lower() in l.lower() or "entitlement" in l.lower() or "role name collision" in l.lower()]
    return lines[-limit:]


def adanmis_roller(ctx: Ctx, setup: Setup) -> None:
    """Y2a, after the entitlement sync: tenant 1 only; tenant 2 gets no dedicated role. The roles are created by Auth's
    Grant for this module (EntitlementPermissionSyncService.ReconcileSpecialProfilesAsync → ResolveDedicatedRolesAsync
    createMissing: true), so they are polled for up to SYNC_WAIT_SECONDS instead of read once."""
    emit("== Y2a — adanmış üç rol (hak eşitlemesinden sonra, yalnız kiracı 1)")
    admin_token = login(setup.t1["admin"])
    deadline = time.time() + SYNC_WAIT_SECONDS
    roles = tenant_roles(admin_token, 1)
    missing = [name for name in DEDICATED_ROLES.values() if name.lower() not in roles]
    while missing and time.time() < deadline:
        time.sleep(3)
        roles = tenant_roles(admin_token, 1)
        missing = [name for name in DEDICATED_ROLES.values() if name.lower() not in roles]
    if missing:
        emit(f"kiracı 1 rol adları ({len(roles)}): " + ", ".join(sorted(roles)))
        emit(f"Auth günlüğü — kiracı 1 / entitlement satırlarının sonu (süzülmüş):")
        for line in auth_log_tail(ctx, setup.tenant1):
            emit(f"  auth| {line}")
        raise OwnerStop([f"kiracı 1: hak eşitlemesinden sonra {SYNC_WAIT_SECONDS} sn içinde adanmış rol yok: "
                         f"{', '.join(missing)}"])
    emit("kiracı 1: adanmış üç rol var (" + ", ".join(DEDICATED_ROLES.values()) + ")")
    for label, role in DEDICATED_ROLES.items():
        assign_role(admin_token, 1, label, setup.t1[label], roles, role)
    del admin_token


def login(user: User) -> str:
    status, raw = http("POST", "http://localhost:5000/api/tenant-auth/login", headers={"X-Tenant-Id": user.tenant},
                       body={"email": user.email, "password": user.password, "rememberMe": False})
    token = field_ci(data_of(raw), "accessToken") if status == 200 else None
    if not isinstance(token, str) or not token:
        raise OwnerStop([f"kullanıcı girişi jeton vermedi (durum {status}); kurulumdaki kullanıcı yolu doğrulanmalı"])
    return token


def read_contract(token: str) -> tuple[int, bool]:
    status, raw = http("GET", "http://localhost:5000/api/product-master/stockable-items",
                       headers={"Authorization": f"Bearer {token}"})
    return status, SCOPE_FORBIDDEN in raw.decode("utf-8", errors="replace")


def measure_user(step: str, label: str, user: User, key_expected: bool, items_expected: bool | None) -> None:
    token = login(user)  # the user signs in AGAIN for every measurement: the key decision is in the new token
    perms = token_permissions(token)
    if items_expected is not None:
        check(step, (ITEMS_READ in perms) == items_expected, f"{label}: jetonda {ITEMS_READ} {'var' if items_expected else 'yok'}")
    check(step, (KEY in perms) == key_expected, f"{label}: jetonda {KEY} {'var' if key_expected else 'yok'}")
    status, scope_denied = read_contract(token)
    del token
    want = 200 if key_expected else 403
    check(step, status == want, f"{label}: GET /api/product-master/stockable-items → {status} (beklenen {want})")
    check(step, not scope_denied, f"{label}: gövdede {SCOPE_FORBIDDEN} yok (403 ise neden rol, kapsam değil)")


def entitlement_row(token: str, tenant: str) -> dict:
    status, raw = http("GET", f"http://localhost:5057/api/platform/tenants/{tenant}/commercial/module-entitlements",
                       headers={"Authorization": f"Bearer {token}"})
    rows = data_of(raw) if status == 200 else None
    for row in rows or []:
        if str(field_ci(row, "moduleCode") or "").lower() == MODULE and field_ci(row, "physicalEntitlementId"):
            return row
    raise Stop(1, f"E3 KIRMIZI — kiracı hak satırı okunamadı (durum {status})")


def add_entitlement(ctx: Ctx, token: str, tenant: str, step: str) -> dict:
    expiry = time.strftime("%Y-%m-%dT00:00:00Z", time.gmtime(time.time() + 180 * 86400))
    # EntitlementSource.Addon = 1 (Platform.Domain/Enums/EntitlementSource.cs; the enum carries JsonStringEnumConverter,
    # so "Addon" would be accepted too — E3-SETUP-PATH-MEASUREMENT §2)
    status, raw = http("POST", f"http://localhost:5057/api/platform/tenants/{tenant}/commercial/module-entitlements",
                       headers={"Authorization": f"Bearer {token}"},
                       body={"moduleCode": MODULE, "source": 1, "isEnabled": True, "expiryDateUtc": expiry,
                             "reason": "MDM-FU06-DL1 E3 hak satırı", "rowVersion": None})
    if status >= 400 and "quota" in raw.decode("utf-8", errors="replace").lower():
        raise OwnerStop(["modules.max kotası yeni kiracıda geçmedi"])
    if not check(step, status in (200, 201), f"hak satırı POST → {status}"):
        raise Stop(1, "E3 KIRMIZI — hak satırı açılamadı")
    return entitlement_row(token, tenant)


def sync_count(ctx: Ctx, tenant: str) -> int:
    """entitlement.sync.applied lines for this tenant AND this module. Tenant creation also writes one for the tenant
    (TenantSubscriptionChangedV1 → Reconcile, no ModuleCode: RegisterTenantCommandHandler → TenantSubscriptionTransaction-
    Writer.WriteIntentsAsync); counting by tenant alone took that line for the module's Grant (E3 run 2)."""
    return log_count(ctx.owned, "auth", SYNC_APPLIED, tenant, MODULE)


def wait_sync_count(ctx: Ctx, tenant: str, above: int) -> int:
    deadline = time.time() + SYNC_WAIT_SECONDS
    count = sync_count(ctx, tenant)
    while time.time() < deadline and count <= above:
        time.sleep(3)
        count = sync_count(ctx, tenant)
    return count


# ── Y1–Y5 ──────────────────────────────────────────────────────────────────────────────────────────────────────────
def y_steps(ctx: Ctx) -> None:
    emit("== Y1 — yığın, taban MDM")
    start_mongod(ctx)
    start_service(ctx, "auth", ROOT)
    start_service(ctx, "platform", ROOT)
    start_service(ctx, "gateway", ROOT)
    start_service(ctx, "mdm", ctx.base_worktree)
    version, actions = platform_catalog(ctx)
    check("Y1", version == "1.3.0", f"Platform kataloğunda modül sürümü {version} (beklenen 1.3.0)")
    check("Y1", actions == 0, f"Platform kataloğunda GLOBAL_PRODUCTS/READ_PRODUCT_MASTER {actions} (beklenen 0)")

    emit("== Y2a — kiracı ve hak satırı")
    setup = kurulum(ctx)
    row1 = add_entitlement(ctx, platform_token(setup), setup.tenant1, "Y2a")
    base_count = wait_sync_count(ctx, setup.tenant1, 0)
    if not check("Y2a", base_count > 0, f"{SYNC_APPLIED} bu kiracı ve {MODULE} için görüldü ({SYNC_WAIT_SECONDS} sn); "
                                        f"sayaç tabanı {base_count}"):
        raise Stop(1, "E3 KIRMIZI — Y2a eşitleme görülmedi")
    adanmis_roller(ctx, setup)
    enforced = own_query(ctx, DB_MDM, "db.mdm_product_legal_entity_scope_rollout_states.countDocuments({Mode:{$in:[2,'Enforced']}})")
    emit(f"yayılım modu: Enforced kayıt sayısı {enforced}")
    if enforced:
        raise Stop(2, "KULE DURUR — yayılım Enforced; TrustedLegalEntityScopeCredential:Mdm:AllowedPairs çifti gerekir")

    emit("== Y2b — ön ölçüm (taban MDM)")
    for role in ("admin", "viewer"):
        measure_user("Y2b", f"kiracı1 {role}", setup.t1[role], key_expected=False, items_expected=True)
    row = auth_permission_row(ctx)
    check("Y2b", row is None, f"Auth kataloğunda {KEY} satırı yok")

    emit("== Y3a — MDM yeni sürüm (bu dal)")
    if not stop_process(service_owned(ctx, "mdm")):
        raise Stop(2, "KULE DURUR — taban MDM durdurulamadı")
    start_service(ctx, "mdm", ROOT)
    version, actions = platform_catalog(ctx)
    check("Y3a", version == "1.4.0", f"Platform kataloğunda modül sürümü {version} (beklenen 1.4.0)")
    check("Y3a", actions == 1, f"Platform kataloğunda GLOBAL_PRODUCTS/READ_PRODUCT_MASTER {actions} (beklenen 1)")

    emit("== Y3b — henüz kazanç yok")
    row = auth_permission_row(ctx) or {}
    check("Y3b", str(row.get("Module", "")).lower() == MODULE, f"Auth kataloğunda Module = {row.get('Module')}")
    check("Y3b", row.get("Scope") in (0, "Tenant"), f"Auth kataloğunda Scope = {row.get('Scope')} (Tenant)")
    status, raw = http("GET", f"http://localhost:5057/api/internal/tenants/{setup.tenant1}/entitled-modules-with-permissions",
                       headers={"X-Internal-Api-Key": ctx.env_secrets["E3_INTERNAL_API_KEY"]})
    keys = next((field_ci(m, "permissionKeys") or [] for m in (data_of(raw) or [])
                 if str(field_ci(m, "moduleCode") or "").lower() == MODULE), [])
    check("Y3b", status == 200 and KEY in keys, f"Platform hak listesi (iç uç, durum {status}) modülde anahtarı listeler")
    for role in ("admin", "viewer"):
        measure_user("Y3b", f"kiracı1 {role}", setup.t1[role], key_expected=False, items_expected=None)
    count = sync_count(ctx, setup.tenant1)
    check("Y3b", count == base_count, f"{SYNC_APPLIED} sayacı {count} = Y2a tabanı {base_count}")

    emit("== Y4a — tetikleyici (PATCH uzlaştırma)")
    platform = platform_token(setup)  # 15-minute platform token: sign in again before the PATCH
    row1 = entitlement_row(platform, setup.tenant1)
    entitlement_id = field_ci(row1, "physicalEntitlementId")
    status, _ = http("PATCH", f"http://localhost:5057/api/platform/tenants/{setup.tenant1}/commercial/module-entitlements/"
                              f"{entitlement_id}/expiry",
                     headers={"Authorization": f"Bearer {platform}"},
                     body={"expiryDateUtc": field_ci(row1, "expiryDateUtc"), "reason": "MDM-FU06-DL1 E3 uzlaştırma",
                           "rowVersion": field_ci(row1, "rowVersion")})
    check("Y4a", status in (200, 204), f"PATCH expiry (aynı bitiş, yeni neden, güncel RowVersion) → {status}")
    after = wait_sync_count(ctx, setup.tenant1, base_count)
    check("Y4a", after > base_count, f"yeni {SYNC_APPLIED} ({SYNC_WAIT_SECONDS} sn): {base_count} → {after}")

    emit("== Y4b — kazanç")
    for role in ("admin", "viewer"):
        measure_user("Y4b", f"kiracı1 {role}", setup.t1[role], key_expected=True, items_expected=None)
    measure_user("Y4b", "kiracı1 anahtarsız kullanıcı", setup.t1["nokey"], key_expected=False, items_expected=None)
    for role in ("steward", "approver", "retirement"):  # F-9: the three dedicated roles get no key
        token = login(setup.t1[role])
        check("Y4b", KEY not in token_permissions(token), f"F-9 kiracı1 adanmış rol ({role}): jetonda {KEY} yok")
        del token

    emit("== Y5 — yeni kiracı, yeni hak")
    for role in ("admin", "viewer"):  # F-9: before the POST the tenant has no product module entitlement
        measure_user("Y5", f"F-9 kiracı2 {role} (hak yok)", setup.t2[role], key_expected=False, items_expected=False)
    t2_before = sync_count(ctx, setup.tenant2)
    add_entitlement(ctx, platform_token(setup), setup.tenant2, "Y5")  # fresh 15-minute platform token
    t2_after = wait_sync_count(ctx, setup.tenant2, t2_before)
    check("Y5", t2_after > t2_before, f"kiracı2 {SYNC_APPLIED} ({SYNC_WAIT_SECONDS} sn): {t2_before} → {t2_after}")
    for role in ("admin", "viewer"):
        measure_user("Y5", f"kiracı2 {role}", setup.t2[role], key_expected=True, items_expected=True)


# ── Y6: always ─────────────────────────────────────────────────────────────────────────────────────────────────────
def save_log_tails(ctx: Ctx, lines: int = 200) -> None:
    """Before the temp directory goes: the last lines of each Auth / Platform log, through the same filter as stdout."""
    if ctx.out_dir is None:
        emit("günlük kopyası yazılmadı (--out-dir verilmedi)")
        return
    for owned in ctx.owned:
        if owned.name not in ("auth", "platform") or not owned.log.exists():
            continue
        tail = owned.log.read_text(encoding="utf-8", errors="replace").splitlines()[-lines:]
        target = ctx.out_dir / f"{owned.log.stem}.tail{lines}.log"
        target.write_text("".join(scrub(line) + "\n" for line in tail), encoding="utf-8")
        emit(f"günlük kopyası (süzülmüş, son {len(tail)} satır): {target.relative_to(ROOT).as_posix()}")


def cleanup(ctx: Ctx) -> bool:
    emit("== Y6 — temizlik (her yolda)")
    ok = True
    errors = log_count(ctx.owned, "auth", EQUALITY_ERROR)
    if any(o.name == "auth" for o in ctx.owned):
        ok &= check("Y4c", errors == 0, f"Auth günlüğünde '{EQUALITY_ERROR}' sayısı {errors} (bütün koşu)")
    for owned in reversed(ctx.owned):
        ok &= stop_process(owned)
    save_log_tails(ctx)
    if ctx.vhost_created:
        ok &= check("Y6", run(["rabbitmqctl", "-q", "delete_vhost", ctx.vhost], timeout=60)[0] == 0, f"vhost {ctx.vhost} silindi")
        _, out = run(["rabbitmqctl", "-q", "list_vhosts"], timeout=60)
        ok &= check("Y6", ctx.vhost not in out.split(), "vhost listede yok")
    if ctx.base_verified and ctx.base_worktree is not None:
        code, _ = run(["git", "-C", str(ROOT), "worktree", "remove", str(ctx.base_worktree)], timeout=120)
        ok &= check("Y6", code == 0, f"taban worktree kaldırıldı (git worktree remove, çıkış {code})")
    busy = [p for p in SERVICE_PORTS if listeners(p)] + ([ctx.mongo_port] if listeners(ctx.mongo_port) else [])
    ok &= check("Y6", not busy, f"port listesi boş ({', '.join(map(str, busy)) or 'dinleyen yok'})")
    after = shared_databases()
    names_before = sorted(n for n, _ in (ctx.shared_dbs_before or []))
    names_after = sorted(n for n, _ in (after or []))
    if ctx.shared_dbs_before is not None or after is not None:
        emit("paylaşılan mongod (27017) veritabanları sonda (ad, boyut — boyut bilgi, karşılaştırılmaz): "
             + json.dumps(after, ensure_ascii=False))
    ok &= check("Y6", names_before == names_after, "F-15 paylaşılan mongod'un veritabanı ADLARI başlangıçtakiyle aynı")
    _, status = run(["git", "-C", str(ROOT), "status", "--porcelain", "--untracked-files=all"], timeout=60)
    stray = [l for l in status.splitlines() if l.strip() and not l[3:].startswith(EVIDENCE_PREFIX)]
    ok &= check("Y6", not stray, f"git status: değişiklik yalnız {EVIDENCE_PREFIX} altında ({len(stray)} başka satır)")
    for line in stray[:20]:
        emit(f"  başka satır: {line}")
    if os.environ.get("E3_KEEP_TEMP") == "1":
        emit(f"geçici dizin korundu (içerik yazılmaz): {ctx.tmp}")
    elif (ctx.tmp / ".e3fu06").exists():
        shutil.rmtree(ctx.tmp, ignore_errors=True)
        emit("geçici dizin silindi (günlükler, mongod verisi)")
    return ok


def main() -> int:
    ap = argparse.ArgumentParser(prog="mdm_fu06_dl1_e3.py", description="MDM-FU06-DL1 E3 kanıt betiği (Y1–Y6)")
    ap.add_argument("--base-worktree", required=True)
    ap.add_argument("--mongo-port", type=int, default=37291)
    ap.add_argument("--out-dir", default=None)
    args = ap.parse_args()

    out_dir = None
    if args.out_dir is not None:
        raw_out = Path(args.out_dir)
        out_dir = (ROOT / raw_out).resolve()
        evidence = (ROOT / EVIDENCE_PREFIX).resolve()
        if (raw_out.is_absolute() or ".." in raw_out.parts or not str(out_dir).startswith(str(evidence) + os.sep)
                or not out_dir.is_dir() or (ROOT / raw_out).is_symlink()):
            emit(f"SONUÇ: kullanım — --out-dir {EVIDENCE_PREFIX} altında var olan bir dizin olmalı (run_record --out)")
            return 5

    missing = [v for v in SECRET_VARS if not os.environ.get(v)]
    if missing:
        emit("SONUÇ: kullanım — eksik ortam değişkeni: " + ", ".join(missing))
        return 5
    _SECRETS.extend(os.environ[v] for v in SECRET_VARS)

    tmp = Path(tempfile.mkdtemp(prefix="e3fu06-"))
    if str(tmp.resolve()).startswith(str(ROOT.resolve())):
        emit("SONUÇ: KULE DURUR — geçici dizin repo içinde")
        return 2
    (tmp / ".e3fu06").write_text("owned by mdm_fu06_dl1_e3.py\n")
    ctx = Ctx(tmp=tmp, mongo_port=args.mongo_port, env_secrets={v: os.environ[v] for v in SECRET_VARS}, out_dir=out_dir)

    def on_signal(signum, _frame):
        raise Stop(2, f"KULE DURUR — sinyal {signum}")

    signal.signal(signal.SIGTERM, on_signal)
    signal.signal(signal.SIGINT, on_signal)
    signal.signal(signal.SIGHUP, on_signal)

    code, verdict = 0, ""
    try:
        emit("MDM-FU06-DL1 E3 · betik " + str(Path(__file__).resolve().relative_to(ROOT)) + " · repo HEAD " + git("rev-parse", "HEAD"))
        preconditions(ctx, Path(args.base_worktree))
        y_steps(ctx)
    except Stop as stop:
        code, verdict = stop.code, stop.verdict
    except Exception as exc:  # noqa: BLE001 — any unexpected failure is a stop, never a pass
        code, verdict = 2, f"KULE DURUR — beklenmeyen hata: {type(exc).__name__}"
    finally:
        try:
            cleaned = cleanup(ctx)
        except Exception as exc:  # noqa: BLE001
            cleaned = False
            emit(f"temizlik hatası: {type(exc).__name__}")
    failed = sorted({s for s, ok, _ in RESULTS if not ok})
    if code == 0 and failed:
        code, verdict = 1, "E3 KIRMIZI — " + ", ".join(failed)
    elif failed and code in (2, 3, 4):
        verdict += " · ayrıca KALDI: " + ", ".join(failed)
    if code == 0 and not cleaned:
        code, verdict = 1, "E3 KIRMIZI — Y6 temizlik"
    if not cleaned and code not in (0, 1):
        emit("UYARI: temizlik tam değil — kule elle ölçer (PID listesi yukarıda)")
    emit("ÖZET: " + ", ".join(f"{s}={'GEÇTİ' if all(ok for t, ok, _ in RESULTS if t == s) else 'KALDI'}"
                             for s in dict.fromkeys(s for s, _, _ in RESULTS)))
    emit("SONUÇ: " + (verdict or "E3 YEŞİL"))
    return code


if __name__ == "__main__":
    sys.exit(main())
