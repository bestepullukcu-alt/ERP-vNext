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
import atexit
import unittest
from unittest import mock

SCRIPT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "run_phase1_gates.sh")
HELPER = re.compile(r"^fetch_base_tip\(\) \{\n.*?^\}\n", re.S | re.M)
# EES MQ-01: the merge_group block that fills GITHUB_BASE_REF with the group's parent commit (cut out like the helper).
MERGE_GROUP = re.compile(r'^if \[ "\$\{GITHUB_EVENT_NAME:-\}" = "merge_group" \].*?^fi\n', re.S | re.M)
# A fetch however it is written (`git -c x=y fetch`, `git pull`), or any depth option, outside the helper.
FETCHING = re.compile(r"\bgit\b.*\b(?:fetch|pull)\b|--depth\b|--deepen\b|--shallow-|--unshallow\b")
# What bash reads at start-up before the script's first line (Codex round 3 on d3ffd9c7d): a BASH_ENV file can export
# GIT_DIR again after the GIT_* clean-up, and an exported function (BASH_FUNC_git%%) replaces the git command itself.
BASH_STARTUP = ("BASH_ENV", "ENV", "SHELLOPTS", "BASHOPTS", "CDPATH")
FUNC_PREFIX = "BASH_FUNC_"
# `bash -c` reads ~/.bashrc when SSH_CLIENT is set and SHLVL is unset or 0; git reads attributes and ignore files under
# HOME / XDG_CONFIG_HOME and /etc/gitattributes (blind pre-check of d01b736b8). Both get an empty home of their own.
SSH_MARKERS = ("SSH_CLIENT", "SSH2_CLIENT", "SSH_CONNECTION")
EMPTY_HOME = tempfile.mkdtemp(prefix="phase1-home-")
atexit.register(shutil.rmtree, EMPTY_HOME, True)


def isolated_env(source) -> dict:
    """SOURCE without anything that could point git or bash at another repository, plus a fixed identity and an empty
    home. Git config, attributes and ignore files outside the throwaway repositories are not read: GIT_CONFIG_GLOBAL
    needs git 2.32 (GIT_232)."""
    return dict({k: v for k, v in source.items() if not k.startswith(("GIT_", FUNC_PREFIX)) and k != "GITHUB_ACTIONS"
                 and k not in BASH_STARTUP and k not in SSH_MARKERS},
                HOME=EMPTY_HOME, XDG_CONFIG_HOME=EMPTY_HOME, GIT_ATTR_NOSYSTEM="1",
                GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL=os.devnull, GIT_TERMINAL_PROMPT="0",
                GIT_AUTHOR_NAME="phase1-test", GIT_AUTHOR_EMAIL="phase1-test@example.invalid",
                GIT_COMMITTER_NAME="phase1-test", GIT_COMMITTER_EMAIL="phase1-test@example.invalid")


ENV = isolated_env(os.environ)


def git_at_least(major: int, minor: int) -> bool:
    if not shutil.which("git"):
        return False
    out = subprocess.run(["git", "--version"], env=ENV, capture_output=True, text=True).stdout
    found = re.search(r"(\d+)\.(\d+)", out)
    return bool(found) and (int(found.group(1)), int(found.group(2))) >= (major, minor)


GIT_232 = git_at_least(2, 32)          # GIT_CONFIG_GLOBAL: below 2.32 the developer's global config would be read


def script_text() -> str:
    with open(SCRIPT, encoding="utf-8") as handle:
        return handle.read()


def git(cwd: str, *args: str) -> str:
    return subprocess.run(["git", *args], cwd=cwd, env=ENV, check=True, capture_output=True, text=True).stdout.strip()


