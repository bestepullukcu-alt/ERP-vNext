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
- Sayfa bazlı `.btn:focus`, `.form-control:focus` gibi focus ring override'ları yapılmaz.
- Sneat'in merkezi focus tanımları geçerlidir.
- **İstisna:** `#inlineFilterHost` altındaki Select2 trigger'ları için, vendor shadow yerine `backbone-custom.css` içinde tanımlanan standart focus görünümü kullanılabilir. Bu override sayfa içinde tekrar yazılamaz.

### CSS-003b: DataTable Row Selection & Hover (MOD-0018)
- DataTable satır seçimi (`.selected` class) ve hover renkleri, DataTables/Bootstrap'ın varsayılan agresif mavisini ezmek için `backbone-custom.css` içinde merkezi olarak tanımlanır.
- Selector **mutlaka** `[class*="datatables-"]` ile yazılır; modül adına özgü (`datatables-countries`, `datatables-legal-entities`) class'larla **yazılmaz**. Aksi hâlde yeni her modülde kural tekrar eklenmesi gerekir.
- Kural kapsamı: seçili satır arka planı (`0.08` opacity), normal hover (`0.04`), seçili+hover (`0.12`), `box-shadow` sıfırlama, `color: inherit`.
- Sayfa bazlı override **yasaktır**; tüm DataTable sayfaları bu merkezi kuraldan otomatik yararlanır.

### CSS-004: DataTable Cellfit Columns
- Bulk checkbox ve Actions gibi sabit genişlikli kolonlar ColVis ile diğer kolonlar gizlendiğinde **genişlememeli**dir.
- Bu kolonlara `cellfit` class'ı verilir ve CSS tanımı `backbone-custom.css` içinde yapılır.
- Inline `style` ile genişlik verilmesi **yasaktır**; bunun yerine `cellfit` class'ı kullanılır.

### CSS-005: Responsive Layout via CSS Media Queries (MOD-0022)
- DataTable header responsive düzeltmeleri **yalnızca CSS** ile (`backbone-custom.css` içinde `@media` query) yapılır.
- JavaScript (`dt-defaults.js`) responsive layout amaçlı class ekleme/çıkarma yapmamalıdır.
- CSS düzeltmeleri masaüstü görünümünü **kesinlikle bozmamalıdır**; tüm kurallar media query (`@media screen and (max-width: 991.98px)`) içinde kapsamlanır.
- `display: contents` tekniği, `.dt-layout-end` hücresini mobilde eriterek çocuklarının (Search, Buttons) üst satırın doğrudan flex item'ları olmasını sağlar.
- **Export Dropdown UI Notu (XS):** Toolbar’da `.btn-icon` (kare ikon buton) ile `extend:'collection'` Export butonu aynı grupta kullanıldığında, Export görsel olarak “üstten-alttan küçük” kalabilir. Bu durumda Export butonuna `dt-export-collection-btn` class’ı verilir ve responsive toolbar CSS’i Export’u ikon buton yüksekliğiyle hizalar.
- **Badge Clipping (Mobile/Tablet):** Filter/ColVis badge’leri `top-0 end-0 translate-middle` ile butonun dışına taşar. Toolbar, `.card-datatable.table-responsive` (overflow) içinde olduğundan **z-index ile çözülemez**; çözüm `backbone-custom.css (MOD-0022)` içinde DataTable top row için **ek `padding-top` “safe area”** bırakmaktır. Bu kural kaldırılmaz.
- **Select2 Shadow (Inline Filter):** `#inlineFilterHost` içindeki Select2 focus/open durumunda vendor `box-shadow` (active ring) kullanılmaz; `backbone-custom.css` içinde shadow kapatılır. Amaç “chip” görünümünde temiz kenar estetiğidir.
- **Select2 Form-Select Estetiği (Inline Filter):** Inline filter Select2 tekli seçim yüzeyi, Sneat `form-select form-select-sm` estetiğine yakın yükseklik/border/padding ile `backbone-custom.css` içinde standardize edilir.
- **Select2 Inline Filter Scroll & Jump Bug (MOD-0031):** Inline filter chip'lerinde Select2'nin focus/init davranışları sayfa sıçramasına (vertical jump) ve yatay taşmaya (horizontal jump) neden olur. **Zorunlu ve Kesin Çözüm:**
    1. **JavaScript:** `dropdownParent: $(document.body)` + `minimumResultsForSearch: Infinity` + `width: 'element'` kombinasyonu kullanılır. (Semtptom-bazlı scroll-restore kodları YASAK.)
    2. **CSS (Root Fix):** `.select2-search__field` viewport köşesine `position: fixed` ile sabitlenmeli (focus anında sıçramayı engeller) ve `body > .select2-container` genişliği `auto`/`max-width: 100vw` ile sınırlandırılmalıdır (yatay taşmayı engeller). Kurallar `backbone-custom.css` içindedir.
