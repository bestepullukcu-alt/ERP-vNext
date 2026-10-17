"""CT-FIX-01 (independent audit 2026-10-17, finding 09) — discovery never turns an unreadable module-registry row into
"no match". The registry's tables (header 'Canonical ID', 9 columns) are read row by row; a short row, an empty id or
an empty name makes discovery stop with 'tamamlanamadı' (exit 2) naming the line. --check-id / --candidate keep their
reader. Run by scripts/run_phase1_gates.sh:

    python3 -m unittest discover -s scripts/tests -p 'test_*.py'
"""

from __future__ import annotations

import importlib.util
import os
import subprocess
import tempfile
import unittest

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SPEC = importlib.util.spec_from_file_location("verify_module_id", os.path.join(REPO, ".antigravity", "scripts", "verify_module_id.py"))
vmi = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(vmi)

HEADER = ("| Canonical ID | Canonical Module Name | Slug | Type | Status | Deprecated Alias | Replacement ID | Owner Domain | Notes |\n"
          "|---|---|---|---|---|---|---|---|---|\n")
ROW = "| MOD-0009-FU02 | Tenant Manager Backend | tenant-manager-backend | follow-up | active | - | - | PSS | n |\n"
OTHER = "| MOD-0010 | Other Module | other | module | active | - | - | PSS | n |\n"


def registry(*rows: str) -> list[str]:
    return ("# Module ID Registry\n\n" + HEADER + "".join(rows) + "\nTail text.\n").split("\n")


class RegistryTableProblems(unittest.TestCase):
    def test_the_real_registry_has_no_unreadable_row(self):
        with open(os.path.join(REPO, vmi.REGISTRY), encoding="utf-8") as f:
            self.assertEqual(vmi.registry_table_problems(f.read().split("\n")), [])

    def test_intact_rows_pass(self):
        self.assertEqual(vmi.registry_table_problems(registry(ROW, OTHER)), [])

    def test_a_row_cut_to_five_columns_is_named_by_line(self):
        cut = "| MOD-0009-FU02 | Tenant Manager Backend | tenant-manager-backend | follow-up | active |\n"
        problems = vmi.registry_table_problems(registry(OTHER, cut))
        self.assertEqual(problems, ["satır 6: 5 sütun, başlık 9 sütun"])

    def test_an_empty_id_cell_is_refused(self):
        self.assertEqual(vmi.registry_table_problems(registry(ROW.replace("MOD-0009-FU02", ""))), ["satır 5: kimlik hücresi boş"])

    def test_an_empty_name_cell_is_refused(self):
        self.assertEqual(vmi.registry_table_problems(registry(ROW.replace("Tenant Manager Backend", ""))), ["satır 5: ad hücresi boş"])

    def test_the_contract_header_is_the_real_registry_header(self):
        with open(os.path.join(REPO, vmi.REGISTRY), encoding="utf-8") as f:
            headers = [l for l in f.read().split("\n") if l.startswith("| Canonical ID")]
        self.assertEqual(len(headers), 3)
        self.assertTrue(all([c.strip() for c in h.strip().strip("|").split("|")] == vmi.REGISTRY_HEADER for h in headers))

    def test_a_cut_header_with_its_row_is_refused_even_beside_an_intact_table(self):
        short = "| Canonical ID | Canonical Module Name |\n|---|---|\n| MOD-0009-FU02 | Zircon quarantarium |\n\n"
        lines = ("# Module ID Registry\n\n" + short + HEADER + OTHER).split("\n")
        self.assertEqual(vmi.registry_table_problems(lines), ["satır 3: tablo başlığı sözleşmedeki 9 sütunluk başlık değil"])

    def test_a_module_row_split_from_its_header_by_a_blank_line_is_refused(self):
        lines = ("# Module ID Registry\n\n" + HEADER + OTHER + "\n" + ROW).split("\n")
        self.assertEqual(vmi.registry_table_problems(lines), ["satır 7: tablo başlığı sözleşmedeki 9 sütunluk başlık değil"])

    def test_another_table_and_a_missing_separator_are_refused(self):
        self.assertEqual(vmi.registry_table_problems(("| Term | Meaning |\n|---|---|\n| a | b |\n").split("\n")),
                         ["satır 1: tablo başlığı sözleşmedeki 9 sütunluk başlık değil"])
        no_sep = HEADER.split("\n")[0] + "\n" + ROW
        self.assertEqual(vmi.registry_table_problems(no_sep.split("\n")), ["satır 2: başlıktan sonra ayraç satırı yok"])

    def test_check_id_reader_is_unchanged(self):
        cut = "| MOD-0009-FU02 | Tenant Manager Backend | x | y | z |\n"
        self.assertEqual([r["id"] for r in vmi.registry_from_lines(registry(OTHER, cut))], ["MOD-0010"])


class DiscoverStopsOnABrokenRow(unittest.TestCase):
    """discover() in a throw-away git repository: the registry check runs before the Blueprint is read, so no openpyxl
    and no Blueprint are needed to see it."""

    def run_discover(self, registry_text: str) -> tuple[list[str], int]:
        with tempfile.TemporaryDirectory() as root:
            env = dict(os.environ, GIT_AUTHOR_NAME="t", GIT_AUTHOR_EMAIL="t@invalid", GIT_COMMITTER_NAME="t",
                       GIT_COMMITTER_EMAIL="t@invalid", GIT_OPTIONAL_LOCKS="0")
            subprocess.run(["git", "init", "-q", root], check=True, env=env)
            subprocess.run(["git", "-C", root, "commit", "-q", "--allow-empty", "-m", "x"], check=True, env=env)
            path = os.path.join(root, vmi.REGISTRY)
            os.makedirs(os.path.dirname(path))
            with open(path, "w", encoding="utf-8") as f:
                f.write(registry_text)
            return vmi.discover(root, "Tenant Manager Backend")

    def test_a_cut_row_stops_discovery_with_its_line(self):
        cut = ROW.replace(" | - | - | PSS | n |", " |")
        lines, code = self.run_discover("\n".join(registry(OTHER, cut)))
        self.assertEqual(code, 2)
        self.assertTrue(any("modül kayıt dosyası satır 6: 5 sütun" in l and "tamamlanamadı" in l for l in lines), lines[-3:])

    def test_a_cut_header_beside_an_intact_table_stops_discovery(self):
        short = "| Canonical ID | Canonical Module Name |\n|---|---|\n| MOD-0009-FU02 | Zircon quarantarium |\n\n"
        lines, code = self.run_discover("# Module ID Registry\n\n" + short + HEADER + OTHER)
        self.assertEqual(code, 2)
        self.assertTrue(any("satır 3: tablo başlığı" in l and "tamamlanamadı" in l for l in lines), lines[-3:])

    def test_an_intact_registry_passes_the_check(self):
        lines, code = self.run_discover("\n".join(registry(ROW, OTHER)))
        self.assertTrue(any("Blueprint dosyası yok" in l for l in lines), lines[-3:])   # stopped later, not at the registry
        self.assertFalse(any("modül kayıt dosyası satır" in l for l in lines))


if __name__ == "__main__":
    unittest.main()
