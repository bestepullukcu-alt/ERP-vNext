#!/usr/bin/env python3
"""
Generate tool adapters (Claude Code, Codex, Antigravity) from the single source in .antigravity/.

    python3 .antigravity/scripts/sync_adapters.py           # write every adapter listed in .antigravity/adapters.json
    python3 .antigravity/scripts/sync_adapters.py --check   # write nothing; exit 1 if any adapter differs from its source

Rules this script enforces (İ5-A pilot):
- .antigravity/ stays the only place a human edits. Every adapter carries a visible "generated" line with the
  source path and the source hash; editing an adapter by hand is drift and fails --check.
- Only what adapters.json lists may live in the managed adapter folders. Any other file there fails --check, so no
  agent, skill or rule enters a tool's automatic discovery without passing through the manifest.
- An agent source may only name skills that exist under .antigravity/skills/ (the 4/4 missing-skill case on
  security-agent must not come back).
- Output is LF and UTF-8 on every platform; --check compares after turning CRLF into LF, so a Windows checkout
  with core.autocrlf=true is not drift. No symlinks are used anywhere.
- İ5-B: the "Çekirdek" block of AGENTS.md (between <!-- core:start --> and <!-- core:end -->) is copied to
  .claude/rules/agents-core.md, so Claude Code loads the binding rules without importing all of AGENTS.md.
  The manifest's instruction_budget also fails --check when AGENTS.md exceeds its byte limit (Antigravity truncates
  at 24000), loses a § heading or a core rule, when CLAUDE.md imports AGENTS.md, or when a listed file has a dead link.

Standard library only. Exit codes: 0 = in sync, 1 = drift, 2 = manifest or source error.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ".antigravity/adapters.json"
REGENERATE = "python3 .antigravity/scripts/sync_adapters.py"
ANTIGRAVITY_TRIGGERS = {"always_on", "model_decision", "glob", "manual"}


class ConfigError(Exception):
    pass


def rel(path: str) -> Path:
    return ROOT.joinpath(*path.split("/"))


def read_text(path: str) -> str:
    p = rel(path)
    if not p.is_file():
        raise ConfigError(f"source not found: {path}")
    return p.read_text(encoding="utf-8").replace("\r\n", "\n")


def split_frontmatter(text: str) -> tuple[str | None, str]:
    """Return (frontmatter block without the --- lines, body). Frontmatter must start on line 1."""
    if not text.startswith("---\n"):
        return None, text
    end = text.find("\n---\n", 4)
    if end == -1:
        raise ConfigError("frontmatter opened with --- but never closed")
    return text[4:end], text[end + 5:]


def frontmatter_list(block: str, key: str) -> list[str]:
    m = re.search(rf"(?m)^{re.escape(key)}:\s*(.*)$", block or "")
    if not m or not m.group(1).strip():
        return []
    return [s.strip() for s in m.group(1).split(",") if s.strip()]


def source_hash(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()[:12]


def yaml_scalar(value: str) -> str:
    # Plain tokens stay unquoted (trigger: glob, model: sonnet) so that a tool reading the header naively still
    # sees the documented form; everything else is double-quoted.
    if re.fullmatch(r"[A-Za-z][A-Za-z0-9_.-]*", value) and value.lower() not in {"true", "false", "null", "yes", "no", "on", "off"}:
        return value
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


def emit_frontmatter(fields: dict) -> str:
    lines = ["---"]
    for key, value in fields.items():
        if isinstance(value, list):
            lines.append(f"{key}:")
            lines.extend(f"  - {yaml_scalar(str(v))}" for v in value)
        else:
            lines.append(f"{key}: {yaml_scalar(str(value))}")
    lines.append("---")
    return "\n".join(lines) + "\n"


def generated_line(source: str, digest: str) -> str:
    return (f"> **Üretilmiş kopya — elle düzenlemeyin.** Kaynak: `{source}` · kaynak özeti: `sha256:{digest}` · "
            f"yeniden üret: `{REGENERATE}`\n")


def toml_basic(value: str) -> str:
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


CORE_START, CORE_END = "<!-- core:start -->", "<!-- core:end -->"


def extract_core(source: str) -> str:
    """İ5-B: the compact core block of AGENTS.md, without its markers."""
    text = read_text(source)
    if text.count(CORE_START) != 1 or text.count(CORE_END) != 1 or text.index(CORE_START) > text.index(CORE_END):
        raise ConfigError(f"{source}: needs exactly one {CORE_START} … {CORE_END} block")
    return text.split(CORE_START, 1)[1].split(CORE_END, 1)[0].strip("\n") + "\n"


def render(item: dict, target: dict) -> str:
    source = item["source"]
    text = extract_core(source) if item.get("extract") == "core" else read_text(source)
    digest = source_hash(text)
    block, body = split_frontmatter(text)
    body = body.lstrip("\n")
    fmt = target.get("format")

    if item["kind"] == "agent":
        missing = [s for s in frontmatter_list(block, "skills") if not rel(f".antigravity/skills/{s}/SKILL.md").is_file()]
        if missing:
            raise ConfigError(f"{source} names skills that do not exist under .antigravity/skills/: {', '.join(missing)}")

    if fmt == "codex-agent-toml":
        fields = target.get("fields") or {}
        for key in ("name", "description"):
            if not str(fields.get(key, "")).strip():
                raise ConfigError(f"{target['path']}: codex agent needs a non-empty '{key}'")
        instructions = generated_line(source, digest) + "\n" + body
        if "'''" in instructions:
            raise ConfigError(f"{source}: body contains ''' and cannot be a TOML literal string")
        out = [f"# Generated by .antigravity/scripts/sync_adapters.py from {source} (sha256:{digest}). Do not edit.",
               f"# Regenerate: {REGENERATE}"]
        out += [f"{k} = {toml_basic(str(v))}" for k, v in fields.items()]
        out.append("developer_instructions = '''")
        out.append(instructions.rstrip("\n"))
        out.append("'''")
        return "\n".join(out) + "\n"

    if fmt != "markdown":
        raise ConfigError(f"{target['path']}: unknown format '{fmt}'")

    fm = target.get("frontmatter")
    if fm == "from-source":
        if block is None:
            raise ConfigError(f"{source}: target asks for the source frontmatter but the source has none")
        head = "---\n" + block + "\n---\n"
    elif isinstance(fm, dict):
        if target["tool"] == "claude-code" and item["kind"] == "agent":
            for key in ("name", "description"):
                if not str(fm.get(key, "")).strip():
                    raise ConfigError(f"{target['path']}: Claude Code agent needs a non-empty '{key}'")
        if target["tool"] == "antigravity":
            if fm.get("trigger") not in ANTIGRAVITY_TRIGGERS:
                raise ConfigError(f"{target['path']}: Antigravity silently drops a rule without a valid trigger")
            if fm["trigger"] == "glob" and not (fm.get("globs") or fm.get("glob")):
                raise ConfigError(f"{target['path']}: trigger 'glob' needs 'globs'")
        head = emit_frontmatter(fm)
    else:
        head = ""
    return (head + "\n" if head else "") + generated_line(source, digest) + "\n" + body.rstrip("\n") + "\n"


def check_instruction_budget(manifest: dict) -> tuple[list[str], list[str]]:
    """İ5-B: size budget, required headings and core rules, forbidden patterns and dead links in the instruction files.
    Returns (problems, warnings)."""
    budget = manifest.get("instruction_budget") or {}
    problems: list[str] = []
    warnings: list[str] = []
    for spec in budget.get("files", []):
        path = spec["path"]
        p = rel(path)
        if not p.is_file():
            problems.append(f"MISSING {path} (instruction file)")
            continue
        raw = p.read_bytes().replace(b"\r\n", b"\n")
        text = raw.decode("utf-8")
        size = len(raw)
        if size > spec.get("max_bytes", 10 ** 9):
            problems.append(f"OVERSIZE {path} is {size} bytes; limit {spec['max_bytes']} (Antigravity truncates AGENTS.md at 24000)")
        elif size > spec.get("warn_bytes", 10 ** 9):
            warnings.append(f"WARN {path} is {size} bytes; warning threshold {spec['warn_bytes']}")
        for heading in spec.get("required_headings", []):
            if not re.search(rf"(?m)^## {re.escape(heading)}(?:\s|$)", text):
                problems.append(f"HEADING {path} lost '## {heading}' (AGENTS.md §-references would break)")
        for pattern in spec.get("forbidden_regex", []):
            if re.search(pattern, text):
                problems.append(f"FORBIDDEN {path} matches {pattern!r}")
    core = budget.get("core")
    if core:
        try:
            block = extract_core(core["source"])
        except ConfigError as e:
            problems.append(f"CORE {e}")
        else:
            for needle in core.get("required_text", []):
                if needle not in block:
                    problems.append(f"CORE-RULE {core['source']} core block lost: {needle!r}")
    for path in budget.get("link_check", []):
        p = rel(path)
        if not p.is_file():
            # İ5B-FIX-01 (independent audit 2026-10-17, finding 02): a listed file that is gone is a problem, not a file
            # to skip — deleting .antigravity/AGENTS-REFERENCE.md would otherwise lose the moved text with a clean check.
            problems.append(f"MISSING {path} (link_check)")
            continue
        for target in re.findall(r"\]\(([^)\s]+)\)", p.read_text(encoding="utf-8")):
            if re.match(r"(https?:|mailto:|#)", target):
                continue
            if not (p.parent / target.split("#", 1)[0]).exists():
                problems.append(f"DEADLINK {path} -> {target}")
    return problems, warnings


def load_manifest() -> dict:
    try:
        data = json.loads(read_text(MANIFEST))
    except json.JSONDecodeError as e:
        raise ConfigError(f"{MANIFEST}: invalid JSON ({e})") from e
    paths = [t["path"] for i in data["items"] for t in i["targets"]]
    if len(paths) != len(set(paths)):
        raise ConfigError(f"{MANIFEST}: two targets share one path")
    for p in paths:
        if not any(p.startswith(d + "/") for d in data["managed_dirs"]):
            raise ConfigError(f"{MANIFEST}: target {p} is outside managed_dirs")
    return data


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    parser = argparse.ArgumentParser(description="Generate or check tool adapters from .antigravity/")
    parser.add_argument("--check", action="store_true", help="write nothing; exit 1 on any difference")
    args = parser.parse_args()

    try:
        manifest = load_manifest()
        expected = {t["path"]: (item["source"], render(item, t)) for item in manifest["items"] for t in item["targets"]}
    except (ConfigError, KeyError) as e:
        print(f"ERROR {e}")
        return 2

    problems: list[str] = []
    for path, (source, content) in expected.items():
        target = rel(path)
        if args.check:
            if not target.is_file():
                problems.append(f"MISSING {path} (source {source}; run {REGENERATE})")
            elif target.read_text(encoding="utf-8").replace("\r\n", "\n") != content:
                problems.append(f"DRIFT {path} differs from source {source}; run {REGENERATE}")
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            with open(target, "w", encoding="utf-8", newline="\n") as fh:
                fh.write(content)
            print(f"wrote {path}")

    for managed in manifest["managed_dirs"]:
        base = rel(managed)
        if not base.is_dir():
            continue
        for f in sorted(p for p in base.rglob("*") if p.is_file()):
            r = f.relative_to(ROOT).as_posix()
            if r not in expected:
                problems.append(f"UNMANAGED {r} is in a tool discovery folder but not in {MANIFEST}")

    budget_problems, budget_warnings = check_instruction_budget(manifest)
    problems += budget_problems
    for line in budget_warnings:
        print(line)
    for line in problems:
        print(line)
    if problems:
        print(f"FAIL {len(problems)} adapter problem(s)")
        return 1
    print(f"OK {len(expected)} adapter(s) match their sources")
    return 0


if __name__ == "__main__":
    sys.exit(main())
