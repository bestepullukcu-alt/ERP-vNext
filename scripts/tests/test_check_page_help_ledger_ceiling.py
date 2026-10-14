"""WP-CI-SERVICE-TESTS-04 (BL-609) — scripts/check_page_help_ledger_ceiling.py on synthetic ledgers.

The header has the real ledger's shape (7eec3a4f9, docs/roadmap/backlog/page-help-ledger.md). Run by
scripts/run_phase1_gates.sh:

    python3 -m unittest discover -s scripts/tests -p 'test_*.py'
"""

from __future__ import annotations

import io
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stdout

SCRIPTS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, SCRIPTS)
import check_page_help_ledger_ceiling as script  # noqa: E402


def ledger(tenant: int, platform: int = 18, frozen: int = 99, extra: str = "") -> str:
    return ("# Sayfa yardımı borç defteri\n\n"
            f"**Tavan (yalnız azalır): kiracı {tenant} · platform {platform} · frozen {frozen}**\n\n"
            + extra + "| kod | ad |\n|---|---|\n| ACCOUNT | Account |\n")


class PageHelpCeilingTests(unittest.TestCase):
    def run_script(self, base: str | None, head: str | None, *extra: str) -> tuple[int, str]:
        with tempfile.TemporaryDirectory() as directory:
            base_path = os.path.join(directory, "base.md")
            head_path = os.path.join(directory, "head.md")
            if base is not None:
                with open(base_path, "w", encoding="utf-8") as handle:
                    handle.write(base)
            if head is not None:
                with open(head_path, "w", encoding="utf-8") as handle:
                    handle.write(head)
            out = io.StringIO()
            with redirect_stdout(out):
                code = script.main(["--base-file", base_path, "--head-file", head_path, *extra])
            return code, out.getvalue()

    def test_equal_ceilings_pass(self):
        code, out = self.run_script(ledger(236), ledger(236))
        self.assertEqual(0, code, out)
        self.assertIn("ok", out)

    def test_smaller_ceilings_pass(self):
        code, out = self.run_script(ledger(236, 18, 99), ledger(230, 17, 99))
        self.assertEqual(0, code, out)

    def test_a_raised_ceiling_is_red_and_named(self):
        code, out = self.run_script(ledger(236), ledger(240))
        self.assertEqual(1, code, out)
        self.assertIn("'kiracı' ceiling raised 236 → 240", out)

    def test_a_raise_with_its_exact_decision_line_passes(self):
        reason = "- Tavan artışı (CT kararı): kiracı 236 → 240 — dört yeni sayfa menüye girdi, metinleri ayrı işte\n\n"
        code, out = self.run_script(ledger(236), ledger(240, extra=reason))
        self.assertEqual(0, code, out)

    def test_a_decision_line_for_other_numbers_does_not_cover_the_raise(self):
        for line in ("- Tavan artışı (CT kararı): kiracı 236 → 239 — gerekçe\n",
                     "- Tavan artışı (CT kararı): platform 236 → 240 — gerekçe\n",
                     "- Tavan artışı (CT kararı): kiracı 236 → 240 — \n",
                     "Tavan artışı (CT kararı): kiracı 236 → 240 — gerekçe\n"):
            with self.subTest(line=line):
                code, out = self.run_script(ledger(236), ledger(240, extra=line + "\n"))
                self.assertEqual(1, code, out)

    def test_a_disappearing_or_new_category_is_red(self):
        no_frozen = ledger(236).replace(" · frozen 99", "")
        code, out = self.run_script(ledger(236), no_frozen)
        self.assertEqual(1, code, out)
        self.assertIn("category 'frozen'", out)
        code, out = self.run_script(no_frozen, ledger(236))
        self.assertEqual(1, code, out)
        self.assertIn("'frozen' ceiling raised 0 → 99", out)

    def test_a_malformed_header_is_red(self):
        for head in (ledger(236).replace("**Tavan", "Tavan"), ledger(236).replace("kiracı 236", "kiracı ikiyüz"),
                     ledger(236) + ledger(236)):
            with self.subTest(head=head[:80]):
                code, out = self.run_script(ledger(236), head)
                self.assertEqual(1, code, out)
                self.assertIn("*** FAILED ***", out)

    def test_no_ledger_in_this_change_or_on_the_base_skips_by_name(self):
        code, out = self.run_script(ledger(236), None)
        self.assertEqual(0, code, out)
        self.assertIn("*** SKIPPED *** — ", out)
        self.assertIn("does not exist in this change", out)
        code, out = self.run_script(None, ledger(236))
        self.assertEqual(0, code, out)
        self.assertIn("does not exist on the base", out)
        self.assertIn("NOTHING WAS CHECKED", out)

    def test_no_base_branch_skips_by_name(self):
        out = io.StringIO()
        with redirect_stdout(out):
            code = script.main([])
        self.assertEqual(0, code)
        self.assertIn("*** SKIPPED *** — no base branch", out.getvalue())

    # Main sync (CI follow-up 3 review): the real --base path — `git show <ref>:<ledger>` in a throwaway repository.
    def test_the_base_ref_is_read_from_git(self):
        import subprocess
        with tempfile.TemporaryDirectory() as repo:
            def git(*args):
                subprocess.run(["git", *args], cwd=repo, check=True, capture_output=True, text=True)
            git("init", "-q")
            git("config", "user.email", "ci@example.invalid")
            git("config", "user.name", "ci")
            ledger_path = os.path.join(repo, script.LEDGER)
            os.makedirs(os.path.dirname(ledger_path))
            with open(ledger_path, "w", encoding="utf-8") as handle:
                handle.write(ledger(236))
            git("add", script.LEDGER)
            git("commit", "-q", "-m", "base")
            base = subprocess.run(["git", "rev-parse", "HEAD"], cwd=repo, check=True, capture_output=True, text=True).stdout.strip()
            git("commit", "-q", "--allow-empty", "-m", "unrelated")
            same_base = subprocess.run(["git", "rev-parse", "HEAD~1"], cwd=repo, check=True, capture_output=True, text=True).stdout.strip()

            cwd = os.getcwd()
            os.chdir(repo)
            try:
                for head, expected in ((ledger(240), 1), (ledger(236), 0), (ledger(230), 0)):
                    with open(ledger_path, "w", encoding="utf-8") as handle:
                        handle.write(head)
                    out = io.StringIO()
                    with redirect_stdout(out):
                        code = script.main(["--base", base])
                    self.assertEqual(expected, code, out.getvalue())
                self.assertIsNone(script.read_base("no-such-ref"))
                self.assertEqual(ledger(236), script.read_base(same_base))
            finally:
                os.chdir(cwd)

    def test_the_header_reader_matches_the_real_ledger_line(self):
        self.assertEqual({"kiracı": 236, "platform": 18, "frozen": 99},
                         script.ceilings("**Tavan (yalnız azalır): kiracı 236 · platform 18 · frozen 99**\n", "real"))


if __name__ == "__main__":
    unittest.main()
