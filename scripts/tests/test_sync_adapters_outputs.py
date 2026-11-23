"""EES-BAGLAMA-01, Codex round 2 (structural fix) — every scenario Codex measured in rounds 1 and 2 is replayed against the
REAL generator: sync_adapters.main() in write mode, on a temporary copy of the instruction folders. A refused run must
exit 2 and leave every file of the copy byte-identical (nothing written before the check). Run by
scripts/run_phase1_gates.sh:

    python3 -m unittest discover -s scripts/tests -p 'test_*.py'

The instruction budget (link check over the whole repository) is switched off in the copy only; it has its own tests.
"""

from __future__ import annotations

import contextlib
import hashlib
import importlib.util
import io
import json
import os
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("sync_adapters", REPO / ".antigravity" / "scripts" / "sync_adapters.py")
sa = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(sa)

COPIED = [".ees", ".antigravity/agents", ".antigravity/skills", ".antigravity/rules", ".claude/agents", ".claude/skills",
          ".claude/rules", ".codex/agents", ".agents"]
OLD_OUTPUTS = [".claude/rules/agents-core.md", ".claude/rules/localization-standard.md", ".claude/agents/l10n-agent.md",
               ".claude/agents/module-pack-author.md", ".claude/agents/security-agent.md",
               ".claude/skills/control-tower/SKILL.md", ".claude/skills/resx-check/SKILL.md",
               ".codex/agents/l10n-agent.toml", ".codex/agents/module-pack-author.toml",
               ".codex/agents/security-agent.toml", ".agents/skills/control-tower/SKILL.md",
               ".agents/skills/resx-check/SKILL.md", ".agents/rules/localization-standard.md"]


def can_symlink(where: Path) -> bool:
    try:
        os.symlink(where / "nowhere", where / ".probe-link")
        os.unlink(where / ".probe-link")
        return True
    except (OSError, NotImplementedError):
        return False