- **Select2 Border Width (Inline Filter):** Vendor Select2 focus/open durumunda `border-width: 2px` yapabilir; inline filter chip’lerinde layout shift/scroll tetiklemesin diye `border-width: 1px` sabitlenir (kural `backbone-custom.css`).
- **Select2 Chevron & Clear Alignment (Premium Standard):** Filtre chiplerinde ok (chevron) ve temizleme butonunun (x) çakışmaması ve her iki tipte (single/multi) görsel tutarlılık sağlanması için şu hiyerarşi zorunludur:
    - **Chevron (Ok):** Her durumda `.select2-selection__arrow::after` üzerinden çizilir. Konumu sabit `right: .7rem`'dir.
    - **Single-Select Clear (x):** Okun solunda, `right: 1.85rem` konumunda olmalıdır.
    - **Multi-Select Actions (Count + Clear):** Okun solunda, grup olarak `right: 1.95rem` konumunda başlamalıdır.
    - **Padding:** Multi-select özet metni, sağdaki interaktif alanla çakışmaması için `right: 3.85rem` padding kullanmalıdır.
- **Shared CSS Placement:** `#inlineFilterHost`, `.dt-layout-end`, badge stacking, Select2 dropdown/search/result ve benzeri tekrar kullanılabilir DataTable/UI stilleri page-level `@section Styles` içinde tutulmaz; merkezi olarak `backbone-custom.css` içinde yaşar.

### CSS-006: Unobtrusive Form Validation Feedback
- ASP.NET Core Unobtrusive Validation'ın ürettiği `.input-validation-error` sınıfı için merkezi tanımlar (`backbone-custom.css`) geliştirilmiştir.
- Hatalı alanlar mutlaka **danger** (`var(--bs-danger)`) rengiyle kırmızı sınırlara (border) ve odaklanma anında (`:focus`) kırmızı estetik gölgelere (`box-shadow`) sahip olmalıdır.
- Hata durumları için sayfa özelinde veya satır içi (inline) CSS yazılması **kesinlikle yasaktır**.

