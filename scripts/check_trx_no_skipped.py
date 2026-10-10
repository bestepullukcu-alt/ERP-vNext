#!/usr/bin/env python3
"""BL-577 — "skipped = 0" for the service test jobs (.github/workflows/service-tests.yml).

A test that did not run proved nothing, and `dotnet test` exits 0 when tests are skipped. This script reads the TRX
files a test step wrote and fails when a test was not executed — named, one per line.

The ONE exception (WP-CI-SERVICE-TESTS-03 FIX1, CT decision): a skip whose reason starts with a code the repository's
allow-list (scripts/ci-skip-allowlist.tsv) names FOR THIS SUITE. Those are tests that cannot be true on this runner by
construction (BL-570 FIX4: MACOS_ONLY on Ubuntu, UID_EQUALS_GID in a uid = gid image, RUNS_AS_ROOT in a root container).
A skip without a reason, with an unknown code, or with a code listed only for another suite is red. The allowed ones are
counted by code in the job log. Without --suite no skip is allowed at all.

It never passes silently: a missing, unreadable or malformed result file, a file with no test results, an unreadable
allow-list, or counters that disagree with the results are all *** FAILED *** in capitals, exit 1.

    python3 scripts/check_trx_no_skipped.py [--suite <matrix suite>] [--allowlist <file>] <result.trx> [...]
"""

from __future__ import annotations

import argparse
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter

NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
PREFIX = "[skipped=0]"
DEFAULT_ALLOWLIST = os.path.join(os.path.dirname(os.path.abspath(__file__)), "ci-skip-allowlist.tsv")
# A reason code: capitals, digits and underscores at the very start of the skip reason (BL-570: "MACOS_ONLY: …").
CODE = re.compile(r"^([A-Z][A-Z0-9_]*)\b")


class AllowlistError(Exception):
    pass


def load_allowlist(path: str) -> set[tuple[str, str]]:
    """(suite, code) pairs. Lines: suite <TAB> CODE <TAB> reason; '#' comments and blank lines are ignored."""
    pairs: set[tuple[str, str]] = set()
    try:
        with open(path, encoding="utf-8") as handle:
            lines = handle.read().splitlines()
    except OSError as error:
        raise AllowlistError(f"allow-list unreadable: {path} ({error})") from error
    for number, line in enumerate(lines, start=1):
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        parts = line.split("\t")
        if len(parts) != 3 or not all(part.strip() for part in parts) or not CODE.fullmatch(parts[1].strip()):
            raise AllowlistError(f"allow-list line {number} is not 'suite<TAB>CODE<TAB>reason': {line!r}")
        pairs.add((parts[0].strip(), parts[1].strip()))
    return pairs


def check(path: str, suite: str | None = None, allowed: set[tuple[str, str]] | None = None) -> tuple[list[str], Counter]:
    """Returns (problems, allowed skips by code) for one TRX file; no problems means every test ran or was allowed."""
    allowed = allowed or set()
    accepted: Counter = Counter()
    try:
        root = ET.parse(path).getroot()
    except FileNotFoundError:
        return [f"*** FAILED *** result file not found: {path} — NOTHING WAS CHECKED"], accepted
    except (ET.ParseError, OSError) as error:
        return [f"*** FAILED *** result file unreadable: {path} ({error}) — NOTHING WAS CHECKED"], accepted

    total = 0
    not_run: list[str] = []
    for result in root.iter(NS + "UnitTestResult"):
        total += 1
        outcome = result.get("outcome", "")
        if outcome in ("Passed", "Failed"):
            continue
        name = result.get("testName", "?")
        # Measured (2026-10-10, a real xUnit skip): the reason is Output/ErrorInfo/Message.
        message = result.find(f"{NS}Output/{NS}ErrorInfo/{NS}Message")
        reason = (message.text or "").strip() if message is not None else ""
        match = CODE.match(reason)
        code = match.group(1) if match else None
        if outcome == "NotExecuted" and suite and code and (suite, code) in allowed:
            accepted[code] += 1
            continue
        why = "no reason" if not reason else f"code {code} not allowed for suite {suite}" if code else "reason without a code"
        not_run.append(f"{name}  [{outcome or 'no outcome'}; {why}: {reason[:120]}]")

    if total == 0:
        return [f"*** FAILED *** no test results in {path} — NOTHING WAS CHECKED"], accepted

    problems: list[str] = []
    if not_run:
        problems.append(f"*** FAILED *** {len(not_run)} test(s) did not run in {path}:")
        problems.extend(f"    {name}" for name in not_run)

    counters = root.find(f"{NS}ResultSummary/{NS}Counters")
    if counters is None:
        problems.append(f"*** FAILED *** no ResultSummary/Counters in {path} — the file is not a complete TRX")
    else:
        not_executed = int(counters.get("notExecuted", "0") or 0)
        if not_executed and not not_run and not accepted:
            problems.append(f"*** FAILED *** {path}: counters say notExecuted={not_executed} but no result is named")
    return problems, accepted


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(add_help=True)
    parser.add_argument("--suite", default=None)
    parser.add_argument("--allowlist", default=DEFAULT_ALLOWLIST)
    parser.add_argument("paths", nargs="*")
    args = parser.parse_args(argv)
    if not args.paths:
        print(f"{PREFIX} *** FAILED *** no result file given — NOTHING WAS CHECKED")
        return 1
    allowed: set[tuple[str, str]] = set()
    if args.suite:
        try:
            allowed = load_allowlist(args.allowlist)
        except AllowlistError as error:
            print(f"{PREFIX} *** FAILED *** {error}")
            return 1
    failed = False
    for path in args.paths:
        problems, accepted = check(path, args.suite, allowed)
        if accepted:
            summary = ", ".join(f"{code} x{count}" for code, count in sorted(accepted.items()))
            print(f"{PREFIX} allowed skips for suite {args.suite}: {sum(accepted.values())} ({summary})")
        if problems:
            failed = True
            for line in problems:
                print(f"{PREFIX} {line}")
        else:
            print(f"{PREFIX} ok — every test ran or was an allowed skip: {path}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
