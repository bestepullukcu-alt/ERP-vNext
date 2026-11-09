"""EES 2.13 (shadow mode) - scripts/ci_select_suites.py and its suite table .github/ci/service-suites.tsv.

Run by scripts/run_phase1_gates.sh:

    python3 -m unittest discover -s scripts/tests -p 'test_*.py'

(a) the table names every suite of .github/workflows/service-tests.yml exactly once, and nothing else - a new suite
    cannot be forgotten (sabotage, measured 2026-11-09: deleting the gateway row in a copy of the tree turns
    test_every_workflow_suite_has_exactly_one_row red, naming the suite);
(b) glob matching; (c) fail closed: unknown code path, global trigger, unreachable base, a table out of step;
(d) a docs-only change selects nothing (a markdown change only auth-app: AdminBootstrapGuardTests scans every *.md).
"""

from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest

SCRIPTS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, SCRIPTS)
import ci_select_suites as script  # noqa: E402

ROOT = script.ROOT


def table():
    return script.parse_suites(script.read_text(script.SUITES_TSV))


def no_suite():
    return script.parse_no_suite(script.read_text(script.NO_SUITE_TSV))


def select(*files):
    return script.select(list(files), table(), no_suite())


class SuiteTableMatchesTheWorkflow(unittest.TestCase):
    """(a) both ways, with the job's runner family."""

    def setUp(self):
        self.workflow = script.workflow_suites(script.read_text(script.WORKFLOW))
        self.rows = [suite for suite, _, _ in table()]

    def test_the_workflow_reader_is_not_blind(self):
        self.assertGreaterEqual(len(self.workflow), 30, self.workflow)
        self.assertEqual("macos", self.workflow.get("auth-acceptance-host"))
        self.assertEqual("linux", self.workflow.get("platform-app"))

    def test_every_workflow_suite_has_exactly_one_row(self):
        missing = [s for s in self.workflow if self.rows.count(s) != 1]
        self.assertEqual([], missing, f"suites of {script.WORKFLOW} without exactly one row in {script.SUITES_TSV}")

    def test_every_row_is_a_workflow_suite_on_its_runner(self):
        extra = [s for s in self.rows if s not in self.workflow]
        self.assertEqual([], extra, f"rows in {script.SUITES_TSV} that {script.WORKFLOW} does not run")
        for suite, os_name, _ in table():
            self.assertEqual(self.workflow[suite], os_name, suite)

    def test_a_dropped_row_falls_back_to_full(self):
        """Defence in depth: even if this test file were skipped, the selector itself refuses a table out of step."""
        with tempfile.TemporaryDirectory() as tmp:
            tree = _tree_copy(tmp)
            path = os.path.join(tree, script.SUITES_TSV)
            lines = _read(path).split("\n")
            gone = next(i for i, line in enumerate(lines) if line.startswith("gateway\t"))
            del lines[gone]
            _write(path, "\n".join(lines))
            _commit(tree, "drop row")
            result = script.compute("HEAD~1", "HEAD", tree)
            self.assertEqual("full", result["mode"])
            self.assertIn("out of step", result["reason"])
            self.assertIn("gateway", result["reason"])


