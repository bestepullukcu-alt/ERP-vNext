#!/usr/bin/env python3
"""EES 2.13 (owner 'Evet, başla' 2026-11-09) - which service-test suites a changed-paths rule WOULD select.

SHADOW MODE: this script only records an answer (.github/workflows/ci-selection-shadow.yml uploads it). It does not
change what .github/workflows/service-tests.yml runs - every suite still runs on every pull request.

    python3 scripts/ci_select_suites.py --base origin/main --head HEAD [--json selection.json] [--summary file.md]

Changed files = `git diff --name-only --no-renames base...head` (a rename counts as both its old and its new path).
The rule, fail closed:
  * FULL (every suite) when the diff cannot be computed (base unreachable, git error), when the suite table cannot be
    read or is out of step with service-tests.yml, when a changed file is a global trigger (the workflow, this script,
    its tables, the skip check, MSBuild-wide files: *.props, *.targets, global.json, nuget.config, *.sln), or when a
    changed file under services/ or gateway/ is named by no suite glob (whole-tree scans do not count) and by no line
    of .github/ci/service-paths-without-suite.tsv - an unknown code path.
  * otherwise SELECTIVE: the suites with at least one glob matching a changed file (possibly none).
Output: JSON {mode, reason, selected, skipped, changed_files_count, evidence} on stdout (and --json), a short Markdown
table appended to --summary. Exit 0 whatever the selection; a non-zero exit is a bug in this script.
Stdlib only. Tests: scripts/tests/test_ci_select_suites.py.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SUITES_TSV = ".github/ci/service-suites.tsv"
NO_SUITE_TSV = ".github/ci/service-paths-without-suite.tsv"
WORKFLOW = ".github/workflows/service-tests.yml"

# A change to any of these can change what every suite does, or how this answer is computed.
GLOBAL_TRIGGERS = (
    "AGENTS.md",  # every suite finds the repository root by this file
    ".github/workflows/service-tests.yml",
    ".github/ci/**",
    "scripts/ci_select_suites.py",
    "scripts/check_trx_no_skipped.py",
    "scripts/ci-skip-allowlist.tsv",
    "**/*.props",
    "**/*.targets",
    "**/global.json",
    "**/nuget.config",
    "**/*.sln",
)

CODE_ROOTS = ("services/", "gateway/")
OS_VALUES = ("linux", "macos")


class TableError(ValueError):
    """A suite table that cannot be trusted - the caller falls back to FULL."""


def glob_regex(glob: str) -> re.Pattern[str]:
    """`**/` = zero or more directories, `**` = anything, `*` / `?` = within one path segment; case-insensitive."""
    out = []
    i = 0
    while i < len(glob):
        if glob.startswith("**/", i):
            out.append("(?:[^/]*/)*")
            i += 3
        elif glob.startswith("**", i):
            out.append(".*")
            i += 2
        elif glob[i] == "*":
            out.append("[^/]*")
            i += 1
        elif glob[i] == "?":
            out.append("[^/]")
            i += 1
        else:
            out.append(re.escape(glob[i]))
            i += 1
    return re.compile("^" + "".join(out) + "$", re.IGNORECASE)


def glob_match(glob: str, path: str) -> bool:
    return bool(glob_regex(glob).match(path))


def fixed_prefix(glob: str) -> str:
    """The literal part of a glob before its first wildcard, cut back to a whole directory."""
    cut = min([glob.index(c) for c in "*?" if c in glob] or [len(glob)])
    head = glob[:cut]
    return head if cut == len(glob) else head[: head.rfind("/") + 1]


def is_tree_scan(glob: str) -> bool:
    """A whole-tree scan selects its suite but does not make a services/ or gateway/ path known."""
    return fixed_prefix(glob).lower() in ("",) + CODE_ROOTS


def _check_glob(glob: str, where: str) -> None:
    if (not glob or glob != glob.strip() or glob.startswith(("/", "./", "!")) or "\\" in glob or "//" in glob
            or ".." in glob.split("/") or any(c in glob for c in "[]{}")):
        raise TableError(f"{where}: unsupported glob {glob!r}")


def _data_lines(text: str, header: str):
    seen_header = False
    for number, raw in enumerate(text.split("\n"), 1):
        line = raw.rstrip("\r")
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        if not seen_header:
            if line != header:
                raise TableError(f"line {number}: expected the header {header!r}")
            seen_header = True
            continue
        yield number, line
    if not seen_header:
        raise TableError("no header line")


def parse_suites(text: str) -> list[tuple[str, str, list[str]]]:
    """[(suite, os, [globs])] in file order; any malformed line raises TableError."""
    rows = []
    names = set()
    for number, line in _data_lines(text, "suite\tos\tglobs"):
        cells = line.split("\t")
        if len(cells) != 3:
            raise TableError(f"line {number}: {len(cells)} columns, expected 3")
        suite, os_name, globs = cells
        if not re.fullmatch(r"[a-z0-9][a-z0-9-]*", suite):
            raise TableError(f"line {number}: bad suite name {suite!r}")
        if suite in names:
            raise TableError(f"line {number}: suite {suite} listed twice")
        if os_name not in OS_VALUES:
            raise TableError(f"line {number}: os {os_name!r} is not one of {OS_VALUES}")
        parts = globs.split(",")
        for glob in parts:
            _check_glob(glob, f"line {number}")
        names.add(suite)
        rows.append((suite, os_name, parts))
    if not rows:
        raise TableError("no suite rows")
    return rows


def parse_no_suite(text: str) -> list[str]:
    globs = []
    for number, line in _data_lines(text, "glob\treason"):
        cells = line.split("\t")
        if len(cells) != 2 or not cells[1].strip():
            raise TableError(f"line {number}: expected glob<TAB>reason")
        _check_glob(cells[0], f"line {number}")
        globs.append(cells[0])
    return globs


def workflow_suites(text: str) -> dict[str, str]:
    """{suite: os} from service-tests.yml - every `- suite:` matrix row, with its job's runner family."""
    suites: dict[str, str] = {}
    jobs = re.search(r"(?m)^jobs:\n", text)
    if not jobs:
        return suites
    section = text[jobs.end():]
    keys = list(re.finditer(r"(?m)^  ([A-Za-z0-9_-]+):\s*$", section))
    for index, key in enumerate(keys):
        body = section[key.start(): keys[index + 1].start() if index + 1 < len(keys) else len(section)]
        runner = re.search(r"(?m)^    runs-on:\s*(\S+)", body)
        family = "macos" if runner and runner.group(1).startswith("macos") else "linux"
        for match in re.finditer(r"(?m)^ +- suite: *(\S+)\s*$", body):
            suites.setdefault(match.group(1), family)
    return suites


