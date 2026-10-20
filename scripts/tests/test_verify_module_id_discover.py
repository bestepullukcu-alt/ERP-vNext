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


class DiscoverySummary(unittest.TestCase):
    """CT-FIX-06 (v7 Employee pilot, Codex): the 'ÖZET' block under the report's header — bounded in UTF-8 bytes, the
    colliding packs first, a deprecated alias said apart, künye values with blob and ref ids, information only."""
    HCM = "execution/domains/hcm/module-packs/MOD-0251-core-hr-employee-master.md"
    PSS = "execution/domains/pss/module-packs/MOD-0251-hris-external-sor.md"
    OK = ("approved", "-", "HCM / HR data owner TBD")

    def summary(self, matched, meta=None, alias=(), findings=(), info=(), failure=None):
        if meta is None:
            meta = {p: [(self.OK, [("a" * 40, {"refs/heads/x"})])] for p, _ in matched}
        return vmi.report_summary(list(info), list(findings), matched, meta, set(alias), failure)

    def test_a_collision_is_named_with_its_packs_and_parent_and_fu_are_not_one(self):
        out = self.summary([(self.HCM, 500), (self.PSS, 1)], findings=("BULGU paket: x", "BULGU blueprint: y"))
        self.assertTrue(out[0].startswith("ÖZET bulgular: blueprint 1 · sor 0 · registry 0 · paket 1"), out[0])
        c = next(i for i, l in enumerate(out) if l.startswith("ÖZET olası kimlik çakışması — inceleme gerekli (tek başına kesin mükerrer değil): MOD-0251"))
        self.assertIn(self.PSS, out[c])
        self.assertTrue(out[c + 1].startswith(f"ÖZET paket künyesi: {self.HCM}") and "owner HCM / HR data owner TBD" in out[c + 1], out[c + 1])
        fu = self.summary([("execution/domains/a/module-packs/MOD-0288-ledger.md", 1),
                           ("execution/domains/a/module-packs/MOD-0288-FU06B-ledger-detail.md", 1)])
        self.assertFalse([l for l in fu if "çakışma —" in l or "kimlik çokluğu" in l], fu)
        for line in out + fu:      # never a finding, a decision or a single-line field of the report contract
            self.assertTrue(line.startswith("ÖZET "), line)
            self.assertFalse(line.startswith(("BULGU ", "sorgu: ", "origin/main: ", "araç: ", "sonuç: ", "çıkış: ", "İNCELENEN REF'LER: ")))

    def test_a_deprecated_alias_is_said_apart_and_does_not_clear_its_paths(self):
        # Codex (summary-boundary-interim-01): MOD-0298 in alias_ids with two paths still said 'olası kimlik çakışması'
        a = "execution/domains/pss/module-packs/MOD-0298-tenant-module-entitlements.md"
        b = "execution/domains/hcm/module-packs/MOD-0298-employee-profile-workspace.md"
        out = self.summary([(a, 18), (b, 1)], alias=("MOD-0298",))
        self.assertIn("ÖZET BİLGİ: MOD-0298 registry'de eski takma ad — takma adın kendisi çakışma sayılmaz", out)
        self.assertTrue(any(l.startswith("ÖZET kimlik çokluğu — takma ad ilişkisi belirsiz, inceleme gerekli: MOD-0298") for l in out), out)
        self.assertFalse([l for l in out if l.startswith("ÖZET olası kimlik çakışması")], out)

    def kunye(self, out):     # CT-FIX-07: found by its prefix, not by position (the open decisions may come first)
        return next(l for l in out if l.startswith("ÖZET paket künyesi: "))

    def test_kunye_values_keep_their_blob_and_ref_ids(self):
        # Codex: grouping by value dropped the blob ids; the same values in several blobs is not one current version
        same = self.summary([(self.HCM, 3)], meta={self.HCM: [(self.OK, [("b" * 40, {"refs/heads/a", "refs/heads/b"}),
                                                                            ("c" * 40, {"refs/heads/c"})])]})
        self.assertIn("blob bbbbbbbbb: refs/heads/a, refs/heads/b; blob ccccccccc: refs/heads/c", self.kunye(same))
        differ = self.summary([(self.HCM, 2)], meta={self.HCM: [(self.OK, [("b" * 40, {"refs/heads/a"})]),
                                                                (("draft", "-", "Other"), [("c" * 40, {"refs/heads/c"})])]})
        self.assertIn("belirsiz — 2 farklı künye", self.kunye(differ))
        self.assertIn("blob bbbbbbbbb: refs/heads/a", self.kunye(differ))
        self.assertIn("status draft · approval_status - · owner Other (blob ccccccccc: refs/heads/c)", self.kunye(differ))
        legacy = self.summary([(self.HCM, 1)], meta={self.HCM: [(None, [("d" * 40, {"refs/heads/a"})])]})
        self.assertIn("künye okunamadı (bilgi; kapı değil)", self.kunye(legacy))
        unread = vmi.report_summary([], [], [(self.HCM, 1)], None, set(), None)
        self.assertIn("ÖZET paket künyeleri: okunamadı — özet eksik (bulgular tam rapordadır)", unread)

    def test_the_block_stays_within_its_byte_budget_and_says_what_it_left_out(self):
        # Codex (summary-boundary-interim-01): 100 paths under one id made one 15985-byte line and an 18001-byte block
        many = [(f"execution/domains/d{i:03d}/module-packs/MOD-0251-{'ç' * 60}-{i}.md", 400 + i) for i in range(100)]
        others = [(f"execution/domains/x/module-packs/CAND-CAP-{i:04d}-{'ğ' * 80}.md", 1) for i in range(40)]
        meta = {p: [(("approved", "-", "ö" * 300), [(f"{i:040x}", {f"refs/heads/{'ü' * 80}-{i}-{j}" for j in range(30)})
                                                   for i in range(5)])] for p, _ in many + others}
        out = vmi.report_summary([], [], others + many, meta, set(), None)   # the other packs come first in the report
        block = "".join(l + "\n" for l in out).encode("utf-8")
        self.assertLessEqual(len(block), vmi.SUMMARY_BUDGET)
        head = ("KEŞİF RAPORU v2\n" + "".join(l + "\n" for l in out)).encode("utf-8")[:4096].decode("utf-8", errors="ignore")
        self.assertIn("ÖZET olası kimlik çakışması — inceleme gerekli", head)        # the collision first, whatever the order
        self.assertIn("ÖZET EKSİK:", head)
        self.assertIn("ÖZET sınır:", head)
        self.assertTrue(all(len(l.encode("utf-8")) <= vmi.SUMMARY_LINE for l in out))
        line = next(l for l in out if l.startswith("ÖZET olası kimlik çakışması"))   # CT-FIX-08: found by prefix
        self.assertRegex(line, r" · \+\d+ yol$")                      # the count of the paths not shown survives the cut

    def test_the_open_decisions_follow_the_collisions(self):
        # CT-FIX-07 (v7 C5 pilot): the collision and the owner TBD were visible but not used; they are named as decisions
        meta = {self.HCM: [(self.OK, [("a" * 40, {"refs/heads/x"})])],
                self.PSS: [(("ready-for-dev", "-", "platform-team"), [("b" * 40, {"refs/remotes/origin/hr-future"})])],
                "execution/domains/x/module-packs/CAND-CAP-0001-legacy.md": [(("done", "-", "-"), [("c" * 40, {"r"})])]}
        out = vmi.report_summary([], [], [(self.HCM, 2), (self.PSS, 1), ("execution/domains/x/module-packs/CAND-CAP-0001-legacy.md", 1)],
                                 meta, set(), None)
        line = next(l for l in out if l.startswith("ÖZET açık kararlar (bilgi; onay değil): "))
        self.assertIn("kimlik MOD-0251 — olası çakışma (2 paket yolu)", line)
        self.assertIn("sahibi belirsiz: MOD-0251-core-hr-employee-master.md (owner 'HCM / HR data owner TBD')", line)
        self.assertIn("sahibi belirsiz: CAND-CAP-0001-legacy.md (owner '-')", line)     # an absent owner, not a stop
        self.assertNotIn("hris-external-sor", line)
        self.assertEqual(out.index(line), 1, out[:3])     # CT-FIX-08: right after the counts, before the collision groups
        alias = vmi.report_summary([], [], [(self.HCM, 2), (self.PSS, 1)], meta, {"MOD-0251"}, None)
        self.assertTrue(any("kimlik MOD-0251 — takma ad ilişkisi belirsiz (2 paket yolu)" in l for l in alias), alias)
        none = vmi.report_summary([], [], [(self.PSS, 1)], meta, set(), None)
        self.assertFalse([l for l in none if l.startswith("ÖZET açık kararlar")], none)

    def test_blueprint_candidates_and_domain_configs_are_listed_and_bounded(self):
        bp = [("MOD-0251", "Core HR / Employee Master"), ("MOD-0298", "Employee Profile Workspace")]
        dcs = [("execution/domains/hcm/domain-config.md", True), ("execution/domains/pss/domain-config.md", False)]
        out = vmi.report_summary([], [], [(self.HCM, 1)], None, set(), None, bp, dcs)
        self.assertIn("ÖZET blueprint adayları: MOD-0251 'Core HR / Employee Master' · MOD-0298 'Employee Profile Workspace'", out)
        self.assertIn("ÖZET domain-config (eşleşen paketlerin klasörü; varlık yalnız HEAD ağacında ölçüldü, paketin kendi ref'indeki durum belirsiz; bu keşif komutu içeriğini incelemedi): "
                      "execution/domains/hcm/domain-config.md (HEAD'de var) · execution/domains/pss/domain-config.md (HEAD'de yok)", out)
        many = vmi.report_summary([], [], [], {}, set(), None, [(f"MOD-{i:04d}", "Ç" * 40) for i in range(300)], [])
        line = next(l for l in many if l.startswith("ÖZET blueprint adayları: "))
        self.assertLessEqual(len(line.encode("utf-8")), vmi.SUMMARY_LINE)
        self.assertRegex(line, r" · \+\d+ aday$")
        self.assertLessEqual(sum(len(l.encode("utf-8")) + 1 for l in many), vmi.SUMMARY_BUDGET)

    def test_domain_config_presence_is_read_from_head_s_tree(self):
        with tempfile.TemporaryDirectory() as root:
            env = dict(os.environ, GIT_AUTHOR_NAME="t", GIT_AUTHOR_EMAIL="t@invalid", GIT_COMMITTER_NAME="t",
                       GIT_COMMITTER_EMAIL="t@invalid", GIT_OPTIONAL_LOCKS="0")
            for d in ("a", "b"):
                os.makedirs(os.path.join(root, "execution", "domains", d))
                with open(os.path.join(root, "execution", "domains", d, "domain-config.md"), "w") as f:
                    f.write("x\n")
            subprocess.run(["git", "init", "-q", root], check=True, env=env)
            subprocess.run(["git", "-C", root, "add", "--", "execution/domains/a/domain-config.md"], check=True, env=env)
            subprocess.run(["git", "-C", root, "commit", "-q", "-m", "a"], check=True, env=env)
            got = vmi.domain_config_at_head(root, ["execution/domains/a/module-packs/MOD-0001-x.md",
                                                   "execution/domains/b/module-packs/MOD-0002-y.md",
                                                   "execution/domains/a/module-packs/MOD-0003-z.md", "docs/other.md"])
            self.assertEqual(got, [("execution/domains/a/domain-config.md", True),
                                   ("execution/domains/b/domain-config.md", False)])   # b is only in the working tree
        # Codex: a failed measurement is not an absence — outside a repository, git cannot answer: None, 'HEAD'de ölçülemedi'
        with tempfile.TemporaryDirectory() as not_a_repo:
            got = vmi.domain_config_at_head(not_a_repo, ["execution/domains/a/module-packs/MOD-0001-x.md"])
            self.assertEqual(got, [("execution/domains/a/domain-config.md", None)])
        out = vmi.report_summary([], [], [], {}, set(), None, (), [("execution/domains/a/domain-config.md", None),
                                                                    ("execution/domains/b/domain-config.md", False)])
        line = next(l for l in out if l.startswith("ÖZET domain-config "))
        self.assertIn("execution/domains/a/domain-config.md (HEAD'de ölçülemedi)", line)
        self.assertIn("execution/domains/b/domain-config.md (HEAD'de yok)", line)

    def test_the_open_decisions_survive_a_wide_query(self):
        # CT-FIX-08 (Claude Denetim001 C7-F): a wide real query's collision groups filled the budget and dropped the decisions
        many = [(f"execution/domains/d{i:02d}/module-packs/MOD-{i // 2:04d}-{'x' * 90}-{i}.md", 3) for i in range(60)]
        out = vmi.report_summary([], [], many, None, set(), None, [("MOD-0001", "A")], [])
        self.assertTrue(out[1].startswith("ÖZET açık kararlar (bilgi; onay değil): kimlik MOD-0000 — olası çakışma"), out[:2])
        self.assertTrue(any(l.startswith("ÖZET EKSİK: ") for l in out), out[-3:])

    def test_an_incomplete_discovery_says_so(self):
        out = vmi.report_summary([], [], [], {}, set(), "Blueprint dosyası yok")
        self.assertIn("ÖZET EKSİK: keşif tamamlanamadı — özet yalnız okunabilen kaynaklardandır", out)


if __name__ == "__main__":
    unittest.main()


class PersonalDataMasking(unittest.TestCase):
    """CT-START-04 (6): a committed discovery report never carries a personal e-mail address."""

    def test_an_owner_e_mail_is_masked_and_the_rest_kept(self):
        self.assertEqual(vmi.mask_personal("owner NY (ny@example.org) · status ready-for-dev"),
                         "owner NY (<e-posta gizlendi>) · status ready-for-dev")

    def test_discover_masks_every_line_before_the_hash(self):
        real = vmi._discover
        try:
            vmi._discover = lambda root, name, resource=None, sor=None: (
                ["VERIFY-MODULE-ID DISCOVER v2", "ÖZET paket künyesi: x.md — owner A (a.b@example.co.uk)", "çıkış: 3"], 3)
            lines, code = vmi.discover(".", "x")
        finally:
            vmi._discover = real
        self.assertEqual(code, 3)
        self.assertFalse(any("@" in l for l in lines), lines)
        self.assertIn("<e-posta gizlendi>", lines[1])

    def test_text_without_an_address_is_unchanged(self):
        line = "BULGU gateway: pv-case-intake-triage → 5011 (MOD-0230)"
        self.assertEqual(vmi.mask_personal(line), line)

