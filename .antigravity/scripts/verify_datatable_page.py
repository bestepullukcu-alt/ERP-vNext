#!/usr/bin/env python3
"""
DataTable Page Verifier (vNext)
===============================

Static checks to enforce the "Golden DataTable" contract (SampleModule baseline):
- Index.cshtml structure markers exist (Filter partial, skeleton, offcanvas)
- window.L10n bridge uses payload partial + loader JS and includes required keys
- index.js uses DtDefaults + DataTables v2 constructor
- Quick View is wired via event delegation (.js-quick-view) (no inline onclick)

Usage:
  python3 .antigravity/scripts/verify_datatable_page.py . --area MDM --module SampleModule
  python3 .antigravity/scripts/verify_datatable_page.py . --area MDM --module SampleModule
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


def check_shared_css_not_embedded(index_path: Path, index_html: str) -> List[Check]:
    checks: List[Check] = []
    checks.append(
        check_not_contains(
            index_path,
            index_html,
            re.compile(r"@section\s+Styles[\s\S]*#inlineFilterHost", re.IGNORECASE),
            "Index.cshtml does not embed inline filter CSS in @section Styles",
            "Reusable #inlineFilterHost styles belong in backbone-custom.css, not page-level @section Styles",
        )
    )
    checks.append(
        check_not_contains(
            index_path,
            index_html,
            re.compile(r"@section\s+Styles[\s\S]*\.dt-layout-end", re.IGNORECASE),
            "Index.cshtml does not embed toolbar CSS in @section Styles",
            "Reusable .dt-layout-end toolbar styles belong in backbone-custom.css, not page-level @section Styles",
        )
    )
    return checks

def check_inline_filter_host_alignment(filter_html: str, js_text: str, filter_path: Path, js_path: Path) -> Check:
    """
    Inline filter host alignment rule:
    - Inline filter host must use px-6 (project standard).
    - Implementation may be either:
      a) _Filter.cshtml adds class="... px-6 ...", OR
      b) index.js adds px-6 via host.classList.add('px-6') after mounting.
    """
    host_has_px6 = bool(
        re.search(
            r"<div[^>]*\bid\s*=\s*\"inlineFilterHost\"[^>]*\bclass\s*=\s*\"[^\"]*\bpx-6\b",
            filter_html,
            re.IGNORECASE,
        )
    )

    js_adds_px6 = ("inlineFilterHost" in js_text) and bool(
        re.search(r"classList\.add\(\s*[^)]*['\"]px-6['\"]", js_text)
    )

    if host_has_px6 or js_adds_px6:
        return Check("Inline filter host aligns with px-6", True)

    return Check(
        "Inline filter host aligns with px-6",
        False,
        f"Expected px-6 on #inlineFilterHost (either in {filter_path} or added in {js_path}).",
    )


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

    return [(k, re.compile(rf"(?<![A-Za-z0-9_]){re.escape(k)}\s*=")) for k in ordered]


def main() -> int:
    parser = argparse.ArgumentParser(description="Verify vNext DataTable page contract (static checks).")
    parser.add_argument("project", help="Repo root (e.g. .)")
    parser.add_argument("--area", default="MDM", help="Area folder under Views/ and assets/js/ (default: MDM)")
    parser.add_argument("--module", required=True, help="Module folder name (case-sensitive) (e.g. SampleModule)")

    args = parser.parse_args()

    root = Path(args.project).resolve()
    if not root.exists():
        print(f"[FAIL] Project path does not exist: {root}")
        return 1

    area = args.area
    module = args.module

    index_cshtml = root / "frontend" / "Diten.Web" / "Views" / area / module / "Index.cshtml"
    filter_cshtml = root / "frontend" / "Diten.Web" / "Views" / area / module / "_Filter.cshtml"
    index_l10n_partial = root / "frontend" / "Diten.Web" / "Views" / area / module / "_IndexL10n.cshtml"
    index_js = root / "frontend" / "Diten.Web" / "wwwroot" / "assets" / "js" / area / module / "index.js"
    index_l10n_js = root / "frontend" / "Diten.Web" / "wwwroot" / "assets" / "js" / area / module / "index.l10n.js"
    dt_defaults_js = root / "frontend" / "Diten.Web" / "wwwroot" / "assets" / "js" / "dt-defaults.js"
    backbone_custom_css = root / "frontend" / "Diten.Web" / "wwwroot" / "assets" / "css" / "backbone-custom.css"

    checks: List[Check] = []

    checks.append(check_file_exists(index_cshtml, "Index.cshtml exists"))
    checks.append(check_file_exists(filter_cshtml, "_Filter.cshtml exists"))
    checks.append(check_file_exists(index_l10n_partial, "_IndexL10n.cshtml exists"))
    checks.append(check_file_exists(index_js, "index.js exists"))
    checks.append(check_file_exists(index_l10n_js, "index.l10n.js exists"))
    checks.append(check_file_exists(dt_defaults_js, "dt-defaults.js exists"))
    checks.append(check_file_exists(backbone_custom_css, "backbone-custom.css exists"))

    # Stop early if core files missing (avoid confusing follow-up errors).
    if any(not c.ok for c in checks[:7]):
        print_report(checks)
        return 1

    index_html = read_text(index_cshtml)
    filter_html = read_text(filter_cshtml)
    index_l10n_html = read_text(index_l10n_partial)
    js_text = read_text(index_js)
    index_l10n_js_text = read_text(index_l10n_js)
    dt_defaults_text = read_text(dt_defaults_js)
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
            re.compile(r"id\s*=\s*\"inlineFilterHost\""),
            "_Filter.cshtml has #inlineFilterHost",
            "Missing inline filter host wrapper (id=\"inlineFilterHost\")",
        )
    )
    checks.append(
        check_contains(
            filter_cshtml,
            filter_html,
            re.compile(r"id\s*=\s*\"inlineFilterCollapse\""),
            "_Filter.cshtml has #inlineFilterCollapse",
            "Missing inline filter collapse container (id=\"inlineFilterCollapse\")",
        )
    )
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
            "Inline filter host should not use mx-*; use px-6 (project standard)",
        )
    )
    checks.append(check_inline_filter_host_alignment(filter_html, js_text, filter_cshtml, index_js))

    # Save View + filter apply/reset contract (v2 pages only)
    # - Reset must take effect immediately (no "Reset then Apply" behavior) -> prevent native form reset conflicts.
    # - Save View visibility is based on applied/effective state: filter selections alone must not toggle Save View.
    if is_v2 and re.search(r"\bdt-save-filter-btn\b", js_text):
        checks.append(
            check_contains(
                index_js,
                js_text,
                re.compile(r"getElementById\(\s*['\"]btnFilterApply['\"]\s*\)"),
                "index.js references #btnFilterApply",
                "Missing Apply button wiring (btnFilterApply)",
            )
        )
        checks.append(
            check_contains(
                index_js,
                js_text,
                re.compile(r"getElementById\(\s*['\"]btnFilterReset['\"]\s*\)"),
                "index.js references #btnFilterReset",
                "Missing Reset button wiring (btnFilterReset)",
            )
        )
        checks.append(
            check_contains(
                index_js,
                js_text,
                re.compile(r"btnFilterApply[\s\S]*addEventListener\(\s*['\"]click['\"][\s\S]*setSaveFilterVisible\s*\(", re.IGNORECASE),
                "Apply click updates Save View visibility",
                "Expected Apply handler to sync Save View visibility based on applied state",
            )
        )
        checks.append(
            check_contains(
                index_js,
                js_text,
                re.compile(r"btnFilterReset[\s\S]*addEventListener\(\s*['\"]click['\"][\s\S]*preventDefault\s*\(", re.IGNORECASE),
                "Reset click prevents native form reset",
                "Expected Reset handler to call event.preventDefault() to avoid native reset overriding programmatic restore",
            )
        )
        checks.append(
            check_contains(
                index_js,
                js_text,
                re.compile(r"btnFilterReset[\s\S]*addEventListener\(\s*['\"]click['\"][\s\S]*setSaveFilterVisible\s*\(", re.IGNORECASE),
                "Reset click updates Save View visibility",
                "Expected Reset handler to sync Save View visibility after applying reset",
            )
        )
        checks.append(
            check_not_contains(
                index_js,
                js_text,
                re.compile(r"change\.saveFilter[\s\S]*setSaveFilterVisible\s*\(", re.IGNORECASE),
                "Filter control change does not toggle Save View",
                "Save View must not be toggled on staged filter changes; it should update after Apply/Reset",
            )
        )

    # Global DT defaults layout padding contract (prevents toolbar padding drift)
    checks.append(
        check_contains(
            dt_defaults_js,
            dt_defaults_text,
            re.compile(r"rowClass:\s*['\"]row\s+px-3\s+my-0\s+justify-content-between['\"]"),
            "dt-defaults.js uses px-3 on topStart rowClass",
            "Expected buildLayout().topStart.rowClass to be 'row px-3 my-0 justify-content-between'",
        )
    )
    checks.append(
        check_contains(
            dt_defaults_js,
            dt_defaults_text,
            re.compile(r"rowClass:\s*['\"]row\s+px-3\s+justify-content-between['\"]"),
            "dt-defaults.js uses px-3 on bottomStart rowClass",
            "Expected buildLayout().bottomStart.rowClass to be 'row px-3 justify-content-between'",
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
    checks.append(
        check_contains(
            backbone_custom_css,
            css_text,
            re.compile(r"#inlineFilterHost\s+\.dt-filter-bar\s+\.filter-chip\s+\.select2-selection--single", re.MULTILINE),
            "backbone-custom.css has shared inline filter Select2 styling",
            "Missing centralized inline filter Select2 rules in backbone-custom.css",
        )
    )
    if ("form-select-sm" in css_text) or ("selectionCssClass" in js_text):
        checks.append(Check("Inline filter styling follows form-select-sm standard", True))
    else:
        checks.append(
            Check(
                "Inline filter styling follows form-select-sm standard",
                False,
                "Missing form-select-sm-aligned inline filter styling contract in backbone-custom.css and/or index.js",
            )
        )
    checks.extend(check_shared_css_not_embedded(index_cshtml, index_html))

    # L10n bridge sanity
    checks.append(
        check_contains(
            index_cshtml,
            index_html,
            re.compile(r"<partial\s+name\s*=\s*\"_IndexL10n\"\s*/>"),
            "Index.cshtml includes <partial name=\"_IndexL10n\" />",
            "Missing _IndexL10n partial include",
        )
    )
    checks.append(
        check_contains(
            index_cshtml,
            index_html,
            re.compile(re.escape(f"~/assets/js/{area}/{module}/index.l10n.js")),
            f"Index.cshtml includes script: ~/assets/js/{area}/{module}/index.l10n.js",
            "Missing/incorrect index.l10n.js include path",
        )
    )
    checks.append(
        check_not_contains(
            index_cshtml,
            index_html,
            re.compile(r"window\.L10n\.[A-Za-z0-9_]+\s*="),
            "Index.cshtml does not inline-assign window.L10n keys",
            "Found legacy inline window.L10n assignment block in Index.cshtml",
        )
    )
    checks.append(
        check_contains(
            index_l10n_partial,
            index_l10n_html,
            re.compile(r"<script[^>]*type\s*=\s*\"application/json\""),
            "_IndexL10n.cshtml renders application/json payload",
            "Missing application/json payload in _IndexL10n.cshtml",
        )
    )
    checks.append(
        check_contains(
            index_l10n_js,
            index_l10n_js_text,
            re.compile(r"JSON\.parse\(\s*payload\.textContent\s*\|\|\s*['\"]\{\}['\"]\s*\)"),
            "index.l10n.js parses payload JSON",
            "Missing JSON.parse(payload.textContent || '{}') in index.l10n.js",
        )
    )
    checks.append(
        check_contains(
            index_l10n_js,
            index_l10n_js_text,
            re.compile(r"window\.L10n\s*=\s*Object\.assign\("),
            "index.l10n.js merges payload into window.L10n",
            "Missing Object.assign merge into window.L10n in index.l10n.js",
        )
    )
    for key, pat in compile_required_l10n_keys(is_v2):
        checks.append(
            check_contains(
                index_l10n_partial,
                index_l10n_html,
                pat,
                f"_IndexL10n.cshtml provides {key}",
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
