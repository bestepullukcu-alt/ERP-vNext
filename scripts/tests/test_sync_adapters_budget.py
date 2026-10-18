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
import re
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
        # İ5B-FIX-06 (Employee EMP-REPORT-01, joint decision): the final answer's four parts, byte-neutral
        for part in (self.DISCOVER, "Son yanıt dört parça", "koşmayan ya da eksik keşif ve nedeni", '"kısmi inceleme"',
                     "gerekçeli ilk kapsam", "açık onay/sahiplik kararları", "küçük düzeltmede tarama gerekmez",
                     "Kod, commit ya da dağıtım izni değildir"):
            self.assertIn(part, line)
        # İ5B-FIX-08 (C6 pilot: EMP-06 / EMP-08 and the report's accuracy): the answer contract's parts are required texts of
        # the production check, not only strings this test looks for
        required = self.manifest["instruction_budget"]["core"]["required_text"]
        for part in self.CONTRACT:
            with self.subTest(part=part[:30]):
                self.assertIn(part, line)
                self.assertIn(part, required)

    CONTRACT = ("okunan kaynaklar (çıkarım ayrı)", 'koşmayan ya da eksik keşif ve nedeni ("kısmi inceleme")',
                "gerekçeli ilk kapsam (tek öneri)", "tüm açık onay/sahiplik kararları (TBD dahil)",
                "Belirsizde sor, salt-okunur adım öner")

    def test_each_contract_part_removed_or_hidden_in_a_comment_is_lost(self):
        # İ5B-FIX-08: the production check refuses the core when any one part goes missing, the rest intact
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        for part in self.CONTRACT:
            for label, replaced in (("removed", ""), ("inside an HTML comment", "<!-- " + part + " -->")):
                with self.subTest(part=part[:30], form=label):
                    self.assertEqual(text.count(part), 1)
                    f.write_text(text.replace(part, replaced, 1), encoding="utf-8")
                    lost = [p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("CORE-RULE") and part in p]
                    self.assertEqual(len(lost), 1, lost)
        f.write_text(text, encoding="utf-8")
        self.assertFalse([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("CORE-")])

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

    def test_a_core_rule_off_its_numbered_line_is_lost(self):
        # v6 audit F5: every numbered core rule turned into an indented code block passed write and --check
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        a, b = text.index("<!-- core:start -->"), text.index("<!-- core:end -->")
        core = text[a:b]
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        required = self.manifest["instruction_budget"]["core"]["required_text"]

        def moved(prefix: str, blank_before: bool = True) -> str:
            out = []
            for line in core.split("\n"):
                if re.match(r"^\d{1,2}\. ", line):
                    if blank_before and out and out[-1].strip():
                        out.append("")
                    out.append(prefix + line)
                else:
                    out.append(line)
            return text[:a] + "\n".join(out) + text[b:]
        for label, changed in (("indented by four spaces", moved("    ")), ("indented by a tab", moved("\t")),
                               ("in a blockquote", moved("> ", blank_before=False)), ("in a table row", moved("| ", blank_before=False))):
            with self.subTest(form=label):
                f.write_text(changed, encoding="utf-8")
                lost = [p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("CORE-RULE")]
                self.assertEqual(len(lost), len(required), lost[:3])
                self.assertTrue(all("not on a numbered core rule line" in p for p in lost), lost[:3])

    def test_raw_html_in_the_core_is_refused(self):
        # Codex (core-format-probes): the numbered lines wrapped in <pre>, <script> or <div hidden> passed write and --check
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        first = next(l for l in text.split("\n") if l.startswith("1. **"))
        last = next(l for l in text.split("\n") if l.startswith("11. **"))
        for label, opening, closing in (("pre", "<pre>", "</pre>"), ("script", "<script>", "</script>"),
                                        ("div hidden", "<div hidden>", "</div>"), ("details", "<details>", "</details>")):
            with self.subTest(form=label):
                f.write_text(text.replace(first, opening + "\n" + first).replace(last, last + "\n" + closing), encoding="utf-8")
                problems = sa.check_instruction_budget(self.manifest)[0]
                self.assertTrue(any(p.startswith("CORE-HTML") for p in problems), problems[:3])
        # opened between two rules, not only around the first line
        mid = next(l for l in text.split("\n") if l.startswith("6. **"))
        f.write_text(text.replace(mid, "<div hidden>\n" + mid + "\n</div>"), encoding="utf-8")
        self.assertTrue(any(p.startswith("CORE-HTML") for p in sa.check_instruction_budget(self.manifest)[0]))
        # inline code that shows a tag is not raw HTML
        f.write_text(text.replace(mid, mid + " Örnek: `<div hidden>` yazılmaz."), encoding="utf-8")
        self.assertFalse([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith(("CORE-HTML", "CORE-RULE"))])

    def test_a_code_span_across_lines_in_the_core_is_refused(self):
        # Codex (core-format-probes-03): every numbered rule inside one double-backtick span passed write and --check
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        first = next(l for l in text.split("\n") if l.startswith("1. **"))
        last = next(l for l in text.split("\n") if l.startswith("11. **"))
        mid = next(l for l in text.split("\n") if l.startswith("6. **"))
        for label, changed in (("double backticks around every rule", text.replace(first, "``\n" + first).replace(last, last + "\n``")),
                               ("single backticks around two rules", text.replace(mid, "`\n" + mid).replace(last, last + "\n`"))):
            with self.subTest(form=label):
                f.write_text(changed, encoding="utf-8")
                self.assertTrue(any(p.startswith("CORE-SPAN") for p in sa.check_instruction_budget(self.manifest)[0]))
        self.redirect("AGENTS.md", REPO / "AGENTS.md")
        self.assertFalse([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("CORE-SPAN")])

    def test_a_container_opened_before_the_core_is_refused(self):
        # İ5B-FIX-07 (v7 audit, Codex and Codex Denetim001): <pre> before core:start and </pre> after core:end passed
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        for label, opening, closing in (("pre", "<pre>", "</pre>"), ("details", "<details>", "</details>"),
                                        ("div hidden", '<div hidden="">', "</div>")):
            with self.subTest(form=label):
                f.write_text(text.replace(sa.CORE_START, opening + "\n" + sa.CORE_START, 1)
                             .replace(sa.CORE_END, sa.CORE_END + "\n" + closing, 1), encoding="utf-8")
                problems = sa.check_instruction_budget(self.manifest)[0]
                self.assertTrue(any(p.startswith("CORE-HTML") and "before the core block" in p for p in problems), problems[:3])
        # after the core the document is free; a tag shown in inline code or a fence before it is not markup
        for label, changed in (("HTML after core:end", text.replace(sa.CORE_END, sa.CORE_END + "\n<details>x</details>", 1)),
                               ("a tag in inline code before the core", text.replace(sa.CORE_START, "Örnek: `<pre>`\n" + sa.CORE_START, 1)),
                               ("a tag in a fence before the core", text.replace(sa.CORE_START, "```\n<pre>\n```\n" + sa.CORE_START, 1))):
            with self.subTest(form=label):
                f.write_text(changed, encoding="utf-8")
                self.assertFalse([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("CORE-HTML")])

    def test_a_rule_inside_a_larger_code_span_is_lost(self):
        # İ5B-FIX-07 (v7 audit): every numbered rule body inside one five-backtick span on its own line passed
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        head, rest = text.split(sa.CORE_START, 1)
        core, tail = rest.split(sa.CORE_END, 1)
        wrapped = "\n".join(re.sub(r"^(\d{1,2}\. )(.+)$", lambda m: m.group(1) + "`````" + m.group(2) + "`````", l)
                            for l in core.split("\n"))
        f.write_text(head + sa.CORE_START + wrapped + sa.CORE_END + tail, encoding="utf-8")
        problems = sa.check_instruction_budget(self.manifest)[0]
        lost = [p for p in problems if p.startswith("CORE-RULE") and p.endswith("(only inside inline code)")]
        self.assertEqual(len(lost), len(self.manifest["instruction_budget"]["core"]["required_text"]), problems[:3])
        # the short code terms of the real rules still count: the span itself, with or without its backticks
        self.assertTrue(sa.on_rule_line("`verify_module_id.py`", "7. x `verify_module_id.py` y"))
        self.assertTrue(sa.on_rule_line("verify_module_id.py", "7. x `verify_module_id.py` y"))
        self.assertTrue(sa.on_rule_line("`TenantId` ile sınırlanır", "2. sorgu `TenantId` ile sınırlanır."))
        self.assertFalse(sa.on_rule_line("TenantId ile", "2. `sorgu TenantId ile sınırlanır`"))
        self.assertTrue(sa.on_rule_line("ile", "2. `ile` gösterilir; kural ile yazılır"))   # one occurrence outside a span is enough

    def test_the_raw_html_opener_class_is_refused_before_and_in_the_core(self):
        # İ5B-FIX-09 (v8 audit, both Denetim001): '<![CDATA[' / '<?' before core:start passed; the CommonMark opener class
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        mid = next(l for l in text.split("\n") if l.startswith("6. **"))
        openers = (("CDATA", "<![CDATA[", "]]>"), ("processing instruction", "<?x", "?>"), ("declaration", "<!DOCTYPE x", ">"),
                   ("closing tag", "</div>", ""), ("open tag without '>'", "<pre", ""))
        for label, opening, closing in openers:
            for where, changed in (("before the core", text.replace(sa.CORE_START, opening + "\n" + sa.CORE_START, 1)
                                    .replace(sa.CORE_END, sa.CORE_END + "\n" + closing, 1)),
                                   ("on a rule line", text.replace(mid, mid + " " + opening + " " + closing, 1))):
                with self.subTest(form=label, where=where):
                    f.write_text(changed, encoding="utf-8")
                    self.assertTrue(any(p.startswith("CORE-HTML") for p in sa.check_instruction_budget(self.manifest)[0]), label)
        for label, changed in (("a comment before the core", text.replace(sa.CORE_START, "<!-- note -->\n" + sa.CORE_START, 1)),
                               ("a fence before the core", text.replace(sa.CORE_START, "```\n<?x ?> <![CDATA[ ]]>\n```\n" + sa.CORE_START, 1)),
                               ("an opener in inline code", text.replace(mid, mid + " Örnek: `<![CDATA[` ve `<?x`.", 1)),
                               ("an opener after the core", text.replace(sa.CORE_END, sa.CORE_END + "\n<![CDATA[ x ]]>", 1)),
                               ("a less-than sign in prose", text.replace(mid, mid + " (a < b, 3<4)", 1))):
            with self.subTest(positive=label):
                f.write_text(changed, encoding="utf-8")
                self.assertFalse([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("CORE-HTML")], label)

    def test_a_phrase_that_is_a_span_of_its_own_is_lost_and_short_terms_still_count(self):
        # İ5B-FIX-09 (v8 audit): the exact-span exception held for a whole natural-language phrase and for a rule split in spans
        self.assertFalse(sa.on_rule_line("gerekçeli ilk kapsam (tek öneri)", "7. x `gerekçeli ilk kapsam (tek öneri)` y"))
        self.assertFalse(sa.on_rule_line("tüm açık onay/sahiplik kararları (TBD dahil)",
                                         "7. `````a b`````; `````tüm açık onay/sahiplik kararları (TBD dahil)`````; `````c d`````"))
        for needle, line in (("`TenantId` ile sınırlanır", "2. sorgu `TenantId` ile sınırlanır."), ("`[HasPermission]`", "3. `[HasPermission]` ile"),
                             ("`.antigravity/AGENTS-REFERENCE.md`", "11. bkz. `.antigravity/AGENTS-REFERENCE.md`"),
                             ("verify_module_id.py", "7. `verify_module_id.py` ile")):
            with self.subTest(short=needle):
                self.assertTrue(sa.on_rule_line(needle, line))
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        part = "gerekçeli ilk kapsam (tek öneri)"
        f.write_text(text.replace(part, "`" + part + "`", 1), encoding="utf-8")
        self.assertTrue(any(p.startswith("CORE-RULE") and part in p and p.endswith("(only inside inline code)")
                            for p in sa.check_instruction_budget(self.manifest)[0]))

    def test_a_required_text_only_in_link_metadata_is_lost(self):
        # İ5B-FIX-10 (C8 audits, both Denetim001 and Codex): a link's destination and title, an image and a reference
        # definition are not visible text — the whole localization rule in a title passed write / check 0 / 0
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        phrase = "Platform modülü 2 dil (en, tr), kiracı modülü 7 dil (en, tr, fr, es, zh, ar, ru)"
        line = next(l for l in text.split("\n") if l.startswith("4. **"))
        self.assertIn(phrase, line)
        u = "https://example.invalid/l10n"

        def lost(changed_line):
            f.write_text(text.replace(line, changed_line, 1), encoding="utf-8")
            return [p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("CORE-RULE") and phrase in p]

        for label, repl in (("a title in double quotes", f'[Dil]({u} "{phrase}")'),
                            ("a title in single quotes", f"[Dil]({u} '{phrase}')"),
                            ("a destination in angle brackets", f"[Dil](<{phrase}>)"),
                            ("a destination with parentheses and a title", f'[Dil](https://example.invalid/(x) "{phrase}")'),
                            ("a label with brackets inside", f'[Dil [l10n]]({u} "{phrase}")'),
                            ("a title with an escaped quote", f'[Dil]({u} "a \\" b {phrase}")'),
                            ("an image's alt text", f"![{phrase}]({u})")):
            with self.subTest(hidden=label):
                problems = lost(line.replace(phrase, repl, 1))
                self.assertTrue(problems, label)
                self.assertTrue(problems[0].endswith("(only inside a link's destination or title, an image or a reference definition)"),
                                problems[0])
        with self.subTest(hidden="a numbered line that is a reference definition"):
            self.assertTrue(lost(f'4. [dil]: AGENTS.md "{phrase}"'))
        for label, repl in (("the visible link label", f"[{phrase}]({u})"),
                            ("the visible label beside a title", f'[{phrase}]({u} "Dil")'),
                            ("plain text after a link", f'[Dil]({u} "x") {phrase}')):
            with self.subTest(visible=label):
                self.assertFalse(lost(line.replace(phrase, repl, 1)), label)
        with self.subTest(visible="text that is not a reference definition (no quoted title)"):
            self.assertFalse(lost(f"4. [dil]: {phrase}"))

    def test_link_syntax_the_check_cannot_read_is_not_verified(self):
        # İ5B-FIX-10 (Codex, C8 follow-up): deeper nesting than the check reads is valid CommonMark and hid the rule in a
        # title with write / check 0 / 0 — a rule line with a '](' left after the known links proves no required text
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        phrase = "Platform modülü 2 dil (en, tr), kiracı modülü 7 dil (en, tr, fr, es, zh, ar, ru)"
        line = next(l for l in text.split("\n") if l.startswith("4. **"))
        u = "https://example.invalid"

        def problems(repl):
            f.write_text(text.replace(line, line.replace(phrase, repl, 1), 1), encoding="utf-8")
            return [p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("CORE-RULE") and phrase in p]

        for label, repl in (("a label nested three deep", f'[Dil [iç [alt]]]({u} "{phrase}")'),
                            ("a destination nested two deep", f'[Dil]({u}/a(b(c)) "{phrase}")'),
                            ("an image nested three deep", f"![{phrase} [a [b]]]({u})"),
                            ("a deep visible label (refused too: a declared limit)", f"[{phrase} [a [b]]]({u})")):
            with self.subTest(unreadable=label):
                found = problems(repl)
                self.assertTrue(found, label)
                self.assertTrue(found[0].endswith("(on a rule line with link syntax the check cannot read — not verified)"), found[0])
        for label, repl in (("one level in the label and the destination", f'[Dil [iç]]({u}/a(b) "{phrase}")'),):
            with self.subTest(read=label):
                found = problems(repl)
                self.assertTrue(found and found[0].endswith(sa.METADATA_HIDDEN), found)
        for label, repl in (("the visible label, one level", f"[{phrase} [a]]({u})"), ("plain text", phrase)):
            with self.subTest(visible=label):
                self.assertFalse(problems(repl), label)

    def test_the_rule_list_has_one_supported_shape(self):
        # İ5B-FIX-11 (Codex, C9 follow-up): a title opened before a rule line ('[x]: u "' or '[Dil](u "'), directly or
        # through an indented bridge line, swallowed rule 4 with write / check 0 / 0 — the numbered rule lines must be one
        # consecutive block 1..N at their real positions in the core, after a blank line; any other shape verifies nothing
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        lines = text.split("\n")
        i4 = next(i for i, l in enumerate(lines) if l.startswith("4. **"))
        u = "https://example.invalid"

        def shape(before, after=(), at=i4):
            f.write_text("\n".join(lines[:at] + list(before) + lines[at:at + 1] + list(after) + lines[at + 1:]), encoding="utf-8")
            return [p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("CORE-SHAPE")]

        for label, before, after in (("a reference title, directly", ["", f'[x]: {u} "'], ['"']),
                                     ("a reference title through an indented bridge", ["", f'[x]: {u} "', "    bridge"], ['"']),
                                     ("an inline title, directly", ["", f'[Dil]({u} "'], ['")']),
                                     ("an inline title through an indented bridge", ["", f'[Dil]({u} "', "    bridge"], ['")']),
                                     ("a plain paragraph before a rule (not a list item)", ["", "Not."], []),
                                     ("a loose list: a blank line between two rules (a declared false refusal)", [""], []),
                                     ("a continuation line between two numbered rules (a declared false refusal)", [], ["   devamı"])):
            with self.subTest(refused=label):
                found = shape(before, after)
                self.assertTrue(found, label)
                self.assertTrue(found[0].endswith("— no required text is verified"), found[0])
        with self.subTest(refused="a second numbered list in the core (a declared false refusal)"):
            end = next(i for i, l in enumerate(lines) if l.startswith("11. "))
            self.assertTrue(shape(["", "1. ikinci liste"], at=end + 1))
        with self.subTest(refused="a rule number out of order"):
            f.write_text(text.replace("\n5. **", "\n6. **", 1), encoding="utf-8")
            self.assertTrue([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("CORE-SHAPE")])
        with self.subTest(refused="rule 1 right after a paragraph line"):
            i1 = next(i for i, l in enumerate(lines) if l.startswith("1. **"))
            self.assertFalse(lines[i1 - 1].strip())                        # today: a blank line before rule 1
            f.write_text("\n".join(lines[:i1 - 1] + ["Not."] + lines[i1:]), encoding="utf-8")
            self.assertTrue([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("CORE-SHAPE")])
        with self.subTest(passes="a continuation line after the last rule (outside the block, not judged by the shape)"):
            end = next(i for i, l in enumerate(lines) if l.startswith("11. "))
            self.assertFalse(shape([], ["   devamı"], at=end))
        with self.subTest(passes="the real core (its links resolve only in place: test_the_real_instruction_files_pass)"):
            f.write_text(text, encoding="utf-8")
            self.assertFalse([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith(("CORE-SHAPE", "CORE-RULE"))])

    def test_a_reference_image_proves_no_required_text(self):
        # İ5B-FIX-11 (Codex, Claude Denetim001 EK-02): the rule only in a reference image's alt text — full, collapsed,
        # shortcut, or inside a visible link label — passed write / check 0 / 0; an image opener left after the links the
        # check reads makes the line 'not verified'. A visible reference link label still counts.
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        phrase = "Platform modülü 2 dil (en, tr), kiracı modülü 7 dil (en, tr, fr, es, zh, ar, ru)"
        line = next(l for l in text.split("\n") if l.startswith("4. **"))

        def problems(repl, definition):
            changed = text.replace(line, line.replace(phrase, repl, 1), 1)
            changed = changed.replace(sa.CORE_END, sa.CORE_END + "\n\n" + definition, 1)   # the definition after the core
            f.write_text(changed, encoding="utf-8")
            return [p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("CORE-RULE") and phrase in p]

        for label, repl, definition in (("a full reference image", f"![{phrase}][lang]", "[lang]: AGENTS.md"),
                                        ("a collapsed reference image", f"![{phrase}][]", f"[{phrase}]: AGENTS.md"),
                                        ("a shortcut reference image", f"![{phrase}]", f"[{phrase}]: AGENTS.md"),
                                        ("a reference image inside a visible link label", f"[X ![{phrase}][lang]](AGENTS.md)", "[lang]: AGENTS.md"),
                                        ("an undefined literal '![...]' (a declared false refusal)", f"![{phrase}]", "")):
            with self.subTest(refused=label):
                found = problems(repl, definition)
                self.assertTrue(found, label)
                self.assertTrue(found[0].endswith(sa.NOT_VERIFIED), found[0])
        for label, repl, definition in (("a reference link, the rule its visible label", f"[{phrase}][lang]", "[lang]: AGENTS.md"),
                                        ("a visible inline link label", f"[{phrase}](AGENTS.md)", ""),
                                        ("plain text with an escaped opener", phrase + " \\![x]", ""),
                                        ("plain text with an escaped link tail", phrase + " a\\](b)", ""),
                                        ("plain text with an autolink", phrase + " <https://example.invalid/x>", "")):
            with self.subTest(visible=label):
                self.assertFalse(problems(repl, definition), label)

    def test_the_core_markers_keep_their_physical_context(self):
        # İ5B-FIX-12 (Codex Denetim001 C10-F1, Codex root): extract_core strips the block's edges, so a core:start inside a
        # title opened before it, or a standalone core:start right after such an opener, hid rule 1 with write / check
        # 0 / 0 — the shape is measured on the source's physical lines: the line before rule 1 must be really blank
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        lines = text.split("\n")
        s = next(i for i, l in enumerate(lines) if sa.CORE_START in l)
        r1 = next(i for i, l in enumerate(lines) if i > s and sa.CORE_RULE_LINE.match(l))
        self.assertEqual(lines[s].strip(), sa.CORE_START)
        self.assertFalse(lines[r1 - 1].strip())                            # today: a real blank line before rule 1
        head, rule1, rest = lines[:s] + lines[s + 1:r1 - 1], lines[r1], lines[r1 + 1:]

        def shape(middle):
            f.write_text("\n".join(head + middle + rest), encoding="utf-8")
            problems = sa.check_instruction_budget(self.manifest)[0]
            return [p for p in problems if p.startswith("CORE-SHAPE")], [p for p in problems if p.startswith("CORE-RULE")]

        for label, middle, why in (
                ("core:start inside a reference title, rule 1 right after", ['[hidden]: AGENTS.md "' + sa.CORE_START, rule1 + '"'],
                 "the physical line before rule 1 is not blank"),
                ("a standalone core:start after a reference title opener", ['[hidden]: AGENTS.md "', sa.CORE_START, rule1 + '"'],
                 "the physical line before rule 1 is not blank"),
                ("core:start inside an inline title, rule 1 right after", ['[Dil](AGENTS.md "' + sa.CORE_START, rule1 + '")'],
                 "the physical line before rule 1 is not blank"),
                ("core:start and rule 1 on one physical line", [sa.CORE_START + rule1], "not 1..")):
            with self.subTest(refused=label):
                found, _ = shape(middle)
                self.assertTrue(found and why in found[0], found)
        with self.subTest(passes="core:start, a real blank line, then the visible rules 1..N"):
            self.assertEqual(shape([sa.CORE_START, "", rule1]), ([], []))
        with self.subTest(passes="the real core"):
            f.write_text(text, encoding="utf-8")
            self.assertFalse([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith(("CORE-SHAPE", "CORE-RULE"))])

    def test_an_escaped_bang_before_a_link_is_text(self):
        # İ5B-FIX-12 (Codex Denetim001 C10-F2): '\![rule](AGENTS.md)' is a visible link after a literal '!', and was
        # refused as an image — one shared range / escape rule for both readers: an image only after an even number of
        # backslashes (none included); otherwise a link whose destination and title alone are hidden
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        phrase = "Platform modülü 2 dil (en, tr), kiracı modülü 7 dil (en, tr, fr, es, zh, ar, ru)"
        line = next(l for l in text.split("\n") if l.startswith("4. **"))
        bs = chr(92)

        def lost(repl):
            f.write_text(text.replace(line, line.replace(phrase, repl, 1), 1), encoding="utf-8")
            return [p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("CORE-RULE") and phrase in p]

        for label, repl in (("one backslash: a visible link after a literal '!'", bs + "![" + phrase + "](AGENTS.md)"),
                            ("three backslashes: still escaped", bs * 3 + "![" + phrase + "](AGENTS.md)")):
            with self.subTest(visible=label):
                self.assertFalse(lost(repl), label)
        for label, repl in (("two backslashes: a real image", bs * 2 + "![" + phrase + "](AGENTS.md)"),
                            ("no backslash: a real image", "![" + phrase + "](AGENTS.md)")):
            with self.subTest(hidden=label):
                self.assertTrue(lost(repl), label)
        with self.subTest(hidden="an escaped bang, the rule only in the link's title"):
            found = lost(bs + '![Dil](AGENTS.md "' + phrase + '")')
            self.assertTrue(found and found[0].endswith(sa.METADATA_HIDDEN), found)

    def test_the_core_start_marker_has_no_open_context(self):
        # İ5B-FIX-13 (Codex root, C11 follow-up): a fence opened right before core:start and closed after core:end (or
        # inside the core) made the rules a code example in the source, with write / check 0 / 0 — the context at the
        # marker is read on the whole source by one small scanner (fence, HTML comment, inline code on its line)
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        lines = text.split("\n")
        s = next(i for i, l in enumerate(lines) if sa.CORE_START in l)
        e = next(i for i, l in enumerate(lines) if sa.CORE_END in l)
        r11 = next(i for i, l in enumerate(lines) if l.startswith("11. "))

        def context(doc):
            f.write_text(doc, encoding="utf-8")
            return [p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("CORE-CONTEXT")]

        def around(open_line, close_line, close_at):
            out = list(lines)
            out.insert(close_at + 1, close_line)
            out.insert(s, open_line)
            return "\n".join(out)

        for label, doc, why in (
                ("a backtick fence opened before core:start, closed after core:end", around("```", "```", e), "fenced code block"),
                ("a tilde fence opened before core:start, closed after core:end", around("~~~", "~~~", e), "fenced code block"),
                ("a four-backtick fence opened before core:start, closed inside the core", around("````", "````", r11), "fenced code block"),
                ("a tilde fence closed by a longer one after core:end", around("~~~~", "~~~~~", e), "fenced code block"),
                ("a fence opened before core:start after a comment opener shown in inline code",
                 around("```", "```", e).replace("\n```\n" + sa.CORE_START, "\nÖrnek: `<!--` işaretidir.\n```\n" + sa.CORE_START, 1),
                 "fenced code block"),
                ("core:start after text on its line (a declared safe refusal)", text.replace(sa.CORE_START, "not " + sa.CORE_START, 1), "alone"),
                ("core:start indented as code (a declared safe refusal)", text.replace(sa.CORE_START, "    " + sa.CORE_START, 1), "alone")):
            with self.subTest(refused=label):
                found = context(doc)
                self.assertTrue(found and why in found[0] and found[0].endswith("— no required text is verified"), found)
        # the CORE-CONTEXT check only: these inputs raise no CORE-CONTEXT. Before İ5B-FIX-14 one of them (a literal '<!--'
        # inside a closed fence) was still refused end to end by the global heading reader, which removed comments before
        # fences; İ5B-FIX-14 fixed that reader, and test_the_heading_reader_reads_comments_and_fences_in_order covers it
        for label, ins in (("a closed backtick fence before core:start", ["```", "örnek", "```", ""]),
                           ("a closed tilde fence before core:start", ["~~~", "örnek", "~~~", ""]),
                           ("a literal comment opener inside a closed fence (CORE-CONTEXT only)", ["```", "<!-- örnek", "```", ""]),
                           ("a literal fence inside a closed HTML comment", ["<!--", "```", "-->", ""]),
                           ("a comment opener inside inline code", ["Örnek: `<!--` işaretidir.", ""]),
                           ("an unclosed comment before core:start, ended by the marker's own '-->' (İ5B-FIX-04's contract)",
                            ["<!--", "açık yorum"])):
            with self.subTest(passes=label):
                self.assertEqual(context("\n".join(lines[:s] + ins + lines[s:])), [])
        with self.subTest(passes="the real file"):
            self.assertEqual(context(text), [])

    def test_the_heading_reader_reads_comments_and_fences_in_order(self):
        # İ5B-FIX-14 (Codex root, Codex Denetim001): visible_text removed every HTML comment before the heading reader read
        # fences, and joined the pieces — a literal '<!--' in a closed code example swallowed the headings after it, and
        # '#<!--x--># 2. …' became a level-2 heading that is not there. One scanner now masks comments in place (spaces,
        # line ends kept) outside fences and inline code, with each reader's own fence grammar
        bad = ("HEADING", "SECTION", "CORE")
        agents = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        before = agents.index(sa.CORE_START)                                       # required headings follow it
        after = agents.index(sa.CORE_END) + len(sa.CORE_END) + 1                    # and follow this point too
        for label, at, ins in (("a closed backtick code example holding a literal '<!--'", before, "```\n<!-- örnek\n```\n\n"),
                               ("a closed tilde code example holding a literal '<!--'", before, "~~~\n<!-- örnek\n~~~\n\n"),
                               ("a comment opener shown in inline code", after, "\nÖrnek: `<!--` işaretidir.\n"),
                               ("an escaped comment opener", after, "\nÖrnek: \\<!-- işaretidir.\n"),
                               ("a fence line inside a real comment", before, "<!--\n```\n-->\n\n")):
            with self.subTest(agents_passes=label):
                self.redirect("AGENTS.md", f)
                f.write_text(agents[:at] + ins + agents[at:], encoding="utf-8")
                self.assertFalse([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith(bad)], label)
        ref = (REPO / self.REF).read_text(encoding="utf-8")
        with self.subTest(ref_passes="a code example holding a literal '<!--' in a section body"):
            problems = self.ref_with(ref.replace("## 3. Port Şeması", "```\n<!-- örnek\n```\n\n## 3. Port Şeması", 1))
            self.assertFalse([p for p in problems if p.startswith(bad)], problems)
            self.assertTrue(any("<!-- örnek" in l for l in sa.level2_sections(sa.visible_text(
                ref.replace("## 3. Port Şeması", "```\n<!-- örnek\n```\n\n## 3. Port Şeması", 1)))["2. Proje Klasör Yapısı"][0]))
        with self.subTest(ref_refused="'#<!--x--># 2. …' is not a level-2 heading (comment deletion must not create one)"):
            problems = self.ref_with(ref.replace("## 2. Proje Klasör Yapısı", "#<!--x--># 2. Proje Klasör Yapısı", 1))
            self.assertTrue([p for p in problems if p.startswith("SECTION") and "2. Proje Klasör Yapısı" in p], problems)
        with self.subTest(diagnosis="a heading only inside a code example is said to be in a code block, not in a comment"):
            head = next(l for l in agents.split("\n") if l.startswith("## Çekirdek"))    # a '<!--' in the example runs past it
            f.write_text(agents.replace(head, "```\n<!-- örnek\n" + head + "\n```\n-->", 1), encoding="utf-8")
            self.redirect("AGENTS.md", f)
            lost = [p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("HEADING") and "Çekirdek" in p]
            self.assertTrue(lost and lost[0].count("(only inside a code block)") == 1, lost)

    def test_the_core_view_masks_comments_outside_fences(self):
        # İ5B-FIX-14: the core block's own view removed comments before fences too — a tilde code example holding a
        # literal '<!--' in the core's preamble hid every rule after it (CORE-RULE). With the same scanner (the core's
        # fence grammar) the rules stay found; the literal opener is still refused as raw HTML, the core's existing rule
        # (İ5B-FIX-09: a fenced example is free before the core, not inside it)
        agents = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        head = next(l for l in agents.split("\n") if l.startswith("## Çekirdek"))
        f.write_text(agents.replace(head, head + "\n\n~~~\n<!-- örnek\n~~~", 1), encoding="utf-8")
        problems = sa.check_instruction_budget(self.manifest)[0]
        self.assertFalse([p for p in problems if p.startswith("CORE-RULE")], problems)
        self.assertTrue([p for p in problems if p.startswith("CORE-HTML") and "(<!--)" in p], problems)

    def html_fixture(self):
        ref = (REPO / self.REF).read_text(encoding="utf-8")
        anchor = self.sections()[0]["anchors"][0]                                  # section 2's first anchor sentence
        r2a, r2b = ref.index("## 2. Proje Klasör Yapısı"), ref.index("## 3. Port Şeması")
        return ref, anchor, r2a, r2b, ref.index(anchor, r2a)

    def section_2_lost(self, text: str) -> list[str]:
        return [p for p in self.ref_with(text) if p.startswith(("SECTION", "ANCHOR")) and "2. Proje Klasör Yapısı" in p]

    def test_raw_html_around_a_required_text_hides_it(self):
        # İ5B-FIX-15 (C13 audits, Codex C13-F1): the heading and section reader read raw HTML as plain text — a required
        # section inside '<script type="text/plain">', '<pre>' or '<div>', or an anchor sentence inside a mid-paragraph
        # '<script>' or '<span hidden>', still counted (write / check 0 / 0). An HTML block now ends at its own end
        # condition (a blank line for a '<div>' block), an element at its own closing tag, and either hides what it holds
        ref, anchor, r2a, r2b, ac = self.html_fixture()
        sc, bs = '<script type="text/plain">', chr(92)
        line_end = ref.rfind("\n", 0, r2a - 1)                                    # the end of the paragraph before section 2
        wrap = lambda before, after: ref[:r2a] + before + ref[r2a:r2b] + after + ref[r2b:]
        for label, text in (
                *((f"section 2 inside an HTML block <{t.split()[0]}>", wrap(f"<{t}>\n", f"</{t.split()[0]}>\n\n"))
                  for t in ('script type="text/plain"', "pre", "style", "textarea")),
                ("section 2's heading inside <div>, no blank line", ref[:r2a] + "<div>\n" + ref[r2a:].replace("\n", "\n</div>\n", 1)),
                ("the anchor inside a mid-paragraph <script>", ref[:ac] + sc + anchor + "</script>" + ref[ac + len(anchor):]),
                ("the anchor inside <span hidden>", ref[:ac] + "<span hidden>" + anchor + "</span>" + ref[ac + len(anchor):]),
                ("a <script> opened mid-line in the paragraph before, closed after section 2",
                 ref[:line_end] + " " + sc + ref[line_end:r2b] + "</script>\n\n" + ref[r2b:]),
                ("a <script> opened mid-line before section 2, never closed", ref[:line_end] + " " + sc + ref[line_end:]),
                ("a <script> holding '</pre>' and a blank line (only its own closing tag ends it)", wrap(sc + "\n</pre>\n\n", "</script>\n\n")),
                ("<span hidden> left open across a blank line", wrap("Visible prefix <span hidden>\n\n", "</span>\n\n")),
                ("<div>example</div> right before the heading (the HTML block ends at a blank line, not at </div>)",
                 ref[:r2a] + "<div>example</div>\n" + ref[r2a:]),
                ("<script/> (a non-void self-closed tag still opens the element)", wrap("<script/>\n\n", "</script>\n\n")),
                ("nested <div>: the inner closing tag does not end the outer", wrap("Metin <div><div>x</div>\n\n", "</div>\n\n")),
                ("a </span> in a closed code example does not close the element", wrap("Metin <span hidden>\n\n```\n</span>\n```\n\n", "</span>\n\n")),
                ("a </span> in inline code does not close the element", wrap("Metin <span hidden> `</span>`\n\n", "</span>\n\n")),
                ("an escaped </span> does not close the element", wrap("Metin <span hidden> " + bs + "</span>\n\n", "</span>\n\n")),
                ("<span/> never closed (a declared safe refusal)", ref[:r2a] + "Metin <span/>\n\n" + ref[r2a:])):
            with self.subTest(refused=label):
                self.assertTrue(self.section_2_lost(text), label)
        agents = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        after = agents.index(sa.CORE_END) + len(sa.CORE_END)
        req = next(f for f in self.manifest["instruction_budget"]["files"] if f["path"] == "AGENTS.md")["required_headings"]
        head = next(l for l in agents[after:].split("\n") if l.startswith("## ") and any(l[3:].startswith(h) for h in req))
        at = agents.index(head, after)
        f = Path(self.tmp.name) / "AGENTS.md"
        for label, text in (("an AGENTS.md required heading inside <pre>", agents[:at] + "<pre>\n" + head + "\n</pre>\n" + agents[at + len(head) + 1:]),
                            ("an AGENTS.md required heading inside <span hidden> across a blank line",
                             agents[:at] + "Metin <span hidden>\n\n" + head + "\n</span>\n" + agents[at + len(head) + 1:])):
            with self.subTest(refused=label):
                f.write_text(text, encoding="utf-8")
                self.redirect("AGENTS.md", f)
                self.assertTrue([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("HEADING")], label)

    def test_unrelated_or_shown_html_keeps_the_required_text(self):
        # İ5B-FIX-15: only the required text inside an HTML context is lost — unrelated closed HTML, HTML shown in code,
        # escaped or in an autolink, and a void tag leave every required section, body and anchor visible
        ref, anchor, r2a, r2b, ac = self.html_fixture()
        bs = chr(92)
        before = lambda ins: ref[:r2a] + ins + ref[r2a:]
        for label, text in (("unrelated <div>Example</div> appended to the last section", ref.rstrip("\n") + "\n<div>Example</div>\n"),
                            ("unrelated <div>Example</div>, a blank line, then the section", before("<div>Example</div>\n\n")),
                            ("unrelated inline HTML in section 2's body", ref[:ac] + "Not: <b>kalın</b> ve <br> satır. " + ref[ac:]),
                            ("an unrelated closed <details> block", ref.replace("## 3. Port Şeması", "<details>\n<summary>x</summary>\nnot\n</details>\n\n## 3. Port Şeması", 1)),
                            ("HTML shown in a closed code example", ref.replace("## 3. Port Şeması", "```\n<script>\n## 2. not a heading\n</script>\n```\n\n## 3. Port Şeması", 1)),
                            ("HTML in inline code inside section 2", ref[:ac] + "Örnek `<script>` ve `<span hidden>`. " + ref[ac:]),
                            ("inline HTML at a paragraph start, the heading right after", before("<b>Not:</b> metin\n")),
                            ("a void <br/> and a blank line", before("Metin <br/>\n\n")),
                            ("a closed nested <div><div>x</div></div> and a blank line", before("<div><div>x</div></div>\n\n")),
                            ("a <script> whose text holds '<script>' ends at its own closing tag", before('Metin <script>var s = "<script>";</script>\n\n')),
                            ("an escaped <span hidden>", before("Metin " + bs + "<span hidden>\n\n")),
                            ("<script/> shown in inline code", before("Metin `<script/>`\n\n")),
                            ("a URI autolink", before("Bkz. <https://example.com/x>\n\n")),
                            ("an e-mail autolink", before("Bkz. <ops@example.com>\n\n")),
                            ("an escaped '<' in prose", before("Metin a" + bs + "<b c\n\n")),
                            ("'a<b' in inline code", before("Metin `a<b` c\n\n")),
                            ("'a < b' with spaces", before("Metin a < b c\n\n")),
                            ("an unreadable tag shown in a closed code example", before('```\n<script data-example="<">\n```\n\n')),
                            ("an unreadable tag after every required text", ref.rstrip("\n") + "\n\nMetin <span\n")):
            with self.subTest(passes=label):
                self.assertFalse([p for p in self.ref_with(text) if p.startswith(("HEADING", "SECTION", "ANCHOR", "CORE"))], label)
        with self.subTest(passes="the real files"):
            for path in ("AGENTS.md", "CLAUDE.md", self.REF):
                t = (REPO / path).read_text(encoding="utf-8")
                self.assertEqual(sa.visible_text(t), sa.scan_comments(t, sa.heading_fence_opener, sa.heading_fence_closes)[0], path)

    def test_an_html_opener_the_check_cannot_read_is_not_verified(self):
        # İ5B-FIX-15 (Codex): a tag the reader cannot read — a '<' inside an attribute — was skipped as plain text, and
        # the required text inside it counted. Such an opener now hides the rest of the file, and the lost required text
        # says why: 'not verified'. A bare 'a<b' is refused too; its message may say only 'missing' (a known limit)
        ref, anchor, r2a, r2b, ac = self.html_fixture()
        reason = "the check cannot read — not verified"
        for label, text in (("the anchor inside <script data-example=\"<\">", ref[:ac] + '<script data-example="<">' + anchor + "</script>" + ref[ac + len(anchor):]),
                            ("a '<' in an attribute before section 2", ref[:r2a] + 'Metin <span title="a<b">x</span>\n\n' + ref[r2a:])):
            with self.subTest(not_verified=label):
                lost = self.section_2_lost(text)
                self.assertTrue(lost and all(reason in p for p in lost), lost)
        with self.subTest(refused="a bare 'a<b' in prose before section 2"):
            self.assertTrue(self.section_2_lost(ref[:r2a] + "Metin a<b c\n\n" + ref[r2a:]))

    def test_an_escaped_less_than_or_an_autolink_is_not_raw_html(self):
        # İ5B-FIX-10 (C8 audits, false refusals): an odd number of backslashes escapes '<', and a URI or an e-mail address
        # in angle brackets is an autolink; an even number of backslashes and a real tag stay raw HTML
        text = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        mid = next(l for l in text.split("\n") if l.startswith("6. **"))

        def refused(add, before_core=False):
            changed = (text.replace(sa.CORE_START, add + "\n" + sa.CORE_START, 1) if before_core
                       else text.replace(mid, mid + " " + add, 1))
            f.write_text(changed, encoding="utf-8")
            return any(p.startswith("CORE-HTML") for p in sa.check_instruction_budget(self.manifest)[0])

        for where in (False, True):
            for label, add in (("an escaped less-than", "\\<b> literal"), ("three backslashes", "\\\\\\<b> literal"),
                               ("a URI autolink", "<https://example.invalid/x>"), ("an e-mail autolink", "<ops@example.invalid>")):
                with self.subTest(allowed=label, before_core=where):
                    self.assertFalse(refused(add, where), label)
            for label, add in (("a real tag", "<b>x</b>"), ("two backslashes then a tag", "\\\\<b> x"),
                               ("angle brackets with a space inside", "<https://example.invalid x>"),
                               ("a one-letter scheme", "<a:b>")):
                with self.subTest(refused=label, before_core=where):
                    self.assertTrue(refused(add, where), label)

    def test_every_required_core_text_is_on_a_numbered_rule_line_today(self):
        core = sa.extract_core("AGENTS.md")
        lines = [l for l in core.split("\n") if sa.CORE_RULE_LINE.match(l)]
        self.assertEqual(len(lines), 11)
        for needle in self.manifest["instruction_budget"]["core"]["required_text"]:
            with self.subTest(needle=needle[:40]):
                self.assertTrue(any(sa.on_rule_line(needle, l) for l in lines))   # İ5B-FIX-07: and not inside a larger span

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
