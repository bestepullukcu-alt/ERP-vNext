#!/usr/bin/env python3
"""WP-0289-DL1 kabul kanıtı betiği (kart S-83; Treasury Intake CT yazar, işçi değiştirmez).

Kullanım (WP worktree'sinin kökünden):
    python3 -I execution/domains/master-data-management/work-packs/scripts/wp0289_dl1_accept.py \
        --base <Expected HEAD> --mongod <mongod yolu> [--out <dizin>]

Depoya hiçbir şey yazmaz; TRX ve günlükler --out dizinine (varsayılan: geçici dizin) gider.
Çıkış: 0 = bütün kontroller PASS · 1 = en az bir FAIL · 2 = betik çalışamadı (girdi hatası).

AC test adı kuralı (prompt "DOĞRULA"): DL-1 "Bitiş" AC'lerinin her biri için en az bir test adının içinde
`Mod0289Dl1_AC<nn>_` geçer (ör. `Mod0289Dl1_AC02_DuplicateBic_returns_409`). AC-07 mimari testiyle (AuditTrailStandard)
ölçülür. Etiketli bir test atlanırsa (NotExecuted) ya da hiç yoksa o AC FAIL'dir.
"""
import argparse
import fnmatch
import hashlib
import os
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

BRANCH = "feature/mdm/wp-0289-dl1"
PACK = "execution/domains/master-data-management/module-packs/MOD-0289-bank-master-bank-account-registry.md"
PACK_SHA256 = "fafef5c408e0e02687f60af4e7857520e66c66fc534f6dc822d3e4fbb7eb5c1b"
AC_TAGS = ["01", "02", "03", "04", "05", "06", "08", "09", "27", "59", "60"]

M = "services/Diten.MdmService/src/Diten.MdmService."
ALLOWED = [
    M + "Domain/Entities/Bank.cs",
    M + "Domain/Entities/BankBranch.cs",
    M + "Domain/Enums/BankLifecycleStatus.cs",
    M + "Domain/Enums/MasterDataAuditOperation.cs",
    M + "Domain/Enums/AuditAggregateType.cs",
    M + "Domain/Repositories/IBankRepository.cs",
    M + "Domain/Repositories/IBankBranchRepository.cs",
    M + "Application/Features/BankMaster/*",
    M + "Application/Contracts/RecordCodes/IRecordCodeAllocator.cs",
    M + "Persistence/Repositories/BankRepository.cs",
    M + "Persistence/Repositories/BankBranchRepository.cs",
    M + "Persistence/Repositories/AuditIntentDeliveryRepository.cs",
    M + "Persistence/DependencyInjection.cs",
    M + "Infrastructure/RecordCodes/*",
    M + "Infrastructure/DependencyInjection.cs",
    M + "Api/Controllers/BanksController.cs",
    M + "Api/Controllers/BankBranchesController.cs",
    M + "Api/ModuleRegistration/BankMasterManifestProvider.cs",
    M + "Api/Program.cs",
    "services/Diten.MdmService/tests/*",
    "tests/architecture/audit-ledger/Diten.MdmService.md",
    "tests/architecture/audit-ledger/CT-DECISIONS.md",
    "tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs",
]
# Yalnız ekleme: bu dosyalarda silinen ya da değişen satır olmaz.
APPEND_ONLY = [
    M + "Api/Program.cs",
    M + "Persistence/DependencyInjection.cs",
    M + "Infrastructure/DependencyInjection.cs",
    M + "Domain/Enums/AuditAggregateType.cs",
    M + "Persistence/Repositories/AuditIntentDeliveryRepository.cs",
    "tests/architecture/audit-ledger/CT-DECISIONS.md",
]
BUILDS = [
    ["dotnet", "build", "services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj", "-c", "Release"],
    ["dotnet", "build", "services/Diten.MdmService/src/Diten.MdmService.Api/Diten.MdmService.Api.csproj", "-c", "Release"],
    ["dotnet", "build", "services/Diten.MdmService/src/Diten.MdmService.Api/Diten.MdmService.Api.csproj"],
]
TEST_PROJECTS = [
    ("mdm-app", "services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Diten.MdmService.Application.Tests.csproj"),
    ("mdm-api", "services/Diten.MdmService/tests/Diten.MdmService.Api.Tests/Diten.MdmService.Api.Tests.csproj"),
]
ARCH = ("arch-audit", "tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj",
        "FullyQualifiedName~AuditTrailStandard")

results = []


def record(name, ok, detail=""):
    results.append((name, ok, detail))
    print(("PASS " if ok else "FAIL ") + name + (" — " + detail if detail else ""), flush=True)


def git(*args):
    return subprocess.run(["git", *args], capture_output=True, text=True)


def run(cmd, log, env):
    with open(log, "w", encoding="utf-8") as f:
        return subprocess.run(cmd, stdout=f, stderr=subprocess.STDOUT, env=env).returncode


