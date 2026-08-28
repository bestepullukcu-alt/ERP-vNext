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

## 🔀 İKİ KÖPRÜ MEKANİZMASI — hangisini seçtiğini BİLEREK seç

Bu depoda köprü iki farklı şekilde kuruluyor ve ikisi arasındaki fark **sessiz bir kusur sınıfı**
üretiyor. Yeni bir yüzey açan herkes hangi deseni izlediğini bilmek zorunda.

| | **A — Otomatik sayım** | **B — Elle tutulan liste** |
|---|---|---|
| Örnek | `Views/WorkCenterNext/_L10n.cshtml` | `Views/Tasks/_IndexL10n.cshtml` |
| Nasıl | `Localizer.GetAllStrings(true)` — tüm resx'i tarar | Her anahtar için bir satır (bugün 157 satır) |
| Anahtar biçimi | resx'teki hâli (PascalCase) | `@Json.Serialize` camelCase'e çevirir |
| resx'e anahtar eklendi, köprüye eklenmedi | **imkânsız** — otomatik gelir | **sessizce düşer** — okuyucu ham anahtarı görür |

⚠ **B deseninde köprü, resx'i saymaz.** `api.js`'in kendi yorumu bunu üç kez yazıyor:
*"a code mapped in api.js without a line here reaches the reader as the generic error."*
Yani B'de bir anahtarın çalışması için **iki** yerde olması gerekir: resx'te **ve** partial'da.

**YENİ YÜZEY AÇIYORSAN A DESENİNİ KULLAN.** Tek satır (`GetAllStrings(true)`), kayma imkânsız.
SharedResource'tan anahtar gerekiyorsa A deseni de onları **tek tek** kopyalar
(WorkCenterNext yalnız altı `Dt*` anahtarını alıyor) — yani "SharedResource'ta var" bir cevap
değildir; o sayfanın yükünde var mı, ona bakılır.

**MEVCUT B YÜZEYLERİNİ BU TURDA ÇEVİRME.** Davranış değişikliğidir (yük büyür, çakışan
anahtarların önceliği değişir) ve ayrı bir karardır — bkz. BL-308.

### Muhafız: anahtar sorulduysa var olmalı

`frontend/Diten.Web/tests/workcenter-next-l10n-key-guard.test.js` bu sınıfı elle ölçülmek
zorunda olmaktan çıkarır. Yeni bir yüzey için aynısını yazarken **üç tuzağı** bilmek şart:

1. **YORUMLAR.** Yorum içinde geçen `t('X')` bir çağrı değildir. Ölçüm yorumları ayıklanmış
   kaynak üzerinde yapılmalı; ayıklayıcının kendi testi olmalı.
2. **AİLELER.** `t('AuditEvent' + code)` bir anahtar değil, bir alan adıdır. Ailenin alanı
   **kaynağından** okunmalı (yürütülebilir sözleşme ya da C# enum'u) ve **bildirilmemiş bir aile
   testi kırmalı** — yoksa muhafız tam da riskin yüksek olduğu yerde susar.
3. **AYNI KLASÖR, İKİ KÖPRÜ.** `WorkCenterNext/quick-create.js` bu klasörde durur ama TASKS
   yükünden okur (camelCase). Her dosya, **bağlandığı köprü okunarak** sınıflandırılmalı;
   sınıflandırılamayan dosya testi kırmalı.

---

## 🚨 Operasyonel Kurallar

### 1. XML Güvenliği
.resx dosyalarında özel karakterleri escape edin:
& -> &amp; | < -> &lt; | > -> &gt; | " -> &quot;

### 2. Yeniden Derleme Protokolü
.resx değişikliği sonrası şu sırayı izleyin:
1. Süreçleri durdur: lsof -ti :5000,5001,5050 | xargs kill -9
   ⚠ **ÖNCE PORTU KİMİN TUTTUĞUNU ÖLÇ.** Bu makinede başka geliştiricilerin worktree'leri var ve
   servisleri koşuyor olabilir. `lsof -p <PID> -a -d cwd -Fn` hangi dizinden koştuğunu söyler;
   **başka bir dizinden koşan servisi durdurma** — sahibinin haberi olmaz. Körlemesine
   `kill -9` bir kez böyle bir servisi düşürdü.
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
