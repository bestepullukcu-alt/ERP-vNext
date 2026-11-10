"""EES pack-readiness-01 - scripts/pack_readiness.py, the pre-review readiness check of a Module Pack.

Run by scripts/run_phase1_gates.sh:

    python3 -m unittest discover -s scripts/tests -p 'test_*.py'

Each rule R1..R6 has one failing synthetic pack; a clean pack raises nothing. The default run reads R1, R2, R3, R5;
R4 and R6 only with --all. --checklist prints the readiness-reader prompt with its six items. The sabotage proof lives here: for every
rule, SabotageProof switches that rule's function off (mock.patch on the module attribute `check` looks up at call time)
and shows that the rule's own failing-case assertion then fails - so no rule test is green by accident.
The repository the rules read (files for R3, AGENTS.md §3 for R5) is a real throw-away git repository, read through
the production Repo class at ref HEAD.
"""

from __future__ import annotations

import io
import os
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from unittest import mock

SCRIPTS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, SCRIPTS)
import pack_readiness as pr  # noqa: E402

AGENTS_MD = """# AGENTS

## 3. Port Şeması

| Servis | Port | Proje Yolu |
|--------|------|------------|
| Gateway (Ocelot) | 5000 | `gateway/Diten.ApiGateway` |
| MDM Service | 5059 | `services/Diten.MdmService/src/Diten.MdmService.Api` |
| Inventory Service | 5067 | `services/Diten.InventoryService/src/Diten.InventoryService.Api` |

## 4. Protected Paths
"""

SOURCE = "\n".join([
    "namespace Diten.MdmService;",
    "",
    "public sealed class LegalEntityGuard",
    "{",
    "    public bool CanArchive(LegalEntity entity) => entity.Status == Status.Draft;",
    "}",
]) + "\n"

PACK_PATH = "execution/domains/master-data-management/module-packs/MOD-9999-readiness-fixture.md"

CLEAN = """---
id: MOD-9999
status: ready-for-dev
---

# MOD-9999 — readiness fixture

## Module Summary

The service is MDM Service (5059); the browser goes through the gateway only.

## Revision notes

> Revizyon 2: K-1 was açık karar here before; kept as history.

## Implementation Notes

- Decision register: K-1 — KARAR D7: archive only from Draft.
- Guard source: `services/Diten.MdmService/src/Diten.MdmService.Domain/LegalEntityGuard.cs:5` `CanArchive`.

## Acceptance Criteria

- [ ] AC-1 (zorunlu) — archiving a Draft legal entity returns 204.
- [ ] AC-2 (zorunlu) — archiving an Active legal entity returns 409.

## Test Expectations

- Integration: AC-1 and AC-2 over HTTP.
- Sabotaj: removing the Draft check in `CanArchive` turns AC-2 red; AC-1 negative case: unknown id returns 404.

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | archive guard | Module Summary, Acceptance Criteria, Test Expectations |
"""


def variant(old: str, new: str) -> str:
    assert old in CLEAN, old
    return CLEAN.replace(old, new, 1)


# one failing pack per rule
FAILING = {
    "R1": variant("| DL-1 | archive guard | Module Summary, Acceptance Criteria, Test Expectations |",
                  "| DL-1 | archive guard | Module Summary, Screens |\n| DL-2 | | Test Expectations |"),
    "R2": variant("## Acceptance Criteria", "Open item: K-1 açık kalır until the owner answers.\n\n## Acceptance Criteria"),
    "R3": variant("LegalEntityGuard.cs:5` `CanArchive`", "LegalEntityGuard.cs:5` `CanDelete`.\n- Old seam: "
                  "`services/Diten.MdmService/src/Diten.MdmService.Domain/Removed.cs:12`, "
                  "`services/Diten.MdmService/src/Diten.MdmService.Domain/LegalEntityGuard.cs:40-44`.\n"
                  "- Source of the rule: `docs/records/audits/no-such-note.md`"),
    "R4": variant("- [ ] AC-2 (zorunlu) — archiving an Active legal entity returns 409.",
                  "- [ ] AC-2 (zorunlu) — archiving an Active legal entity returns 409.\n"
                  "- [ ] AC-3 (zorunlu) — an archived legal entity is hidden from the list."),
    "R5": variant("MDM Service (5059)", "MDM Service (5058)"),
    "R6": variant("- [ ] AC-1 (zorunlu)", "- Owner of the export format: TBD.\n- [ ] AC-1 (zorunlu)"),
}
RULE_FUNC = dict(pr.RULES)
OLD_SEAM = "services/Diten.MdmService/src/Diten.MdmService.Domain/OldSeam.cs"

