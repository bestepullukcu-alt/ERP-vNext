#!/usr/bin/env python3
"""BL-577 — "skipped = 0" for the service test jobs (.github/workflows/service-tests.yml).

A test that did not run proved nothing, and `dotnet test` exits 0 when tests are skipped. This script reads the TRX
files a test step wrote and fails when ANY test was not executed — named, one per line. There is no allow-list: a
skip is a Control Tower decision, not a line here.

It never passes silently: a missing, unreadable or malformed result file, a file with no test results, or counters
that disagree with the results are all *** FAILED *** in capitals, exit 1.

    python3 scripts/check_trx_no_skipped.py <result.trx> [<result.trx> ...]
"""

from __future__ import annotations

import sys
import xml.etree.ElementTree as ET

NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
PREFIX = "[skipped=0]"


def check(path: str) -> list[str]:
    """Returns the problems found in one TRX file; an empty list means every test in it ran."""
    try:
        root = ET.parse(path).getroot()
    except FileNotFoundError:
        return [f"*** FAILED *** result file not found: {path} — NOTHING WAS CHECKED"]
    except (ET.ParseError, OSError) as error:
        return [f"*** FAILED *** result file unreadable: {path} ({error}) — NOTHING WAS CHECKED"]

    results = root.iter(NS + "UnitTestResult")
    total = 0
    not_run: list[str] = []
    for result in results:
        total += 1
        outcome = result.get("outcome", "")
        if outcome not in ("Passed", "Failed"):
            not_run.append(f"{result.get('testName', '?')}  [{outcome or 'no outcome'}]")

    if total == 0:
        return [f"*** FAILED *** no test results in {path} — NOTHING WAS CHECKED"]

    problems: list[str] = []
    if not_run:
        problems.append(f"*** FAILED *** {len(not_run)} test(s) did not run in {path}:")
        problems.extend(f"    {name}" for name in not_run)

    counters = root.find(f"{NS}ResultSummary/{NS}Counters")
    if counters is None:
        problems.append(f"*** FAILED *** no ResultSummary/Counters in {path} — the file is not a complete TRX")
    else:
        not_executed = int(counters.get("notExecuted", "0") or 0)
        if not_executed and not not_run:
            problems.append(f"*** FAILED *** {path}: counters say notExecuted={not_executed} but no result is named")
    return problems


def main(paths: list[str]) -> int:
    if not paths:
        print(f"{PREFIX} *** FAILED *** no result file given — NOTHING WAS CHECKED")
        return 1
    failed = False
    for path in paths:
        problems = check(path)
        if problems:
            failed = True
            for line in problems:
                print(f"{PREFIX} {line}")
        else:
            print(f"{PREFIX} ok — every test ran: {path}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
