"""Blueprint 8.7 Master-first rule (CEO 2026-10-16) in the identity gates of .antigravity/scripts/verify_module_id.py:
--candidate refuses a CAND-CAP outside the frozen set, one without a registry row and a deprecated alias (naming its
canonical target); --check-id --repo-only passes only the frozen pre-rule reservation. Each refusal has a positive
control beside it. Run by scripts/run_phase1_gates.sh:

    python3 -m unittest discover -s scripts/tests -p 'test_*.py'
"""

from __future__ import annotations

import contextlib
import importlib.util
import io
import os
import tempfile
import unittest

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SPEC = importlib.util.spec_from_file_location("verify_module_id", os.path.join(REPO, ".antigravity", "scripts", "verify_module_id.py"))
vmi = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(vmi)

HEADER = ("| Canonical ID | Canonical Module Name | Slug | Type | Status | Deprecated Alias | Replacement ID | Owner Domain | Notes |\n"
          "|---|---|---|---|---|---|---|---|---|\n")
LEDGER = "execution/portfolio/blueprint-master-plan-reconciliation.md"


class Fixture:
    """A throw-away root with a registry, a reconciliation ledger and (for --check-id) a Blueprint workbook."""

    def __init__(self, rows: str, ledger_ids: tuple[str, ...] = (), blueprint: tuple[tuple[str, str], ...] = ()):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = self.tmp.name
        self.put(vmi.REGISTRY, "# Module ID Registry\n\n" + HEADER + rows)
        self.put(LEDGER, "# Ledger\n\n" + "\n".join(f"- {i}" for i in ledger_ids) + "\n")
        if blueprint:
            import openpyxl
            wb = openpyxl.Workbook()
            ws = wb.active
            ws.title = "Blueprint_Data"
            ws.append(["Module ID", "Module Name"])
            for row in blueprint:
                ws.append(list(row))
            path = os.path.join(self.root, vmi.BLUEPRINT)
            os.makedirs(os.path.dirname(path), exist_ok=True)
            wb.save(path)

    def put(self, rel: str, text: str) -> None:
        path = os.path.join(self.root, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8") as f:
            f.write(text)

    def run(self, fn, *args) -> tuple[int, str]:
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = fn(self.root, *args)
        return code, out.getvalue()

    def close(self) -> None:
        self.tmp.cleanup()


CANDIDATE_ROW = "| {id} | {name} | slug | Candidate Follow-up | candidate / pending-EA |  | MOD-0352 | dom | n |\n"
ALIAS_ROW = "| CAND-CAP-0001 | Tenant User / Identity Foundation | slug | Deprecated Alias | deprecated | CAND-CAP-0001 | MOD-0360 | dom | n |\n"


class CandidateGate(unittest.TestCase):
    def test_positive_control_a_frozen_active_candidate_with_row_and_ledger_passes(self):
        fx = Fixture(CANDIDATE_ROW.format(id="CAND-CAP-0007-FU01", name="Strategy Hardening"), ledger_ids=("CAND-CAP-0007-FU01",))
        try:
            code, out = fx.run(vmi.check_candidate, "CAND-CAP-0007-FU01", "Strategy Hardening")
            self.assertEqual(code, 0, out)
        finally:
            fx.close()

    def test_a_new_candidate_with_a_well_formed_row_and_ledger_is_refused(self):
        fx = Fixture(CANDIDATE_ROW.format(id="CAND-CAP-0012", name="New Thing"), ledger_ids=("CAND-CAP-0012",))
        try:
            code, out = fx.run(vmi.check_candidate, "CAND-CAP-0012", "New Thing")
            self.assertEqual(code, 2, out)
            self.assertIn("not one of the CAND-CAP identities that existed before the Master-first rule", out)
            self.assertIn("Module Change Request", out)
        finally:
            fx.close()

    def test_an_unregistered_candidate_is_refused_with_the_master_first_reason(self):
        fx = Fixture("")
        try:
            code, out = fx.run(vmi.check_candidate, "CAND-CAP-0099", "X")
            self.assertEqual(code, 2, out)
            self.assertIn("has no registry row", out)
            self.assertIn("Module Change Request", out)
        finally:
            fx.close()

    def test_a_deprecated_alias_is_refused_and_names_its_canonical_target(self):
        fx = Fixture(ALIAS_ROW, ledger_ids=("CAND-CAP-0001",))
        try:
            code, out = fx.run(vmi.check_candidate, "CAND-CAP-0001", "Tenant User / Identity Foundation")
            self.assertEqual(code, 2, out)
            self.assertIn("CAND-CAP-0001 is a deprecated alias of MOD-0360", out)
        finally:
            fx.close()

    def test_the_frozen_set_is_exactly_the_candidates_main_held(self):
        self.assertEqual(len(vmi.FROZEN_CANDIDATES), 19)
        self.assertTrue(all(c.startswith("CAND-CAP-") for c in vmi.FROZEN_CANDIDATES))
        self.assertNotIn("CAND-CAP-0012", vmi.FROZEN_CANDIDATES)


class RepoOnlyGate(unittest.TestCase):
    ROW = "| {id} | Reserved Thing | slug | Module | reserved |  |  | dom | EA reservation 2026-09-01 |\n"
    BP = (("MOD-0001", "Something Else"),)

    def test_positive_control_the_frozen_reservation_passes(self):
        fx = Fixture(self.ROW.format(id="NEW-MOD-0041"), blueprint=self.BP)
        try:
            code, out = fx.run(vmi.check_id, "NEW-MOD-0041", "Reserved Thing", None, True)
            self.assertEqual(code, 0, out)
        finally:
            fx.close()

    def test_a_new_repo_only_reservation_is_refused_even_with_reservation_evidence(self):
        fx = Fixture(self.ROW.format(id="NEW-MOD-0099"), blueprint=self.BP)
        try:
            code, out = fx.run(vmi.check_id, "NEW-MOD-0099", "Reserved Thing", None, True)
            self.assertEqual(code, 2, out)
            self.assertIn("not a reservation that existed before the Master-first rule", out)
        finally:
            fx.close()

    def test_a_blueprint_module_needs_no_repo_only(self):
        fx = Fixture("| MOD-0001 | Something Else | slug | Module | planned |  |  | dom | n |\n", blueprint=self.BP)
        try:
            code, out = fx.run(vmi.check_id, "MOD-0001", "Something Else", None, False)
            self.assertEqual(code, 0, out)
        finally:
            fx.close()


if __name__ == "__main__":
    unittest.main()
