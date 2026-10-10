"""WP-CI-SERVICE-TESTS-03 FIX1 — scripts/check_cross_db_enforcement.sh, its one exact-path exception.

Each case copies the PRODUCTION script, byte for byte, into a temporary tree next to the files it scans (the script scans
`services` under its own parent directory) and runs it. The last case runs it on the real repository.

    python3 -m unittest discover -s scripts/tests -p 'test_*.py'
"""

from __future__ import annotations

import os
import shutil
import subprocess
import tempfile
import unittest

SCRIPTS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPO = os.path.dirname(SCRIPTS)
GATE = os.path.join(SCRIPTS, "check_cross_db_enforcement.sh")
EXCEPTION = ("services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/"
             "VerifiedMarketOperationalPersistenceServiceCollectionExtensions.cs")
CLIENT = "        var mongoClient = new MongoClient(mongoSettings);\n"


class CrossDbGateTests(unittest.TestCase):
    def run_gate(self, files: dict[str, str]) -> tuple[int, str]:
        with tempfile.TemporaryDirectory() as root:
            os.makedirs(os.path.join(root, "scripts"))
            shutil.copyfile(GATE, os.path.join(root, "scripts", "check_cross_db_enforcement.sh"))
            for relative, content in files.items():
                path = os.path.join(root, relative)
                os.makedirs(os.path.dirname(path), exist_ok=True)
                with open(path, "w", encoding="utf-8") as handle:
                    handle.write(content)
            result = subprocess.run(["bash", os.path.join(root, "scripts", "check_cross_db_enforcement.sh")],
                                    capture_output=True, text=True, timeout=120)
            return result.returncode, result.stdout + result.stderr

    def test_the_named_exception_with_one_client_passes(self):
        code, out = self.run_gate({EXCEPTION: "class A {\n" + CLIENT + "}\n"})
        self.assertEqual(0, code, out)

    def test_a_second_client_in_the_named_exception_is_red(self):
        code, out = self.run_gate({EXCEPTION: "class A {\n" + CLIENT + CLIENT + "}\n"})
        self.assertEqual(1, code, out)
        self.assertIn("VerifiedMarketOperationalPersistenceServiceCollectionExtensions.cs", out)

    def test_a_new_file_outside_the_allowed_paths_is_red(self):
        code, out = self.run_gate({"services/Diten.Platform/src/Diten.Platform.Application/Shadow.cs": CLIENT})
        self.assertEqual(1, code, out)
        self.assertIn("Shadow.cs", out)

    def test_a_file_next_to_the_exception_is_not_excused(self):
        sibling = EXCEPTION.replace("VerifiedMarketOperational", "OtherOperational")
        code, out = self.run_gate({sibling: CLIENT})
        self.assertEqual(1, code, out)

    def test_a_test_file_is_allowed(self):
        code, out = self.run_gate({"services/Diten.Platform/tests/Some.Tests/Fixture.cs": CLIENT})
        self.assertEqual(0, code, out)

    # FIX2 (mutation Y6): a Windows checkout reports the path with backslashes; the gate normalises them before it
    # compares, so the named exception still matches (and only it).
    def test_a_backslashed_path_of_the_exception_is_normalised(self):
        backslashed = EXCEPTION.replace("services/", "services/", 1).split("/", 1)
        code, out = self.run_gate({"services/" + backslashed[1].replace("/", "\\"): "class A {\n" + CLIENT + "}\n"})
        self.assertEqual(0, code, out)

    def test_the_repository_passes(self):
        result = subprocess.run(["bash", GATE], capture_output=True, text=True, timeout=300, cwd=REPO)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
