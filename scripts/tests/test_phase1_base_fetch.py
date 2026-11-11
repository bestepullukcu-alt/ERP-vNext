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
        self.assertGreaterEqual(len(re.findall(r"\bfetch_base_tip \"\$GITHUB_BASE_REF\"", text)), 1)


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


if __name__ == "__main__":
    unittest.main()
