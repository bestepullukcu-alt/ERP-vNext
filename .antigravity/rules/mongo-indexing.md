---
description: "DB-001 — Diten ERP vNext MongoDB İndeksleme, Multi-Tenancy İzolasyonu ve Performans Standartları"
---

# MongoDB Index Kuralları (Diten ERP vNext)

Bu doküman, MongoDB veritabanı seviyesinde veri izolasyonunu garanti altına almak ve sorgu performansını en üst düzeyde tutmak için uyulması zorunlu kuralları tanımlar.

## 🛡️ Kritik Zorunluluk: Tenant-First Indexing

Diten ERP vNext "Siloed Data" mantığıyla çalıştığı için, her sorgu `TenantId` (GUID) filtresi ile başlar. Bu nedenle:

- **KURAL:** Her collection'da mutlaka `TenantId` ile başlayan bir **Compound Index (Bileşik İndeks)** bulunmalıdır.
- **Standart Format:** `{ "TenantId": 1, "Sık_Kullanılan_Alan": 1 }`
- **Neden:** `TenantId` içermeyen bir indeks, multi-tenant bir sistemde performans felaketine (COLLSCAN) yol açar.

[Image of a database index structure showing B-Tree organization for multi-tenant data partitioning]

---

## 🚀 İndeksleme Kılavuzu ve Best Practices

### 1. Sorgu ve Sıralama (Sort) Uyumu
- İndeksler, **Equality -> Sort -> Range (ESR)** kuralına göre tasarlanmalıdır.
- Örneğin; `LegalEntities` tablosunda aktif kayıtları isme göre sıralamak için:
  `{ "TenantId": 1, "Status": 1, "Title": 1 }`

### 2. Tekil (Unique) İndeksler
- Bir verinin kiracı bazında tekil olması gerekiyorsa (Örn: Vergi Numarası), `unique: true` indeksi mutlaka `TenantId` içermelidir:
  `{ "TenantId": 1, "TaxNumber": 1 }` (Unique: true)

### 3. Case-Insensitive Search (Collation)
- Arama yapılan alanlarda (Title, Name vb.) indeks tanımlanırken, büyük/küçük harf duyarlılığını ortadan kaldırmak için `Collation` desteği eklenmelidir.

---

## 🚨 Yasaklar ve Kısıtlamalar

- **Sınırsız Regex Yasaktır:** `^...` ile başlamayan (wildcard start) regex aramaları indeksi kullanamaz. Büyük