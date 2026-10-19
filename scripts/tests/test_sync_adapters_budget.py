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

    def test_one_context_reader_for_comments_code_and_html(self):
        # İ5B-FIX-16 (C14 audits; Codex C14-F1 / -F2, Claude Denetim001 C14-F1 / -F2, Codex root N1c, N2-N4): the comment,
        # code and HTML readers each kept their own grammar, so the context they agree on was wrong. Inside an HTML block
        # the text is raw (no code span, escape or fence); in a paragraph Markdown goes on, so a closing tag shown in code
        # does not close an element; a raw-text element ends at its own real closing tag; a code span may cross line ends
        # in its paragraph and needs a run of the same length; an inline '<!--' is a comment only if it closes in its
        # paragraph. Every case below is hidden from a reader, or shown to it, as written
        ref, anchor, r2a, r2b, ac = self.html_fixture()
        sc, bs, bt = '<script type="text/plain">', chr(92), "`"
        wrap = lambda before, after: ref[:r2a] + before + ref[r2a:r2b] + after + ref[r2b:]
        before = lambda ins: ref[:r2a] + ins + ref[r2a:]
        for label, text in (
                ("Codex F1: a '</div>' in a script inside a div does not close the div",
                 wrap("<div>\n" + sc + "\n</div>\n\n", "</script>\n</div>\n\n")),
                ("Codex F1: a '</div>' in a textarea inside a div does not close the div",
                 wrap("<div>\n<textarea>\n</div>\n\n", "</textarea>\n</div>\n\n")),
                ("Codex F1: a '</span>' in a script inside a span does not close the span",
                 wrap("Metin <span hidden> " + sc + " </span>\n\n", "</script> </span>\n\n")),
                ("Claude: an escaped backtick opens no code span (script)", wrap(bs + bt + sc + bt + "\n\n", bs + bt + "</script>" + bt + "\n\n")),
                ("Claude: an escaped backtick opens no code span (div hidden)", wrap(bs + bt + "<div hidden>" + bt + "\n\n", bs + bt + "</div>" + bt + "\n\n")),
                ("Claude: a double backtick without its pair opens no code span", wrap(bt * 2 + sc + bt + "\n\n", bt * 2 + "</script>" + bt + "\n\n")),
                ("Claude: a code span across two lines holds the '</div>'", wrap("<div hidden>\n\na " + bt + "b\nc </div>" + bt + " d\n\n", "</div>\n\n")),
                ("Codex render: a script in a paragraph is not closed by a '</script>' shown in code", before("Metin " + sc + "\n" + bt + "</script>" + bt + "\n\n")),
                ("N1c: a code span across two lines holds the '<!--'; the div hidden after it is real",
                 wrap("Metin " + bt + "a\nz <!--" + bt + " <div hidden> -->\n\n", "</div>\n\n")),
                ("N2: inside an HTML block backticks are raw text, so the span is real", wrap("</p>\n" + bt + "<span hidden>" + bt + "\n\n", "</span>\n\n")),
                ("N3: inside an HTML block a fence line is raw text, so the span is real",
                 wrap("</div>\n" + bt * 3 + "\n<span hidden>\n\n", "</span>\n" + bt * 3 + "\n\n")),
                ("N4: an inline '<!--' not closed in its paragraph is text; the div hidden block is real",
                 wrap("Metin <!--\n\n<div hidden> -->\n\n", "</div>\n\n")),
                ("a fence-looking line in an HTML block does not pair with a real code example after it",
                 wrap("<div>\n" + bt * 3 + "\n</div>\n\n" + bt * 3 + "\nörnek\n" + bt * 3 + "\n\nMetin <span hidden>\n\n", "</span>\n\n")),
                ("a '<!--' in an HTML block that does not close in the block stays open (not verified)",
                 wrap("</p>\n<!-- x\n\n", "\n-->\n\n"))):
            with self.subTest(hidden=label):
                self.assertTrue(self.section_2_lost(text), label)
        with self.subTest(not_verified="a '<!--' left open in an HTML block is said to be a comment the check cannot read"):
            lost = self.section_2_lost(wrap("</p>\n<!-- x\n\n", "\n-->\n\n"))
            self.assertTrue(lost and all("code span or comment" in p and "not verified" in p for p in lost), lost)
        with self.subTest(declared="the span form of N1c: refused by the declared element model, not a measured hiding"):
            self.assertTrue(self.section_2_lost(wrap("Metin " + bt + "a\nz <!--" + bt + " <span hidden> -->\n\n", "</span>\n\n")))
        for label, text in (
                ("Codex F2: a block-level script is closed by its '</script>' even between backticks", before(sc + "\n" + bt + "</script>" + bt + "\n\n")),
                ("Codex render: a script in a paragraph is closed by a bare '</script>'", before("Metin " + sc + "\n</script>\n\n")),
                ("Codex F2: a fence-looking line inside a block-level script is raw text", before(sc + "\n" + bt * 3 + "\n</script>\n\n")),
                ("Codex F2: a tilde line inside a block-level style is raw text", before("<style>\n~~~\n</style>\n\n")),
                ("N1: a line starting with '<!--' ends the paragraph, so the backtick before it is text and the comment is real",
                 wrap("Metin " + bt + "a\n<!--" + bt + " <span hidden> -->\n\n", "</span>\n\n")),
                ("N4c: a block comment closed on the next line hides only itself", before("<!--\nnot <div hidden> -->\n\n")),
                ("Claude reverse: an escaped backtick, then a real code span that shows a tag", before(bs + bt + " and " + bt + sc + bt + " shown as code\n\n")),
                ("an inline comment closed in its paragraph", before("Metin <!-- not\nshown --> görünür\n\n")),
                ("a block comment may hold a blank line", before("<!--\nnot\n\nshown -->\n\n")),
                ("an escaped backtick before a real inline comment opens no code span", before("Metin " + bs + bt + " <!-- <div hidden> --> sonra " + bt + "\n\n"))):
            with self.subTest(passes=label):
                self.assertFalse([p for p in self.ref_with(text) if p.startswith(("HEADING", "SECTION", "ANCHOR", "CORE"))], label)
        with self.subTest(passes="the real files"):
            for path in ("AGENTS.md", "CLAUDE.md", self.REF):
                t = (REPO / path).read_text(encoding="utf-8")
                self.assertEqual(sa.visible_text(t), sa.scan_comments(t, sa.heading_fence_opener, sa.heading_fence_closes)[0], path)

    def test_the_same_reader_holds_for_agents_md_and_the_core(self):
        # İ5B-FIX-16 (Claude Denetim001 C14-F1): the same escaped and unpaired backticks hid a required heading after the
        # core and the core rules themselves; the core's raw-HTML rule now reads code spans with the same lexer
        agents = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        sc, bs, bt = '<script type="text/plain">', chr(92), "`"
        after = agents.index(sa.CORE_END) + len(sa.CORE_END)
        req = next(x for x in self.manifest["instruction_budget"]["files"] if x["path"] == "AGENTS.md")["required_headings"]
        head = next(l for l in agents[after:].split("\n") if l.startswith("## ") and any(l[3:].startswith(h) for h in req))
        at = agents.index(head, after)
        with self.subTest(hidden="an AGENTS.md required heading inside an escaped-backtick script"):
            f.write_text(agents[:at] + bs + bt + sc + bt + "\n\n" + head + "\n\n" + bs + bt + "</script>" + bt + "\n" + agents[at + len(head) + 1:], encoding="utf-8")
            self.assertTrue([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("HEADING")])
        core_head = next(l for l in agents.split("\n") if l.startswith("## Çekirdek"))
        for label, opening, closing in (("escaped backtick", bs + bt + sc + bt, bs + bt + "</script>" + bt),
                                        ("unpaired double backtick", bt * 2 + sc + bt, bt * 2 + "</script>" + bt)):
            with self.subTest(core=label):
                f.write_text(agents.replace(core_head, core_head + "\n\n" + opening, 1).replace(sa.CORE_END, sa.CORE_END + "\n\n" + closing, 1), encoding="utf-8")
                self.assertTrue([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("CORE-HTML")], label)
        with self.subTest(core_passes="an escaped backtick, then a real code span that shows a tag on a rule line"):
            mid = next(l for l in agents.split("\n") if l.startswith("6. **"))
            f.write_text(agents.replace(mid, mid + " " + bs + bt + " ve " + bt + "<div hidden>" + bt + " yazılmaz.", 1), encoding="utf-8")
            self.assertFalse([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith(("CORE-HTML", "CORE-RULE"))])

    def test_the_rule_line_readers_use_the_same_lexer(self):
        # İ5B-FIX-16 (Codex root): the rule-line readers kept their own code-span regex — after an escaped backtick it
        # paired the wrong runs, so a phrase that is a code span of its own counted as stated. The core's rule (a phrase
        # shown in code is not stated; a short term is) is unchanged; only the grammar is the shared lexer's
        bs, bt = chr(92), "`"
        for label, needle, line, want in (
                ("a phrase in a real code span after an escaped backtick", "Platform modülü 2 dil",
                 "4. " + bs + bt + " and " + bt + "Platform modülü 2 dil" + bt, False),
                ("the same phrase after a plain backtick pair", "Platform modülü 2 dil", "4. " + bt + "x" + bt + " and Platform modülü 2 dil", True),
                ("a short term in a code span still counts", "TenantId", "4. " + bs + bt + " and " + bt + "TenantId" + bt, True),
                ("a phrase after an unpaired double backtick is text", "Platform modülü 2 dil", "4. " + bt * 2 + "x" + bt + " Platform modülü 2 dil", True)):
            with self.subTest(label):
                self.assertIs(sa.on_rule_line(needle, line), want, label)

    def test_what_the_reader_cannot_read_is_said_to_be_not_verified(self):
        # İ5B-FIX-16 (Claude Denetim001 C14-F2, Codex): 'List<T>' written as text opens an element that is never closed —
        # still refused (a declared safe limit), but the lost heading said '(only inside a code block)'. A code span whose
        # pair lies only past a block quote or list line, and a bare 'a<b c', are not read either: each says why
        agents = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        h1 = next(l for l in agents.split("\n") if l.startswith("## 1."))
        f.write_text(agents.replace(h1, h1 + "\n\nÖrnek: List<T> kullanılır.", 1), encoding="utf-8")
        lost = [p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("HEADING")]
        self.assertTrue(lost and all("not verified" in p and "code block" not in p for p in lost), lost[:2])
        ref, anchor, r2a, r2b, ac = self.html_fixture()
        bt = "`"
        for label, text in (("a code span whose pair lies past a block quote line", ref[:r2a] + "Metin " + bt + "a\n> b" + bt + " <div hidden>\n\n" + ref[r2a:r2b] + "</div>\n\n" + ref[r2b:]),
                            ("a bare 'a<b c'", ref[:r2a] + "Metin a<b c\n\n" + ref[r2a:])):
            with self.subTest(not_verified=label):
                lost = self.section_2_lost(text)
                self.assertTrue(lost and all("not verified" in p for p in lost), lost)

    def test_a_tag_ends_outside_quotes_and_a_close_needs_evidence(self):
        # İ5B-FIX-17 (C15 audits; Codex C15-F1 / -F2, Claude Denetim001 C15-F1): a tag ended at the first '>', even inside
        # a quoted value, so a '</div>' or '<script>' written in an attribute became a tag of its own. And a closing tag
        # in indented code, in a link's destination or title, or in a paragraph whose container the reader cannot prove
        # closed the open element. A tag now ends outside quotes (a '<' in a quoted value is still not read: not
        # verified), and a closing tag that would close an open element counts only in an HTML block or on a proven
        # plain paragraph line; elsewhere it is not verified. A closing tag that closes nothing stays harmless
        ref, anchor, r2a, r2b, ac = self.html_fixture()
        sc = '<script type="text/plain">'
        wrap = lambda before, after: ref[:r2a] + before + ref[r2a:r2b] + after + ref[r2b:]
        before = lambda ins: ref[:r2a] + ins + ref[r2a:]
        nv = "not verified"
        for label, text in (
                ("Codex F1: a '</div>' in a double-quoted value", wrap('<div hidden title="> </div>">\n\n', "</div>\n\n")),
                ("Codex F1: a '</div>' in a single-quoted value", wrap("<div hidden title='> </div>'>\n\n", "</div>\n\n")),
                ("Claude: a '</script>' in indented code", wrap("Not: " + sc + " başlar.\n\n    </script>\n\n", "</script>\n\n")),
                ("Claude: a '</div>' in indented code", wrap("Not: <div hidden> başlar.\n\n    </div>\n\n", "</div>\n\n")),
                ("Claude: a '</div>' in tab-indented code", wrap("Not: <div hidden> başlar.\n\n\t</div>\n\n", "</div>\n\n")),
                ("Claude: a '</div>' in indented code inside a list item", wrap("- madde <div hidden>\n\n      </div>\n\n", "</div>\n\n")),
                ("Claude: a '</script>' in a link title", wrap("Not: " + sc + ' [x](https://example.com/a "</script>")\n\n', "</script>\n\n")),
                ("Claude: a '</div>' in a link title", wrap('Not: <div hidden> [x](https://example.com/a "</div>")\n\n', "</div>\n\n")),
                ("a '</div>' in a paragraph that may continue a list item", wrap("- madde <div hidden>\n\n  devam </div>\n\n", "</div>\n\n")),
                ("a '</div>' on a block quote line", wrap("Metin <div hidden>\n> alıntı </div>\n\n", "</div>\n\n")),
                ("a '</div>' in a paragraph with link syntax the check cannot read", wrap('Metin <div hidden> [[[x]]](y "</div>")\n\n', "</div>\n\n"))):
            with self.subTest(not_verified=label):
                lost = self.section_2_lost(text)
                self.assertTrue(lost and all(nv in p for p in lost), lost)
        with self.subTest(hidden="a quoted value may hold '>': the element is real and hides what follows"):
            self.assertTrue(self.section_2_lost(wrap('<div hidden title="a>b">\n\n', "</div>\n\n")))
        with self.subTest(not_verified="Codex F2: a '<script>' in a quoted value is a tag the check cannot read, not a script"):
            lost = self.section_2_lost(before('<div title="> <script type=text/plain>">örnek</div>\n\n'))
            self.assertTrue(lost and all("HTML tag" in p and "cannot read" in p and "never closed" not in p for p in lost), lost)
        for label, text in (
                ("a closed element whose quoted value holds '>'", before('<div title="x>y">z</div>\n\n')),
                ("a '</div>' in indented code with nothing open", before("Not:\n\n    </div>\n\n")),
                ("a '</div>' in a link title with nothing open", before('Not: [x](https://example.com/a "</div>")\n\n')),
                ("a real '</div>' on an indented paragraph continuation line", before("Metin <div hidden>\n    </div>\n\n")),
                ("a type-7 line whose quoted value holds '>' starts an HTML block, read raw", before('<span hidden title="a>b">\n`</span>`\n\n'))):
            with self.subTest(passes=label):
                self.assertFalse([p for p in self.ref_with(text) if p.startswith(("HEADING", "SECTION", "ANCHOR", "CORE"))], label)
        agents = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        after = agents.index(sa.CORE_END) + len(sa.CORE_END)
        req = next(x for x in self.manifest["instruction_budget"]["files"] if x["path"] == "AGENTS.md")["required_headings"]
        head = next(l for l in agents[after:].split("\n") if l.startswith("## ") and any(l[3:].startswith(h) for h in req))
        at = agents.index(head, after)
        with self.subTest(not_verified="Claude: an AGENTS.md required heading after a '</script>' in indented code"):
            f.write_text(agents[:at] + "Not: " + sc + " başlar.\n\n    </script>\n\n" + head + "\n\n</script>\n" + agents[at + len(head) + 1:], encoding="utf-8")
            lost = [p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("HEADING")]
            self.assertTrue(lost and all(nv in p for p in lost), lost[:2])

    def test_a_closing_tag_has_the_commonmark_form(self):
        # İ5B-FIX-18 (C16 audits; Claude Denetim001 C16-F1, Codex root): a closing tag with an attribute or a slash —
        # '</div x>', '</div/>', '</div title="x">' — is text in a CommonMark paragraph, written escaped, but the reader
        # let it close the open element. A closing tag is now '</name', ASCII spaces or tabs with at most one line end,
        # and '>' — the same rule for the closing arm of an HTML block's start line. In an HTML block a browser still
        # reads '</div x>' as a closing tag, so it closes there. NBSP is not CommonMark whitespace: a normative model,
        # not a claim about every renderer
        ref, anchor, r2a, r2b, ac = self.html_fixture()
        sc, bt, nbsp = '<script type="text/plain">', "`", chr(160)
        wrap = lambda before, after: ref[:r2a] + before + ref[r2a:r2b] + after + ref[r2b:]
        before = lambda ins: ref[:r2a] + ins + ref[r2a:]
        for label, text in (
                ("Claude: '</div x>' in a paragraph", wrap("Not: <div hidden> başlar.\n\nKapanış </div x> sahte.\n\n", "</div>\n\n")),
                ("Claude: '</div/>' in a paragraph", wrap("Not: <div hidden> başlar.\n\nKapanış </div/> sahte.\n\n", "</div>\n\n")),
                ("Claude: '</div title=\"x\">' in a paragraph", wrap('Not: <div hidden> başlar.\n\nKapanış </div title="x"> sahte.\n\n', "</div>\n\n")),
                ("Claude: '</script x>' in a paragraph", wrap("Not: " + sc + " başlar.\n\nKapanış </script x> sahte.\n\n", "</script>\n\n")),
                ("a malformed closing token is read on: its backtick pairs with a later one, and the div after is real",
                 wrap('Metin </div x="' + bt + '"> sonra ' + bt + "<div hidden>" + bt + " kod.\n\n", "</div>\n\n")),
                ("NBSP: '</div" + "\\u00a0>' in a paragraph is text", wrap("Not: <div hidden> başlar.\n\nKapanış </div" + nbsp + "> sahte.\n\n", "</div>\n\n")),
                ("NBSP: '</div" + "\\u00a0>' alone on a line starts no HTML block", wrap("Not: <div hidden> başlar.\n\n</div" + nbsp + ">\n\n", "</div>\n\n"))):
            with self.subTest(hidden=label):
                self.assertTrue(self.section_2_lost(text), label)
        with self.subTest(declared="NBSP: '</span" + "\\u00a0>' alone after a blank line starts no type-7 block (the declared element model)"):
            self.assertTrue(self.section_2_lost(wrap("Metin <span hidden> x\n\n</span" + nbsp + ">\n\n", "</span>\n\n")))
        for label, text in (
                ("'</div x>' with nothing open", before("Not: </div x> metin.\n\n")),
                ("'</div x>' on a raw HTML block line closes the div", before("<div hidden>\n</div x>\n\n")),
                ("'</div/>' alone on a line starts an HTML block and closes the div", before("Not: <div hidden> başlar.\n\n</div/>\n\n")),
                ("a valid closing tag with one line end before '>' (the next line is not a block quote)", before("Metin <div hidden> x </div\n    >\n\n")),
                ("a valid closing tag with spaces before '>'", before("Metin <div hidden> x </div  >\n\n"))):
            with self.subTest(passes=label):
                self.assertFalse([p for p in self.ref_with(text) if p.startswith(("HEADING", "SECTION", "ANCHOR", "CORE"))], label)
        agents = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        after = agents.index(sa.CORE_END) + len(sa.CORE_END)
        req = next(x for x in self.manifest["instruction_budget"]["files"] if x["path"] == "AGENTS.md")["required_headings"]
        head = next(l for l in agents[after:].split("\n") if l.startswith("## ") and any(l[3:].startswith(h) for h in req))
        at = agents.index(head, after)
        with self.subTest(hidden="Claude: an AGENTS.md required heading after '</div x>'"):
            f.write_text(agents[:at] + "Not: <div hidden> başlar.\n\nKapanış </div x> sahte.\n\n" + head + "\n\n</div>\n" + agents[at + len(head) + 1:], encoding="utf-8")
            self.assertTrue([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("HEADING")])

    def test_a_closing_tag_name_in_an_html_block_ends_where_html_ends_it(self):
        # İ5B-FIX-19 (C17 audits; Claude Denetim001 C17-F1, Codex root): in an HTML block a closing tag was read with
        # Python's '\\s', so '</div' + NBSP, VT or U+3000 + '>' closed the div. The HTML tokenizer ends a tag name only at
        # TAB, LF, FF or SPACE, '/' or '>'; any other character is part of the name, so that token closes no div. After
        # the name, an attribute or a slash still closes, as a browser reads it. A lone CR is not normalized here (CRLF
        # is, when the file is read): '</div' + CR + '>' is refused — a declared limit, the model refuses more
        ref, anchor, r2a, r2b, ac = self.html_fixture()
        sc, nbsp, vt, ideo, ff, cr = '<script type="text/plain">', chr(0xA0), chr(0x0B), chr(0x3000), chr(0x0C), chr(0x0D)
        wrap = lambda before, after: ref[:r2a] + before + ref[r2a:r2b] + after + ref[r2b:]
        before = lambda ins: ref[:r2a] + ins + ref[r2a:]
        for label, ch in (("NBSP", nbsp), ("VT", vt), ("U+3000", ideo)):
            with self.subTest(hidden=f"Claude: '</div' + {label} + '>' on an HTML block line"):
                self.assertTrue(self.section_2_lost(wrap("<div hidden>\n</div" + ch + ">\n\n", "</div>\n\n")), label)
        with self.subTest(hidden="Claude: '</script' + NBSP + '>' in an HTML block inside an inline script"):
            self.assertTrue(self.section_2_lost(wrap("Metin " + sc + "\n\n<div>\n</script" + nbsp + ">\n</div>\n\n", "</script>\n\n")))
        with self.subTest(declared="a lone CR after the name is not read as whitespace (the model refuses more)"):
            self.assertTrue(self.section_2_lost(wrap("<div hidden>\n</div" + cr + ">\n\n", "</div>\n\n")))
        # the class, not examples: every character Python calls whitespace, except HTML's own (SPACE, TAB, LF, FF), stays
        # part of the name after '</div' on an HTML block line, so the div stays open (CR included: not normalized here)
        others = [c for c in map(chr, range(0x110000)) if c.isspace() and c not in " \t\n\f"]
        self.assertIn(nbsp, others)
        for ch in others:
            with self.subTest(hidden=f"'</div' + U+{ord(ch):04X} + '>' on an HTML block line"):
                self.assertTrue(self.section_2_lost(wrap("<div hidden>\n</div" + ch + ">\n\n", "</div>\n\n")), hex(ord(ch)))
        # the start of an HTML block: a type-7 line is a complete open or closing tag in CommonMark's grammar (§6.6:
        # attribute names start with a letter, '_' or ':'), followed only by spaces or tabs; an opening tag is not pre,
        # script, style or textarea (a closing tag may be). Otherwise the line is paragraph text, read as Markdown
        bt = "`"
        for label, start in (("Codex: '<x !>' is no complete open tag", "<x !>"), ("Codex: '<x>' followed by NBSP", "<x>" + nbsp)):
            with self.subTest(hidden=label + ": no HTML block starts, so '`</div>`' is code"):
                self.assertTrue(self.section_2_lost(wrap("<div hidden>\n\n" + start + "\n" + bt + "</div>" + bt + "\n\n", "</div>\n\n")), label)
        for family, start in (("type 6", "</div" + nbsp + ">"), ("type 7", "</x" + nbsp + ">")):
            with self.subTest(hidden=f"{family}: a closing start line with NBSP starts no HTML block, so the next line's '`</div>`' is code"):
                self.assertTrue(self.section_2_lost(wrap("<div hidden>\n\n" + start + "\n" + bt + "</div>" + bt + "\n\n", "</div>\n\n")), family)
        with self.subTest(hidden="type 7: an attribute after an NBSP is no complete open tag ('<x' + NBSP + 'a>')"):
            self.assertTrue(self.section_2_lost(wrap("<div hidden>\n\n<x" + nbsp + "a>\n" + bt + "</div>" + bt + "\n\n", "</div>\n\n")))
        with self.subTest(hidden="Codex: '<script/>' starts no type-7 block, so '`</script>`' is code inside the inline script"):
            self.assertTrue(self.section_2_lost(wrap("Metin " + sc + "\n\n<script/>\n" + bt + "</script>" + bt + "\n\n", "</script>\n\n")))
        # the start condition's own whitespace (CommonMark §4.6): after a type-1, type-6 or type-7 tag name only a SPACE or
        # a TAB (or '>', '/>', the end of the line) — every other character Python calls whitespace starts no HTML block,
        # so the line is a paragraph and the code span still holds the closing tags
        for ch in [c for c in map(chr, range(0x110000)) if c.isspace() and c not in " \t\n"]:
            for family, start, close in (("type 6", "<p" + ch + ">", "</div>"), ("type 1", "<script" + ch + ">", "</script> </div>"),
                                         ("type 7", "<x" + ch + ">", "</div>")):
                with self.subTest(hidden=f"{family}: '{start[:-2]}' + U+{ord(ch):04X} + '>' starts no HTML block"):
                    self.assertTrue(self.section_2_lost(wrap("<div hidden>\n\n" + start + "\n" + bt + close + bt + "\n\n", "</div>\n\n")),
                                    f"{family} {hex(ord(ch))}")
        for label, text in (
                ("'<p>' starts a type-6 HTML block: '`</div>`' is raw and closes the div", before("<div hidden>\n\n<p></p>\n" + bt + "</div>" + bt + "\n\n")),
                ("'<p ' + TAB + 'class=\"a\">' starts a type-6 HTML block", before('<div hidden>\n\n<p \tclass="a"></p>\n' + bt + "</div>" + bt + "\n\n")),
                ("'<script>' starts a type-1 block: '`</script> </div>`' is raw and closes both", before("<div hidden>\n\n<script>\n" + bt + "</script> </div>" + bt + "\n\n")),
                ("a valid '<x title=\"ok\">' line starts an HTML block: '`</div>`' is raw and closes the div",
                 before('<div hidden>\n\n<x title="ok">\n</x>\n' + bt + "</div>" + bt + "\n\n")),
                ("'<x>' with trailing ASCII spaces starts an HTML block", before("<div hidden>\n\n<x>  \n</x>\n" + bt + "</div>" + bt + "\n\n")),
                ("a closing '</script>' line starts a type-7 block (the exception is for opening tags only)",
                 before("<div hidden>\n\n</script>\n" + bt + "</div>" + bt + "\n\n")),
                ("'</div x>' on an HTML block line closes the div", before("<div hidden>\n</div x>\n\n")),
                ("'</div >' on an HTML block line closes the div", before("<div hidden>\n</div >\n\n")),
                ("'</div' + TAB + 'x>' on an HTML block line closes the div", before("<div hidden>\n</div\tx>\n\n")),
                ("'</div' + FF + '>' on an HTML block line closes the div", before("<div hidden>\n</div" + ff + ">\n\n")),
                ("'</div/>' on an HTML block line closes the div", before("<div hidden>\n</div/>\n\n")),
                ("'</div' + NBSP + '>' in a paragraph with nothing open", before("Not: </div" + nbsp + "> metin.\n\n"))):
            with self.subTest(passes=label):
                self.assertFalse([p for p in self.ref_with(text) if p.startswith(("HEADING", "SECTION", "ANCHOR", "CORE"))], label)
        agents = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        after = agents.index(sa.CORE_END) + len(sa.CORE_END)
        req = next(x for x in self.manifest["instruction_budget"]["files"] if x["path"] == "AGENTS.md")["required_headings"]
        head = next(l for l in agents[after:].split("\n") if l.startswith("## ") and any(l[3:].startswith(h) for h in req))
        at = agents.index(head, after)
        with self.subTest(hidden="an AGENTS.md required heading after '</div' + NBSP + '>' on an HTML block line"):
            f.write_text(agents[:at] + "<div hidden>\n</div" + nbsp + ">\n\n" + head + "\n\n</div>\n" + agents[at + len(head) + 1:], encoding="utf-8")
            self.assertTrue([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("HEADING")])

    def test_an_opening_tag_name_and_the_raw_text_elements_are_read_as_html_reads_them(self):
        # İ5B-FIX-20 (C18 audits; Codex Denetim001 C18-F1, Claude Denetim001 C18-F1). Two roots, one contract:
        # 1. in an HTML block an opening tag's name was cut at Python's '\\s': '<div' + NBSP + ' hidden>' was stacked as
        #    'div', so '</div>' closed it while HTML's element ('div' + NBSP) stayed open and hid the section. HTML ends a
        #    name only at TAB, LF, FF, SPACE, '/' or '>'; a longer name the check cannot match is 'not verified';
        # 2. the raw-text elements were script, style, textarea and title only. HTML's tokenizer also reads xmp, iframe,
        #    noembed and noframes raw (RAWTEXT), so a '</div>' inside one is text and the outer div stays open. noscript
        #    (raw only when scripting is on) and plaintext (never closes) depend on what the check cannot know: their
        #    opener is 'not verified' — reading noscript raw would pass '<noscript><div hidden></noscript>', which hides
        #    the section when scripting is off
        ref, anchor, r2a, r2b, ac = self.html_fixture()
        nbsp, ff, nv = chr(0xA0), chr(0x0C), "not verified"
        wrap = lambda before, after: ref[:r2a] + before + ref[r2a:r2b] + after + ref[r2b:]
        before = lambda ins: ref[:r2a] + ins + ref[r2a:]
        others = [c for c in map(chr, range(0x110000)) if c.isspace() and c not in " \t\n\f"]
        self.assertIn(nbsp, others)
        for ch in others:
            with self.subTest(hidden=f"Codex: '<div' + U+{ord(ch):04X} + ' hidden>' in an HTML block, closed by '</div>'"):
                lost = self.section_2_lost(wrap("<hr>\n<div" + ch + " hidden>\n</div>\n\n", "</div" + ch + ">\n\n"))
                self.assertTrue(lost and all(nv in p for p in lost), (hex(ord(ch)), lost[:1]))
        with self.subTest(hidden="'<br' + NBSP + '>' in an HTML block is no void br: HTML opens an element never closed"):
            self.assertTrue(self.section_2_lost(wrap("<hr>\n<br" + nbsp + ">\n\n", "\n")))
        new_raw = ("xmp", "iframe", "noembed", "noframes")
        for name in ("script", "style", "textarea", "title") + new_raw:
            with self.subTest(hidden=f"Claude: '</div>' inside '<{name}>' in an HTML block is text — the outer div stays open"):
                self.assertTrue(self.section_2_lost(wrap(f"<div hidden>\n<{name}>\n</div>\n</{name}>\n\n", "</div>\n\n")), name)
            with self.subTest(hidden=f"Claude: '</div>' inside '<{name}>' in a paragraph is text — the outer div stays open"):
                self.assertTrue(self.section_2_lost(wrap(f"Not: <div hidden> <{name}> </div> </{name}> başlar.\n\n", "</div>\n\n")), name)
        for label, start in (("noscript holds a '</div>'", "<div hidden>\n<noscript>\n</div>\n</noscript>\n\n"),
                             ("noscript holds an opening '<div hidden>' (scripting off: the div stays open)",
                              "<noscript>\n<div hidden>\n</noscript>\n\n"),
                             ("plaintext never closes", "<div hidden>\n<plaintext>\n</div>\n\n"),
                             ("noscript in a paragraph", "Not: <noscript> <div hidden> </noscript> başlar.\n\n")):
            with self.subTest(not_verified=label):
                lost = self.section_2_lost(wrap(start, "</div>\n\n"))
                self.assertTrue(lost and all(nv in p for p in lost), (label, lost[:1]))
        for label, text in (
                ("'<div hidden>' in an HTML block, closed by '</div>'", before("<hr>\n<div hidden>\n</div>\n\n")),
                ("'<div' + TAB + 'hidden>'", before("<hr>\n<div\thidden>\n</div>\n\n")),
                ("'<div' + FF + 'hidden>'", before("<hr>\n<div" + ff + "hidden>\n</div>\n\n")),
                ("'<div' + LF + 'hidden>'", before("<hr>\n<div\nhidden>\n</div>\n\n")),
                ("'<div/>' then '</div>'", before("<hr>\n<div/>\n</div>\n\n")),
                ("'<br/>' and '<br>' in an HTML block", before("<hr>\n<br/><br>\n\n")),
                ("'<div' + NBSP + ' hidden>' in a paragraph (no CommonMark tag) with its '</div>'",
                 before("Not: <div" + nbsp + " hidden> metin </div> son.\n\n")),
                *((f"'<{n}></div></{n}>' with nothing open", before(f"<{n}>\n</div>\n</{n}>\n\n")) for n in new_raw),
                *((f"a real div holding a closed '<{n}>x</{n}>'", before(f"<div hidden>\n<{n}>x</{n}>\n</div>\n\n")) for n in new_raw),
                ("a closing '</noscript>' alone", before("<hr>\n</noscript>\n\n"))):
            with self.subTest(passes=label):
                self.assertFalse([p for p in self.ref_with(text) if p.startswith(("HEADING", "SECTION", "ANCHOR", "CORE"))], label)
        agents = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        after = agents.index(sa.CORE_END) + len(sa.CORE_END)
        req = next(x for x in self.manifest["instruction_budget"]["files"] if x["path"] == "AGENTS.md")["required_headings"]
        head = next(l for l in agents[after:].split("\n") if l.startswith("## ") and any(l[3:].startswith(h) for h in req))
        at = agents.index(head, after)
        for label, ins in (("Codex: '<div' + NBSP + ' hidden>' in an HTML block", "<hr>\n<div" + nbsp + " hidden>\n</div>\n\n"),
                           ("Claude: '</div>' inside '<xmp>'", "<div hidden>\n<xmp>\n</div>\n</xmp>\n\n")):
            with self.subTest(hidden="an AGENTS.md required heading after " + label):
                f.write_text(agents[:at] + ins + head + "\n\n</div>\n" + agents[at + len(head) + 1:], encoding="utf-8")
                self.assertTrue([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("HEADING")], label)

    def test_a_close_below_the_top_and_foreign_content_are_not_verified(self):
        # İ5B-FIX-21 (C19 audits; Claude Denetim001 C19-F1, Codex Denetim001 C19-F1). Two roots, one contract:
        # 1. a closing tag that matched an element below the top of the stack closed it and dropped every element above
        #    it. HTML may ignore such a close ('</div>' with a table, template, select or object open: no div in scope)
        #    or reopen a formatting element after it, so the hidden div stays in force. It is now 'close': not verified;
        # 2. inside svg or math HTML's raw-text names are foreign elements, and '<div>' breaks out as HTML, so
        #    '<svg><xmp><div hidden></xmp></svg>' leaves an HTML hidden div open. The reader cannot prove an HTML
        #    namespace there: an svg or math opener is 'tag': not verified (a declared limit — a valid SVG is refused too;
        #    inside a raw-text element it is text). A strict top-of-stack close alone does not close the second root
        ref, anchor, r2a, r2b, ac = self.html_fixture()
        nv, bt = "not verified", "`"
        wrap = lambda before, after: ref[:r2a] + before + ref[r2a:r2b] + after + ref[r2b:]
        before = lambda ins: ref[:r2a] + ins + ref[r2a:]
        cases = [(f"Claude: '</div>' with '<{el}>' open in an HTML block", wrap(f"<div hidden>\n<{el}>\n</div>\n\n", f"</{el}>\n</div>\n\n"))
                 for el in ("table", "template", "select", "object")]
        cases += [("Claude: '</div>' with '<table>' open in a paragraph", wrap("Not: <div hidden> <table> </div> başlar.\n\n", "</table>\n</div>\n\n")),
                  ("'</p>' with '<b hidden>' open: HTML reopens the b around the next text", wrap("<p>\n<b hidden>\n</p>\n\n", "</b>\n\n"))]
        cases += [(f"Codex: '<{root}><{raw}><div hidden></{raw}></{root}>' in an HTML block", wrap(f"<hr>\n<{root}><{raw}><div hidden></{raw}></{root}>\n\n", "</div>\n\n"))
                  for root, raw in (("svg", "xmp"), ("svg", "noembed"), ("math", "xmp"))]
        cases += [("'<svg><xmp><div hidden></xmp></svg>' in a paragraph", wrap("Not: <svg><xmp><div hidden></xmp></svg> başlar.\n\n", "</div>\n\n")),
                  ("declared: a closed valid SVG is not verified too", before("<hr>\n<svg><rect/></svg>\n\n"))]
        for label, text in cases:
            with self.subTest(not_verified=label):
                lost = self.section_2_lost(text)
                self.assertTrue(lost and all(nv in p for p in lost), (label, lost[:1]))
        # a void element or a closing tag stacks nothing, so neither rule above hides a wrong block start there: these
        # lines start no HTML block (CommonMark §4.6 — SPACE / TAB only, a complete tag), so '`</div>`' stays code
        ff, nbsp = chr(0x0C), chr(0xA0)
        for label, start in (("type 6: '<hr' + FF + '>'", "<hr" + ff + ">"), ("type 7: '<br>' + NBSP at the end", "<br>" + nbsp),
                             ("type 7: '<br !>' is no complete tag", "<br !>"), ("type 7: '<br' + FF + 'a>'", "<br" + ff + "a>")):
            with self.subTest(hidden=label + ": no HTML block starts, so '`</div>`' is code"):
                self.assertTrue(self.section_2_lost(wrap("<div hidden>\n\n" + start + "\n" + bt + "</div>" + bt + "\n\n", "</div>\n\n")), label)
        for label, text in (
                ("a balanced '<div hidden><table></table></div>'", before("<div hidden>\n<table>\n</table>\n</div>\n\n")),
                ("nested divs closed in order", before("<div hidden>\n<div>\n</div>\n</div>\n\n")),
                ("'<b hidden></b>' closed at the top", before("<p>\n<b hidden></b>\n</p>\n\n")),
                ("an HTML '<xmp>' holding '<div hidden>' (text)", before("<hr>\n<xmp><div hidden></xmp>\n\n")),
                ("'<svg>' inside an HTML '<xmp>' is text", before("<hr>\n<xmp><svg></xmp>\n\n")),
                ("'<svg>' in a code span", before("Not: " + bt + "<svg>" + bt + " metin.\n\n")),
                ("a lone '</svg>'", before("<hr>\n</svg>\n\n")),
                ("a closing tag that closes nothing inside an open div", before("<div hidden>\n</span>\n</div>\n\n"))):
            with self.subTest(passes=label):
                self.assertFalse([p for p in self.ref_with(text) if p.startswith(("HEADING", "SECTION", "ANCHOR", "CORE"))], label)
        agents = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        after = agents.index(sa.CORE_END) + len(sa.CORE_END)
        req = next(x for x in self.manifest["instruction_budget"]["files"] if x["path"] == "AGENTS.md")["required_headings"]
        head = next(l for l in agents[after:].split("\n") if l.startswith("## ") and any(l[3:].startswith(h) for h in req))
        at = agents.index(head, after)
        for label, ins, tail in (("Claude: '</div>' with '<table>' open", "<div hidden>\n<table>\n</div>\n\n", "</table>\n</div>\n"),
                                 ("Codex: '<svg><xmp><div hidden></xmp></svg>'", "<hr>\n<svg><xmp><div hidden></xmp></svg>\n\n", "</div>\n")):
            with self.subTest(hidden="an AGENTS.md required heading after " + label):
                f.write_text(agents[:at] + ins + head + "\n\n" + tail + agents[at + len(head) + 1:], encoding="utf-8")
                self.assertTrue([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("HEADING")], label)

    def test_a_masked_range_html_ends_earlier_is_not_verified(self):
        # İ5B-FIX-22 (C20 audits; Claude Denetim001 C20-F1, Codex root): the reader masked a range that HTML ends earlier,
        # so a real '<table>' behind it was never seen and '</div>' closed the hidden div at the top (HTML ignores it).
        # HTML ends a comment at '--!>' and at once after '<!-->' or '<!--->'; inside a raw-text element '<!--' is text
        # (or a script escape that a '</script>' still ends); '<?' and '<![CDATA[' outside foreign content end at their
        # first '>'. The reader does not follow those ends: each is not verified (a declared limit — also with no hidden
        # element open). A standard comment, '<?x?>', '<![CDATA[x]]>' and a raw-text element without a comment are as before
        ref, anchor, r2a, r2b, ac = self.html_fixture()
        nv, bt = "not verified", "`"
        wrap = lambda before, after: ref[:r2a] + before + ref[r2a:r2b] + after + ref[r2b:]
        before = lambda ins: ref[:r2a] + ins + ref[r2a:]
        cases = [(f"Claude: '{c}' then '<{el}>' in an HTML block", wrap(f"<div hidden>\n{c} <{el}> -->\n</div>\n\n", f"</{el}>\n</div>\n\n"))
                 for c, el in (("<!-- a --!>", "table"), ("<!-->", "table"), ("<!--->", "table"), ("<!-- a --!>", "template"))]
        cases += [("Claude: '<!-- a --!>' then '<table>' in a paragraph", wrap("Not: <div hidden> <!-- a --!> <table> --> </div> başlar.\n\n", "</table>\n</div>\n\n"))]
        cases += [(f"Codex: a comment inside '<{el}>' hides its real close", wrap(f"<div hidden>\n<{el}><!-- </{el}> <table> --></{el}>\n</div>\n\n", "</table>\n</div>\n\n"))
                  for el in ("style", "script")]
        cases += [("Codex: a comment inside '<style>' in a paragraph", wrap("Not: <div hidden> <style><!-- </style> <table> --></style> </div> başlar.\n\n", "</table>\n</div>\n\n"))]
        cases += [(f"Codex: '{o}' ends at its first '>' in HTML", wrap(f"<div hidden>\n{o} x > <table> {e}\n</div>\n\n", "</table>\n</div>\n\n"))
                  for o, e in (("<?", "?>"), ("<![CDATA[", "]]>"))]
        cases += [("'<? x > <table> ?>' in a paragraph", wrap("Not: <div hidden> <? x > <table> ?> </div> başlar.\n\n", "</table>\n</div>\n\n")),
                  ("'<? <!-- > --> <table> ?>': its real first '>' is inside a masked comment", wrap("<div hidden>\n<? <!-- > --> <table> ?>\n</div>\n\n", "</table>\n</div>\n\n")),
                  ("'<!X <!-- > --> <table> >': a comment inside a declaration", wrap("<div hidden>\n<!X <!-- > --> <table> >\n</div>\n\n", "</table>\n</div>\n\n")),
                  ("declared: '--!>' with no element open", before("<!-- a --!> b -->\n\n")),
                  ("declared: '<!-->' with no element open", before("Not: <!--> metin -->.\n\n"))]
        for label, text in cases:
            with self.subTest(not_verified=label):
                lost = self.section_2_lost(text)
                self.assertTrue(lost and all(nv in p for p in lost), (label, lost[:1]))
        for label, text in (
                ("a standard comment holding '<table>', then the real '</div>'", before("<div hidden>\n<!-- a <table> -->\n</div>\n\n")),
                ("a standard comment in a paragraph", before("Not: <div hidden> <!-- a <table> --> </div> son.\n\n")),
                ("'<style>' text without a comment", before("<div hidden>\n<style>a { b: c }</style>\n</div>\n\n")),
                ("'<?x?>' and '<![CDATA[x]]>' with no '>' inside", before("<div hidden>\n<?x?> <![CDATA[x]]>\n</div>\n\n")),
                ("'<!-->' in a code span", before("Not: " + bt + "<!-->" + bt + " ve " + bt + "--!>" + bt + " metin.\n\n")),
                ("'<!-- a --!> b -->' in a fence", before(bt * 3 + "\n<!-- a --!> b -->\n" + bt * 3 + "\n\n"))):
            with self.subTest(passes=label):
                self.assertFalse([p for p in self.ref_with(text) if p.startswith(("HEADING", "SECTION", "ANCHOR", "CORE"))], label)
        agents = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        after = agents.index(sa.CORE_END) + len(sa.CORE_END)
        req = next(x for x in self.manifest["instruction_budget"]["files"] if x["path"] == "AGENTS.md")["required_headings"]
        head = next(l for l in agents[after:].split("\n") if l.startswith("## ") and any(l[3:].startswith(h) for h in req))
        at = agents.index(head, after)
        for label, ins in (("Claude: '<!-- a --!> <table> -->'", "<div hidden>\n<!-- a --!> <table> -->\n</div>\n\n"),
                           ("Codex: '<!--> <table> -->'", "<div hidden>\n<!--> <table> -->\n</div>\n\n"),
                           ("Codex: a comment inside '<style>'", "<div hidden>\n<style><!-- </style> <table> --></style>\n</div>\n\n")):
            with self.subTest(hidden="an AGENTS.md required heading after " + label):
                f.write_text(agents[:at] + ins + head + "\n\n</table>\n</div>\n" + agents[at + len(head) + 1:], encoding="utf-8")
                self.assertTrue([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("HEADING")], label)

    def test_a_comment_inside_a_tag_is_not_verified(self):
        # İ5B-FIX-23 (C21 audits; Claude Denetim001 and Codex Denetim001 C21-F1): the comment reader took a '<!--' inside an
        # attribute value for a comment and masked it, so the masked view showed one long tag and the real '<table>' after
        # the quote was never seen ('</div>' then closed the hidden div at the top; HTML ignores it). HTML reads no comment
        # inside a tag. A comment span starting inside a tag the reader reads is 'tag': not verified — a declared limit: a
        # literal '<!-- -->' wholly inside an attribute is refused too. A comment right after a tag is as before
        ref, anchor, r2a, r2b, ac = self.html_fixture()
        nv, bt = "not verified", "`"
        wrap = lambda before, after: ref[:r2a] + before + ref[r2a:r2b] + after + ref[r2b:]
        before = lambda ins: ref[:r2a] + ins + ref[r2a:]
        tail = "</table>\n</div>\n\n"
        cases = [("Claude: a double-quoted value", wrap('<div hidden title="a<!--b"> <table> -->">\n</div>\n\n', tail)),
                 ("Claude: a single-quoted value", wrap("<div hidden title='a<!--b'> <table> -->'>\n</div>\n\n", tail)),
                 ("Claude: an unquoted value", wrap("<div hidden title=a<!--b> <table> -->>\n</div>\n\n", tail)),
                 ("Claude: '<template>'", wrap('<div hidden title="a<!--b"> <template> -->">\n</div>\n\n', "</template>\n</div>\n\n")),
                 ("Claude: in a paragraph", wrap('Not: <div hidden title="a<!--b"> <table> -->"> </div> başlar.\n\n', tail)),
                 ("Codex: '<span title=\"<!--\">'", wrap('<div hidden>\n<span title="<!--"> <table> -->"></span>\n</div>\n\n', tail)),
                 ("Codex: '<span title='<!--'>'", wrap("<div hidden>\n<span title='<!--'> <table> -->'></span>\n</div>\n\n", tail)),
                 ("a closing tag that ends at its first '>'", wrap("<div hidden>\n</span <!-- > <table> -->>\n</div>\n\n", tail)),
                 ("declared: a literal comment wholly inside an attribute", before('<span title="<!-- x -->">y</span>\n\n'))]
        for label, text in cases:
            with self.subTest(not_verified=label):
                lost = self.section_2_lost(text)
                self.assertTrue(lost and all(nv in p for p in lost), (label, lost[:1]))
        for label, text in (
                ("an ordinary attribute", before('<div hidden title="a b">\n</div>\n\n')),
                ("a comment right after a tag", before("<div hidden><!-- x --></div>\n\n")),
                ("a standard comment between two tags", before("<div hidden>\n<!-- a <table> -->\n</div>\n\n")),
                ("a tag with a comment in a code span", before("Not: " + bt + '<span title="<!--">' + bt + " metin.\n\n")),
                ("a tag with a comment in a fence", before(bt * 3 + '\n<span title="<!--"> -->\n' + bt * 3 + "\n\n"))):
            with self.subTest(passes=label):
                self.assertFalse([p for p in self.ref_with(text) if p.startswith(("HEADING", "SECTION", "ANCHOR", "CORE"))], label)
        agents = (REPO / "AGENTS.md").read_text(encoding="utf-8")
        f = Path(self.tmp.name) / "AGENTS.md"
        self.redirect("AGENTS.md", f)
        after = agents.index(sa.CORE_END) + len(sa.CORE_END)
        req = next(x for x in self.manifest["instruction_budget"]["files"] if x["path"] == "AGENTS.md")["required_headings"]
        head = next(l for l in agents[after:].split("\n") if l.startswith("## ") and any(l[3:].startswith(h) for h in req))
        at = agents.index(head, after)
        for label, ins in (("Claude: a double-quoted value", '<div hidden title="a<!--b"> <table> -->">\n</div>\n\n'),
                           ("Codex: '<span title=\"<!--\">'", '<div hidden>\n<span title="<!--"> <table> -->"></span>\n</div>\n\n')):
            with self.subTest(hidden="an AGENTS.md required heading after " + label):
                f.write_text(agents[:at] + ins + head + "\n\n</table>\n</div>\n" + agents[at + len(head) + 1:], encoding="utf-8")
                self.assertTrue([p for p in sa.check_instruction_budget(self.manifest)[0] if p.startswith("HEADING")], label)

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
