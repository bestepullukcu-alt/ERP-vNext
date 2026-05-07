---
description: "MOD-0013 Dynamic Localization Standard — UI metinlerinin modül tipine göre (Platform için 2 dil, Tenant için 7 dil) senkronize olmasını garanti eder"
---

# Dynamic-Localization-Standard (MOD-0013)

## 🎯 Temel Prensipler

### 1. Sıfır Statik Metin
- NEVER write hardcoded text in .cshtml, .html, or .js files.
- Tüm metinler .resx dosyalarından @SharedLocalizer["Key"] veya @Localizer["Key"] ile gelmelidir.
- JS tarafındaki metinler window.L10n bridge objesinden okunmalıdır.

### 2. Keşif Kuralı — Eklerken Tara
Yeni bir anahtar eklemeden önce tüm dil dosyalarını keşfet:
find frontend/Diten.Web/Resources -name "SharedResource.*.resx" -type f

Kural: Yeni anahtar, eklendiği modülün tipine göre (Platform için en/tr; Tenant için en/fr/es/zh/ar/ru/tr) o bağlamda geçerli olan TÜM dosyalara aynı anda eklenmelidir.

### 3. Gerçek Çeviri Disiplini
- İngilizce metni diğer dosyalara yer tutucu olarak kopyalamayın.
- Eğer çeviriden emin değilseniz, en yakın doğru çeviriyi kullanın ama boş bırakmayın.

---

## 🌉 Köprü Sistemi: Razor -> JavaScript

JS dosyalarında ihtiyaç duyulan metinler için L10n Bridge deseni zorunludur.

### Zorunlu Pattern: Partial + JSON Payload + Loader JS

**Razor Partial (`_IndexL10n.cshtml`):**
```cshtml
<script id="module-l10n" type="application/json">
    @Json.Serialize(new
    {
        MyNewKey = SharedLocalizer["MyNewKey"].Value,
        MyModuleKey = Localizer["MyModuleKey"].Value
    })
</script>
```

**Loader JS (`index.l10n.js`):**
```javascript
(function () {
    const payload = document.getElementById('module-l10n');
    if (!payload) return;

    // ASP.NET Json.Serialize outputs camelCase. Convert to PascalCase.
    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);
    
    try {
        const raw = JSON.parse(payload.textContent || '{}');
        const normalized = {};
        for (const key of Object.keys(raw)) {
            normalized[toPascalCase(key)] = raw[key];
        }
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
    } catch(err) {
        console.error('Localization payload error', err);
    }
})();
```

**Page JS (`index.js`):**
```javascript
const label = window.L10n?.MyNewKey;
```

### Yasak Pattern

`Index.cshtml` içinde onlarca satır `window.L10n.MyKey = ...` assignment bloğu yazmak standart değildir. Yeni veya revize edilen sayfalarda bu pattern kullanılmaz.

> **KRİTİK:** Her zaman `@Json.Serialize(...)` kullanın. `@Html.Raw(...)` kullanmayın; tek tırnak veya özel karakter içeren diller JS stringini bozabilir.

---

## 🚨 Operasyonel Kurallar

### 1. XML Güvenliği
.resx dosyalarında özel karakterleri escape edin:
& -> &amp; | < -> &lt; | > -> &gt; | " -> &quot;

### 2. Yeniden Derleme Protokolü
.resx değişikliği sonrası şu sırayı izleyin:
1. Süreçleri durdur: lsof -ti :5000,5001,5050 | xargs kill -9
2. Cache temizle: rm -rf frontend/Diten.Web/bin frontend/Diten.Web/obj
3. Rebuild: ./run_all.sh
4. Tarayıcıda Hard Refresh (Ctrl+F5) yapın.

---

## 📂 Desteklenen Diller

**Platform Modülleri (Admin):**
Yalnızca `en` ve `tr` dilleri desteklenmektedir.

**Tenant Modülleri:**
Tüm 7 dil desteklenmektedir.

| Kod | Dil |
|---|---|
| en | English (Default) |
| fr | Français |
| es | Español |
| zh | 中文 |
| ar | العربية |
| ru | Русский |
| tr | Türkçe |

---

## 🛠️ UI Standartları

### Server-to-JS Toast Lokalizasyonu
Controller'dan gelen TempData mesajını Razor içinde lokalize edin:
var successMsg = @Json.Serialize(TempData["SuccessMessage"] != null ? SharedLocalizer[TempData["SuccessMessage"].ToString()].Value : null);

### Dinamik View (Create/Edit)
Create.cshtml içinde isEditMode değişkeni kullanarak başlıkları ve butonları dinamikleştirin:
@(isEditMode ? SharedLocalizer["Update"] : SharedLocalizer["Save"])

### Form Validation
- novalidate özniteliğini form etiketine ekleyin.
- DataAnnotations için SharedResource marker class'ını kullanın.
- invalid-feedback sınıflarını Bootstrap 5 standartlarına göre yapılandırın.

---
Diten ERP vNext Localization Constitution - MOD-0013
