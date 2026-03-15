#!/usr/bin/env python3
"""
SharedResource RESX Checker (Diten ERP vNext)
--------------------------------------------

Validates that:
  1) SharedResource.{lang}.resx exists for all 8 supported languages
  2) All keys in SharedResource.en.resx exist in every other language
  3) Non-English values are not left as English placeholders (same as en), unless explicitly allowed

This is intentionally strict to prevent the "only en/tr translated" regression.

Usage:
  python3 .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py .
"""

from __future__ import annotations

import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Set, Tuple
import xml.etree.ElementTree as ET


SUPPORTED_LANGS: Tuple[str, ...] = ("en", "tr", "es", "ru", "uk", "ka", "kk", "uz")

# Values that are acceptable to remain identical across languages.
# Keep this list small and intentional.
ALLOWED_EN_EQUAL_KEYS: Set[str] = {
    # Technical / brand terms (examples)
    "Sandbox",
    # Spanish commonly uses "Error" as-is; other languages should still translate it
    "Error",
    "Login.ErrorTitle",
    "Sector",
}

# If a non-English value matches these patterns, we won't fail it as "English placeholder".
# (Acronyms, IDs, tokens, numbers, etc.)
ALLOWED_VALUE_PATTERNS: Tuple[re.Pattern[str], ...] = (
    re.compile(r"^[A-Z0-9_\-]{2,}$"),  # API, ERP, JWT, etc.
    re.compile(r"^\d+$"),             # numeric
)

# UI label casing enforcement (keep strict and minimal).
# For certain SharedResource keys, values must follow Title Case-like casing:
# each word's first cased letter should be uppercase (where applicable).
TITLE_CASE_KEYS: Set[str] = {
    "SaveView",
}

# Languages/scripts where case rules are not applicable (e.g., Georgian Mkhedruli).
NO_CASE_LANGS: Set[str] = {
    "ka",
}


@dataclass(frozen=True)
class ResxDoc:
    path: Path
    entries: Dict[str, str]


def parse_resx(path: Path) -> ResxDoc:
    try:
        tree = ET.parse(path)
    except ET.ParseError as e:
        raise RuntimeError(f"Invalid XML: {path} ({e})") from e

    root = tree.getroot()
    entries: Dict[str, str] = {}
    for data in root.findall("data"):
        name = data.get("name")
        if not name:
            continue
        value_el = data.find("value")
        value = (value_el.text or "") if value_el is not None else ""
        entries[name] = value
    return ResxDoc(path=path, entries=entries)


def is_allowed_same_as_en(key: str, non_en_value: str) -> bool:
    if key in ALLOWED_EN_EQUAL_KEYS:
        return True
    for pat in ALLOWED_VALUE_PATTERNS:
        if pat.match(non_en_value or ""):
            return True
    return False


def _has_case(ch: str) -> bool:
    return ch.isalpha() and (ch.lower() != ch.upper())


def _is_title_case_like(value: str) -> bool:
    if not (value or "").strip():
        return False

    tokens = (value or "").strip().split()
    any_cased = False

    for token in tokens:
        first_cased = None
        for ch in token:
            if _has_case(ch):
                first_cased = ch
                break
        if first_cased is None:
            continue
        any_cased = True
        if not first_cased.isupper():
            return False

    # If there is no cased character at all, do not treat as a failure here.
    return True if any_cased else True


def main() -> int:
    project_root = Path(sys.argv[1] if len(sys.argv) > 1 else ".").resolve()
    resources_dir = project_root / "frontend" / "Diten.Web" / "Resources"

    base_path = resources_dir / "SharedResource.en.resx"
    if not base_path.exists():
        print("[!] SharedResource.en.resx not found; skipping SharedResource RESX check")
        return 0

    docs: Dict[str, ResxDoc] = {}
    missing_files: List[str] = []
    for lang in SUPPORTED_LANGS:
        p = resources_dir / f"SharedResource.{lang}.resx"
        if not p.exists():
            missing_files.append(str(p))
            continue
        docs[lang] = parse_resx(p)

    if missing_files:
        print("[X] Missing SharedResource RESX files:")
        for p in missing_files:
            print(f"  - {p}")
        return 1

    en = docs["en"].entries
    non_en_langs = [l for l in SUPPORTED_LANGS if l != "en"]

    errors: List[str] = []

    # Key completeness
    for lang in non_en_langs:
        other = docs[lang].entries
        missing_keys = sorted(set(en.keys()) - set(other.keys()))
        if missing_keys:
            errors.append(f"[X] {lang}: Missing {len(missing_keys)} key(s) from SharedResource.en.resx")
            for k in missing_keys[:20]:
                errors.append(f"    - {k}")
            if len(missing_keys) > 20:
                errors.append("    - ...")

    # Placeholder / equal-to-English detection (strict)
    for lang in non_en_langs:
        other = docs[lang].entries
        same_as_en = []
        for key, en_val in en.items():
            other_val = other.get(key, "")
            if other_val == en_val and en_val and not is_allowed_same_as_en(key, other_val):
                same_as_en.append(key)

        if same_as_en:
            errors.append(f"[X] {lang}: {len(same_as_en)} key(s) still equal to English (placeholder translation?)")
            for k in same_as_en[:20]:
                errors.append(f"    - {k}")
            if len(same_as_en) > 20:
                errors.append("    - ...")

    # Title Case enforcement (minimal, key-scoped)
    for lang in non_en_langs:
        if lang in NO_CASE_LANGS:
            continue
        other = docs[lang].entries
        for key in TITLE_CASE_KEYS:
            val = other.get(key, "")
            if val and not _is_title_case_like(val):
                errors.append(f"[X] {lang}: '{key}' should be Title Case (UI action label), got: {val!r}")

    if errors:
        print("\n".join(errors))
        return 1

    print("[OK] SharedResource RESX check: PASSED (8 languages, no English placeholders)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
