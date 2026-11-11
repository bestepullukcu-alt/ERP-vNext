"""The phase1 base fetch never makes a full clone shallow (owner 'Evet, düzelt' 2026-11-06).

scripts/run_phase1_gates.sh compares three steps with the PR base tip and fetches it through ONE function,
fetch_base_tip. CI's checkout is already shallow (actions/checkout, depth 1), and there a depth-1 fetch is right.
In a FULL clone a depth-1 fetch marks the whole repository shallow, and every worktree of that clone shares it.
Measured 2026-11-05: one local run with GITHUB_BASE_REF=main cut main's history at its tip for every tower on the
PC for about 21 hours. pack_approval.py and prompt_gate.py refuse a shallow repository, and `git log` blamed every
file on the tip, until `git fetch --unshallow`.

These tests cut the REAL function out of the shipped script and run it against throwaway repositories in a temporary
folder. Nothing here touches this checkout: the environment drops every GIT_* variable first, because a git hook or
`git rebase -x` exports GIT_DIR / GIT_INDEX_FILE and every git call below would then land in the developer's
repository (review 2026-11-06, reproduced in a throwaway clone). Run by scripts/run_phase1_gates.sh:

    python3 -m unittest discover -s scripts/tests -p 'test_*.py'
"""

from __future__ import annotations

import os
import pathlib
import re
import shutil
import subprocess
import tempfile
import unittest

SCRIPT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "run_phase1_gates.sh")
HELPER = re.compile(r"^fetch_base_tip\(\) \{\n.*?^\}\n", re.S | re.M)
# EES MQ-01: the merge_group block that fills GITHUB_BASE_REF with the group's parent commit (cut out like the helper).
MERGE_GROUP = re.compile(r'^if \[ "\$\{GITHUB_EVENT_NAME:-\}" = "merge_group" \].*?^fi\n', re.S | re.M)
# A fetch however it is written (`git -c x=y fetch`, `git pull`), or any depth option, outside the helper.
FETCHING = re.compile(r"\bgit\b.*\b(?:fetch|pull)\b|--depth\b|--deepen\b|--shallow-|--unshallow\b")
ENV = dict({k: v for k, v in os.environ.items() if not k.startswith("GIT_") and k != "GITHUB_ACTIONS"},
           GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL=os.devnull, GIT_TERMINAL_PROMPT="0",
           GIT_AUTHOR_NAME="phase1-test", GIT_AUTHOR_EMAIL="phase1-test@example.invalid",
           GIT_COMMITTER_NAME="phase1-test", GIT_COMMITTER_EMAIL="phase1-test@example.invalid")


def script_text() -> str:
    with open(SCRIPT, encoding="utf-8") as handle:
        return handle.read()


def git(cwd: str, *args: str) -> str:
    return subprocess.run(["git", *args], cwd=cwd, env=ENV, check=True, capture_output=True, text=True).stdout.strip()


