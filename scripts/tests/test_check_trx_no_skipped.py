"""WP-CI-SERVICE-TESTS-03 FIX1 — the skip allow-list of scripts/check_trx_no_skipped.py, on TRX fixtures.

The fixtures have the shape a real xUnit skip produces (measured 2026-10-10): outcome="NotExecuted" and the reason in
Output/ErrorInfo/Message; the reasons are BL-570 FIX4's (UnixConditionalFacts.cs). Run by scripts/run_phase1_gates.sh:

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
import check_trx_no_skipped as script  # noqa: E402

ALLOWLIST = os.path.join(SCRIPTS, "ci-skip-allowlist.tsv")

MACOS_ONLY = "MACOS_ONLY: Linux decides anonymity by /proc/self/fd (tested on its own)."
UID_EQUALS_GID = "UID_EQUALS_GID: euid 1001 = egid — a uid offset moved onto the gid reads the same value here."
RUNS_AS_ROOT = "RUNS_AS_ROOT: root reads a mode-000 file (CAP_DAC_OVERRIDE); the refusal is not observable."


def trx(*results: tuple[str, str, str | None]) -> str:
    rows = []
    for name, outcome, reason in results:
        output = (f"<Output><ErrorInfo><Message>{reason}</Message></ErrorInfo></Output>" if reason is not None else "")
        rows.append(f'<UnitTestResult testName="{name}" outcome="{outcome}">{output}</UnitTestResult>')
    return ('<?xml version="1.0" encoding="utf-8"?>'
            '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>'
            + "".join(rows)
            + f'</Results><ResultSummary outcome="Completed"><Counters total="{len(results)}" notExecuted="0" />'
              "</ResultSummary></TestRun>")


class SkipAllowListTests(unittest.TestCase):
    def run_script(self, content: str, *args: str) -> tuple[int, str]:
        with tempfile.TemporaryDirectory() as directory:
            path = os.path.join(directory, "result.trx")
            with open(path, "w", encoding="utf-8") as handle:
                handle.write(content)
            out = io.StringIO()
            with redirect_stdout(out):
                code = script.main([*args, "--allowlist", ALLOWLIST, path] if args else [path])
            return code, out.getvalue()

    def test_allowed_codes_pass_for_their_suite_and_are_counted(self):
        code, out = self.run_script(trx(("A.Passes", "Passed", None),
                                        ("A.MacOnly", "NotExecuted", MACOS_ONLY),
                                        ("A.UidGid", "NotExecuted", UID_EQUALS_GID),
                                        ("A.Root", "NotExecuted", RUNS_AS_ROOT)), "--suite", "auth-app")
        self.assertEqual(0, code, out)
        self.assertIn("allowed skips for suite auth-app: 3 (MACOS_ONLY x1, RUNS_AS_ROOT x1, UID_EQUALS_GID x1)", out)

    def test_a_skip_without_a_reason_is_red(self):
        code, out = self.run_script(trx(("A.Silent", "NotExecuted", None)), "--suite", "auth-app")
        self.assertEqual(1, code, out)
        self.assertIn("A.Silent", out)
        self.assertIn("no reason", out)

    def test_an_unknown_code_is_red(self):
        code, out = self.run_script(trx(("A.Mystery", "NotExecuted", "SOMETIMES: flaky on Tuesdays")), "--suite", "auth-app")
        self.assertEqual(1, code, out)
        self.assertIn("A.Mystery", out)
        self.assertIn("code SOMETIMES not allowed for suite auth-app", out)

    def test_an_allowed_code_in_another_suite_is_red(self):
        code, out = self.run_script(trx(("M.MacOnly", "NotExecuted", MACOS_ONLY)), "--suite", "mdm-app")
        self.assertEqual(1, code, out)
        self.assertIn("code MACOS_ONLY not allowed for suite mdm-app", out)

    def test_unix_only_on_ubuntu_is_red(self):
        code, out = self.run_script(trx(("A.WindowsOnly", "NotExecuted", "UNIX_ONLY")), "--suite", "auth-app")
        self.assertEqual(1, code, out)
        self.assertIn("UNIX_ONLY", out)

    def test_one_bad_skip_among_allowed_ones_is_red_and_named(self):
        code, out = self.run_script(trx(("A.MacOnly", "NotExecuted", MACOS_ONLY),
                                        ("A.Silent", "NotExecuted", None)), "--suite", "auth-app")
        self.assertEqual(1, code, out)
        self.assertIn("A.Silent", out)
        self.assertNotIn("A.MacOnly  [", out)

    def test_without_a_suite_no_skip_is_allowed(self):
        code, out = self.run_script(trx(("A.MacOnly", "NotExecuted", MACOS_ONLY)))
        self.assertEqual(1, code, out)

    def test_the_code_must_start_the_reason(self):
        code, out = self.run_script(trx(("A.Sneaky", "NotExecuted", "skipped because MACOS_ONLY")), "--suite", "auth-app")
        self.assertEqual(1, code, out)

    def test_a_malformed_allowlist_is_red(self):
        with tempfile.TemporaryDirectory() as directory:
            bad = os.path.join(directory, "allow.tsv")
            with open(bad, "w", encoding="utf-8") as handle:
                handle.write("auth-app MACOS_ONLY 1 spaces-not-tabs\n")
            trx_path = os.path.join(directory, "r.trx")
            with open(trx_path, "w", encoding="utf-8") as handle:
                handle.write(trx(("A.Passes", "Passed", None)))
            out = io.StringIO()
            with redirect_stdout(out):
                code = script.main(["--suite", "auth-app", "--allowlist", bad, trx_path])
            self.assertEqual(1, code, out.getvalue())
            self.assertIn("allow-list line 1", out.getvalue())

    # WP-LANE-MAIN-SYNC-02 B3 (owner decision 2026-10-14, BL-637): one Manufacturing line, its own suite only, 35 = the tests
    # that carry the code (measured). WP-CI-SERVICE-TESTS-05 (CT decision A, BL-646 items 3 and 6): the platform-app BL-534
    # line (2, removed with the WP-AUTH-ADMIN-BOOTSTRAP-01 merge) and the mdm-api CONTRACT_LINE_ABSENT line (1, review
    # 2026-12-31). The pin stays EXACT: anything else added is still red here.
    def test_the_repository_allowlist_holds_exactly_the_auth_manufacturing_bl534_and_contract_line_entries(self):
        self.assertEqual({("auth-app", "MACOS_ONLY"): 1, ("auth-app", "UID_EQUALS_GID"): 1, ("auth-app", "RUNS_AS_ROOT"): 1,
                          ("manufacturing-tests", "MANUFACTURING_TEST_MONGO"): 35,
                          ("platform-app", "BL-534"): 2, ("mdm-api", "CONTRACT_LINE_ABSENT"): 1},
                         script.load_allowlist(ALLOWLIST))

    # WP-CI-SERVICE-TESTS-05 (CT decision A): the code pattern accepts a backlog item code (BL-534) as ONE code — never a
    # prefix of one ("BL", "BL-", "BL-534x", "BL-abc") and never an empty reason.
    def test_a_backlog_item_code_is_read_whole(self):
        self.assertEqual("BL-534", script.CODE.match("BL-534: two measurements of the old seed").group(1))
        self.assertEqual("BL-534", script.CODE.match("BL-534").group(1))
        for reason in ("BL", "BL-", "BL-534x", "BL-abc", "BL-534x: almost", "bl-534: lower case"):
            with self.subTest(reason=reason):
                match = script.CODE.match(reason)
                self.assertNotEqual("BL-534", match.group(1) if match else None)
        self.assertIsNone(script.CODE.match(""))

    def test_only_the_whole_backlog_code_is_an_allowed_skip(self):
        allowed = "BL-534: PlatformAdministratorSeedMeasurementTests, a measurement of the old seed."
        code, out = self.run_script(trx(("P.Seed", "NotExecuted", allowed)), "--suite", "platform-app")
        self.assertEqual(0, code, out)
        self.assertIn("allowed skips for suite platform-app: 1 (BL-534 x1)", out)
        for reason in ("BL", "BL-", "BL-534x", "BL-abc", "BL-535: another item", ""):
            with self.subTest(reason=reason):
                code, out = self.run_script(trx(("P.Seed", "NotExecuted", reason)), "--suite", "platform-app")
                self.assertEqual(1, code, out)
                self.assertIn("P.Seed", out)

    # FIX2: a code has a maximum — a second test borrowing RUNS_AS_ROOT is red, and named.
    def test_more_skips_with_a_code_than_its_maximum_is_red(self):
        code, out = self.run_script(trx(("A.Root", "NotExecuted", RUNS_AS_ROOT),
                                        ("A.Borrower", "NotExecuted", RUNS_AS_ROOT)), "--suite", "auth-app")
        self.assertEqual(1, code, out)
        self.assertIn("2 skips with code RUNS_AS_ROOT in suite auth-app, at most 1 allowed", out)

    # FIX2 (mutation Y7): only a NotExecuted result can be an allowed skip — another outcome with an allowed reason is red.
    def test_an_allowed_reason_on_another_outcome_is_red(self):
        for outcome in ("Inconclusive", "Timeout", "Aborted", ""):
            with self.subTest(outcome=outcome):
                code, out = self.run_script(trx(("A.Odd", outcome, MACOS_ONLY)), "--suite", "auth-app")
                self.assertEqual(1, code, out)
                self.assertIn("A.Odd", out)

    def test_a_repeated_or_maxless_allowlist_line_is_red(self):
        for content in ("auth-app\tMACOS_ONLY\t1\tr\nauth-app\tMACOS_ONLY\t1\tr\n", "auth-app\tMACOS_ONLY\tr\n",
                        "auth-app\tMACOS_ONLY\t0\tr\n"):
            with self.subTest(content=content), tempfile.TemporaryDirectory() as directory:
                bad = os.path.join(directory, "allow.tsv")
                with open(bad, "w", encoding="utf-8") as handle:
                    handle.write(content)
                with self.assertRaises(script.AllowlistError):
                    script.load_allowlist(bad)


if __name__ == "__main__":
    unittest.main()