SLICE_ROW = "| DL-1 | archive guard | Module Summary, Acceptance Criteria, Test Expectations |"
SLICE_HEAD = "## Teslim dilimleri\n\n| dilim | ad | kapsadığı bölümler |\n|---|---|---|\n"
# finding 1: tables the tool's own reading accepted but pack_approval.slice_ids refuses
GATE_ONLY = {
    "### heading": variant("## Teslim dilimleri\n", "### Teslim dilimleri\n"),
    "## 8.1 heading": variant("## Teslim dilimleri\n", "## 8.1 Teslim dilimleri\n"),
    "no separator row": variant("|---|---|---|\n" + SLICE_ROW, SLICE_ROW),
    "prose before table": variant(SLICE_HEAD, "## Teslim dilimleri\n\nOne slice.\n\n| dilim | ad | kapsadığı bölümler |\n"
                                              "|---|---|---|\n"),
    "HTML comment": variant(SLICE_ROW, SLICE_ROW + "\n\n<!-- DL-2 later -->"),
    "escaped pipe": variant("| DL-1 | archive guard |", "| DL-1 | archive \\| guard |"),
    "unclosed fence": CLEAN + "\n```text\nan example that never closes\n",
}
# finding 4: each history mechanism alone
BLOCKQUOTE_ONLY = variant("- Decision register:", "> K-1 açık karar in the first draft.\n\n- Decision register:")
HEADING_ONLY = variant("## Implementation Notes", "## Revizyon geçmişi\n\n- K-1 açık karar in revision 1.\n\n"
                                                  "## Implementation Notes")
# finding 2: a pin named elsewhere in the paragraph
PIN_IN_PARAGRAPH = "- Source read at `R@{old}` (branch, read only).\n- Read: `" + OLD_SEAM + ":3` `LegalEntityGuard`."
PIN_ABSENT = ("- Source `R@0123456789abcdef0123456789abcdef01234567` (not in this clone).\n"
              "- Read: `services/Diten.MdmService/src/Diten.MdmService.Domain/Gone.cs:3`.")
DCP_PATH = "execution/portfolio/delivery-capability-packs/DCP-999-readiness-fixture.md"
DCP = """---
id: DCP-999
status: draft
---

# DCP-999

## 8.1 Delivery slices

| Slice | Kapsam | Not |
|---|---|---|
| S-1 | MOD-9999 |  |
"""