@unittest.skipIf(os.name == "nt", "Windows may start WSL's bash, whose git cannot open file:///C:/ — POSIX only")
@unittest.skipUnless(shutil.which("bash") and shutil.which("git"), "needs bash and git")
class FetchBaseTipTests(unittest.TestCase):
    def setUp(self):
        match = HELPER.search(script_text())
        self.assertIsNotNone(match, "fetch_base_tip() is gone from run_phase1_gates.sh")
        self.helper = match.group(0)
        self.tmp = tempfile.TemporaryDirectory()
        self.origin = os.path.join(self.tmp.name, "origin")
        os.makedirs(self.origin)
        git(self.origin, "init", "-q", "-b", "main")
        for n in range(3):
            self.commit(f"c{n}")

    def tearDown(self):
        self.tmp.cleanup()

    def commit(self, name: str) -> str:
        with open(os.path.join(self.origin, f"{name}.txt"), "w", encoding="utf-8") as handle:
            handle.write(name)
        git(self.origin, "add", f"{name}.txt")
        git(self.origin, "commit", "-q", "-m", name)
        return git(self.origin, "rev-parse", "HEAD")

    def clone(self, name: str, *args: str) -> str:
        path = os.path.join(self.tmp.name, name)
        git(self.tmp.name, "clone", "-q", *args, pathlib.Path(self.origin).as_uri(), path)
        return path

    def fetch(self, clone: str, github_actions: bool = False) -> None:
        env = dict(ENV, GITHUB_ACTIONS="true") if github_actions else ENV
        subprocess.run(["bash", "-c", f"set -euo pipefail\n{self.helper}fetch_base_tip main"],
                       cwd=clone, env=env, check=True, capture_output=True, text=True)

    def test_a_full_clone_stays_full(self):
        clone = self.clone("full")
        tip = self.commit("base-moved")
        self.fetch(clone)
        self.assertEqual(git(clone, "rev-parse", "--is-shallow-repository"), "false",
                         "the base fetch made a full clone shallow — every worktree of it loses main's history")
        self.assertEqual(git(clone, "rev-parse", "FETCH_HEAD"), tip)
        self.assertEqual(git(clone, "rev-list", "--count", "FETCH_HEAD"), "4")

    def test_a_full_clone_stays_full_in_github_actions_too(self):
        clone = self.clone("self-hosted")
        self.commit("base-moved")
        self.fetch(clone, github_actions=True)
        self.assertEqual(git(clone, "rev-parse", "--is-shallow-repository"), "false")

    def test_a_shallow_local_clone_is_not_cut_shorter(self):
        clone = self.clone("local-shallow", "--depth=1")
        tip = self.commit("base-moved")
        self.fetch(clone)
        self.assertEqual(git(clone, "rev-parse", "FETCH_HEAD"), tip)
        self.assertEqual(git(clone, "rev-list", "--count", "FETCH_HEAD"), "2",
                         "outside GitHub Actions a depth-1 fetch cut a shallow clone's main back to one commit")

    def test_the_github_actions_checkout_still_fetches_only_the_tip(self):
        clone = self.clone("ci", "--depth=1")
        tip = self.commit("base-moved")
        self.fetch(clone, github_actions=True)
        self.assertEqual(git(clone, "rev-parse", "--is-shallow-repository"), "true")
        self.assertEqual(git(clone, "rev-parse", "FETCH_HEAD"), tip)
        self.assertEqual(git(clone, "rev-list", "--count", "FETCH_HEAD"), "1",
                         "CI's shallow checkout fetched history — the base tip alone is enough (tree-to-tree)")

    def test_the_github_actions_checkout_fetches_a_merge_group_parent_by_its_sha(self):
        # EES MQ-01: on a merge_group run the base is the group's parent COMMIT (a SHA, not a branch name)
        clone = self.clone("ci-sha", "--depth=1")
        tip = self.commit("group-parent")
        env = dict(ENV, GITHUB_ACTIONS="true")
        subprocess.run(["bash", "-c", f"set -euo pipefail\n{self.helper}fetch_base_tip {tip}"],
                       cwd=clone, env=env, check=True, capture_output=True, text=True)
        self.assertEqual(git(clone, "rev-parse", "FETCH_HEAD"), tip)
        self.assertEqual(git(clone, "rev-parse", "--is-shallow-repository"), "true")


class EveryBaseFetchGoesThroughTheHelperTests(unittest.TestCase):
    def test_no_git_fetch_outside_fetch_base_tip(self):
        text = script_text()
        match = HELPER.search(text)
        self.assertIsNotNone(match, "fetch_base_tip() is gone from run_phase1_gates.sh")
        outside = [line.strip() for line in (text[:match.start()] + text[match.end():]).splitlines()
                   if FETCHING.search(line) and not line.lstrip().startswith("#")]
        self.assertEqual(outside, [], "a step fetches by itself — use fetch_base_tip so a full clone stays full")
        # EES MQ-01 Codex round 1: the base is fetched ONCE (the shared base block), never again inside a step.
        code = "\n".join(line for line in text[match.end():].splitlines() if not line.lstrip().startswith("#"))
        self.assertEqual(len(re.findall(r"\bfetch_base_tip\b", code)), 1,
                         "fetch_base_tip is called more than once — the three base steps must share the one base")
        self.assertEqual(len(re.findall(r"\bfetch_base_tip \"\$GITHUB_BASE_REF\"", text)), 1)