@unittest.skipIf(os.name == "nt", "Windows may start WSL's bash, whose git cannot open file:///C:/ — POSIX only")
@unittest.skipUnless(shutil.which("bash") and GIT_232, "needs bash and git 2.32+")
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
    script takes the group's parent commit (MERGE_GROUP_BASE_SHA) instead — whatever GITHUB_BASE_REF holds — and a run
    without a real SHA is red."""

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

    def test_a_merge_group_run_takes_the_group_parent_even_when_github_base_ref_is_set(self):
        # blind pre-check 5: should GitHub one day fill GITHUB_BASE_REF on merge_group, the queue still compares with
        # the group's parent (and does not go red forever on a parent check against a branch tip).
        done = self.run_block(GITHUB_EVENT_NAME="merge_group", GITHUB_BASE_REF="main", MERGE_GROUP_BASE_SHA=self.SHA)
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
# stub programs first on PATH: `git`, `python3`, `dotnet` and `sleep` (each logs its arguments) and an empty stub for
# every ./scripts/*.sh the script runs. No network, no real repository, no build. The git stub is strict (blind
# pre-check 1): fetch outcomes are scripted per try (a failure writes "fatal: stub fetch failure <try>" to stderr),
# FETCH_HEAD is scripted, and `rev-parse --verify X^{commit}`, `cat-file -e X:path` and `diff X …` succeed only when X
# is one of the run's known commits — a step that compares with anything else fails, it is not quietly answered. A line
# anywhere in the script that drops or changes the base on its way to the steps changes what the stubs log, and
# SabotageTests below proves each such change turns these tests red.

GIT_STUB = r"""#!/usr/bin/env bash
{ printf 'git'; printf ' %s' "$@"; printf '\n'; } >> "$STUB_DIR/log"
known() { grep -qxF -- "$1" "$STUB_DIR/commits"; }
case "$1" in
  fetch)
    n=$(( $(cat "$STUB_DIR/fetch_count") + 1 ))
    echo "$n" > "$STUB_DIR/fetch_count"
    rm -f "$STUB_DIR/fetched"
    if [ "$(sed -n "${n}p" "$STUB_DIR/fetch_outcomes")" = ok ]; then
      : > "$STUB_DIR/fetched"
      exit 0
    fi
    echo "fatal: stub fetch failure $n" >&2
    exit 128 ;;
  rev-parse)
    if [ "$2" = "--is-shallow-repository" ]; then echo true; exit 0; fi
    last="${!#}"
    if [ "$last" = 'FETCH_HEAD^{commit}' ]; then
      if [ ! -f "$STUB_DIR/fetched" ]; then exit 1; fi
      cat "$STUB_DIR/fetch_head"
      exit 0
    fi
    if [ "$2" = "--verify" ] && [[ "$last" == *"^{commit}" ]] && known "${last%"^{commit}"}"; then
      echo "${last%"^{commit}"}"
      exit 0
    fi
    exit 128 ;;
  cat-file)
    obj="${!#}"
    if known "${obj%%:*}"; then exit 0; fi
    exit 128 ;;
  diff)
    shift
    first=""
    list=no
    for arg in "$@"; do
      if [ "$arg" = "--raw" ]; then list=yes; fi
      if [ -z "$first" ] && [[ "$arg" != -* ]]; then first="$arg"; fi
    done
    if ! known "$first"; then echo "fatal: stub: bad revision '$first'" >&2; exit 128; fi
    mode="$(cat "$STUB_DIR/diff_mode")"
    if [ "$list" = yes ]; then
      if [ "$mode" = fail_list ]; then exit 128; fi
      if [ "$mode" = fail_file ]; then printf ':100644 100644 0000000 0000000 M\0%s\0' "tests/architecture/audit-ledger/Diten.Stub.md"; fi
      exit 0
    fi
    if [ "$mode" = fail_file ]; then exit 128; fi
    exit 0 ;;
esac
exit 0
"""
LOGGING_STUB = "#!/usr/bin/env bash\n{{ printf '{name}'; printf ' %s' \"$@\"; printf '\\n'; }} >> \"$STUB_DIR/log\"\nexit 0\n"
SHA = "0123456789abcdef0123456789abcdef01234567"
OTHER = "fedcba9876543210fedcba9876543210fedcba98"
LEDGER = "tests/architecture/audit-ledger"


def run_gates(text: str, outcomes: list[str], fetch_head: str = SHA, commits: list[str] | None = None,
              diff_mode: str = "", **env: str) -> dict:
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
        known = [fetch_head] if commits is None else commits
        for name, body in (("log", ""), ("fetch_count", "0\n"), ("fetch_outcomes", "\n".join(outcomes) + "\n"),
                           ("fetch_head", fetch_head + "\n"), ("commits", "".join(c + "\n" for c in known)),
                           ("diff_mode", diff_mode + "\n")):
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


def base_reached_every_step(run: dict, sha: str, ref: str) -> list[str]:
    """What is wrong (empty = nothing) with a run that must pass with `ref` fetched, resolved to `sha`, and `sha` as the
    base of all three base steps — read from the commands that CONSUME the base, not from what the script prints."""
    problems = []
    if run["code"] != 0 or "[phase1] gates passed" not in run["out"]:
        problems.append(f"exit {run['code']}, not a passed run")
    if "SKIPPED" in run["out"] or "FAILED" in run["out"]:
        problems.append("a step said SKIPPED / FAILED")
    fetches = calls(run, "git fetch")
    if not fetches or len(fetches) > 3 or any(not line.endswith(f" origin {ref}") for line in fetches):
        problems.append(f"fetched {fetches}, expected 1-3 fetches of {ref}")
    audit = [f"git rev-parse --verify --quiet {sha}^{{commit}}", f"git cat-file -e {sha}:{LEDGER}",
             f"git diff --raw -z --no-renames --no-ext-diff {sha} HEAD -- {LEDGER}/Diten.*.md"]
    if [line for line in run["log"] if line.startswith(("git rev-parse --verify", "git cat-file", "git diff"))
            and "FETCH_HEAD" not in line] != audit:
        problems.append("the audit ledger step did not compare with the base: "
                        + str([line for line in run["log"] if line.startswith(("git cat-file", "git diff"))]))
    ceiling = calls(run, "scripts/check_page_help_ledger_ceiling.py")
    if ceiling != [f"python3 scripts/check_page_help_ledger_ceiling.py --base {sha}"]:
        problems.append(f"page-help ceiling ran as {ceiling}")
    tags = calls(run, "scripts/ac_coverage.py check-tags")
    if tags != [f"python3 -B scripts/ac_coverage.py check-tags --base {sha}"]:
        problems.append(f"acceptance criteria tags ran as {tags}")
    return problems


def red_on_an_unfetchable_base(run: dict) -> list[str]:
    """What is wrong (empty = nothing) with a run whose three fetches all failed: it must stop red before any step and
    show the last try's git error (blind pre-check 4)."""
    problems = []
    if run["code"] != 1:
        problems.append(f"exit {run['code']}, expected 1")
    if "[phase1] base: *** FAILED ***" not in run["out"]:
        problems.append("no '*** FAILED ***' base message")
    if "[phase1]   last try, git: fatal: stub fetch failure 3" not in run["out"] or "stub fetch failure 2" in run["out"]:
        problems.append("the last try's git error is not shown (alone)")
    if "SKIPPED" in run["out"] or "[phase1] gates passed" in run["out"]:
        problems.append("a step skipped / the run went on")
    if calls(run, "scripts/check_page_help_ledger_ceiling.py") or calls(run, "check-tags") or calls(run, "git diff"):
        problems.append("a base step ran without the base")
    if len(calls(run, "git fetch")) != 3 or calls(run, "sleep") != ["sleep 5", "sleep 5"]:
        problems.append(f"tries {calls(run, 'git fetch')}, sleeps {calls(run, 'sleep')} — expected 3 tries 5 s apart")
    return problems


