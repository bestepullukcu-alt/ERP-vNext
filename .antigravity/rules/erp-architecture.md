---
description: "ERP-ARCH-001 — Diten ERP vNext Mikroservis Katmanlama, Bağımlılık ve CQRS Standartları"
---

# ERP Mimari Kuralları (Diten ERP vNext)

Bu doküman, sistemdeki her bir mikroservisin (MDM, Auth vb.) sahip olması gereken katmanlı mimari yapısını ve bu katmanlar arasındaki etkileşim kurallarını belirler.

## 🏗️ Katmanlı Mimari Yapısı (Layering)

Her mikroservis aşağıdaki 5 temel projeden oluşmalıdır:

1. **`<Service>.Api` (Presentation):** Dış dünyaya açılan kapı. Controller'lar, Middleware'ler ve Swagger burada yer alır.
2. **`<Service>.Application` (Orchestration):** İş mantığının (Business Logic) kalbi. CQRS Command/Query'ler, Handler'lar, Mapping ve Validation buradadır.
3. **`<Service>.Domain` (Core):** Sistemin en iç katmanı. Entity'ler, Value Object'ler, Domain Exception'lar ve Repository Interface'leri buradadır.
4. **`<Service>.Persistence` (Data Access):** MongoDB implementasyonu. DbContext, Repository sınıfları ve Tenant-Filter mantığı burada hapsedilir.
5. **`<Service>.Infrastructure` (Cross-Cutting):** Mail, SMS, File Storage gibi dış servis entegrasyonlarını barındırır.



---

## 🛡️ Bağımlılık Kuralları (Zorunlu)

- **Akış Yönü:** `Api -> Application -> Domain`.
- **Domain Bağımsızlığı:** Domain katmanı en merkezdedir; diğer hiçbir katmanı (Application, Api, Persistence) referans alamaz.
- **Ters Bağımlılık YASAK:** Üst katmanlar alt katmanları bilir, ancak alt katmanlar üsttekilerden habersizdir.
- **Soyutlama:** `Application` katmanı veritabanına doğrudan erişmez; `Domain` içinde tanımlanmış Interface'leri (`IRepository`) kullanır.

---

## ⚡ CQRS & MediatR Disiplini

- **Sıfır İş Mantığı (Controller):** Controller içinde `if-else` veya business logic bulunamaz. Sadece isteği alır ve MediatR üzerinden ilgili Command/Query'ye paslar.
- **Handler Bağımsızlığı:** Her Handler sadece kendi işinden sorumludur.
- **Validation Pipeline:** FluentValidation kullanılarak oluşturulan validasyonlar, Handler çalışmadan önce MediatR Pipeline üzerinden otomatik tetiklenmelidir.

---

## 🍃 Persistence (MongoDB) Standartları

- **Driver İzolasyonu:** `MongoDB.Driver` ve `MongoDB.Bson` kütüphaneleri sadece `Persistence` projesinde referanslanmalıdır. **Domain katmanında (`using MongoDB.Bson;` dahil) MongoDB import YASAKTIR.** İstisna: `BsonRepresentation` attribute zorunluysa yalnızca o attribute import edilebilir.
- **Otomatik Tenant Filtresi:** Her sorgu, anayasada belirtilen `X-Tenant-Id` (GUID) değerini otomatik olarak veritabanı seviyesinde filtrelemelidir.
- **Tracking:** Okuma işlemlerinde performans için `AsNoTracking` benzeri yaklaşımlar (Mongo için projeksiyonlar) tercih edilmelidir.

---

## 🚨 Genel Uygulama Kuralları

1. **Asenkron Yapı:** Tüm Girdi/Çıktı (I/O) işlemleri `async` olmalı ve `CancellationToken` mutlaka en alt katmana kadar iletilmelidir.
2. **Hata Yönetimi:** Tüm hatalar (Business veya System) `ProblemDetails` formatında, merkezi bir `GlobalExceptionHandler` üzerinden dönülmelidir.
3. **DTO Kullanımı:** Katmanlar arası veri taşıma için her zaman DTO'lar (Data Transfer Objects) kullanılmalıdır; Entity'ler asla API yanıtı olarak dönülmemelidir.
4. **Interface Standartı:** Bağımlılıklar (DI) her zaman Interface'ler üzerinden yönetilmelidir.

---

## 🧱 EntityBase Zorunlu Alanlar

> 📖 Detaylar için bkz: `.antigravity/rules/entity-base-template.md`

