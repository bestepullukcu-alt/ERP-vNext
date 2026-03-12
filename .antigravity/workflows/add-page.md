---
description: "WORKFLOW-002 — Mevcut Modüle Action Bazlı Sayfa ve UI Bileşeni Ekleme"
---

# /add-page - Sayfa ve Action Ekleme

## 🛠️ 1. Sayfa Tipine Göre Zorunlu Standartlar

### A. DataTable Liste Sayfası (Index)
- **HTML Şablonu:** `.antigravity/rules/frontend-datatable-template.md` birebir kopyalanır (iskelete dokunulmaz).
- **JS Şablonu:** `.antigravity/rules/frontend-js-standard.md` birebir uygulanır:
  - `new DataTable(el, window.DtDefaults.create({...}))`
  - `window.DtDefaults.exportButtons(addNewText, addNewAttr, extraButtons, options)`
  - Quick View: `onclick="populateOffcanvas(...)"` **YASAK**. `.js-quick-view` + event delegation kullanılır.
- **Kalite Kapısı:** Teslimden önce `.antigravity/workflows/quality-gate-datatable.md` checklist'i eksiksiz işaretlenir.

### B. Form Sayfası (Create/Edit)
- **Şablon:** `.antigravity/rules/frontend-form-template.md`
- **Layout:** `_LayoutBackbone.cshtml` zorunludur.
- **Validation:** `novalidate` + Bootstrap `invalid-feedback`.

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
- **Component:** Veri azsa Offcanvas, çoksa tam sayfa tasarımını yap.
- **Modallar:** Onay gerektiren işlemlerde `_GlobalConfirmation` entegrasyonunu kullan.

---

## ⚖️ 3. Teknik Mühürler (Guards)

- [ ] **DtDefaults Check:** `DtDefaults.create()` kullanılıyor mu?
- [ ] **Buttons Check:** `DtDefaults.exportButtons(..., options)` kullanılıyor mu?
- [ ] **QuickView Check:** `.js-quick-view` + event delegation var mı? Inline `onclick` yok mu?
- [ ] **Confirm Check:** Tekil silme `window.showConfirm` üzerinden mi?
- [ ] **Toast Check:** Bildirimler `window.showToast` üzerinden mi?
- [ ] **L10n Check:** Hardcoded string yok mu? `window.L10n` bridge dolu mu (8 dil)?
- [ ] **CSRF Check:** Formlarda `@Html.AntiForgeryToken()` var mı?

---
Diten ERP vNext Page Extension Standard - 2024