def red_with(run: dict, message: str) -> list[str]:
    """What is wrong (empty = nothing) with a run that must stop red (exit 1) with `message` before the builds."""
    problems = []
    if run["code"] != 1:
        problems.append(f"exit {run['code']}, expected 1")
    if message not in run["out"]:
        problems.append(f"no {message!r}")
    if "[phase1] gates passed" in run["out"] or calls(run, "dotnet"):
        problems.append("the run went on")
    return problems


PULL_REQUEST = dict(GITHUB_EVENT_NAME="pull_request", GITHUB_BASE_REF="main")
MERGE_GROUP_RUN = dict(GITHUB_EVENT_NAME="merge_group", MERGE_GROUP_BASE_SHA=SHA)
OVERRIDES = ("AUDIT_LEDGER_BASE", "PAGE_HELP_LEDGER_BASE", "AC_TAG_BASE")


def ref_of(event: dict) -> str:
    return event["MERGE_GROUP_BASE_SHA"] if event.get("GITHUB_EVENT_NAME") == "merge_group" else event["GITHUB_BASE_REF"]


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
        self.assertEqual(red_with(run, "[phase1] base: *** FAILED ***"), [], run["out"])

    def test_the_third_try_gives_all_three_steps_the_same_verified_sha(self):
        run = run_gates(script_text(), ["fail", "fail", "ok"], **PULL_REQUEST)
        self.assertEqual(base_reached_every_step(run, SHA, "main"), [], run["out"])
        self.assertEqual(calls(run, "git fetch"), ["git fetch --no-tags origin main"] * 3)
        self.assertEqual(calls(run, "sleep"), ["sleep 5", "sleep 5"])

    def test_a_first_try_success_fetches_once(self):
        run = run_gates(script_text(), ["ok"], **PULL_REQUEST)
        self.assertEqual(base_reached_every_step(run, SHA, "main"), [], run["out"])
        self.assertEqual((len(calls(run, "git fetch")), calls(run, "sleep")), (1, []))

    def test_github_actions_fetches_the_tip_at_depth_1_and_still_reaches_every_step(self):
        run = run_gates(script_text(), ["ok"], GITHUB_ACTIONS="true", **PULL_REQUEST)
        self.assertEqual(base_reached_every_step(run, SHA, "main"), [], run["out"])
        self.assertEqual(calls(run, "git fetch"), ["git fetch --no-tags --depth=1 origin main"])

    def test_a_merge_group_run_gives_all_three_steps_the_group_parent(self):
        run = run_gates(script_text(), ["fail", "ok"], **MERGE_GROUP_RUN)
        self.assertEqual(base_reached_every_step(run, SHA, SHA), [], run["out"])
        self.assertEqual(calls(run, "git fetch"), [f"git fetch --no-tags origin {SHA}"] * 2)

    def test_a_merge_group_run_ignores_a_filled_github_base_ref(self):
        run = run_gates(script_text(), ["ok"], GITHUB_BASE_REF="main", **MERGE_GROUP_RUN)
        self.assertEqual(base_reached_every_step(run, SHA, SHA), [], run["out"])

    def test_a_merge_group_fetch_that_lands_on_another_commit_is_red(self):
        run = run_gates(script_text(), ["ok"], fetch_head=OTHER, **MERGE_GROUP_RUN)
        self.assertEqual(red_with(run, "not the merge group's parent"), [], run["out"])

    def test_with_the_verified_base_an_audit_step_that_cannot_compare_is_red(self):
        # blind pre-check 2: base not a commit / the ledger diff / one file's diff cannot be produced → FAILED, exit 1
        for kwargs, message in (
                (dict(commits=[]), f"audit ledger growth: *** FAILED *** — base '{SHA}' is not a commit"),
                (dict(diff_mode="fail_list"), "audit ledger growth: *** FAILED *** — the ledger diff against"),
                (dict(diff_mode="fail_file"), "audit ledger growth: *** FAILED *** — the diff of")):
            with self.subTest(message):
                run = run_gates(script_text(), ["ok"], **kwargs, **PULL_REQUEST)
                self.assertEqual(red_with(run, message), [], run["out"])

    def test_without_a_verified_base_those_audit_paths_still_skip(self):
        for kwargs, base, message in (
                (dict(commits=[]), OTHER, f"audit ledger growth: *** SKIPPED *** — base '{OTHER}' is not a commit"),
                (dict(commits=[OTHER], diff_mode="fail_list"), OTHER,
                 "audit ledger growth: *** SKIPPED *** — the ledger diff against")):
            with self.subTest(message):
                run = run_gates(script_text(), [], GITHUB_EVENT_NAME="push", AUDIT_LEDGER_BASE=base, **kwargs)
                self.assertEqual(run["code"], 0, run["out"])
                self.assertIn(message, run["out"])
                self.assertIn("[phase1] gates passed", run["out"])

    def test_a_run_without_a_base_keeps_its_no_base_paths(self):
        run = run_gates(script_text(), [], GITHUB_EVENT_NAME="push")
        self.assertEqual(run["code"], 0, run["out"])
        self.assertEqual(calls(run, "git fetch"), [])
        self.assertIn("audit ledger growth: *** SKIPPED *** — no base branch", run["out"])
        self.assertEqual(calls(run, "scripts/check_page_help_ledger_ceiling.py"),
                         ["python3 scripts/check_page_help_ledger_ceiling.py"])
        self.assertEqual(calls(run, "check-tags"), ["python3 -B scripts/ac_coverage.py check-tags"])

    def test_the_local_overrides_still_win(self):
        a, b, c = "a" * 40, "b" * 40, "c" * 40
        for event in ({}, PULL_REQUEST):
            run = run_gates(script_text(), ["ok"], commits=[SHA, a], AUDIT_LEDGER_BASE=a, PAGE_HELP_LEDGER_BASE=b,
                            AC_TAG_BASE=c, **event)
            self.assertEqual(run["code"], 0, run["out"])
            self.assertIn(f"git diff --raw -z --no-renames --no-ext-diff {a} HEAD -- {LEDGER}/Diten.*.md", run["log"])
            self.assertEqual(calls(run, "scripts/check_page_help_ledger_ceiling.py"),
                             [f"python3 scripts/check_page_help_ledger_ceiling.py --base {b}"])
            self.assertEqual(calls(run, "check-tags"), [f"python3 -B scripts/ac_coverage.py check-tags --base {c}"])
        # A PHASE1_BASE_SHA from the caller's environment is never a base: the script sets it itself.
        run = run_gates(script_text(), [], GITHUB_EVENT_NAME="push", PHASE1_BASE_SHA=OTHER)
        self.assertNotIn(OTHER, "\n".join(run["log"]) + run["out"])

    def test_in_github_actions_an_override_is_red(self):
        # blind pre-check 3: in CI an override would replace the verified base; the value itself is never printed
        for name in OVERRIDES:
            for event in (PULL_REQUEST, MERGE_GROUP_RUN, dict(GITHUB_EVENT_NAME="push")):
                with self.subTest(name=name, event=event.get("GITHUB_EVENT_NAME")):
                    run = run_gates(script_text(), ["ok"], GITHUB_ACTIONS="true", **{name: OTHER}, **event)
                    self.assertEqual(red_with(run, f"[phase1] base: *** FAILED *** — {name} is set in GitHub Actions"),
                                     [], run["out"])
                    self.assertEqual(calls(run, "git fetch"), [])
                    self.assertNotIn(OTHER, run["out"])


