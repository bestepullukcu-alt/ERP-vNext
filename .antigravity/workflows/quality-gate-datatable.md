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

### B. HTML / Razor (Index.cshtml)

- [ ] **_Filter Partial:** `<partial name="_Filter" />` sayfanın en üstünde mevcut mu?
- [ ] **_Filter.cshtml:** `Views/{{AreaName}}/{{ModuleName}}/_Filter.cshtml` dosyası oluşturulmuş mu?
- [ ] **Offcanvas Yapısı:** Offcanvas içeriği `<dl class="row">` + `<dt>/<dd>` yapısıyla mı yazılmış? (`<div class="row"><div class="col-6">` yapısı yasaktır)
- [ ] **Hardcoded String:** Offcanvas dahil tüm görünür metinler `@Localizer[...]` veya `@SharedLocalizer[...]` üzerinden geliyor mu?
- [ ] **L10n Bridge:** `@section Scripts` içinde `window.L10n` bloğu standart şablona göre dolu mu? (en azından `Active, Passive, Unknown, Actions, Edit, ViewDetails, QuickView, BulkDelete, BulkDeleteConfirm, AreYouSure, Cancel` key'leri)?
- [ ] **Layout:** `Layout = "_LayoutBackbone"` kullanılıyor mu?

### C. Localization

- [ ] **8 Dil:** Modüle özgü tüm yeni key'ler (`en, tr, ru, es, ka, kk, uk, uz`) dosyalarına eklenmiş mi?
- [ ] **SharedResource:** Genel UI key'leri (`Active`, `Passive`, `Status`, `Filter`, `Reset`, `Apply`, `BulkDelete`, `AreYouSure`, `Cancel`, ...) sadece `SharedLocalizer` üzerinden mi geliyor?
- [ ] **Module Resx:** Modül key'leri (`{{ModuleName}}Title`, `PageDescription`, `Actions`, `EditBtn`, `QuickView`, `AddNew{{ModuleName}}`, ...) sadece modül `.resx` dosyalarından mı geliyor?

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