### CSS-007: Standard Page Header Spacing
- Her sayfanın ana başlık div'i (header) alt boşluk olarak `mb-4` (1rem) kullanmalıdır.
- Sayfanın en üst boşluğu (padding-top) Backbone template'inde `backbone-custom.css` üzerinden **16px** (1rem) olarak override edilir. Vendor `core.css` editlenmez.
- Not: `mb-6` global olarak yasak değildir; sadece **Page Header wrapper** için `mb-4` standardı uygulanır.
- Breadcrumb kullanılıyorsa `mt-2` kullanılmaz (üst boşluk verilmez). `nav` üzerinde sadece `text-muted` bırakılır.
- **Page Description kuralı:** Liste/Index sayfalarında başlık altında `@Localizer["PageDescription"]` gösterilir. Breadcrumb bulunan Create/Edit/Details sayfalarında generic `PageDescription` kullanılmaz.
- **Genel Kural (Breadcrumb'a bağlı):** Sayfada breadcrumb **yoksa** başlık altında `@Localizer["PageDescription"]` göstermek zorunludur. Breadcrumb **varsa** `PageDescription` gösterilmez.
- **Inline Filter Spacing (MOD-0025):** `#inlineFilterHost` toolbar altına mount edildiğinde **host yatay padding standardı `px-6`**’dır (`mx-*` ile dışarı taşırılmaz). (Not: DataTable toolbar row padding standardı `px-3`’tür; host `px-6` olarak bırakılır.) Filtre paneli içindeki form sarmalayıcısı (`collapse` altındaki ilk `div`) üst boşluk olarak `pt-0` kullanmalıdır.

---

## ⚙️ JavaScript Kuralları

### JS-001: Window Scope Guard
- Yeni sayfa JS'leri `window` objesine yalnızca şu standart anahtarları ekleyebilir:
  - `window.L10n` (L10n bridge)
  - `window.showToast`, `window.showConfirm`
  - `window.ApiBaseUrl`, `window.DtDefaults`, `window.personalizationClient`
- Bunlar dışında `window.*` ataması **yasaktır**. Module pattern veya IIFE kullanılmalıdır.
- `window.L10n` veri besleme standardı: view içindeki inline assignment listesi yerine `_IndexL10n.cshtml` JSON payload + `index.l10n.js` merge pattern'i kullanılır.

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

### UI-002: DataTable Filtering (Inline Collapse Pattern)
- Tablo filtreleri için **offcanvas kullanılmaz**.
- Filtre kodu ayrı bir `_Filter.cshtml` partial view içerisinde tutulmalıdır.
- Filter butonuna basıldığında sayfa içinde, toolbar’ın hemen altında açılan **inline collapsible** panel kullanılır:
  - Host: `#inlineFilterHost`
  - Collapse: `#inlineFilterCollapse`
- Filtre kontrolleri kompakt “chip/dropdown” görünümünde olmalıdır (Select2).
- Filtre dropdown’larında arama (**search**) zorunludur.
- Filtreleme işlemi açık bir **Apply** (`btn-primary`) butonu ile tetiklenmelidir.
- "Apply" butonuna tıklandığında inline panel otomatik kapanmalıdır.
- "Reset" butonu paneli kapatmaz.

### UI-003: Save View (Default View)
- Toolbar’da `Save View` butonu bulunur fakat default **gizlidir**.
- Görünürlük kuralı: **Applied/effective table state** ile **Saved default state** farklıysa görünür; aynıysa gizlenir. (Staged filtre seçimleri Apply edilmeden Save View’u tetiklemez.)
- Persist hedefi localStorage değildir; shared personalization capability kullanılır:
  - Gateway route: `/api/personalization/*`
  - Frontend client: `window.personalizationClient`
  - Backend owner: `Diten.Platform`
- Save View state kapsamı:
  - **Kaydedilenler:** filtreler + search + column visibility + column order + sorting (varsa)
  - **Kaydedilmeyenler:** page number (pagination)
- Localization: `SaveView` metni **SharedResource** üzerinden gelir ve 9 dilde eksiksiz olmalıdır.

### UI-005: Column Reorder (v2)
- Kolon sürükle-bırak sıralama gerekiyorsa DataTables `ColReorder` kullanılır; custom sortable header hack’i yazılmaz.
- `ColReorder` sadece anlamlı data kolonlarında açılır; control/checkbox/actions kolonları reorder kapsamına alınmaz.
- `columnOrder` Save View state’inin bir parçasıdır ve refresh sonrası restore edilmelidir.
- `column-reorder.dt` / `columns-reordered.dt` event’leri dirty-state ve Save View görünürlüğünü güncellemelidir.

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
- **Legacy (v1):** Bazı mevcut liste sayfaları `stateSave: true` kullanıyor olabilir.
- **DataTable v2 Standard (data-dt-standard="v2") için otomatik cache YASAKTIR:**
  - `stateSave: false` zorunludur (2 saatlik otomatik cache / restore kaldırılır).
  - Kalıcılık yalnızca **Save View** mekanizmasıyla yapılır.
- Aktif filtre/arama varsa `window.DtDefaults.updateVisualState(api, filterCount)` ile görsel bildirim (badge, border vurgusu) sağlanmalıdır.

### UI-030: DataTable v2 Standard — State Model & Persistence (ZORUNLU)
Bu bölüm yalnızca `data-dt-standard="v2"` ile işaretlenmiş DataTable sayfaları için geçerlidir.

**A) State Tanımları**
- **baselineDefault:** savedView yokken referans alınan temiz başlangıç state’i:
  - filters: `''`, search: `''`, colVis: init default, sorting: init default (**single-sort**), pageLength: init default (**yalnız referans**)