BLOCK_END = 'readonly PHASE1_BASE_SHA="$phase1_base"\n'
MERGE_GROUP_END = '  GITHUB_BASE_REF="$merge_group_base"\nfi\n'
NO_READONLY = ('readonly PHASE1_BASE_SHA="$phase1_base"', 'PHASE1_BASE_SHA="$phase1_base"')


def mutant(text: str, *edits: tuple[str, str]) -> str:
    for old, new in edits:
        assert text.count(old) == 1, f"sabotage anchor {old!r} is not in the script exactly once — update the sabotage"
        text = text.replace(old, new)
    return text


def run_gates_real_git(text: str, base_ledger: str, head_ledger: str, older_ledger: str | None = None,
                       merge_group: bool = False, dirty: str | None = None, attributes: str | None = None,
                       ledger_name: str = "Diten.Stub.md", symlink: bool = False) -> dict:
    """Runs the script TEXT end to end with REAL git (Codex round 2 on b1b7a495e). A bare origin's main holds
    `base_ledger`; the clone's HEAD holds `head_ledger`. Only python3, dotnet, sleep and the sibling .sh scripts are
    stubs: the base is fetched from origin, resolved and compared by git itself.

    With `older_ledger` the base has MOVED on (blind pre-check of 5612aab8b): the PR branch starts at an older commit
    holding `older_ledger`, so the fetched base is neither HEAD's parent nor the merge base. With `merge_group` the base
    is a queue commit off main (MERGE_GROUP_BASE_SHA) and origin/main stays at the older commit. The clone is made
    BEFORE the base moves on, so its local `main` and `origin/main` are stale until the script's own fetch (Codex
    round 3 on d3ffd9c7d: a clone made after it let `"$GITHUB_BASE_REF"` stand in for the base).

    `dirty` starts bash from a polluted environment, cleaned by isolated_env: "bash_env" (a BASH_ENV file exporting
    GIT_DIR of another clone) or "function" (an exported `git` function). `leaked` says whether it got through."""
    with tempfile.TemporaryDirectory() as tmp:
        seed, origin, repo, stubs, venv = (os.path.join(tmp, n) for n in ("seed", "origin.git", "repo", "stubs", "venv"))
        ledger = os.path.join(LEDGER, ledger_name)
        os.makedirs(os.path.join(seed, LEDGER))
        commit = ("-c", "core.hooksPath=/dev/null", "commit", "-q")
        git(tmp, "init", "-q", "-b", "main", seed)
        pathlib.Path(seed, ledger).write_text(base_ledger if older_ledger is None else older_ledger, encoding="utf-8")
        git(seed, "add", ledger)
        git(seed, *commit, "-m", "older")
        start = git(seed, "rev-parse", "HEAD")
        base = start
        git(tmp, "clone", "-q", "--bare", seed, origin)
        git(tmp, "clone", "-q", origin, repo)            # local main = origin/main = start, from here on stale
        if older_ledger is not None:
            pathlib.Path(seed, ledger).write_text(base_ledger, encoding="utf-8")
            git(seed, *commit, "-am", "base")
            base = git(seed, "rev-parse", "HEAD")
            # merge_group: the group's parent lives on a queue branch and origin's main stays behind
            git(seed, "push", "-q", origin, "HEAD:refs/heads/" + ("queue" if merge_group else "main"))
        git(repo, "checkout", "-q", "-b", "pr", start)
        if symlink:          # Codex round 4 on 44d7ba95f: the ledger becomes a link to a file that carries the new line
            pathlib.Path(repo, "real.md").write_text(head_ledger, encoding="utf-8")
            os.remove(os.path.join(repo, ledger))
            os.symlink(os.path.relpath(os.path.join(repo, "real.md"), os.path.join(repo, LEDGER)), os.path.join(repo, ledger))
            git(repo, "add", "real.md")
        else:
            pathlib.Path(repo, ledger).write_text(head_ledger, encoding="utf-8")
        pathlib.Path(repo, "other.txt").write_text("x\n", encoding="utf-8")
        if attributes is not None:                       # the change's own .gitattributes
            pathlib.Path(repo, ".gitattributes").write_text(attributes, encoding="utf-8")
            git(repo, "add", ".gitattributes")
        git(repo, "add", ledger, "other.txt")
        git(repo, *commit, "-m", "head")
        for folder in (os.path.join(repo, "scripts"), stubs, os.path.join(venv, "bin")):
            os.makedirs(folder, exist_ok=True)
        files = {os.path.join(repo, "scripts", "run_phase1_gates.sh"): text,
                 os.path.join(venv, "bin", "python3"): LOGGING_STUB.format(name="python3")}
        for name in ("python3", "dotnet", "sleep"):
            files[os.path.join(stubs, name)] = LOGGING_STUB.format(name=name)
        for name in set(re.findall(r"(?m)^\./scripts/([\w.-]+\.sh)\b", text)):
            files[os.path.join(repo, "scripts", name)] = LOGGING_STUB.format(name=name)
        for path, body in files.items():
            with open(path, "w", encoding="utf-8") as handle:
                handle.write(body)
            os.chmod(path, 0o755)
        pathlib.Path(stubs, "log").write_text("", encoding="utf-8")
        start_env, other, marker = ENV, os.path.join(tmp, "other"), os.path.join(tmp, "marker")
        if dirty:
            git(tmp, "clone", "-q", origin, other)
            if dirty == "bash_env":
                evil = os.path.join(tmp, "evil.sh")
                pathlib.Path(evil).write_text(f"export GIT_DIR='{other}/.git'\n", encoding="utf-8")
                polluted = dict(os.environ, BASH_ENV=evil)
            elif dirty == "xdg_attributes":            # the developer's own git attributes hide the ledger diff
                xdg = os.path.join(tmp, "xdg")
                os.makedirs(os.path.join(xdg, "git"))
                pathlib.Path(xdg, "git", "attributes").write_text("*.md -diff\n", encoding="utf-8")
                polluted = dict(os.environ, XDG_CONFIG_HOME=xdg)
            else:
                polluted = dict(os.environ, **{"BASH_FUNC_git%%": f'() {{  : > "{marker}"; command git "$@"\n}}'})
            start_env = isolated_env(polluted)
        clean = {k: v for k, v in start_env.items() if not k.startswith(("GITHUB_", "MERGE_GROUP_", "AUDIT_LEDGER_",
                                                                           "PAGE_HELP_LEDGER_", "AC_TAG_", "PHASE1_"))}
        event = dict(GITHUB_EVENT_NAME="merge_group", MERGE_GROUP_BASE_SHA=base) if merge_group else PULL_REQUEST
        clean.update(PATH=stubs + os.pathsep + ENV.get("PATH", ""), STUB_DIR=stubs, CT_TOOLS_VENV=venv, **event)
        done = subprocess.run(["bash", os.path.join(repo, "scripts", "run_phase1_gates.sh")], cwd=repo, env=clean,
                              capture_output=True, text=True, timeout=120)
        leaked = os.path.exists(os.path.join(other, ".git", "FETCH_HEAD")) or os.path.exists(marker)
    return {"code": done.returncode, "out": done.stdout + done.stderr, "leaked": leaked}


