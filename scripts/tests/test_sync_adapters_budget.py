"""İ5B-FIX-01 (independent audit 2026-10-17, finding 02) — the instruction budget never passes when a file it is asked to
link-check is gone: a missing link_check file is a MISSING problem, like a missing instruction file. İ5B-FIX-03 (v4
audits, V4A-F1 / V4B-B5): every Markdown link form, every import form outside code, a core rule only inside an HTML
comment, and a budget problem before any write. İ5B-FIX-04 (v5 quality review 4): headings and required sections are
read from the visible text, and AGENTS-REFERENCE.md's sections keep a visible body with their anchor sentences. Run by
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

    # İ5B-FIX-04 (v5 quality review 4, Codex): the visible text decides; every case is a counter-example
    REF = ".antigravity/AGENTS-REFERENCE.md"

    def ref_with(self, text: str) -> list[str]:
        f = Path(self.tmp.name) / "AGENTS-REFERENCE.md"
        f.write_text(text, encoding="utf-8")
        self.redirect(self.REF, f)
        return sa.check_instruction_budget(self.manifest)[0]

    def sections(self):
        return next(f for f in self.manifest["instruction_budget"]["files"] if f["path"] == self.REF)["required_sections"]

    def test_the_reference_file_has_required_sections_with_anchors(self):
        specs = self.sections()
        self.assertEqual(len(specs), 8)
        self.assertTrue(all(s.get("anchors") for s in specs))

    def test_an_emptied_reference_file_fails(self):
        problems = self.ref_with("")
        self.assertEqual(sum(p.startswith(f"SECTION {self.REF}") for p in problems), 8, problems)

    def test_headings_kept_bodies_removed_fails(self):
        text = (REPO / self.REF).read_text(encoding="utf-8")
        heads = "\n\n".join(l for l in text.split("\n") if l.startswith("#")) + "\n"
        problems = self.ref_with(heads)
        self.assertTrue(any("has no visible content" in p for p in problems), problems)
        self.assertTrue(any(p.startswith(f"ANCHOR {self.REF}") for p in problems), problems)

    def test_bodies_inside_html_comments_or_only_blank_fail(self):
        text = (REPO / self.REF).read_text(encoding="utf-8")
        lines = text.split("\n")
        commented, blanked = [], []
        inside = False
        for l in lines:
            if l.startswith("## "):
                inside = True
                commented.append(l)
                blanked.append(l)
                commented.append("<!--")
                continue
            if l.startswith("# "):
                inside = False
            if inside:
                commented.append(l)
                blanked.append("" if l.strip() else l)
            else:
                commented.append(l)
                blanked.append(l)
        commented_text = "\n".join(commented).replace("\n## ", "\n-->\n## ") + "\n-->\n"
        for label, t in (("bodies in comments", commented_text), ("bodies blank", "\n".join(blanked) + "\n"),
                         ("bodies a comment only", "\n".join(l if not l.strip() or l.startswith("#") else "<!-- x -->" for l in lines))):
            with self.subTest(form=label):
                problems = self.ref_with(t)
                self.assertTrue(any("has no visible content" in p for p in problems), problems)

    def test_padding_without_anchors_fails(self):
        text = (REPO / self.REF).read_text(encoding="utf-8")
        padded = "\n".join(l if l.startswith("#") or not l.strip() else "Bu bölüm başka bir yerde anlatılıyor." for l in text.split("\n"))
        problems = self.ref_with(padded)
        self.assertEqual(sum(p.startswith(f"ANCHOR {self.REF}") for p in problems), 12, problems)

    def test_one_anchor_removed_fails_by_name(self):
        text = (REPO / self.REF).read_text(encoding="utf-8")
        anchor = "**port 5065**"
        self.assertEqual(text.count(anchor), 1)
        problems = self.ref_with(text.replace(anchor, "port beş bin altmış beş"))
        self.assertEqual([p for p in problems if p.startswith("ANCHOR")],
                         [f"ANCHOR {self.REF} '## 3. Port Şeması — tarihli atama notları' lost: {anchor!r}"])

    def test_a_heading_only_inside_a_comment_or_in_code_is_lost(self):
        text = (REPO / self.REF).read_text(encoding="utf-8")
        head = "## 12. Adaptasyon Notu"
        self.assertEqual(text.count(head), 1)
        for label, t in (("inside an HTML comment", text.replace(head, "<!--\n" + head + "\n-->")),
                         ("inside a code fence", text.replace(head, "```\n" + head + "\n```"))):
            with self.subTest(form=label):
                problems = self.ref_with(t)
                self.assertTrue(any(p.startswith(f"SECTION {self.REF} '## 12. Adaptasyon Notu'") for p in problems), problems)

    def test_a_section_written_twice_fails(self):
        text = (REPO / self.REF).read_text(encoding="utf-8")
        problems = self.ref_with(text + "\n## 12. Adaptasyon Notu\n\nikinci kopya\n")
        self.assertIn(f"SECTION {self.REF} '## 12. Adaptasyon Notu' 2 times (AGENTS.md points readers here)", problems)

    def test_an_unclosed_comment_at_the_top_hides_the_rest(self):
        # Codex: an unclosed '<!--' at the start leaves nothing visible; a closed-comment-only pattern would miss it
        text = (REPO / self.REF).read_text(encoding="utf-8")
        problems = self.ref_with("<!--\n" + text)
        self.assertEqual(sum(p.startswith(f"SECTION {self.REF}") for p in problems), 8, problems)
        # in AGENTS.md an unclosed '<!--' ends at the next '-->' (HTML reads it so): at the top it hides only what comes
        # before the core marker; after the last closed comment it hides every heading that follows
        agents = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        f.write_text("<!--\n" + agents, encoding="utf-8")
        self.assertFalse([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith(("HEADING", "CORE"))])
        self.assertEqual(agents.count("<!-- core:end -->"), 1)
        f.write_text(agents.replace("<!-- core:end -->", "<!-- core:end -->\n<!--"), encoding="utf-8")
        lost = [p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("HEADING AGENTS.md lost")]
        self.assertEqual(len(lost), 14, lost)        # every required heading after the core block: 1.–12., 6.1, DCP-002

    def test_an_agents_md_heading_only_inside_a_comment_or_a_code_block_is_lost(self):
        # Codex (candidate2-adapter-heading): the real '## 12.' heading put inside a fence passed --check
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        head = next(l for l in text.split("\n") if l.startswith("## 12."))
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        for label, wrapped, why in (("an HTML comment", "<!-- " + head + " -->", "only inside an HTML comment"),
                                    ("a backtick fence", "```\n" + head + "\n```", "only inside a code block"),
                                    ("a tilde fence", "~~~\n" + head + "\n~~~", "only inside a code block")):
            with self.subTest(form=label):
                f.write_text(text.replace(head, wrapped), encoding="utf-8")
                problems = sa.check_instruction_budget(self.manifest)[0]
                self.assertIn(f"HEADING AGENTS.md lost '## 12.' ({why}) (AGENTS.md §-references would break)", problems)

    # İ5B-FIX-05 (the Employee pilot-01, Codex): a named module / scope request is researched before options are offered
    MODULE_RULE = "Adı verilen yeni modül ya da kapsam istenince önce araştır"
    DISCOVER = '`python3 .antigravity/scripts/verify_module_id.py . --discover "<ad>"`'

    def test_the_module_request_rule_is_a_required_core_text_once(self):
        self.assertIn(self.MODULE_RULE, self.manifest["instruction_budget"]["core"]["required_text"])
        core = sa.extract_core("AGENTS.md")
        self.assertEqual(core.count(self.MODULE_RULE), 1)
        line = next(l for l in core.split("\n") if self.MODULE_RULE in l)
        self.assertTrue(line.startswith("7. **Modül ve kimlik"), line[:60])      # Codex: item 7 only
        for part in (self.DISCOVER, "Keşif koşmaz ya da eksikse bunu söyle", "küçük düzeltmede tarama gerekmez",
                     "Kod, commit ya da dağıtım izni değildir"):
            self.assertIn(part, line)

    def test_the_generated_core_carries_the_rule(self):
        generated = (REPO / ".claude/rules/agents-core.md").read_text(encoding="utf-8")
        self.assertEqual(generated.count(self.MODULE_RULE), 1)
        self.assertIn(self.DISCOVER, generated)

    def test_the_module_request_rule_hidden_in_a_comment_or_removed_is_lost(self):
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        start = text.index(" **Modül talebi:**")
        end = text.index("dağıtım izni değildir.", start) + len("dağıtım izni değildir.")
        rule = text[start:end]
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        for label, replaced, why in (("inside an HTML comment", " <!--" + rule + " -->", "only inside an HTML comment"),
                                     ("removed", "", "")):
            with self.subTest(form=label):
                f.write_text(text[:start] + replaced + text[end:], encoding="utf-8")
                lost = [p for p in sa.check_instruction_budget(self.manifest)[0]
                        if p.startswith("CORE-RULE") and self.MODULE_RULE in p]
                self.assertEqual(len(lost), 1, lost)
                self.assertIn(why, lost[0])

    def test_a_core_rule_only_inside_a_code_block_is_lost(self):
        # Codex (new-entry-probes): the new sentence moved into a fence inside the core, the adapter regenerated, passed
        # --check — a fenced block is an example, not a rule; inline code spans in a rule stay readable
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        start = text.index(" **Modül talebi:**")
        end = text.index("dağıtım izni değildir.", start) + len("dağıtım izni değildir.")
        rule = text[start + 1:end]
        first = self.manifest["instruction_budget"]["core"]["required_text"][0]
        self.assertEqual(text.count(first), 1)
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        for label, changed, needle in (
                ("the module rule in a backtick fence", text[:start] + "\n   ```\n   " + rule + "\n   ```" + text[end:], self.MODULE_RULE),
                ("the module rule in a tilde fence", text[:start] + "\n   ~~~\n   " + rule + "\n   ~~~" + text[end:], self.MODULE_RULE),
                ("the module rule in a fence indented as a list item's content", text[:start] + "\n      ```\n      " + rule + "\n      ```" + text[end:], self.MODULE_RULE),
                ("the authority order in a fence", text.replace(first, "\n```\n" + first + "\n```\n"), first)):
            with self.subTest(form=label):
                f.write_text(changed, encoding="utf-8")
                lost = [p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("CORE-RULE") and needle in p]
                self.assertEqual(len(lost), 1, lost)
                self.assertIn("only inside a code block", lost[0])

    def test_an_unclosed_fence_in_the_core_hides_the_rules_after_it(self):
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        f.write_text(text.replace("8. **Git (§9", "```\n8. **Git (§9", 1), encoding="utf-8")
        lost = [p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("CORE-RULE")]
        self.assertTrue(any("`main`'e commit/push yok" in p for p in lost), lost)
        self.assertFalse(any(self.MODULE_RULE in p for p in lost), lost)      # item 7 comes before the fence
        # an inline span of three backticks is not a fence: the rules after it stay rules
        f.write_text(text.replace("8. **Git (§9", "Örnek: ```kod``` yazılır.\n8. **Git (§9", 1), encoding="utf-8")
        self.assertFalse([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("CORE-RULE")])

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
