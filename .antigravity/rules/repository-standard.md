---
description: "REPO-001 — Diten ERP vNext Generic IRepository<T> Standardı ve Repository Katman Kuralları"
---

# Repository Standardı (Diten ERP vNext)

Bu doküman, Application katmanındaki generic repository interface'ini ve tüm modüllerde standart olarak bu yapının nasıl kullanılacağını belirler.

---

## 🏗️ Mimari: Repository Yapısı

Varsayılan standart generic repository'dir. Yeni modüllerde önce `IRepository<T>` / `GenericRepository<T>` altyapısı tercih edilir.

Golden referanslar ve bazı servis baseline'ları specific repository kullanabilir. Specific repository yalnızca şu şartlarla kabul edilir:

- Tenant filter tüm read/update/delete/bulk delete yollarında zorunlu uygulanır.
- Soft delete fiziksel silmenin yerine geçer.
- `TenantId` request/DTO/form payload'dan alınmaz.
- Standart CRUD tekrarları bilinçli ve module pack'te/onaylı baseline'da gerekçeli olmalıdır.

```
Application/Interfaces/
  IRepository<T>            ← Tek ve Ortak Interface — Tüm standart metodları içerir

Persistence/Repositories/
  GenericRepository<T>      ← IRepository<T>'yi implement eden ana sınıftır
```

---

## 📋 IRepository\<T\> Şablonu (Application katmanında tanımlanır)

Tüm standart ve ortak metodlar burada toplanır. Eğer bir entity için extra bir sorgu ihtiyacı doğarsa, generic interface yeni bir metodla (`FindOneAsync`, `CountAsync` vb.) genişletilir.

```csharp
// Application/Interfaces/IRepository.cs
namespace Diten.{Service}Service.Application.Interfaces;

public interface IRepository<T> where T : EntityBase
{
    Task<T> CreateAsync(T entity, CancellationToken ct = default);
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    
    // Yeni Eklenen Ortak Metodlar
    Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<long> CountAsync(CancellationToken ct = default);
}
```

---

## 📋 Kullanım Kuralı (MANDATORY)

1. **Default:** Handler'lar doğrudan `IRepository<Product>` inject etmelidir.
2. **Specific Repository İstisnası:** Module pack veya servis baseline'ı specific repository'yi açıkça seçiyorsa `I{Module}Repository` kullanılabilir.
3. **Garanti:** Specific repository kullanan modül, generic repository ile aynı tenant isolation ve soft delete güvenliğini sağlamak zorundadır.
4. **Ekstra Metod İhtiyacı:** Eğer bir entity için `GetByCodeAsync` gibi bir ihtiyaç varsa, önce generic altyapıya uygun çözüm değerlendirilir; specific repository seçildiyse metod sadece ilgili module interface'inde tutulabilir.

---

## 📋 GenericRepository\<T\> Implementasyonu (Persistence katmanında)

```csharp
// Persistence/Repositories/GenericRepository.cs
namespace Diten.{Service}Service.Persistence.Repositories;

public class GenericRepository<TEntity> : IRepository<TEntity>
    where TEntity : EntityBase
{
    // ... Standart RepositoryBase implementasyonu buraya taşınır ...
}
```

---

## ✅ Kontrol Listesi

- [ ] Generic repository veya onaylı specific repository baseline'ı açık mı?
- [ ] Handler'larda module pack ile uyumlu repository interface'i kullanılıyor mu?
- [ ] Repository tüm ortak metodları (BulkDelete vb.) tenant-aware ve soft-delete aware uyguluyor mu?
- [ ] DI kaydı generic veya specific repository seçimine göre net mi?
