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

    def test_auto_merge_tool_is_denied(self):
        self.assertEqual(self.decision("mcp__ccd_pr__set_auto_merge", {}), "deny")

    def test_other_tools_and_bad_input_pass(self):
        self.assertIsNone(self.decision("Read", {}))
        self.assertEqual(self.run_hook("not json"), (0, ""))
        self.assertEqual(self.run_hook("[1, 2]"), (0, ""))


if __name__ == "__main__":
    unittest.main()
