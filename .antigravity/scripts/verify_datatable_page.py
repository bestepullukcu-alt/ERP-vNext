#!/usr/bin/env python3
"""
DataTable Page Verifier (vNext)
===============================

Static checks to enforce the "Golden DataTable" contract (LegalEntities baseline):
- Index.cshtml structure markers exist (Filter partial, skeleton, offcanvas)
- window.L10n bridge includes required keys (no raw keys in JS)
- index.js uses DtDefaults + DataTables v2 constructor
- Quick View is wired via event delegation (.js-quick-view) (no inline onclick)

Usage:
  python3 .antigravity/scripts/verify_datatable_page.py . --area MDM --module LegalEntities
  python3 .antigravity/scripts/verify_datatable_page.py . --area MDM --module Countries
"""

from __future__ import annotations

import argparse
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import List, Pattern, Tuple


@dataclass(frozen=True)
class Check:
    name: str
    ok: bool
    details: str = ""


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def has(pattern: Pattern[str], text: str) -> bool:
    return pattern.search(text) is not None


def check_file_exists(path: Path, label: str) -> Check:
    if path.exists() and path.is_file():
        return Check(label, True, str(path))
    return Check(label, False, f"Missing: {path}")


def check_contains(path: Path, text: str, pattern: Pattern[str], label: str, hint: str) -> Check:
    if has(pattern, text):
        return Check(label, True)
    return Check(label, False, f"{hint} (file: {path})")


def check_not_contains(path: Path, text: str, pattern: Pattern[str], label: str, hint: str) -> Check:
    if has(pattern, text):
        return Check(label, False, f"{hint} (file: {path})")
    return Check(label, True)


def print_report(checks: List[Check]) -> None:
    failed = [c for c in checks if not c.ok]
    passed = [c for c in checks if c.ok]

    print("\nGolden DataTable Verify Report")
    print("=" * 32)
    print(f"Passed: {len(passed)}")
    print(f"Failed: {len(failed)}\n")

    for c in checks:
        status = "[PASS]" if c.ok else "[FAIL]"
        line = f"{status} {c.name}"
        print(line)
        if c.details and not c.ok:
            print(f"  - {c.details}")

    if failed:
        print("\nResult: FAIL")
    else:
        print("\nResult: PASS")


def compile_required_l10n_keys(is_v2: bool) -> List[Tuple[str, Pattern[str]]]:
    """
    Required L10n bridge keys for DataTable pages.

    v1 (legacy): minimal contract.
    v2 (data-dt-standard="v2"): toolbar/filter vocabulary keys are mandatory.
    """
    keys = [
        # Core shared status/actions
        "Active",
        "Passive",
        "Unknown",
        "Actions",
        "Edit",
        "ViewDetails",
        "QuickView",
        # Bulk actions / confirm
        "BulkDelete",
        "BulkDeleteConfirm",
        "AreYouSure",
        "Cancel",
    ]

    if is_v2:
        keys.extend(
            [
                # Toolbar + filter vocabulary (v2)
                "Search",
                "Export",
                "Import",
                "Filter",
                "Apply",
                "Reset",
                "ShowAll",
                "SaveView",
                "ColumnVisibility",
                "Status",
            ]
        )

    # Deduplicate while keeping order
    seen = set()
    ordered = []
    for k in keys:
        if k in seen:
            continue
        seen.add(k)
        ordered.append(k)

    return [(k, re.compile(rf"window\.L10n\.{re.escape(k)}\s*=")) for k in ordered]


