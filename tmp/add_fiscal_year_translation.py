import xml.etree.ElementTree as ET
import os

def add_translation(file_path, key, value):
    if not os.path.exists(file_path):
        print(f"File not found: {file_path}")
        return

    tree = ET.parse(file_path)
    root = tree.getroot()

    # Check if key already exists
    for data in root.findall('data'):
        if data.get('name') == key:
            # Update value if exists
            data.find('value').text = value
            tree.write(file_path, encoding='utf-8', xml_declaration=True)
            return

    # Add new entry
    new_data = ET.SubElement(root, 'data', name=key)
    new_data.set('xml:space', 'preserve')
    val_elem = ET.SubElement(new_data, 'value')
    val_elem.text = value

    tree.write(file_path, encoding='utf-8', xml_declaration=True)

validations = {
    'InvalidFiscalYear': {
        'en': 'Invalid format (e.g., 01-01)',
        'tr': 'Geçersiz format (Örn: 01-01)',
        'es': 'Formato no válido (p. ej., 01-01)',
        'ru': 'Неверный формат (например, 01-01)',
        'uz': 'Noto\'g\'ri format (masalan, 01-01)',
        'uk': 'Некоректний формат (наприклад, 01-01)',
        'ka': 'არასწორი ფორმატი (მაგ., 01-01)',
        'kk': 'Жарамсыз формат (мысалы, 01-01)'
    }
}

base_path = '/Users/alitufanoglu/Desktop/ERP-vNext/frontend/Diten.Web/Resources/SharedResource.'

for lang in ['en', 'tr', 'es', 'ru', 'uz', 'uk', 'ka', 'kk']:
    file_path = f"{base_path}{lang}.resx"
    for key, lang_map in validations.items():
        if lang in lang_map:
            add_translation(file_path, key, lang_map[lang])
            print(f"Added {key} to {lang}")
