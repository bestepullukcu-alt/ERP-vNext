---
description: "MOD-0013 Dynamic Localization Standard — UI metinlerinin 8 dilde senkronize olmasını garanti eder"
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

Kural: Yeni anahtar keşfedilen TÜM dosyalara (en, tr, es, ru, uk, ka, kk, uz) aynı anda eklenmelidir.

### 3. Gerçek Çeviri Disiplini
- İngilizce metni diğer dosyalara yer tutucu olarak kopyalamayın.
- Eğer çeviriden emin değilseniz, en yakın doğru çeviriyi kullanın ama boş bırakmayın.

---

## 🌉 Köprü Sistemi: Razor -> JavaScript

JS dosyalarında ihtiyaç duyulan metinler için L10n Bridge deseni zorunludur.



**Razor View (.cshtml):**
window.L10n = window.L10n || {};
window.L10n.MyNewKey = @Json.Serialize(SharedLocalizer["MyNewKey"].Value);

**JavaScript (.js):**
var label = (window.L10n && window.L10n.MyNewKey) || 'Fallback English';

> **KRİTİK:** Her zaman @Json.Serialize(...) kullanın. @Html.Raw(...) kullanmayın; Uzbekçe (o'zbekcha) gibi dillerdeki tek tırnaklar JS stringini bozar ve sayfayı patlatır.

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

| Kod | Dil |
|---|---|
| en | English (Default) |
| tr | Türkçe |
| es | Español |
| ru | Русский |
| uk | Українська |
| ka | ქართული |
| kk | Қазақша |
| uz | O'zbek |

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