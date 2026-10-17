#!/usr/bin/env python3
"""
Known failing tests (CT-OPS-01, fail-closed since the 2026-10-15 audit): compare a test run with
tests/known-failures.tsv and say whether a repeated LOCAL diagnosis is worth running again.

    python3 scripts/known_failures.py check <trx-or-console-log> [--suite NAME]
    python3 scripts/known_failures.py fingerprint <trx> [--suite NAME]
    python3 scripts/known_failures.py should-rerun --suite NAME
    python3 scripts/known_failures.py validate
    python3 scripts/known_failures.py --self-test

check         exit 0  no new regression in THIS comparison (not "tests passed", not "accepted"): registered failures
                      with the registered fingerprint, or KISMEN-DÜZELDİ — a listed failure lost some of its recorded
                      items and gained none (proved item by item against tests/known-failures-items.json); the
                      registry is updated by hand, never automatically
              exit 1  YENİ (failing, not registered) or YENİ-AYRINTI (registered, but its failure detail changed —
                      e.g. one more violation inside an already red test, or any change of a message-type failure):
                      a regression, stop and report
              exit 2  the run or the registry cannot be trusted: empty, unreadable or unknown format, no results, no
                      test executed, aborted, a console summary that does not match the failures found; or the
                      registry is one validate rejects (a duplicate (suite, test), …), tests/known-failures.tsv and
                      tests/known-failures-items.json disagree, or — without --suite — one test name is open in two
                      suites (CT-FIX-02: checked before any classification; the registry is never changed)
              exit 3  console log: names compared, failure details NOT verified — not enough for acceptance
              Acceptance uses a TRX (`dotnet test … --logger "trx;LogFileName=<file>"`).
              Outcomes other than Passed / NotExecuted (Error, Timeout, Aborted, …) count as failed.
fingerprint   prints the fingerprint of every failed test in a TRX (to record or update a registry row);
              --items-json prints the item lists of list-type failures as JSON instead.
should-rerun  per registered test: KOŞ önerisi / ATLA önerisi, from its rerun_trigger globs and every path changed since
              its last_verified_commit. ATLA covers ONLY the named registered tests; the rest of the suite (security
              guards included) still runs. Any git error → KOŞ önerisi and exit 2. A registry validate rejects -> DUR,
              exit 2, no recommendation (CT-FIX-02).
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
import json
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REGISTRY = ROOT / "tests" / "known-failures.tsv"
ITEMS = ROOT / "tests" / "known-failures-items.json"
COLUMNS = ["suite", "test", "first_recorded_commit", "first_recorded_source", "last_verified_commit",
           "rerun_trigger", "reference", "status", "failure_fingerprint", "owner", "review_by"]
READ_ONLY_GIT = {"cat-file", "diff", "ls-files", "rev-parse", "status"}
SKIP_CAVEAT = ("ATLA yalnız adı verilen kayıtlı testlerin yerel tekrar tanısı içindir: süitin geri kalanı (güvenlik "
               "bekçileri dahil), CI, zorunlu kabul testleri ve sahibin açıkça istediği testler her zaman koşulur.")
ANSI = re.compile(r"\x1b\[[0-9;]*[A-Za-z]")
FAILED_LINE = re.compile(r"^\s*(?:failed|başarısız)\s+(\S.*?)(?:\s+\[[^\]]*\])?\s*$", re.I)
SUMMARY_LINE = re.compile(r"(?:failed|passed|başarısız|başarılı)!\s*-\s*(?:failed|başarısız):\s*(\d+),\s*"
                          r"(?:passed|başarılı):\s*(\d+),\s*(?:skipped|atlanan):\s*(\d+),\s*(?:total|toplam):\s*(\d+)", re.I)
NOT_FAILED = {"Passed", "NotExecuted"}
# CT-FIX-03 (R5, V4A-F6): a TRX is trusted only when its one ResultSummary and its one Counters agree with the raw
# result rows (counted before two rows with one name are merged: a theory test gives several rows, 144 rows / 143 names
# in the real architecture suite). Every counter named after an outcome equals the rows with that outcome; total equals
# the rows; executed equals the rows that are not NotExecuted. A summary says Failed exactly when a row failed. The
# 'notExecuted' counter is 0 or the NotExecuted rows: VSTest writes a skipped test as a NotExecuted row and leaves that
# counter 0 (measured on four real CRM TRX files: total 1264, executed 1259, 5 NotExecuted rows, notExecuted="0"); any
# other value is refused. An outcome outside this vocabulary is refused (Codex: a registered message cannot launder it).
COUNTER_OUTCOMES = {"passed": "Passed", "failed": "Failed", "error": "Error", "timeout": "Timeout", "aborted": "Aborted",
                    "inconclusive": "Inconclusive", "passedButRunAborted": "PassedButRunAborted",
                    "notRunnable": "NotRunnable", "disconnected": "Disconnected", "warning": "Warning",
                    "completed": "Completed", "inProgress": "InProgress", "pending": "Pending"}
SUMMARY_PASSING = {"Completed", "Passed"}
KNOWN_OUTCOMES = set(COUNTER_OUTCOMES.values()) | {"NotExecuted"}
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


def items_of(message: str) -> list[str]:
    """The "- …" lines of a failure message, whitespace-normalised, sorted, unique."""
    text = normalise(message)
    return sorted({re.sub(r"\s+", " ", m.group(1)).strip() for m in re.finditer(r"(?m)^\s*-\s+(.+)$", text)})


def items_fingerprint(items: list[str]) -> str:
    return f"items:{len(items)}:{hashlib.sha256(chr(10).join(sorted(items)).encode()).hexdigest()[:16]}"


def fingerprint(message: str) -> str:
    items = items_of(message)
    if items:
        return items_fingerprint(items)
    flat = re.sub(r"\s+", " ", normalise(message))
    return "msg:" + hashlib.sha256(flat.encode()).hexdigest()[:16]


def load_items(rows: list[dict], text: str | None) -> tuple[dict[str, list[str]], list[str]]:
    """Item lists of list-type rows, and every disagreement between them and the TSV fingerprints."""
    problems = []
    try:
        data = json.loads(text) if text is not None else {"tests": {}}
        lists = data["tests"]
        if not isinstance(lists, dict) or not all(isinstance(v, list) for v in lists.values()):
            raise ValueError("'tests' must map a test name to a list of items")
    except (ValueError, KeyError, TypeError) as e:
        return {}, [f"{ITEMS.name}: okunamadı ({e})"]
    wanted = {r["test"]: r["failure_fingerprint"] for r in rows if r["failure_fingerprint"].startswith("items:")}
    for name, fp in sorted(wanted.items()):
        if name not in lists:
            problems.append(f"{ITEMS.name}: liste tipindeki '{name}' için öğe listesi yok")
        elif items_fingerprint(lists[name]) != fp:
            problems.append(f"{ITEMS.name}: '{name}' listesi TSV parmak iziyle uyuşmuyor ({items_fingerprint(lists[name])} ≠ {fp})")
    for name in sorted(set(lists) - set(wanted)):
        problems.append(f"{ITEMS.name}: '{name}' TSV'de liste tipinde kayıtlı değil")
    return lists, problems


def parse_trx(text: str) -> dict:
    try:
        root = ET.fromstring(text)
    except ET.ParseError as e:
        raise Untrusted(f"TRX okunamadı: {e}")
    results, outcome = {}, None
    raw: list[str] = []
    summaries, counters = [], []
    for el in root.iter():
        tag = el.tag.rsplit("}", 1)[-1]
        if tag == "UnitTestResult":
            message = "".join(t.text or "" for t in el.iter() if t.tag.rsplit("}", 1)[-1] == "Message")
            name, outcome_ = el.get("testName", "").strip(), el.get("outcome", "")
            raw.append(outcome_)
            if name in results:   # two results with one name: a failing one must never be hidden by another
                prev_outcome, prev_message = results[name]
                failing = [(o, m) for o, m in ((prev_outcome, prev_message), (outcome_, message)) if o not in NOT_FAILED]
                if failing:
                    outcome_, message = failing[0][0], "\n".join(sorted(m for _, m in failing))
                else:
                    outcome_ = "Passed" if "Passed" in (prev_outcome, outcome_) else outcome_
            results[name] = (outcome_, message)
        elif tag == "ResultSummary":
            outcome = el.get("outcome")
            summaries.append(el)
        elif tag == "Counters":
            counters.append(el)
    if not results:
        raise Untrusted("TRX'te hiç test sonucu yok (koşu çökmüş ya da hiç başlamamış olabilir)")
    if all(outcome_ == "NotExecuted" for outcome_, _ in results.values()):
        raise Untrusted("TRX'te hiçbir test koşmamış (hepsi NotExecuted)")
    if outcome in ("Aborted", "Error", "Timeout"):
        raise Untrusted(f"TRX koşu sonucu '{outcome}': koşu tamamlanmadı")
    reconcile(raw, summaries, counters)
    return {"format": "trx", "results": results}


def reconcile(raw: list[str], summaries: list, counters: list) -> None:
    """CT-FIX-03 (R5): Untrusted unless the summary and the counters agree with the raw result rows (see COUNTER_OUTCOMES)."""
    if len(summaries) != 1:
        raise Untrusted(f"TRX'te {len(summaries)} ResultSummary var, tam 1 olmalı")
    if len(counters) != 1:
        raise Untrusted(f"TRX'te {len(counters)} Counters var, tam 1 olmalı — sonuç satırları sayaçla uzlaştırılamaz")
    values = {}
    for key, value in counters[0].attrib.items():
        if not re.fullmatch(r"0|[1-9]\d{0,8}", value):
            raise Untrusted(f"TRX sayacı {key}='{value[:12]}' negatif olmayan bir tam sayı değil")
        values[key] = int(value)
    for key in ("total", "executed", "passed", "failed"):
        if key not in values:
            raise Untrusted(f"TRX sayaçlarında '{key}' yok")
    count = {o: raw.count(o) for o in set(raw)}
    unknown = sorted(set(raw) - KNOWN_OUTCOMES)
    if unknown:
        raise Untrusted(f"TRX'te tanınmayan sonuç türü: {', '.join(u[:20] or '(boş)' for u in unknown[:3])} — koşu güvenilir değil")
    if "notExecuted" in values and values["notExecuted"] not in (0, count.get("NotExecuted", 0)):
        raise Untrusted(f"TRX sayacı notExecuted={values['notExecuted']}: 0 ya da NotExecuted satır sayısı "
                        f"({count.get('NotExecuted', 0)}) olmalı")
    if values["total"] != len(raw):
        raise Untrusted(f"TRX sayacı total={values['total']}, sonuç satırı {len(raw)} — koşu güvenilir değil")
    for key, name in COUNTER_OUTCOMES.items():
        if key in values and values[key] != count.get(name, 0):
            raise Untrusted(f"TRX sayacı {key}={values[key]}, '{name}' sonuç satırı {count.get(name, 0)} — koşu güvenilir değil")
    if values["executed"] != len(raw) - count.get("NotExecuted", 0):
        raise Untrusted(f"TRX sayacı executed={values['executed']}, koşan sonuç satırı {len(raw) - count.get('NotExecuted', 0)}")
    failing = sum(n for o, n in count.items() if o not in NOT_FAILED)
    summary = summaries[0].get("outcome")
    if failing and summary != "Failed":
        raise Untrusted(f"TRX özeti '{summary}' ama {failing} sonuç satırı başarısız — koşu güvenilir değil")
    if not failing and summary not in SUMMARY_PASSING:
        raise Untrusted(f"TRX özeti '{summary}' ama başarısız sonuç satırı yok — kırmızı yeşile dönmez, koşu güvenilir değil")


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


def classify(rows: list[dict], run: dict, suite: str | None, items: dict[str, list[str]] | None = None) -> dict:
    registered = {r["test"]: r for r in rows if r["status"] == "open" and (suite is None or r["suite"] == suite)}
    items = items or {}
    out = {"BİLİNEN": [], "KISMEN-DÜZELDİ": [], "YENİ": [], "YENİ-AYRINTI": [], "DÜZELDİ": [], "KOŞMADI": [], "BİLİNMİYOR": []}
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
        elif fingerprint(message) == row["failure_fingerprint"]:
            out["BİLİNEN"].append(name)
        elif row["failure_fingerprint"].startswith("items:") and name in items:
            now, before = set(items_of(message)), set(items[name])
            added, gone = sorted(now - before), sorted(before - now)
            if not now:
                # a red list-type test whose message lost its list: a different failure, never "everything fixed"
                out["YENİ-AYRINTI"].append(f"{name} (liste biçimi kayboldu: mesajda öğe yok — incele)")
            elif added:
                shown = "; ".join(a[:90] for a in added[:3]) + (f"; … +{len(added) - 3}" if len(added) > 3 else "")
                out["YENİ-AYRINTI"].append(f"{name} ({len(added)} yeni öğe: {shown})")
            elif gone and now < before:
                out["KISMEN-DÜZELDİ"].append(f"{name} ({len(gone)} kayıtlı öğe giderildi, yeni öğe yok)")
            else:
                out["YENİ-AYRINTI"].append(f"{name} (öğe kümesi aynı, parmak izi farklı — incele)")
        else:
            out["YENİ-AYRINTI"].append(f"{name} (kayıt {row['failure_fingerprint']}, şimdi {fingerprint(message)} — incele)")
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
        for key in ("owner", "review_by"):
            if not (r.get(key) or "").strip():
                problems.append(f"line {i}: {key} is required (write 'sahip ataması bekliyor' / 'karar bekliyor' when undecided)")
        if (r["suite"], r["test"]) in seen:
            problems.append(f"line {i}: duplicate ({r['suite']}, {r['test']})")
        seen.add((r["suite"], r["test"]))
    return problems


def registry_stop(rows: list[dict], item_problems: list[str], suite: str | None, tracked: list[str]) -> list[str]:
    """CT-FIX-02 (K2): why check / should-rerun may not use this registry — validate()'s own problems and the item-list
    disagreements; and for a check without --suite, a test name open in more than one suite: a TRX does not name its
    suite, so the row is never chosen by guessing from the message. The registry and the item lists are not changed."""
    problems = validate(rows, tracked=tracked) + item_problems
    if suite is None:
        seen: dict[str, set[str]] = {}
        for r in rows:
            if r["status"] == "open":
                seen.setdefault(r["test"], set()).add(r["suite"])
        for name, suites in sorted(seen.items()):
            if len(suites) > 1:
                problems.append(f"'{name}' {len(suites)} süitte açık kayıtlı ({', '.join(sorted(suites))}); TRX süiti "
                                "kanıtlamaz — --suite verin")
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
        f"S\tNs.A.Known1\taaa\tBL-1\tbbb\tservices/*,tests/*\tBL-1\topen\t{fp_list}\tteam\tkarar bekliyor",
        f"S\tNs.A.Known2(scenario: \"x\")\taaa\tBL-1\tbbb\tdocs/*\tBL-1\topen\t{fp_msg}\tteam\tkarar bekliyor",
    ]) + "\n"
    rows = read_registry(reg)
    item_lists = {"Ns.A.Known1": items_of(known_list)}

    def trx(results: list[tuple[str, str, str]], outcome: str = "Failed") -> str:
        body = "".join(f'<UnitTestResult testName="{n}" outcome="{o}"><Output><ErrorInfo><Message>{m}</Message></ErrorInfo></Output></UnitTestResult>'
                       for n, o, m in results)
        return ('<?xml version="1.0"?><TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>'
                f'{body}</Results><ResultSummary outcome="{outcome}"/></TestRun>')

    k2 = 'Ns.A.Known2(scenario: &quot;x&quot;)'
    good = trx([("Ns.A.Known1", "Failed", known_list), (k2, "Failed", known_msg.replace('"', "&quot;")), ("Ns.A.Ok", "Passed", "")])
    cases = []

    # CT-FIX-03 (R5): a TRX must carry one Counters that agrees with its rows and a summary that agrees with them. The
    # classification cases below are about classification, so run() writes the counters and summary the rows imply
    # (with_counters); the R5 counter cases pass their TRX untouched (counters=False).
    def with_counters(text: str) -> str:
        outs = re.findall(r'<UnitTestResult [^>]*outcome="([^"]*)"', text)
        failing = any(o not in NOT_FAILED for o in outs)
        attrs = {"total": len(outs), "executed": len(outs) - outs.count("NotExecuted"), "passed": outs.count("Passed"),
                 "failed": outs.count("Failed"), "error": outs.count("Error"), "notExecuted": 0}
        c = "<Counters " + " ".join(f'{k}="{v}"' for k, v in attrs.items()) + "/>"

        def summary(m):
            o = m.group(1)
            if o not in ("Aborted", "Error", "Timeout"):
                o = "Failed" if failing else "Completed"
            return f'<ResultSummary outcome="{o}">{c}</ResultSummary>'
        return re.sub(r'<ResultSummary outcome="([^"]*)"/>', summary, text)

    def run(text: str, counters: bool = True) -> tuple[dict | None, int]:
        try:
            parsed = parse_run(with_counters(text) if counters else text)
        except Untrusted:
            return None, EXIT_UNTRUSTED
        result = classify(rows, parsed, "S", item_lists)
        return result, exit_code(result, parsed["format"])

    r, code = run(good)
    # CT-FIX-03 (R5, V4A-F6; Codex's TRX matrix): the counters and the summary against the raw rows, TRX untouched.
    # The positives run the whole check (every registered test present: exit 0); the sabotages ask parse_run itself, so
    # a refusal for another reason (a registered test missing) can never make them pass.
    def counted(rows_, summary="Failed", counters=None, extra=""):
        body = "".join(f'<UnitTestResult testName="{n}" outcome="{o}"><Output><ErrorInfo><Message>{m}</Message></ErrorInfo></Output></UnitTestResult>'
                       for n, o, m in rows_)
        c = "" if counters is None else "<Counters " + " ".join(f'{k}="{v}"' for k, v in counters.items()) + "/>"
        return f'<TestRun><Results>{body}</Results><ResultSummary outcome="{summary}">{c}</ResultSummary>{extra}</TestRun>'

    def untrusted(text: str) -> bool:
        try:
            parse_run(text)
            return False
        except Untrusted:
            return True
    base3 = [("Ns.A.Known1", "Failed", known_list), (k2, "Failed", known_msg.replace('"', "&quot;")), ("Ns.A.Ok", "Passed", "")]
    ok_c = {"total": 3, "executed": 3, "passed": 1, "failed": 2}
    cases.append(("CT-FIX-03 R5: counters and summary agree with the rows -> trusted; the registered reds stay BİLİNEN (exit 0)",
                  run(counted(base3, "Failed", ok_c), counters=False)[1] == EXIT_OK))
    skip = base3 + [("Ns.A.Skip", "NotExecuted", "")]
    for label, nx in (("notExecuted=\"0\" (VSTest)", 0), ("notExecuted = the skipped rows", 1)):
        cases.append((f"CT-FIX-03 R5: a skipped test — a NotExecuted row, {label}, executed = total - skipped -> trusted (exit 0)",
                      run(counted(skip, "Failed", {"total": 4, "executed": 3, "passed": 1, "failed": 2, "notExecuted": nx}), counters=False)[1] == EXIT_OK))
    two_c = "<Counters " + " ".join(f'{k}="{v}"' for k, v in ok_c.items()) + "/>"
    for label, text in (
            ("no Counters", counted(base3, "Failed")),
            ("two Counters", counted(base3, "Failed", ok_c).replace("</ResultSummary>", two_c + "</ResultSummary>")),
            ("two ResultSummary", counted(base3, "Failed", ok_c, extra='<ResultSummary outcome="Failed"/>')),
            ("total more than the rows", counted(base3, "Failed", dict(ok_c, total=4))),
            ("failed more than the failed rows", counted(base3, "Failed", dict(ok_c, failed=3, passed=0))),
            ("every row passed, counters say failed", counted([(n, "Passed", "") for n, _, _ in base3], "Failed", ok_c)),
            ("executed not the rows that ran", counted(base3, "Failed", dict(ok_c, executed=2))),
            ("an arbitrary notExecuted", counted(skip, "Failed", {"total": 4, "executed": 3, "passed": 1, "failed": 2, "notExecuted": 999})),
            ("a negative counter", counted(base3, "Failed", dict(ok_c, failed=-1))),
            ("a text counter", counted(base3, "Failed", dict(ok_c, total="many"))),
            ("no executed counter", counted(base3, "Failed", {k: v for k, v in ok_c.items() if k != "executed"})),
            ("summary Failed, no failed row", counted([("Ns.A.Ok", "Passed", "")], "Failed", {"total": 1, "executed": 1, "passed": 1, "failed": 0})),
            ("summary Completed with a failed row", counted(base3, "Completed", ok_c)),
            ("summary NotExecuted while rows ran", counted([("Ns.A.Ok", "Passed", "")], "NotExecuted", {"total": 1, "executed": 1, "passed": 1, "failed": 0})),
            ("an unknown outcome on a registered test with its known message (Codex)",
             counted([("Ns.A.Known1", "Bogus", known_list)], "Failed", {"total": 1, "executed": 1, "passed": 0, "failed": 0}))):
        cases.append((f"CT-FIX-03 R5 sabotage: {label} -> not trusted (exit 2; red never turns green)", untrusted(text)))
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
    r, code = run(good.replace('<UnitTestResult testName="Ns.A.Ok" outcome="Passed">',
                               '<UnitTestResult testName="Ns.B.Dup" outcome="Failed"><Output><ErrorInfo><Message>x</Message></ErrorInfo></Output></UnitTestResult>'
                               '<UnitTestResult testName="Ns.B.Dup" outcome="Passed">'))
    cases.append(("sabotage: a failing result followed by a passing one with the same name -> still YENİ, exit 1",
                  code == 1 and "Ns.B.Dup" in r["YENİ"]))
    fewer = "Defter kapsamı eksik (1):\n  - Diten.A: 3 yazma komutu var\n"
    r, code = run(good.replace(known_list, fewer))
    cases.append(("one recorded item fixed, none added -> KISMEN-DÜZELDİ, exit 0", code == 0 and r["KISMEN-DÜZELDİ"] and not r["YENİ-AYRINTI"]))
    swapped = "Defter kapsamı eksik (1):\n  - Diten.A: 3 yazma komutu var\n  - Diten.Z: 9 yazma komutu var\n"
    r, code = run(good.replace(known_list, swapped))
    cases.append(("sabotage: one item fixed but another added -> YENİ-AYRINTI, exit 1", code == 1 and r["YENİ-AYRINTI"] and not r["KISMEN-DÜZELDİ"]))
    r, code = run(good.replace("Expected: &quot;abc&quot;", "Expected: &quot;abd&quot;"))
    cases.append(("sabotage: a message-type failure changed -> YENİ-AYRINTI, exit 1", code == 1 and r["YENİ-AYRINTI"]))
    r, code = run(good.replace(known_list, "System.InvalidOperationException: failed before collecting violations"))
    cases.append(("sabotage: a list-type failure lost its list -> YENİ-AYRINTI, exit 1, never KISMEN-DÜZELDİ",
                  code == 1 and r["YENİ-AYRINTI"] and not r["KISMEN-DÜZELDİ"]))
    cases.append(("sabotage: no test executed (all NotExecuted) -> exit 2",
                  run(trx([("Ns.X.Other", "NotExecuted", "")]))[1] == 2))
    _, problems = load_items(rows, json.dumps({"tests": {"Ns.A.Known1": items_of(known_list)[:1]}}))
    cases.append(("sabotage: item list disagrees with the TSV fingerprint -> problem", any("uyuşmuyor" in p for p in problems)))
    _, problems = load_items(rows, json.dumps({"tests": {}}))
    cases.append(("sabotage: list-type row without an item list -> problem", any("öğe listesi yok" in p for p in problems)))
    _, problems = load_items(rows, json.dumps({"tests": item_lists}))
    cases.append(("item lists that hash to the TSV fingerprints -> no problem", not problems))
    problems = validate(read_registry(reg.replace("\tteam\tkarar bekliyor", "\t\t", 1)), exists=lambda s: True)
    cases.append(("sabotage: empty owner/review_by -> validate fails", any("owner is required" in p for p in problems)))
    decisions = decide(rows, "S", {"bbb": ["services/Diten.X/Handler.cs"]})
    cases.append(("should-rerun: per test — trigger hit KOŞ, the other ATLA", [d[1] for d in decisions] == ["KOŞ önerisi", "ATLA önerisi"]))
    problems = validate(read_registry(reg.replace(fp_msg, "")), exists=lambda s: True)
    cases.append(("sabotage: open row without fingerprint -> validate fails", any("failure_fingerprint" in p for p in problems)))
    problems = validate(read_registry(reg.replace("docs/*", "services/{A,B}/*")), exists=lambda s: True)
    cases.append(("sabotage: brace glob -> validate fails", any("braces" in p for p in problems)))
    # CT-FIX-02 (K2/K4): the registry check through main() itself — check and should-rerun refuse a registry validate
    # rejects before the run is read; a check without --suite refuses a name open in two suites. In memory: the registry
    # and item files are replaced by text objects, the run file does not exist, the output is captured.
    import contextlib
    head = git("rev-parse", "HEAD").strip()
    kreg = lambda *rows_: "\t".join(COLUMNS) + "\n" + "".join(
        f"{suite}\tNs.T\t{head}\tfixture\t{head}\tscripts/*\tfixture\topen\t{fp}\tteam\tkarar bekliyor\n" for suite, fp in rows_)

    class Text:
        def __init__(self, text: str | None, name: str):
            self.text, self.name = text, name

        def read_text(self, encoding: str = "utf-8") -> str:
            return self.text

        def exists(self) -> bool:
            return self.text is not None

    def via_main(argv: list[str], reg_text: str) -> tuple[int, str]:
        saved, saved_argv, out = {k: globals()[k] for k in ("REGISTRY", "ITEMS")}, sys.argv, io.StringIO()
        globals().update(REGISTRY=Text(reg_text, "known-failures.tsv"), ITEMS=Text(None, "known-failures-items.json"))
        sys.argv = ["known_failures.py", *argv]
        try:
            with contextlib.redirect_stdout(out):
                code = main()
        finally:
            sys.argv = saved_argv
            globals().update(saved)
        return code, out.getvalue()
    nope = "/nonexistent/ct-fix-02/run.trx"
    one = via_main(["check", nope, "--suite", "S"], kreg(("S", fp_msg)))
    dup = via_main(["check", nope, "--suite", "S"], kreg(("S", fp_msg), ("S", fp_list)))
    two = via_main(["check", nope], kreg(("S", fp_msg), ("S2", fp_msg)))
    rer = via_main(["should-rerun", "--suite", "S"], kreg(("S", fp_msg), ("S", fp_list)))
    cases.append(("CT-FIX-02 K2: main() check — a valid registry reaches the run file (DUR: run untrusted, exit 2)",
                  one[0] == EXIT_UNTRUSTED and "koşu çıktısı güvenilir değil" in one[1] and "HATA" not in one[1]))
    cases.append(("CT-FIX-02 K2: main() check — a duplicate (suite, test) stops before the run is read, exit 2",
                  dup[0] == EXIT_UNTRUSTED and "duplicate" in dup[1] and "koşu çıktısı" not in dup[1]))
    cases.append(("CT-FIX-02 K2: main() check without --suite — one name open in two suites -> exit 2, never guessed",
                  two[0] == EXIT_UNTRUSTED and "süitte açık kayıtlı" in two[1] and "koşu çıktısı" not in two[1]))
    cases.append(("CT-FIX-02 K2: main() should-rerun — a duplicate (suite, test) -> exit 2, no KOŞ / ATLA line",
                  rer[0] == EXIT_UNTRUSTED and "duplicate" in rer[1] and "önerisi\t" not in rer[1]))
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
    pf.add_argument("--items-json", action="store_true")
    pr = sub.add_parser("should-rerun"); pr.add_argument("--suite", required=True)
    sub.add_parser("validate")
    a = p.parse_args()
    rows = read_registry(REGISTRY.read_text(encoding="utf-8")) if a.cmd != "fingerprint" else []
    items, item_problems = ({}, []) if a.cmd == "fingerprint" else load_items(
        rows, ITEMS.read_text(encoding="utf-8") if ITEMS.exists() else None)

    if a.cmd in ("check", "should-rerun"):
        # CT-FIX-02 (K2): classify / recommend only from a registry validate accepts — a duplicate row would otherwise let
        # the last row win (a changed failure read as BİLİNEN) or give one test both KOŞ and ATLA.
        try:
            stop = registry_stop(rows, item_problems, a.suite, git("ls-files", "-z").split("\0"))
        except RuntimeError as e:
            stop = [f"git ölçümü başarısız — {e}"]
        if stop:
            for line in stop:
                print(f"HATA {line}")
            print("DUR: tests/known-failures.tsv / tests/known-failures-items.json geçersiz ya da belirsiz — sınıflandırma "
                  "ve tekrar önerisi yapılmaz (python3 scripts/known_failures.py validate).")
            return EXIT_UNTRUSTED

    if a.cmd == "validate":
        try:
            tracked = git("ls-files", "-z").split("\0")
        except RuntimeError as e:
            print(f"HATA {e}"); return EXIT_UNTRUSTED
        problems = validate(rows, tracked=tracked) + item_problems
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
            if a.items_json:
                lists = {n: items_of(m) for n, (o, m) in sorted(run["results"].items()) if o not in NOT_FAILED and items_of(m)}
                print(json.dumps(lists, ensure_ascii=False, indent=2, sort_keys=True))
                return EXIT_OK
            for name, (outcome, message) in sorted(run["results"].items()):
                if outcome not in NOT_FAILED:
                    print(f"{fingerprint(message)}\t{outcome}\t{name}")
            return EXIT_OK
        result = classify(rows, run, a.suite, items)
        for label in ("BİLİNEN", "KISMEN-DÜZELDİ", "YENİ", "YENİ-AYRINTI", "DÜZELDİ", "KOŞMADI", "BİLİNMİYOR"):
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
        if result["KISMEN-DÜZELDİ"] and code == EXIT_OK:
            print("NOT: KISMEN-DÜZELDİ — bu karşılaştırmada yeni regresyon yok; testler geçmiş ya da kabul edilmiş sayılmaz. "
                  "Kaydı (TSV parmak izi ve JSON listesi) elle güncelleyin.")
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
