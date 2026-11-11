"""Pins every decision of scripts/hooks/command_brake.py (EES 2.4), including the bypass forms found by the
independent review of 2026-11-09 and the false positives it reported."""
import json
import pathlib
import subprocess
import sys
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts" / "hooks"))
import command_brake as cb  # noqa: E402

DENY = [
    "gh pr merge 412 --merge", "cd x && gh pr merge 1", "gh -R o/r pr merge 1",
    "gh pr review 12 --approve", "gh pr review 12 -a -b ok", "gh pr review 12 -ab ok", "gh -R o/r pr review 12 --approve",
    "gh api -X PUT repos/o/r/pulls/5/merge", "gh api repos/o/r/pulls/5/merge -X PUT",
    "gh api repos/o/r/pulls/5/reviews -f event=APPROVE", "gh api repos/o/r/pulls/5/reviews -f event=approve",
    "gh api graphql -f query='mutation { mergePullRequest(input:{}) { clientMutationId } }'",
    "gh api graphql -f query='mutation { enablePullRequestAutoMerge(input:{}) { clientMutationId } }'",
    "gh api repos/o/r/branches/main/protection -X DELETE", "gh api --method=DELETE repos/o/r/branches/main/protection",
    "gh api -X PUT repos/o/r/branches/main/protection", "gh api -X PATCH repos/o/r",
    "git push --force origin feature/x", "git push -f", "git push -uf origin x", "git push --force-with-lease origin x",
    "git push origin +feature/x", "git push origin +main",
    "git push origin --delete old", "git push origin :old",
    "git push --tags", "git push --all origin", "git push --mirror origin",
    "git push origin main", "git push origin HEAD:main", "git push origin refs/heads/master", "git push origin main:main",
    'git push origin "main"', "git push origin 'main'", "git push origin main;echo ok", "git push origin main&&echo ok",
    'bash -c "git push origin main"', "git push \\\n origin main", "/usr/bin/git push origin main",
    "git -C ../other push origin main", "GIT_TRACE=1 git push origin main", "env FOO=1 git push origin main",
    "gh release create v1", "gh repo edit --visibility private",
    "killall -9 dotnet", "echo a; pkill -f dotnet", 'bash -c "pkill -f dotnet"', "nohup pkill dotnet",
    "pgrep dotnet | xargs killall", "kill $(pgrep dotnet)",
    "gh pr review 12 --approve=true", "gh api -XDELETE repos/o/r/branches/main/protection",
    "gh api -X POST repos/o/r/merges -f base=main -f head=x", "gh api -X PATCH repos/o/r/git/refs/heads/main -f sha=1",
    "gh api -X DELETE repos/o/r/git/refs/heads/feature-x", "sudo -u ali git push origin main",
    "eval 'git push origin main'", "cd ../x && git push origin main",
    "gh api -X POST repos/o/r/pulls/5/reviews --input r.json", "gh api --input b.json -X PUT repos/o/r/pulls/5/reviews/1/events",
    "gh secret set TOKEN", "gh api -X PUT repos/o/r/collaborators/someone",
]
DENY_ON_MAIN = ["git push", "git push origin", "git push origin HEAD", "git push origin @", "git push -o ci.skip origin",
                "git push 2>&1 | tail -5", "git push origin 2>&1", "git push > /tmp/log", "git push origin &> out.txt"]
ASK = [
    "git reset --hard origin/main", "git clean -fd", "git clean --force", "git branch -D old",
    "git checkout -- .", "git checkout .", "git restore .", "git stash drop",
    "printf x | xargs -I{} git push origin {}",
    "git push origin `echo main`", "gh api graphql -F query=@m.graphql", "echo 'git push origin main' | bash",
    "git -c alias.p=push p origin main", "git checkout -f other", "git switch --discard-changes x", "git reset --merge",
]
ALLOW = [
    "git push origin chore/ees/s2-guvenlik-01", "git push -u origin feature/main-menu", "git push origin HEAD",
    "gh pr view 12 --json reviews", "gh pr create --draft --title x", "git status", "kill 12345",
    "grep -n killall run_watch.sh", "git reset --soft HEAD~1", "gh api repos/o/r/branches/main",
    'git commit -m "fix push to main bug"', 'git commit -m "docs: git push -f is forbidden"',
    "git log --grep=push origin/main main", "rg 'gh pr merge' docs | head", "git restore --staged .",
    "gh api repos/o/r/pulls/5/reviews -f event=COMMENT -f body='please approve this'",
    "git push origin feature/x 2>&1 | tail -5", "gh pr merge --help", "bash scripts/dev-up.sh",
    "gh api repos/o/r/pulls/5/reviews", "gh api graphql -f query='{ viewer { login } }'",
]


