---
description: "WORKFLOW-000 — Yeni Modül Oluşturma Orkestrasyonu (Ana Senaryo)"
---

# /add-module - Yeni Modül Oluşturma

Bu workflow, bir modülün sıfırdan son kullanıcıya ulaşana kadarki tüm katmanlarını koordine eder. Ajan, bu adımları sırasıyla ve HİÇBİR inisiyatif almadan ZORUNLU olarak uygulamalıdır.

## 🎭 Görev Dağılımı (Orkestra)

1. **Phase 1: Analiz (business-analyst)**
   - Modülün alanlarını (fields), IFRS/KVKK gereksinimlerini belirle.
   - UI ve tablolarda kullanılacak anahtar kelimeleri (Keys) çıkar.

2. **Phase 2: Veri Mimarisi (data-agent & backend-architect)**
   - MongoDB koleksiyonunu tasarla (`ITenantDocument` tabanlı).
   - Domain Entity ve Repository katmanını oluştur. (Soft Delete ve TenantId ZORUNLUDUR).

3. **Phase 3: İş Mantığı & Yerelleştirme (backend-architect & l10n-agent)**
   - `/add-endpoint-cqrs` akışını başlat (Request, Command, Handler, Validator).
   - API Controller'ı oluştur ve Ocelot Gateway rotasını ekle.
   - **[KRİTİK]:** `.antigravity/rules/localization-standard.md` dosyasını oku.
   - Modüle ait 8 dil dosyasını (`en, es, ka, kk, ru, tr, uk, uz`) EKSİKSİZ oluştur ve çevirileri işle.
   - **SharedResource vs ViewResource:** Ortak kelimeleri (Kaydet, Sil, Aktif vb.) modül dosyasına ASLA ekleme, bunları `SharedLocalizer`'dan çek.

4. **Phase 4: Arayüz (frontend-ui-ux)**
   - **[KRİTİK]:** `.antigravity/rules/frontend-datatable-template.md` dosyasını referans al.
   - `Views/{AreaName}/{ModuleName}/Index.cshtml` sayfasını oluştururken SADECE bu şablonu kopyala. Şablondaki HTML yapısına, Skeleton Loader, Bulk Action Bar ve Offcanvas yapılarına DOKUNMA, sadece değişkenleri doldur.
   - `wwwroot/assets/js/{AreaName}/{ModuleName}/index.js` dosyasını Module Pattern (IIFE) ile oluştur. AJAX, DataTable v2 ve `window.L10n` bridge entegrasyonunu eksiksiz yap.
   - `_LayoutBackbone` içine menü linkini ekle ve `ViewBag.ActiveMenu` ile bağla.

5. **Phase 5: Kalite & Güvenlik (testing-agent & security-agent)**
   - xUnit testlerini yaz (Tenant isolation check).
   - `/tenant-audit` komutunu çalıştırarak sızıntı kontrolü yap.

## ⚖️ Altın Kurallar
- **Sıfır İnisiyatif Kuralı:** Ajan, standart Liste/CRUD (DataTable) sayfaları için arayüz uyduramaz, kesinlikle Master Template'i kullanmak zorundadır.
- Modül mutlaka `MDM/` (veya ilgili Area) klasörü altında olmalıdır.
- Soft Delete ve TenantId filtrelemesi asla atlanamaz.
- Details/Edit gibi alt sayfalar yapıldığında Sneat PRO standartlarına ve 3'lü kart düzenine sadık kalınmalıdır.