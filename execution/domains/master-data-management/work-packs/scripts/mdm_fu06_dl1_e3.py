#!/usr/bin/env python3
"""
MDM-FU06-DL1 — E3 kanıt betiği (MOD-0290-FU06 §18 "Yerel yığın (E3): yükseltme yolu", Y1–Y6).

Kule yazar (S-83); işçi DEĞİŞTİRMEZ, yalnız koşar. Tek koşu, run_record altında, temiz ağaçta:

    python3 scripts/run_record.py run --out docs/records/audits/2026-11/mod0290-fu06-dl1/e3-run -- \
        python3 execution/domains/master-data-management/work-packs/scripts/mdm_fu06_dl1_e3.py \
        --base-worktree <repo dışı mutlak yol>

--base-worktree   `git worktree add --detach <yol> $(git merge-base origin/main HEAD)` ile açılmış, repo DIŞINDA bir
                  worktree. Betik HEAD'ini, temizliğini ve taban koşulunu ölçer; orada yalnız MDM Api projesini derler;
                  Y6'da `git worktree remove` ile kaldırır (yalnız doğrulanmışsa).
--mongo-port      atılabilir mongod portu (varsayılan 37291; 27017–27021 yasak).

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
    3  KULE DURUR, SAHİBE SORAR — ölçülmemiş kurulum yolu (kiracı, kullanıcı ve rol verme, modules.max, platform
       yöneticisi jetonu); betik o noktada durur
    4  E3 BEKLER — 1 dakikalık yük 12'den büyük
    5  kullanım ya da ortam değişkeni eksik
Her yolda (hata, sinyal, erken çıkış) Y6 temizliği koşar: yalnız betiğin başlattığı süreçler, kayıtlı PID + başlangıç
zamanı eşleşirse durdurulur; ada göre süreç öldürülmez.

Ölçüm sınırı: servis ayar adları, uç yolları, günlük satırları ve koleksiyon adları kod okumasıdır (Expected HEAD
13ca54a01); betik kule tarafından koşulmadan önce derlenmedi, çalıştırılmadı.
"""
from __future__ import annotations

import argparse
import base64
import json
import os
import re
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import urllib.error
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


def emit(line: str) -> None:
    for secret in _SECRETS:
        if secret:
            line = line.replace(secret, "[SIR]")
    line = JWT_LIKE.sub("[JETON]", line)
    if "authorization" in line.lower():
        line = "[Authorization içeren satır yazılmadı]"
    print(line, flush=True)


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
    load = load_1min()
    emit(f"yük ({where}): 1 dakikalık {load:.2f} (eşik {LOAD_LIMIT:.0f})")
    if load > LOAD_LIMIT:
        raise Stop(4, f"E3 BEKLER — 1 dakikalık yük {load:.2f} > {LOAD_LIMIT:.0f} ({where})")


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

    # builds: this branch's four projects, then the base MDM in its worktree (load gate before each)
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


def service_env(ctx: Ctx, name: str) -> dict:
    env = {k: v for k, v in os.environ.items() if not k.startswith("E3_") and k != "ASPNETCORE_URLS"}
    s = ctx.env_secrets
    uri = f"mongodb://127.0.0.1:{ctx.mongo_port}/?replicaSet={RS_NAME}&directConnection=true"
    env.update({"ASPNETCORE_ENVIRONMENT": "Development", "DOTNET_ENVIRONMENT": "Development",
                "JwtSettings__Secret": s["E3_JWT_SECRET"]})
    eventing = {"Eventing__Transport": "RabbitMQ", "Eventing__VirtualHost": ctx.vhost}
    if name == "auth":
        env.update({"MongoDbSettings__ConnectionString": uri, "MongoDbSettings__DatabaseName": DB_AUTH,
                    "PlatformService__InternalApiKey": s["E3_INTERNAL_API_KEY"],
                    "InternalEventAuth__ApiKey": s["E3_INTERNAL_API_KEY"], **eventing})
    elif name == "platform":
        env.update({"MongoDbSettings__ConnectionString": uri, "MongoDbSettings__DatabaseName": DB_PLATFORM,
                    "BackgroundJobs__StorageDatabaseName": DB_PLATFORM_JOBS,
                    "AuthService__InternalApiKey": s["E3_INTERNAL_API_KEY"],
                    "ModuleRegistrationCredentials__Mdm__Identifier": s["E3_MDM_REGISTRATION_ID"],
                    "ModuleRegistrationCredentials__Mdm__ActiveSecret": s["E3_MDM_REGISTRATION_SECRET"],
                    "Eventing__WorkerEnabled": "true", **eventing})
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


# ── the unmeasured setup: the tower stops and asks the owner ────────────────────────────────────────────────────────
@dataclass
class User:
    email: str
    password: str


@dataclass
class Setup:
    platform_admin_token: str
    tenant1: str
    tenant2: str
    t1: dict          # role -> User: "admin", "viewer", "nokey", "steward", "approver", "retirement"
    t2: dict          # role -> User: "admin", "viewer"