class TablesAreWellFormed(unittest.TestCase):
    def test_every_fixed_path_names_something_that_exists(self):
        """A renamed directory or file leaves a dead glob behind - red here, not a silent miss."""
        dead = []
        for suite, _, globs in table():
            for glob in globs:
                prefix = script.fixed_prefix(glob)
                if prefix and not os.path.exists(os.path.join(ROOT, prefix)):
                    dead.append(f"{suite}: {glob}")
        self.assertEqual([], dead)

    def test_every_no_suite_line_matches_a_file(self):
        files = []
        for top in ("services", "gateway"):
            for base, dirs, names in os.walk(os.path.join(ROOT, top)):
                dirs[:] = [d for d in dirs if d not in ("bin", "obj", "node_modules")]
                files += [os.path.relpath(os.path.join(base, n), ROOT).replace(os.sep, "/") for n in names]
        self.assertGreater(len(files), 1000)
        stale = [g for g in no_suite() if not any(script.glob_match(g, f) for f in files)]
        self.assertEqual([], stale)

    def test_malformed_tables_are_refused(self):
        good = "suite\tos\tglobs\na\tlinux\tservices/A/**\n"
        self.assertEqual([("a", "linux", ["services/A/**"])], script.parse_suites(good))
        for bad in ("a\tlinux\tx/**\n",                                  # no header
                    "suite\tos\tglobs\na\twindows\tx/**\n",              # os
                    "suite\tos\tglobs\na\tlinux\tx/**\na\tlinux\ty/**\n",  # twice
                    "suite\tos\tglobs\na\tlinux\t\n",                    # empty glob
                    "suite\tos\tglobs\na\tlinux\t!x/**\n",               # negation is not supported
                    "suite\tos\tglobs\na\tlinux\t../x\n",
                    "suite\tos\tglobs\na\tlinux\tx/**,\n",
                    "suite\tos\tglobs\na\tlinux\tx/[ab]\n",
                    "suite\tos\tglobs\n"):
            with self.assertRaises(script.TableError, msg=repr(bad)):
                script.parse_suites(bad)


class GlobMatching(unittest.TestCase):
    """(b)"""

    def test_cases(self):
        cases = [
            ("services/**", "services/A/b.cs", True),
            ("services/**", "servicesX/b.cs", False),
            ("services/A/**", "services/A/x/y/z.cs", True),
            ("services/A/**", "services/AB/z.cs", False),
            ("**/*.md", "README.md", True),
            ("**/*.md", "docs/a/b/c.md", True),
            ("**/*.md", "docs/a/b/c.mdx", False),
            ("**/appsettings*.json", "appsettings.json", True),
            ("**/appsettings*.json", "gateway/G/appsettings.Development.json", True),
            ("**/appsettings*.json", "gateway/G/appsettings/x.json", False),
            ("docs/**/supplier.openapi.yaml", "docs/supplier.openapi.yaml", True),
            ("docs/**/supplier.openapi.yaml", "docs/a/b/supplier.openapi.yaml", True),
            ("docs/**/supplier.openapi.yaml", "docs/a/xsupplier.openapi.yaml", False),
            ("scripts/test-env/mod0188-ci-mongo.sh", "scripts/test-env/mod0188-ci-mongo.sh", True),
            ("scripts/test-env/mod0188-ci-mongo.sh", "scripts/test-env/mod0188-ci-mongo.sh.bak", False),
            ("**/Dockerfile.*", "a/Dockerfile.api", True),
            ("**/Dockerfile.*", "a/Dockerfile", False),
            ("a?c", "abc", True),
            ("a?c", "a/c", False),
            ("**/*.sln", "services/X/Y.SLN", True),       # case-insensitive
            ("x.y", "xzy", False),                        # a dot is literal
        ]
        for glob, path, expected in cases:
            self.assertEqual(expected, script.glob_match(glob, path), f"{glob} ~ {path}")

    def test_tree_scans(self):
        for glob in ("**/*.md", "services/**", "services/**/*.cs", "gateway/**/*.cs", "**/appsettings*.json"):
            self.assertTrue(script.is_tree_scan(glob), glob)
        for glob in ("services/Diten.Platform/src/**/*.cs", "services/A/**", "docs/**/x.yaml", ".gitignore"):
            self.assertFalse(script.is_tree_scan(glob), glob)