DEBT = ("- OldCmd\n", "- OldCmd\n- NewCmd\n")          # (base, head): one list line added
MOVED = ("- OldCmd\n- Other\n", "- Other\n- OldCmd\n")   # the same lines in another order: not growth
# (base, head, older): NewCmd was audited away on the base after the PR branched; the PR still carries it
BASE_MOVED_ON = ("- OldCmd\n", "- OldCmd\n- NewCmd\n", "- OldCmd\n- NewCmd\n")
DEBT_RUNS = {"pull request, base = HEAD's parent": dict(),
             "pull request, base moved on": dict(older=True),
             "merge group, parent off main": dict(older=True, merge_group=True)}


def run_debt(text: str, older: bool = False, merge_group: bool = False, **extra) -> dict:
    if older:
        base, head, earlier = BASE_MOVED_ON
        return run_gates_real_git(text, base, head, older_ledger=earlier, merge_group=merge_group, **extra)
    return run_gates_real_git(text, *DEBT, **extra)


# Codex round 4 on 44d7ba95f: names git quotes in --name-only output (non-ASCII, a double quote, a tab)
ODD_NAMES = ("Diten.Sécurité.md", 'Diten.a"b.md', "Diten.a\tb.md")


HIDING_ATTRIBUTES = ("*.md -diff\n", "tests/architecture/audit-ledger/** binary\n")
# the ledger diff without the flags that ignore attributes: isolation alone must keep the developer's attributes out
WITHOUT_TEXT = ("git diff --text --no-textconv --no-ext-diff --unified=0", "git diff --unified=0")


