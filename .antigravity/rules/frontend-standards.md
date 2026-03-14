---
description: "FRONT-001 — Diten.Web Frontend Katmanı Zorunlu UI/UX ve Kodlama Standartları (MOD-0013, MOD-0022, MOD-0023, MOD-0024 Genişlemeleri)"
---

# Frontend Standards (Diten ERP vNext)

Bu dosya, Diten.Web frontend katmanı için zorunlu kuralları tanımlar. Tüm ajanlar bu kurallara uymak zorundadır.

---

## 🎨 CSS Kuralları

### CSS-001: No Hardcoded Colors
- Tüm renk referansları `var(--bs-*)` CSS variables veya Sneat class'ları (`bg-label-*`, `text-*`) üzerinden olmalı.
- Hardcoded hex değerleri (`#e74c3c`, `#ff4c51` vb.) yasaktır.
- **İstisna:** `_GlobalNotification.cshtml` ve `_GlobalConfirmation.cshtml` içindeki mevcut tanımlar (legacy).

### CSS-002: Font-Size Freeze
- `html { font-size }` tanımına **dokunulmaz**.
- Sneat'in `16px` rem bazı korunmalıdır.
- `site.css` dosyası `_LayoutBackbone`'da yüklenmez; sadece modern `backbone-custom.css` kullanılır.

### CSS-003: No Focus Override
- `.btn:focus`, `.form-control:focus` gibi focus ring override'ları yapılmaz.
- Sneat'in merkezi focus tanımları geçerlidir.

### CSS-004: DataTable Cellfit Columns
- Bulk checkbox ve Actions gibi sabit genişlikli kolonlar ColVis ile diğer kolonlar gizlendiğinde **genişlememeli**dir.
- Bu kolonlara `cellfit` class'ı verilir ve CSS tanımı `backbone-custom.css` içinde yapılır.
- Inline `style` ile genişlik verilmesi **yasaktır**; bunun yerine `cellfit` class'ı kullanılır.

### CSS-005: Responsive Layout via CSS Media Queries (MOD-0022)
- DataTable header responsive düzeltmeleri **yalnızca CSS** ile (`backbone-custom.css` içinde `@media` query) yapılır.
- JavaScript (`dt-defaults.js`) responsive layout amaçlı class ekleme/çıkarma yapmamalıdır.
- CSS düzeltmeleri masaüstü görünümünü **kesinlikle bozmamalıdır**; tüm kurallar media query (`@media screen and (max-width: 991.98px)`) içinde kapsamlanır.
- `display: contents` tekniği, `.dt-layout-end` hücresini mobilde eriterek çocuklarının (Search, Buttons) üst satırın doğrudan flex item'ları olmasını sağlar.

### CSS-006: Unobtrusive Form Validation Feedback
- ASP.NET Core Unobtrusive Validation'ın ürettiği `.input-validation-error` sınıfı için merkezi tanımlar (`backbone-custom.css`) geliştirilmiştir.
- Hatalı alanlar mutlaka **danger** (`var(--bs-danger)`) rengiyle kırmızı sınırlara (border) ve odaklanma anında (`:focus`) kırmızı estetik gölgelere (`box-shadow`) sahip olmalıdır.
- Hata durumları için sayfa özelinde veya satır içi (inline) CSS yazılması **kesinlikle yasaktır**.

---

## ⚙️ JavaScript Kuralları

### JS-001: Window Scope Guard
- Yeni sayfa JS'leri `window` objesine yalnızca şu standart anahtarları ekleyebilir:
  - `window.L10n` (L10n bridge)
  - `window.showToast`, `window.showConfirm`
  - `window.ApiBaseUrl`, `window.DtDefaults`
- Bunlar dışında `window.*` ataması **yasaktır**. Module pattern veya IIFE kullanılmalıdır.

### JS-002: Module Pattern for Page Scripts
- Her sayfa için özel hazırlanan JavaScript dosyaları (örn: `index.js`, `create.js`) **Module Pattern** yapısında olmalıdır.
- Kod doğrudan `DOMContentLoaded` içine yazılmaz; bir Manager/List objesi (örn: `LegalEntitiesList`) içinde fonksiyonel parçalara bölünür.
- Sayfa yüklendiğinde sadece bu objenin `init()` metodu çağrılır.

### JS-003: Name-Based Column Access
- DataTable kolonlarına erişirken sabit indis (`column(7)`) kullanılmamalıdır.
- Kolon tanımlarına mutlaka `name` özelliği verilmeli ve erişim `api.column('name:name')` şeklinde yapılmalıdır.

---

## 🏛️ UI ve DataTable Standartları

### UI-001: DataTable Central Config (Sneat 2.x Layout API)
- Her yeni DataTable sayfası `window.DtDefaults.create({...})` ile initialize edilir.
- Eski `dom` string kullanımı **yasaktır**. Sneat 2.x `layout` API kullanılır.
- `DtDefaults.create()` otomatik olarak:
    - `#skeleton-loader`'ı `initComplete`'te gizler.
    - Sneat class düzeltmelerini `drawCallback` üzerinden uygular.
    - Hover Effect (`table-hover`) otomatik eklenir.

