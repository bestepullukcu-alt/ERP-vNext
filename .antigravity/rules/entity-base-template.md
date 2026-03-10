# EntityBase Reference Template (Diten ERP vNext)

Bu dosya, Diten ERP vNext'teki tüm MongoDB entity'lerinin miras aldığı `EntityBase` sınıfının referans belgesidir.
Yeni bir entity yazan ajan, bu belgede hangi alanların otomatik olarak miras alındığını görerek **aynı alanları entity'ye tekrar eklemez**.

---

## 📋 EntityBase Zorunlu Alanları

Herhangi bir entity `EntityBase`'ten miras aldığında aşağıdaki alanlar **otomatik olarak** eklenir. Bunları entity içinde TEKRAR TANIMLAMA:

```csharp
// EntityBase içindeki alanlar (tekrar yazmak yasak):
Guid   Id          // MongoDB _id alanı (GUID, BsonRepresentation String)
Guid   TenantId    // Multi-tenant izolasyon anahtarı (ZORUNLU)
bool   IsDeleted   // Soft Delete flag (default: false)
DateTimeOffset? DeletedAt   // Soft Delete timestamp (UTC)
DateTimeOffset  CreatedAt   // Kayıt oluşturma zamanı (UTC, otomatik)
DateTimeOffset? UpdatedAt   // Son güncelleme zamanı (UTC, UpdateAsync'te set edilmeli)
```

## 📋 Opsiyonel Audit Alanları

Bu alanlar **EntityBase içinde değildir**. User-aware modüllerde (örn. LegalEntities, Users) entity'ye **manuel olarak** eklenir:

```csharp
// Entity içine manuel ekle (gerekirse):
[BsonRepresentation(BsonType.String)]
Guid? CreatedBy  // İşlemi yapan kullanıcının ID'si

[BsonRepresentation(BsonType.String)]
Guid? UpdatedBy  // Son güncelleyen kullanıcının ID'si
```

> **Ne zaman eklenecek?** Modülde "Kimin oluşturdu?" sorusu iş gereksinimi ise ekle. MDM referans veriler (Countries, Cities, Currencies) bu alanları gerektirmez.

---

## 🛠️ Yeni Entity Yazma Şablonu

```csharp
namespace Diten.{Service}Service.Domain.Entities;

/// <summary>
/// {Entity} entity for {description}.
/// Inherits Id, TenantId, IsDeleted, DeletedAt, CreatedAt, UpdatedAt from EntityBase.
/// All queries automatically filter by TenantId and IsDeleted=false.
/// </summary>
public class {Entity} : EntityBase
{
    // ── Required Fields ──────────────────────────────────────────
    [Required]
    public string FieldName { get; set; } = string.Empty;

    // ── Optional Fields ───────────────────────────────────────────
    public string? OptionalField { get; set; }

    // ── Status ────────────────────────────────────────────────────
    public bool IsActive { get; set; } = true;

    // ── Audit (opsiyonel, gerekirse ekle) ─────────────────────────
    // [BsonRepresentation(BsonType.String)]
    // public Guid? CreatedBy { get; set; }
    // [BsonRepresentation(BsonType.String)]
    // public Guid? UpdatedBy { get; set; }
}
```

---

## ⚠️ Kritik Kurallar

1. **MongoDB.Bson importu Domain'de YASAK:** `using MongoDB.Bson;` veya `using MongoDB.Bson.Serialization.Attributes;` satırları Domain (Entity) dosyalarında **kullanılamaz**. Bu kullanımlar mimari ihlaldir (bkz. `erp-architecture.md`). Tek istisna: Audit alanları için `[BsonRepresentation]` gerekli ise yalnızca o attribute import edilebilir.

2. **UpdatedAt manuel set edilir:** Repository'nin `UpdateAsync` metodu içinde `entity.UpdatedAt = DateTimeOffset.UtcNow;` satırı **zorunludur**. Otomatik değil.

3. **TenantId hiçbir zaman DTO içinde taşınmaz:** Handler içinde `entity.TenantId = _tenantContext.TenantId;` şeklinde server-side set edilir.

---

## 🏗️ Repository Şablonu (Özet)

```csharp
public sealed class {Entity}Repository : RepositoryBase<{Entity}>, I{Entity}Repository
{
    public {Entity}Repository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "{collection_name}") { }

    public async Task<bool> UpdateAsync({Entity} entity, CancellationToken ct = default)
    {
        var filter = Builders<{Entity}>.Filter.And(
            TenantFilter,
            Builders<{Entity}>.Filter.Eq(e => e.Id, entity.Id));

        entity.UpdatedAt = DateTimeOffset.UtcNow; // ZORUNLU
        var result = await Collection.ReplaceOneAsync(filter, entity, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<{Entity}>.Filter.And(
            TenantFilter,
            Builders<{Entity}>.Filter.Eq(e => e.Id, id));

        var update = Builders<{Entity}>.Update
            .Set(e => e.IsDeleted, true)
            .Set(e => e.DeletedAt, DateTimeOffset.UtcNow); // ZORUNLU

        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
```

---

## ✅ Entity Yazım Kontrol Listesi

- [ ] `EntityBase`'ten miras alınıyor mu?
- [ ] `Id`, `TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`, `UpdatedAt` entity içinde TEKRAR tanımlanmadı mı?
- [ ] `using MongoDB.Bson;` Domain katmanında kullanılmıyor mu?
- [ ] `Repository.UpdateAsync` içinde `entity.UpdatedAt = DateTimeOffset.UtcNow` var mı?
- [ ] `Repository.DeleteAsync` içinde hem `IsDeleted = true` hem `DeletedAt = UtcNow` set ediliyor mu?
- [ ] `TenantId` hiçbir DTO veya Request Body'de bulunmuyor mu?
