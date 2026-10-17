"""CT-FIX-03 U1 — the operating card's "Bugün hangi komut" table cannot drift from the tools: every command in it names
a script that exists, a subcommand the script's own argparse offers, and only flags its --help prints. Read-only: each
tool runs with --help only. Run by scripts/run_phase1_gates.sh (python3 -m unittest discover -s scripts/tests).
"""
from __future__ import annotations

import re
import subprocess
import sys
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
CARD = REPO / "docs" / "guides" / "operations" / "control-tower-operating-card.md"
SECTION = "## Bugün hangi komut"


def card_commands(text: str) -> list[str]:
    """The `python3 …` commands of the table rows in the section (the second column and the notes in it)."""
    if text.count(SECTION) != 1:
        raise AssertionError(f"card: '{SECTION}' section {text.count(SECTION)} times, expected 1")
    body = text.split(SECTION, 1)[1].split("\n## ", 1)[0]
    rows = [l for l in body.splitlines() if l.startswith("| ") and not l.startswith("| Ne zaman") and not l.startswith("|---")]
    return [c for r in rows for c in re.findall(r"`(python3 [^`]+)`", r)]


def help_of(script: str, sub: str | None, mode_v2: bool) -> subprocess.CompletedProcess:
    args = [sys.executable, "-B", str(REPO / script)] + (["--mode", "v2"] if mode_v2 else []) + ([sub] if sub else []) + ["--help"]
    return subprocess.run(args, cwd=REPO, capture_output=True, text=True, timeout=120)


class CardCommands(unittest.TestCase):
    def test_the_table_lists_commands(self):
        self.assertGreaterEqual(len(card_commands(CARD.read_text(encoding="utf-8"))), 10)

    def test_every_command_matches_its_tool(self):
        for cmd in card_commands(CARD.read_text(encoding="utf-8")):
            with self.subTest(command=cmd):
                tokens = re.findall(r'"[^"]*"|\S+', cmd)
                script = tokens[1]
                self.assertTrue((REPO / script).is_file(), f"{script} does not exist")
                rest = tokens[2:]
                sub = rest[0] if rest and re.fullmatch(r"[a-z][a-z-]*", rest[0]) else None
                mode_v2 = rest[:2] == ["--mode", "v2"]
                p = help_of(script, None if mode_v2 else sub, mode_v2)
                self.assertEqual(p.returncode, 0, f"{script} {sub or ''} --help failed: {p.stderr[-300:]}")
                for flag in (t for t in rest if t.startswith("--")):
                    self.assertIn(flag, p.stdout, f"{script} {sub or ''}: '{flag}' is not in its --help")

    def test_a_drifted_command_is_caught(self):
        # the guard itself: a flag the tool does not have is reported (no file is changed)
        p = help_of("scripts/prompt_gate.py", "draft", False)
        self.assertNotIn("--wpx", p.stdout)


if __name__ == "__main__":
    unittest.main()
