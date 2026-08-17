#!/usr/bin/env python3
"""Generate execution/DASHBOARD.md from module pack YAML frontmatter."""

from __future__ import annotations

import argparse
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, List


@dataclass
class ModulePack:
    domain: str
    file_path: Path
    meta: Dict[str, str]


def parse_frontmatter(text: str) -> Dict[str, str]:
    lines = text.splitlines()
    if len(lines) < 3 or lines[0].strip() != "---":
        return {}

    meta: Dict[str, str] = {}
    i = 1
    while i < len(lines):
        line = lines[i]
        if line.strip() == "---":
            break
        if ":" in line:
            key, value = line.split(":", 1)
            meta[key.strip()] = value.strip().strip('"').strip("'")
        i += 1

    return meta


def discover_packs(repo_root: Path) -> List[ModulePack]:
    packs: List[ModulePack] = []
    domains_root = repo_root / "execution" / "domains"

    if not domains_root.exists():
        return packs

    for domain_dir in sorted(p for p in domains_root.iterdir() if p.is_dir()):
        module_packs_dir = domain_dir / "module-packs"
        if not module_packs_dir.exists():
            continue

        for pack_file in sorted(module_packs_dir.glob("*.md")):
            if pack_file.name.startswith("."):
                continue
            if pack_file.name.lower() == ".gitkeep":
                continue

            meta = parse_frontmatter(pack_file.read_text(encoding="utf-8"))
            packs.append(ModulePack(domain=domain_dir.name, file_path=pack_file, meta=meta))

    return packs


def md_escape(value: str) -> str:
    return value.replace("|", "\\|").replace("\n", " ").strip()


def render_dashboard(packs: List[ModulePack], repo_root: Path) -> str:
    now = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC")

    by_domain: Dict[str, List[ModulePack]] = {}
    for pack in packs:
        by_domain.setdefault(pack.domain, []).append(pack)

    for domain_packs in by_domain.values():
        domain_packs.sort(key=lambda x: x.meta.get("id", x.file_path.name))

    total = len(packs)
    status_counts: Dict[str, int] = {}
    for pack in packs:
        status = pack.meta.get("status", "unknown")
        status_counts[status] = status_counts.get(status, 0) + 1

    status_summary = ", ".join(
        f"`{key}`: {status_counts[key]}" for key in sorted(status_counts)
    ) or "No module packs yet"

    lines: List[str] = []
    lines.append("# Execution Dashboard")
    lines.append("")
    lines.append(f"Generated: {now}")
    lines.append("")
    lines.append("## Summary")
    lines.append("")
    lines.append(f"- Total module packs: **{total}**")
    lines.append(f"- Status distribution: {status_summary}")
    lines.append("")

    if not by_domain:
        lines.append("No module packs found under `execution/domains/*/module-packs/`.")
        lines.append("")
        lines.append("Run this script again after adding your first module pack.")
        return "\n".join(lines) + "\n"

    for domain in sorted(by_domain):
        lines.append(f"## {domain}")
        lines.append("")
        lines.append("| ID | Name | Status | Owner | Branch | Started | Target | File |")
        lines.append("|---|---|---|---|---|---|---|---|")

        for pack in by_domain[domain]:
            rel_path = pack.file_path.relative_to(repo_root)
            meta = pack.meta
            row = [
                md_escape(meta.get("id", "-")),
                md_escape(meta.get("name", "-")),
                md_escape(meta.get("status", "-")),
                md_escape(meta.get("owner", "-")),
                md_escape(meta.get("branch", "-")),
                md_escape(meta.get("started", "-")),
                md_escape(meta.get("target", "-")),
                md_escape(str(rel_path)),
            ]
            lines.append("| " + " | ".join(row) + " |")

        lines.append("")

    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Generate execution/DASHBOARD.md from module pack YAML frontmatter"
    )
    parser.add_argument(
        "repo_root",
        nargs="?",
        default=".",
        help="Path to repository root (default: current directory)",
    )
    parser.add_argument(
        "--output",
        default="execution/DASHBOARD.md",
        help="Output markdown path, relative to repo root",
    )

    args = parser.parse_args()

    repo_root = Path(args.repo_root).resolve()
    output_path = (repo_root / args.output).resolve()

    packs = discover_packs(repo_root)
    markdown = render_dashboard(packs, repo_root)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(markdown, encoding="utf-8")

    print(f"Dashboard generated: {output_path}")
    print(f"Module packs scanned: {len(packs)}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
