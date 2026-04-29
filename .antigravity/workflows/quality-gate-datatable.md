---
description: "QUALITY-GATE-DT — DataTable Sayfası Teslim Öncesi Zorunlu Kontrol Listesi"
---

# /quality-gate-datatable — DataTable Sayfası Kalite Kapısı

Bu workflow, bir DataTable liste sayfası tamamlandığında **hem agent hem orkestratör tarafından işaretlenmesi zorunlu** kontrol listesidir. Module pack'teki `golden_reference` kararına göre uygulanır:

- `slim`: `8 ve altı` form alanı, `_CreateEditOffcanvas.cshtml` ile Index içinde create/edit.
- `compact`: `8'den fazla` form alanı, ayrı `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml`.

> ⛔ Aşağıdaki listede **tek bir madde bile işaretlenmemişse** sayfa teslim edilemez. İlgili agent geri döner ve sorunu düzeltir.

---

## ✅ Kalite Kapısı Kontrol Listesi

### A. JavaScript (index.js)

- [ ] **DtDefaults:** `new DataTable(el, window.DtDefaults.create({...}))` kullanılıyor mu? Ham `$(...).DataTable({...})` yok mu?
- [ ] **ExportButtons:** `DtDefaults.exportButtons(addNewText, addNewAttr, extraButtons, options)` kullanılıyor mu? Elle `layout.topEnd.buttons` tanımı yok mu?
- [ ] **Bulk Action:** `getSelectedIds()`, `updateBulkBar()`, `clearSelection()` fonksiyonları implement edilmiş mi?
- [ ] **Header Checkbox Sync:** `dt-checkboxes-select-all` değişince tüm satır checkbox'ları ve bar güncelleniyor mu? `indeterminate` state var mı?
- [ ] **Shared Bulk Binding:** Bulk selection/action shared `DitenDataTable.createCrudTable(...)` veya manuel DataTable init sonrası `DitenDataTable.bindBulkSelection(...)` ile bağlanmış mı? Elle `#btnBulkDelete` listener'ı tek kaynak yapılmamış mı?
- [ ] **Shared Row Actions:** Action kolonu `DitenDataTable.renderActions(...)` ile üretilip `DitenDataTable.bindActionDispatcher(...)` veya `createCrudTable` üzerinden dispatch ediliyor mu?
- [ ] **Responsive Control Columns:** Control kolonu `className: 'control'`, checkbox kolonu `dt-checkboxes-cell cell-fit`, action kolonu `cell-fit all` standardını ve responsive priority değerlerini taşıyor mu?
- [ ] **Tek Satır Silme:** `window.showConfirm()` wrapper'ı kullanılıyor mu? Direkt `Swal.fire` ile bypass edilmiyor mu?
- [ ] **Delete Success Lifecycle:** Tek satır silme success akışı `row.remove().draw()` yerine `dt.ajax.reload(..., false)` ile mi tamamlanıyor? Toast tablo yenilendikten sonra mı gösteriliyor?
- [ ] **Quick View:** Module pack quick view istiyorsa inline `onclick="populateOffcanvas(...)"` yok mu? `.js-quick-view` + event delegation ile çalışıyor mu?
- [ ] **Toast:** Başarı/hata bildirimleri `window.showToast('KeyOrMessage', 'success'|'error'|'warning'|'info')` üzerinden mi geçiyor?
- [ ] **API Profile:** Modül `proxy-profile` veya `direct-gateway-profile` olarak açıkça doğrulandı mı? Platform/admin modüllerde default `proxy-profile` mı?
- [ ] **API URL — proxy-profile:** Browser JS endpoint'i `/{AreaName}/{ModuleName}/api` mi? Console/Network'te doğrudan `:5000/api/...` çağrısı yok mu?
- [ ] **API URL — direct-gateway-profile:** Sadece izinli tenant/public shell ise `window.API.{service} + '/api/{{ModuleNameLower}}'` formatı kullanılıyor mu? `/mdm/api/v1/...` formatı yok mu?
- [ ] **Auth Headers:** `getAuthHeaders()` tüm fetch/ajax çağrılarına ekleniyor mu ve DataTable JS içinde `document.cookie`, `access_token`, `Authorization: Bearer` üretimi yok mu?
- [ ] **drawCallback:** `DtDefaults.updateVisualState(this.api(), filterCount)` çağrılıyor mu?
- [ ] **StateSave (v2):** `stateSave: false` DataTable config’de AÇIKÇA set edilmiş mi? (DtDefaults baseConfig’deki `true`’yu override eder; bkz: `frontend-js-standard.md §JS Mimari Kuralları`)
- [ ] **Save View — async init:** `initDataTable` `async` mi? `await loadDefaultView()` DataTable init’ten önce çağrılıyor mu?
- [ ] **Save View — helpers:** `loadDefaultView`, `saveDefaultView`, `setSaveFilterVisible`, `isDirtyComparedToDefault`, `applySavedTableState`, `getCurrentView` fonksiyonları implement edilmiş mi? (bkz: `frontend-js-standard.md §Save View — Tam İmplementasyon Şablonu`)
- [ ] **Save View — real persistence:** Save View button action sadece `setSaveFilterVisible(false)` yapmıyor; `saveDefaultView(getCurrentView(api))` ile `personalizationClient.saveView/updateView` çağırıyor mu?
- [ ] **Save View — valid payload:** `saveDefaultView` payload `viewName` alanı boş kalamaz; `getSavedViewName(...) || L.SaveView || 'Default'` benzeri fallback var mı?
- [ ] **Save View — reload restore:** Sayfa yenilenince `loadDefaultView()` DataTable init'ten önce çalışıyor ve `filters + search + colVis + columnOrder + order` geri uygulanıyor mu?
- [ ] **Save View — reset restores factory baseline:** Inline filter Reset, sadece filtreleri/search'ü değil fabrika/default baseline'daki boş `filters/search` + default `colVis` + default `columnOrder` + default `order` tamamını geri uyguluyor mu? ColVis ile kolon kapat → Save View → Reset senaryosunda kapatılan kolonlar geri açılıyor mu?
- [ ] **Save View — extraButtons:** `saveFilterBtn` (`dt-save-filter-btn` class, başlangıçta `d-none`) `DtDefaults.exportButtons()` `extraButtons`’a eklenmiş mi?
- [ ] **Save View — arm:** `initComplete` içinde `setTimeout(() => { saveFilterArmed = true; }, 0)` var mı?
- [ ] **Save View Görünürlük Testi:** Dirty-state üretildiğinde `dt-save-filter-btn` görünür oluyor mu? Baseline'a dönünce tekrar gizleniyor mu?
- [ ] **Save View (v2):** Save View görünürlüğü **applied/effective state**’e göre mi? (Filter için Apply/Reset sonrası; search/colVis/sort immediate apply)
- [ ] **Save View Scope (v2):** Kaydedilenler: filters + search + colVis + columnOrder + sorting; kaydedilmeyenler: page number + pageLength
- [ ] **Personalization Client (v2):** `window.personalizationClient` doğru `moduleKey`/`pageKey` ile çağrılıyor mu? (`moduleKey`: AreaName, `pageKey`: ModuleName)
- [ ] **Personalization Tenant Header (v2):** Shared `personalizationClient` çift modlu mu? `tenant_user` için `X-Tenant-Id` gönderiyor, `platform_admin/partner_admin` için göndermiyor mu? Gateway/Platform middleware `/api/personalization/*` path'ini hem tenant hem platform actor için kabul ediyor mu?
- [ ] **401 / Expired JWT Guard (v2):** `personalizationClient` isteği `401 Unauthorized` aldığında `isAuthHandledError()` kontrolü var mı? Generic `ErrorOccurred` toast’ı ile maskelenmiyor mu?
- [ ] **Delete Endpoint Ownership:** Tekil delete ve bulk delete URL'leri modül endpoint'ini mi kullanıyor? (`/api/{{ModuleNameLower}}` + `/api/{{ModuleNameLower}}/bulk`) Başka modül endpoint'i var mı?
- [ ] **Bulk Delete Confirm Parity:** Bulk delete confirm akışı tekil delete ile aynı ortak wrapper/görsel standardı kullanıyor mu? Farklı modal/component kullanımı var mı?
- [ ] **SweetAlert Sidebar Stability:** Desktop'ta sol menü açıkken delete confirm açılıp kapanınca header/navbar konumu sabit kalıyor mu? Çözüm global body/html scrollbar hack'i değil, shared `backbone-custom.css` sidebar + `html.swal2-shown` offset standardı mı?
- [ ] **ColReorder Aktivasyon (v2):** Standart kolon yapısında (control + checkbox + N veri + action) `colReorder: { columns: ‘:gt(1):not(:last-child)’ }` DataTable config’de **aktif mi**? (Devre dışıysa neden belirten yorum satırı var mı?)
- [ ] **ColReorder Save View (v2):** `column-reorder.dt` + `columns-reordered.dt` event’leri dirty-state hesabına (`isDirtyComparedToDefault`) dahil mi? `captureColumnOrder` / `applyColumnOrder` implement edilmiş mi?