def read_text(relative: str, root: str = ROOT) -> str:
    with open(os.path.join(root, relative), encoding="utf-8") as handle:
        return handle.read()


def changed_files(base: str, head: str, root: str = ROOT) -> tuple[list[str] | None, str]:
    """(files, "") or (None, why) when the diff cannot be computed."""
    for ref in (base, head):
        probe = subprocess.run(["git", "rev-parse", "--verify", "--quiet", ref + "^{commit}"], cwd=root,
                               capture_output=True, text=True)
        if probe.returncode != 0:
            return None, f"ref {ref!r} is not reachable"
    diff = subprocess.run(["git", "-c", "core.quotepath=off", "diff", "--name-only", "--no-renames", "-z",
                           f"{base}...{head}"], cwd=root, capture_output=True, text=True)
    if diff.returncode != 0:
        return None, f"git diff {base}...{head} failed: {diff.stderr.strip()[:200]}"
    return sorted({name for name in diff.stdout.split("\0") if name}), ""


def select(files: list[str], suites: list[tuple[str, str, list[str]]], no_suite: list[str]) -> dict:
    """The selection for a list of changed paths (pure; no git, no files)."""
    names = [suite for suite, _, _ in suites]

    def full(reason: str) -> dict:
        return {"mode": "full", "reason": reason, "selected": names, "skipped": [], "changed_files_count": len(files),
                "evidence": {}}

    globals_hit = [f for f in files if any(glob_match(g, f) for g in GLOBAL_TRIGGERS)]
    if globals_hit:
        return full(f"global trigger changed: {globals_hit[0]}"
                    + (f" (+{len(globals_hit) - 1} more)" if len(globals_hit) > 1 else ""))

    compiled = [(suite, [(glob_regex(g), is_tree_scan(g)) for g in globs]) for suite, _, globs in suites]
    exempt = [glob_regex(g) for g in no_suite]
    evidence: dict[str, str] = {}
    unknown = []
    for path in files:
        known = False
        for suite, patterns in compiled:
            for regex, tree_scan in patterns:
                if regex.match(path):
                    evidence.setdefault(suite, path)
                    known = known or not tree_scan
        if path.startswith(CODE_ROOTS) and not known and not any(r.match(path) for r in exempt):
            unknown.append(path)
    if unknown:
        return full(f"unknown code path (no suite row names it): {unknown[0]}"
                    + (f" (+{len(unknown) - 1} more)" if len(unknown) > 1 else ""))

    selected = [n for n in names if n in evidence]
    return {"mode": "selective",
            "reason": f"{len(selected)} of {len(names)} suites matched {len(files)} changed file(s)",
            "selected": selected, "skipped": [n for n in names if n not in evidence],
            "changed_files_count": len(files), "evidence": {n: evidence[n] for n in selected}}


