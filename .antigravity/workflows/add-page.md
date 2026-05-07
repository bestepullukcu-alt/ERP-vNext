---
description: "WORKFLOW-002 — Mevcut Modüle Action Bazlı Sayfa ve UI Bileşeni Ekleme"
---

# /add-page - Sayfa ve Action Ekleme

## 🛠️ 1. Sayfa Tipine Göre Zorunlu Standartlar

### A. DataTable Liste Sayfası (Index)
- **HTML Şablonu:** `.antigravity/rules/frontend-datatable-template.md` birebir kopyalanır (iskelete dokunulmaz).
- **Golden Variant:** Module pack'teki `golden_reference` uygulanır.
  - `slim`: `8 ve altı` form alanı; `_CreateEditOffcanvas.cshtml` ile Index içinde create/edit.
  - `compact`: `8'den fazla` form alanı; `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml`.
- **Partial Standardı:** Her DataTable modülünde `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`, marker class, `index.l10n.js`, `index.js` zorunludur.
- **JS Şablonu:** `.antigravity/rules/frontend-js-standard.md` birebir uygulanır:
  - `new DataTable(el, window.DtDefaults.create({...}))`
  - `window.DtDefaults.exportButtons(addNewText, addNewAttr, extraButtons, options)`
  - Quick View: `onclick="populateOffcanvas(...)"` **YASAK**. `.js-quick-view` + event delegation kullanılır.
- **Kalite Kapısı:** Teslimden önce `.antigravity/workflows/quality-gate-datatable.md` checklist'i eksiksiz işaretlenir.

### B. Form Sayfası (Create/Edit)
- **Şablon:** `.antigravity/rules/frontend-form-template.md`
- **Kapsam:** Bu bölüm Compact (`golden_reference: compact`) modüller için kullanılır. Slim modüllerde create/edit formu `_CreateEditOffcanvas.cshtml` içindedir.
- **Layout:** Shell tipine göre admin modüllerinde `_LayoutPlatformAdmin.cshtml`, tenant modüllerinde `_LayoutTenantShell.cshtml` zorunludur.
- **Validation:** `novalidate` + Bootstrap `invalid-feedback`.
- **Header/Breadcrumb Standardı:** Üst blok kompakt action-page standardında olmalıdır:
  - wrapper: `d-flex ... mb-3 row-gap-4`
  - başlık: `h5.mb-0`
  - breadcrumb: `{{ModuleName}}Title > Current Action`
  - `Home` / area breadcrumb varsayılanı kullanılmaz
  - `PageDescription` eklenmez
- **Dependent Select Standardı:** Parent/child select ilişkisi varsa:
  - child alan parent seçilmeden disabled başlar
  - parent seçilince child seçenekleri uygun alt kümeyle yeniden oluşturulur
  - uygunsuz mevcut seçim temizlenir
  - select2 kullanılıyorsa disabled state ve seçenekler UI tarafında yeniden senkronlanır
  - uygunsuz seçenekler dropdown içinde disabled/gri halde bırakılmaz

### C. Details (Read-Only) Sayfası
- **Şablon:** `.antigravity/rules/frontend-details-template.md`
- **Kurallar:** `.antigravity/workflows/details-page-rules.md` (VIEW-002)

### D. Delete (Tekil / Toplu)
- **Tekil Silme (Row Action):** `window.showConfirm('DeleteConfirmation', callback, entityName)` zorunludur.
  - Callback içinde Gateway'e `DELETE {apiUrl}/api/{resource}/{id}` çağrılır.
- **Toplu Silme:** `Swal.fire(...)` kullanılır ve `DELETE {apiUrl}/api/{resource}/bulk` çağrılır (`body: { ids }`).

### E. Bildirimler (Notifications)
- **Toast:** Her zaman `window.showToast(...)` üzerinden verilir (`RecordCreated`, `RecordUpdated`, `RecordDeleted`, `ErrorOccurred`).
- **TempData:** MVC post/redirect/GET senaryolarında `TempData["SuccessMessage"]` / `TempData["ErrorMessage"]` kullanılır (Layout backbone otomatik toast basar).

---

## 🎭 2. Görev Dağılımı (Orkestra)

### Step 1: Backend (backend-architect)
- **Logic:** `POST` metodlarını ve `Guid id` parametrelerini hazırla.
- **Feedback:** İşlem sonunda `TempData["SuccessMessage"]` / `TempData["ErrorMessage"]` veya JSON `{ success: true|false, messageKey: "RecordCreated"|... }` döndürerek toast sistemini besle.

### Step 2: UI & UX (frontend-ui-ux)
- **Component:** Kararı module pack verir. `slim` ise offcanvas, `compact` ise tam sayfa tasarımını yap.
- **Modallar:** Onay gerektiren işlemlerde `_GlobalConfirmation` entegrasyonunu kullan.

---

## ⚖️ 3. Teknik Mühürler (Guards)

- [ ] **DtDefaults Check:** `DtDefaults.create()` kullanılıyor mu?
- [ ] **Buttons Check:** `DtDefaults.exportButtons(..., options)` kullanılıyor mu?
- [ ] **QuickView Check:** `.js-quick-view` + event delegation var mı? Inline `onclick` yok mu?
- [ ] **Confirm Check:** Tekil silme `window.showConfirm` üzerinden mi?
- [ ] **Toast Check:** Bildirimler `window.showToast` üzerinden mi?
- [ ] **L10n Check:** Hardcoded string yok mu? `window.L10n` bridge dolu mu (Platform: 2 dil, Tenant: 7 dil)?
- [ ] **CSRF Check:** Formlarda `@Html.AntiForgeryToken()` var mı?
- [ ] **Dependent Select Check:** Parent/child select varsa child alan parent seçimi sonrası aktifleşiyor, sadece geçerli seçenekleri gösteriyor ve eski uygunsuz değerleri temizliyor mu?

---
Diten ERP vNext Page Extension Standard - 2024
