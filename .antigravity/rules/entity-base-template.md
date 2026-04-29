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

Bu alanlar **EntityBase içinde değildir**. İlgili modülün türüne göre aşağıdaki politika uygulanır:

### Eklenme Politikası

| Modül Türü | `CreatedBy` / `UpdatedBy` | Örnek |
|------------|--------------------------|-------|
| **İş Modülü** (kullanıcı aksiyonu içeren) | **ZORUNLU** | Tasks, Orders, Invoices |
| **Referans / Seed Veri** (sistem tarafından yönetilen) | **YASAK** | Currencies, LifecycleStates, Categories |
| **Sistem Kaydı** (arka plan işlemi) | **OPSIYONEL** | AuditLog, SystemEvent |

> **Karar sorusu:** "Bu kaydı kim oluşturdu?" sorusu kullanıcıya gösterilecek mi veya iş kuralına girdi mi?
> Evet → ekle. Hayır → ekleme.

```csharp
// Entity içine manuel ekle (iş modüllerinde zorunlu):
[BsonRepresentation(BsonType.String)]
public Guid? CreatedBy { get; set; }  // User ID who created this record

[BsonRepresentation(BsonType.String)]
public Guid? UpdatedBy { get; set; }  // User ID who last updated this record
```

Handler içinde set edilme şekli:

```csharp
// Handler içinde — token'dan alınır, DTO'dan değil
entity.CreatedBy = _currentUserContext.UserId;   // oluşturmada
entity.UpdatedBy = _currentUserContext.UserId;   // güncellemede
```

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

> **ÖNEMLİ:** `I{Entity}Repository` daima `IRepository<{Entity}>`'den extend eder.
> Standart CRUD metotları (`CreateAsync`, `GetByIdAsync`, `GetAllAsync`, `UpdateAsync`, `DeleteAsync`, `ExistsAsync`) generic base'den gelir, specific interface'e **tekrar yazılmaz**.
> Bkz: `.antigravity/rules/repository-standard.md`

```csharp
// Specific interface — sadece entity'e özgü metodlar
public interface I{Entity}Repository : IRepository<{Entity}>
{
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId, CancellationToken ct = default);
    // Standart CRUD buraya YAZILMAZ — IRepository<T>'den gelir
}

// Concrete implementation
public sealed class {Entity}Repository : RepositoryBase<{Entity}>, I{Entity}Repository
{
    public {Entity}Repository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "{collection_name}") { }

    // Sadece entity-specific metodlar implement edilir
    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId, CancellationToken ct = default)
    {
        var filter = Builders<{Entity}>.Filter.And(
            TenantFilter,
            Builders<{Entity}>.Filter.Eq(x => x.Code, code));
        if (excludeId.HasValue)
            filter &= Builders<{Entity}>.Filter.Ne(x => x.Id, excludeId.Value);
        return await Collection.Find(filter).AnyAsync(ct);
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
- [ ] Modül türü kontrol edildi mi? İş modülü ise `CreatedBy`/`UpdatedBy` eklendi mi?
- [ ] `CreatedBy`/`UpdatedBy` DTO'dan değil, `_currentUserContext.UserId`'den set ediliyor mu?
