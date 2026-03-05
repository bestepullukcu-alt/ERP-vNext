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
    'InvalidEmail': {
        'en': 'Invalid email address',
        'tr': 'Geçersiz e-posta adresi',
        'es': 'Dirección de correo electrónico no válida',
        'ru': 'Неверный адрес электронной почты',
        'uz': 'Noto\'g\'ri elektron pochta manzili',
        'uk': 'Некоректна адреса електронної пошти',
        'ka': 'არასწორი ელფოსტის მისამართი',
        'kk': 'Жарамсыз электрондық пошта мекенжайы'
    },
    'InvalidUrl': {
        'en': 'Invalid URL format',
        'tr': 'Geçersiz URL formatı',
        'es': 'Formato URL no válido',
        'ru': 'Неверный формат URL',
        'uz': 'URL formati noto\'g\'ri',
        'uk': 'Некоректний формат URL',
        'ka': 'არასწორი URL ფორმატი',
        'kk': 'URL форматы жарамсыз'
    },
    'InvalidPhone': {
        'en': 'Invalid phone number',
        'tr': 'Geçersiz telefon numarası',
        'es': 'Número de teléfono no válido',
        'ru': 'Неверный номер телефона',
        'uz': 'Telefon raqami noto\'g\'ri',
        'uk': 'Некоректний номер телефону',
        'ka': 'არასწორი ტელეფონის ნომერი',
        'kk': 'Телефон нөмірі жарамсыз'
    },
    'NumericOnly': {
        'en': 'Only digits are allowed',
        'tr': 'Sadece rakam girilebilir',
        'es': 'Sólo se permiten dígitos',
        'ru': 'Допускаются только цифры',
        'uz': 'Faqat raqamlar ruxsat etiladi',
        'uk': 'Дозволені лише цифри',
        'ka': 'ნებადართულია მხოლოდ ციფრები',
        'kk': 'Тек цифрларға рұқсат етіледі'
    },
    'FieldRequired': {
        'en': 'This field is required',
        'tr': 'Bu alan zorunludur',
        'es': 'Este campo es obligatorio',
        'ru': 'Это поле обязательно к заполнению',
        'uz': 'Ushbu maydon to\'ldirilishi shart',
        'uk': 'Це поле є обов\'язковим',
        'ka': 'ეს ველი სავალდებულოა',
        'kk': 'Бұл өрісті толтыру міндетті'
    }
}

base_path = '/Users/alitufanoglu/Desktop/ERP-vNext/frontend/Diten.Web/Resources/SharedResource.'

for lang in ['en', 'tr', 'es', 'ru', 'uz', 'uk', 'ka', 'kk']:
    file_path = f"{base_path}{lang}.resx"
    for key, lang_map in validations.items():
        if lang in lang_map:
            add_translation(file_path, key, lang_map[lang])
            print(f"Added {key} to {lang}")
