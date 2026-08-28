---
description: "DB-001 — Diten ERP vNext MongoDB İndeksleme, Multi-Tenancy İzolasyonu ve Performans Standartları"
---

# MongoDB Index Kuralları (Diten ERP vNext)

Bu doküman, MongoDB veritabanı seviyesinde veri izolasyonunu garanti altına almak ve sorgu performansını en üst düzeyde tutmak için uyulması zorunlu kuralları tanımlar.

## 🛡️ Kritik Zorunluluk: Tenant-First Indexing

Diten ERP vNext "Siloed Data" mantığıyla çalıştığı için, tenant-owned her sorgu `TenantId` (GUID) filtresi ile başlar. Bu nedenle:

- **KURAL:** Her tenant-owned collection'da mutlaka `TenantId` ile başlayan bir **Compound Index (Bileşik İndeks)** bulunmalıdır.
- **Standart Format:** `{ "TenantId": 1, "Sık_Kullanılan_Alan": 1 }`
- **Neden:** `TenantId` içermeyen bir indeks, multi-tenant bir sistemde performans felaketine (COLLSCAN) yol açar.
- **İstisna:** Module pack içinde açıkça onaylanmış Platform global katalogları `GlobalEntity` kullanabilir. Bu collection'larda `TenantId` index'i aranmaz; global unique/query indexleri, `IsDeleted=false` davranışı ve RBAC erişimi doğrulanır.

[Image of a database index structure showing B-Tree organization for multi-tenant data partitioning]

---

## 🚀 İndeksleme Kılavuzu ve Best Practices

### 1. Sorgu ve Sıralama (Sort) Uyumu
- İndeksler, **Equality -> Sort -> Range (ESR)** kuralına göre tasarlanmalıdır.
- Örneğin; `SampleModule` tablosunda aktif kayıtları isme göre sıralamak için:
  `{ "TenantId": 1, "Status": 1, "Title": 1 }`

### 2. Tekil (Unique) İndeksler
- Bir verinin kiracı bazında tekil olması gerekiyorsa (Örn: Vergi Numarası), `unique: true` indeksi mutlaka `TenantId` içermelidir:
  `{ "TenantId": 1, "TaxNumber": 1 }` (Unique: true)
- Platform global kataloglarında global unique index ancak module pack'te gerekçelendirilmişse kullanılabilir.

### 3. Case-Insensitive Search (Collation)
- Arama yapılan alanlarda (Title, Name vb.) indeks tanımlanırken, büyük/küçük harf duyarlılığını ortadan kaldırmak için `Collation` desteği eklenmelidir.

---

## 🚨 Yasaklar ve Kısıtlamalar

- **Sınırsız Regex Yasaktır:** `^...` ile başlamayan (wildcard start) regex aramaları indeksi kullanamaz. Büyük

---

## 🧪 Test Veritabanları (DB-010)

- **Bir Mongo testi koşu başına yeni bir veritabanı YARATMAZ.** İzolasyon veritabanı adıyla değil,
  **kiracı kimliğiyle** sağlanır — üretimde nasıl sağlanıyorsa aynen öyle.
- **Bir test `MongoDbIndexConfigurations.EnsureIndexesAsync` ÇAĞIRMAZ.** O, üretim açılış yoludur ve
  platformun **tüm** şemasını kurar. Test yalnız ihtiyacı olan profili ister:
  ```csharp
  await PlatformSchemaManifest.ApplyAsync(database, new[] { SchemaProfile.BusinessReferenceData });
  ```
- **Doğru desen:** paylaşılan bir veritabanı + test başına yeni `TenantId`.
  Konusu kiracıya bağlı OLMAYAN bir test (veritabanı-geneli bir kural, idempotent olması gereken bir
  tohum) kendi veritabanını **sabit bir son ekle** alır — GUID ile değil.

### Neden — mekanizma, sayı değil

Her koleksiyon ve her indeks, işletim sisteminde **açık bir dosyadır**. Test sınıfı başına bir veritabanı
× platformun tam şeması = süreç başına dosya limiti. Limit aşılınca `mongod` `fassert` ile kendini öldürür;
ölünce `DisposeAsync` **hiç çalışmaz**, atılacak veritabanları birikir ve sonraki koşu enkazın üstüne başlar.

⚠ **Testler yeşilken düzenek çöker.** Hata testte değil, altyapıda görünür — ve `Connection refused` diye
okunur, "çok fazla dosya" diye değil. Teşhisi pahalı yapan budur.

### Yasak

⚠ **Kırmızıyı, muhafızın bilinen-ihlal listesine satır ekleyerek yeşile çevirmek YASAKTIR.**
O liste yalnız **küçülebilir**. Bir dosya düzeltildiğinde listeden düşmesini ikinci bir test zorunlu kılar;
bayat bir istisna bir deliktir — dosya düzelir, ruhsat kalır, sonraki ihlal bedava girer.

### Muhafız

`tests/architecture/TenantArchitecture.ArchitectureTests` — her push ve her PR'da CI koşuyor
(`scripts/run_phase1_gates.sh`). Yerelde:

```bash
dotnet test tests/architecture/TenantArchitecture.ArchitectureTests
```
