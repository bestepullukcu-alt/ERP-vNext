import zipfile
import xml.etree.ElementTree as ET
from collections import defaultdict

NS = {"a": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}

def read_excel_catalog(file_path):
    with zipfile.ZipFile(file_path, 'r') as zip_ref:
        # Load shared strings
        shared_strings = []
        if "xl/sharedStrings.xml" in zip_ref.namelist():
            root = ET.fromstring(zip_ref.read("xl/sharedStrings.xml"))
            for si in root.findall("a:si", NS):
                shared_strings.append("".join(t.text or "" for t in si.findall(".//a:t", NS)))

        # Load Sheet 4 (Module Catalog) - assuming it's sheet4.xml
        # In a real scenario we'd check xl/workbook.xml for names
        target = "xl/worksheets/sheet4.xml"
        root = ET.fromstring(zip_ref.read(target))
        
        modules = []
        for row in root.findall(".//a:sheetData/a:row", NS):
            cells = {}
            for c in row.findall("a:c", NS):
                ref = c.attrib.get("r", "")
                col = "".join(filter(str.isalpha, ref))
                v = c.find("a:v", NS)
                val = ""
                if v is not None:
                    val = v.text
                    if c.attrib.get("t") == "s":
                        val = shared_strings[int(val)]
                cells[col] = val
            
            if cells.get("A") and cells["A"].startswith("MOD-"):
                modules.append({
                    "id": cells.get("A"),
                    "name": cells.get("B"),
                    "classification": cells.get("C"),
                    "src": cells.get("D"),
                    "has_sheet": cells.get("F") == "YES"
                })
        return modules

catalog = read_excel_catalog("execution/modules_pages_planning_v3.xlsx")
for m in catalog:
    # Look for MDM or Product or Item related things
    if any(kw in m["name"].lower() for kw in ["master data", "product", "item", "sku", "category", "currency", "legal entity", "unit of measure", "uom"]):
        print(f"{m['id']} | {m['name']} | {m['src']}")