def kurulum(ctx: Ctx) -> Setup:
    """Paket §18 "Ölçülemeyen": bu yollar ölçülmedi; uydurulmaz. Kule ölçüp bu işlevi yazana kadar betik burada durur."""
    raise OwnerStop([
        "platform yöneticisi jetonu (FirstPlatformAdministrator aracının girdileri ölçüldü: --email, --frontend-base-url, "
        "--link-file + DITEN_AUTH_MONGO_*, DITEN_PLATFORM_MONGO_*, JwtSettings__Secret; araç parola değil tek kullanımlık "
        "bağlantı yazar — bağlantıdan jetona yol ölçülmedi)",
        "kiracı oluşturma yolu (iki kiracı)",
        "kullanıcı oluşturma ve rol verme yolu (kiracı 1: Admin, Viewer, anahtarsız kullanıcı, ProductDataSteward, "
        "ProductIdentityApprover, ProductIdentityRetirementSteward; kiracı 2: Admin, Viewer)",
        "modules.max kotasının yeni kiracıda geçip geçmediği (AddTenantModuleEntitlementCommandHandler → "
        "IQuotaService.TryConsumeEntitlementAsync)",
    ])


def login(user: User) -> str:
    status, raw = http("POST", "http://localhost:5000/api/tenant-auth/login",
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
    # EntitlementSource.Addon = 1 (Platform.Domain/Enums/EntitlementSource.cs; Platform.API has no string enum converter)
    status, raw = http("POST", f"http://localhost:5057/api/platform/tenants/{tenant}/commercial/module-entitlements",
                       headers={"Authorization": f"Bearer {token}"},
                       body={"moduleCode": MODULE, "source": 1, "isEnabled": True, "expiryDateUtc": expiry,
                             "reason": "MDM-FU06-DL1 E3 hak satırı", "rowVersion": None})
    if status >= 400 and "quota" in raw.decode("utf-8", errors="replace").lower():
        raise OwnerStop(["modules.max kotası yeni kiracıda geçmedi"])
    if not check(step, status in (200, 201), f"hak satırı POST → {status}"):
        raise Stop(1, "E3 KIRMIZI — hak satırı açılamadı")
    return entitlement_row(token, tenant)


def wait_sync_count(ctx: Ctx, tenant: str, above: int) -> int:
    deadline = time.time() + SYNC_WAIT_SECONDS
    count = log_count(ctx.owned, "auth", SYNC_APPLIED, tenant)
    while time.time() < deadline and count <= above:
        time.sleep(3)
        count = log_count(ctx.owned, "auth", SYNC_APPLIED, tenant)
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
    row1 = add_entitlement(ctx, setup.platform_admin_token, setup.tenant1, "Y2a")
    base_count = wait_sync_count(ctx, setup.tenant1, 0)
    if not check("Y2a", base_count > 0, f"{SYNC_APPLIED} bu kiracı için görüldü ({SYNC_WAIT_SECONDS} sn); sayaç tabanı {base_count}"):
        raise Stop(1, "E3 KIRMIZI — Y2a eşitleme görülmedi")
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
    count = log_count(ctx.owned, "auth", SYNC_APPLIED, setup.tenant1)
    check("Y3b", count == base_count, f"{SYNC_APPLIED} sayacı {count} = Y2a tabanı {base_count}")

    emit("== Y4a — tetikleyici (PATCH uzlaştırma)")
    row1 = entitlement_row(setup.platform_admin_token, setup.tenant1)
    entitlement_id = field_ci(row1, "physicalEntitlementId")
    status, _ = http("PATCH", f"http://localhost:5057/api/platform/tenants/{setup.tenant1}/commercial/module-entitlements/"
                              f"{entitlement_id}/expiry",
                     headers={"Authorization": f"Bearer {setup.platform_admin_token}"},
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
    t2_before = log_count(ctx.owned, "auth", SYNC_APPLIED, setup.tenant2)
    add_entitlement(ctx, setup.platform_admin_token, setup.tenant2, "Y5")
    t2_after = wait_sync_count(ctx, setup.tenant2, t2_before)
    check("Y5", t2_after > t2_before, f"kiracı2 {SYNC_APPLIED} ({SYNC_WAIT_SECONDS} sn): {t2_before} → {t2_after}")
    for role in ("admin", "viewer"):
        measure_user("Y5", f"kiracı2 {role}", setup.t2[role], key_expected=True, items_expected=True)


# ── Y6: always ─────────────────────────────────────────────────────────────────────────────────────────────────────
def cleanup(ctx: Ctx) -> bool:
    emit("== Y6 — temizlik (her yolda)")
    ok = True
    errors = log_count(ctx.owned, "auth", EQUALITY_ERROR)
    if any(o.name == "auth" for o in ctx.owned):
        ok &= check("Y4c", errors == 0, f"Auth günlüğünde '{EQUALITY_ERROR}' sayısı {errors} (bütün koşu)")
    for owned in reversed(ctx.owned):
        ok &= stop_process(owned)
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
    args = ap.parse_args()

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
    ctx = Ctx(tmp=tmp, mongo_port=args.mongo_port, env_secrets={v: os.environ[v] for v in SECRET_VARS})

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