@unittest.skipUnless(shutil.which("bash"), "needs bash")
class MergeGroupBaseTests(unittest.TestCase):
    """EES MQ-01: on a merge_group run GITHUB_BASE_REF is empty (GitHub sets it only for pull_request events); the
    script takes the group's parent commit (MERGE_GROUP_BASE_SHA) instead, and a run without a real SHA is red."""

    SHA = "0123456789abcdef0123456789abcdef01234567"

    def run_block(self, **env: str) -> subprocess.CompletedProcess:
        match = MERGE_GROUP.search(script_text())
        self.assertIsNotNone(match, "the merge_group base block is gone from run_phase1_gates.sh")
        clean = {k: v for k, v in ENV.items() if k not in ("GITHUB_EVENT_NAME", "GITHUB_BASE_REF", "MERGE_GROUP_BASE_SHA")}
        script = f"set -euo pipefail\n{match.group(0)}printf '%s' \"${{GITHUB_BASE_REF:-}}\""
        return subprocess.run(["bash", "-c", script], env=dict(clean, **env), capture_output=True, text=True)

    def test_a_merge_group_run_compares_with_the_group_parent(self):
        done = self.run_block(GITHUB_EVENT_NAME="merge_group", MERGE_GROUP_BASE_SHA=self.SHA)
        self.assertEqual((done.returncode, done.stdout), (0, self.SHA), done.stderr)

    def test_a_merge_group_run_without_a_real_parent_is_red(self):
        for value in ("", "main", "0" * 40, self.SHA[:39], self.SHA.upper(), self.SHA + "\nx"):
            done = self.run_block(GITHUB_EVENT_NAME="merge_group", MERGE_GROUP_BASE_SHA=value)
            self.assertEqual(done.returncode, 1, f"{value!r}: {done.stdout}{done.stderr}")
            self.assertIn("*** FAILED ***", done.stdout)
        done = self.run_block(GITHUB_EVENT_NAME="merge_group")
        self.assertEqual(done.returncode, 1, "MERGE_GROUP_BASE_SHA unset")

    def test_a_pull_request_or_push_run_is_unchanged(self):
        done = self.run_block(GITHUB_EVENT_NAME="pull_request", GITHUB_BASE_REF="main", MERGE_GROUP_BASE_SHA=self.SHA)
        self.assertEqual((done.returncode, done.stdout), (0, "main"))
        done = self.run_block(GITHUB_EVENT_NAME="push", MERGE_GROUP_BASE_SHA=self.SHA)
        self.assertEqual((done.returncode, done.stdout), (0, ""))

    def test_the_block_runs_before_the_first_base_step(self):
        text = script_text()
        match = MERGE_GROUP.search(text)
        self.assertIsNotNone(match)
        self.assertLess(match.end(), text.index("\ncheck_audit_ledger_growth\n"))


# --------------------------------------------------------------------------------------------------- the whole script
# EES MQ-01, Codex round 1 (ENGEL + ÖNEMLİ): the text checks above lock how the base is WRITTEN, not that it REACHES the
# three base steps. These tests run the WHOLE shipped script — every line, in order — in a throwaway folder, with small
# stub programs first on PATH: `git` (fetch outcomes scripted per try, FETCH_HEAD scripted), `python3`, `dotnet` and
# `sleep` (each logs its arguments and succeeds) and an empty stub for every ./scripts/*.sh the script runs. No network,
# no real repository, no build. A line added anywhere in the script that drops or changes the base on its way to the
# steps changes what the stubs log, and SabotageTests below proves each such change turns these tests red.

GIT_STUB = r"""#!/usr/bin/env bash
{ printf 'git'; printf ' %s' "$@"; printf '\n'; } >> "$STUB_DIR/log"
case "$1" in
  fetch)
    n=$(( $(cat "$STUB_DIR/fetch_count") + 1 ))
    echo "$n" > "$STUB_DIR/fetch_count"
    rm -f "$STUB_DIR/fetched"
    if [ "$(sed -n "${n}p" "$STUB_DIR/fetch_outcomes")" = ok ]; then
      : > "$STUB_DIR/fetched"
      exit 0
    fi
    exit 1 ;;
  rev-parse)
    if [ "$2" = "--is-shallow-repository" ]; then echo true; exit 0; fi
    last="${!#}"
    if [ "$last" = 'FETCH_HEAD^{commit}' ]; then
      if [ ! -f "$STUB_DIR/fetched" ]; then exit 1; fi
      cat "$STUB_DIR/fetch_head"
      exit 0
    fi
    echo "${last%^\{commit\}}"
    exit 0 ;;
esac
exit 0
"""
LOGGING_STUB = "#!/usr/bin/env bash\n{{ printf '{name}'; printf ' %s' \"$@\"; printf '\\n'; }} >> \"$STUB_DIR/log\"\nexit 0\n"
SHA = "0123456789abcdef0123456789abcdef01234567"
OTHER = "fedcba9876543210fedcba9876543210fedcba98"


