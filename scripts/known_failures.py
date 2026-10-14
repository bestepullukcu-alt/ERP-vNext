#!/usr/bin/env python3
"""
Known failing tests (CT-OPS-01): compare a test run with tests/known-failures.tsv and say whether a repeated
LOCAL diagnosis is worth running again.

    python3 scripts/known_failures.py check <console-log-or-trx> [--suite NAME]
    python3 scripts/known_failures.py should-rerun --suite NAME
    python3 scripts/known_failures.py validate
    python3 scripts/known_failures.py --self-test

check         BİLİNEN  registered and still failing
              YENİ     failing but not registered          -> exit 1 (a new regression: stop and report)
              DÜZELDİ  registered but did not fail          -> update the registry row (do not delete it silently)
should-rerun  KOŞ önerisi / ATLA önerisi from each open row's rerun_trigger globs, compared with every path changed
              since that row's last_verified_commit (committed, uncommitted and untracked).
validate      every row names a test and commits that exist in this repository; (suite, test) is unique.

ATLA is ONLY a recommendation for repeating a LOCAL diagnosis. It never applies to CI, to required acceptance tests,
to security tests or to a test the owner explicitly asked for — those always run.

Globs use fnmatch: `*` also matches `/`, so `services/*` covers every depth. Standard library only; read-only.
"""
from __future__ import annotations

import argparse
import csv
import fnmatch
import io
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REGISTRY = ROOT / "tests" / "known-failures.tsv"
COLUMNS = ["suite", "test", "first_recorded_commit", "first_recorded_source", "last_verified_commit",
           "rerun_trigger", "reference", "status"]
SKIP_CAVEAT = ("ATLA yalnız yerel tekrar tanı içindir: CI, zorunlu kabul testleri, güvenlik testleri ve sahibin "
               "açıkça istediği testler her zaman koşulur.")
FAILED_LINE = re.compile(r"^\s*(?:Failed|Başarısız)\s+(\S.*?)(?:\s+\[[^\]]*\])?\s*$")
SUMMARY_LINE = re.compile(r"(?:Failed|Başarısız)!?\s*-\s*(?:Failed|Başarısız):\s*\d+", re.I)


def read_registry(text: str) -> list[dict]:
    rows = list(csv.DictReader(io.StringIO(text), delimiter="\t"))
    if rows and list(rows[0].keys()) != COLUMNS:
        raise SystemExit(f"registry header must be: {' | '.join(COLUMNS)}")
    return rows


def parse_run(text: str) -> tuple[set[str], set[str] | None]:
    """Return (failed test names, executed test names or None when the log does not list passing tests)."""
    if "<TestRun" in text[:2000]:
        root = ET.fromstring(text)
        failed, executed = set(), set()
        for el in root.iter():
            if el.tag.endswith("UnitTestResult"):
                name = el.get("testName", "").strip()
                executed.add(name)
                if el.get("outcome") == "Failed":
                    failed.add(name)
        return failed, executed
    failed = {m.group(1).strip() for line in text.splitlines() if (m := FAILED_LINE.match(line))}
    return failed, None


def classify(rows: list[dict], failed: set[str], executed: set[str] | None, suite: str | None) -> dict:
    known = {r["test"] for r in rows if r["status"] == "open" and (suite is None or r["suite"] == suite)}
    return {
        "BİLİNEN": sorted(failed & known),
        "YENİ": sorted(failed - known),
        "DÜZELDİ": sorted(t for t in known - failed if executed is None or t in executed),
        "KOŞMADI": sorted(t for t in known - failed if executed is not None and t not in executed),
    }


def decide(rows: list[dict], suite: str, changed_since: dict[str, list[str]]) -> tuple[str, list[str]]:
    """changed_since maps a row's last_verified_commit to the paths changed after it."""
    hits = []
    for r in rows:
        if r["suite"] != suite or r["status"] != "open":
            continue
        globs = [g.strip() for g in r["rerun_trigger"].split(",") if g.strip()]
        for path in changed_since.get(r["last_verified_commit"], []):
            if any(fnmatch.fnmatch(path, g) for g in globs):
                hits.append(f"{r['test']} <- {path}")
                break
    return ("KOŞ önerisi" if hits else "ATLA önerisi"), hits


def git(*args: str) -> subprocess.CompletedProcess:
    return subprocess.run(["git", "-C", str(ROOT), *args], capture_output=True, text=True)


def commit_exists(sha: str) -> bool:
    return bool(sha) and git("cat-file", "-e", f"{sha}^{{commit}}").returncode == 0


def changed_paths_since(sha: str) -> list[str]:
    committed = git("diff", "--name-only", f"{sha}..HEAD").stdout.split()
    pending = [line[3:].split(" -> ")[-1] for line in git("status", "--porcelain", "--untracked-files=all").stdout.splitlines()]
    return sorted(set(committed) | set(pending))


def validate(rows: list[dict], exists=commit_exists) -> list[str]:
    problems, seen = [], set()
    for i, r in enumerate(rows, start=2):
        if not r["test"].strip() or not r["suite"].strip():
            problems.append(f"line {i}: suite and test are required")
        for key in ("first_recorded_commit", "last_verified_commit"):
            if not exists(r[key]):
                problems.append(f"line {i}: {key} '{r[key]}' is not a commit in this repository")
        if not r["rerun_trigger"].strip():
            problems.append(f"line {i}: rerun_trigger is required (without it ATLA could never be justified)")
        if r["status"] not in ("open", "fixed"):
            problems.append(f"line {i}: status must be open or fixed")
        if (r["suite"], r["test"]) in seen:
            problems.append(f"line {i}: duplicate ({r['suite']}, {r['test']})")
        seen.add((r["suite"], r["test"]))
    return problems


