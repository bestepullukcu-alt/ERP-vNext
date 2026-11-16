"""EES-BAGLAMA-01 (2026-11-16) — the EES lane cards are wired through sync_adapters without touching what every existing
session loads by itself. Each test names the rule it guards; each was shown red by breaking that rule once (sabotage
record in .ees/gecis/baglama-kaniti.md). Run by scripts/run_phase1_gates.sh:

    python3 -m unittest discover -s scripts/tests -p 'test_*.py'
"""

from __future__ import annotations

import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("sync_adapters", REPO / ".antigravity" / "scripts" / "sync_adapters.py")
sa = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(sa)

# What a session loads by itself, with no skill or agent invoked: the pilot boundary says no .ees source reaches these.
AUTO_LOADED_DIRS = (".claude/rules/", ".agents/rules/")
# Codex and Antigravity may call a skill under .agents/skills by themselves and no switch for that is verified yet, so no
# EES skill goes there until the pilot measures it (blind check 2, finding 3).
IMPLICIT_SKILL_DIRS = (".agents/skills/",)
AUTO_LOADED_FILES = ("AGENTS.md", "CLAUDE.md")
CARD_LIMITS = {"cekirdek": 70, "kule": 90, "hat": 110, "isci": 60, "denetci": 50}   # owner decision 2026-11-16


def from_ees(item: dict) -> bool:
    return item["source"].startswith(".ees/") or any(p.startswith(".ees/") for p in item.get("compose", []))


def pilot_boundary_problems(manifest: dict) -> list[str]:
    bad = []
    for item in manifest["items"]:
        if not from_ees(item):
            continue
        for t in item["targets"]:
            if (t["path"].startswith(AUTO_LOADED_DIRS) or t["path"] in AUTO_LOADED_FILES
                    or (item["kind"] == "skill" and t["path"].startswith(IMPLICIT_SKILL_DIRS))):
                bad.append(f"{item['id']} -> {t['path']}")
    if any(i.get("extract") == "core" and i["source"].startswith(".ees/") for i in manifest["items"]):
        bad.append("the always-loaded core block is generated from .ees/")
    return bad


class PilotBoundary(unittest.TestCase):
    def setUp(self):
        self.manifest = sa.load_manifest()

    def test_the_manifest_lives_in_ees(self):
        self.assertEqual(sa.MANIFEST, ".ees/adapters.json")
        self.assertFalse((REPO / ".antigravity" / "adapters.json").exists(), "two manifests would disagree")

    def test_no_ees_source_reaches_a_file_every_session_loads(self):
        self.assertEqual(pilot_boundary_problems(self.manifest), [])

    def test_the_boundary_check_fires_on_a_rules_target(self):
        broken = json.loads(json.dumps(self.manifest))
        isci = next(i for i in broken["items"] if i["id"] == "ees-isci")
        isci["targets"].append({"tool": "claude-code", "format": "markdown", "path": ".claude/rules/ees-cekirdek.md"})
        self.assertEqual(pilot_boundary_problems(broken), ["ees-isci -> .claude/rules/ees-cekirdek.md"])

    def test_the_boundary_check_fires_on_a_codex_skill_target(self):
        broken = json.loads(json.dumps(self.manifest))
        hat = next(i for i in broken["items"] if i["id"] == "ees-hat")
        hat["targets"].append({"tool": "codex+antigravity", "format": "markdown", "path": ".agents/skills/ees-hat/SKILL.md"})
        self.assertEqual(pilot_boundary_problems(broken), ["ees-hat -> .agents/skills/ees-hat/SKILL.md"])

    def test_every_pilot_entry_point_has_the_ees_prefix(self):
        for item in self.manifest["items"]:
            if from_ees(item):
                self.assertTrue(item["id"].startswith("ees-"), item["id"])
                for t in item["targets"]:
                    self.assertIn("/ees-", t["path"], t["path"])

    def test_old_sources_carry_no_seviye(self):
        # a seviye in an old source would change an old adapter's model field on the next regeneration
        for item in self.manifest["items"]:
            if not from_ees(item):
                block, _ = sa.split_frontmatter(sa.read_text(item["source"]) if item.get("extract") != "core" else "")
                self.assertIsNone(sa.source_tier(block, item["source"]), item["id"])

    def test_old_adapters_keep_their_old_source(self):
        # the 13 adapters from before the wiring: same source, no card, no tier — so their bytes cannot change
        old = {"agents-core", "l10n-agent", "security-agent", "module-pack-author", "resx-check", "control-tower",
               "localization-standard"}
        items = {i["id"]: i for i in self.manifest["items"]}
        self.assertTrue(old <= set(items))
        for name in old:
            self.assertFalse(from_ees(items[name]), name)

    def test_new_skills_are_never_invoked_by_the_model_in_claude(self):
        for item in self.manifest["items"]:
            if item["kind"] == "skill" and from_ees(item):
                claude = [t for t in item["targets"] if t["tool"] == "claude-code"]
                self.assertTrue(claude and all(t["frontmatter"].get("disable-model-invocation") is True for t in claude),
                                item["id"])

    def test_every_card_has_the_owner_line_limit(self):
        files = {f["path"]: f for f in self.manifest["instruction_budget"]["files"]}
        for card, limit in CARD_LIMITS.items():
            spec = files.get(f".ees/kartlar/{card}.md")
            self.assertIsNotNone(spec, card)
            self.assertEqual(spec.get("max_lines"), limit, card)


