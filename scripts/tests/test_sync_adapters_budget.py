"""İ5B-FIX-01 (independent audit 2026-10-17, finding 02) — the instruction budget never passes when a file it is asked to
link-check is gone: a missing link_check file is a MISSING problem, like a missing instruction file. İ5B-FIX-03 (v4
audits, V4A-F1 / V4B-B5): every Markdown link form, every import form outside code, a core rule only inside an HTML
comment, and a budget problem before any write. Run by
scripts/run_phase1_gates.sh:

    python3 -m unittest discover -s scripts/tests -p 'test_*.py'
"""

from __future__ import annotations

import importlib.util
import os
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("sync_adapters", REPO / ".antigravity" / "scripts" / "sync_adapters.py")
sa = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(sa)


class InstructionBudget(unittest.TestCase):
    def setUp(self):
        self.manifest = sa.load_manifest()
        self.real_rel = sa.rel
        self.tmp = tempfile.TemporaryDirectory()

    def tearDown(self):
        sa.rel = self.real_rel
        self.tmp.cleanup()

    def redirect(self, path: str, to: Path):
        sa.rel = lambda p: to if p == path else self.real_rel(p)

    def test_the_real_instruction_files_pass(self):
        self.assertEqual(sa.check_instruction_budget(self.manifest), ([], []))

    def test_a_missing_link_check_file_is_missing_not_skipped(self):
        self.redirect(".antigravity/AGENTS-REFERENCE.md", Path(self.tmp.name) / "absent.md")
        problems, _ = sa.check_instruction_budget(self.manifest)
        self.assertIn("MISSING .antigravity/AGENTS-REFERENCE.md (link_check)", problems)

    def test_a_missing_instruction_file_is_still_missing(self):
        self.redirect("CLAUDE.md", Path(self.tmp.name) / "absent.md")
        problems, _ = sa.check_instruction_budget(self.manifest)
        self.assertTrue(any(p.startswith("MISSING CLAUDE.md") for p in problems), problems)

    def test_a_dead_link_is_still_a_dead_link(self):
        broken = Path(self.tmp.name) / "CLAUDE.md"
        broken.write_text((REPO / "CLAUDE.md").read_text(encoding="utf-8") + "\n[gone](does-not-exist.md)\n", encoding="utf-8")
        self.redirect("CLAUDE.md", broken)
        problems, _ = sa.check_instruction_budget(self.manifest)
        self.assertTrue(any(p.startswith("DEADLINK CLAUDE.md -> does-not-exist.md") for p in problems), problems)

    def test_link_check_lists_the_reference_file(self):
        self.assertIn(".antigravity/AGENTS-REFERENCE.md", self.manifest["instruction_budget"]["link_check"])

    # İ5B-FIX-03 (v4 audits, V4A-F1 / V4B-B5): every case is an audit counter-example; never removed or weakened
    def claude_with(self, extra: str) -> list[str]:
        f = Path(self.tmp.name) / "CLAUDE.md"
        f.write_text((REPO / "CLAUDE.md").read_text(encoding="utf-8") + extra, encoding="utf-8")
        self.redirect("CLAUDE.md", f)
        return sa.check_instruction_budget(self.manifest)[0]

    def test_dead_links_in_every_markdown_link_form(self):
        for label, extra in (("a titled link", '\n[x](gone-titled.md "title")\n'),
                             ("a single-quoted title", "\n[x](gone-single.md 'title')\n"),
                             ("an angle-bracket destination", "\n[x](<gone angle.md>)\n"),
                             ("a reference-style link", "\n[x][r]\n\n[r]: gone-reference.md\n"),
                             ("a titled reference definition", '\n[x][r2]\n\n[r2]: gone-ref-title.md "t"\n')):
            with self.subTest(form=label):
                problems = self.claude_with(extra)
                self.assertTrue(any(p.startswith("DEADLINK CLAUDE.md -> gone") for p in problems), problems)

    def test_every_import_form_of_agents_md_outside_code_is_forbidden(self):
        for label, extra in (("a line-start import", "\n@AGENTS.md\n"), ("a ./ path", "\n@./AGENTS.md\n"),
                             ("inside a sentence", "\nKurallar için @AGENTS.md okunur.\n"),
                             ("another case", "\n@agents.md\n"), ("a path through a folder", "\n@./docs/../AGENTS.md\n"),
                             ("behind an unclosed fence", "\n```\n@AGENTS.md\n"),
                             ("after a lone backtick (no code span)", "\nbir ` işareti @AGENTS.md\n")):
            with self.subTest(form=label):
                problems = self.claude_with(extra)
                self.assertTrue(any(p.startswith("FORBIDDEN CLAUDE.md:") and "AGENTS.MD" in p.upper() for p in problems), problems)

    def test_an_import_shown_as_code_is_not_an_import(self):
        for label, extra in (("a code span", "\nÖrnek: `@AGENTS.md` yazılmaz.\n"),
                             ("a code span with text before the import", "\nÖrnek: `bkz. @AGENTS.md` yazılmaz.\n"),
                             ("a double-backtick code span", "\nÖrnek: ``a ` @AGENTS.md`` yazılmaz.\n"),
                             ("a closed fence", "\n```\n@AGENTS.md\n```\n"), ("a longer closing fence", "\n```\n@AGENTS.md\n`````\n")):
            with self.subTest(form=label):
                self.assertFalse(any(p.startswith("FORBIDDEN") for p in self.claude_with(extra)))

    def test_a_core_rule_only_inside_an_html_comment_is_lost(self):
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        needle = self.manifest["instruction_budget"]["core"]["required_text"][0]
        self.assertEqual(text.count(needle), 1)
        f = Path(self.tmp.name) / "AGENTS.md"
        f.write_text(text.replace(needle, "<!-- " + needle + " -->"), encoding="utf-8")
        self.redirect("AGENTS.md", f)
        problems, _ = sa.check_instruction_budget(self.manifest)
        self.assertTrue(any(p.startswith("CORE-RULE") and "only inside an HTML comment" in p for p in problems), problems)

    def test_oversize_message_names_the_file_it_is_about(self):
        problems = self.claude_with("x" * 3000)
        over = [p for p in problems if p.startswith("OVERSIZE CLAUDE.md")]
        self.assertTrue(over, problems)
        self.assertNotIn("Antigravity", over[0])

    def test_a_budget_problem_writes_no_adapter(self):
        import contextlib
        import io
        import sys
        out = Path(self.tmp.name) / "out"
        big = Path(self.tmp.name) / "CLAUDE.md"
        big.write_text("x" * 3000, encoding="utf-8")
        targets = {t["path"] for i in self.manifest["items"] for t in i["targets"]}
        # every adapter target goes to a temporary folder, so even a regression could not write into the repository
        sa.rel = lambda p: big if p == "CLAUDE.md" else (out / p if p in targets else self.real_rel(p))
        argv, sys.argv = sys.argv, ["sync_adapters.py"]
        try:
            with contextlib.redirect_stdout(io.StringIO()) as buf:
                code = sa.main()
        finally:
            sys.argv = argv
        self.assertEqual(code, 1, buf.getvalue())
        self.assertIn("nothing written", buf.getvalue())
        self.assertFalse(out.exists() and any(out.rglob("*")), "an adapter was written despite a budget problem")


if __name__ == "__main__":
    unittest.main()
