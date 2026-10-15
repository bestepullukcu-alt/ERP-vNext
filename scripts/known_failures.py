#!/usr/bin/env python3
"""
Known failing tests (CT-OPS-01, fail-closed since the 2026-10-15 audit): compare a test run with
tests/known-failures.tsv and say whether a repeated LOCAL diagnosis is worth running again.

    python3 scripts/known_failures.py check <trx-or-console-log> [--suite NAME]
    python3 scripts/known_failures.py fingerprint <trx> [--suite NAME]
    python3 scripts/known_failures.py should-rerun --suite NAME
    python3 scripts/known_failures.py validate
    python3 scripts/known_failures.py --self-test

check         exit 0  only registered failures, each with the registered failure fingerprint
              exit 1  YENİ (failing, not registered) or YENİ-AYRINTI (registered, but its failure detail changed —
                      e.g. one more violation inside an already red test): a regression, stop and report
              exit 2  the run cannot be trusted: empty, unreadable or unknown format, no results, aborted, or the
                      console summary does not match the failures found
              exit 3  console log: names compared, failure details NOT verified — not enough for acceptance
              Acceptance uses a TRX (`dotnet test … --logger "trx;LogFileName=<file>"`).
              Outcomes other than Passed / NotExecuted (Error, Timeout, Aborted, …) count as failed.
fingerprint   prints the fingerprint of every failed test in a TRX (to record or update a registry row).
should-rerun  per registered test: KOŞ önerisi / ATLA önerisi, from its rerun_trigger globs and every path changed since
              its last_verified_commit. ATLA covers ONLY the named registered tests; the rest of the suite (security
              guards included) still runs. Any git error → KOŞ önerisi and exit 2.
validate      every row names a test, commits that exist, a non-empty fingerprint for open rows, triggers that match at
              least one tracked path; (suite, test) is unique.

ATLA is ONLY a recommendation for repeating a LOCAL diagnosis. It never applies to CI, to required acceptance tests,
to security tests or to a test the owner explicitly asked for — those always run.

Fingerprint: for a failure message that lists items ("- …" lines), the sorted item set (count + sha256); otherwise the
whitespace-normalised message (sha256). Globs use fnmatch: `*` also matches `/`. Standard library only. Read-only:
git runs only the commands in READ_ONLY_GIT, with GIT_OPTIONAL_LOCKS=0.
"""
from __future__ import annotations

import argparse
import ast
import csv
import fnmatch
import hashlib
import io
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REGISTRY = ROOT / "tests" / "known-failures.tsv"
COLUMNS = ["suite", "test", "first_recorded_commit", "first_recorded_source", "last_verified_commit",
           "rerun_trigger", "reference", "status", "failure_fingerprint"]
READ_ONLY_GIT = {"cat-file", "diff", "ls-files", "rev-parse", "status"}
SKIP_CAVEAT = ("ATLA yalnız adı verilen kayıtlı testlerin yerel tekrar tanısı içindir: süitin geri kalanı (güvenlik "
               "bekçileri dahil), CI, zorunlu kabul testleri ve sahibin açıkça istediği testler her zaman koşulur.")
ANSI = re.compile(r"\x1b\[[0-9;]*[A-Za-z]")
FAILED_LINE = re.compile(r"^\s*(?:failed|başarısız)\s+(\S.*?)(?:\s+\[[^\]]*\])?\s*$", re.I)
SUMMARY_LINE = re.compile(r"(?:failed|passed|başarısız|başarılı)!\s*-\s*(?:failed|başarısız):\s*(\d+),\s*"
                          r"(?:passed|başarılı):\s*(\d+),\s*(?:skipped|atlanan):\s*(\d+),\s*(?:total|toplam):\s*(\d+)", re.I)
NOT_FAILED = {"Passed", "NotExecuted"}
EXIT_OK, EXIT_REGRESSION, EXIT_UNTRUSTED, EXIT_NAMES_ONLY = 0, 1, 2, 3


class Untrusted(Exception):
    """The run file cannot be trusted as evidence (fail-closed)."""


def read_registry(text: str) -> list[dict]:
    rows = list(csv.DictReader(io.StringIO(text), delimiter="\t"))
    header = next(csv.reader(io.StringIO(text), delimiter="\t"), [])
    if header != COLUMNS:
        raise SystemExit(f"registry header must be: {' | '.join(COLUMNS)}")
    return rows


def normalise(text: str) -> str:
    return re.sub(r"[ \t]+", " ", ANSI.sub("", text).replace("\r\n", "\n")).strip()