class Selection(unittest.TestCase):
    def test_a_service_change_selects_its_closure_suites(self):
        result = select("services/Diten.PvgService/src/Diten.PvgService.Domain/X.cs")
        self.assertEqual("selective", result["mode"])
        for suite in ("pvg-api", "pvg-case-processing", "pvg-regpv-base", "pvg-meddra-coding",
                      "pvg-signal-management", "platform-app", "auth-app"):
            self.assertIn(suite, result["selected"])
        self.assertNotIn("gateway", result["selected"])
        self.assertEqual(len(table()), len(result["selected"]) + len(result["skipped"]))

    def test_runtime_reads_select_their_suite(self):
        self.assertEqual(["procurement-api"], select("execution/domains/procurement/module-packs/x/pack.yaml")["selected"])
        self.assertIn("procurement-api", select("docs/analysis/other/supplier.openapi.yaml")["selected"])
        self.assertIn("planning-tests", select("scripts/test-env/mod0188-ci-mongo.sh")["selected"])
        self.assertIn("auth-scr", select("scripts/dev/setup-service-trust.sh")["selected"])
        self.assertIn("pvg-api", select("tools/x/appsettings.Production.json")["selected"])
        self.assertIn("crm-app", select("services/Diten.Platform/src/Diten.Platform.API/appsettings.json")["selected"])

    def test_docs_only_change_selects_nothing(self):
        """(d)"""
        result = select("docs/guides/x.png", "docs/records/a/b.tsv", "plans/x.txt")
        self.assertEqual("selective", result["mode"])
        self.assertEqual([], result["selected"])

    def test_a_markdown_change_selects_only_auth_app(self):
        """AdminBootstrapGuardTests (auth-app) reads every *.md of the repository for an old dev password."""
        self.assertEqual(["auth-app"], select("docs/guides/x.md", "plans/y.md")["selected"])

    def test_a_service_path_without_suite_is_known(self):
        result = select("services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Domain/X.cs")
        self.assertEqual("selective", result["mode"])
        self.assertIn("platform-app", result["selected"])  # its services/** scan still applies


class FailClosed(unittest.TestCase):
    """(c)"""

    def test_unknown_code_path_selects_every_suite(self):
        for path in ("services/Diten.NewService/src/X.cs", "gateway/Diten.NewGateway/Program.cs",
                     "services/Diten.InventoryService/tests/Diten.InventoryService.RuntimeProbe/Program.cs"):
            result = select("docs/a.md", path)
            self.assertEqual("full", result["mode"], path)
            self.assertIn("unknown code path", result["reason"])
            self.assertEqual([], result["skipped"])
            self.assertEqual(len(table()), len(result["selected"]))

    def test_global_triggers_select_every_suite(self):
        for path in (".github/workflows/service-tests.yml", ".github/ci/service-suites.tsv",
                     ".github/ci/service-paths-without-suite.tsv", "scripts/ci_select_suites.py",
                     "scripts/check_trx_no_skipped.py", "scripts/ci-skip-allowlist.tsv", "Directory.Build.props",
                     "services/X/Directory.Packages.props", "a/b.targets", "global.json", "NuGet.config",
                     "services/Diten.Platform/Diten.Platform.sln"):
            result = select("docs/x.png", path)
            self.assertEqual("full", result["mode"], path)
            self.assertIn("global trigger", result["reason"])

    def test_unreachable_base_selects_every_suite(self):
        result = script.compute("refs/heads/no-such-branch-ees-213", "HEAD")
        self.assertEqual("full", result["mode"])
        self.assertIn("not reachable", result["reason"])
        self.assertEqual(len(table()), len(result["selected"]))

    def test_unreadable_table_selects_every_workflow_suite(self):
        with tempfile.TemporaryDirectory() as tmp:
            tree = _tree_copy(tmp)
            _write(os.path.join(tree, script.SUITES_TSV), "garbage\n")
            _commit(tree, "break table")
            result = script.compute("HEAD~1", "HEAD", tree)
            self.assertEqual("full", result["mode"])
            self.assertIn("unreadable", result["reason"])
            self.assertGreaterEqual(len(result["selected"]), 30)


