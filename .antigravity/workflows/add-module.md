---
description: "WORKFLOW-000 — Yeni Modül Oluşturma Orkestrasyonu (Ana Senaryo)"
---

# /add-module - Yeni Modül Oluşturma

Bu workflow, bir modülün sıfırdan son kullanıcıya ulaşana kadarki tüm katmanlarını koordine eder.

## 🎭 Görev Dağılımı (Orkestra)

1. **Phase 1: Analiz (business-analyst)**
   - Modülün alanlarını (fields), IFRS/KVKK gereksinimlerini ve 8 dil anahtarlarını belirle.
2. **Phase 2: Veri Mimarisi (data-agent & backend-architect)**
   - MongoDB koleksiyonunu tasarla (`ITenantDocument` tabanlı).
   - Domain Entity ve Repository katmanını oluştur.
3. **Phase 3: İş Mantığı (backend-architect & l10n-agent)**
   - `/add-endpoint-cqrs` akışını başlat (Request, Command, Handler, Validator).
   - `.resx` dosyalarına 8 dilde çevirileri işle.
4. **Phase 4: Arayüz (frontend-ui-ux)**
   - `_LayoutBackbone` kullanarak Index (DataTable) ve Details sayfalarını oluştur.
   - `window.L10n` bridge ve Skeleton Loader entegrasyonunu yap.
5. **Phase 5: Kalite & Güvenlik (testing-agent & security-agent)**
   - xUnit testlerini yaz (Tenant isolation check).
   - `/tenant-audit` komutunu çalıştırarak sızıntı kontrolü yap.

## ⚖️ Altın Kurallar
- Modül mutlaka `MDM/` klasörü altında olmalıdır.
- Soft Delete ve TenantId filtrelemesi asla atlanamaz.
- UI, Sneat PRO standartlarına ve 3'lü kart düzenine (Details) sadık kalmalıdır.