def fingerprint(message: str) -> str:
    text = normalise(message)
    items = sorted({re.sub(r"\s+", " ", m.group(1)).strip() for m in re.finditer(r"(?m)^\s*-\s+(.+)$", text)})
    if items:
        return f"items:{len(items)}:{hashlib.sha256(chr(10).join(items).encode()).hexdigest()[:16]}"
    flat = re.sub(r"\s+", " ", text)
    return "msg:" + hashlib.sha256(flat.encode()).hexdigest()[:16]


def parse_trx(text: str) -> dict:
    try:
        root = ET.fromstring(text)
    except ET.ParseError as e:
        raise Untrusted(f"TRX okunamadı: {e}")
    results, outcome = {}, None
    for el in root.iter():
        tag = el.tag.rsplit("}", 1)[-1]
        if tag == "UnitTestResult":
            message = "".join(t.text or "" for t in el.iter() if t.tag.rsplit("}", 1)[-1] == "Message")
            results[el.get("testName", "").strip()] = (el.get("outcome", ""), message)
        elif tag == "ResultSummary":
            outcome = el.get("outcome")
    if not results:
        raise Untrusted("TRX'te hiç test sonucu yok (koşu çökmüş ya da hiç başlamamış olabilir)")
    if outcome in ("Aborted", "Error", "Timeout"):
        raise Untrusted(f"TRX koşu sonucu '{outcome}': koşu tamamlanmadı")
    return {"format": "trx", "results": results}


def parse_console(text: str) -> dict:
    clean = ANSI.sub("", text)
    summaries = [m for m in SUMMARY_LINE.finditer(clean)]
    if not summaries:
        raise Untrusted("konsol çıktısında özet satırı yok ('Failed! - Failed: n, Passed: …'); biçim tanınmadı ya da koşu bitmedi")
    expected = sum(int(m.group(1)) for m in summaries)
    failed = {m.group(1).strip() for line in clean.splitlines() if (m := FAILED_LINE.match(line))}
    if len(failed) != expected:
        raise Untrusted(f"özet {expected} başarısız diyor, çıktıda {len(failed)} test adı bulundu")
    return {"format": "console", "failed": failed}


def parse_run(text: str) -> dict:
    if not text.strip():
        raise Untrusted("koşu dosyası boş")
    head = text.lstrip("﻿")[:4000]
    if "<TestRun" in head:
        return parse_trx(text)
    return parse_console(text)


def classify(rows: list[dict], run: dict, suite: str | None) -> dict:
    registered = {r["test"]: r for r in rows if r["status"] == "open" and (suite is None or r["suite"] == suite)}
    out = {"BİLİNEN": [], "YENİ": [], "YENİ-AYRINTI": [], "DÜZELDİ": [], "KOŞMADI": [], "BİLİNMİYOR": []}
    if run["format"] == "console":
        failed = run["failed"]
        out["BİLİNEN"] = sorted(failed & set(registered))
        out["YENİ"] = sorted(failed - set(registered))
        out["BİLİNMİYOR"] = sorted(set(registered) - failed)   # a console log cannot prove a test ran
        return out
    results = run["results"]
    for name, (outcome, message) in sorted(results.items()):
        if outcome in NOT_FAILED:
            continue
        row = registered.get(name)
        if row is None:
            out["YENİ"].append(name)
        elif fingerprint(message) != row["failure_fingerprint"]:
            out["YENİ-AYRINTI"].append(f"{name} (kayıt {row['failure_fingerprint']}, şimdi {fingerprint(message)})")
        else:
            out["BİLİNEN"].append(name)
    for name in sorted(registered):
        if name not in results or results[name][0] == "NotExecuted":
            out["KOŞMADI"].append(name)
        elif results[name][0] == "Passed":
            out["DÜZELDİ"].append(name)
    return out


def exit_code(result: dict, run_format: str) -> int:
    if result["YENİ"] or result["YENİ-AYRINTI"]:
        return EXIT_REGRESSION
    if run_format == "console":
        return EXIT_NAMES_ONLY
    if result["KOŞMADI"]:
        return EXIT_UNTRUSTED   # a registered test did not run: the run cannot confirm the known failures
    return EXIT_OK


