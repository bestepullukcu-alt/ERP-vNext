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

### JS-002: Module Pattern for Page Scripts
- Her sayfa için özel hazırlanan JavaScript dosyaları (örn: `index.js`, `create.js`) **Module Pattern** (veya IIFE) yapısında olmalıdır.
- Kod doğrudan `DOMContentLoaded` içine yazılmaz; bir Manager/List objesi (örn: `LegalEntitiesList`) içinde fonksiyonel parçalara (initDataTable, handleEvents vb.) bölünür.
- Sayfa yüklendiğinde (`DOMContentLoaded`) sadece bu objenin `init()` metodu çağrılır.
- Bu yaklaşım; kodun okunabilirliğini artırır, global scope kirliliğini önler ve gerektiğinde belli fonksiyonların (örn: tabloyu yenilemek) dışarıdan tetiklenmesine olanak tanır.

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
    - Layout yapısını inject eder.
    - `#skeleton-loader`'ı `initComplete`'te gizler.
    - Sneat class düzeltmelerini **`drawCallback`** üzerinden (her çizimde tazeleyerek) uygular.
    - **Responsive Renderer:** Mobil görünüm için gerekli olan detay tablosunu merkezi olarak oluşturur (`responsiveRenderer`). Sayfa içinde tekrar tanımlanması yasaktır.
    - **Hover Effect:** Tüm tablolar kullanıcı odaklanmasını artırmak için otomatik olarak `table-hover` sınıfına sahiptir.
- Export butonları `DtDefaults.exportButtons(addNewText, addNewAttr, extraButtons)` factory'si ile oluşturulur. Sayfaya özel butonlar (Filtre vb.) `extraButtons` dizisi olarak bu fonksiyona geçilmelidir.

### UI-002: DataTable Filtering (Offcanvas Pattern)
- Tablo filtreleri için sağ taraftan açılan Bootstrap Offcanvas (`#offcanvasFilter`) kullanılır.
- **Modülerlik:** Filtre offcanvas kodu her zaman ayrı bir `_Filter.cshtml` partial view içerisinde tutulmalıdır.
- **Tetikleyici:** Filtreleme işlemi input "change" olayında değil, açık bir **Apply** (`btn-primary`) butonu tıklandığında tetiklenmelidir (`dt.draw()`).
- **Kapatma:** "Apply" butonuna tıklandığında filtreleme ile birlikte offcanvas otomatik olarak kapatılmalıdır.
- **Görsel Standartlar:** 
    - Form elemanlarının `.filter-inputs-wrapper.mb-6` divi içine alınmalıdır.
    - Alt kısımdaki "Apply" ve "Reset" butonları arasında `gap-6` boşluğu bulunmalıdır.
    - Offcanvas panelinin içe bakan (leading) köşelerine `0.375rem` radius verilmeli ve bu stil `backbone-custom.css` içinde tanımlanmalıdır (satır içi stil kullanımından kaçınılmalıdır).
- **Reset:** Offcanvas içinde mutlaka bir **Reset** (`btn-label-danger`) butonu bulunmalıdır.
- **L10n:** "Apply" butonu her zaman `@SharedLocalizer["Apply"]` üzerinden lokalize edilmelidir.
- Filtreleme işlemi asenkron yapılmalı, sayfa yenilenmemelidir.

### UI-003: DataTable Native Loading (Processing) Standards
- Sayfa ilk açılışında veya AJAX işlemlerinde (filtreleme, silme, yenileme) DataTable'ın yerleşik `processing: true` mekanizması kullanılır.
- **Spinner Tasarımı:** Sneat standartlarına uyum için `sk-fold` (veya benzeri bir Spinkit bileşeni) kullanılmalıdır.
- Kod yapısında `language.processing` alanı üzerinden bu HTML tanımlanmalıdır.
- Bu yaklaşım, sadece sayfa açılışında değil, verinin her yenilendiği durumda otomatik olarak tetiklendiği için tercih edilmelidir. Özel statik skeleton loader'lardan kaçınılmalıdır.

### UI-004: Global Confirmation Standards (SweetAlert2)
- Tüm silme veya kritik işlem onayları için `window.showConfirm(key, callback, entityName)` kullanılır.
- Onay modalı tasarımı şu standartlara uymalıdır:
    - İkon ve Başlık: `justify-content: center` ve `w-100` ile tam ortalı.
    - Dinamik Veri: Silinecek öğenin adı (entityName) `badge bg-label-primary` içinde gösterilmelidir.
    - Butonlar: `gap-*` kullanılmaz, butonlar arası boşluk her iki butona verilen `mx-2` class'ı ile sağlanır.
    - "İptal" butonu `btn-label-secondary`, "Onay" butonu işlemin türüne göre (`danger`, `primary` vb.) seçilir.

### UI-005: Page Header & Description Standardı
- Liste ve Dashboard sayfalarının en üstünde (kartın dışında) bir başlık alanı bulunmalıdır.
- Yapı: `h4.mb-1` (Başlık) ve `p.mb-0` (Açıklama).
- Konteynır: `d-flex flex-column flex-md-row justify-content-between align-items-start align-items-md-center mb-6`.
- Tüm metinler sayfa bazlı lokalizasyon dosyasından (`@Localizer["..."]`) alınmalıdır.

