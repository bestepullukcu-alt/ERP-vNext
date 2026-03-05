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
    'RecordUpdated': {
        'en': 'Record has been updated successfully.',
        'tr': 'Kayıt başarıyla güncellendi.',
        'es': 'El registro se ha actualizado correctamente.',
        'ru': 'Запись успешно обновлена.',
        'uz': 'Yozuv muvaffaqiyatli yangilandi.',
        'uk': 'Запис успішно оновлено.',
        'ka': 'ჩანაწერი წარმატებით განახლდა.',
        'kk': 'Жазба сәтті жаңартылды.'
    }
}

base_path = '/Users/alitufanoglu/Desktop/ERP-vNext/frontend/Diten.Web/Resources/SharedResource.'

for lang in ['en', 'tr', 'es', 'ru', 'uz', 'uk', 'ka', 'kk']:
    file_path = f"{base_path}{lang}.resx"
    for key, lang_map in translations.items():
        if lang in lang_map:
            add_translation(file_path, key, lang_map[lang])
            print(f"Added {key} to {lang}")