class Decisions(unittest.TestCase):
    def check(self, cases, expected, branch="feature/x"):
        for c in cases:
            with self.subTest(c=c):
                v = cb.decide(c, branch=branch)
                self.assertEqual(v[0] if v else None, expected)

    def test_deny(self):
        self.check(DENY, "deny")

    def test_deny_on_main(self):
        self.check(DENY_ON_MAIN, "deny", branch="main")

    def test_ask(self):
        self.check(ASK, "ask")

    def test_allow(self):
        self.check(ALLOW, None)


class Protocol(unittest.TestCase):
    def run_hook(self, payload):
        p = subprocess.run([sys.executable, str(ROOT / "scripts" / "hooks" / "command_brake.py")],
                           input=payload, capture_output=True, text=True, timeout=10)
        return p.returncode, p.stdout

    def decision(self, tool, tool_input):
        rc, out = self.run_hook(json.dumps({"tool_name": tool, "tool_input": tool_input}))
        self.assertEqual(rc, 0)
        return json.loads(out)["hookSpecificOutput"]["permissionDecision"] if out else None

    def test_deny_and_ask_json(self):
        self.assertEqual(self.decision("Bash", {"command": "gh pr merge 1"}), "deny")
        self.assertEqual(self.decision("Bash", {"command": "git clean -fd"}), "ask")

    def test_every_shell_tool_is_braked(self):
        for tool in ("Bash", "Monitor", "PowerShell", "mcp__terminal__run_in_terminal"):
            self.assertEqual(self.decision(tool, {"command": "gh pr merge 1 --merge"}), "deny", tool)
            self.assertIsNone(self.decision(tool, {"command": "git status"}), tool)
        self.assertEqual(self.run_hook(json.dumps({"tool_name": "Monitor", "tool_input": "x"})), (0, ""))

    def test_auto_merge_tool_is_denied(self):
        self.assertEqual(self.decision("mcp__ccd_pr__set_auto_merge", {}), "deny")

    def test_other_tools_and_bad_input_pass(self):
        self.assertIsNone(self.decision("Read", {}))
        self.assertEqual(self.run_hook("not json"), (0, ""))
        self.assertEqual(self.run_hook("[1, 2]"), (0, ""))


