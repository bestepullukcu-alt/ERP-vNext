---
description: "QUALITY-GATE-DT — DataTable Sayfası Teslim Öncesi Zorunlu Kontrol Listesi"
---

# /quality-gate-datatable — DataTable Sayfası Kalite Kapısı

Bu workflow, bir DataTable liste sayfası (Countries, Cities, Currencies, vb.) tamamlandığında **hem agent hem orkestratör tarafından işaretlenmesi zorunlu** kontrol listesidir.

> ⛔ Aşağıdaki listede **tek bir madde bile işaretlenmemişse** sayfa teslim edilemez. İlgili agent geri döner ve sorunu düzeltir.

---

## ✅ Kalite Kapısı Kontrol Listesi

### A. JavaScript (index.js)

- [ ] **DtDefaults:** `new DataTable(el, window.DtDefaults.create({...}))` kullanılıyor mu? Ham `$(...).DataTable({...})` yok mu?
- [ ] **ExportButtons:** `DtDefaults.exportButtons(addNewText, addNewAttr, extraButtons, options)` kullanılıyor mu? Elle `layout.topEnd.buttons` tanımı yok mu?
- [ ] **Bulk Action:** `getSelectedIds()`, `updateBulkBar()`, `clearSelection()` fonksiyonları implement edilmiş mi?
- [ ] **Header Checkbox Sync:** `dt-checkboxes-select-all` değişince tüm satır checkbox'ları ve bar güncelleniyor mu? `indeterminate` state var mı?
- [ ] **Tek Satır Silme:** `window.showConfirm()` wrapper'ı kullanılıyor mu? Direkt `Swal.fire` ile bypass edilmiyor mu?
- [ ] **Delete Success Lifecycle:** Tek satır silme success akışı `row.remove().draw()` yerine `dt.ajax.reload(..., false)` ile mi tamamlanıyor? Toast tablo yenilendikten sonra mı gösteriliyor?
- [ ] **Quick View:** Inline `onclick="populateOffcanvas(...)"` yok mu? `.js-quick-view` + event delegation ile offcanvas doluyor mu?
- [ ] **Toast:** Başarı/hata bildirimleri `window.showToast('KeyOrMessage', 'success'|'error'|'warning'|'info')` üzerinden mi geçiyor?
- [ ] **API URL:** `apiUrl + '/api/{{ModuleNameLower}}'` formatında mı? `/mdm/api/v1/...` formatı yok mu?
- [ ] **Auth Headers:** `getAuthHeaders()` tüm fetch/ajax çağrılarına ekleniyor mu?
- [ ] **drawCallback:** `DtDefaults.updateVisualState(this.api(), filterCount)` çağrılıyor mu?
- [ ] **StateSave (v2):** Sayfa `data-dt-standard="v2"` ise `stateSave: false` set edilmiş mi? Otomatik cache/restore yok mu?
- [ ] **Save View (v2):** Save View görünürlüğü **applied/effective state**’e göre mi? (Filter için Apply/Reset sonrası; search/colVis/sort immediate apply)
- [ ] **Save View Scope (v2):** Kaydedilenler: filters + search + colVis + columnOrder + sorting; kaydedilmeyenler: page number + pageLength
- [ ] **Personalization Client (v2):** Save View localStorage ile değil `window.personalizationClient` üzerinden `/api/personalization/views` çağrılarıyla mı yapılıyor?
- [ ] **401 / Expired JWT Guard (v2):** `personalizationClient` isteği `401 Unauthorized` aldığında shared refresh/login akışı devreye giriyor mu? Generic `ErrorOccurred` toast'ı ile maskelenmiyor mu?
- [ ] **Column Reorder (v2):** `ColReorder` aktifse `columnOrder` capture/restore ediliyor mu? `column-reorder.dt` dirty-state hesabına dahil mi?

### B. HTML / Razor (Index.cshtml)