- **currentState (staged/UI):** ekranda seçili state (Apply basılmadan değişebilir)
- **appliedState:** tabloya uygulanmış state (effective)
- **savedView:** Save View ile persist edilen default view

**B) Persistence Kapsamı**
- Otomatik state cache/stateSave YOK.
- Save View ile kaydedilenler: filters + search + colVis + columnOrder + sorting
- Kaydedilmeyenler: page number + pageLength

**C) Dirty-State (Save View görünürlük)**
- `isDirty = normalize(appliedState) != normalize(savedView || baselineDefault)`
- Tetikleyiciler:
  - Filter: **Apply / Reset** sonrası
  - search/colVis/sorting: **immediate apply**
- Apply: tabloyu günceller + paneli kapatır; Save View görünürlüğü appliedState’e göre güncellenir
- Reset: savedView varsa ona, yoksa baseline’a döner → Save View gizlenir

**D) normalize() Mekanik Kuralları**
- `null|undefined|''` → `''`, string: `trim()`
- filter primitive → string normalize (`1` == `"1"`, boolean → `"true"/"false"`)
- colVis: **index-based** `Array<boolean>` (dinamik kolon mutasyonu varsa explicit override)
- columnOrder: `Array<number>` ve tüm kolon indekslerini bir kez içermeli
- sorting: `Array<[index:number, dir:'asc'|'desc']>`; dir lower-case

**E) Refresh / Unapplied Changes**
- Apply basılmamış staged filtre değişiklikleri refresh ile persist edilmez.
- Refresh:
  - savedView yoksa baseline temiz state
  - savedView varsa savedView restore

### UI-031: DataTable v2 Standard — Responsive Öncelikleri (ZORUNLU)
- Search küçük ekranlarda **full-width** önceliklidir.
- Save View dar ekranda önce text kaybeder, sonra **icon-only** olur (tooltip + aria-label zorunlu).
- Add New primary action’dır; görünür kalır, wrap kontrollü olur (md altı icon-only).
- Export/Import grubu tek “block” gibi davranır; wrap kontrollü olur.
- Inline filter bar’da Apply/Reset tablet ve altı **alt satıra** geçebilir; mobilde eşit genişlikte yan yana olmalıdır.

### UI-032: Toolbar Stabilitesi (Hover / Z-Index / Clipping) (ZORUNLU)
- Toolbar’da hover sırasında konum değişimi (transform/translateY) YASAKTIR.
- Badge’ler (Filter/ColVis) hiçbir durumda kesilmez:
  - `overflow:hidden` kaynaklı clipping engellenir
  - z-index/stacking düzeni dropdown’larla çakışmaz
- Badge clipping fix’i, badge’i içeri taşıyarak ikonları kapatmak değil; **top row’da safe area** bırakmaktır (bkz: `backbone-custom.css` MOD-0022). “Sadece z-index artır” yaklaşımı kabul edilmez.
- Action group border-radius Save View görünür/gizli iki durumda da temiz ve tutarlı görünmelidir.

### UI-033: Accessibility (A11y) — Toolbar & Filter (ZORUNLU)
- Icon-only butonlarda `aria-label` + lokalize tooltip/title zorunludur.
- Filter trigger için `aria-controls="inlineFilterCollapse"` + `aria-expanded` zorunludur.
- Inline filter chip Select2'lerinde search input **kullanılmaz** (`minimumResultsForSearch: Infinity`). Search input DOM'a eklenince Select2 focus tetikler ve sayfa scroll yapar (MOD-0031).
- Keyboard navigation: Tab ile erişilebilirlik, ESC kapanış davranışı QA’da doğrulanmalıdır.

---

## 🌍 Localization (L10N)

### L10N-001: Layout L10n Coverage
- `_LayoutBackbone.cshtml` içindeki tüm metinler `@SharedLocalizer["Key"]` ile dile bağlanır.

### L10N-002: Universal Coverage (8 Languages)
- Yeni eklenen her Key, sistemdeki **tüm 9 dil dosyasına** (`az, en, tr, ru, es, ka, kk, uk, uz`) eksiksiz eklenmelidir.
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
