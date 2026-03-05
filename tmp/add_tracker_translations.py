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

translations = {
    'ValidationErrors': {
        'en': 'Errors: {0}',
        'tr': 'Hata: {0}',
        'es': 'Errores: {0}',
        'ru': 'Ошибки: {0}',
        'uz': 'Xatolar: {0}',
        'uk': 'Помилки: {0}',
        'ka': 'შეცდომა: {0}',
        'kk': 'Қателер: {0}'
    },
    'RequiredStatus': {
        'en': 'Required: {0} / {1}',
        'tr': 'Zorunlu: {0} / {1}',
        'es': 'Requerido: {0} / {1}',
        'ru': 'Обязательно: {0} / {1}',
        'uz': 'Zarur: {0} / {1}',
        'uk': 'Обов\'язково: {0} / {1}',
        'ka': 'სავალდებულო: {0} / {1}',
        'kk': 'Міндетті: {0} / {1}'
    }
}

base_path = '/Users/alitufanoglu/Desktop/ERP-vNext/frontend/Diten.Web/Resources/SharedResource.'

for lang in ['en', 'tr', 'es', 'ru', 'uz', 'uk', 'ka', 'kk']:
    file_path = f"{base_path}{lang}.resx"
    for key, lang_map in translations.items():
        if lang in lang_map:
            add_translation(file_path, key, lang_map[lang])
            print(f"Added {key} to {lang}")