class RepoSettings(unittest.TestCase):
    """The brake is on for everyone who opens the repository: .claude/settings.json registers it as a PreToolUse hook
    for Bash and the auto-merge tool, and the registered command, run the way Claude Code runs it, denies a merge."""

    def hooks(self):
        settings = json.loads((ROOT / ".claude" / "settings.json").read_text(encoding="utf-8"))
        return [(e.get("matcher", ""), h) for e in settings.get("hooks", {}).get("PreToolUse", [])
                for h in e.get("hooks", []) if h.get("type") == "command" and "scripts/hooks/command_brake.py" in h.get("command", "")]

    def entries(self):
        return [(m, h["command"]) for m, h in self.hooks()]

    def run_registered(self, payload, env_extra=None, path=None):
        """Every registered command, run the way Claude Code runs it: bash -c, the hook's own timeout."""
        import os
        hooks = self.hooks()
        self.assertTrue(hooks, "command_brake.py is not a PreToolUse hook in .claude/settings.json")
        env = {**os.environ, "CLAUDE_PROJECT_DIR": str(ROOT), **(env_extra or {})}
        if path is not None:
            env["PATH"] = path
        import shutil
        bash = shutil.which("bash")
        if not bash:
            self.skipTest("no bash on this machine (Claude Code runs hooks with bash; CI has it)")
        return [subprocess.run([bash, "-c", h["command"]], input=payload.encode("utf-8"), capture_output=True,
                               timeout=h["timeout"], env=env) for _, h in hooks]

    def decision_of(self, p):
        self.assertEqual(p.returncode, 0, p.stderr)
        self.assertTrue(p.stdout, "the brake printed nothing")
        return json.loads(p.stdout)["hookSpecificOutput"]["permissionDecision"]

    def test_hook_is_synchronous_with_a_real_timeout(self):
        # an async hook's decision blocks nothing; a tiny timeout kills the brake before it answers (Codex R1-2)
        for _, h in self.hooks():
            self.assertFalse(h.get("async"), "the brake must not be async")
            self.assertFalse(h.get("asyncRewake"), "the brake must not run in the background (Codex R2-2)")
            self.assertTrue(isinstance(h.get("timeout"), (int, float)) and 5 <= h["timeout"] <= 30, h.get("timeout"))

    def test_decision_survives_a_non_utf8_console(self):
        # Codex R1-1: under cp1252 the Turkish reason raised UnicodeEncodeError and the decision was lost
        payload = json.dumps({"tool_name": "Bash", "tool_input": {"command": "gh pr merge 1 --merge"}}, ensure_ascii=False)
        cp1252 = {"PYTHONIOENCODING": "cp1252", "PYTHONUTF8": "0", "LC_ALL": "C"}
        for p in self.run_registered(payload, cp1252):
            self.assertEqual(self.decision_of(p), "deny")
            p.stdout.decode("ascii")   # the decision itself is plain ASCII
        # input side: UTF-8 bytes that cp1252 cannot decode (Á = C3 81) must not make the brake pass a push to main
        payload = json.dumps({"tool_name": "Bash", "tool_input": {"command": "echo Á 中文 && git push origin main"}},
                             ensure_ascii=False)
        for p in self.run_registered(payload, cp1252):
            self.assertEqual(self.decision_of(p), "deny")

    def test_a_failing_brake_runs_once_and_stays_failed(self):
        # exec: when the script itself fails, the next interpreter must not re-run it on an empty stdin and hide it
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            fake = pathlib.Path(d, "scripts", "hooks", "command_brake.py")
            fake.parent.mkdir(parents=True)
            fake.write_text("import pathlib, sys\nwith open(pathlib.Path(__file__).with_name('runs'), 'a') as f:\n"
                            "    f.write('x')\nsys.exit(1)\n")
            for p in self.run_registered("{}", {"CLAUDE_PROJECT_DIR": d}):
                self.assertNotEqual(p.returncode, 0)
            self.assertEqual(fake.with_name("runs").read_text(), "x" * len(self.hooks()))

    def test_no_usable_python_passes_with_a_warning(self):
        import os, tempfile
        with tempfile.TemporaryDirectory() as d:
            for name in ("python3", "python"):
                stub = pathlib.Path(d, name)
                stub.write_text("#!/bin/sh\nexit 1\n")
                stub.chmod(0o755)
            for p in self.run_registered("{}", path=d + os.pathsep + os.environ.get("PATH", "")):
                # exit 0 stderr is not shown in the normal flow; systemMessage is (Codex R2-1). No decision: it passes.
                self.assertEqual(p.returncode, 0, p.stderr)
                out = json.loads(p.stdout)
                self.assertIn("brake is off", out.get("systemMessage", ""))
                self.assertNotIn("hookSpecificOutput", out)

    def test_probe_requires_python_3_7(self):
        # the real probe expression from the registered command, run with a controlled sys.version_info (Codex R2-3)
        import re
        for _, command in self.entries():
            m = re.search(r"-c '([^']*version_info[^']*)'", command)
            self.assertIsNotNone(m, "no version probe in the registered command")
            for version, ok in (((3, 6, 15), False), ((3, 7, 0), True), ((3, 12, 1), True), ((2, 7, 18), False)):
                code = f"import sys; sys.version_info = {version!r}; exec({m.group(1)!r})"
                rc = subprocess.run([sys.executable, "-c", code], capture_output=True, timeout=20).returncode
                self.assertEqual(rc == 0, ok, f"probe on {version}: exit {rc}")

    def test_broken_python3_falls_back_before_reading_input(self):
        # a python3 that exists but does not run (the Windows Store alias), or runs but is older than 3.7, must not eat
        # stdin or end the brake: the next interpreter answers
        for body in ("cat >/dev/null\nexit 49\n",
                     'case "$*" in *version_info*) exit 1;; esac\ncat >/dev/null\nexit 0\n'):
            with self.subTest(stub=body.splitlines()[0]):
                self.fallback_case(body)

    def fallback_case(self, body):
        import os, tempfile
        payload = json.dumps({"tool_name": "Bash", "tool_input": {"command": "gh pr merge 1 --merge"}})
        with tempfile.TemporaryDirectory() as d:
            stub = pathlib.Path(d, "python3")
            stub.write_text("#!/bin/sh\n" + body)
            stub.chmod(0o755)
            try:
                os.symlink(sys.executable, pathlib.Path(d, "python"))
            except (OSError, NotImplementedError):
                self.skipTest("no symlinks on this machine")
            for p in self.run_registered(payload, path=d + os.pathsep + os.environ.get("PATH", "")):
                self.assertEqual(self.decision_of(p), "deny")

    def test_registered_for_every_braked_tool(self):
        import re
        entries = self.entries()
        self.assertTrue(entries, "command_brake.py is not a PreToolUse hook in .claude/settings.json")
        covers = lambda m, tool: m in ("", "*") or re.fullmatch(m, tool) is not None   # "" and "*" mean every tool
        for tool in sorted(cb.SHELL_TOOLS | cb.DENY_MCP):
            self.assertTrue(any(covers(m, tool) for m, _ in entries), f"no brake matcher covers {tool}")

    def test_checkout_without_the_script_passes(self):
        import tempfile
        payload = json.dumps({"tool_name": "Bash", "tool_input": {"command": "git status"}})
        with tempfile.TemporaryDirectory() as empty:
            for p in self.run_registered(payload, {"CLAUDE_PROJECT_DIR": empty}):
                self.assertEqual((p.returncode, p.stdout), (0, b""), p.stderr)

    def test_registered_command_denies_a_merge(self):
        payload = json.dumps({"tool_name": "Bash", "tool_input": {"command": "gh pr merge 1 --merge"}})
        for p in self.run_registered(payload):
            self.assertEqual(self.decision_of(p), "deny")


if __name__ == "__main__":
    unittest.main()
