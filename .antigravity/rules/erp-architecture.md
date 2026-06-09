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

## 📁 Dosya ve Sınıf Organizasyonu (Action-Based Separation)

> **Tek gerçek standart:** Golden Reference Slim/Compact kodu.
> - Backend: `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceSlim/`
> - Pack-of-record: `execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md`

Kodun okunabilirliğini ve bakımını kolaylaştırmak için CQRS yapıları **Grup Dosyaları** (örn: `ProductCommands.cs`) içinde tutulamaz:

1. **Her Command için ayrı dosya:** `CreateProductCommand.cs`, `UpdateProductCommand.cs` (sealed record, `IRequest<Response<T>>`).
2. **Her Query için ayrı dosya:** `GetProductListQuery.cs`, `GetProductByIdQuery.cs` (sealed record).
3. **Her Handler için ayrı dosya:** `CreateProductHandler.cs`, `DeleteProductHandler.cs` (sealed class, **Command/Query/Request suffix YOK**).
4. **Her Validator için ayrı dosya:** `CreateProductValidator.cs` (**Command suffix YOK**).
5. **Folder yapısı (Golden Reference birebir):**
   ```
   Features/{Module}/
   ├── Commands/                            ← her command ayrı dosya
   ├── Queries/                             ← her query ayrı dosya
   ├── Handlers/CommandHandlers/            ← AYRI klasör (zorunlu)
   ├── Handlers/QueryHandlers/              ← AYRI klasör (zorunlu)
   ├── Validators/                          ← her validator ayrı dosya
   └── {Module}Models.cs                    ← TEK dosyada tüm DTO/ViewModel'ler
   ```
6. **Kural:** Bir dosya içinde birden fazla public sınıf **KESİNLİKLE YASAKTIR**.

**Yasaklar:**
- ❌ Handler isminde `*CommandHandler.cs` / `*QueryHandler.cs` / `*RequestHandler.cs` suffix
- ❌ `Handlers/` tek klasörü (CommandHandlers/QueryHandlers ayrımı olmadan)
- ❌ `Features/{Module}/Requests/Commands/` gibi ekstra alt katman

---

## 🍃 Persistence (MongoDB) Standartları

- **Driver İzolasyonu:** `MongoDB.Driver` ve `MongoDB.Bson` kütüphaneleri sadece `Persistence` projesinde referanslanmalıdır. **Domain katmanında (`using MongoDB.Bson;` dahil) MongoDB import YASAKTIR.** İstisna: `BsonRepresentation` attribute zorunluysa yalnızca o attribute import edilebilir.
- **Otomatik Tenant Filtresi:** Her sorgu, anayasada belirtilen `X-Tenant-Id` (GUID) değerini otomatik olarak veritabanı seviyesinde filtrelemelidir.
- **Tracking:** Okuma işlemlerinde performans için `AsNoTracking` benzeri yaklaşımlar (Mongo için projeksiyonlar) tercih edilmelidir.

---

## 🚨 Genel Uygulama Kuralları

1. **Asenkron Yapı:** Tüm Girdi/Çıktı (I/O) işlemleri `async` olmalı ve `CancellationToken` mutlaka en alt katmana kadar iletilmelidir.
2. **Hata Yönetimi:** İş mantığı hataları (kayıt yok, duplicate, yetki) `Response<T>.Fail()` ile döndürülür. Beklenmedik system hataları `ExceptionHandlingBehavior` pipeline katmanı tarafından yakalanır ve `500` olarak sarılır. `throw Exception` sadece kritik infrastructure hatalarında kullanılabilir. Bkz: `response-envelope.md`.
3. **DTO Kullanımı:** Katmanlar arası veri taşıma için her zaman DTO'lar (Data Transfer Objects) kullanılmalıdır; Entity'ler asla API yanıtı olarak dönülmemelidir.
4. **Interface Standartı:** Bağımlılıklar (DI) her zaman Interface'ler üzerinden yönetilmelidir.
5. **Pipeline Behaviors (4 Zorunlu):** Her mikroserviste `ValidationBehavior`, `LoggingBehavior`, `ExceptionHandlingBehavior`, `PerformanceBehavior` sırasıyla kayıtlı olmalıdır. Bkz: `pipeline-behaviors.md`.
6. **Handler Tasarımı:** Handler'lar tek bir aggregate üzerinde çalışır. Email, dış servis çağrısı, alt entity upsert ayrı servis/command'a aittir. Bkz: `handler-design.md`.
7. **Kod Stili:** Tüm yorumlar ve log mesajları İngilizce. PascalCase property, `_camelCase` private field. Bkz: `code-style.md`.

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