class Features(unittest.TestCase):
    def setUp(self):
        sa.load_manifest()
        self.tmp = tempfile.TemporaryDirectory()
        self.real_rel = sa.rel
        self.tier_map = sa.TIER_MAP

    def tearDown(self):
        sa.rel = self.real_rel
        sa.TIER_MAP = self.tier_map
        self.tmp.cleanup()

    def files(self, **texts: str):
        """Serve the given repo paths from a temporary folder (keys use __ for /)."""
        paths = {}
        for key, text in texts.items():
            p = Path(self.tmp.name) / key
            p.write_text(text, encoding="utf-8")
            paths[key.replace("__", "/")] = p
        sa.rel = lambda path: paths.get(path) or self.real_rel(path)

    def test_too_many_lines_in_a_card_fails(self):
        self.files(**{".ees__kartlar__x.md": "a\n" * 4})
        budget = {"instruction_budget": {"files": [{"path": ".ees/kartlar/x.md", "max_lines": 3}]}}
        self.assertEqual(sa.check_instruction_budget(budget)[0], ["OVERLINES .ees/kartlar/x.md has 4 lines; limit 3"])

    def test_line_count_ignores_windows_line_ends(self):
        p = Path(self.tmp.name) / "crlf.md"
        p.write_bytes(b"a\r\nb\r\nc\r\n")
        sa.rel = lambda path: p if path == "x.md" else self.real_rel(path)
        budget = {"instruction_budget": {"files": [{"path": "x.md", "max_lines": 3, "max_bytes": 6}]}}
        self.assertEqual(sa.check_instruction_budget(budget), ([], []))

    def test_composed_cards_come_first_and_are_named(self):
        self.files(c1="# Kart 1\n", c2="# Kart 2\n", src="---\nseviye: standart\n---\n\nGövde\n")
        item = {"id": "x", "kind": "agent", "source": "src", "compose": ["c1", "c2"]}
        out = sa.render(item, {"tool": "other", "format": "markdown", "path": "p"})
        self.assertIn("Kaynak: `c1 + c2 + src`", out)
        self.assertLess(out.index("# Kart 1"), out.index("# Kart 2"))
        self.assertLess(out.index("# Kart 2"), out.index("Gövde"))
        self.assertNotIn("seviye", out)

    def test_a_card_change_changes_every_adapter_that_carries_it(self):
        item = {"id": "x", "kind": "agent", "source": "src", "compose": ["c1"]}
        target = {"tool": "other", "format": "markdown", "path": "p"}
        self.files(c1="# Kart\n", src="Gövde\n")
        before = sa.render(item, target)
        self.files(c1="# Kart (değişti)\n", src="Gövde\n")
        after = sa.render(item, target)
        self.assertNotEqual(before.split("sha256:")[1][:12], after.split("sha256:")[1][:12])

    def test_seviye_becomes_the_tools_model_fields(self):
        self.files(src="---\nseviye: ust\n---\nGövde\n")
        sa.TIER_MAP = {"claude-code": {"ust": {"model": "opus", "effort": "high"}}}
        item = {"id": "x", "kind": "agent", "source": "src"}
        out = sa.render(item, {"tool": "claude-code", "format": "markdown", "path": ".claude/agents/x.md",
                               "frontmatter": {"name": "x", "description": "d"}})
        self.assertIn("model: opus\neffort: high\n", out)
        codex = sa.render(item, {"tool": "codex", "format": "codex-agent-toml", "path": ".codex/agents/x.toml",
                                 "fields": {"name": "x", "description": "d"}})
        self.assertNotIn("model", codex.split("developer_instructions")[0])   # no verified Codex mapping: no field

    def test_a_target_that_writes_its_own_model_beside_a_seviye_fails(self):
        self.files(src="---\nseviye: ust\n---\nGövde\n")
        sa.TIER_MAP = {"claude-code": {"ust": {"model": "opus"}}}
        with self.assertRaises(sa.ConfigError):
            sa.render({"id": "x", "kind": "agent", "source": "src"},
                      {"tool": "claude-code", "format": "markdown", "path": "p",
                       "frontmatter": {"name": "x", "description": "d", "model": "haiku"}})

    def test_a_kart_field_that_disagrees_with_the_manifest_fails(self):
        self.files(**{"c1": "# Kart\n", "src": "---\nkart: denetci\n---\nGövde\n"})
        with self.assertRaises(sa.ConfigError):
            sa.render({"id": "x", "kind": "agent", "source": "src", "compose": ["c1"]},
                      {"tool": "other", "format": "markdown", "path": "p"})

    def test_an_unknown_seviye_fails(self):
        self.files(src="---\nseviye: cok-ust\n---\nGövde\n")
        with self.assertRaises(sa.ConfigError):
            sa.render({"id": "x", "kind": "agent", "source": "src"}, {"tool": "other", "format": "markdown", "path": "p"})

    def test_a_skill_seviye_switches_no_model(self):
        self.files(src="---\nseviye: ust\n---\nGövde\n")
        sa.TIER_MAP = {"claude-code": {"ust": {"model": "opus"}}}
        out = sa.render({"id": "x", "kind": "skill", "source": "src"},
                        {"tool": "claude-code", "format": "markdown", "path": "p",
                         "frontmatter": {"name": "x", "description": "d", "disable-model-invocation": True}})
        self.assertNotIn("model", out.split("---")[1].replace("disable-model-invocation", ""))
        self.assertIn("disable-model-invocation: true\n", out)


if __name__ == "__main__":
    unittest.main()
