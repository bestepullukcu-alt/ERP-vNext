#!/usr/bin/env python3
"""Print the standard multi-module status table (REP-001, .antigravity/rules/status-reporting-and-evidence.md).

Nobody hand-writes these numbers. The script reads three sources that already exist in the repository:

  1. module packs      — the `status:` field in each pack's front matter
  2. CT decision records — the front matter block REP-001 §6 asks for
                           (module / work_package / evidence_level / decision / commit)
  3. git                — how far the branch under review is behind origin/main

Anything it cannot find is printed as "kayıt yok". It never guesses a level, a decision or a percentage.

    python3 scripts/status_report.py --packs "execution/domains/*/module-packs/*.md"
    python3 scripts/status_report.py --ref origin/feature/mvp6-logistics \
        --packs "execution/domains/supply-chain-execution/module-packs/*.md"
"""

from __future__ import annotations

import argparse
import fnmatch
import re
import subprocess
import sys
from pathlib import Path

FRONT_MATTER = re.compile(r"\A---\s*\n(.*?)\n---\s*\n", re.S)
MODULE_CODE = re.compile(r"\b((?:MOD|CAND-CAP|DCP)-[0-9]{3,4})\b")
EVIDENCE_LEVELS = {"E0", "E1", "E2", "E3", "E4", "E5"}
DECISIONS = {"accepted", "rejected", "conditional"}


def run(args: list[str], cwd: Path) -> str:
    result = subprocess.run(args, cwd=cwd, capture_output=True, text=True)
    return result.stdout if result.returncode == 0 else ""


def repo_root(start: Path) -> Path:
    top = run(["git", "rev-parse", "--show-toplevel"], start).strip()
    return Path(top) if top else start


def list_files(root: Path, ref: str | None, pattern: str) -> list[str]:
    """Paths matching the glob, either in the working tree or in a git ref."""
    if ref:
        tracked = run(["git", "ls-tree", "-r", "--name-only", ref], root).splitlines()
        return sorted(p for p in tracked if fnmatch.fnmatch(p, pattern))
    return sorted(str(p.relative_to(root)) for p in root.glob(pattern))


def read_file(root: Path, ref: str | None, path: str) -> str:
    if ref:
        return run(["git", "show", f"{ref}:{path}"], root)
    full = root / path
    return full.read_text(encoding="utf-8") if full.exists() else ""


def front_matter(text: str) -> dict[str, str]:
    match = FRONT_MATTER.search(text)
    if not match:
        return {}
    fields: dict[str, str] = {}
    for line in match.group(1).splitlines():
        if ":" not in line or line.lstrip().startswith("#"):
            continue
        key, _, value = line.partition(":")
        fields[key.strip()] = value.split("#", 1)[0].strip().strip('"').strip("'")
    return fields


def module_code_of(path: str, fields: dict[str, str]) -> str | None:
    for candidate in (fields.get("module"), fields.get("module_id"), fields.get("id"), Path(path).name):
        if candidate:
            found = MODULE_CODE.search(candidate)
            if found:
                return found.group(1)
    return None


def collect_packs(root: Path, ref: str | None, pattern: str) -> dict[str, dict[str, str]]:
    packs: dict[str, dict[str, str]] = {}
    for path in list_files(root, ref, pattern):
        fields = front_matter(read_file(root, ref, path))
        code = module_code_of(path, fields)
        if not code:
            continue
        packs[code] = {
            "path": path,
            "status": fields.get("status", "kayıt yok"),
            "title": fields.get("title", ""),
        }
    return packs


def collect_decisions(root: Path, ref: str | None, pattern: str) -> dict[str, list[dict[str, str]]]:
    """CT decision records, keyed by module code. Only records with the REP-001 front matter count."""
    decisions: dict[str, list[dict[str, str]]] = {}
    for path in list_files(root, ref, pattern):
        fields = front_matter(read_file(root, ref, path))
        code = module_code_of(path, fields) if fields.get("module") else None
        if not code:
            continue
        level = fields.get("evidence_level", "")
        record = {
            "path": path,
            "work_package": fields.get("work_package", ""),
            "evidence_level": level if level in EVIDENCE_LEVELS else "",
            "decision": fields.get("decision", ""),
            "commit": fields.get("commit", ""),
            "scope": fields.get("scope", ""),
            "next_missing": fields.get("next_missing", ""),
        }
        decisions.setdefault(code, []).append(record)
    return decisions


def behind_main(root: Path, ref: str | None) -> str:
    target = ref or "HEAD"
    behind = run(["git", "rev-list", "--count", f"{target}..origin/main"], root).strip()
    ahead = run(["git", "rev-list", "--count", f"origin/main..{target}"], root).strip()
    if not behind:
        return "ölçülemedi (origin/main yok mu?)"
    return f"{behind} commit geride, {ahead} commit ileride"


def cell(value: str) -> str:
    return value.replace("|", "\\|") if value else "kayıt yok"


def main() -> int:
    parser = argparse.ArgumentParser(description="REP-001 durum tablosu")
    parser.add_argument("--packs", default="execution/domains/*/module-packs/*.md",
                        help="module pack glob'u (varsayılan: tüm domain paketleri)")
    parser.add_argument("--records", default="docs/records/audits/*/*.md",
                        help="CT karar kaydı glob'u")
    parser.add_argument("--ref", default=None,
                        help="çalışma ağacı yerine bu git ref'inden oku (ör. origin/feature/mvp6-logistics)")
    args = parser.parse_args()

    root = repo_root(Path.cwd())
    packs = collect_packs(root, args.ref, args.packs)
    decisions = collect_decisions(root, args.ref, args.records)

    if not packs:
        print(f"Bu glob hiçbir module pack bulmadı: {args.packs}", file=sys.stderr)
        return 1

    source = args.ref or "çalışma ağacı"
    print(f"Kaynak: {source} · ana dala göre: {behind_main(root, args.ref)}")
    print(f"Paketler: {args.packs} · karar kayıtları: {args.records}")
    print()
    print("| Modül | Paket durumu | Kanıt seviyesi | Kanıtın yeri | CT kararı | Sıradaki tek eksik |")
    print("|---|---|---|---|---|---|")

    for code in sorted(packs):
        pack = packs[code]
        records = decisions.get(code, [])
        best = max(records, key=lambda r: r["evidence_level"], default=None)
        if best:
            where = f"`{best['path']}@{best['commit']}`" if best["commit"] else f"`{best['path']}` (commit yok)"
            decision = best["decision"] if best["decision"] in DECISIONS else ""
            print(f"| {code} | {cell(pack['status'])} | {cell(best['evidence_level'])} | {where} "
                  f"| {cell(decision)} | {cell(best['next_missing'])} |")
        else:
            print(f"| {code} | {cell(pack['status'])} | kayıt yok | kayıt yok | kayıt yok | kayıt yok |")

    print()
    counted = sum(1 for code in packs if decisions.get(code))
    print(f"Toplam {len(packs)} paket; CT karar kaydı olan {counted}. "
          "Yüzde yazacaksan bu tabloyla birlikte yaz (REP-001 §2).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
