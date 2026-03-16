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
- [ ] **Quick View:** Inline `onclick="populateOffcanvas(...)"` yok mu? `.js-quick-view` + event delegation ile offcanvas doluyor mu?
- [ ] **Toast:** Başarı/hata bildirimleri `window.showToast('Key', 'success'|'error')` üzerinden mi geçiyor?
- [ ] **API URL:** `apiUrl + '/api/{{ModuleNameLower}}'` formatında mı? `/mdm/api/v1/...` formatı yok mu?
- [ ] **Auth Headers:** `getAuthHeaders()` tüm fetch/ajax çağrılarına ekleniyor mu?
- [ ] **drawCallback:** `DtDefaults.updateVisualState(this.api(), filterCount)` çağrılıyor mu?
- [ ] **StateSave (v2):** Sayfa `data-dt-standard="v2"` ise `stateSave: false` set edilmiş mi? Otomatik cache/restore yok mu?
- [ ] **Save View (v2):** Save View görünürlüğü Apply beklemeden tetikleniyor mu? (filter/search/colVis/sort)
- [ ] **Save View Scope (v2):** Kaydedilenler: filters + search + colVis + sorting; kaydedilmeyenler: page number + pageLength
- [ ] **Storage Key (v2):** `dt:view-default:{tenantId}:{userId}:{module}:{tableId}` formatı kullanılıyor mu? `tableId` çakışmasız mı?

### B. HTML / Razor (Index.cshtml)

- [ ] **_Filter Partial:** `<partial name="_Filter" />` sayfanın en üstünde mevcut mu?
- [ ] **_Filter.cshtml:** `Views/{{AreaName}}/{{ModuleName}}/_Filter.cshtml` dosyası oluşturulmuş mu?
- [ ] **PageDescription:** Breadcrumb yoksa başlık altında `<p class="mb-0">@Localizer["PageDescription"]</p>` var mı?
- [ ] **Inline Filter:** `#inlineFilterHost` + `#inlineFilterCollapse` mevcut mu? Offcanvas filter yok mu?
- [ ] **Filter Bar UI:** Filtreler “chip/dropdown” (Select2) gibi kompakt mı? Dropdown search zorunlu mu?
- [ ] **DataTable v2 Marker:** `<table ... data-dt-standard="v2" id="dt-...">` mevcut mu? (multi-table sayfalarda her tablo için id farklı mı?)
- [ ] **Hardcoded String:** `_Filter.cshtml` dahil tüm görünür metinler `@Localizer[...]` veya `@SharedLocalizer[...]` üzerinden geliyor mu?
- [ ] **L10n Bridge (v2):** `window.L10n` minimum key set’i tamam mı? (`Search, Export, Import, Filter, Apply, Reset, ShowAll, SaveView, ColumnVisibility, Status` dahil)
- [ ] **Layout:** `Layout = "_LayoutBackbone"` kullanılıyor mu?

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
- [ ] **Unapplied Refresh:** Apply basılmadan staged filtre değişikliği yap → refresh: savedView yoksa temiz state, savedView varsa savedView restore davranışı doğru mu?

### E. 📱 Responsive (v2) — Breakpoint Doğrulama

- [ ] **≥992px:** Toolbar tek satır; Save View icon+text; Add New icon+text
- [ ] **768–991px:** Save View icon-only (tooltip/aria); wrap kontrollü (random drop yok)
- [ ] **<768px:** Search full-width öncelikli; action groups kontrollü wrap; Add New icon-only ve sağda kontrollü konum
- [ ] **<576px Export UI:** Export dropdown, aynı gruptaki `.btn-icon` butonlarla yükseklik/vertical padding olarak hizalı mı? (Üstten-alttan küçük kalmıyor mu?) `dt-export-collection-btn` class’ı korunuyor mu?
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

### H. 🔍 Statik Guard (ZORUNLU / Opsiyonel Ayrımı)

- [ ] **Zorunlu:** `python3 .antigravity/scripts/verify_datatable_page.py . --area {{AreaName}} --module {{ModuleName}}` çalıştırıldı mı?
- [ ] **Zorunlu (v2 marker varsa):** table `id` + `data-dt-standard="v2"` ve required `window.L10n` bridge key'leri script tarafından doğrulandı mı?
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