### UI-020: Skeleton Loader Lifecycle
- DataTable liste sayfalarında `#skeleton-loader` bloğu zorunludur (bkz: `views-organization.md`).
- Skeleton show/hide davranışı merkezi olarak `DtDefaults.create()` ile yönetilir (`preXhr` + `drawCallback`).
- Sayfa bazlı skeleton show/hide hack'leri ancak özel UX ihtiyacı varsa kabul edilir.

### UI-021: Template Customizer Dependencies (Pickr)
- `_LayoutBackbone.cshtml` içinde `template-customizer.js` kullanılıyorsa `Pickr` global'ı da yüklenmelidir; aksi halde console'da `Pickr is not defined` hatası alınır.
- Zorunlu vendor dosyaları: `assets/vendor/libs/pickr/pickr.js` + `assets/vendor/libs/pickr/pickr-themes.css`.

### UI-011: DataTable Responsive Header Layout (MOD-0022)
- **Breakpoint:** `@media (max-width: 991.98px)`
- **Row 1:** Length (100) solda, Search sağda — aynı satırda.
- **Row 2:** Export, Import, ColVis, Filter ve Add butonu — **full-width** yayılır.
- Butonlar mobilde tek grup yapılmaz; mevcut 3'lü grup yapısı korunur.

### UI-012: DataTable Button Group Architecture
- `DtDefaults.exportButtons()` butonları ayrı feature grupları olarak döner:
    - **Grup 1:** Export + Import
    - **Grup 2:** ColVis + Filter
    - **Grup 3:** Add New
- Tüm butonlar birleştirilmemelidir (tek bir mega btn-group yapılmaz).

### UI-002: DataTable Filtering (Offcanvas Pattern)
- Tablo filtreleri için sağ taraftan açılan `#offcanvasFilter` kullanılır.
- Filtre kodu ayrı bir `_Filter.cshtml` partial view içerisinde tutulmalıdır.
- Filtreleme işlemi açık bir **Apply** (`btn-primary`) butonu ile tetiklenmelidir.
- "Apply" butonuna tıklandığında offcanvas otomatik kapatılmalıdır.

### UI-004: Global Confirmation Standards (SweetAlert2)
- Tüm silme veya kritik işlem onayları için `window.showConfirm(key, callback, entityName)` kullanılır.
- Onay modalı tasarımı:
    - İkon ve Başlık: `justify-content: center` ile tam ortalı.
    - Entity adı: `badge bg-label-primary` içinde.
    - Butonlar arası boşluk `mx-2` ile sağlanır.

### UI-015: Unified Form Progress & Validation Tracker (MOD-0024)
- Form sayfalarında doluluk ve doğruluk oranını takip eden `required-fields-tracker.js` kullanılır.
- Rozet Davranışı:
    - 🔴 **Kırmızı:** Eksik zorunlu alan VEYA format hatası varsa.
    - 🟡 **Sarı:** Zorunlu alanlar tamam ama format hataları varsa.
    - 🟢 **Yeşil:** Tamamen eksiksiz ve hatasız.

### UI-013: Form Pages Grid & Layout
- Form sayfalarında `col-lg-10 mx-auto` **kullanılmaz**; kartlar `col-12` içinde tam genişlikte olmalıdır.
- Sütunları sarmalayan Row'lar her zaman `<div class="row g-6">` (`g-6` kritik) olmalıdır.
- Kart başlıkları ikon içerdiğinde `d-flex align-items-center` kullanılmalıdır.
- Yükseklik dengesi için yan yana gelen farklı kartlara `h-100` eklenmelidir.

### UI-010: State Persistence & Visual Feedback (StateSave)
- Tüm liste sayfalarında `stateSave: true` zorunludur.
- Aktif filtre/arama varsa `window.DtDefaults.updateVisualState(api, filterCount)` ile görsel bildirim (badge, border vurgusu) sağlanmalıdır.

---

## 🌍 Localization (L10N)

### L10N-001: Layout L10n Coverage
- `_LayoutBackbone.cshtml` içindeki tüm metinler `@SharedLocalizer["Key"]` ile dile bağlanır.

### L10N-002: Universal Coverage (8 Languages)
- Yeni eklenen her Key, sistemdeki **tüm 8 dil dosyasına** (`en, tr, ru, es, ka, kk, uk, uz`) eksiksiz eklenmelidir.
- Diğer dillerde metnin "Key" ismiyle görünmesi kabul edilemez.

---

## 🛠️ Input Kısıtlamaları (MOD-0023)

### UI-017: Input Restrictions
- **Numeric Only:** `.numeric-only` sınıfı ile sadece rakam girişi.
- **Phone Mask:** `.phone-mask` sınıfı ile telefon formatı kısıtlaması.
- HTML5 types (`type="email"`, `type="url"`, `type="tel"`) zorunludur.

---

## 🛡️ Production Safety

### PROD-001: Layout & ViewStart Freeze
- `_Layout.cshtml` ve `_ViewStart.cshtml` değiştirilmez; archive uyumluluğu korunur.
- Geliştirmeler `backbone-custom.css` üzerinden yapılır.

### PROD-004: Archive Freeze
- `Views/Archive/` altındaki dosyalar refactor planı olmadan değiştirilmez.

---
