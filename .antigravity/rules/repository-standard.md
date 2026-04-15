---
description: "REPO-001 — Diten ERP vNext Generic IRepository<T> Standardı ve Repository Katman Kuralları"
---

# Repository Standardı (Diten ERP vNext)

Bu doküman, Application katmanındaki generic repository interface'ini ve tüm modüllerde standart olarak bu yapının nasıl kullanılacağını belirler.

---

## 🏗️ Mimari: Tek Katmanlı Generic Repository Yapısı (YENİ STANDART)

Diten ERP vNext mimarisinde kod karmaşıklığını azaltmak ve hızı artırmak için **Specific Repository Interface** kullanımı **YASAKLANMIŞTIR**. Tüm handler'lar doğrudan `IRepository<T>` interface'ini kullanır.

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

1. **Specific Interface YASAKTIR:** `IProductRepository`, `ISkuRepository` gibi interface'ler oluşturulamaz.
2. **Generic Injection:** Handler'lar doğrudan `IRepository<Product>` inject etmelidir.
3. **Ekstra Metod İhtiyacı:** Eğer bir entity için `GetByCodeAsync` gibi bir ihtiyaç varsa, bu metod `IRepository<T>` içine generic bir şekilde (`FindOneAsync` gibi) eklenmeli veya `GenericRepository` üzerinden çözülmelidir.

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

- [x] Specific interface'ler kaldırıldı mı?
- [x] Handler'larda doğrudan `IRepository<T>` kullanılıyor mu?
- [x] `RepositoryBase` veya `GenericRepository` tüm ortak metodları (BulkDelete vb.) içeriyor mu?
- [x] DI kaydı `services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>))` şeklinde mi?
