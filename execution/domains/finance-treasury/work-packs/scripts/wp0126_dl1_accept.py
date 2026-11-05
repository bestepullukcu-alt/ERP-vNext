#!/usr/bin/env python3
"""WP-0126-DL1 kabul kanıtı betiği (kart S-83; Treasury Intake CT yazar, işçi değiştirmez).

Kullanım (WP worktree'sinin kökünden):
    python3 -I execution/domains/finance-treasury/work-packs/scripts/wp0126_dl1_accept.py \
        --base <Expected HEAD> --mongod <mongod yolu> [--out <dizin>]

Depoya hiçbir şey yazmaz; TRX ve günlükler --out dizinine (varsayılan: geçici dizin) gider.
Çıkış: 0 = bütün kontroller PASS · 1 = en az bir FAIL · 2 = betik çalışamadı (girdi hatası).

AC test adı kuralı (prompt "DOĞRULA"): DL-1 "Bitiş" AC'lerinin her biri için en az bir test adının içinde
`Mod0126Dl1_AC<nn>_` geçer (ör. `Mod0126Dl1_AC16_MissingIdempotencyKey_returns_400`). AC-18 mimari testiyle
(AuditTrailStandard) ölçülür. Etiketli bir test atlanırsa (NotExecuted) ya da hiç yoksa o AC FAIL'dir.
Servis yeni olduğu için test projeleri `services/Diten.FinanceTreasuryService/tests/*/*.csproj` ile bulunur;
hiç proje yoksa FAIL.
"""
import argparse
import fnmatch
import glob
import hashlib
import os
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

BRANCH = "feature/fin/wp-0126-dl1"
PACK = "execution/domains/finance-treasury/module-packs/MOD-0126-payments-approvals.md"
PACK_SHA256 = "e8e5ce47332273ef6bd0d532ce51e52c533ea6762ad065f6687274ba8c8d1288"
AC_TAGS = ["01", "02", "03", "16", "17", "20", "21", "22", "23", "29", "32", "33", "39", "41", "45", "47"]

S = "services/Diten.FinanceTreasuryService/"
ALLOWED = [
    S + "*",
    "tests/architecture/audit-ledger/Diten.FinanceTreasuryService.md",
    "tests/architecture/audit-ledger/CT-DECISIONS.md",
    "tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs",
    "docs/analysis/contracts/payment-batch.openapi.yaml",
]
# DL-2 / DL-3 / DL-4 yolları: servis içinde olsa da bu dilimde yazılmaz (Repo Scope, prompt YAPMA).
FORBIDDEN = [
    S + "src/Diten.FinanceTreasuryService.Infrastructure/Workflow/*",
    S + "src/Diten.FinanceTreasuryService.Infrastructure/Evidence/*",
    S + "src/Diten.FinanceTreasuryService.Infrastructure/BankDispatch/*",
    S + "src/Diten.FinanceTreasuryService.Infrastructure/SodWaivers/*",
    S + "src/Diten.FinanceTreasuryService.Infrastructure/ApproverCandidates/*",
    S + "src/Diten.FinanceTreasuryService.Application/Common/ApproverCandidates/*",
]
# Yalnız ekleme: bu dosyalarda silinen ya da değişen satır olmaz.
APPEND_ONLY = [
    "tests/architecture/audit-ledger/CT-DECISIONS.md",
]
API = S + "src/Diten.FinanceTreasuryService.Api/Diten.FinanceTreasuryService.Api.csproj"
BUILDS = [
    ["dotnet", "build", API, "-c", "Release"],
    ["dotnet", "build", API],
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
    out = a.out or tempfile.mkdtemp(prefix="wp0126-dl1-accept-")
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
    forbidden = [p for p in changed if any(fnmatch.fnmatch(p, g) for g in FORBIDDEN)]
    record("DL-2…DL-4 yollarına dokunulmadı", not forbidden, ", ".join(forbidden[:10]))
    removed = []
    for p in APPEND_ONLY:
        d = git("diff", "--unified=0", a.base, "HEAD", "--", p).stdout
        if any(l.startswith("-") and not l.startswith("---") for l in d.splitlines()):
            removed.append(p)
    record("yalnız ekleme dosyalarında silinen satır yok", not removed, ", ".join(removed))
    ledger = git("diff", a.base, "HEAD", "--", "tests/architecture/audit-ledger/").stdout
    debt = [l for l in ledger.splitlines() if re.match(r"^\+- ", l)]
    record("denetim defterine borç satırı eklenmedi (D35/2)", not debt, str(len(debt)) + " satır" if debt else "")
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
    projects = sorted(glob.glob(S + "tests/*/*.csproj"))
    record("servis test projeleri var", bool(projects), ", ".join(os.path.basename(p) for p in projects) or "yok")
    for proj in projects:
        name = os.path.splitext(os.path.basename(proj))[0]
        trx = name + ".trx"
        code = run(["dotnet", "test", proj, "--logger", "trx;LogFileName=" + trx, "--results-directory", out],
                   os.path.join(out, name + ".log"), env)
        res = trx_outcomes(os.path.join(out, trx))
        failed = [n for n, o in res if o == "Failed"]
        record("test " + name, code == 0 and bool(res) and not failed,
               "çıkış %d · %d sonuç · %d kırmızı" % (code, len(res), len(failed)))
        for n, o in res:
            for t in AC_TAGS:
                if "Mod0126Dl1_AC" + t + "_" in n:
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
    record("AC-18 AuditTrailStandard", code == 0 and bool(res) and all(o == "Passed" for _, o in res),
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