def git(*args: str) -> str:
    if not args or args[0] not in READ_ONLY_GIT:
        raise RuntimeError(f"git {' '.join(args[:1])}: not a read-only command allowed here")
    env = dict(os.environ, GIT_OPTIONAL_LOCKS="0")
    done = subprocess.run(["git", "--no-optional-locks", "-C", str(ROOT), *args], capture_output=True, env=env)
    if done.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} → {done.returncode}: {done.stderr.decode(errors='replace').strip()[:200]}")
    return done.stdout.decode("utf-8", errors="replace")


def commit_exists(sha: str) -> bool:
    try:
        return bool(sha) and not sha.startswith("-") and bool(git("cat-file", "-t", f"{sha}^{{commit}}"))
    except RuntimeError:
        return False


def changed_paths_since(sha: str) -> list[str]:
    paths = set()
    out = git("diff", "--name-status", "-M", "-z", f"{sha}..HEAD").split("\0")
    i = 0
    while i < len(out) and out[i]:
        status = out[i]
        n = 2 if status[:1] in ("R", "C") else 1
        paths.update(out[i + 1:i + 1 + n])
        i += 1 + n
    entries = git("status", "--porcelain=v1", "-z", "--untracked-files=all").split("\0")
    i = 0
    while i < len(entries) and entries[i]:
        entry = entries[i]
        paths.add(entry[3:])
        if entry[:1] in ("R", "C"):
            paths.add(entries[i + 1]); i += 1
        i += 1
    return sorted(p for p in paths if p)


def decide(rows: list[dict], suite: str, changed_since: dict[str, list[str]]) -> list[tuple[str, str, str]]:
    """(test, KOŞ önerisi | ATLA önerisi, reason) per open row of the suite."""
    out = []
    for r in rows:
        if r["suite"] != suite or r["status"] != "open":
            continue
        globs = [g.strip() for g in r["rerun_trigger"].split(",") if g.strip()]
        hit = next((p for p in changed_since.get(r["last_verified_commit"], []) if any(fnmatch.fnmatch(p, g) for g in globs)), None)
        out.append((r["test"], "KOŞ önerisi", f"tetikleyici: {hit}") if hit else (r["test"], "ATLA önerisi", "tetikleyici yol değişmedi"))
    return out


def validate(rows: list[dict], exists=commit_exists, tracked: list[str] | None = None) -> list[str]:
    problems, seen = [], set()
    for i, r in enumerate(rows, start=2):
        if not r["test"].strip() or not r["suite"].strip():
            problems.append(f"line {i}: suite and test are required")
        for key in ("first_recorded_commit", "last_verified_commit"):
            if not exists(r[key]):
                problems.append(f"line {i}: {key} '{r[key]}' is not a commit in this repository")
        globs = [g.strip() for g in r["rerun_trigger"].split(",") if g.strip()]
        if not globs:
            problems.append(f"line {i}: rerun_trigger is required (without it ATLA could never be justified)")
        for g in globs:
            if "{" in g or "}" in g:
                problems.append(f"line {i}: glob '{g}' uses braces, which fnmatch does not support")
            elif tracked is not None and not any(fnmatch.fnmatch(p, g) for p in tracked):
                problems.append(f"line {i}: glob '{g}' matches no tracked path")
        if r["status"] not in ("open", "fixed"):
            problems.append(f"line {i}: status must be open or fixed")
        if r["status"] == "open" and not re.fullmatch(r"(items:\d+:|msg:)[0-9a-f]{16}", r["failure_fingerprint"] or ""):
            problems.append(f"line {i}: an open row needs a failure_fingerprint (python3 scripts/known_failures.py fingerprint <trx>)")
        if (r["suite"], r["test"]) in seen:
            problems.append(f"line {i}: duplicate ({r['suite']}, {r['test']})")
        seen.add((r["suite"], r["test"]))
    return problems


def write_calls(source: str) -> list[str]:
    found = []
    for node in ast.walk(ast.parse(source)):
        if isinstance(node, ast.Call):
            f = node.func
            if isinstance(f, ast.Attribute) and f.attr in {"write_text", "write_bytes", "unlink", "rename", "mkdir", "rmdir", "touch", "rmtree"}:
                found.append(f"line {node.lineno}: .{f.attr}()")
            if isinstance(f, ast.Name) and f.id == "open":
                mode = node.args[1] if len(node.args) > 1 else next((k.value for k in node.keywords if k.arg == "mode"), None)
                if mode is not None and not (isinstance(mode, ast.Constant) and set(str(mode.value)) <= set("rbt")):
                    found.append(f"line {node.lineno}: open() with a write mode")
    return found