def main() -> int:
    parser = argparse.ArgumentParser(description="Verify vNext DataTable page contract (static checks).")
    parser.add_argument("project", help="Repo root (e.g. .)")
    parser.add_argument("--area", default="MDM", help="Area folder under Views/ and assets/js/ (default: MDM)")
    parser.add_argument("--module", required=True, help="Module folder name (case-sensitive) (e.g. LegalEntities)")

    args = parser.parse_args()

    root = Path(args.project).resolve()
    if not root.exists():
        print(f"[FAIL] Project path does not exist: {root}")
        return 1

    area = args.area
    module = args.module

    index_cshtml = root / "frontend" / "Diten.Web" / "Views" / area / module / "Index.cshtml"
    filter_cshtml = root / "frontend" / "Diten.Web" / "Views" / area / module / "_Filter.cshtml"
    index_js = root / "frontend" / "Diten.Web" / "wwwroot" / "assets" / "js" / area / module / "index.js"
    backbone_custom_css = root / "frontend" / "Diten.Web" / "wwwroot" / "assets" / "css" / "backbone-custom.css"

    checks: List[Check] = []

    checks.append(check_file_exists(index_cshtml, "Index.cshtml exists"))
    checks.append(check_file_exists(filter_cshtml, "_Filter.cshtml exists"))
    checks.append(check_file_exists(index_js, "index.js exists"))
    checks.append(check_file_exists(backbone_custom_css, "backbone-custom.css exists"))

    # Stop early if core files missing (avoid confusing follow-up errors).
    if any(not c.ok for c in checks[:4]):
        print_report(checks)
        return 1

    index_html = read_text(index_cshtml)
    filter_html = read_text(filter_cshtml)
    js_text = read_text(index_js)
    css_text = read_text(backbone_custom_css)
    is_v2 = bool(re.search(r"data-dt-standard\s*=\s*\"v2\"", index_html))

    checks.append(
        check_contains(
            index_cshtml,
            index_html,
            re.compile(r"<partial\s+name\s*=\s*\"_Filter\"\s*/>"),
            "Index.cshtml includes <partial name=\"_Filter\" />",
            "Missing filter partial include",
        )
    )
    checks.append(
        check_contains(
            index_cshtml,
            index_html,
            re.compile(r"Layout\s*=\s*\"_LayoutBackbone\"\s*;"),
            "Index.cshtml uses _LayoutBackbone",
            "Missing Layout = \"_LayoutBackbone\";",
        )
    )
    checks.append(
        check_contains(
            index_cshtml,
            index_html,
            re.compile(r"id\s*=\s*\"skeleton-loader\""),
            "Index.cshtml has #skeleton-loader",
            "Missing skeleton loader (id=\"skeleton-loader\")",
        )
    )
    checks.append(
        check_contains(
            index_cshtml,
            index_html,
            re.compile(r"id\s*=\s*\"offcanvasDetailsPreview\""),
            "Index.cshtml has #offcanvasDetailsPreview",
            "Missing offcanvas (id=\"offcanvasDetailsPreview\")",
        )
    )

    checks.append(
        check_contains(
            index_cshtml,
            index_html,
            re.compile(r"Localizer\[\s*\"PageDescription\"\s*\]"),
            "Index.cshtml shows @Localizer[\"PageDescription\"]",
            "Missing PageDescription under the page title (required on non-breadcrumb pages like Index)",
        )
    )

    # Inline filter spacing contract
    checks.append(
        check_contains(
            filter_cshtml,
            filter_html,
            re.compile(r"class\s*=\s*\"[^\"]*\bpt-0\b[^\"]*\bpb-3\b[^\"]*\""),
            "_Filter.cshtml uses pt-0 pb-3 wrapper",
            "Expected filter wrapper spacing class 'pt-0 pb-3' (pt-2 is not allowed)",
        )
    )
    checks.append(
        check_not_contains(
            filter_cshtml,
            filter_html,
            re.compile(r"\bpt-2\b"),
            "_Filter.cshtml does not use pt-2",
            "Found pt-2 in filter wrapper (should be pt-0)",
        )
    )

    # Inline filter host alignment contract (avoid mx-* margins)
    checks.append(
        check_not_contains(
            index_js,
            js_text,
            re.compile(r"classList\.add\(\s*['\"]mx-"),
            "index.js does not add mx-* to inlineFilterHost",
            "Inline filter host should not use mx-*; use px-3 to align with toolbar padding",
        )
    )

    # Global CSS guards (toolbar responsive + badge safe area)
    checks.append(
        check_contains(
            backbone_custom_css,
            css_text,
            re.compile(r"@media\s+screen\s+and\s+\(max-width:\s*991\.98px\)"),
            "backbone-custom.css has MOD-0022 media query",
            "Missing responsive toolbar media query (max-width: 991.98px)",
        )
    )
    checks.append(
        check_contains(
            backbone_custom_css,
            css_text,
            re.compile(r"div\.dt-container\s*>\s*\.row:first-child\s*\{[^}]*padding-top\s*:", re.DOTALL),
            "Responsive toolbar reserves top safe-area (padding-top)",
            "Missing toolbar top safe-area (prevents badge clipping on mobile/tablet)",
        )
    )
    checks.append(
        check_contains(
            backbone_custom_css,
            css_text,
            re.compile(r"\.dt-export-collection-btn\b[\s\S]*?min-block-size\s*:", re.MULTILINE),
            "Export button height aligned (dt-export-collection-btn min-block-size)",
            "Missing dt-export-collection-btn min-block-size alignment rule (prevents 'short' Export button on mobile)",
        )
    )

    # L10n bridge sanity
    checks.append(
        check_contains(
            index_cshtml,
            index_html,
            re.compile(r"window\.L10n\s*=\s*window\.L10n\s*\|\|\s*\{\s*\}"),
            "Index.cshtml initializes window.L10n",
            "Missing window.L10n initialization block",
        )
    )
    for key, pat in compile_required_l10n_keys(is_v2):
        checks.append(
            check_contains(
                index_cshtml,
                index_html,
                pat,
                f"Index.cshtml bridges window.L10n.{key}",
                f"Missing L10n key: {key}",
            )
        )

    # v2: Table identity marker (prevents multi-table storage key collisions)
    if is_v2:
        checks.append(
            check_contains(
                index_cshtml,
                index_html,
                re.compile(r"<table[^>]*(?:id\s*=\s*\"[^\"]+\")[^>]*data-dt-standard\s*=\s*\"v2\"|<table[^>]*data-dt-standard\s*=\s*\"v2\"[^>]*id\s*=\s*\"[^\"]+\"", re.IGNORECASE),
                "Index.cshtml has <table id=\"...\" data-dt-standard=\"v2\">",
                "Missing v2 marker and/or table id (required: <table id=\"...\" data-dt-standard=\"v2\">)",
            )
        )

    # Script include should point to the module path (case-sensitive)
    expected_script = f"~/assets/js/{area}/{module}/index.js"
    checks.append(
        check_contains(
            index_cshtml,
            index_html,
            re.compile(re.escape(expected_script)),
            f"Index.cshtml includes script: {expected_script}",
            "Missing/incorrect index.js include path",
        )
    )

    # Anti-patterns in Index.cshtml
    checks.append(
        check_not_contains(
            index_cshtml,
            index_html,
            re.compile(r"onclick\s*="),
            "Index.cshtml has no inline onclick=",
            "Inline onclick= found (should be event delegation in index.js)",
        )
    )
    checks.append(
        check_not_contains(
            index_cshtml,
            index_html,
            re.compile(r"populateOffcanvas\s*\("),
            "Index.cshtml does not define/call populateOffcanvas(...)",
            "populateOffcanvas(...) found in Index.cshtml (must live in index.js module scope)",
        )
    )

    # JS contract
    checks.append(
        check_contains(
            index_js,
            js_text,
            re.compile(r"new\s+DataTable\s*\("),
            "index.js uses DataTables v2 constructor (new DataTable(...))",
            "Missing new DataTable(...)",
        )
    )
    checks.append(
        check_contains(
            index_js,
            js_text,
            re.compile(r"window\.DtDefaults\.create\s*\("),
            "index.js uses window.DtDefaults.create(...)",
            "Missing window.DtDefaults.create(...)",
        )
    )
    checks.append(
        check_contains(
            index_js,
            js_text,
            re.compile(r"DtDefaults\.exportButtons\s*\("),
            "index.js uses DtDefaults.exportButtons(...)",
            "Missing DtDefaults.exportButtons(...)",
        )
    )
    checks.append(
        check_contains(
            index_js,
            js_text,
            re.compile(r"\.js-quick-view"),
            "index.js includes .js-quick-view selector",
            "Missing .js-quick-view wiring for Quick View",
        )
    )
    checks.append(
        check_contains(
            index_js,
            js_text,
            re.compile(r"closest\(\s*['\\\"]\.js-quick-view['\\\"]\s*\)"),
            "index.js uses event delegation (closest('.js-quick-view'))",
            "Quick View must use event delegation (closest('.js-quick-view'))",
        )
    )
    checks.append(
        check_contains(
            index_js,
            js_text,
            re.compile(r"getAuthHeaders\s*="),
            "index.js defines getAuthHeaders()",
            "Missing getAuthHeaders() helper",
        )
    )
    checks.append(
        check_contains(
            index_js,
            js_text,
            re.compile(r"headers\s*:\s*getAuthHeaders\s*\(\s*\)"),
            "index.js uses getAuthHeaders() in fetch headers",
            "Missing headers: getAuthHeaders() usage",
        )
    )
    checks.append(
        check_not_contains(
            index_js,
            js_text,
            re.compile(r"\.(?:DataTable|dataTable)\s*\("),
            "index.js does not use jQuery DataTable plugin (.(DataTable|dataTable)(...))",
            "Found jQuery DataTable plugin usage; must use DataTables v2 constructor",
        )
    )

    print_report(checks)
    return 1 if any(not c.ok for c in checks) else 0


if __name__ == "__main__":
    raise SystemExit(main())
