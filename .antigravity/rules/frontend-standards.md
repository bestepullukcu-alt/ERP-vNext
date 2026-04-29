---
description: "FRONT-001 — Diten.Web Frontend Katmanı Zorunlu Kodlama ve UI/UX Standartları"
---

# Frontend Standards (Diten ERP vNext)

Bu dosya, projenin mimari bütünlüğünü korumak için belirlenmiş **kod yazım standartlarını** ve **yapısal düzeni** tanımlar. Tüm ajanlar bu kurallara uymak zorundadır.

---

## 1. Controller Standartları (C#)

Controller katmanı navigasyon odaklı ve "ince" (thin) tutulmalıdır.

*   **Attribute Routing:** Route tanımları action üzerinde açıkça belirtilmelidir.
    *   *Kural:* `[Route("module-name/page-name")]` gibi anlaşılır ve modül ismini içeren route'lar tercih edilmelidir.
    *   *Kritik Kural:* Tüm Razor sayfalarındaki linklerde (`<a>` etiketleri) `asp-controller` ve `asp-action` attribute'ları birlikte ve açıkça belirtilmelidir. Tek başına `asp-action` kullanımı Route çözümleme hatalarına neden olabilir.
*   **Minimal Logic:** Controller içinde iş mantığı veya veri manipülasyonu yapılmamalı; sadece ilgili View döndürülmelidir.
*   **Thin Action (No-ViewModel):** Action metotları asla C# `ViewModel` nesnesi doldurmamalıdır. Controller sadece boş bir "UI Shell" (Razor View) döndürmekle görevlidir. 
    *   *Kritik Kural:* Veri (Tablo verisi, Lookup/Drowdown listeleri, Detay bilgileri) asla Razor tarafında `@model` ile taşınmaz; tamamı AJAX/Fetch ile frontend tarafında yönetilir.

---

## 2. Partial View Standartları (Razor)

Karmaşık ekranların yönetilebilirliğini artırmak için **Partial View** yapısı standarttır.

*   **Adlandırma Kuralları:** Tüm partial view dosyaları alt çizgi (`_`) ile başlamalıdır.
    *   *Örnek:* `_Filter.cshtml`, `_CreateModal.cshtml`.
*   **Modüler Bölümleme:** Ekranın büyük parçaları (Modallar, Filtreler, Sidebar bileşenleri) bağımsız partial'lara bölünmelidir.
*   **Single Responsibility:** Her partial view sadece kendi UI parçasından sorumlu olmalı; sayfa genelini etkileyen script'ler veya stil blokları ana view'da toplanmalıdır.

---

## 3. API Bağlantı Standartları (Single Source of Truth)

API bağlantılarında hardcoded port veya domain kullanımından kaçınılmalıdır.

*   **API Profile Kararı:** Her DataTable modülü `proxy-profile` veya `direct-gateway-profile` ile üretilir.
    *   *proxy-profile (Platform/admin MVC default):* Browser JS sadece same-origin frontend proxy'ye gider (`/{AreaName}/{ModuleName}/api`). Frontend controller bu isteği server-side `GatewayUrl` üzerinden Gateway `5000` rotasına taşır.
    *   *direct-gateway-profile:* Sadece browser-safe token stratejisi olan tenant/public shell ekranlarında kullanılır ve merkezi `window.API.{service}` objesine gider.
*   **HttpOnly Auth Kuralı:** DataTable JS içinde `document.cookie`, `access_token` veya `Authorization: Bearer` üretimi yasaktır. HttpOnly cookie yalnızca MVC controller/proxy tarafından server-side okunur.
*   **Gateway Awareness:** Browser JS asla mikroservis portlarına (`5056`, `5057`, `5058`) doğrudan gitmez. `proxy-profile` kullanıldığında browser Network kaydında Gateway değil same-origin frontend proxy görünür; Gateway çağrısı server-side yapılır.

---

## 4. JavaScript ve CSS Standartları

### 4.1 JavaScript Paternleri
*   **Strict Mode:** Tüm JS dosyaları `'use strict';` ile başlatılmalıdır.
*   **Module Pattern:** Her sayfa/modül kendi IIFE (Module Pattern) yapısı içinde izole edilmelidir (Örn: `const ProductList = (function() { ... })();`).
*   **Initialization:** Kodlar DOM hazır olduktan sonra çalışmalıdır (DOMContentLoaded).
*   **Error Handling:** Tüm asenkron (`fetch`) işlemler `try-catch` blokları içinde ele alınmalı, hata durumunda `window.showToast` ile geri bildirim verilmelidir.

