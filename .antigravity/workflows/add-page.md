---
description: "WORKFLOW-002 — Mevcut Modüle Action Bazlı Sayfa ve UI Bileşeni Ekleme"
---

# /add-page - Sayfa ve Action Ekleme

## 🛠️ 1. Action Tiplerine Göre Özel Kurallar

### A. View (Details) / Create / Update Seçimi
- **Veri Yoğunluğu Kuralı:** - Eğer form/veri alanı az ise (Örn: Sadece Ad/Soyad/Kod), ayrı bir sayfa yerine **Offcanvas** bileşeni kullanılmalıdır.
  - Eğer veri alanı çok ve sekmeli yapı gerekiyorsa (Örn: LegalEntity), tam sayfa (`Details.cshtml`) kullanılmalıdır.
- **Layout:** Her iki durumda da temel yapı `_LayoutBackbone.cshtml` standartlarına uymalıdır.

### B. Delete Action (Onay Mekanizması)
- **UI:** Standart SweetAlert yerine projenin global bileşeni olan `Views/Shared/_GlobalConfirmation.cshtml` kullanılmalıdır.
- **Tetikleme:** Silme butonu bu modalı tetiklemeli ve onay alındığında ilgili Controller'daki **Soft Delete** aksiyonuna `POST` yapmalıdır.

### C. Bildirimler (Notifications)
- **Sistem:** Başarı, hata veya uyarı mesajları için `Views/Shared/_GlobalNotification.cshtml` bileşeni kullanılmalıdır.
- **Tetikleme:** `TempData` veya AJAX response üzerinden gelen mesajlar bu global bileşen aracılığıyla kullanıcıya sunulmalıdır.

---

## 🎭 2. Görev Dağılımı (Orkestra)

### Step 1: Backend (backend-architect)
- **Logic:** `POST` metodlarını ve `Guid id` parametrelerini hazırla.
- **Feedback:** İşlem sonunda `TempData["Success"]` veya JSON `success:true` dönerek `_GlobalNotification`'ı besle.

### Step 2: UI & UX (frontend-ui-ux)
- **Component:** Veri azsa Offcanvas, çoksa tam sayfa tasarımını yap.
- **Modallar:** Onay gerektiren işlemlerde `_GlobalConfirmation` entegrasyonunu kullan.

---

## ⚖️ 3. Teknik Mühürler (Guards)

- [ ] **Modal Check:** Silme işlemi `_GlobalConfirmation` kullanıyor mu?
- [ ] **Toast Check:** Bildirimler `_GlobalNotification` üzerinden mi akıyor?
- [ ] **UX Check:** Veri azlığına göre Offcanvas/Page tercihi doğru mu?
- [ ] **CSRF:** Formlarda `@Html.AntiForgeryToken()` var mı?

---
Diten ERP vNext Page Extension Standard - 2024