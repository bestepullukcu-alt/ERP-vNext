import os
import xml.etree.ElementTree as ET

res_dir = "/Users/alitufanoglu/Desktop/ERP-vNext/frontend/Diten.Web/Resources"
files = [f for f in os.listdir(res_dir) if f.startswith("SharedResource.") and f.endswith(".resx")]

new_keys = {
    "Login.Title": {"en": "Sign In", "tr": "Giriş Yap"},
    "Login.Email": {"en": "Email", "tr": "E-posta"},
    "Login.Password": {"en": "Password", "tr": "Şifre"},
    "Login.RememberMe": {"en": "Remember Me", "tr": "Beni Hatırla"},
    "Login.Submit": {"en": "Sign In", "tr": "Giriş Yap"},
    "Login.ErrorTitle": {"en": "Error", "tr": "Hata"},
    "Login.ErrorRequired": {"en": "Email and password are required", "tr": "E-posta ve şifre zorunludur"},
    "Login.Error": {"en": "Invalid email or password", "tr": "Geçersiz e-posta veya şifre"},
    "Login.Loading": {"en": "Signing in...", "tr": "Giriş yapılıyor..."}
}

for file in files:
    path = os.path.join(res_dir, file)
    lang = file.split(".")[1] if "." in file else "en"
    if lang == "resx": lang = "en"
    
    tree = ET.parse(path)
    root = tree.getroot()
    
    existing_keys = [data.get("name") for data in root.findall("data")]
    
    for key, values in new_keys.items():
        if key not in existing_keys:
            data = ET.SubElement(root, "data", name=key, xml_space="preserve")
            val = values.get(lang, values["en"])
            value_el = ET.SubElement(data, "value")
            value_el.text = val
    
    # Write back
    tree.write(path, encoding="utf-8", xml_declaration=True)
    print(f"Updated {file}")