### 4.2 CSS Yazım Standartları
*   **Bootstrap Önceliği:** Stil düzenlemelerinde öncelikle Bootstrap utility sınıfları (`d-flex`, `text-center`, `mb-4`) kullanılmalıdır.
*   **Semantic Badge Classes:** Durum ve etiket gösterimlerinde Sneat/Bootstrap label sınıfları (`bg-label-primary`, `bg-label-success`) standarttır.
*   **No Hardcoded Colors:** Tüm renkler CSS değişkenleri (`var(--bs-*)`) üzerinden yönetilmelidir.

---

## 🏛️ DataTable ve UI Spesifikasyonları (Detay)

### UI-001: DataTable Config & DtDefaults
- Tüm DataTable'lar `window.DtDefaults.create()` ile oluşturulur.
- Inline CSS yerine `backbone-custom.css` içindeki paylaşımlı sınıflar kullanılır.

> [!IMPORTANT]
> Bu döküman, projenin teknik kalitesini ve sürdürülebilirliğini korumak amacıyla hazırlanmıştır. Yeni geliştirilecek tüm modüller bu modüler yapıyı ve kod standartlarını takip etmelidir.

---

## 🌍 Localization (L10N)

### L10N-001: Layout L10n Coverage
- `_LayoutPlatformAdmin.cshtml` ve `_LayoutTenantShell.cshtml` içindeki tüm metinler `@SharedLocalizer["Key"]` ile dile bağlanır.

### L10N-002: Universal Coverage (8 Languages)
- Yeni eklenen her Key, sistemdeki **tüm 7 dil dosyasına** (`en, fr, es, zh, ar, ru, tr`) eksiksiz eklenmelidir.
- Diğer dillerde metnin "Key" ismiyle görünmesi kabul edilemez.

---

## 🛠️ Input Kısıtlamaları (MOD-0023)

### UI-017: Input Restrictions
- **Numeric Only:** `.numeric-only` sınıfı ile sadece rakam girişi.
- **Phone Mask:** `.phone-mask` sınıfı ile telefon formatı kısıtlaması.
- HTML5 types (`type="email"`, `type="url"`, `type="tel"`) zorunludur.

---

## 5. UI/UX Bütünlüğü ve Detay Standartları (MOD-0024)

Bu kurallar, projedeki görsel tutarlılığı (consistency) korumak için ZORUNLUDUR.

### UI-020: Zorunlu Alan İşaretleri (Required Markers)
- Backend `Validator` sınıfları (`FluentValidation`) içinde `NotEmpty()` veya `NotNull()` olarak tanımlanan tüm alanların UI tarafındaki `<label>` etiketlerine `<span class="text-danger">*</span>` eklenmesi ZORUNLUDUR.
- Bu marker, label metninin hemen sağında ve bir boşluk bırakılarak yer almalıdır.
- Required kontratı tek kaynaklı olmalıdır: Backend validator `NotEmpty()`/`NotNull()`, Web ViewModel `[Required]`, Razor `required` attribute'u, label yıldızı ve global required-fields tracker aynı alan listesini göstermelidir.
- Opsiyonel alanlarda `[Required]`, HTML `required`, label yıldızı veya otomatik `data-val-required` üretecek non-nullable value type kullanılmaz. Opsiyonel numeric/date alanlar Web ViewModel'de nullable olmalıdır (`int?`, `decimal?`, `DateTime?`, vb.).
- Backend request DTO'su opsiyonel bir alanı nullable tanımlıyorsa Web ViewModel ve save payload da nullable olmalıdır. Örnek: backend `int? SortOrder` ise frontend `SortOrder` da `int?` olmalıdır; boş bırakılırsa API/handler default değeri uygular.
- Global required-fields tracker sonucu Create ekranı ilk açıldığında açıklanabilir olmalıdır: paydadaki sayı yalnızca gerçek required alan sayısıdır; paydaki başlangıç dolu değerler yalnızca bilinçli default verilen required alanlardan gelebilir.