def self_test() -> int:
    known_list = "Defter kapsamı eksik (2):\n  - Diten.A: 3 yazma komutu var\n  - Diten.B: 1 yazma komutu var\n"
    plus_one = known_list + "  - Diten.C: 5 yazma komutu var\n"
    known_msg = "Assert.Equal() Failure: Strings differ\n  Expected: \"abc\"\n  Actual:   \"def\""
    fp_list, fp_msg = fingerprint(known_list), fingerprint(known_msg)
    reg = "\t".join(COLUMNS) + "\n" + "\n".join([
        f"S\tNs.A.Known1\taaa\tBL-1\tbbb\tservices/*,tests/*\tBL-1\topen\t{fp_list}",
        f"S\tNs.A.Known2(scenario: \"x\")\taaa\tBL-1\tbbb\tdocs/*\tBL-1\topen\t{fp_msg}",
    ]) + "\n"
    rows = read_registry(reg)

    def trx(results: list[tuple[str, str, str]], outcome: str = "Failed") -> str:
        body = "".join(f'<UnitTestResult testName="{n}" outcome="{o}"><Output><ErrorInfo><Message>{m}</Message></ErrorInfo></Output></UnitTestResult>'
                       for n, o, m in results)
        return ('<?xml version="1.0"?><TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>'
                f'{body}</Results><ResultSummary outcome="{outcome}"/></TestRun>')

    k2 = 'Ns.A.Known2(scenario: &quot;x&quot;)'
    good = trx([("Ns.A.Known1", "Failed", known_list), (k2, "Failed", known_msg.replace('"', "&quot;")), ("Ns.A.Ok", "Passed", "")])
    cases = []

    def run(text: str) -> tuple[dict | None, int]:
        try:
            parsed = parse_run(text)
        except Untrusted:
            return None, EXIT_UNTRUSTED
        result = classify(rows, parsed, "S")
        return result, exit_code(result, parsed["format"])

    r, code = run(good)
    cases.append(("trx: registered failures with the same fingerprint -> 2 BİLİNEN, exit 0", code == 0 and len(r["BİLİNEN"]) == 2))
    r, code = run(good.replace(known_list, plus_one))
    cases.append(("sabotage: one more violation inside a known red test -> YENİ-AYRINTI, exit 1", code == 1 and r["YENİ-AYRINTI"]))
    r, code = run(good.replace('<UnitTestResult testName="Ns.A.Ok" outcome="Passed">', '<UnitTestResult testName="Ns.B.New" outcome="Error">'))
    cases.append(("sabotage: a new test with outcome Error -> YENİ, exit 1", code == 1 and r["YENİ"] == ["Ns.B.New"]))
    cases.append(("sabotage: empty file -> exit 2", run("")[1] == 2))
    cases.append(("sabotage: TRX without results -> exit 2", run(trx([]))[1] == 2))
    cases.append(("sabotage: aborted run -> exit 2", run(trx([("Ns.A.Known1", "Failed", known_list)], outcome="Aborted"))[1] == 2))
    cases.append(("sabotage: unknown console format -> exit 2", run("some tool printed something\n")[1] == 2))
    console = "\x1b[31m  Failed Ns.A.Known1 [1 ms]\x1b[0m\nFailed!  - Failed:     2, Passed: 9, Skipped: 0, Total: 11\n"
    cases.append(("sabotage: console summary says 2, only 1 name found -> exit 2", run(console)[1] == 2))
    r, code = run(console.replace("Failed:     2", "Failed:     1"))
    cases.append(("console log: names only -> exit 3, no DÜZELDİ claimed", code == 3 and not r["DÜZELDİ"] and r["BİLİNMİYOR"]))
    r, code = run(trx([("Ns.A.Known1", "Failed", known_list), (k2, "NotExecuted", "")]))
    cases.append(("trx: registered test NotExecuted -> KOŞMADI (exit 2), not DÜZELDİ",
                  code == 2 and k2.replace("&quot;", '"') in r["KOŞMADI"] and not r["DÜZELDİ"]))
    decisions = decide(rows, "S", {"bbb": ["services/Diten.X/Handler.cs"]})
    cases.append(("should-rerun: per test — trigger hit KOŞ, the other ATLA", [d[1] for d in decisions] == ["KOŞ önerisi", "ATLA önerisi"]))
    problems = validate(read_registry(reg.replace(fp_msg, "")), exists=lambda s: True)
    cases.append(("sabotage: open row without fingerprint -> validate fails", any("failure_fingerprint" in p for p in problems)))
    problems = validate(read_registry(reg.replace("docs/*", "services/{A,B}/*")), exists=lambda s: True)
    cases.append(("sabotage: brace glob -> validate fails", any("braces" in p for p in problems)))
    try:
        git("fetch"); refused = False
    except RuntimeError:
        refused = True
    cases.append(("read-only: git fetch refused; no write call in this script", refused and not write_calls(Path(__file__).read_text(encoding="utf-8"))))
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
    p = argparse.ArgumentParser(description="Known failing tests: check a run (fail-closed), fingerprint, recommend a local re-run, validate.")
    sub = p.add_subparsers(dest="cmd", required=True)
    pc = sub.add_parser("check"); pc.add_argument("run_file"); pc.add_argument("--suite")
    pf = sub.add_parser("fingerprint"); pf.add_argument("trx_file"); pf.add_argument("--suite")
    pr = sub.add_parser("should-rerun"); pr.add_argument("--suite", required=True)
    sub.add_parser("validate")
    a = p.parse_args()
    rows = read_registry(REGISTRY.read_text(encoding="utf-8")) if a.cmd != "fingerprint" else []

    if a.cmd == "validate":
        try:
            tracked = git("ls-files", "-z").split("\0")
        except RuntimeError as e:
            print(f"HATA {e}"); return EXIT_UNTRUSTED
        problems = validate(rows, tracked=tracked)
        for line in problems:
            print(f"HATA {line}")
        print("OK registry" if not problems else f"FAIL {len(problems)} problem(s)")
        return 1 if problems else 0

    if a.cmd in ("check", "fingerprint"):
        path = Path(a.run_file if a.cmd == "check" else a.trx_file)
        try:
            run = parse_run(path.read_text(encoding="utf-8-sig", errors="strict"))
        except (Untrusted, UnicodeDecodeError, OSError) as e:
            print(f"DUR: koşu çıktısı güvenilir değil — {e}")
            return EXIT_UNTRUSTED
        if a.cmd == "fingerprint":
            if run["format"] != "trx":
                print("DUR: parmak izi yalnız TRX'ten alınır"); return EXIT_UNTRUSTED
            for name, (outcome, message) in sorted(run["results"].items()):
                if outcome not in NOT_FAILED:
                    print(f"{fingerprint(message)}\t{outcome}\t{name}")
            return EXIT_OK
        result = classify(rows, run, a.suite)
        for label in ("BİLİNEN", "YENİ", "YENİ-AYRINTI", "DÜZELDİ", "KOŞMADI", "BİLİNMİYOR"):
            for name in result[label]:
                print(f"{label}\t{name}")
        print("ÖZET " + " ".join(f"{k}={len(v)}" for k, v in result.items()))
        code = exit_code(result, run["format"])
        if result["YENİ"] or result["YENİ-AYRINTI"]:
            print("DUR: kayıtta olmayan kırmızı ya da bilinen kırmızıda yeni ayrıntı var — regresyon olarak bildirin.")
        elif code == EXIT_UNTRUSTED:
            print("DUR: kayıtlı testlerden bazıları koşmadı — süitin tamamını --suite ile koşun.")
        elif code == EXIT_NAMES_ONLY:
            print("NOT: konsol çıktısı yalnız adları gösterir; ayrıntı doğrulanmadı. Kabul için TRX ile tekrar koşun.")
        if result["DÜZELDİ"]:
            print("NOT: DÜZELDİ satırları için kaydı güncelleyin (status=fixed ve last_verified_commit); sessizce silmeyin.")
        return code

    if a.cmd == "should-rerun":
        commits = {r["last_verified_commit"] for r in rows if r["suite"] == a.suite and r["status"] == "open"}
        if not commits:
            print(f"KOŞ önerisi: '{a.suite}' için kayıtlı açık satır yok")
            return EXIT_OK
        try:
            changed = {c: changed_paths_since(c) for c in commits}
        except RuntimeError as e:
            print(f"KOŞ önerisi: git ölçümü başarısız — {e}")
            return EXIT_UNTRUSTED
        decisions = decide(rows, a.suite, changed)
        for test, verdict, reason in decisions:
            print(f"{verdict}\t{test}\t{reason}")
        skip = [t for t, v, _ in decisions if v == "ATLA önerisi"]
        if skip:
            print("Yalnız 'ATLA önerisi' satırındaki testlerin tekrar koşusu atlanabilir; süitin geri kalanını koşun.")
        print(SKIP_CAVEAT)
        return EXIT_OK
    return 2


if __name__ == "__main__":
    sys.exit(main())