def compute(base: str, head: str, root: str = ROOT) -> dict:
    files, why = changed_files(base, head, root)
    try:
        workflow = workflow_suites(read_text(WORKFLOW, root))
    except OSError as error:
        workflow, why_wf = {}, str(error)
    else:
        why_wf = ""
    try:
        suites = parse_suites(read_text(SUITES_TSV, root))
        no_suite = parse_no_suite(read_text(NO_SUITE_TSV, root))
    except (OSError, TableError) as error:
        names = sorted(workflow) or ["*"]  # "*" = every suite: nothing could be read, the consumer must run all
        return _fallback(names, files, f"suite table unreadable: {error}")
    table = {suite: os_name for suite, os_name, _ in suites}
    if not workflow or table != workflow:
        drift = sorted(set(table) ^ set(workflow)) or sorted(s for s in table if table[s] != workflow.get(s))
        return _fallback([s for s, _, _ in suites], files,
                         f"{SUITES_TSV} is out of step with {WORKFLOW}: {', '.join(drift[:5]) or why_wf or 'no suites read'}")
    if files is None:
        return _fallback([s for s, _, _ in suites], files, f"diff unavailable: {why}")
    return select(files, suites, no_suite)


def _fallback(names: list[str], files: list[str] | None, reason: str) -> dict:
    return {"mode": "full", "reason": reason, "selected": names, "skipped": [],
            "changed_files_count": len(files) if files is not None else None, "evidence": {}}


def summary_markdown(result: dict) -> str:
    total = len(result["selected"]) + len(result["skipped"])
    lines = [
        "### Service-test selection (shadow mode - nothing is skipped yet)",
        "",
        "| Mode | Selected | Changed files | Reason |",
        "|---|---|---|---|",
        f"| {result['mode']} | {len(result['selected'])} / {total} | {result['changed_files_count']} | "
        f"{result['reason'].replace('|', '/')} |",
        "",
    ]
    if result["mode"] == "selective" and result["selected"]:
        lines += ["| Suite | First matching file |", "|---|---|"]
        lines += [f"| {s} | `{result['evidence'].get(s, '')}` |" for s in result["selected"]]
        lines.append("")
    return "\n".join(lines) + "\n"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    parser.add_argument("--base", required=True)
    parser.add_argument("--head", required=True)
    parser.add_argument("--json", dest="json_out")
    parser.add_argument("--summary")
    args = parser.parse_args(argv)

    result = compute(args.base, args.head)
    text = json.dumps(result, indent=2, ensure_ascii=False)
    print(text)
    if args.json_out:
        with open(args.json_out, "w", encoding="utf-8") as handle:
            handle.write(text + "\n")
    if args.summary:
        try:
            with open(args.summary, "a", encoding="utf-8") as handle:
                handle.write(summary_markdown(result))
        except OSError as error:
            print(f"[ci_select_suites] summary not written: {error}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
