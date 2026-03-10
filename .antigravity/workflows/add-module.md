---
description: "WORKFLOW-000 — Yeni Modül Oluşturma Orkestrasyonu (Ana Senaryo)"
---

# /add-module - Yeni Modül Oluşturma

Bu workflow, bir modülün sıfırdan son kullanıcıya ulaşana kadarki tüm katmanlarını koordine eder. Ajan, bu adımları sırasıyla ve HİÇBİR inisiyatif almadan ZORUNLU olarak uygulamalıdır.

## 🎭 Görev Dağılımı (Orkestra)

1. **Phase 1: Analiz (business-analyst)**
   - Modülün alanlarını (fields), IFRS/KVKK gereksinimlerini belirle.
   - UI ve tablolarda kullanılacak anahtar kelimeleri (Keys) çıkar.

1.5. **Phase 1.5: Mimari Doğrulama (ORKESTRATOR)**
   - **KRİTİK ADIM:** Kod yazmadan ÖNCE, üretilecek kodun taslağını kural dosyalarıyla kıyasla.
   - **Mimari Onay Zorunluluğu:** Ajan, aşağıdaki kontrol listesini doldurup KULLANICIDAN ONAY ALMADAN kod yazamaz:
     ```
     □ PRD'deki TÜM alanlar Entity'ye eklendi mi?
     □ Alan isimleri global ERP standartlarına uygun mu? (PlateCode → Code)
     □ Repository interface'inde TenantId/Soft-Delete garantisi var mı?
     □ EntityBase'ten miras alınıyor mu? (TenantId, IsDeleted)
     □ CQRS yapısı (Command, Query, Handler, Validator) planlandı mı?
     ```
   - **Onay Formatı:** "Faz 1.5 Mimari Doğrulama tamamlandı. Onayınızı bekliyorum."
   - Kullanıcı onaylamazsa → Phase 1'e dön, düzelt.

2. **Phase 2: Veri Mimarisi (data-agent & backend-architect)**
   - MongoDB koleksiyonunu tasarla (`ITenantDocument` tabanlı).
   - Domain Entity ve Repository katmanını oluştur. (Soft Delete ve TenantId ZORUNLUDUR).

3. **Phase 3: İş Mantığı & Yerelleştirme (backend-architect & l10n-agent)**
   - `/add-endpoint-cqrs` akışını başlat (Request, Command, Handler, Validator).
   - API Controller'ı oluştur ve Ocelot Gateway rotasını ekle.
   - **ÖNCE `l10n-agent`:** `.antigravity/rules/localization-standard.md` kuralına göre 8 dil `.resx` senkronizasyonunu `Resources/Views/{AreaName}/{ModuleName}/Index.{lang}.resx` yapısında tamamla.
   - Sadece projede desteklenen dillerde (8 dil) `.resx` dosyalarını `Resources/Views/{AreaName}/{ModuleName}/Index.{lang}.resx` yolunda oluştur.
   - **Kritik:** Ortak kelimeleri (`Kaydet`, `Sil` vb.) `SharedResource`'tan al, yazma. Sayfaya özel olan başlıkları ve tablo kolon anahtarlarını ekle.

4. **Phase 4: Arayüz (frontend-ui-ux)**
   - **[KRİTİK]:** `.antigravity/rules/frontend-datatable-template.md` dosyasını referans al.
   - `Views/{AreaName}/{ModuleName}/Index.cshtml` sayfasını oluştururken SADECE bu şablonu kopyala. Şablondaki HTML yapısına, Skeleton Loader, Bulk Action Bar ve Offcanvas yapılarına DOKUNMA, sadece değişkenleri doldur.
   - `Views/{AreaName}/{ModuleName}/_Filter.cshtml` partial view'ını oluştur.
   - `wwwroot/assets/js/{AreaName}/{ModuleName}/index.js` dosyasını `DtDefaults.create()` ve Module Pattern (IIFE) ile oluştur. Bakınız: `.antigravity/rules/frontend-js-standard.md`
   - `_LayoutBackbone` içine menü linkini ekle ve aktif state için `ViewContext.RouteData` dinamik kontrolü yap.

3.5. **Phase 3.5: Gateway Doğrulama (integration-agent)**
   - **[KRİTİK]:** `.antigravity/rules/routes.md` dosyasını oku.
   - `ocelot.json`'a yeni modül için **iki explicit rota** ekle:
     - `UpstreamPathTemplate: "/api/{resource}"` → `DownstreamPathTemplate: "/api/{resource}"`  (Port: 5050)
     - `UpstreamPathTemplate: "/api/{resource}/{everything}"` → `DownstreamPathTemplate: "/api/{resource}/{everything}"` (Port: 5050)
   - Her iki rotaya da `UpstreamHttpMethod`: `["GET", "POST", "PUT", "PATCH", "DELETE"]` ekle.
   - Yeni rotaları catch-all rotasından (`/services/mdm/{everything}`) **ÖNCE** konumlandır.
   - Gateway rotası eklenmeden "İşlem tamam" denilmez.

5. **Phase 5: Kalite & Güvenlik (testing-agent & security-agent)**
   - xUnit testlerini yaz (Tenant isolation check).
   - `/tenant-audit` komutunu çalıştırarak sızıntı kontrolü yap.

## ⚖️ Altın Kurallar
- **Sıfır İnisiyatif Kuralı:** Ajan, standart Liste/CRUD (DataTable) sayfaları için arayüz uyduramaz, kesinlikle Master Template'i kullanmak zorundadır.
- Modül mutlaka `MDM/` (veya ilgili Area) klasörü altında olmalıdır.
- Soft Delete ve TenantId filtrelemesi asla atlanamaz.
- Details/Edit gibi alt sayfalar yapıldığında Sneat PRO standartlarına ve 3'lü kart düzenine sadık kalınmalıdır.