def run_gates(text: str, outcomes: list[str], fetch_head: str = SHA, **env: str) -> dict:
    """Runs the script TEXT end to end with the stubs; returns its exit code, its output and the stubs' log."""
    with tempfile.TemporaryDirectory() as tmp:
        repo, stubs, venv = (os.path.join(tmp, name) for name in ("repo", "stubs", "venv"))
        for folder in (os.path.join(repo, "scripts"), stubs, os.path.join(venv, "bin")):
            os.makedirs(folder)
        files = {os.path.join(repo, "scripts", "run_phase1_gates.sh"): text,
                 os.path.join(stubs, "git"): GIT_STUB,
                 os.path.join(venv, "bin", "python3"): LOGGING_STUB.format(name="python3")}
        for name in ("python3", "dotnet", "sleep"):
            files[os.path.join(stubs, name)] = LOGGING_STUB.format(name=name)
        for name in set(re.findall(r"(?m)^\./scripts/([\w.-]+\.sh)\b", text)):
            files[os.path.join(repo, "scripts", name)] = LOGGING_STUB.format(name=name)
        for path, body in files.items():
            with open(path, "w", encoding="utf-8") as handle:
                handle.write(body)
            os.chmod(path, 0o755)
        for name, body in (("log", ""), ("fetch_count", "0\n"), ("fetch_outcomes", "\n".join(outcomes) + "\n"),
                           ("fetch_head", fetch_head + "\n")):
            with open(os.path.join(stubs, name), "w", encoding="utf-8") as handle:
                handle.write(body)
        clean = {k: v for k, v in ENV.items() if not k.startswith(("GITHUB_", "MERGE_GROUP_", "AUDIT_LEDGER_",
                                                                     "PAGE_HELP_LEDGER_", "AC_TAG_", "PHASE1_"))}
        clean.update(PATH=stubs + os.pathsep + ENV.get("PATH", ""), STUB_DIR=stubs, CT_TOOLS_VENV=venv, **env)
        done = subprocess.run(["bash", os.path.join(repo, "scripts", "run_phase1_gates.sh")], cwd=repo, env=clean,
                              capture_output=True, text=True, timeout=120)
        with open(os.path.join(stubs, "log"), encoding="utf-8") as handle:
            log = handle.read().splitlines()
    return {"code": done.returncode, "out": done.stdout + done.stderr, "log": log}


def calls(run: dict, needle: str) -> list[str]:
    return [line for line in run["log"] if needle in line]


def base_reached_every_step(run: dict, sha: str) -> list[str]:
    """What is wrong (empty = nothing) with a run that must pass with `sha` as the base of all three base steps."""
    problems = []
    if run["code"] != 0 or "[phase1] gates passed" not in run["out"]:
        problems.append(f"exit {run['code']}, not a passed run")
    if "SKIPPED" in run["out"]:
        problems.append("a step said SKIPPED")
    if f"[phase1] audit ledger growth: ok — no list line added against {sha}" not in run["out"]:
        problems.append("the audit ledger step did not compare with the base")
    ceiling = calls(run, "scripts/check_page_help_ledger_ceiling.py")
    if ceiling != [f"python3 scripts/check_page_help_ledger_ceiling.py --base {sha}"]:
        problems.append(f"page-help ceiling ran as {ceiling}")
    tags = calls(run, "scripts/ac_coverage.py check-tags")
    if tags != [f"python3 -B scripts/ac_coverage.py check-tags --base {sha}"]:
        problems.append(f"acceptance criteria tags ran as {tags}")
    if len(calls(run, "git fetch")) > 3:
        problems.append("more than three fetches")
    return problems


def red_on_an_unfetchable_base(run: dict) -> list[str]:
    """What is wrong (empty = nothing) with a run whose three fetches all failed: it must stop red before any step."""
    problems = []
    if run["code"] != 1:
        problems.append(f"exit {run['code']}, expected 1")
    if "[phase1] base: *** FAILED ***" not in run["out"]:
        problems.append("no '*** FAILED ***' base message")
    if "SKIPPED" in run["out"] or "[phase1] gates passed" in run["out"]:
        problems.append("a step skipped / the run went on")
    if calls(run, "scripts/check_page_help_ledger_ceiling.py") or calls(run, "check-tags"):
        problems.append("a base step ran without the base")
    if len(calls(run, "git fetch")) != 3 or calls(run, "sleep") != ["sleep 5", "sleep 5"]:
        problems.append(f"tries {calls(run, 'git fetch')}, sleeps {calls(run, 'sleep')} — expected 3 tries 5 s apart")
    return problems


