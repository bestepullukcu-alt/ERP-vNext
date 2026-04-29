#!/usr/bin/env python3
"""
DataTable Page Verifier (vNext)
===============================

Static checks to enforce the Golden DataTable contract:
- slim: GoldenReferenceSlim (<=8 form fields, create/edit offcanvas)
- compact: GoldenReferenceCompact (>8 form fields, full create/edit/details pages)
- Index.cshtml structure markers exist (Filter partial, skeleton, offcanvas)
- window.L10n bridge uses payload partial + loader JS and includes required keys
- index.js uses DtDefaults + DataTables v2 constructor
- Quick View is wired via event delegation (.js-quick-view) (no inline onclick)
- API profile is enforced:
  - proxy: Platform/admin MVC default, browser JS calls /{Area}/{Module}/api
  - direct-gateway: browser-safe shells use window.API.{service}
- DataTable JS never reads HttpOnly cookies or constructs Bearer tokens

Usage:
  python3 .antigravity/scripts/verify_datatable_page.py . --area Platform --module ModuleCatalog --reference compact --api-profile proxy
  python3 .antigravity/scripts/verify_datatable_page.py . --area DevEnablement --module GoldenReferenceSlim --reference slim --api-profile direct-gateway
"""

from __future__ import annotations

import argparse
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Optional, Pattern, Tuple


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
    - Inline filter host must use px-3 (project standard).
    - Implementation may be either:
      a) _Filter.cshtml adds class="... px-3 ...", OR
      b) index.js adds px-3 via host.classList.add('px-3') after mounting.
    """
    host_has_px3 = bool(
        re.search(
            r"<div[^>]*\bid\s*=\s*\"inlineFilterHost\"[^>]*\bclass\s*=\s*\"[^\"]*\bpx-3\b",
            filter_html,
            re.IGNORECASE,
        )
    )

    js_adds_px3 = ("inlineFilterHost" in js_text) and bool(
        re.search(r"classList\.add\(\s*[^)]*['\"]px-3['\"]", js_text)
    )

    if host_has_px3 or js_adds_px3:
        return Check("Inline filter host aligns with px-3", True)

    return Check(
        "Inline filter host aligns with px-3",
        False,
        f"Expected px-3 on #inlineFilterHost (either in {filter_path} or added in {js_path}).",
    )


def extract_section_keys(text: str) -> List[str]:
    """
    Extract localized section heading keys from card/section blocks.

    This intentionally checks section/card information architecture rather than
    field-level markup. Compact Create/Edit and Details must expose the same
    logical sections in the same order.
    """
    section_keys: List[str] = []
    section_blocks = re.findall(r"<section\b[\s\S]*?</section>", text, re.IGNORECASE)
    heading_pattern = re.compile(
        r"<h[1-6][^>]*>[\s\S]*?(SharedLocalizer|Localizer)\[\s*\"([^\"]+)\"\s*\][\s\S]*?</h[1-6]>",
        re.IGNORECASE,
    )

    for block in section_blocks:
        match = heading_pattern.search(block)
        if not match:
            continue
        section_keys.append(f"{match.group(1)}:{match.group(2)}")

    return section_keys


def check_compact_form_details_section_parity(form_path: Path, form_text: str, details_path: Path, details_text: str) -> Check:
    form_sections = extract_section_keys(form_text)
    details_sections = extract_section_keys(details_text)

    if not form_sections:
        return Check(
            "Compact _Form.cshtml exposes logical section/card headings",
            False,
            f"No localized section headings found in {form_path}",
        )

    if not details_sections:
        return Check(
            "Compact Details.cshtml exposes logical section/card headings",
            False,
            f"No localized section headings found in {details_path}",
        )

    if form_sections == details_sections:
        return Check("Compact _Form.cshtml matches Details.cshtml section/card map", True)

    return Check(
        "Compact _Form.cshtml matches Details.cshtml section/card map",
        False,
        "Expected _Form.cshtml and Details.cshtml to use the same localized section headings in the same order. "
        f"Form: {form_sections}; Details: {details_sections}",
    )


VALUE_TYPES_REQUIRING_NULLABLE_WHEN_OPTIONAL = {
    "byte",
    "short",
    "int",
    "long",
    "float",
    "double",
    "decimal",
    "DateOnly",
    "DateTime",
    "DateTimeOffset",
    "TimeOnly",
    "TimeSpan",
}


def extract_form_model_class(form_text: str) -> Optional[str]:
    match = re.search(r"@model\s+([A-Za-z0-9_.<>]+)", form_text)
    if not match:
        return None
    return match.group(1).split(".")[-1]


def find_model_source(root: Path, class_name: str) -> Tuple[Optional[Path], str]:
    models_root = root / "frontend" / "Diten.Web" / "Models"
    if not models_root.exists():
        return None, ""

    class_pattern = re.compile(rf"\bclass\s+{re.escape(class_name)}\b")
    for path in models_root.rglob("*.cs"):
        text = read_text(path)
        if class_pattern.search(text):
            return path, text

    return None, ""


def parse_model_properties(model_text: str) -> Dict[str, Tuple[str, str]]:
    properties: Dict[str, Tuple[str, str]] = {}
    prop_pattern = re.compile(
        r"(?P<attrs>(?:\s*\[[^\]]+\]\s*)*)\s*public\s+(?P<type>[A-Za-z0-9_?<>.]+)\s+(?P<name>[A-Za-z0-9_]+)\s*\{",
        re.MULTILINE,
    )

    for match in prop_pattern.finditer(model_text):
        properties[match.group("name")] = (match.group("type"), match.group("attrs") or "")

    return properties


def is_nullable_type(type_name: str) -> bool:
    return type_name.endswith("?") or type_name.startswith("Nullable<")


def is_value_type_that_requires_nullable_when_optional(type_name: str) -> bool:
    clean = type_name.replace("?", "")
    return clean in VALUE_TYPES_REQUIRING_NULLABLE_WHEN_OPTIONAL


def label_has_required_marker(form_text: str, field_name: str) -> bool:
    label_pattern = re.compile(
        rf"<label\b[^>]*asp-for\s*=\s*\"{re.escape(field_name)}\"[^>]*>[\s\S]*?</label>",
        re.IGNORECASE,
    )
    match = label_pattern.search(form_text)
    return bool(match and "text-danger" in match.group(0))


def extract_bound_field_tags(form_text: str) -> List[Tuple[str, str]]:
    field_tags: List[Tuple[str, str]] = []
    tag_pattern = re.compile(r"<(input|select|textarea)\b[^>]*asp-for\s*=\s*\"([A-Za-z0-9_]+)\"[^>]*>", re.IGNORECASE)

    for match in tag_pattern.finditer(form_text):
        field_tags.append((match.group(2), match.group(0)))

    return field_tags


def input_type(tag: str) -> str:
    match = re.search(r"\btype\s*=\s*\"([^\"]+)\"", tag, re.IGNORECASE)
    return match.group(1).lower() if match else ""


def check_form_required_contract(root: Path, form_path: Path, form_text: str) -> List[Check]:
    checks: List[Check] = []
    model_class = extract_form_model_class(form_text)
    if not model_class:
        return [
            Check(
                "Form declares a strongly typed ViewModel",
                False,
                f"Missing @model declaration in {form_path}",
            )
        ]

    model_path, model_text = find_model_source(root, model_class)
    if not model_path:
        return [
            Check(
                "Form ViewModel source file is discoverable",
                False,
                f"Could not find class {model_class} under frontend/Diten.Web/Models",
            )
        ]

    properties = parse_model_properties(model_text)
    optional_value_type_violations: List[str] = []
    required_marker_violations: List[str] = []

    for field_name, tag in extract_bound_field_tags(form_text):
        if field_name not in properties:
            continue

        prop_type, attrs = properties[field_name]
        tag_type = input_type(tag)
        has_marker = label_has_required_marker(form_text, field_name)
        has_required_attr = bool(re.search(r"\brequired\b", tag, re.IGNORECASE))
        has_required_attribute = "[Required" in attrs

        if has_marker and not (has_required_attr or has_required_attribute):
            required_marker_violations.append(
                f"{field_name} has label '*' but no HTML required or [Required] on {model_class}.{field_name}"
            )

        is_optional_numeric_or_date = (
            not has_marker
            and tag_type in {"number", "date", "datetime-local", "time", "month"}
            and is_value_type_that_requires_nullable_when_optional(prop_type)
            and not is_nullable_type(prop_type)
        )

        if is_optional_numeric_or_date:
            optional_value_type_violations.append(
                f"{field_name} is optional in Razor but {model_class}.{field_name} is non-nullable {prop_type}; use {prop_type}? to avoid generated data-val-required."
            )

    if optional_value_type_violations:
        checks.append(
            Check(
                "Optional numeric/date fields use nullable ViewModel types",
                False,
                "; ".join(optional_value_type_violations) + f" (model: {model_path})",
            )
        )
    else:
        checks.append(Check("Optional numeric/date fields use nullable ViewModel types", True))

    if required_marker_violations:
        checks.append(
            Check(
                "Required label markers match ViewModel required metadata",
                False,
                "; ".join(required_marker_violations) + f" (model: {model_path})",
            )
        )
    else:
        checks.append(Check("Required label markers match ViewModel required metadata", True))

    return checks


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
    parser.add_argument("--module", required=True, help="Module folder name (case-sensitive) (e.g. GoldenReferenceSlim)")
    parser.add_argument(
        "--reference",
        choices=["slim", "compact"],
        default=None,
        help="Golden reference variant: slim (<=8 form fields, create/edit offcanvas) or compact (>8 form fields, full pages)",
    )
    parser.add_argument(
        "--api-profile",
        choices=["proxy", "direct-gateway"],
        default=None,
        help="API call profile. Defaults to proxy for Platform area, direct-gateway for other areas.",
    )

    args = parser.parse_args()

    root = Path(args.project).resolve()
    if not root.exists():
        print(f"[FAIL] Project path does not exist: {root}")
        return 1

    area = args.area
    module = args.module
    reference = args.reference
    api_profile = args.api_profile or ("proxy" if area.lower() == "platform" else "direct-gateway")

    index_cshtml = root / "frontend" / "Diten.Web" / "Views" / area / module / "Index.cshtml"
    filter_cshtml = root / "frontend" / "Diten.Web" / "Views" / area / module / "_Filter.cshtml"
    data_table_partial = root / "frontend" / "Diten.Web" / "Views" / area / module / "_DataTable.cshtml"
    index_l10n_partial = root / "frontend" / "Diten.Web" / "Views" / area / module / "_IndexL10n.cshtml"
    create_edit_offcanvas = root / "frontend" / "Diten.Web" / "Views" / area / module / "_CreateEditOffcanvas.cshtml"
    create_page = root / "frontend" / "Diten.Web" / "Views" / area / module / "Create.cshtml"
    edit_page = root / "frontend" / "Diten.Web" / "Views" / area / module / "Edit.cshtml"
    details_page = root / "frontend" / "Diten.Web" / "Views" / area / module / "Details.cshtml"
    form_partial = root / "frontend" / "Diten.Web" / "Views" / area / module / "_Form.cshtml"
    index_js = root / "frontend" / "Diten.Web" / "wwwroot" / "assets" / "js" / area / module / "index.js"
    index_l10n_js = root / "frontend" / "Diten.Web" / "wwwroot" / "assets" / "js" / area / module / "index.l10n.js"
    dt_defaults_js = root / "frontend" / "Diten.Web" / "wwwroot" / "assets" / "js" / "dt-defaults.js"
    backbone_custom_css = root / "frontend" / "Diten.Web" / "wwwroot" / "assets" / "css" / "backbone-custom.css"

    checks: List[Check] = []

    checks.append(check_file_exists(index_cshtml, "Index.cshtml exists"))
    checks.append(check_file_exists(filter_cshtml, "_Filter.cshtml exists"))
    checks.append(check_file_exists(data_table_partial, "_DataTable.cshtml exists"))
    checks.append(check_file_exists(index_l10n_partial, "_IndexL10n.cshtml exists"))
    checks.append(check_file_exists(index_js, "index.js exists"))
    checks.append(check_file_exists(index_l10n_js, "index.l10n.js exists"))
    checks.append(check_file_exists(dt_defaults_js, "dt-defaults.js exists"))
    checks.append(check_file_exists(backbone_custom_css, "backbone-custom.css exists"))

    if reference == "slim":
        checks.append(check_file_exists(create_edit_offcanvas, "Slim reference has _CreateEditOffcanvas.cshtml"))
    elif reference == "compact":
        checks.append(check_file_exists(create_page, "Compact reference has Create.cshtml"))
        checks.append(check_file_exists(edit_page, "Compact reference has Edit.cshtml"))
        checks.append(check_file_exists(details_page, "Compact reference has Details.cshtml"))
        checks.append(check_file_exists(form_partial, "Compact reference has _Form.cshtml"))

    # Stop early if core files missing (avoid confusing follow-up errors).
    if any(not c.ok for c in checks[:8]):
        print_report(checks)
        return 1

    index_html = read_text(index_cshtml)
    filter_html = read_text(filter_cshtml)
    index_l10n_html = read_text(index_l10n_partial)
    js_text = read_text(index_js)
    index_l10n_js_text = read_text(index_l10n_js)
    dt_defaults_text = read_text(dt_defaults_js)
    css_text = read_text(backbone_custom_css)
    data_table_html = read_text(data_table_partial) if data_table_partial.exists() else ""
    is_v2 = bool(re.search(r"data-dt-standard\s*=\s*\"v2\"", index_html + data_table_html))

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
            data_table_partial if data_table_partial.exists() else index_cshtml,
            index_html + data_table_html,
            re.compile(r"id\s*=\s*\"skeleton-loader\""),
            "DataTable markup has #skeleton-loader",
            "Missing skeleton loader (id=\"skeleton-loader\")",
        )
    )
    if reference != "compact":
        checks.append(
            check_contains(
                index_cshtml,
                index_html,
                re.compile(r"id\s*=\s*\"offcanvasDetailsPreview\""),
                "Index.cshtml has #offcanvasDetailsPreview",
                "Missing offcanvas (id=\"offcanvasDetailsPreview\")",
            )
        )
    if reference == "compact":
        checks.append(
            check_not_contains(
                index_cshtml,
                index_html,
                re.compile(r"id\s*=\s*\"offcanvasCreateEdit\"|_CreateEditOffcanvas"),
                "Compact Index does not include create/edit offcanvas",
                "Compact modules must use full Create/Edit pages, not Index create/edit offcanvas",
            )
        )
        if form_partial.exists() and details_page.exists():
            form_text = read_text(form_partial)
            checks.append(
                check_compact_form_details_section_parity(
                    form_partial,
                    form_text,
                    details_page,
                    read_text(details_page),
                )
            )
            checks.extend(check_form_required_contract(root, form_partial, form_text))
    elif reference == "slim":
        slim_offcanvas_text = read_text(create_edit_offcanvas) if create_edit_offcanvas.exists() else ""
        checks.append(
            check_contains(
                create_edit_offcanvas,
                slim_offcanvas_text,
                re.compile(r"id\s*=\s*\"offcanvasCreateEdit\""),
                "Slim _CreateEditOffcanvas.cshtml has #offcanvasCreateEdit",
                "Slim modules must provide create/edit offcanvas",
            )
        )
        if create_edit_offcanvas.exists():
            checks.extend(check_form_required_contract(root, create_edit_offcanvas, slim_offcanvas_text))

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
            "Inline filter host should not use mx-*; use px-3 (project standard)",
        )
    )
    checks.append(check_inline_filter_host_alignment(filter_html, js_text, filter_cshtml, index_js))

    # Save View + filter apply/reset contract (v2 pages only)
    # - Reset must take effect immediately (no "Reset then Apply" behavior) -> prevent native form reset conflicts.
    # - Save View visibility is based on applied/effective state: filter selections alone must not toggle Save View.
    if is_v2 and re.search(r"\bdt-save-filter-btn\b", js_text):
        personalization_client = root / "frontend" / "Diten.Web" / "wwwroot" / "assets" / "js" / "personalization-client.js"
        personalization_text = read_text(personalization_client)
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
                re.compile(r"saveDefaultView[\s\S]*viewName\s*:\s*\([^\n\r]*\|\|\s*L\.SaveView\s*\|\|\s*['\"]Default['\"]", re.IGNORECASE),
                "Save View payload has non-empty default name",
                "Expected saveDefaultView payload viewName to fall back to 'Default' when no saved/localized name is available",
            )
        )
        checks.append(
            check_contains(
                personalization_client,
                personalization_text,
                re.compile(r"actorType[\s\S]*tenant_user[\s\S]*X-Tenant-Id", re.IGNORECASE),
                "personalizationClient sends tenant header only for tenant users",
                "Expected shared personalizationClient to send X-Tenant-Id for tenant_user Save View requests while omitting it for platform actors",
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
                re.compile(r"btnFilterReset[\s\S]*addEventListener\(\s*['\"]click['\"][\s\S]*setSaveFilterVisible\s*\(\s*isDirtyComparedToDefault\s*\(", re.IGNORECASE),
                "Reset click recalculates Save View dirty visibility",
                "Expected Reset handler to call setSaveFilterVisible(isDirtyComparedToDefault(api)) after factory reset",
            )
        )
        checks.append(
            check_contains(
                index_js,
                js_text,
                re.compile(r"getResetBaselineState[\s\S]*filters\s*:\s*emptyFilters\s*\([\s\S]*search\s*:\s*['\"]{2}[\s\S]*colVis\s*:\s*defaultColVis\s*\([\s\S]*columnOrder\s*:\s*Array\.from[\s\S]*order\s*:\s*baseOrder", re.IGNORECASE),
                "Reset baseline is factory table state",
                "Expected getResetBaselineState() to return empty filters/search + default colVis + default columnOrder + baseOrder, not saved view state",
            )
        )
        checks.append(
            check_contains(
                index_js,
                js_text,
                re.compile(r"btnFilterReset[\s\S]*addEventListener\(\s*['\"]click['\"][\s\S]*applySavedTableState\s*\([^,]+,\s*getResetBaselineState\s*\(\s*\)", re.IGNORECASE),
                "Reset click restores full factory table state",
                "Reset must call applySavedTableState(api, getResetBaselineState()) or equivalent; clearing only filters/search leaves ColVis stale",
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
            re.compile(r"rowClass:\s*['\"]row\s+(?:px-3\s+)?my-0\s+justify-content-between['\"]"),
            "dt-defaults.js topStart rowClass matches golden toolbar contract",
            "Expected buildLayout().topStart.rowClass to match golden toolbar contract",
        )
    )
    checks.append(
        check_contains(
            dt_defaults_js,
            dt_defaults_text,
            re.compile(r"rowClass:\s*['\"]row\s+(?:px-3\s+)?justify-content-between['\"]"),
            "dt-defaults.js bottomStart rowClass matches golden toolbar contract",
            "Expected buildLayout().bottomStart.rowClass to match golden toolbar contract",
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
                data_table_partial if data_table_partial.exists() else index_cshtml,
                index_html + data_table_html,
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
            re.compile(r"\bdocument\.cookie\b|\baccess_token\b|Authorization\s*:\s*[`'\"]?Bearer", re.IGNORECASE),
            "index.js does not read HttpOnly auth cookies or build Bearer tokens",
            "DataTable JS must not read document.cookie/access_token or construct Authorization: Bearer; use MVC same-origin proxy when cookies are HttpOnly",
        )
    )
    if api_profile == "proxy":
        expected_proxy_path = f"/{area}/{module}/api"
        checks.append(
            check_contains(
                index_js,
                js_text,
                re.compile(rf"const\s+endpoint\s*=\s*['\"]{re.escape(expected_proxy_path)}['\"]"),
                f"index.js uses same-origin frontend proxy endpoint ({expected_proxy_path})",
                f"Proxy profile requires const endpoint = '{expected_proxy_path}'",
            )
        )
        checks.append(
            check_not_contains(
                index_js,
                js_text,
                re.compile(r"window\.API\?\.\s*platform|window\.API\.platform|window\.ApiBaseUrl\s*(?:\+|\|\|)|localhost:5000|:5000/api/", re.IGNORECASE),
                "proxy profile avoids direct browser Gateway calls",
                "Platform/admin DataTable JS must call same-origin frontend proxy, not window.API.platform/window.ApiBaseUrl/:5000 directly",
            )
        )
    else:
        checks.append(
            check_contains(
                index_js,
                js_text,
                re.compile(r"window\.API\?\.[A-Za-z0-9_]+|window\.API\.[A-Za-z0-9_]+"),
                "direct-gateway profile uses window.API service base",
                "Direct Gateway profile requires window.API.{service} as the service base URL",
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

    # Bulk action / selection contract (v2 modules)
    # Quality gate (quality-gate-datatable.md) requires bulk surface; verifier enforces it
    # so a green static run cannot pass while bulk selection is silently broken.
    if is_v2:
        checks.append(
            check_contains(
                data_table_partial if data_table_partial.exists() else index_cshtml,
                data_table_html or index_html,
                re.compile(r"class\s*=\s*\"[^\"]*\bdt-checkboxes-select-all\b"),
                "_DataTable.cshtml has select-all checkbox header (dt-checkboxes-select-all)",
                "Missing select-all checkbox header (class containing dt-checkboxes-select-all)",
            )
        )
        checks.append(
            check_contains(
                index_js,
                js_text,
                re.compile(r"\bbulkOptions\b|\bbulkBarSelector\b|\bbulkCountSelector\b"),
                "index.js declares bulk action config (bulkOptions / bulkBarSelector)",
                "Missing bulk action config (bulkOptions or bulkBarSelector wiring)",
            )
        )
        # Bulk surface accepts two valid patterns:
        #  - imperative (Slim): explicit getSelectedIds() + #btnBulkDelete trigger
        #  - declarative (Compact): bulkOptions.onBulkAction.delete + [data-bulk-action]
        checks.append(
            check_contains(
                index_js,
                js_text,
                re.compile(r"\bgetSelectedIds\s*\(|onBulkAction\b"),
                "index.js wires bulk selection (getSelectedIds(...) or onBulkAction)",
                "Missing bulk selection wiring (expected getSelectedIds(...) or bulkOptions.onBulkAction)",
            )
        )
        checks.append(
            check_contains(
                index_js,
                js_text,
                re.compile(r"['\"`][^'\"`]*/bulk['\"`]"),
                "index.js calls bulk endpoint (.../bulk)",
                "Missing bulk endpoint binding (expected URL ending with /bulk)",
            )
        )
        checks.append(
            check_contains(
                index_js,
                js_text,
                re.compile(r"\bbtnBulkDelete\b|\bbulk-delete-btn\b|data-bulk-action"),
                "index.js wires bulk delete trigger (#btnBulkDelete | .bulk-delete-btn | [data-bulk-action])",
                "Missing bulk delete trigger (#btnBulkDelete, .bulk-delete-btn or [data-bulk-action])",
            )
        )
        checks.append(
            check_contains(
                index_js,
                js_text,
                re.compile(r"reloadWithToast\s*\(|reloadWithSuccessToast\s*\("),
                "index.js uses shared reload-with-toast lifecycle (DitenDataTable.reloadWithToast)",
                "Missing reload-with-toast lifecycle wiring; tek satır ve bulk delete aynı reloadWithToast üzerinden gitmeli",
            )
        )
        checks.append(
            check_contains(
                index_js,
                js_text,
                re.compile(r"\bclearSelectionSelector\b|\bclearSelection\s*\("),
                "index.js wires clear-selection (clearSelectionSelector or clearSelection())",
                "Missing clear-selection wiring (bulk bar must expose a clear control)",
            )
        )

    print_report(checks)
    return 1 if any(not c.ok for c in checks) else 0


if __name__ == "__main__":
    raise SystemExit(main())