### B. HTML / Razor (Index.cshtml)

- [ ] **_Filter Partial:** `<partial name="_Filter" />` sayfanın en üstünde mevcut mu?
- [ ] **_Filter.cshtml:** `Views/{{AreaName}}/{{ModuleName}}/_Filter.cshtml` dosyası oluşturulmuş mu?
- [ ] **Title Block Standardı:** Breadcrumb olmayan Index sayfalarında üst başlık kompakt `Item Master` standardında mı? (`<div class="mb-3">` + `<h5 class="mb-0">` + `<p class="mb-0 text-muted">`)
- [ ] **PageDescription:** Breadcrumb yoksa başlık altında `<p class="mb-0">@Localizer["PageDescription"]</p>` var mı?
- [ ] **Inline Filter:** `#inlineFilterHost` + `#inlineFilterCollapse` mevcut mu? Offcanvas filter yok mu? Host hizası `px-3` ile mi? (`px-6`/`mx-*` yok mu?) Wrapper `pt-0 pb-3` mi?
- [ ] **Filter Tracker Exclusion:** `_Filter.cshtml` içindeki `#filterForm` `data-no-tracker` taşıyor mu? Global required-fields tracker filtre formuna "Required 0/0" badge'i basmamalıdır.
- [ ] **Filter Badge Count:** Filter butonundaki badge, Apply sonrası aktif filtre sayısını doğru gösteriyor mu? (örn. 2 select doluysa badge=2; Reset sonrası badge=0)
- [ ] **Filter Bar UI:** Filtreler “chip/dropdown” (Select2) gibi kompakt mı? Dropdown search zorunlu mu?
- [ ] **Filter Field Type:** Domain/Service/Category/Type/Owner/Status gibi sınırlı değer kümesi filtreleri text input değil Select2 chip mi? Multi gerekenlerde `multiple="multiple"` var mı?
- [ ] **Filter Select Styling:** Inline filter Select2 tetikleyicileri `form-select form-select-sm` estetiğine uyuyor mu? (`selectionCssClass` + merkezi CSS)
- [ ] **Filter Select2 Init Contract:** Inline filter selectleri `frontend-js-standard.md` init contract'ına uyuyor mu? (`dropdownParent: $(document.body)`, `dropdownCssClass: 'dt-inline-filter-dropdown'`, `minimumResultsForSearch`, `width: 'element'`; yasak pattern: `dropdownParent: $select.parent()`, `width:'100%'`)
- [ ] **Multi Select Summary:** Multi-select filtrelerde GoldenReference `syncMultiSelectSummary` davranışı var mı? Label `data-placeholder` olarak sabit görünüyor, seçimde count badge ve clear span çıkıyor, boşken label kaybolmuyor mu?
- [ ] **Single Select Empty Option:** Single-select filtrelerde ilk option `value=""` + `ShowAll` mı? Multi-selectlerde sahte boş option yerine placeholder/summary standardı mı kullanılıyor?
- [ ] **Select2 Overflow (MOD-0031):** Herhangi bir inline filter Select2 açıldığında sayfada yatay/dikey scroll çıkmıyor mu? (`backbone-custom.css` içinde `.filter-chip .select2-selection { inline-size: auto !important; }` override'ı mevcut mu?)
- [ ] **DataTable v2 Marker:** `<table ... data-dt-standard="v2" id="dt-...">` mevcut mu? (multi-table sayfalarda her tablo için id farklı mı?)
- [ ] **Hardcoded String:** `_Filter.cshtml` dahil tüm görünür metinler `@Localizer[...]` veya `@SharedLocalizer[...]` üzerinden geliyor mu?
- [ ] **Required Contract Parity:** Backend validator `NotEmpty()`/`NotNull()`, Web ViewModel `[Required]`, Razor `required`, label yıldızı ve required-fields tracker aynı alan listesini mi gösteriyor?
- [ ] **Optional Value-Type Guard:** Opsiyonel görünen numeric/date alanlar Web ViewModel'de nullable mı (`int?`, `decimal?`, `DateTime?`)? Non-nullable value type yüzünden otomatik `data-val-required` üretilmiyor mu?
- [ ] **Required Progress Baseline:** Create ekranı ilk açılış required progress değeri açıklanabilir mi? Payda gerçek required alan sayısı, pay yalnızca bilinçli default dolu required alanlar mı?
- [ ] **L10n Bridge (v2):** `window.L10n` minimum key set’i tamam mı? (`Search, Export, Import, Filter, Apply, Reset, ShowAll, SaveView, ColumnVisibility, Status` dahil)
- [ ] **L10n Delivery Pattern:** `Index.cshtml` içinde uzun `window.L10n.Key = ...` bloğu yok mu? `_IndexL10n.cshtml` payload partial'ı ve `index.l10n.js` loader scripti kullanılıyor mu?
- [ ] **L10n Bridge (v2):** Reorder kullanılan sayfalarda `ColumnOrder` key’i de bridge ediliyor mu?
- [ ] **Layout:** `Layout = "_LayoutBackbone"` kullanılıyor mu?
- [ ] **Shared CSS Placement:** Reusable toolbar / inline-filter / Select2 stilleri `Index.cshtml @section Styles` içine gömülmemiş mi? Ortak kurallar `backbone-custom.css` içinde mi?
- [ ] **Index Form Surface Boundary:** Slim ise Index içinde `_CreateEditOffcanvas.cshtml` var mı? Compact ise Index içinde create/edit amaçlı editor offcanvas yok mu?

### C. Localization

- [ ] **7 Dil:** Modüle özgü tüm yeni key'ler (`en, fr, es, zh, ar, ru, tr`) dosyalarına eklenmiş mi?
- [ ] **SharedResource:** Genel UI key'leri (`Active`, `Passive`, `Status`, `Filter`, `Reset`, `Apply`, `BulkDelete`, `AreYouSure`, `Cancel`, ...) sadece `SharedLocalizer` üzerinden mi geliyor?
- [ ] **SaveView Key:** `SharedResource.*.resx` içinde `SaveView` key'i 7 dilde mevcut mu? `window.L10n.SaveView` bridge ediliyor mu?
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

### G. 🧱 Overlay / Z-Index / Clipping / Row Selection (v2)

- [ ] **Badge Clipping:** Filter/ColVis badge hiçbir durumda kesilmiyor mu? (overflow hidden kaynaklı değil)
- [ ] **Dropdown Stacking:** ColVis/Export/Filter dropdown’ları butonların altında kalmıyor mu?
- [ ] **Action Group Radius:** Save View görünür/gizli iki durumda da border-radius tutarlı mı?
- [ ] **Fix Strategy:** Badge clipping için “badge’i butonun içine alıp ikonu kapatma” veya “sadece z-index artır” yok; çözüm `backbone-custom.css (MOD-0022)` top safe-area padding + doğru stacking olmalı.
- [ ] **Row Selection Hover (MOD-0018):** Bulk seçim sonrası satır üzerine gelindiğinde agresif mavi yok mu? `backbone-custom.css` içindeki `[class*=”datatables-”]` kuralı geçerli mi? Modüle özgü override yazılmamış mı?

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
- [ ] **Network Sağlamlığı:** `proxy-profile` ise browser Network'te çağrı same-origin `/{AreaName}/{ModuleName}/api` olarak görünüyor mu ve proxy'nin Gateway'e gittiği controller kodu/log ile doğrulandı mı? `direct-gateway-profile` ise `/api/{{ModuleNameLower}}` Gateway çağrısı `200` dönüyor mu? `401/403/500` yok mu? (`500` ise önce MongoDB 27017 ve backend logları kontrol edilmeden UI teslim edilmez.)
- [ ] **Quick View:** Module pack quick view istiyorsa "Quick View" açılıyor ve alanlar doluyor mu?
- [ ] **Boş Durum:** Tablo boşsa "No records found" veya eşdeğer L10n mesajı düzgün gösteriliyor mu?
- [ ] **Import Placeholder Toast:** Import/ComingSoon aksiyonu hata gibi görünmeyen `warning` veya `info` toast ile mi gösteriliyor? Yanlışlıkla `error` hissi üretmiyor mu?
- [ ] **Delete Toast Parity:** Tek satır delete toast'ı create success ve bulk delete success ile aynı görsel/lifecycle parity'de mi? Solda beyaz şerit, ripple artığı veya error hissi yok mu?
- [ ] **Delete State Preservation:** Tek satır delete sonrası mevcut page/filter/search state korunuyor mu? Bulk action bar ve header checkbox stale kalmıyor mu?

### H. 🔍 Statik Guard (ZORUNLU / Opsiyonel Ayrımı)

- [ ] **Zorunlu:** `python3 .antigravity/scripts/verify_datatable_page.py . --area {{AreaName}} --module {{ModuleName}} --reference slim|compact --api-profile proxy|direct-gateway` çalıştırıldı mı? Platform/admin için `--api-profile proxy` kullanılmalıdır.
- [ ] **Zorunlu (v2 marker varsa):** table `id` + `data-dt-standard="v2"` ve required `window.L10n` bridge key'leri/payload loader pattern'i script tarafından doğrulandı mı?
- [ ] **Opsiyonel:** Deeper pattern checks (override bayrakları, multi-sort QA) manuel olarak kontrol edildi mi?

### I. 📋 Module CRUD Completeness (ZORUNLU)

> ⛔ Bu bölüm yalnızca DataTable sayfasını değil, **tam modülü** kapsar. Tek bir madde eksikse modül teslim edilemez.

- [ ] **Slim Create/Edit:** `golden_reference: slim` ise `_CreateEditOffcanvas.cshtml` mevcut mu ve Add New offcanvas açıyor mu?
- [ ] **Compact Create sayfası:** `golden_reference: compact` ise `Views/{AreaName}/{ModuleName}/Create.cshtml` mevcut mu?
- [ ] **Compact Details sayfası:** `golden_reference: compact` ise `Views/{AreaName}/{ModuleName}/Details.cshtml` mevcut mu?
- [ ] **Compact Edit sayfası:** `golden_reference: compact` ise `Edit.cshtml` mevcut mu?
- [ ] **Compact Form/Details Section Parity:** `golden_reference: compact` ise `_Form.cshtml` ve `Details.cshtml` aynı logical section/card haritasını kullanıyor mu? Details dört bölümse Create/Edit de dört ayrı card/section mı? Alanlar aynı section altında mı?
- [ ] **Compact Create Navigation Pattern:** Compact ise "Add New" route tabanlı Create sayfasına yönleniyor mu?
- [ ] **Add New Button Binding:** `DtDefaults.exportButtons()` Add New butonu `{ href: '/{ModuleName}/Create' }` attr'ı ve `initComplete` içindeki `.add-new` click handler'ı ile çalışıyor mu? Inline `onclick` yok mu?
- [ ] **Rebuild Guard:** Yeniden yapım söz konusuysa silinmiş Create/Edit/Details sayfaları yeni sürümüyle değiştirildi mi?
- [ ] **API Dokümantasyonu:** `documentation-writer` Swagger/README güncellemesini tamamladı mı?
- [ ] **Kullanıcı Kılavuzu:** `user-manual-generator` son kullanıcı rehberini hazırladı mı?

### F. Localization Dosya İsimlendirme

- [ ] **Marker Class:** `Views/{AreaName}/{ModuleName}/{ModuleName}Index.cs` dosyası oluşturulmuş mu? (Örn: `SampleModuleIndex.cs`)
- [ ] **Resx Dosya Adı Eşleşmesi:** Resx dosya adı marker class adıyla birebir eşleşiyor mu? (Örn: class = `SampleModuleIndex` → `SampleModuleIndex.en.resx`, `SampleModuleIndex.tr.resx`, ...)
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
| `.antigravity/rules/frontend-js-standard.md` | JavaScript şablonu (DtDefaults tabanlı) + **§Save View — Tam İmplementasyon Şablonu** |
| `.antigravity/rules/frontend-standards.md` | CSS, UI, L10n genel kurallar |
| `.antigravity/workflows/add-module.md` | Tam modül oluşturma orkestrasyonu |
| `.antigravity/workflows/add-page.md` | Sayfa/action ekleme kuralları |
| `.antigravity/scripts/verify_datatable_page.py` | Golden DataTable statik doğrulama (Index.cshtml + index.js kontratı) |
| `.antigravity/workflows/migrate-datatable-v2.md` | Legacy → v2 migration checklist |
