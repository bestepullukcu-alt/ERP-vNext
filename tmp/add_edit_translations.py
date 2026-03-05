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
    'EditCompany': {
        'en': 'Edit Company',
        'tr': 'Şirket Düzenle',
        'es': 'Editar Empresa',
        'ru': 'Редактировать компанию',
        'uz': 'Shirkatni tahrirlash',
        'uk': 'Редагувати компанію',
        'ka': 'კომპანიის რედაქტირება',
        'kk': 'Компанияны редакциялау'
    },
    'EditCompanyDescription': {
        'en': 'Edit existing company details.',
        'tr': 'Mevcut şirket detaylarını düzenleyin.',
        'es': 'Editar los detalles de la empresa existente.',
        'ru': 'Редактировать детали существующей компании.',
        'uz': 'Mavjud shirkat tafsilotlarini tahrirlash.',
        'uk': 'Редагувати деталі існуючої компанії.',
        'ka': 'არსებული კომპანიის დეტალების რედაქტირება.',
        'kk': 'Қолданыстағы компания мәліметтерін өңдеу.'
    }
}

base_path = '/Users/alitufanoglu/Desktop/ERP-vNext/frontend/Diten.Web/Resources/Views/MDM/LegalEntities.'

for lang in ['en', 'tr', 'es', 'ru', 'uz', 'uk', 'ka', 'kk']:
    file_path = f"{base_path}{lang}.resx"
    for key, lang_map in translations.items():
        if lang in lang_map:
            add_translation(file_path, key, lang_map[lang])
            print(f"Added {key} to {lang}")
