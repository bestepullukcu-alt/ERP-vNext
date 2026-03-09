---
description: "[Tenant İzolasyonu ve Veri Güvenliği Denetim Akışı — Diten ERP vNext]"
---
# Workflow: Tenant Güvenlik Denetimi (Audit)

Bu denetimin ana amacı, sistemdeki "Kiracı Sızıntısı" (Tenant Leak) risklerini tespit etmek ve veri izolasyonunun her katmanda %100 sağlandığını garanti etmektir.

---

## 🔍 1. Kritik Denetim Noktaları

### Veritabanı Katmanı (Persistence & Mongo)
- [ ] **Filtresiz Sorgular:** `TenantId` filtresi içermeyen veya `RepositoryBase` üzerinden geçmeyen ham (raw) Mongo sorguları var mı?
- [ ] **Eksik Arayüzler:** `ITenantDocument` veya `BaseTenantDocument` uygulamayan Entity sınıfları var mı?
- [ ] **İndeks Denetimi:** `TenantId` ile başlamayan koleksiyon indeksleri var mı? (Performans ve sızıntı riski).
- [ ] **İzolasyon İhlali:** `Persistence` katmanı dışında (örneğin Application veya Api içinde) `MongoDB.Driver` kullanımı var mı?

[Image of a multi-tenant database isolation architecture showing tenant data partitioning and filtering mechanisms]

### Uygulama Katmanı (Application & CQRS)
- [ ] **DTO Denetimi:** Request DTO'ları veya Body yapıları içinde `TenantId` alanı var mı? (Bu bilgi sadece Header'dan alınmalıdır).
- [ ] **Handler Bağımsızlığı:** Bir Handler, `ITenantContext` dışından manuel bir TenantId kabul ediyor mu?
- [ ] **Cross-Tenant İşlemler:** Bir kiracının ID'sini (GUID) kullanarak başka bir kiracıya ait veriye (Details/Update/Delete) erişim denetimi (Authorization) eksik mi?

### Sunum Katmanı (Api & Controller)
- [ ] **İş Kuralları:** Controller içinde veritabanı sorgusu veya `if-else` gibi iş kuralları var mı? (Mimari ihlal).
- [ ] **Header Zorunluluğu:** `X-Tenant-Id` header'ını zorunlu tutmayan (Public yollar hariç) endpoint'ler var mı?

---

## 📊 2. Denetim Çıktısı (Audit Report)

Her denetim sonunda aşağıdaki formatta bir "Bulgu Listesi" sunulmalıdır:

| Risk Seviyesi | Dosya Yolu | Tespit Edilen Bulgu | Önerilen Düzeltme |
|:---:|---|---|---|
| 🔴 KRİTİK | `Diten.MDM.Persistence/Repos/CityRepo.cs` | Ham sorguda TenantId filtresi yok. | `ApplyTenantFilter()` metodunu kullan. |
| 🟡 ORTA | `Diten.MDM.Application/DTOs/CityDto.cs` | DTO içinde TenantId alanı bulundu. | Alanı DTO'dan kaldır, Header'dan oku. |
| 🔵 DÜŞÜK | `Diten.Web/Views/MDM/Cities/Index.cshtml` | Skeleton Loader eksik. | `_SkeletonLoader` partial view ekle. |

---

## 🚀 3. Aksiyon Planı

1. **Tespit:** `Explorer` ve `Debugger` ajanları ile yukarıdaki maddeleri tara.
2. **Raporla:** Bulgu listesini kullanıcıya sun ve onay al.
3. **Düzelt:** Onaylanan bulguları mühürlü "Anayasa" (Rules) ve "Workflows" dosyalarına göre refactor et.

---
Diten ERP vNext Tenant Safety Shield - AUDIT-001