def trx_outcomes(path):
    out = []
    if not os.path.exists(path):
        return out
    ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    for r in ET.parse(path).getroot().iterfind(".//t:UnitTestResult", ns):
        out.append((r.get("testName", ""), r.get("outcome", "")))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", required=True, help="prompt'taki Expected HEAD")
    ap.add_argument("--mongod", required=True, help="DITEN_TEST_MONGOD olarak verilecek mongod yolu (CI: 7.0.28)")
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    out = a.out or tempfile.mkdtemp(prefix="wp0289-dl1-accept-")
    os.makedirs(out, exist_ok=True)
    if git("rev-parse", "--verify", a.base + "^{commit}").returncode != 0:
        print("girdi hatası: --base bir commit değil", file=sys.stderr)
        return 2
    head = git("rev-parse", "HEAD").stdout.strip()
    print("HEAD " + head + " · base " + a.base + " · çıktı " + out, flush=True)

    branch = git("branch", "--show-current").stdout.strip()
    record("dal", branch == BRANCH, branch or "(ayrık HEAD)")
    record("taban HEAD'in atası", git("merge-base", "--is-ancestor", a.base, "HEAD").returncode == 0)
    dirty = git("status", "--porcelain", "--untracked-files=all").stdout.strip()
    record("worktree temiz", not dirty, (str(len(dirty.splitlines())) + " satır") if dirty else "")
    pack = git("show", "HEAD:" + PACK)
    sha = hashlib.sha256(pack.stdout.encode("utf-8")).hexdigest() if pack.returncode == 0 else ""
    record("paket değişmedi (onaylı sürüm)", sha == PACK_SHA256, sha[:16])

    changed = [p for p in git("diff", "--name-only", a.base, "HEAD").stdout.splitlines() if p]
    outside = [p for p in changed if not any(fnmatch.fnmatch(p, g) for g in ALLOWED)]
    record("değişen dosyalar yalnız İzinli yollar", bool(changed) and not outside,
           ("değişiklik yok" if not changed else ", ".join(outside[:10])))
    removed = []
    for p in APPEND_ONLY:
        d = git("diff", "--unified=0", a.base, "HEAD", "--", p).stdout
        if any(l.startswith("-") and not l.startswith("---") for l in d.splitlines()):
            removed.append(p)
    record("yalnız ekleme dosyalarında silinen satır yok", not removed, ", ".join(removed))
    ledger = git("diff", a.base, "HEAD", "--", "tests/architecture/audit-ledger/").stdout
    debt = [l for l in ledger.splitlines() if re.match(r"^\+- ", l)]
    record("denetim defterine borç satırı eklenmedi (AC-7)", not debt, str(len(debt)) + " satır" if debt else "")
    record("git diff --check temiz", git("diff", "--check", a.base, "HEAD").returncode == 0)

    mongod_ok = os.path.isabs(a.mongod) and os.path.isfile(a.mongod)
    record("mongod yolu mutlak ve var", mongod_ok, a.mongod)
    env = dict(os.environ)
    env["DITEN_TEST_MONGOD"] = a.mongod
    env.setdefault("DOTNET_ROOT", os.path.expanduser("~/.dotnet"))
    ver = subprocess.run([a.mongod, "--version"], capture_output=True, text=True) if mongod_ok else None
    if ver is not None:
        print("mongod: " + (ver.stdout.splitlines() or ["?"])[0], flush=True)

    for i, cmd in enumerate(BUILDS):
        code = run(cmd, os.path.join(out, "build-%d.log" % i), env)
        record("derleme " + " ".join(cmd[2:]), code == 0, "çıkış %d" % code)

    tags = {t: [] for t in AC_TAGS}
    for name, proj in TEST_PROJECTS:
        trx = name + ".trx"
        code = run(["dotnet", "test", proj, "--logger", "trx;LogFileName=" + trx, "--results-directory", out],
                   os.path.join(out, name + ".log"), env)
        res = trx_outcomes(os.path.join(out, trx))
        failed = [n for n, o in res if o == "Failed"]
        record("test " + name, code == 0 and bool(res) and not failed,
               "çıkış %d · %d sonuç · %d kırmızı" % (code, len(res), len(failed)))
        for n, o in res:
            for t in AC_TAGS:
                if "Mod0289Dl1_AC" + t + "_" in n:
                    tags[t].append((n, o))
    for t in AC_TAGS:
        got = tags[t]
        ok = bool(got) and all(o == "Passed" for _, o in got)
        record("AC-%s etiketli testler geçti" % t, ok,
               "%d test" % len(got) + ("" if ok or not got else " · " + ", ".join(n for n, o in got if o != "Passed")[:200]))

    name, proj, flt = ARCH
    code = run(["dotnet", "test", proj, "--filter", flt, "--logger", "trx;LogFileName=" + name + ".trx",
                "--results-directory", out], os.path.join(out, name + ".log"), env)
    res = trx_outcomes(os.path.join(out, name + ".trx"))
    record("AC-07 AuditTrailStandard", code == 0 and bool(res) and all(o == "Passed" for _, o in res),
           "çıkış %d · %d sonuç" % (code, len(res)))

    code = run([sys.executable, "-m", "unittest", "discover", "-s", "scripts/tests", "-p", "test_*.py"],
               os.path.join(out, "scripts-tests.log"), env)
    record("scripts/tests (yerel kapı)", code == 0, "çıkış %d" % code)

    fails = [n for n, ok, _ in results if not ok]
    print("SONUÇ: " + ("PASS" if not fails else "FAIL — %d kontrol" % len(fails)), flush=True)
    print("Sabotajlar bu betikte koşulmaz: her biri işçi raporunda kırmızı test adıyla verilir; kabulde CT yeniden koşar.")
    return 0 if not fails else 1


if __name__ == "__main__":
    sys.exit(main())