PULL_REQUEST = dict(GITHUB_EVENT_NAME="pull_request", GITHUB_BASE_REF="main")
MERGE_GROUP_RUN = dict(GITHUB_EVENT_NAME="merge_group", MERGE_GROUP_BASE_SHA=SHA)


@unittest.skipIf(os.name == "nt", "the stubs are bash scripts — POSIX only")
@unittest.skipUnless(shutil.which("bash"), "needs bash")
class OneVerifiedBaseReachesEveryStepTests(unittest.TestCase):
    def test_a_base_that_cannot_be_fetched_is_red_on_a_pull_request(self):
        run = run_gates(script_text(), ["fail", "fail", "fail"], **PULL_REQUEST)
        self.assertEqual(red_on_an_unfetchable_base(run), [], run["out"])

    def test_a_base_that_cannot_be_fetched_is_red_on_a_merge_group(self):
        run = run_gates(script_text(), ["fail", "fail", "fail"], **MERGE_GROUP_RUN)
        self.assertEqual(red_on_an_unfetchable_base(run), [], run["out"])

    def test_a_fetch_without_a_commit_behind_it_is_red(self):
        run = run_gates(script_text(), ["ok", "ok", "ok"], fetch_head="not-a-sha", **PULL_REQUEST)
        self.assertEqual(run["code"], 1, run["out"])
        self.assertIn("[phase1] base: *** FAILED ***", run["out"])

    def test_the_third_try_gives_all_three_steps_the_same_verified_sha(self):
        run = run_gates(script_text(), ["fail", "fail", "ok"], **PULL_REQUEST)
        self.assertEqual(base_reached_every_step(run, SHA), [], run["out"])
        self.assertEqual(calls(run, "git fetch"), ["git fetch --no-tags origin main"] * 3)
        self.assertEqual(calls(run, "sleep"), ["sleep 5", "sleep 5"])

    def test_a_first_try_success_fetches_once(self):
        run = run_gates(script_text(), ["ok"], **PULL_REQUEST)
        self.assertEqual(base_reached_every_step(run, SHA), [], run["out"])
        self.assertEqual((len(calls(run, "git fetch")), calls(run, "sleep")), (1, []))

    def test_a_merge_group_run_gives_all_three_steps_the_group_parent(self):
        run = run_gates(script_text(), ["fail", "ok"], **MERGE_GROUP_RUN)
        self.assertEqual(base_reached_every_step(run, SHA), [], run["out"])
        self.assertEqual(calls(run, "git fetch"), [f"git fetch --no-tags origin {SHA}"] * 2)

    def test_a_merge_group_fetch_that_lands_on_another_commit_is_red(self):
        run = run_gates(script_text(), ["ok"], fetch_head=OTHER, **MERGE_GROUP_RUN)
        self.assertEqual(run["code"], 1, run["out"])
        self.assertIn("not the merge group's parent", run["out"])

    def test_a_run_without_a_base_keeps_its_no_base_paths(self):
        run = run_gates(script_text(), [], GITHUB_EVENT_NAME="push")
        self.assertEqual(run["code"], 0, run["out"])
        self.assertEqual(calls(run, "git fetch"), [])
        self.assertIn("audit ledger growth: *** SKIPPED *** — no base branch", run["out"])
        self.assertEqual(calls(run, "scripts/check_page_help_ledger_ceiling.py"),
                         ["python3 scripts/check_page_help_ledger_ceiling.py"])
        self.assertEqual(calls(run, "check-tags"), ["python3 -B scripts/ac_coverage.py check-tags"])

    def test_the_local_overrides_still_win(self):
        overrides = dict(AUDIT_LEDGER_BASE="a" * 40, PAGE_HELP_LEDGER_BASE="b" * 40, AC_TAG_BASE="c" * 40)
        for event in ({}, PULL_REQUEST):
            run = run_gates(script_text(), ["ok"], **overrides, **event)
            self.assertEqual(run["code"], 0, run["out"])
            self.assertIn(f"no list line added against {'a' * 40}", run["out"])
            self.assertEqual(calls(run, "scripts/check_page_help_ledger_ceiling.py"),
                             [f"python3 scripts/check_page_help_ledger_ceiling.py --base {'b' * 40}"])
            self.assertEqual(calls(run, "check-tags"), [f"python3 -B scripts/ac_coverage.py check-tags --base {'c' * 40}"])
        # A PHASE1_BASE_SHA from the caller's environment is never a base: the script sets it itself.
        run = run_gates(script_text(), [], GITHUB_EVENT_NAME="push", PHASE1_BASE_SHA=OTHER)
        self.assertNotIn(OTHER, "\n".join(run["log"]) + run["out"])


