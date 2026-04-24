#!/usr/bin/env python3
"""Improved Parse modules_pages_planning_v3.xlsx for multi-module domain bootstrap."""

from __future__ import annotations

import argparse
import json
import re
import zipfile
from collections import defaultdict
from pathlib import Path
import xml.etree.ElementTree as ET

NS = {
    "a": "http://schemas.openxmlformats.org/spreadsheetml/2006/main",
    "r": "http://schemas.openxmlformats.org/officeDocument/2006/relationships",
    "p": "http://schemas.openxmlformats.org/package/2006/relationships",
}

# Domain mapping registry
DOMAIN_MAP = {
    "master-data-management": {
        "keywords": ["Product", "Item", "SKU", "Category", "Currency", "Country", "Unit", "Legal Entity", "Unit of Measure", "UOM", "Variant", "Composition", "Packaging", "Lot-Batch-Serial", "Lifecycle", "Reference Data"],
        "src_sheets": ["SRC - Back Bone", "SRC - Logistics", "SRC - Inventory"]
    },
    "platform-shared-services": {
        "keywords": ["Auth", "RBAC", "ABAC", "Tenant", "Identity", "Audit", "Log", "Workflow", "Notification", "API Gateway", "Webhook", "Event", "Secret", "Vault"],
        "src_sheets": ["SRC - Back Bone"]
    },
    "enterprise-strategy-business-performance": {
        "keywords": ["KPI", "Strategy", "Performance", "Planning", "Metric", "Entity", "Structure"],
        "src_sheets": ["SRC - Back Bone"]
    }
}

def col_to_index(col: str) -> int:
    n = 0
    for ch in col:
        if ch.isalpha():
            n = n * 26 + ord(ch.upper()) - 64
    return n - 1

def load_workbook_index(zip_ref: zipfile.ZipFile) -> tuple[dict[str, str], list[str]]:
    shared_strings: list[str] = []
    if "xl/sharedStrings.xml" in zip_ref.namelist():
        root = ET.fromstring(zip_ref.read("xl/sharedStrings.xml"))
        for si in root.findall("a:si", NS):
            text = "".join((t.text or "") for t in si.findall(".//a:t", NS))
            shared_strings.append(text)

    wb = ET.fromstring(zip_ref.read("xl/workbook.xml"))
    rels = ET.fromstring(zip_ref.read("xl/_rels/workbook.xml.rels"))
    rid_to_target = {r.attrib["Id"]: r.attrib["Target"] for r in rels.findall("p:Relationship", NS)}

    sheets: dict[str, str] = {}
    for s in wb.findall("a:sheets/a:sheet", NS):
        name = s.attrib["name"]
        rid = s.attrib["{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id"]
        sheets[name] = "xl/" + rid_to_target[rid]

    return sheets, shared_strings

def read_sheet_rows(zip_ref: zipfile.ZipFile, target: str, shared_strings: list[str]) -> list[list[str]]:
    if target not in zip_ref.namelist(): return []
    root = ET.fromstring(zip_ref.read(target))
    rows: list[list[str]] = []
    for row in root.findall(".//a:sheetData/a:row", NS):
        cells: defaultdict[int, str] = defaultdict(str)
        for c in row.findall("a:c", NS):
            ref = c.attrib.get("r", "")
            m = re.match(r"([A-Z]+)\d+", ref)
            if not m: continue
            idx = col_to_index(m.group(1))
            t = c.attrib.get("t")
            v = c.find("a:v", NS)
            if v is None:
                is_node = c.find("a:is/a:t", NS)
                value = is_node.text if is_node is not None else ""
            else:
                value = v.text or ""
                if t == "s":
                    try: value = shared_strings[int(value)]
                    except (ValueError, IndexError): pass
            cells[idx] = str(value).strip()
        if cells:
            max_idx = max(cells.keys())
            rows.append([cells[i] for i in range(max_idx + 1)])
    return rows

def parse_dependencies(value: str) -> list[str]:
    return [item.strip() for item in value.split(";") if item.strip()]

def bootstrap_domain_data(file_path: Path, domain_name: str) -> dict:
    domain_cfg = DOMAIN_MAP.get(domain_name.lower())
    if not domain_cfg:
        raise ValueError(f"Domain '{domain_name}' not found in registry. Known: {list(DOMAIN_MAP.keys())}")

    with zipfile.ZipFile(file_path, "r") as zip_ref:
        sheets, shared_strings = load_workbook_index(zip_ref)
        module_catalog = read_sheet_rows(zip_ref, sheets["Module Catalog"], shared_strings)
        release_wave_map = read_sheet_rows(zip_ref, sheets["Release Wave Map - Auto"], shared_strings)
        backbone = read_sheet_rows(zip_ref, sheets["BackBone"], shared_strings)

        found_modules = []
        for row in module_catalog:
            if not row or not row[0].startswith("MOD-"): continue
            mod_id = row[0]
            mod_name = row[1] if len(row) > 1 else ""
            mod_src = row[3] if len(row) > 3 else ""
            
            # Matching logic: by SRC sheet or by Keyword
            match_src = any(src in mod_src for src in domain_cfg["src_sheets"])
            match_kw = any(kw.lower() in mod_name.lower() for kw in domain_cfg["keywords"])
            
            if match_src or match_kw:
                # Find details in Wave Map
                wave_row = next((r for r in release_wave_map if r and r[0] == mod_id), [])
                # Find details in Backbone
                bb_row = next((r for r in backbone if len(r) > 6 and r[6] == mod_id), [])
                
                # Try to find a dedicated sheet for pages
                pages = []
                sheet_name = next((name for name in sheets if f"({mod_id})" in name), None)
                if sheet_name:
                    rows = read_sheet_rows(zip_ref, sheets[sheet_name], shared_strings)
                    # Simple heuristic for pages: rows after 'Pages Breakdown'
                    start_collecting = False
                    for p_row in rows:
                        if len(p_row) > 0 and "Pages Breakdown" in p_row[0]:
                            start_collecting = True
                            continue
                        if start_collecting and len(p_row) >= 4 and p_row[2].isdigit():
                            pages.append({
                                "seq": p_row[2],
                                "name": p_row[3],
                                "h1_1": p_row[4] if len(p_row) > 4 else "",
                                "h2_1": p_row[8] if len(p_row) > 8 else "",
                                "notes": p_row[12] if len(p_row) > 12 else ""
                            })

                found_modules.append({
                    "module_id": mod_id,
                    "module_name": mod_name,
                    "src": mod_src,
                    "suggested_wave": wave_row[16] if len(wave_row) > 16 else "",
                    "dependency_gates": parse_dependencies(wave_row[8] if len(wave_row) > 8 else ""),
                    "backbone_pages": pages or (parse_dependencies(bb_row[12]) if len(bb_row) > 12 else []),
                    "notes": bb_row[14] if len(bb_row) > 14 else ""
                })

        return {
            "domain": domain_name,
            "module_count": len(found_modules),
            "modules": found_modules
        }

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--file", default="execution/modules_pages_planning_v3.xlsx")
    parser.add_argument("--domain", required=True)
    parser.add_argument("--pretty", action="store_true")

    args = parser.parse_args()
    parsed = bootstrap_domain_data(Path(args.file), args.domain)
    print(json.dumps(parsed, ensure_ascii=False, indent=2 if args.pretty else None))

if __name__ == "__main__":
    main()
