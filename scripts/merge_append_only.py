#!/usr/bin/env python3
"""Git merge driver for the append-only approval files (speed review item 4, owner 'uygula' 2026-10-29).

approvals.tsv, reviews.tsv and append-only-exceptions.tsv only ever grow at the end. When a Control Tower merges
origin/main into its branch, both sides appended rows after the common base and git reports a conflict on the last
line. The standing rule (feedback: "main first, then ours") resolves it by hand every time; this driver does the same:

    merging origin/main in:   result = main's file + our new rows          (main stays a prefix, as on main)
    any other merge:          result = our file + their new rows           (the first parent stays a prefix)

It refuses (exit 1, git leaves the normal conflict) when the base is not a prefix of both sides: a side that changed or
dropped an old row is not an append and must stay a visible conflict. `pack_approval.py history` stays the guard.

Install once per clone (the attribute lines are in .gitattributes; without this config git merges as plain text):
    git config merge.append-only.name "append-only rows: main first, then ours"
    git config merge.append-only.driver "python3 scripts/merge_append_only.py %O %A %B %P"
On Windows use `python` instead of `python3` if that is the interpreter's name.

    python3 scripts/merge_append_only.py --self-test
"""
from __future__ import annotations

import os
import subprocess
import sys
import tempfile


def _lines(text: str) -> list[str]:
    """Rows split on \\n only — the same way pack_approval.py reads them (CRLF folded, a final newline is no row)."""
    rows = text.replace("\r\n", "\n").split("\n")
    return rows[:-1] if rows and rows[-1] == "" else rows


def _read(path: str) -> list[str]:
    with open(path, encoding="utf-8", newline="") as f:
        return _lines(f.read())


def _main_lines(path_in_repo: str) -> list[str] | None:
    """The file as origin/main has it, or None when it cannot be read (no remote, new file)."""
    try:
        out = subprocess.run(["git", "show", f"origin/main:{path_in_repo}"], capture_output=True, check=True)
    except (OSError, subprocess.CalledProcessError):
        return None
    return _lines(out.stdout.decode("utf-8"))


def merge(base: list[str], ours: list[str], theirs: list[str], main: list[str] | None) -> list[str] | None:
    """The merged rows, or None when either side is not base + appended rows.

    When `theirs` is origin/main's file, main's rows come first (they are already on main and must stay its prefix);
    otherwise our side is the first parent and stays the prefix. A row both sides added identically appears once.
    """
    n = len(base)
    if ours[:n] != base or theirs[:n] != base:
        return None
    first, second = (theirs, ours) if main is not None and theirs == main else (ours, theirs)
    added = set(first[n:])
    return first + [row for row in second[n:] if row not in added]


def _write(path: str, rows: list[str]) -> None:
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write("\n".join(rows) + "\n")


def run(base_path: str, ours_path: str, theirs_path: str, path_in_repo: str) -> int:
    merged = merge(_read(base_path), _read(ours_path), _read(theirs_path), _main_lines(path_in_repo))
    if merged is None:
        sys.stderr.write(f"merge_append_only: {path_in_repo} — eski bir satır değişmiş ya da silinmiş; elle çözün\n")
        return 1
    _write(ours_path, merged)
    return 0


def self_test() -> int:
    cases = 0

    def check(name: str, got, want) -> None:
        nonlocal cases
        cases += 1
        if got != want:
            raise AssertionError(f"{name}: {got!r} != {want!r}")

    base = ["h", "r1"]
    # Merging origin/main in: main's file first, then our rows (sabotage: drop the main branch and this turns wrong).
    check("main alınırken main önce", merge(base, base + ["o1"], base + ["m1"], base + ["m1"]), base + ["m1", "o1"])
    # Merging a worker branch (not main): our side stays the prefix, the worker's rows follow (review ORTA-1).
    check("main dışı birleştirmede bizim taraf önek", merge(base, base + ["o1"], base + ["w1"], base + ["m1"]),
          base + ["o1", "w1"])
    # The same new row on both sides (both merged main) appears once.
    check("aynı satır bir kez", merge(base, base + ["m1", "o1"], base + ["m1"], base + ["m1"]), base + ["m1", "o1"])
    # Rows split on \n only, like pack_approval.py: a vertical tab inside a row does not split it.
    check("yalnız \\n ile bölünür", _lines("h\nr1\x0bx\nr2\n"), ["h", "r1\x0bx", "r2"])
    # A changed or dropped old row is refused (sabotage: remove the prefix check and these turn wrong).
    check("değişen eski satır reddedilir", merge(base, ["h", "r1x", "o1"], base + ["m1"], None), None)
    check("silinen eski satır reddedilir", merge(base, ["h"], base + ["m1"], None), None)
    # No main available: theirs then ours.
    check("main yoksa bizim taraf önek", merge(base, base + ["o1"], base + ["t1"], None), base + ["o1", "t1"])

    # End to end through git: a real conflict resolved by the driver, rows in the expected order.
    with tempfile.TemporaryDirectory() as tmp:
        def g(*args: str) -> str:
            return subprocess.run(["git", "-C", tmp, *args], capture_output=True, check=True, text=True).stdout

        g("init", "-q", "-b", "main")
        g("config", "user.email", "t@example.invalid")
        g("config", "user.name", "t")
        script = os.path.abspath(__file__)
        g("config", "merge.append-only.driver", f'"{sys.executable}" "{script}" %O %A %B %P')
        with open(os.path.join(tmp, ".gitattributes"), "w", newline="") as f:
            f.write("rows.tsv merge=append-only\n")
        _write(os.path.join(tmp, "rows.tsv"), ["h", "r1"])
        g("add", ".gitattributes", "rows.tsv")
        g("commit", "-q", "-m", "base")
        g("checkout", "-q", "-b", "ct")
        _write(os.path.join(tmp, "rows.tsv"), ["h", "r1", "o1"])
        g("commit", "-q", "-am", "ours")
        g("checkout", "-q", "main")
        _write(os.path.join(tmp, "rows.tsv"), ["h", "r1", "m1"])
        g("commit", "-q", "-am", "main")
        g("update-ref", "refs/remotes/origin/main", "main")
        g("checkout", "-q", "ct")
        g("merge", "-q", "--no-edit", "main")
        check("git ile uçtan uca", _read(os.path.join(tmp, "rows.tsv")), ["h", "r1", "m1", "o1"])

    print(f"merge_append_only self-test: {cases}/{cases} geçti")
    return 0


if __name__ == "__main__":
    if sys.argv[1:] == ["--self-test"]:
        sys.exit(self_test())
    if len(sys.argv) != 5:
        sys.stderr.write("kullanım: merge_append_only.py %O %A %B %P  |  --self-test\n")
        sys.exit(2)
    sys.exit(run(*sys.argv[1:5]))