def self_test() -> int:
    reg = "\t".join(COLUMNS) + "\n" + "\n".join([
        "S\tNs.A.Known1\taaa\tBL-1\tbbb\tservices/*,tests/*\tBL-1\topen",
        "S\tNs.A.Known2(scenario: \"x\")\taaa\tBL-1\tbbb\tdocs/*\tBL-1\topen",
    ]) + "\n"
    rows = read_registry(reg)
    console_known = "  Failed Ns.A.Known1 [1 ms]\n  Başarısız Ns.A.Known2(scenario: \"x\") [2 ms]\nFailed!  - Failed:     2, Passed: 9\n"
    console_new = console_known + "  Failed Ns.B.Brand_new_regression [3 ms]\n"
    trx_fixed = ('<?xml version="1.0"?><TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>'
                 '<UnitTestResult testName="Ns.A.Known1" outcome="Failed"/>'
                 '<UnitTestResult testName="Ns.A.Known2(scenario: &quot;x&quot;)" outcome="Passed"/></Results></TestRun>')
    cases = []
    c = classify(rows, *parse_run(console_known), "S")
    cases.append(("known only -> 2 BİLİNEN, 0 YENİ", c["BİLİNEN"] == ["Ns.A.Known1", 'Ns.A.Known2(scenario: "x")'] and not c["YENİ"]))
    c = classify(rows, *parse_run(console_new), "S")
    cases.append(("sabotage: unregistered failure -> YENİ names it", c["YENİ"] == ["Ns.B.Brand_new_regression"]))
    c = classify(rows, *parse_run(trx_fixed), "S")
    cases.append(("trx: registered test passed -> DÜZELDİ", c["DÜZELDİ"] == ['Ns.A.Known2(scenario: "x")'] and c["BİLİNEN"] == ["Ns.A.Known1"]))
    verdict, _ = decide(rows, "S", {"bbb": ["frontend/x.js", ".antigravity/rules/y.md"]})
    cases.append(("no trigger path changed -> ATLA önerisi", verdict == "ATLA önerisi"))
    verdict, hits = decide(rows, "S", {"bbb": ["services/Diten.X/Handler.cs"]})
    cases.append(("trigger path changed -> KOŞ önerisi", verdict == "KOŞ önerisi" and hits and "Ns.A.Known1" in hits[0]))
    problems = validate(rows, exists=lambda sha: sha != "aaa")
    cases.append(("sabotage: unknown commit -> validate fails", any("first_recorded_commit 'aaa'" in p for p in problems)))
    bad = reg.replace("services/*,tests/*", "")
    cases.append(("sabotage: empty trigger -> validate fails", any("rerun_trigger" in p for p in validate(read_registry(bad), exists=lambda s: True))))
    for name, ok in cases:
        print(f"{'PASS' if ok else 'FAIL'} {name}")
    failed = [n for n, ok in cases if not ok]
    print(f"self-test: {len(cases) - len(failed)}/{len(cases)} passed")
    return 1 if failed else 0


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if "--self-test" in sys.argv[1:]:
        return self_test()
    p = argparse.ArgumentParser(description="Known failing tests: check a run, recommend a local re-run, validate the registry.")
    sub = p.add_subparsers(dest="cmd", required=True)
    pc = sub.add_parser("check"); pc.add_argument("run_file"); pc.add_argument("--suite")
    pr = sub.add_parser("should-rerun"); pr.add_argument("--suite", required=True)
    sub.add_parser("validate")
    a = p.parse_args()
    rows = read_registry(REGISTRY.read_text(encoding="utf-8"))

    if a.cmd == "validate":
        problems = validate(rows)
        for line in problems:
            print(f"HATA {line}")
        print("OK registry" if not problems else f"FAIL {len(problems)} problem(s)")
        return 1 if problems else 0

    if a.cmd == "check":
        failed, executed = parse_run(Path(a.run_file).read_text(encoding="utf-8", errors="replace"))
        result = classify(rows, failed, executed, a.suite)
        for label in ("BİLİNEN", "YENİ", "DÜZELDİ", "KOŞMADI"):
            for name in result[label]:
                print(f"{label}\t{name}")
        print(f"ÖZET bilinen={len(result['BİLİNEN'])} yeni={len(result['YENİ'])} düzeldi={len(result['DÜZELDİ'])} koşmadı={len(result['KOŞMADI'])}")
        if result["DÜZELDİ"]:
            print("NOT: DÜZELDİ satırları için kaydı güncelleyin (status=fixed ve last_verified_commit); sessizce silmeyin.")
        if result["YENİ"]:
            print("DUR: kayıtta olmayan yeni kırmızı var — regresyon olarak bildirin.")
        return 1 if result["YENİ"] else 0

    if a.cmd == "should-rerun":
        commits = {r["last_verified_commit"] for r in rows if r["suite"] == a.suite and r["status"] == "open"}
        if not commits:
            print(f"KOŞ önerisi: '{a.suite}' için kayıtlı açık satır yok")
            return 0
        verdict, hits = decide(rows, a.suite, {c: changed_paths_since(c) for c in commits})
        print(verdict)
        for h in hits[:10]:
            print(f"  tetikleyici: {h}")
        print(SKIP_CAVEAT)
        return 0
    return 2


if __name__ == "__main__":
    sys.exit(main())
