# Frontend Standards (MOD-0013 Genişlemesi)

Bu dosya, Diten.Web frontend katmanı için zorunlu kuralları tanımlar.
Tüm ajanlar bu kurallara uymak zorundadır.

---

## CSS Kuralları

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

---

## JavaScript Kuralları

### JS-001: Window Scope Guard
- Yeni sayfa JS'leri `window` objesine yalnızca şu standart anahtarları ekleyebilir:
  - `window.L10n` (L10n bridge)
  - `window.showToast` (Toast sistemi — sadece partial tarafından)
  - `window.showConfirm` (Modal sistemi — sadece partial tarafından)
  - `window.ApiBaseUrl` (API kök URL — sadece Layout tarafından)
  - `window.DtDefaults` (DataTable merkezi config — sadece dt-defaults.js tarafından)
- Bunlar dışında `window.*` ataması **yasaktır**. Module pattern veya IIFE kullanılmalıdır.

---

## Asset Kuralları

### ASSET-001: Favicon Set
- Deploy öncesinde tam favicon seti (favicon.ico, favicon-32x32.png, apple-touch-icon.png) zorunludur.

### ASSET-002: SVG-First
- Logo ve ikon varlıkları SVG formatında olmalıdır. PNG sadece fotoğraf içeriği için kullanılır.

### PERF-001: Asset Size Limit
- Yeni eklenen her görsel ≤100 KB olmalıdır.
- >100 KB görseller için WebP formatı ve lazy-load (`loading="lazy"`) zorunludur.

---

## Build Kuralları

### BUILD-001: Minify & Cache-Bust
- `_LayoutBackbone.cshtml` içindeki tüm `<link>` ve `<script>` tag'lerine `asp-append-version="true"` eklenir.
- Production build'de CSS/JS dosyaları minify edilmelidir.

---

## UI Kuralları

### UI-001: DataTable Central Config (Sneat 2.x Layout API)
- Her yeni DataTable sayfası `window.DtDefaults.create({...})` ile initialize edilir.
- Eski `dom` string kullanımı **yasaktır**. Sneat 2.x `layout` API kullanılır:
  - `topStart`: pageLength seçici
  - `topEnd`: search bar + export + add-new butonları
  - `bottomStart`: info ("Showing X to Y")
  - `bottomEnd`: pagination (chevron ikonlu)
- `DtDefaults.create()` otomatik olarak:
  - Layout yapısını inject eder
  - `#skeleton-loader`'ı `initComplete`'te gizler (`fadeOut(300)`)
  - Sneat class düzeltmelerini uygular (setTimeout, `btn-secondary` kaldırma vb.)
- Export butonları `DtDefaults.exportButtons()` factory'si ile oluşturulur.
- "Add New" butonu `DtDefaults.exportButtons('Button Text', { attrs })` ile DataTable'a gömülür — card-header'a ayrıca eklenmez.

### UI-002: DataTable Filtering (Offcanvas Pattern)
- Tablo filtreleri için sağ taraftan açılan Bootstrap Offcanvas (`#offcanvasFilter`) kullanılır.
- Filtre butonu DataTable toolbar'ına (Search yanına) icon-only (`bx-filter-alt`) olarak eklenir.
- Offcanvas içinde mutlaka bir **Reset** (`btn-label-danger`) butonu bulunmalıdır.
- Filtreleme işlemi asenkron (`dt.draw()`) yapılmalı, sayfa yenilenmemelidir.

### UI-003: Skeleton Shimmer Standards
- Yükleme durumları için `backbone-custom.css` içindeki `.shimmer` class'ı kullanılır.
- Skeleton, tablonun Toolbar'ını kapatmamalı, sadece veri alanını (`top: 72px` veya benzeri bir offset ile) örtmelidir.
- `min-height: 200px` kuralı hem Skeleton görünürlüğü hem de CLS (Layout Shift) engelleme için zorunludur.

### L10N-001: Layout L10n Coverage
- `_LayoutBackbone.cshtml` içindeki tüm metinler `@SharedLocalizer["Key"]` ile dile bağlanır.
- Statik metin (`My Profile`, `Settings` vb.) yazılması yasaktır.
- `_Layout.cshtml` bu kurala tabi değildir (frozen).

---

## Referans Kuralları

### REF-001: Sneat Reference Template
- Projede `frontend/_Reference/Theme/full-version/` altında Sneat Admin PRO template'inin tam sürümü bulunur.
- Yeni sayfa oluştururken ilgili referans dosyası incelenir:
  - Liste sayfası → `html/vertical-menu-template/app-user-list.html` + `assets/js/app-user-list.js`
  - Form sayfası → ilgili `app-*-add.html` veya `app-*-edit.html`
- Bu dosyalar **read-only** referanstır. Doğrudan kopyalanmaz, Razor + L10n yapısına **adapte** edilir.
- CSS class'ları, DOM hiyerarşisi ve JS pattern'ler bu referansla uyumlu olmalıdır.

---

## Production Safety Kuralları

### PROD-001: Layout Freeze
- `_Layout.cshtml` dosyası **değiştirilmez**. Archive sayfaları bu layout'a bağımlıdır.

### PROD-002: ViewStart Freeze
- `_ViewStart.cshtml` dosyası **değiştirilmez**. Default layout `_Layout` olarak kalır.

### PROD-003: site.css Freeze
- `wwwroot/css/site.css` dosyası **değiştirilmez**. `_LayoutBackbone` bu dosyayı yüklemez.

### PROD-004: Archive Freeze
- `Views/Archive/` ve `wwwroot/assets/js/Archive/` altındaki dosyalar **değiştirilmez** (refactor planı olmadan).