### UI-021: Gelişmiş Filtreleme (Multi-Select Mandate)
- Kategori, Ürün Tipi, Durum, Departman gibi "Sınıflandırma" odaklı filtreler daima **Multi-Select (Select2)** olarak tasarlanmalıdır.
- Tekli seçim (`single select`) sadece mantıksal olarak "kesinlikle tek bir seçenek" gerektiren durumlarda kullanılır.
- Filtrelerde `multiple="multiple"` özniteliği ve `dt-defaults.js` içindeki summary chip tasarımı kullanılmalıdır.

### UI-022: Sayfa Başlığı ve Breadcrumb Yerleşimi
- Tüm liste ve detay sayfaları, projedeki standart **Page Header** yapısını kullanmalıdır.
- Breadcrumb ile sayfa başlığı (`h5`) arasındaki dikey boşluk ve tıklama alanları (hover areas) standart CSS sınıfları ile korunmalıdır. 
- Breadcrumb item'ları üzerine gelindiğinde (hover) oluşan alanın, altındaki içerik tarafından kapatılmadığı (`z-index` kontrolü) doğrulanmalıdır.

### UI-023: Modal & JS Senkronizasyonu
- Eğer bir JavaScript dosyasında (`index.js`, `form.js`) bir Modal id'sine (`#saveViewModal`, `#editModal` vb.) referans veriliyorsa, bu modalın HTML iskeletinin ilgili View dosyasında (veya bir partial içinde) bulunması ZORUNLUDUR.
- JS trigger'ları, olmayan modal id'lerine bağlanarak sistemin "sessizce hata" (silently failing) vermesine izin verilmez.

### UI-024: Compact Form / Details Bilgi Mimarisi Paritesi
- `golden_reference: compact` olan modüllerde `Details.cshtml` ve `_Form.cshtml` aynı mantıksal bölüm haritasını kullanmalıdır.
- Create/Edit ekranları, Details ekranındaki logical section sayısını ve alan sahipliğini korur. Örneğin Details `Identity`, `Description`, `Classification`, `Status` olarak dört bölümden oluşuyorsa `_Form.cshtml` de dört ayrı card/section üretir.
- Farklı logical bölümleri aynı card içine toplamak, formu Details'tan daha az sayıda ana card ile sunmak veya alanı Details'taki bölümünden farklı bir bölümde göstermek kabul edilmez.
- Bu kontrol yalnızca dosya varlığı kontrolü değildir; render edilen UI yüzeyi ve Razor section/card yapısı birlikte doğrulanmalıdır.

---

## 🛡️ Production Safety

### PROD-001: Layout & ViewStart Freeze
- `_Layout.cshtml` ve kök `_ViewStart.cshtml` değiştirilmez; archive uyumluluğu korunur.
- Geliştirmeler `backbone-custom.css` üzerinden yapılır.

### PROD-005: Shell Ayrımı (Platform Admin vs Tenant)
- Yeni bir modül eklenirken **shell ataması zorunludur**: modül ya **platform admin** ya da **tenant** shell'ine bağlanır, asla ikisine birden değil.
- Layout seçimi:
  - **Admin modülleri** → `Layout = "_LayoutPlatformAdmin";` ve view `Views/Platform/{Controller}/` altına konur. Controller route'u `[Route("Platform/[controller]")]` olur. `Views/Platform/_ViewStart.cshtml` zaten admin layout'u atadığı için view'da manuel `Layout = ` opsiyoneldir.
  - **Tenant modülleri** → `Layout = "_LayoutTenantShell";` ve view kök Views altına konur (`Views/{Controller}/` veya alan altında). Controller route'u `Platform` prefix'i içermez.
- Sidebar yerleştirme:
  - Admin modülü → `_LayoutPlatformAdmin.cshtml` içindeki `Platform Administration` listesine `<li class="menu-item">` eklenir.
  - Tenant modülü → `_LayoutTenantShell.cshtml` içinde uygun `menu-header` (Workspace, Developer Sandbox vb.) altına eklenir.
- Bir modülün her iki sidebar'da birden görünmesi YASAKTIR. Aynı modülün hem `/Platform/...` hem kök URL'de servis edilmesi YASAKTIR.
- Legacy `_Layout.cshtml` (812 satır) yalnızca eski tenant modülleri (ESBP, DemandIdeas, InventoryGovernance, DeliveryExecutionManagement, ManagementGovernance, DecompositionTreeBuilder) tarafından kullanılır; bu dosya freeze altındadır ve yeni modül onu kullanamaz.

### PROD-004: Archive Freeze
- `Views/Archive/` altındaki dosyalar refactor planı olmadan değiştirilmez.

---