class Fixture(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = tempfile.mkdtemp(prefix="pack-readiness-")
        cls.addClassCleanup(shutil.rmtree, cls.root, ignore_errors=True)
        cls.put("AGENTS.md", AGENTS_MD)
        cls.put("services/Diten.MdmService/src/Diten.MdmService.Domain/LegalEntityGuard.cs", SOURCE)
        cls.put(PACK_PATH, CLEAN)
        cls.put(OLD_SEAM, SOURCE)                                # exists only in the first commit
        cls.git("init", "-q")
        cls.git("add", "AGENTS.md", "services", "execution")
        cls.git("commit", "-q", "-m", "fixture: first")
        cls.old = cls.git("rev-parse", "HEAD").strip()
        cls.git("rm", "-q", OLD_SEAM)
        cls.git("commit", "-q", "-m", "fixture: seam removed")
        cls.repo = pr.Repo(cls.root, "HEAD")

    @classmethod
    def git(cls, *args: str) -> str:
        env = dict(os.environ, GIT_AUTHOR_NAME="fixture", GIT_AUTHOR_EMAIL="fixture@example.invalid",
                   GIT_COMMITTER_NAME="fixture", GIT_COMMITTER_EMAIL="fixture@example.invalid")
        return subprocess.run(["git", "-c", "commit.gpgsign=false", "-c", "core.hooksPath=/dev/null", "-C", cls.root,
                               *args], check=True, capture_output=True, text=True, env=env).stdout

    @classmethod
    def put(cls, rel: str, text: str):
        path = os.path.join(cls.root, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8") as f:
            f.write(text)

    def items(self, text: str, all_rules: bool = True):
        return pr.check(PACK_PATH, text, self.repo, all_rules=all_rules)

    def assert_fires(self, code: str, text: str, all_rules: bool = True):
        got = [i for i in self.items(text, all_rules) if i.code == code]
        self.assertTrue(got, f"{code} raised nothing")
        return got


class CleanPack(Fixture):
    def test_clean_pack_raises_nothing(self):
        self.assertEqual([i.render() for i in self.items(CLEAN)], [])               # --all
        self.assertEqual([i.render() for i in self.items(CLEAN, all_rules=False)], [])

    def test_history_is_not_read_as_an_open_decision(self):
        # the clean pack has "K-1 was açık karar" inside a revision block quote and "K-1 — KARAR D7" in the body
        self.assertNotIn("R2", {i.code for i in self.items(CLEAN)})

    def test_exit_codes(self):
        out = io.StringIO()
        with redirect_stdout(out):
            self.assertEqual(pr.main([os.path.join(self.root, PACK_PATH), "--base", "HEAD"]), 0)
        self.addCleanup(self.put, PACK_PATH, CLEAN)
        self.put(PACK_PATH, FAILING["R1"])                       # R1 is ENGEL: exit 1
        with redirect_stdout(out):
            self.assertEqual(pr.main([os.path.join(self.root, PACK_PATH), "--base", "HEAD"]), 1)
        self.assertIn("HAZIRLIK R1 satır", out.getvalue())
        self.put(PACK_PATH, FAILING["R6"])                       # R6 is ÖNEMLİ and only under --all: exit 0
        out = io.StringIO()
        with redirect_stdout(out):
            self.assertEqual(pr.main([os.path.join(self.root, PACK_PATH), "--base", "HEAD"]), 0)
            self.assertNotIn("HAZIRLIK R6", out.getvalue())
            self.assertEqual(pr.main([os.path.join(self.root, PACK_PATH), "--base", "HEAD", "--all"]), 0)
        self.assertIn("HAZIRLIK R6 satır", out.getvalue())
        with redirect_stdout(out), redirect_stderr(io.StringIO()):
            self.assertEqual(pr.main([os.path.join(self.root, "missing.md"), "--base", "HEAD"]), 2)
            self.assertEqual(pr.main([os.path.join(self.root, PACK_PATH), "--base", "no-such-ref"]), 2)


class Rules(Fixture):
    def test_r1_slice_table(self):
        got = self.assert_fires("R1", FAILING["R1"])
        text = " ".join(i.message for i in got)
        self.assertIn("`Screens`", text)                       # a covered section that is not a heading
        self.assertIn("DL-2: `ad` hücresi boş", text)           # an empty cell
        self.assertTrue(all(i.severity == pr.ENGEL for i in got))
        missing = variant("## Teslim dilimleri\n", "## Slices (old)\n")
        self.assertIn("tablosu yok", self.assert_fires("R1", missing)[0].message)

    def test_r2_decision_open_and_decided(self):
        got = self.assert_fires("R2", FAILING["R2"])
        self.assertEqual(got[0].severity, pr.ONEMLI)
        self.assertIn("`K-1`", got[0].message)
        decided_line = next(n for n, l in enumerate(FAILING["R2"].split("\n"), 1) if "KARAR D7" in l)
        self.assertIn(f"satır {decided_line}", got[0].message)  # both lines are reported

    def test_r3_references(self):
        got = self.assert_fires("R3", FAILING["R3"])
        msgs = {i.severity: [] for i in got}
        for i in got:
            msgs[i.severity].append(i.message)
        self.assertTrue(any("Removed.cs" in m for m in msgs.get(pr.ENGEL, [])))       # missing file
        self.assertTrue(any("6 satır" in m for m in msgs.get(pr.ONEMLI, [])))         # 40-44 beyond 6 lines
        self.assertTrue(any("CanDelete" in m for m in msgs.get(pr.ONEMLI, [])))       # quote not within ±3 lines
        self.assertTrue(any("no-such-note.md" in m for m in msgs.get(pr.ONEMLI, [])))  # cited document missing

    def test_r4_acceptance_tied_to_tests_and_sabotage(self):
        got = self.assert_fires("R4", FAILING["R4"])
        self.assertEqual({tuple(i.subjects[:1]) for i in got}, {("AC-3",)})
        self.assertEqual(len(got), 2)                            # not in tests, and no sabotage
        self.assertTrue(all(i.severity == pr.ONEMLI for i in got))

    def test_r5_port_matches_agents_table(self):
        got = self.assert_fires("R5", FAILING["R5"])
        self.assertIn("5058", got[0].message)
        self.assertIn("5059", got[0].message)

    def test_r6_unresolved_marker_in_ready_pack(self):
        got = self.assert_fires("R6", FAILING["R6"])
        self.assertEqual(got[0].severity, pr.ONEMLI)
        draft = FAILING["R6"].replace("status: ready-for-dev", "status: draft")
        self.assertNotIn("R6", {i.code for i in self.items(draft)})   # a draft may still carry TBD


class RuleSets(Fixture):
    def test_default_set_is_r1_r2_r3_r5(self):
        self.assertEqual(pr.DEFAULT_RULES, ("R1", "R2", "R3", "R5"))
        for code in ("R1", "R2", "R3", "R5"):
            with self.subTest(rule=code):
                self.assert_fires(code, FAILING[code], all_rules=False)
        for code in ("R4", "R6"):
            with self.subTest(rule=code):
                self.assertNotIn(code, {i.code for i in self.items(FAILING[code], all_rules=False)})
                self.assert_fires(code, FAILING[code], all_rules=True)

    def test_only_r1_and_missing_files_are_engel(self):
        sev = {(i.code, i.severity) for code in FAILING for i in self.items(FAILING[code])}
        self.assertEqual({c for c, s_ in sev if s_ == pr.ENGEL}, {"R1", "R3"})

    def test_checklist_needs_no_pack_and_has_the_six_items(self):
        out = io.StringIO()
        with redirect_stdout(out):
            self.assertEqual(pr.main(["--checklist"]), 0)
        text = out.getvalue()
        self.assertEqual(len(pr.CHECKLIST_ITEMS), 6)
        for n, item in enumerate(pr.CHECKLIST_ITEMS, 1):
            self.assertIn(f"({n}) {item}", text)
        for rule in ("satır numarasını", "üslup ya da tasarım görüşü", "bilinen", "ENGEL, ÖNEMLİ ya da KÜÇÜK",
                     "numaralı liste", "inceleme DEĞİLDİR", "ayrı bir oturum ve ayrı bir model",
                     "inceleme kayıtlarını"):
            self.assertIn(rule, text)
        with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
            pr.main([])                                          # neither a pack nor --checklist


class ReviewFindings(Fixture):
    """Blind review KOŞULLU findings 1-4, each with its own sabotage."""

    def with_pin(self, template):
        return variant("## Acceptance Criteria", template.format(old=self.old) + "\n\n## Acceptance Criteria")

    def test_f1_gate_reader_refusal_is_engel(self):
        for name, text in GATE_ONLY.items():
            with self.subTest(case=name):
                got = [i for i in self.assert_fires("R1", text) if "slice_ids" in i.message]
                self.assertTrue(got and got[0].severity == pr.ENGEL, name)

    def test_f1_sabotage_gate_reader_off(self):
        stub = mock.Mock(slice_ids=lambda path, text: ["DL-1"])
        with mock.patch.object(pr, "gate_module", lambda: stub):
            for name, text in GATE_ONLY.items():
                if name == "no separator row":
                    continue                                     # R1's own reading also refuses it (no row left)
                with self.subTest(case=name), self.assertRaises(AssertionError):
                    self.assert_fires("R1", text)

    def test_f2_pin_named_in_the_paragraph(self):
        self.assertNotIn("R3", {i.code for i in self.items(self.with_pin(PIN_IN_PARAGRAPH))})
        got = self.assert_fires("R3", self.with_pin(PIN_ABSENT))
        self.assertEqual([i.severity for i in got], [pr.ONEMLI])  # named commit absent: downgraded, not ENGEL
        self.assertIn("bu klonda yok", got[0].message)

    def test_f2_sabotage_paragraph_pin_off(self):
        with mock.patch.object(pr, "_blocks", lambda pack: {}):
            got = self.assert_fires("R3", self.with_pin(PIN_IN_PARAGRAPH))
            self.assertEqual(got[0].severity, pr.ENGEL)
            self.assertEqual(self.assert_fires("R3", self.with_pin(PIN_ABSENT))[0].severity, pr.ENGEL)

    def test_f3_empty_cells_read_like_the_gate(self):
        gate = pr.gate_module()
        self.assertEqual(pr.NOT_GIVEN.pattern, gate.NOT_GIVEN.pattern)
        for cell in ("–", "?"):
            with self.subTest(cell=cell):
                text = variant("| DL-1 | archive guard |", f"| DL-1 | {cell} |")
                self.assertNotIn("R1", {i.code for i in self.items(text)})
        self.assertEqual(gate.slice_ids(DCP_PATH, DCP), ["S-1"])
        self.assertEqual(pr.check(DCP_PATH, DCP, self.repo), [])  # the DCP `Not` column may be empty

    def test_f3_sabotage_dcp_note_column_checked(self):
        with mock.patch.object(pr, "DCP_FREE_COLUMNS", 3), self.assertRaises(AssertionError):
            self.assertEqual([i for i in pr.check(DCP_PATH, DCP, self.repo) if i.code == "R1"], [])

    def test_f4_history_block_quote_only(self):
        self.assertNotIn("R2", {i.code for i in self.items(BLOCKQUOTE_ONLY)})
        with mock.patch.object(pr, "BLOCKQUOTE_IS_HISTORY", False):
            self.assert_fires("R2", BLOCKQUOTE_ONLY)

    def test_f4_history_heading_only(self):
        self.assertNotIn("R2", {i.code for i in self.items(HEADING_ONLY)})
        with mock.patch.object(pr, "HISTORY_HEADING", re.compile(r"(?!)")):
            self.assert_fires("R2", HEADING_ONLY)


class GitEnvironment(Fixture):
    def test_f5_fixture_commits_skip_hooks_and_signing(self):
        hook = os.path.join(self.root, ".git", "hooks", "pre-commit")
        os.makedirs(os.path.dirname(hook), exist_ok=True)
        with open(hook, "w") as f:
            f.write("#!/bin/sh\nexit 1\n")
        os.chmod(hook, 0o755)
        self.addCleanup(os.remove, hook)
        self.git("commit", "-q", "--allow-empty", "-m", "fixture: hook must not run")   # check=True: a hook would fail
        self.git("reset", "-q", "--soft", "HEAD~1")

    def test_f6_git_never_lazy_fetches(self):
        seen = []
        real = subprocess.run

        def spy(cmd, *a, **k):
            seen.append(k.get("env") or {})
            return real(cmd, *a, **k)

        with mock.patch.object(pr.subprocess, "run", spy):
            pr.Repo(self.root, "HEAD").lines("AGENTS.md")
            pr.repo_root(self.root)
        self.assertTrue(seen)
        self.assertTrue(all(e.get("GIT_NO_LAZY_FETCH") == "1" for e in seen))


class SabotageProof(Fixture):
    """Switch each rule off; its own failing-case assertion must then fail."""

    def test_each_rule_is_load_bearing(self):
        for code, fn in pr.RULES:
            with self.subTest(rule=code):
                self.assert_fires(code, FAILING[code])            # baseline: the rule catches its case
                with mock.patch.object(pr, fn, lambda pack, repo: []):
                    with self.assertRaises(AssertionError):
                        self.assert_fires(code, FAILING[code])

    def test_every_rule_has_a_failing_case(self):
        self.assertEqual(set(FAILING), {code for code, _ in pr.RULES})


if __name__ == "__main__":
    unittest.main()