## 🔐 RBAC Permission Key Formatı

Controller endpoint'lerinde kullanılan `[HasPermission]` attribute'u için iki format servis bazlı kabul edilir:

| Servis | Format | Örnek |
|---|---|---|
| `Diten.Platform` (Platform admin shell) | `platform.{resource}.{action}` | `platform.administrators.read` |
| `Diten.MdmService`, `Diten.DevEnablementService`, `Diten.AuthService` (Tenant shell) | `{module}.{resource}.{action}` | `module.sample-module.read` |

**Karar kuralı:** Controller hangi servisin API katmanındaysa o format kullanılır. Module pack'te `service` ve `shell` alanları bu kararı tetikler.

**Örnekler:**
- Platform admin: `[HasPermission("platform.tenants.create")]`, `[HasPermission("platform.subscription-plans.update")]`
- Tenant: `[HasPermission("module.sample-module.read")]`, `[HasPermission("module.products.delete")]`

**Actions:** `read`, `create`, `update`, `delete`, `bulk-delete` (+ modül-spesifik aksiyonlar: `suspend`, `archive`, `assign-roles`, vb.)

**Policy:**
- Platform admin controller'ları: `[Authorize(Policy = "PlatformActor")]`
- Tenant controller'ları: `[Authorize]`

Eğer bir endpoint henüz RBAC'a bağlanmadıysa `[Authorize]` ile koruma altında tutulur. `[AllowAnonymous]` sadece Public health check endpoint'leri için kabul edilir.

`actor_type=platform_admin` claim'i tüm permission kontrollerini otomatik geçer (bkz: `API/Security/HasPermissionAttribute.cs`).

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

## 🏷️ Domain Enum Zorunluluğu

Lookup collection'larında (`LifecycleState`, `ItemType`, `TrackingPolicy` vb.) saklanan `Code` değerleri iş mantığında (handler, helper, validator) kullanılıyorsa, bu kodlar **Domain katmanında enum olarak da tanımlanmalıdır.**

```csharp
// Domain/Enums/ProductEnums.cs
public enum ProductLifecycleStateCode
{
    Draft = 1,
    Active = 2,
    Blocked = 3,
    Obsolete = 4
}
```

> **Kural:** Handler veya helper içinde `"DRAFT"`, `"ACTIVE"` gibi string literal ile durum kontrolü **YASAKTIR**.
> Lookup entity'nin `Code` alanı ile Domain enum arasında mapping yapılır.
> Bkz: `code-style.md § Magic String Yasağı`

---

## 🔒 Interface Seviyesinde Zorunluluklar

### Multi-Tenancy ve Soft-Delete Garantisi

1. **Generic Repository Sözleşmesi:** Tüm veri erişim işlemleri `IRepository<T>` üzerinden yürütülür. TenantId ve Soft-Delete filtreleri `GenericRepository<T>` seviyesinde garanti edilir.

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
- [ ] `[HasPermission("module.resource.action")]` veya en az `[Authorize]` koruması var mı?
- [ ] Entity-base-template.md okundu mu? (Zorunlu/Opsiyonel alanlar doğru uygulandı mı?)
- [ ] `Response<T>` envelope kullanılıyor mu? (`response-envelope.md`)
- [ ] Tüm controller'lar `CustomBaseController`'dan miras alıyor mu?
- [ ] 4 pipeline behavior kayıtlı mı ve sırası doğru mu? (`pipeline-behaviors.md`)
- [ ] Handler'lar tek sorumluluk ilkesine uyuyor mu? (`handler-design.md`)
- [ ] Tüm yorumlar ve log mesajları İngilizce mi? (`code-style.md`)
- [ ] Lookup kodları (lifecycle, status) Domain enum olarak tanımlı mı? (string literal yasak)
- [ ] Specific repository YASAKLANMIŞTIR. Doğrudan `IRepository<T>` kullanılıyor mu? (`repository-standard.md`)