def red_on_new_debt(run: dict) -> list[str]:
    """What is wrong (empty = nothing) with a run whose HEAD carries `- NewCmd` that the fetched base does not have."""
    problems = []
    if run["code"] != 1:
        problems.append(f"exit {run['code']}, expected 1")
    if "audit ledger growth: FAILED" not in run["out"] or "- NewCmd" not in run["out"]:
        problems.append("the added line was not reported")
    return problems


@unittest.skipIf(os.name == "nt", "Windows may start WSL's bash, whose git cannot open file:///C:/ — POSIX only")
@unittest.skipUnless(shutil.which("bash") and GIT_232, "needs bash and git 2.32+")
class TheFetchedBaseReachesTheLedgerContentTests(unittest.TestCase):
    """Codex round 2 on b1b7a495e: the per-file diff of the audit ledger, the comparison that finds new debt, must
    read the fetched base — not HEAD, HEAD's parent, the merge base or origin/main. Real git, as in CI."""

    def test_a_line_the_fetched_base_does_not_have_is_red(self):
        for name, how in DEBT_RUNS.items():
            with self.subTest(name):
                run = run_debt(script_text(), **how)
                self.assertEqual(red_on_new_debt(run), [], run["out"])

    def test_a_polluted_bash_start_reaches_no_other_repository(self):
        # Codex round 3 on d3ffd9c7d: BASH_ENV exporting GIT_DIR made the run fetch into another clone
        for dirty in ("bash_env", "function"):
            with self.subTest(dirty):
                run = run_debt(script_text(), older=True, dirty=dirty)
                self.assertFalse(run["leaked"], run["out"])
                self.assertEqual(red_on_new_debt(run), [], run["out"])
        for dirty, sabotage in (("bash_env", ("BASH_STARTUP", ())), ("function", ("FUNC_PREFIX", "NO_SUCH_PREFIX_"))):
            with self.subTest(sabotage=dirty), mock.patch.dict(globals(), dict([sabotage])):
                self.assertTrue(run_debt(script_text(), older=True, dirty=dirty)["leaked"],
                                f"without the {sabotage[0]} clean-up the probe saw nothing")

    def test_the_changes_own_gitattributes_cannot_hide_a_line(self):
        # blind pre-check of d01b736b8: `*.md -diff` in the PR turned the ledger diff into "Binary files differ"
        for attributes in HIDING_ATTRIBUTES:
            with self.subTest(attributes=attributes.strip()):
                run = run_debt(script_text(), older=True, attributes=attributes)
                self.assertEqual(red_on_new_debt(run), [], run["out"])

    def test_the_developers_git_attributes_and_bashrc_are_not_read(self):
        run = run_debt(mutant(script_text(), WITHOUT_TEXT), older=True, dirty="xdg_attributes")
        self.assertEqual(red_on_new_debt(run), [], run["out"])
        # Codex round 4 on 44d7ba95f: the negative control now starts from a minimal environment (PATH, the polluted
        # HOME, SSH_CLIENT, no SHLVL) in a throwaway cwd, so nothing else of the caller's environment can run in it.
        with tempfile.TemporaryDirectory() as home:
            marker = os.path.join(home, "function-ran")
            pathlib.Path(home, ".bashrc").write_text("export GIT_DIR=/hijacked\n", encoding="utf-8")
            minimal = {"PATH": os.environ.get("PATH", "/usr/bin:/bin"), "HOME": home, "SSH_CLIENT": "10.0.0.1 22 22"}
            probe = ["bash", "-c", 'echo "${GIT_DIR:-}"']
            leaked = subprocess.run(probe, env=minimal, cwd=home, capture_output=True, text=True).stdout.strip()
            polluted = dict({k: v for k, v in os.environ.items() if k != "SHLVL"}, HOME=home,
                            SSH_CLIENT="10.0.0.1 22 22", **{"BASH_FUNC_echo%%": f'() {{  : > "{marker}"; builtin echo "$@"\n}}'})
            got = subprocess.run(probe, env=isolated_env(polluted), cwd=home, capture_output=True, text=True).stdout.strip()
            function_ran = os.path.exists(marker)
        self.assertEqual(got, "", "isolated_env let ~/.bashrc in")
        self.assertFalse(function_ran, "isolated_env let an exported function run")
        if leaked != "/hijacked":
            self.skipTest("this bash does not read ~/.bashrc for `bash -c` over SSH; nothing to sabotage")

    def test_a_ledger_turned_into_a_symbolic_link_is_red(self):
        run = run_debt(script_text(), older=True, symlink=True)
        self.assertEqual(run["code"], 1, run["out"])
        self.assertIn("is not a regular file in this change (git mode 120000)", run["out"])

    def test_a_line_added_to_a_ledger_with_an_odd_name_is_red(self):
        for name in ODD_NAMES:
            with self.subTest(name=name):
                run = run_debt(script_text(), older=True, ledger_name=name)
                self.assertEqual(red_on_new_debt(run), [], run["out"])

    def test_the_same_lines_in_another_order_pass(self):
        run = run_gates_real_git(script_text(), *MOVED)
        self.assertEqual(run["code"], 0, run["out"])
        self.assertIn("[phase1] audit ledger growth: ok", run["out"])
        self.assertIn("[phase1] gates passed", run["out"])


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
        # blind pre-check 1: the audit step consumes something other than the base
        ("the audit diff compares HEAD with HEAD", PULL_REQUEST,
         [('git diff --raw -z --no-renames --no-ext-diff "$base" ${head:+"$head"}',
           'git diff --raw -z --no-renames --no-ext-diff HEAD ${head:+"$head"}')]),
        ("the audit step verifies another ref", PULL_REQUEST,
         [('git rev-parse --verify --quiet "$base^{commit}"', 'git rev-parse --verify --quiet "${base}x^{commit}"')]),
        # blind pre-check 5: merge_group takes GITHUB_BASE_REF again when it is filled
        ("merge_group defers to a filled GITHUB_BASE_REF", dict(MERGE_GROUP_RUN, GITHUB_BASE_REF="main"),
         [('if [ "${GITHUB_EVENT_NAME:-}" = "merge_group" ]; then\n',
           'if [ "${GITHUB_EVENT_NAME:-}" = "merge_group" ] && [ -z "${GITHUB_BASE_REF:-}" ]; then\n')]),
    ]

    def test_each_sabotage_of_a_passing_run_is_seen(self):
        for name, event, edits in self.PASSING:
            with self.subTest(name):
                run = run_gates(mutant(script_text(), *edits), ["fail", "fail", "ok"], **event)
                self.assertNotEqual(base_reached_every_step(run, SHA, ref_of(event)), [],
                                    f"sabotage '{name}' went unseen:\n{run['out']}")

    def test_a_base_failure_that_no_longer_stops_the_run_is_seen(self):
        edits = [("last try, git: /'\n    exit 1\n", "last try, git: /'\n")]
        # pull_request: on a merge_group run the parent check right after it still stops the run red (defence in depth).
        run = run_gates(mutant(script_text(), *edits), ["fail", "fail", "fail"], **PULL_REQUEST)
        self.assertNotEqual(red_on_an_unfetchable_base(run), [], run["out"])

    def test_a_failure_without_the_git_error_is_seen(self):
        edits = [("    printf '%s\\n' \"$base_fetch_err\" | sed 's/^/[phase1]   last try, git: /'\n", "")]
        run = run_gates(mutant(script_text(), *edits), ["fail", "fail", "fail"], **PULL_REQUEST)
        self.assertNotEqual(red_on_an_unfetchable_base(run), [], run["out"])

    def test_a_merge_group_without_the_parent_check_is_seen(self):
        edits = [('if [ "${GITHUB_EVENT_NAME:-}" = "merge_group" ] && [ "$base_fetched" != "${MERGE_GROUP_BASE_SHA:-}" ]; then',
                  "if false; then")]
        run = run_gates(mutant(script_text(), *edits), ["ok"], fetch_head=OTHER, **MERGE_GROUP_RUN)
        self.assertNotEqual(red_with(run, "not the merge group's parent"), [], run["out"])

    def test_an_audit_step_that_skips_green_with_the_verified_base_is_seen(self):
        run = run_gates(mutant(script_text(), ("    unable_status=1\n", "    unable_status=0\n")), ["ok"], commits=[],
                        **PULL_REQUEST)
        self.assertNotEqual(red_with(run, "is not a commit"), [], run["out"])

    @unittest.skipUnless(GIT_232, "needs git 2.32+")
    def test_a_ledger_file_diff_that_leaves_the_base_is_seen(self):
        # Codex round 2 on b1b7a495e + blind pre-check of 5612aab8b: the per-file diff reads something other than the
        # fetched base. FETCH_HEAD is left out: in CI it is the base itself (an equivalent mutant).
        line = 'git diff --text --no-textconv --no-ext-diff --unified=0 "$base" ${head:+"$head"} -- ":(literal)$file"'
        for name, written in (("HEAD", line.replace('"$base"', "HEAD")),
                              ("HEAD's parent", line.replace('"$base"', "HEAD~1")),
                              ("the merge base (three dots)", line.replace('"$base" ${head:+"$head"}',
                                                                           '"$base"...${head:-HEAD}')),
                              ("origin/main", line.replace('"$base"', "origin/main")),
                              ("the base branch name", line.replace('"$base"', '"$GITHUB_BASE_REF"'))):
            with self.subTest(name):
                mutated = mutant(script_text(), (line, written))
                seen = [run for how in DEBT_RUNS.values() if red_on_new_debt(run := run_debt(mutated, **how))]
                self.assertNotEqual(seen, [], f"sabotage '{name}' went unseen")

    @unittest.skipUnless(GIT_232, "needs git 2.32+")
    def test_a_ledger_diff_that_obeys_the_changes_gitattributes_is_seen(self):
        # blind pre-check of d01b736b8: without --text the PR's `*.md -diff` hides the added line
        mutated = mutant(script_text(), WITHOUT_TEXT)
        for attributes in HIDING_ATTRIBUTES:
            with self.subTest(attributes=attributes.strip()):
                run = run_debt(mutated, older=True, attributes=attributes)
                self.assertEqual(run["code"], 0, run["out"])                 # the mutant really goes green ...
                self.assertNotEqual(red_on_new_debt(run), [], run["out"])   # ... and the check sees it

    @unittest.skipUnless(GIT_232, "needs git 2.32+")
    def test_an_isolation_that_keeps_the_developers_attributes_is_seen(self):
        real = isolated_env

        def leaky(source):
            env = real(source)
            env.pop("GIT_ATTR_NOSYSTEM")
            env["XDG_CONFIG_HOME"] = source.get("XDG_CONFIG_HOME", env["XDG_CONFIG_HOME"])
            return env

        with mock.patch.dict(globals(), isolated_env=leaky):
            run = run_debt(mutant(script_text(), WITHOUT_TEXT), older=True, dirty="xdg_attributes")
        self.assertNotEqual(red_on_new_debt(run), [], "the developer's attributes did not reach git: the probe is blind")

    @unittest.skipUnless(GIT_232, "needs git 2.32+")
    def test_a_ledger_step_without_the_mode_check_is_seen(self):
        # Codex round 4 on 44d7ba95f: without the mode check the symbolic link passes green
        mutated = mutant(script_text(), ("      100644|100755|000000) ;;\n      *)\n", "      *) ;;\n      100644)\n"))
        run = run_debt(mutated, older=True, symlink=True)
        self.assertEqual(run["code"], 0, run["out"])                    # the mutant really goes green ...
        self.assertNotIn("git mode 120000", run["out"])                 # ... so the test above would see it

    @unittest.skipUnless(GIT_232, "needs git 2.32+")
    def test_a_ledger_step_that_reads_quoted_names_is_seen(self):
        # Codex round 4 on 44d7ba95f: the old listing (--name-only, newline-separated) misses an odd name's new line
        old = ('''  while IFS= read -r -d '' meta && IFS= read -r -d '' file; do
    new_mode="${meta#:* }"''', '''  git diff --name-only "$base" ${head:+"$head"} -- "$ledger_dir/Diten.*.md" > "$changed"
  while IFS= read -r file; do
    new_mode=100644
    meta="${meta:-}"''')
        seen = [name for name in ODD_NAMES if red_on_new_debt(run_debt(mutant(script_text(), old), older=True,
                                                                      ledger_name=name))]
        self.assertNotEqual(seen, [], "the quoted-name mutant went red for no odd name: the test is blind")

    def test_an_override_accepted_in_github_actions_is_seen(self):
        run = run_gates(mutant(script_text(), ('    if [ -n "${!base_override:-}" ]; then', "    if false; then")),
                        ["ok"], GITHUB_ACTIONS="true", AC_TAG_BASE=OTHER, **PULL_REQUEST)
        self.assertNotEqual(red_with(run, "AC_TAG_BASE is set in GitHub Actions"), [], run["out"])


if __name__ == "__main__":
    unittest.main()