Her entity `EntityBase`'ten miras almalıdır. `EntityBase`, aşağıdaki alanları **otomatik sağlar** — entity içinde tekrar tanımlanmaz:

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | MongoDB `_id` |
| `TenantId` | `Guid` | Multi-tenant izolasyon anahtarı |
| `IsDeleted` | `bool` | Soft Delete flag |
| `DeletedAt` | `DateTimeOffset?` | Soft Delete timestamp |
| `CreatedAt` | `DateTimeOffset` | Oluşturma zamanı (UTC) |
| `UpdatedAt` | `DateTimeOffset?` | Güncelleme zamanı — `UpdateAsync` içinde set edilmeli |

**Opsiyonel Audit Alanları** (User-aware modüllerde manuel eklenir):
- `CreatedBy (Guid?)` — `[BsonRepresentation(BsonType.String)]` ile
- `UpdatedBy (Guid?)` — `[BsonRepresentation(BsonType.String)]` ile

---

## � RBAC Permission Key Formatı

Controller endpoint'lerinde kullanılan `[HasPermission]` attribute'u için standart format:

```
[HasPermission("Modules.{ModuleName}.{Action}")]
```

**Örnekler:**
- `[HasPermission("Modules.Countries.Read")]`
- `[HasPermission("Modules.Countries.Create")]`
- `[HasPermission("Modules.LegalEntities.Delete")]`

**Actions:** `Read`, `Create`, `Update`, `Delete`, `BulkDelete`

Eğer bir endpoint henüz RBAC'a bağlanmadıysa `[Authorize]` ile koruma altında tutulur. `[AllowAnonymous]` sadece Public health check endpoint'leri için kabul edilir.

---

## �📛 İsimlendirme Standartları (Naming Standards)

### Global İsimlendirme Zorunluluğu

1. **Yerel İsimlendirme YASAK:** Alan adları (field names) ülke veya bölge spesifik isimlendirme içeremez.
   - ❌ YANLIŞ: `PlateCode` (TR-spesifik plaka kodu)
   - ✅ DOĞRU: `Code` (genel kod alanı)

2. **Genel ERP Standartları:** Tüm alan adları global ERP standartlarına uygun olmalıdır:
   - `Code` yerine `PlateCode`, `CityCode`, `RegionCode` gibi spesifik isimler KULLANILMAZ
   - Ülke/bölge ayrımı yapılacaksa `CountryId`, `RegionId` gibi referans alanları kullanılır

3. **Koordinat Alanları:** Coğrafi veri içeren modüllerde standart alanlar:
   - `Latitude` (double?) - Enlem
   - `Longitude` (double?) - Boylam
   - Bu alanlar PRD'de belirtilmişse MUTLAKA eklenmelidir

---

## 🔒 Interface Seviyesinde Zorunluluklar

### Multi-Tenancy ve Soft-Delete Garantisi

1. **Repository Interface Sözleşmesi:** Her Repository interface'i, TenantId ve Soft-Delete filtrelerinin uygulandığını XML comment ile BELİRTMELİDİR:
   ```csharp
   /// <summary>
   /// Repository for {Entity} operations.
   /// All queries automatically filter by TenantId and IsDeleted=false.
   /// </summary>
   public interface I{Entity}Repository
   ```

2. **Implementasyon Garantisi:** Repository implementasyonu `RepositoryBase<TEntity>` sınıfından miras almalı ve `TenantFilter` kullanmak ZORUNDADIR.

3. **Controller Seviyesinde Tenant Doğrulama:** API endpoint'leri, tenant izolasyonunu sağlamak için `[HasPermission]` attribute ile korunmalıdır.

---

## ✅ Kontrol Listesi
- [ ] Proje yapısı 5 katmana uygun mu?
- [ ] Domain katmanında `using MongoDB.Bson;` veya `using MongoDB.Driver;` import var mı? (varsa ihlal)
- [ ] Controller içinde MediatR dışında bir mantık var mı?
- [ ] MongoDB implementasyonu sadece Persistence içinde mi?
- [ ] Alan isimleri global ERP standartlarına uygun mu? (PlateCode → Code kontrolü)
- [ ] Repository interface'inde TenantId/Soft-Delete garantisi XML comment olarak yazılmış mı?
- [ ] PRD'deki TÜM alanlar Entity'ye eklendi mi? (Latitude, Longitude vb.)
- [ ] `UpdateAsync` içinde `entity.UpdatedAt = DateTimeOffset.UtcNow` var mı?
- [ ] `DeleteAsync` içinde hem `IsDeleted = true` hem `DeletedAt = UtcNow` set ediliyor mu?
- [ ] `[HasPermission("Modules.{Module}.{Action}")]` veya en az `[Authorize]` koruması var mı?
- [ ] Entity-base-template.md okundu mu? (Zorunlu/Opsiyonel alanlar doğru uygulandı mı?)
