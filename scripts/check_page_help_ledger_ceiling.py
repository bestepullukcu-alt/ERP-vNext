#!/usr/bin/env python3
"""BL-609 — the page-help ledger's ceilings only go down, checked against the BASE branch (phase1, pull requests).

docs/roadmap/backlog/page-help-ledger.md (UXH-001) carries its ceilings in one header line:

    **Tavan (yalnız azalır): kiracı 236 · platform 18 · frozen 99**

The web test measures that the ledger's rows stay under the ceiling; it cannot see a ceiling RAISED in the same pull
request. This script compares the header of this change with the base branch's: a ceiling above the base is red, by
name. A deliberate raise is a Control Tower decision written in the ledger itself, with its reason, as one line per
category, that must name the exact base and new value:

    - Tavan artışı (CT kararı): kiracı 236 → 240 — <gerekçe>

A category that disappears, or a new one without such a line, is red too. A malformed header is red (never "no
ceiling, nothing to compare"). When the ledger does not exist in this change or on the base, or no base is given (a
push, not a pull request), the check SKIPS by name, in capitals, and says that nothing was checked.

    python3 scripts/check_page_help_ledger_ceiling.py --base <git ref>            # head = the file on disk
    python3 scripts/check_page_help_ledger_ceiling.py --base-file A --head-file B  # tests
"""

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys

LEDGER = "docs/roadmap/backlog/page-help-ledger.md"
PREFIX = "[phase1] page-help ceiling:"
HEADER = re.compile(r"^\*\*Tavan \(yalnız azalır\):(?P<body>[^*]+)\*\*\s*$", re.M)
PAIR = re.compile(r"^\s*(?P<name>[^\W\d][\w-]*)\s+(?P<value>\d+)\s*$")
RAISE = re.compile(r"^- Tavan artışı \(CT kararı\): (?P<name>[^\W\d][\w-]*) (?P<old>\d+) → (?P<new>\d+) — (?P<why>\S.*)$", re.M)


class LedgerError(Exception):
    pass


def ceilings(text: str, where: str) -> dict[str, int]:
    headers = list(HEADER.finditer(text))
    if len(headers) != 1:
        raise LedgerError(f"{where}: expected exactly one '**Tavan (yalnız azalır): …**' header line, found {len(headers)}")
    result: dict[str, int] = {}
    for part in headers[0].group("body").split("·"):
        pair = PAIR.match(part)
        if not pair:
            raise LedgerError(f"{where}: ceiling '{part.strip()}' is not '<category> <number>'")
        if pair.group("name") in result:
            raise LedgerError(f"{where}: category '{pair.group('name')}' appears twice")
        result[pair.group("name")] = int(pair.group("value"))
    return result


def compare(base_text: str, head_text: str) -> list[str]:
    """Problems (empty = ok) of the head ledger against the base ledger."""
    base = ceilings(base_text, "base")
    head = ceilings(head_text, "this change")
    raises = {(m.group("name"), int(m.group("old")), int(m.group("new"))) for m in RAISE.finditer(head_text)}
    problems: list[str] = []
    for name in sorted(base.keys() - head.keys()):
        problems.append(f"category '{name}' ({base[name]} on the base) disappeared from the header")
    for name in sorted(head):
        old = base.get(name, 0)
        new = head[name]
        if new > old and (name, old, new) not in raises:
            problems.append(f"'{name}' ceiling raised {old} → {new} without a line "
                            f"'- Tavan artışı (CT kararı): {name} {old} → {new} — <gerekçe>'")
    return problems


def read_text(path: str) -> str:
    with open(path, encoding="utf-8") as handle:
        return handle.read()


def read_base(ref: str) -> str | None:
    shown = subprocess.run(["git", "show", f"{ref}:{LEDGER}"], capture_output=True, text=True)
    return shown.stdout if shown.returncode == 0 else None


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base", default=None, help="git ref of the base branch")
    parser.add_argument("--base-file", default=None)
    parser.add_argument("--head-file", default=LEDGER)
    args = parser.parse_args(argv)

    if args.base is None and args.base_file is None:
        print(f"{PREFIX} *** SKIPPED *** — no base branch (not a pull request). NOTHING WAS CHECKED.")
        return 0
    if not os.path.exists(args.head_file):
        print(f"{PREFIX} *** SKIPPED *** — {args.head_file} does not exist in this change. NOTHING WAS CHECKED.")
        return 0
    if args.base_file is not None:
        base_text = read_text(args.base_file) if os.path.exists(args.base_file) else None
    else:
        base_text = read_base(args.base)
    if base_text is None:
        print(f"{PREFIX} *** SKIPPED *** — the ledger does not exist on the base ({args.base or args.base_file}); "
              "this change introduces it. NOTHING WAS CHECKED.")
        return 0

    try:
        problems = compare(base_text, read_text(args.head_file))
    except LedgerError as error:
        print(f"{PREFIX} *** FAILED *** {error}")
        return 1
    if problems:
        print(f"{PREFIX} *** FAILED *** the ceilings only go down (UXH-001, BL-609):")
        for problem in problems:
            print(f"{PREFIX}     {problem}")
        return 1
    print(f"{PREFIX} ok — no ceiling above the base")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
