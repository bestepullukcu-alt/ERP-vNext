"""EES pack-readiness-01 - scripts/pack_readiness.py, the pre-review readiness check of a Module Pack.

Run by scripts/run_phase1_gates.sh:

    python3 -m unittest discover -s scripts/tests -p 'test_*.py'

Each rule R1..R6 has one failing synthetic pack; a clean pack raises nothing. The sabotage proof lives here: for every
rule, SabotageProof switches that rule's function off (mock.patch on the module attribute `check` looks up at call time)
and shows that the rule's own failing-case assertion then fails - so no rule test is green by accident.
The repository the rules read (files for R3, AGENTS.md §3 for R5) is a real throw-away git repository, read through
the production Repo class at ref HEAD.
"""

from __future__ import annotations

import io
import os
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


class Fixture(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = tempfile.mkdtemp(prefix="pack-readiness-")
        cls.addClassCleanup(shutil.rmtree, cls.root, ignore_errors=True)
        cls.put("AGENTS.md", AGENTS_MD)
        cls.put("services/Diten.MdmService/src/Diten.MdmService.Domain/LegalEntityGuard.cs", SOURCE)
        cls.put(PACK_PATH, CLEAN)
        env = dict(os.environ, GIT_AUTHOR_NAME="fixture", GIT_AUTHOR_EMAIL="fixture@example.invalid",
                   GIT_COMMITTER_NAME="fixture", GIT_COMMITTER_EMAIL="fixture@example.invalid")
        for args in (["init", "-q"], ["add", "AGENTS.md", "services", "execution"], ["commit", "-q", "-m", "fixture"]):
            subprocess.run(["git", "-C", cls.root, *args], check=True, capture_output=True, env=env)
        cls.repo = pr.Repo(cls.root, "HEAD")

    @classmethod
    def put(cls, rel: str, text: str):
        path = os.path.join(cls.root, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8") as f:
            f.write(text)

    def items(self, text: str):
        return pr.check(PACK_PATH, text, self.repo)

    def assert_fires(self, code: str, text: str):
        got = [i for i in self.items(text) if i.code == code]
        self.assertTrue(got, f"{code} raised nothing")
        return got


class CleanPack(Fixture):
    def test_clean_pack_raises_nothing(self):
        self.assertEqual([i.render() for i in self.items(CLEAN)], [])

    def test_history_is_not_read_as_an_open_decision(self):
        # the clean pack has "K-1 was açık karar" inside a revision block quote and "K-1 — KARAR D7" in the body
        self.assertNotIn("R2", {i.code for i in self.items(CLEAN)})

    def test_exit_codes(self):
        out = io.StringIO()
        with redirect_stdout(out):
            self.assertEqual(pr.main([os.path.join(self.root, PACK_PATH), "--base", "HEAD"]), 0)
        self.put(PACK_PATH, FAILING["R6"])
        self.addCleanup(self.put, PACK_PATH, CLEAN)
        with redirect_stdout(out):
            self.assertEqual(pr.main([os.path.join(self.root, PACK_PATH), "--base", "HEAD"]), 1)
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
        self.assertEqual(got[0].severity, pr.ENGEL)
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
        self.assertEqual(got[0].severity, pr.ENGEL)
        draft = FAILING["R6"].replace("status: ready-for-dev", "status: draft")
        self.assertNotIn("R6", {i.code for i in self.items(draft)})   # a draft may still carry TBD


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
