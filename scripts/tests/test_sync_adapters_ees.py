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

CARD_LIMITS = {"cekirdek": 70, "kule": 90, "hat": 110, "isci": 60, "denetci": 50}   # owner decision 2026-11-16
OLD_ITEMS = {"agents-core", "l10n-agent", "security-agent", "module-pack-author", "resx-check", "control-tower",
             "localization-standard"}   # the 13 adapters from before the wiring


def from_ees(item: dict) -> bool:
    return sa.is_ees(item["source"]) or any(sa.is_ees(p) for p in item.get("compose", []))


class PilotBoundary(unittest.TestCase):
    """Codex round 1, finding 1: every case runs the generator's own pre-write validation, not a helper of this file."""

    def setUp(self):
        self.manifest = sa.load_manifest()

    def broken(self, change) -> dict:
        data = json.loads(json.dumps(self.manifest))
        change(data)
        return data

    def item(self, data: dict, name: str) -> dict:
        return next(i for i in data["items"] if i["id"] == name)

    def refused(self, change, needle: str):
        with self.assertRaises(sa.ConfigError) as ctx:
            sa.validate_manifest(self.broken(change))
        self.assertIn(needle, str(ctx.exception))

    def test_the_manifest_lives_in_ees(self):
        self.assertEqual(sa.MANIFEST, ".ees/adapters.json")
        self.assertFalse((REPO / ".antigravity" / "adapters.json").exists(), "two manifests would disagree")

    def test_the_real_manifest_passes_the_validation(self):
        sa.validate_manifest(self.manifest)

    def test_a_rules_target_from_ees_is_refused(self):
        self.refused(lambda d: self.item(d, "ees-isci")["targets"].append(
            {"tool": "claude-code", "format": "markdown", "path": ".claude/rules/ees-cekirdek.md"}), "pilot boundary")

    def test_a_dot_slash_source_is_refused(self):
        # Codex: './.ees/kartlar/cekirdek.md' passed the old text-prefix test and was written to .claude/rules
        def change(d):
            d["items"].append({"id": "ees-probe", "kind": "rule", "source": "./.ees/kartlar/cekirdek.md",
                               "targets": [{"tool": "claude-code", "format": "markdown", "path": ".claude/rules/ees-probe.md"}]})
        self.refused(change, "not a canonical repository path")

    def test_a_card_hidden_in_compose_still_counts_as_ees(self):
        def change(d):
            d["items"].append({"id": "probe", "kind": "rule", "source": ".antigravity/rules/localization-standard.md",
                               "compose": [".ees/kartlar/cekirdek.md"],
                               "targets": [{"tool": "claude-code", "format": "markdown", "path": ".claude/rules/probe.md"}]})
        self.refused(change, "pilot boundary")

    def test_a_codex_skill_target_is_refused(self):
        self.refused(lambda d: self.item(d, "ees-hat")["targets"].append(
            {"tool": "codex+antigravity", "format": "markdown", "path": ".agents/skills/ees-hat/SKILL.md",
             "frontmatter": {"name": "ees-hat", "description": "d"}}), "pilot boundary")

    def test_a_pilot_name_without_the_prefix_is_refused(self):
        # Codex: id and paths kept, the real name fields set to the old agent's name
        def change(d):
            for t in self.item(d, "ees-l10n-agent")["targets"]:
                (t.get("frontmatter") or t.get("fields"))["name"] = "l10n-agent"
        self.refused(change, "prefix")

    def test_a_name_used_twice_for_one_tool_is_refused(self):
        def change(d):
            for t in self.item(d, "ees-belge-ajani")["targets"]:
                (t.get("frontmatter") or t.get("fields"))["name"] = "ees-isci"
        self.refused(change, "is used by")

    def test_a_pilot_skill_open_to_the_model_is_refused(self):
        def change(d):
            self.item(d, "ees-kule")["targets"][0]["frontmatter"]["disable-model-invocation"] = False
        self.refused(change, "disable-model-invocation")

    def test_a_refused_manifest_writes_nothing(self):
        import contextlib
        import io
        import sys
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        bad = Path(tmp.name) / "adapters.json"
        data = self.broken(lambda d: self.item(d, "ees-isci")["targets"].append(
            {"tool": "claude-code", "format": "markdown", "path": ".claude/rules/ees-cekirdek.md"}))
        bad.write_text(json.dumps(data), encoding="utf-8")
        out = Path(tmp.name) / "out"
        targets = {t["path"] for i in data["items"] for t in i["targets"]}
        real = sa.rel
        sa.rel = lambda p: bad if p == sa.MANIFEST else (out / p if p in targets else real(p))
        argv, sys.argv = sys.argv, ["sync_adapters.py"]
        try:
            with contextlib.redirect_stdout(io.StringIO()) as buf:
                code = sa.main()
        finally:
            sys.argv = argv
            sa.rel = real
            sa.load_manifest()
        self.assertEqual(code, 2, buf.getvalue())
        self.assertIn("pilot boundary", buf.getvalue())
        self.assertFalse(out.exists() and any(out.rglob("*")), "an adapter was written despite a refused manifest")

    def test_old_sources_carry_no_seviye(self):
        # a seviye in an old source would change an old adapter's model field on the next regeneration
        for item in self.manifest["items"]:
            if not from_ees(item):
                block, _ = sa.split_frontmatter(sa.read_text(item["source"]) if item.get("extract") != "core" else "")
                self.assertIsNone(sa.source_tier(block, item["source"]), item["id"])

    def test_old_adapters_keep_their_old_source(self):
        items = {i["id"]: i for i in self.manifest["items"]}
        self.assertTrue(OLD_ITEMS <= set(items))
        for name in OLD_ITEMS:
            self.assertFalse(from_ees(items[name]), name)

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

    def test_a_commented_seviye_or_kart_line_fails(self):
        # Codex: 'seviye: cok-ust # typo', 'kart: denetci # selected role' and 'seviye: standart # note' were skipped
        for front in ("seviye: cok-ust # typo", "seviye: standart # note", "kart: denetci # selected role",
                      "Seviye: ust", "  seviye: ust", 'seviye: "ust"', "seviye: ust\nseviye: hafif"):
            self.files(c1="# Kart\n", src=f"---\n{front}\n---\nGövde\n")
            with self.assertRaises(sa.ConfigError, msg=front):
                sa.render({"id": "x", "kind": "agent", "source": "src", "compose": ["c1"]},
                          {"tool": "other", "format": "markdown", "path": "p"})

    def test_an_unmapped_tool_target_may_not_write_its_own_model(self):
        # Codex: Codex has no mapping, so model = "test-model" in fields went into the TOML unchecked
        self.files(src="---\nseviye: standart\n---\nGövde\n")
        sa.TIER_MAP = {"claude-code": {"standart": {"model": "sonnet"}}}
        with self.assertRaises(sa.ConfigError):
            sa.render({"id": "x", "kind": "agent", "source": "src"},
                      {"tool": "codex", "format": "codex-agent-toml", "path": "p",
                       "fields": {"name": "x", "description": "d", "model": "test-model"}})

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
