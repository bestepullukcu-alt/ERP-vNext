import zipfile
import xml.etree.ElementTree as ET
import os

def read_xlsx(file_path):
    with zipfile.ZipFile(file_path, 'r') as zip_ref:
        # Get shared strings
        shared_strings = []
        if 'xl/sharedStrings.xml' in zip_ref.namelist():
            with zip_ref.open('xl/sharedStrings.xml') as f:
                tree = ET.parse(f)
                root = tree.getroot()
                # Shared strings are in <si><t>...
                for si in root.findall('{http://schemas.openxmlformats.org/spreadsheetml/2006/main}si'):
                    t = si.find('{http://schemas.openxmlformats.org/spreadsheetml/2006/main}t')
                    if t is not None:
                        shared_strings.append(t.text)
                    else:
                        # Handle multi-part text if necessary
                        r_texts = []
                        for r in si.findall('{http://schemas.openxmlformats.org/spreadsheetml/2006/main}r'):
                            rt = r.find('{http://schemas.openxmlformats.org/spreadsheetml/2006/main}t')
                            if rt is not None and rt.text:
                                r_texts.append(rt.text)
                        shared_strings.append("".join(r_texts))

        # Get sheet names
        sheet_names = {}
        with zip_ref.open('xl/workbook.xml') as f:
            tree = ET.parse(f)
            root = tree.getroot()
            sheets = root.find('{http://schemas.openxmlformats.org/spreadsheetml/2006/main}sheets')
            for sheet in sheets.findall('{http://schemas.openxmlformats.org/spreadsheetml/2006/main}sheet'):
                sheet_names[sheet.get('sheetId')] = sheet.get('name')

        print(f"Sheets found: {list(sheet_names.values())}")
        print("-" * 40)

        # Read each sheet
        for sheet_file in zip_ref.namelist():
            if sheet_file.startswith('xl/worksheets/sheet') and sheet_file.endswith('.xml'):
                sheet_id = sheet_file.replace('xl/worksheets/sheet', '').replace('.xml', '')
                name = sheet_names.get(sheet_id, f"Sheet {sheet_id}")
                print(f"\nSheet Index: {sheet_id} - Name: {name}")
                
                with zip_ref.open(sheet_file) as f:
                    tree = ET.parse(f)
                    root = tree.getroot()
                    sheet_data = root.find('{http://schemas.openxmlformats.org/spreadsheetml/2006/main}sheetData')
                    
                    for row in sheet_data.findall('{http://schemas.openxmlformats.org/spreadsheetml/2006/main}row'):
                        row_data = []
                        for c in row.findall('{http://schemas.openxmlformats.org/spreadsheetml/2006/main}c'):
                            v_node = c.find('{http://schemas.openxmlformats.org/spreadsheetml/2006/main}v')
                            if v_node is not None:
                                val = v_node.text
                                t = c.get('t')
                                if t == 's': # shared string
                                    val = shared_strings[int(val)]
                                row_data.append(str(val))
                            else:
                                row_data.append("")
                        print("\t".join(row_data))

if __name__ == "__main__":
    file_path = "execution/modules_pages_planning_v3.xlsx"
    if os.path.exists(file_path):
        read_xlsx(file_path)
    else:
        print("File not found")