class RealGenerator(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name) / "repo"
        self.root.mkdir()
        for name in ("AGENTS.md", "CLAUDE.md"):
            shutil.copy2(REPO / name, self.root / name)
        for rel in COPIED:
            shutil.copytree(REPO / rel, self.root / rel, symlinks=True)
        self.saved = (sa.ROOT, sa.check_instruction_budget)
        sa.ROOT = self.root
        sa.check_instruction_budget = lambda manifest: ([], [])

    def tearDown(self):
        sa.ROOT, sa.check_instruction_budget = self.saved
        sa.load_manifest()
        self.tmp.cleanup()

    # --- helpers -------------------------------------------------------------------------------------------------
    def snapshot(self) -> dict:
        seen = {}
        for p in sorted(self.root.rglob("*")):
            rel = p.relative_to(self.root).as_posix()
            if p.is_symlink():
                seen[rel] = "link:" + os.readlink(p)
            elif p.is_file():
                seen[rel] = hashlib.sha256(p.read_bytes()).hexdigest()
        return seen

    def run_main(self, *args: str) -> tuple[int, str]:
        argv, sys.argv = sys.argv, ["sync_adapters.py", *args]
        try:
            with contextlib.redirect_stdout(io.StringIO()) as buf:
                code = sa.main()
        finally:
            sys.argv = argv
        return code, buf.getvalue()

    def manifest(self, change) -> None:
        p = self.root / ".ees" / "adapters.json"
        data = json.loads(p.read_text(encoding="utf-8"))
        change(data)
        p.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    def source(self, rel: str, old: str, new: str) -> None:
        p = self.root / rel
        text = p.read_text(encoding="utf-8")
        self.assertIn(old, text)
        p.write_text(text.replace(old, new, 1), encoding="utf-8")

    def item(self, data: dict, name: str) -> dict:
        return next(i for i in data["items"] if i["id"] == name)

    def refused(self, needle: str) -> None:
        before = self.snapshot()
        code, out = self.run_main()
        self.assertEqual(code, 2, out)
        self.assertIn(needle, out)
        self.assertEqual(self.snapshot(), before, "a file changed although the run was refused")

    # --- control -------------------------------------------------------------------------------------------------
    def test_the_copy_is_in_sync_and_a_write_changes_nothing(self):
        code, out = self.run_main("--check")
        self.assertEqual(code, 0, out)
        before = self.snapshot()
        code, out = self.run_main()
        self.assertEqual(code, 0, out)
        self.assertEqual(self.snapshot(), before)

    def test_the_old_outputs_are_generated_byte_identical(self):
        manifest = sa.load_manifest()
        planned = {t["path"]: sa.render(i, t) for i in manifest["items"] for t in i["targets"]}
        for path in OLD_OUTPUTS:
            self.assertEqual(planned[path], (REPO / path).read_bytes().decode("utf-8").replace("\r\n", "\n"), path)

    # --- Codex round 2, ENGEL 1: symbolic links in the target path -----------------------------------------------
    def test_a_target_that_is_a_symbolic_link_is_refused_before_any_write(self):
        if not can_symlink(self.root):
            self.skipTest("symbolic links are not available here")
        target = self.root / ".claude" / "agents" / "ees-isci.md"
        target.unlink()
        os.symlink(Path("..") / "rules" / "agents-core.md", target)
        self.refused("symbolic link")

    def test_a_parent_folder_that_is_a_symbolic_link_is_refused_before_any_write(self):
        if not can_symlink(self.root):
            self.skipTest("symbolic links are not available here")
        agents = self.root / ".claude" / "agents"
        shutil.rmtree(agents)
        os.symlink(Path("rules"), agents)
        self.refused("symbolic link")

    # --- round 1, finding 1: forbidden targets and non-canonical paths -------------------------------------------
    def test_a_dot_slash_ees_source_is_refused(self):
        def change(d):
            d["items"].append({"id": "ees-probe", "kind": "rule", "source": "./.ees/kartlar/cekirdek.md",
                               "targets": [{"tool": "claude-code", "format": "markdown",
                                            "path": ".claude/rules/ees-probe.md"}]})
        self.manifest(change)
        self.refused("not a canonical repository path")

    def test_a_forbidden_target_is_refused(self):
        self.manifest(lambda d: self.item(d, "ees-isci")["targets"].append(
            {"tool": "claude-code", "format": "markdown", "path": ".claude/rules/ees-probe.md",
             "frontmatter": {"name": "ees-probe", "description": "d"}}))
        self.refused("pilot boundary")

    def test_a_codex_skill_target_is_refused(self):
        self.manifest(lambda d: self.item(d, "ees-hat")["targets"].append(
            {"tool": "codex+antigravity", "format": "markdown", "path": ".agents/skills/ees-probe/SKILL.md",
             "frontmatter": {"name": "ees-probe", "description": "d"}}))
        self.refused("pilot boundary")

    # --- round 2, ENGEL 2 and round 1, finding 6: the name that is really written --------------------------------
    def test_a_name_masked_by_an_unused_fields_object_is_refused(self):
        def change(d):
            t = self.item(d, "ees-l10n-agent")["targets"][0]
            t["frontmatter"]["name"] = "l10n-agent"
            t["fields"] = {"name": "ees-mask"}
        self.manifest(change)
        self.refused("does not use 'fields'")

    def test_the_generated_name_is_checked_not_the_manifest(self):
        # the manifest check is bypassed on purpose: only the output check can see the real name
        def change(d):
            t = self.item(d, "ees-l10n-agent")["targets"][1]
            t["fields"]["name"] = "l10n-agent"
        self.manifest(change)
        real = sa.validate_manifest
        sa.validate_manifest = lambda data: None
        try:
            self.refused("lacks the 'ees-' prefix")
        finally:
            sa.validate_manifest = real

    def test_a_generated_name_used_twice_is_refused(self):
        def change(d):
            for t in self.item(d, "ees-belge-ajani")["targets"]:
                (t.get("frontmatter") or t.get("fields"))["name"] = "ees-isci"
        self.manifest(change)
        real = sa.validate_manifest
        sa.validate_manifest = lambda data: None
        try:
            self.refused("is also used by")
        finally:
            sa.validate_manifest = real

    # --- round 2, finding 3 and round 1, finding 4: the frontmatter keys -----------------------------------------
    def test_a_flow_style_frontmatter_is_refused(self):
        self.source(".ees/ajanlar/isci.md", "seviye: standart\nkart: isci", '{"seviye": ust, "kart": isci}')
        self.refused("in the frontmatter — write it once")

    def test_a_flow_style_mapping_with_other_keys_is_refused(self):
        self.source(".ees/ajanlar/isci.md", "seviye: standart\nkart: isci", "{other: x, seviye: ust, kart: isci}")
        self.refused("in the frontmatter — write it once")

    def test_a_commented_field_is_refused(self):
        self.source(".ees/ajanlar/isci.md", "seviye: standart", "seviye: standart # not")
        self.refused("unsupported 'seviye'")

    # --- round 2, finding 4 and round 1, finding 5: model fields -------------------------------------------------
    def test_a_skill_target_with_its_own_model_is_refused(self):
        self.manifest(lambda d: self.item(d, "ees-hat")["targets"][0]["frontmatter"].update(model="haiku"))
        self.refused("writes model itself")

    def test_an_unmapped_tool_target_with_its_own_model_is_refused(self):
        self.manifest(lambda d: self.item(d, "ees-isci")["targets"][1]["fields"].update(model="test-model"))
        self.refused("writes model itself")

    def test_a_generated_model_that_differs_from_the_seviye_is_refused(self):
        # bypass the render-time check: the output check alone must see the extra model field
        self.manifest(lambda d: self.item(d, "ees-hat")["targets"][0]["frontmatter"].update(model="haiku"))
        real = sa.tier_fields
        sa.tier_fields = lambda tool, tier, own, where: {}
        try:
            self.refused("differ from what seviye")
        finally:
            sa.tier_fields = real

    # --- blind check 4: new variants against the same two properties ---------------------------------------------
    def test_n1_a_non_ascii_look_alike_ees_path_is_refused(self):
        # 'ſ' (U+017F) folds to 's' on a case-insensitive APFS volume
        def change(d):
            self.item(d, "localization-standard")["compose"] = [".ee\u017f/kartlar/cekirdek.md"]
        self.manifest(change)
        self.refused("not a canonical repository path")

    def test_n1d_n13_n14_the_managed_folders_are_fixed(self):
        for extra in (".claude/rule\u017f", ".agents/workflows", ".cursor/rules"):
            with self.subTest(extra=extra):
                self.manifest(lambda d: d["managed_dirs"].append(extra))
                self.refused("managed_dirs must be exactly")
                self.manifest(lambda d: d["managed_dirs"].remove(extra))

    def test_n2_a_target_that_is_a_hard_link_is_refused(self):
        target = self.root / ".claude" / "agents" / "ees-isci.md"
        target.unlink()
        try:
            os.link(self.root / ".claude" / "rules" / "agents-core.md", target)
        except (OSError, NotImplementedError):
            self.skipTest("hard links are not available here")
        self.refused("hard link")

    def test_n3_a_source_that_is_a_hard_link_is_refused(self):
        probe = self.root / ".antigravity" / "rules" / "probe.md"
        try:
            os.link(self.root / ".ees" / "kartlar" / "cekirdek.md", probe)
        except (OSError, NotImplementedError):
            self.skipTest("hard links are not available here")
        self.manifest(lambda d: d["items"].append(
            {"id": "probe", "kind": "rule", "source": ".antigravity/rules/probe.md",
             "targets": [{"tool": "claude-code", "format": "markdown", "path": ".claude/rules/probe.md"}]}))
        self.refused("hard link")

    def test_n4_a_target_that_is_a_directory_is_refused(self):
        target = self.root / ".claude" / "agents" / "ees-isci.md"
        target.unlink()
        target.mkdir()
        self.refused("is not a file")

    def test_n6_an_unknown_or_misplaced_tool_label_is_refused(self):
        for tool in ("Claude-Code", "claude", "codex"):
            with self.subTest(tool=tool):
                self.manifest(lambda d: self.item(d, "ees-isci")["targets"][0].update(tool=tool))
                self.refused("is unknown or does not write there")
                self.manifest(lambda d: self.item(d, "ees-isci")["targets"][0].update(tool="claude-code"))

    def test_n7_one_name_per_tool_whatever_the_kind(self):
        self.manifest(lambda d: self.item(d, "ees-hat")["targets"][0]["frontmatter"].update(name="ees-isci"))
        self.refused("is used by")

    def test_n8_n9_a_toml_key_that_is_not_a_plain_name_is_refused(self):
        for key in ('"name"', "zz = 1\n[t]\nyy", '"model"', "model "):
            with self.subTest(key=key):
                self.manifest(lambda d: self.item(d, "ees-l10n-agent")["targets"][1]["fields"].update({key: "x"}))
                self.refused("is not a plain name")
                self.manifest(lambda d: self.item(d, "ees-l10n-agent")["targets"][1]["fields"].pop(key))

    def test_n10_n11_a_yaml_key_or_value_that_could_hide_a_field_is_refused(self):
        self.manifest(lambda d: self.item(d, "ees-hat")["targets"][0]["frontmatter"].update({"model ": "haiku"}))
        self.refused("is not a plain name")
        self.manifest(lambda d: self.item(d, "ees-hat")["targets"][0]["frontmatter"].pop("model "))
        self.manifest(lambda d: self.item(d, "ees-l10n-agent")["targets"][0]["frontmatter"].update(
            zz='"open\nclose"\n"name": l10n-agent'))
        self.refused("without line breaks")

    def test_an_unreadable_generated_header_line_is_refused(self):
        # bypass the manifest check: the output reader alone must refuse a header line it cannot read
        self.manifest(lambda d: self.item(d, "ees-hat")["targets"][0]["frontmatter"].update({"zz ": "x"}))
        real = sa.validate_manifest
        sa.validate_manifest = lambda data: None
        try:
            self.refused("unreadable frontmatter line")
        finally:
            sa.validate_manifest = real

    def test_n12_a_source_with_a_byte_order_mark_is_refused(self):
        p = self.root / ".ees" / "ajanlar" / "isci.md"
        p.write_bytes(b"\xef\xbb\xbf" + p.read_bytes())
        self.refused("byte order mark")

    def test_n15_a_stray_file_stops_the_run_before_any_write(self):
        (self.root / ".claude" / "agents" / "stray.md").write_text("x\n", encoding="utf-8")
        self.source(".ees/kartlar/isci.md", "# İşçi kartı", "# İşçi kartı (değişti)")
        before = self.snapshot()
        code, out = self.run_main()
        self.assertEqual(code, 1, out)
        self.assertIn("nothing written", out)
        self.assertEqual(self.snapshot(), before)

    def test_f2_a_finder_ds_store_file_is_not_a_stray(self):
        for folder in (".claude/skills", ".agents/skills", ".claude/skills/ees-hat", ".claude/agents"):
            (self.root / folder / ".DS_Store").write_bytes(b"\x00\x00\x00\x01Bud1")
        code, out = self.run_main("--check")
        self.assertEqual(code, 0, out)
        self.assertNotIn("UNMANAGED", out)
        code, out = self.run_main()
        self.assertEqual(code, 0, out)
        self.assertTrue((self.root / ".claude" / "skills" / ".DS_Store").is_file())   # left alone, never deleted

    def test_f2_only_a_plain_file_named_ds_store_is_ignored(self):
        near = self.root / ".claude" / "skills" / ".DS_Store.md"
        near.write_text("x\n", encoding="utf-8")
        code, out = self.run_main("--check")
        self.assertEqual(code, 1, out)
        self.assertIn("UNMANAGED .claude/skills/.DS_Store.md", out)
        near.unlink()
        if can_symlink(self.root):
            os.symlink(self.root / "AGENTS.md", self.root / ".claude" / "skills" / ".DS_Store")
            code, out = self.run_main("--check")
            self.assertEqual(code, 1, out)
            self.assertIn("UNMANAGED .claude/skills/.DS_Store", out)

    # --- blind check 4, re-check: small gaps --------------------------------------------------------------------
    def test_x6_x12_names_and_keys_ignore_letter_case(self):
        self.manifest(lambda d: self.item(d, "ees-belge-ajani")["targets"][0]["frontmatter"].update(name="ees-Isci"))
        self.refused("is used by")
        self.manifest(lambda d: self.item(d, "ees-belge-ajani")["targets"][0]["frontmatter"].update(name="ees-belge-ajani"))
        self.manifest(lambda d: self.item(d, "ees-l10n-agent")["targets"][0]["frontmatter"].update(NAME="l10n-agent"))
        self.refused("differ only in letter case")

    def test_the_output_check_alone_compares_names_without_letter_case(self):
        self.manifest(lambda d: self.item(d, "ees-belge-ajani")["targets"][0]["frontmatter"].update(name="ees-Isci"))
        real = sa.validate_manifest
        sa.validate_manifest = lambda data: None
        try:
            self.refused("is also used by")
        finally:
            sa.validate_manifest = real

    def test_x5b_profile_counts_as_a_model_field(self):
        self.manifest(lambda d: self.item(d, "ees-isci")["targets"][1]["fields"].update(profile="fast"))
        self.refused("writes profile itself")

    def test_x3_a_sync_tmp_target_is_reserved(self):
        self.manifest(lambda d: self.item(d, "l10n-agent")["targets"][0].update(path=".claude/agents/l10n-agent.md.sync-tmp"))
        self.refused("reserved for the generator")

    def test_x2_x13_a_squatted_temporary_name_does_not_matter(self):
        # the old fixed '<target>.sync-tmp' name is occupied by a folder; the generator uses a fresh name instead
        (self.root / ".claude" / "agents" / "ees-isci.md.sync-tmp").mkdir()
        self.source(".ees/kartlar/isci.md", "# İşçi kartı", "# İşçi kartı (değişti)")
        code, out = self.run_main()
        self.assertEqual(code, 0, out)          # no crash, no half write: every output is written in full
        self.assertTrue((self.root / ".claude" / "agents" / "ees-isci.md.sync-tmp").is_dir())
        self.assertIn("(değişti)", (self.root / ".claude" / "agents" / "ees-isci.md").read_text(encoding="utf-8"))
        self.assertEqual(self.run_main("--check")[0], 0)
        self.assertEqual(list((self.root / ".claude").rglob("*.sync-tmp")),
                         [self.root / ".claude" / "agents" / "ees-isci.md.sync-tmp"])   # no temporary file left


if __name__ == "__main__":
    unittest.main()
