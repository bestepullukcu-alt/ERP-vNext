---
description: "WORKFLOW-002 — Diten ERP vNext Yeni MongoDB Koleksiyonu ve Veri Modeli Geliştirme Akışı"
---

# Workflow: Mongo Collection Ekle

Bu akış, veritabanı seviyesinde izolasyonu ve performansı korumak için izlenecek standart operasyon adımlarını tanımlar.

## 📥 1. Gerekli Inputlar
- **Entity Tanımı:** Koleksiyon adı ve barındıracağı alanlar (C# class yapısı).
- **Benzersizlik (Unique):** Hangi alanların kiracı bazında tekil olması gerektiği.
- **Sorgu Profili:** En sık kullanılacak filtreleme ve sıralama (sort) senaryoları.

---

## 🛡️ 2. Uygulama Kuralları (Mühürlü)

1. **İzolasyon Kuralı:** Tenant-owned entity sınıfı servisin `TenantId` taşıyan kiracı taban sınıfından (ad servise göre değişir — ör. Platform.Common: `TenantScopedEntity`; Auth/MDM: `EntityBase`; DevEnablement: `BaseEntity`; bkz. `entity-base-template.md`) türemeli ve `Guid TenantId` taşımalıdır. Platform global katalog istisnası (`GlobalEntity`) yalnız module pack'te açıkça yazılırsa geçerlidir (`multi-tenancy.md`).
2. **Endeksleme (Indexing):** `{ "TenantId": 1, "Sık_Filtre_Alanı": 1 }` şeklinde bir bileşik indeks (Compound Index) oluşturulmadan koleksiyon yayına alınamaz.
3. **Repository Standartı:** Sorgular her zaman Repository katmanı üzerinden yapılmalı; `TenantId` filtresi veritabanı sürücüsü seviyesinde veya Repository içinde zorlanmalıdır (Tenant Enforced).
4. **Bson Mapping:** Tarih alanları (`DateTime`) ve benzersiz kimlikler (`Guid`) doğru BSON tipleriyle eşleştirilmelidir.



---

## 🚀 3. Uygulama Sıralaması

1. **Domain Katmanı:** Hedef servisin Domain projesinde Entity sınıfını ve repository sözleşmesini oluştur.
2. **Persistence Katmanı:** Hedef servisin Persistence projesinde repository implementasyonunu hazırla.
3. **İndeksleme:** `Persistence` katmanındaki `Context` veya `Seed` dosyalarında `CreateIndex` tanımlarını yap.
4. **Validation:** Verinin koleksiyona girmeden önceki şema doğrulamasını (FluentValidation) hazırla.

---

## ✅ Kontrol Listesi
- [ ] Entity sınıfı servis baseline'ındaki tenant-aware base entity'den türüyor mu?
- [ ] `TenantId` içeren bir Compound Index tanımlandı mı?
- [ ] Benzersizlik (Unique) kuralı `TenantId` kapsıyor mu?
- [ ] Tüm asenkron işlemler `CancellationToken` alıyor mu?