BLOCK_END = 'readonly PHASE1_BASE_SHA="$phase1_base"\n'
MERGE_GROUP_END = '  GITHUB_BASE_REF="$merge_group_base"\nfi\n'
NO_READONLY = ('readonly PHASE1_BASE_SHA="$phase1_base"', 'PHASE1_BASE_SHA="$phase1_base"')


def mutant(text: str, *edits: tuple[str, str]) -> str:
    for old, new in edits:
        assert text.count(old) == 1, f"sabotage anchor {old!r} is not in the script exactly once — update the sabotage"
        text = text.replace(old, new)
    return text


@unittest.skipIf(os.name == "nt", "the stubs are bash scripts — POSIX only")
@unittest.skipUnless(shutil.which("bash"), "needs bash")
class SabotageTests(unittest.TestCase):
    """Each change below breaks the one-base rule; each must make the behaviour checks above report a problem. The
    anchors must be found exactly once (a sabotage that does not apply proves nothing)."""

    PASSING = [
        ("unset GITHUB_BASE_REF after its assignment", PULL_REQUEST,
         [(MERGE_GROUP_END, MERGE_GROUP_END + "unset GITHUB_BASE_REF\n")]),
        ("empty GITHUB_BASE_REF after its assignment (merge_group)", MERGE_GROUP_RUN,
         [(MERGE_GROUP_END, MERGE_GROUP_END + 'GITHUB_BASE_REF=""\n')]),
        ("unset PHASE1_BASE_SHA after the base block", PULL_REQUEST, [(BLOCK_END, BLOCK_END + "unset PHASE1_BASE_SHA\n")]),
        ("another SHA after the base block", PULL_REQUEST, [(BLOCK_END, BLOCK_END + f"PHASE1_BASE_SHA={OTHER}\n")]),
        ("not read-only + another SHA before the tags step", PULL_REQUEST,
         [NO_READONLY, ("\ncheck_ac_tags\n", f"\nPHASE1_BASE_SHA={OTHER}\ncheck_ac_tags\n")]),
        ("not read-only + an empty base before the ceiling step", MERGE_GROUP_RUN,
         [NO_READONLY, ("\ncheck_page_help_ledger_ceiling\n", '\nPHASE1_BASE_SHA=""\ncheck_page_help_ledger_ceiling\n')]),
        ("the tags step stops reading the base", PULL_REQUEST,
         [('local base="${AC_TAG_BASE:-$PHASE1_BASE_SHA}"', 'local base="${AC_TAG_BASE:-}"')]),
        ("the audit step stops reading the base", PULL_REQUEST,
         [('local base="${AUDIT_LEDGER_BASE:-$PHASE1_BASE_SHA}"', 'local base="${AUDIT_LEDGER_BASE:-}"')]),
        ("one try only", PULL_REQUEST, [("for base_try in 1 2 3; do", "for base_try in 1; do")]),
    ]

    def test_each_sabotage_of_a_passing_run_is_seen(self):
        for name, event, edits in self.PASSING:
            with self.subTest(name):
                run = run_gates(mutant(script_text(), *edits), ["fail", "fail", "ok"], **event)
                self.assertNotEqual(base_reached_every_step(run, SHA), [], f"sabotage '{name}' went unseen:\n{run['out']}")

    def test_a_base_failure_that_no_longer_stops_the_run_is_seen(self):
        edits = [('checks would compare nothing."\n    exit 1\n', 'checks would compare nothing."\n')]
        # pull_request: on a merge_group run the parent check right after it still stops the run red (defence in depth).
        run = run_gates(mutant(script_text(), *edits), ["fail", "fail", "fail"], **PULL_REQUEST)
        self.assertNotEqual(red_on_an_unfetchable_base(run), [], run["out"])

    def test_a_merge_group_without_the_parent_check_is_seen(self):
        edits = [('if [ "${GITHUB_EVENT_NAME:-}" = "merge_group" ] && [ "$base_fetched" != "${MERGE_GROUP_BASE_SHA:-}" ]; then',
                  "if false; then")]
        run = run_gates(mutant(script_text(), *edits), ["ok"], fetch_head=OTHER, **MERGE_GROUP_RUN)
        self.assertEqual(run["code"], 0, "the sabotage did not apply")


if __name__ == "__main__":
    unittest.main()