### UI-006: Global Footer Standardı
- Alt bilgi (Footer) metni şu formatta sabitlenmiştir: `© 2018 | made with by Diten`.
- Emoji (kalp vb.) kullanımı ve yıl değişikliği standart dışıdır.

### UI-007: Temiz Dışa Aktarma (Export) Standartları
- Excel, PDF, CSV ve Yazdırma gibi işlemler sırasında tablodaki HTML etiketleri (`<a>`, `<span>` vb.) mutlaka temizlenmelidir (strip HTML).
- **Kolon Seçimi:** Dışa aktarma dosyalarında "Checkbox" ve "Actions" (İşlemler) kolonları bulunmamalı, sadece saf veri kolonları yer almalıdır.
- Tüm sayfalar `window.DtDefaults.exportButtons()` fabrikasını kullanarak bu standarda otomatik olarak uymalıdır.
- Bu standart, `dt-defaults.js` içindeki `commonExportOptions` nesnesi ile merkezi olarak yönetilir.

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

### JS-003: Name-Based Column Access
- DataTable kolonlarına erişirken sabit indis (`column(7)`) kullanılmamalıdır.
- Kolon tanımlarına mutlaka `name` özelliği verilmeli ve erişim `api.column('name:name')` şeklinde yapılmalıdır.
- Bu yaklaşım, tabloya kolon eklendiğinde veya sıralama değiştiğinde kodun kırılmasını engeller.

### UI-008: Advanced Filtering with Select2
- Tüm filtreleme dropdown'ları için standart HTML select yerine **Select2** kütüphanesi kullanılmalıdır.
- Offcanvas içindeki Select2 bileşenleri `dropdownParent: $('#offcanvasFilter')` parametresi ile başlatılmalıdır.
- Resetleme işlemi sırasında Select2 tetikleyicisi (`.trigger('change')`) unutulmamalıdır.

### UI-009: DataTable ColVis (Kolon Görünürlüğü)
- Tüm liste tablolarında kullanıcının kolonları gizleyip açabilmesi için **ColVis** özelliği aktif edilmelidir.
- **Varlık Yönetimi:** Dış bağımlılığı önlemek için `buttons.colVis.js` yerel olarak (`/assets/vendor/libs/datatables-buttons/`) yüklenmelidir.
- **Tasarım Standartları:**
    - ColVis butonu `.dt-colvis-btn` class'ına sahip olmalı ve yanındaki varsayılan dropdown oku (`::after`) `backbone-custom.css` üzerinden gizlenmelidir.
    - Tasarım "icon-only" (sadece göz ikonu) ve `btn-label-secondary` stilinde olmalıdır.
- **İçerik Filtreleme:** Kullanıcı deneyimini bozmamak adına; "Responsive Control", "Checkbox" ve "Actions" gibi sistem kolonları ColVis listesinden `columns: [...]` parametresi ile hariç tutulmalıdır. Sadece ana veri kolonları listelenmelidir.

### UI-010: DataTable State Persistence & Visual Feedback (StateSave)
- **Kalıcılık (stateSave):** Tüm liste sayfalarında kullanıcının arama, sayfalama, sıralama ve kolon görünürlüğü tercihleri `stateSave: true` ile tarayıcı hafızasında (localStorage) saklanmalıdır.
- **Görsel Bildirim Standartları:** Kullanıcının aktif bir filtre veya arama uyguladığını anlaması için `window.DtDefaults.updateVisualState(api, filterCount)` fonksiyonu kullanılmalıdır.
    - **Filtre Butonu:** Aktif filtre varsa buton `btn-label-primary` rengine döner ve sağ üst köşesinde seçili filtre sayısını gösteren bir `badge` belirir.
    - **Search (Arama):** Arama kutusunda metin varsa kutunun kenarlığı ve arka planı vurgulanır.
    - **ColVis:** Kullanıcı bir sütunu gizlediyse, "Göz" ikonu üzerinde küçük bir mavi bildirim noktası (`badge-dot`) gösterilir.
- **Sıfırlama (Reset):** "Reset" işlemi sadece tabloyu değil, tarayıcı hafızasındaki state değerini de (`api.state.clear()`) temizlemelidir.
- **Senkronizasyon:** Sütun gizleme olayları (`column-visibility.dt`) dinlenmeli ve görsel göstergeler anlık olarak güncellenmelidir.

### PROD-001: Layout Freeze
- `_Layout.cshtml` dosyası **değiştirilmez**. Archive sayfaları bu layout'a bağımlıdır.

### PROD-002: ViewStart Freeze
- `_ViewStart.cshtml` dosyası **değiştirilmez**. Default layout `_Layout` olarak kalır.

### PROD-003: site.css Freeze
- `wwwroot/css/site.css` dosyası **değiştirilmez**. `_LayoutBackbone` bu dosyayı yüklemez.

### PROD-004: Archive Freeze
- `Views/Archive/` ve `wwwroot/assets/js/Archive/` altındaki dosyalar **değiştirilmez** (refactor planı olmadan).
