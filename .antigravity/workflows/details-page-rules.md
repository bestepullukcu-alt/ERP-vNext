---
description: "[Detay Sayfası UI Düzen Kuralları — Diten ERP vNext]"
---
# Detay (Details) Sayfası UI Kuralları

Bir kaydın "Salt Okunur Detaylarını" oluştururken veya düzenlerken, aşağıdaki iki modelden birini seçmelisiniz. Bu modeller, Diten ERP vNext görsel standartlarına (Sneat 2.x) uygun olmalıdır.

---

## KURAL #1: Model Seçimi ve Kapasite

### Model A: Offcanvas "Hızlı Bakış" (Hafif Veriler İçin)
- **Kullanım:** 5-10 kısa özellik, karmaşık sekme (tab) içermeyen yapılar.
- **Tetikleme:** Liste/Index sayfasındaki DataTable satırından tıklanır.
- **Diten Şartı:** İçerik AJAX ile yüklenmeli ve `window.L10n` bridge yapısı ile yerelleştirilmelidir (Platform modülleri: 2 dil, Tenant modülleri: 7 dil).

### Model B: İzole Tam Detay Sayfası (Ağır Veriler İçin)
- **Kullanım:** İlişkili tablolar, çok sayıda sekme veya finansal/iletişim gibi blok grupları.
- **Tetikleme:** `/{Controller}/Details/{id}` rotasına gidilerek açılır.
- **Diten Şartı:** Shell tipine göre admin modüllerinde `Layout = "_LayoutPlatformAdmin";`, tenant modüllerinde `Layout = "_LayoutTenantShell";` kullanılmalı ve asenkron veri için Skeleton Loader eklenmelidir.

---

## KURAL #2: Düzen ve Multi-Tenancy Güvenliği
- Sol taraftaki dar "Kullanıcı/Profil Kartı" yapısını KULLANMAYIN. Sayfa `col-12` (tam genişlik) olmalıdır.
- **Güvenlik:** Backend tarafındaki Handler, başka kiracıların verisine erişimi engellemek için `X-Tenant-Id` kontrolünü sıkı bir şekilde yapmalıdır.

## KURAL #3: Başlık ve Dinamik Açıklama (L10n)
- Sayfa başlığının altında (`<p class="mb-0">`) dinamik bir alt açıklama olmalıdır.
- **L10n Şartı:** "No:", "Tip:" gibi tüm sabit metinler mutlaka `@SharedLocalizer` üzerinden gelmelidir.
- Örnek Mantık: 
    ```csharp
    @{
        var descParts = new List<string>();
        if(!string.IsNullOrEmpty(Model.Type)) { descParts.Add(SharedLocalizer[Model.Type]); }
        if(!string.IsNullOrEmpty(Model.Number)) { descParts.Add(SharedLocalizer["RegistrationNo"] + ": " + Model.Number); }
    }
    <p class="mb-0 text-muted">@(string.Join(" • ", descParts))</p>
    ```

## KURAL #4: Izgara (Grid) Yapısı (3'lü Kart Düzeni)
- Kartları Bootstrap `row g-6` (Diten standart boşluğu) içine alın.
- Responsive sütun yapısı: `<div class="col-12 col-md-6 col-lg-4">`. Bu, geniş ekranlarda 3 kartın yan yana gelmesini sağlar.

## KURAL #5: Bilgi Kartları İçinde Dikey Dizilim
- Kart içindeki veri listeleri (`<dl class="row mb-0">`) dikey (üstten alta) dizilmelidir. Yan yana (`col-sm-4` vb.) yapıları kullanmayın.
- **Diten Standart Şablonu:**
  - `<dt class="col-12 fw-medium text-heading mb-1">@SharedLocalizer["Label"]</dt>`
  - `<dd class="col-12 mb-4">@Model.Value</dd>`

## KURAL #6: Details Surface Görünümü
- Read-only details bölümleri `card backbone-preview-section` class'larını birlikte kullanmalıdır.
- Details surface, liste/card radius'u ile aynı token'dan beslenmelidir; hardcoded radius veya renk kullanılmaz.
- Border kaldırılacaksa bu yalnız ilgili Details sayfasının wrapper class'ı altında scoped yapılır: `border: 0; border-radius: var(--bs-card-border-radius); background: var(--bs-card-bg);`.
- Layout veya tüm `.card` elemanlarına yayılan global border/radius override yazılmaz; bordered layout ihtiyacı Sneat settings üzerinden yönetilir.

## KURAL #7: Details İçinde Tab / Navbar Standardı
- Details sayfasında birden fazla içerik grubu tab ile ayrılacaksa düz Bootstrap `nav-tabs` kullanılmaz.
- Zorunlu referans WorkCenter tab bar'dır: `Views/WorkCenter/WorkCenterTabs.cshtml`.
- Tab bar dış yüzeyi `card mb-4`, içi `card-header p-3` ve `nav-align-top` olmalıdır.
- Tab listesi `nav nav-pills d-inline-flex gap-2 flex-wrap` olmalıdır.
- Her tab butonu `nav-link small border shadow-none wc-tab-compact` sınıflarını taşımalıdır.
- Tab butonlarında `bx` ikon + `wc-tab-icon` kullanılmalıdır. Desktop'ta ikon+metin, mobilde sadece ikon gösterilmelidir:
  - Desktop metni: `<span class="d-none d-sm-inline-flex align-items-center">...`
  - Mobil ikon: `<i class="bx ... d-sm-none wc-tab-icon"></i>`
- Details tabları yalnız içerik gruplarını ayırır; sidebar/menu placement, navigation tree, drag-drop veya publish/rollback navigation davranışı üretmez.

---
Diten ERP vNext Salt Okunur Standartları - VIEW-002