- [ ] **_Filter Partial:** `<partial name="_Filter" />` sayfanın en üstünde mevcut mu?
- [ ] **_Filter.cshtml:** `Views/{{AreaName}}/{{ModuleName}}/_Filter.cshtml` dosyası oluşturulmuş mu?
- [ ] **PageDescription:** Breadcrumb yoksa başlık altında `<p class="mb-0">@Localizer["PageDescription"]</p>` var mı?
- [ ] **Inline Filter:** `#inlineFilterHost` + `#inlineFilterCollapse` mevcut mu? Offcanvas filter yok mu? Host hizası `px-6` ile mi? (`mx-*` yok mu?) Wrapper `pt-0 pb-3` mi?
- [ ] **Filter Badge Count:** Filter butonundaki badge, Apply sonrası aktif filtre sayısını doğru gösteriyor mu? (örn. 2 select doluysa badge=2; Reset sonrası badge=0)
- [ ] **Filter Bar UI:** Filtreler “chip/dropdown” (Select2) gibi kompakt mı? Dropdown search zorunlu mu?
- [ ] **Filter Select Styling:** Inline filter Select2 tetikleyicileri `form-select form-select-sm` estetiğine uyuyor mu? (`selectionCssClass` + merkezi CSS)
- [ ] **Select2 Overflow (MOD-0031):** Herhangi bir inline filter Select2 açıldığında sayfada yatay/dikey scroll çıkmıyor mu? (`backbone-custom.css` içinde `.filter-chip .select2-selection { inline-size: auto !important; }` override'ı mevcut mu?)
- [ ] **DataTable v2 Marker:** `<table ... data-dt-standard="v2" id="dt-...">` mevcut mu? (multi-table sayfalarda her tablo için id farklı mı?)
- [ ] **Hardcoded String:** `_Filter.cshtml` dahil tüm görünür metinler `@Localizer[...]` veya `@SharedLocalizer[...]` üzerinden geliyor mu?
- [ ] **L10n Bridge (v2):** `window.L10n` minimum key set’i tamam mı? (`Search, Export, Import, Filter, Apply, Reset, ShowAll, SaveView, ColumnVisibility, Status` dahil)
- [ ] **L10n Delivery Pattern:** `Index.cshtml` içinde uzun `window.L10n.Key = ...` bloğu yok mu? `_IndexL10n.cshtml` payload partial'ı ve `index.l10n.js` loader scripti kullanılıyor mu?
- [ ] **L10n Bridge (v2):** Reorder kullanılan sayfalarda `ColumnOrder` key’i de bridge ediliyor mu?
- [ ] **Layout:** `Layout = "_LayoutBackbone"` kullanılıyor mu?
- [ ] **Shared CSS Placement:** Reusable toolbar / inline-filter / Select2 stilleri `Index.cshtml @section Styles` içine gömülmemiş mi? Ortak kurallar `backbone-custom.css` içinde mi?

### C. Localization

- [ ] **8 Dil:** Modüle özgü tüm yeni key'ler (`en, tr, ru, es, ka, kk, uk, uz`) dosyalarına eklenmiş mi?
- [ ] **SharedResource:** Genel UI key'leri (`Active`, `Passive`, `Status`, `Filter`, `Reset`, `Apply`, `BulkDelete`, `AreYouSure`, `Cancel`, ...) sadece `SharedLocalizer` üzerinden mi geliyor?
- [ ] **SaveView Key:** `SharedResource.*.resx` içinde `SaveView` key'i 8 dilde mevcut mu? `window.L10n.SaveView` bridge ediliyor mu?
- [ ] **Vocabulary:** `Search/Export/Import/Filter/Apply/Reset/ShowAll/ColumnVisibility` gibi toolbar metinleri SharedResource üzerinden mi geliyor?
- [ ] **No Fallback:** Toolbar/action metinlerinde hardcoded fallback (`|| 'Export'`) yok mu? (Eksik key teslimi bloklar.)
- [ ] **RESX Placeholder Check:** `python3 .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py .` çalıştırıldı mı? (Non-English dosyalarda English placeholder bırakmak teslimi bloklar.)
- [ ] **Module Resx:** Modül key'leri (`{{ModuleName}}Title`, `PageDescription`, `Actions`, `EditBtn`, `QuickView`, `AddNew{{ModuleName}}`, ...) sadece modül `.resx` dosyalarından mı geliyor?

### D. 🎛️ State Model (v2) — Uygulama Davranışı

- [ ] **baselineDefault:** SavedView yokken referans state net mi? (boş filters/search + default colVis + default single-sort + default pageLength)
- [ ] **pageLength Ayrımı:** `pageLength` baseline’da tanımlı; persistence/dirty compare kapsamı dışı mı?
- [ ] **normalize:** `null/undefined/''` eşitliği, string trim, `1`==`\"1\"`, boolean→`\"true\"/\"false\"`, colVis index-based, sorting normalize kuralı uygulanıyor mu?
- [ ] **normalize:** `columnOrder` tekil/tam kolon dizisi olarak normalize ediliyor mu?
- [ ] **Unapplied Refresh:** Apply basılmadan staged filtre değişikliği yap → refresh: savedView yoksa temiz state, savedView varsa savedView restore davranışı doğru mu?

### E. 📱 Responsive (v2) — Breakpoint Doğrulama

- [ ] **Toolbar Padding:** Toolbar row class `row px-3 my-0 justify-content-between` (DtDefaults) olarak mı geliyor? (px-6 olmamalı)
- [ ] **≥992px:** Toolbar tek satır; Save View icon+text; Add New icon+text
- [ ] **768–991px:** Save View icon-only (tooltip/aria); wrap kontrollü (random drop yok)
- [ ] **<768px:** Search full-width öncelikli; action groups kontrollü wrap; Add New icon-only ve sağda kontrollü konum
- [ ] **<576px Export UI:** Export dropdown, aynı gruptaki `.btn-icon` butonlarla yükseklik/vertical padding olarak hizalı mı? (Üstten-alttan küçük kalmıyor mu?) `dt-export-collection-btn` class’ı korunuyor mu?
- [ ] **Column Drag UX:** Reorder aktifse header sürükleme davranışı çalışıyor mu? Control/checkbox/actions kolonları yanlışlıkla taşınmıyor mu?
- [ ] **Inline Filter:** <992px Apply/Reset alt satıra geçiyor; <576px eşit genişlikte yan yana

### F. ♿ Accessibility (A11y) (v2)

- [ ] **Icon-only Buttons:** `aria-label` + lokalize tooltip/title var mı?
- [ ] **Filter Trigger:** `aria-controls` + `aria-expanded` doğru mu?
- [ ] **Keyboard:** Tab ile erişim, dropdown focus, ESC kapanış çalışıyor mu?
- [ ] **Badge:** Screen reader akışını bozmuyor mu? (decorative ise aria-hidden stratejisi uygulanmış mı?)

### G. 🧱 Overlay / Z-Index / Clipping (v2)

- [ ] **Badge Clipping:** Filter/ColVis badge hiçbir durumda kesilmiyor mu? (overflow hidden kaynaklı değil)
- [ ] **Dropdown Stacking:** ColVis/Export/Filter dropdown’ları butonların altında kalmıyor mu?
- [ ] **Action Group Radius:** Save View görünür/gizli iki durumda da border-radius tutarlı mı?
- [ ] **Fix Strategy:** Badge clipping için “badge’i butonun içine alıp ikonu kapatma” veya “sadece z-index artır” yok; çözüm `backbone-custom.css (MOD-0022)` top safe-area padding + doğru stacking olmalı.

### D. Routing & Sidebar

- [ ] **Sidebar:** `_LayoutBackbone.cshtml` içine aktif sayfa vurgulama ile menu item eklenmiş mi?
- [ ] **Controller Route:** Frontend controller `/{{ModuleName}}` (MDM area'sı olmadan kök) ile erişilebilir mi?
- [ ] **Ocelot:** `ocelot.json` içinde `/api/{{ModuleNameLower}}` ve `/api/{{ModuleNameLower}}/{everything}` rotaları doğru servise yönlendirilmiş mi?

### E. 🖥️ Runtime Browser Doğrulama (ZORUNLU)

> ⚠️ Bu bölüm sadece kod incelemesiyle geçilemez. Agent, sayfayı **gerçek browser'da açarak** aşağıdaki kontrolleri yapmak ZORUNDADIR.

- [ ] **Sayfa Yükleme:** Sayfa login redirect olmadan yükleniyor mu?
- [ ] **DataTable Toolbar:** Search kutusu, Export dropdown, Filter butonu ve "Add New" butonu görünüyor mu?
- [ ] **Localization:** Sayfa başlığı, alt başlık ve tablo kolon başlıkları raw key olarak DEĞİL çevrilmiş metin olarak görünüyor mu?
- [ ] **Console Hatasızlık:** Browser console'da JavaScript hatası yok mu?
- [ ] **Network Sağlamlığı:** `/api/{{ModuleNameLower}}` çağrısı `200` dönüyor mu? `401/500` yok mu? (`500` ise önce MongoDB 27017 ve backend logları kontrol edilmeden UI teslim edilmez.)
- [ ] **Quick View:** "Quick View" offcanvas açılıyor ve alanlar doluyor mu?
- [ ] **Boş Durum:** Tablo boşsa "No records found" veya eşdeğer L10n mesajı düzgün gösteriliyor mu?
- [ ] **Import Placeholder Toast:** Import/ComingSoon aksiyonu hata gibi görünmeyen `warning` veya `info` toast ile mi gösteriliyor? Yanlışlıkla `error` hissi üretmiyor mu?
- [ ] **Delete Toast Parity:** Tek satır delete toast'ı create success ve bulk delete success ile aynı görsel/lifecycle parity'de mi? Solda beyaz şerit, ripple artığı veya error hissi yok mu?
- [ ] **Delete State Preservation:** Tek satır delete sonrası mevcut page/filter/search state korunuyor mu? Bulk action bar ve header checkbox stale kalmıyor mu?

### H. 🔍 Statik Guard (ZORUNLU / Opsiyonel Ayrımı)

- [ ] **Zorunlu:** `python3 .antigravity/scripts/verify_datatable_page.py . --area {{AreaName}} --module {{ModuleName}}` çalıştırıldı mı?
- [ ] **Zorunlu (v2 marker varsa):** table `id` + `data-dt-standard="v2"` ve required `window.L10n` bridge key'leri/payload loader pattern'i script tarafından doğrulandı mı?
- [ ] **Opsiyonel:** Deeper pattern checks (override bayrakları, multi-sort QA) manuel olarak kontrol edildi mi?

### F. Localization Dosya İsimlendirme

- [ ] **Marker Class:** `Views/{AreaName}/{ModuleName}/{ModuleName}Index.cs` dosyası oluşturulmuş mu? (Örn: `CountriesIndex.cs`)
- [ ] **Resx Dosya Adı Eşleşmesi:** Resx dosya adı marker class adıyla birebir eşleşiyor mu? (Örn: class = `CountriesIndex` → `CountriesIndex.en.resx`, `CountriesIndex.tr.resx`, ...)
- [ ] **PageDescription Key:** `.resx` dosyalarında `PageDescription` key'i tanımlı mı? Alt başlık hardcoded yazılmamış mı?

---

## 🔄 Workflow Adımları

1. **Agent:** Geliştirmeyi tamamladıktan sonra bu listeyi kendi kendine işaretler.
2. **Agent:** Sayfayı browser'da açarak E bölümündeki runtime kontrollerini yapar.
3. **Orkestratör:** Teslim raporunda bu listenin (A–F dahil) tamamlandığını onaylar.
4. **Kullanıcı onayı:** Orkestratör, `run_all.sh` ile sistemi yeniden derleyip kullanıcıya "sayfa hazır" raporu sunar.

---

## ⚡ Hızlı Referans — Kullanılacak Dosyalar

| Dosya | Konu |
|-------|------|
| `.antigravity/rules/frontend-datatable-template.md` | HTML/Razor şablonu |
| `.antigravity/rules/frontend-js-standard.md` | JavaScript şablonu (DtDefaults tabanlı) |
| `.antigravity/rules/frontend-standards.md` | CSS, UI, L10n genel kurallar |
| `.antigravity/workflows/add-module.md` | Tam modül oluşturma orkestrasyonu |
| `.antigravity/workflows/add-page.md` | Sayfa/action ekleme kuralları |
| `.antigravity/scripts/verify_datatable_page.py` | Golden DataTable statik doğrulama (Index.cshtml + index.js kontratı) |
| `.antigravity/workflows/migrate-datatable-v2.md` | Legacy → v2 migration checklist |