class CommandLine(unittest.TestCase):
    def test_writes_json_and_appends_the_summary_and_exits_zero(self):
        with tempfile.TemporaryDirectory() as tmp:
            tree = _tree_copy(tmp)
            os.makedirs(os.path.join(tree, "docs"), exist_ok=True)
            _write(os.path.join(tree, "docs", "note.txt"), "x\n")
            _commit(tree, "docs only")
            out, summary = os.path.join(tmp, "s.json"), os.path.join(tmp, "s.md")
            _write(summary, "before\n")
            run = subprocess.run([sys.executable, os.path.join(tree, "scripts", "ci_select_suites.py"), "--base",
                                  "HEAD~1", "--head", "HEAD", "--json", out, "--summary", summary],
                                 cwd=tree, capture_output=True, text=True)
            self.assertEqual(0, run.returncode, run.stderr)
            result = json.loads(_read(out))
            self.assertEqual({"mode", "reason", "selected", "skipped", "changed_files_count", "evidence"}, set(result))
            self.assertEqual(("selective", [], 1), (result["mode"], result["selected"], result["changed_files_count"]))
            text = _read(summary)
            self.assertTrue(text.startswith("before\n"))
            self.assertIn("| selective | 0 / ", text)

    def test_a_rename_counts_both_paths(self):
        with tempfile.TemporaryDirectory() as tmp:
            tree = _tree_copy(tmp)
            src = os.path.join(tree, "services", "Diten.SafetyStockService", "src", "Diten.SafetyStockService.Calculation")
            os.makedirs(src, exist_ok=True)
            _write(os.path.join(src, "Moved.cs"), "class Moved {}\n")
            _commit(tree, "add")
            dst = os.path.join(tree, "docs")
            os.makedirs(dst, exist_ok=True)
            shutil.move(os.path.join(src, "Moved.cs"), os.path.join(dst, "Moved.cs"))
            _commit(tree, "move out")
            result = script.compute("HEAD~1", "HEAD", tree)
            self.assertIn("safety-stock-calculation", result["selected"])


def _read(path: str) -> str:
    with open(path, encoding="utf-8") as handle:
        return handle.read()


def _write(path: str, text: str) -> None:
    with open(path, "w", encoding="utf-8") as handle:
        handle.write(text)


def _tree_copy(tmp: str) -> str:
    """A throwaway git repository holding the selector, its tables, the workflow and the services/gateway tree shape."""
    tree = os.path.join(tmp, "tree")
    for relative in (script.SUITES_TSV, script.NO_SUITE_TSV, script.WORKFLOW, "scripts/ci_select_suites.py"):
        os.makedirs(os.path.dirname(os.path.join(tree, relative)), exist_ok=True)
        shutil.copy(os.path.join(ROOT, relative), os.path.join(tree, relative))
    subprocess.run(["git", "init", "-q", tree], check=True)
    _commit(tree, "base")
    return tree


def _commit(tree: str, message: str) -> None:
    env = dict(os.environ, GIT_AUTHOR_NAME="t", GIT_AUTHOR_EMAIL="t@example.invalid", GIT_COMMITTER_NAME="t",
               GIT_COMMITTER_EMAIL="t@example.invalid")
    subprocess.run(["git", "-C", tree, "add", "--all", "."], check=True, env=env)
    subprocess.run(["git", "-C", tree, "-c", "commit.gpgsign=false", "commit", "-q", "--allow-empty", "-m", message],
                   check=True, env=env)


if __name__ == "__main__":
    unittest.main()


class ReviewFindings20261109(unittest.TestCase):
    """Dependencies found by the independent review of 2026-11-09 stay mapped."""

    def test_esign_contract_selects_platform_app(self):
        self.assertIn("platform-app", select("docs/analysis/contracts/esign.openapi.yaml")["selected"])

    def test_decision_records_select_pvg_api(self):
        self.assertIn("pvg-api", select("docs/records/decisions/2026-11/x.md")["selected"])

    def test_agents_md_runs_everything(self):
        self.assertEqual(select("AGENTS.md")["mode"], "full")
