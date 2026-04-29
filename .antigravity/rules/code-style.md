---
description: "STYLE-001 — Diten ERP vNext Kod Stili, İsimlendirme ve Yorum Kuralları"
---

# Kod Stili Standardı (Diten ERP vNext)

Bu doküman, tüm mikroservislerde tutarlı kod stili ve isimlendirme kurallarını tanımlar.

---

## 🗣️ Yorum Dili: Sadece İngilizce

```csharp
// ✅ DOĞRU
// Validate that the category belongs to the current tenant before assigning.
if (!await _categoryRepository.ExistsAsync(request.CategoryId, ct))
    return Response<Guid>.Fail("Category not found.", 404);

// ❌ YANLIŞ — Türkçe yorum
// Kategorinin mevcut tenant'a ait olduğunu doğrula
```

**Kural:** Kod içi tüm yorumlar, XML doc comment'ler ve log mesajları İngilizce yazılır.
Hata mesajları (`Response<T>.Fail(...)` içindeki string'ler) de İngilizce olur.

---

## 📛 İsimlendirme Kuralları

### Sınıf ve Interface

| Yapı | Kural | Örnek |
|------|-------|-------|
| Sınıf | PascalCase | `ProductRepository` |
| Interface | `I` + PascalCase | `IProductRepository` |
| Abstract sınıf | PascalCase | `CustomBaseController` |
| Record | PascalCase | `CreateProductRequest` |
| Enum | PascalCase | `ProductType` |

### Method ve Property

| Yapı | Kural | Örnek |
|------|-------|-------|
| Public method | PascalCase | `GetByIdAsync` |
| Private method | PascalCase | `BuildTenantFilter` |
| Public property | PascalCase | `IsActive`, `CreatedAt` |
| Private field | `_camelCase` | `_repository`, `_tenantContext` |
| Async method | `...Async` suffix | `InsertAsync`, `GetAllAsync` |

### Property İsimlendirme Hataları

```csharp
// ❌ YANLIŞ — camelCase property
public bool isActive { get; set; }
public bool isVirtual { get; set; }
public bool isOnGoing { get; set; }

// ✅ DOĞRU — PascalCase
public bool IsActive { get; set; }
public bool IsVirtual { get; set; }
public bool IsOngoing { get; set; }
```

### Yazım Hataları (Typo Kara Listesi)

```
❌ Infrastucture   →   ✅ Infrastructure
❌ Persistance     →   ✅ Persistence
❌ Authentification →  ✅ Authentication
❌ Repositary      →   ✅ Repository
❌ Mediater        →   ✅ Mediator
```

---

## 📁 Namespace ve Dosya Kuralları

- **File-scoped namespace** zorunludur:
  ```csharp
  // ✅ DOĞRU
  namespace Diten.MdmService.Application.Features.Products;

  // ❌ YANLIŞ
  namespace Diten.MdmService.Application.Features.Products
  {
      ...
  }
  ```

- **Sınıf ismi = Dosya ismi** — her dosyada tek public sınıf.

- **Klasör ismi = Namespace** — fiziksel klasör yapısı namespace'i yansıtır.

---

## 🔧 C# Dil Özellikleri

### Nullable

```xml
<!-- Her .csproj'da zorunlu -->
<Nullable>enable</Nullable>
```

Null olabilecek tüm referans tipleri `?` ile işaretlenir:
```csharp
public string? Description { get; set; }
public Guid? CategoryId { get; set; }
```

### Default değerler

```csharp
// String property — boş string ile başlat
public string Name { get; set; } = string.Empty;

// Liste property — boş koleksiyon ile başlat
public List<string> Tags { get; set; } = [];

// Nullable — init etme
public string? Description { get; set; }
```

### Async

```csharp
// ✅ Tüm I/O işlemleri async
public async Task<Response<ProductDto>> Handle(
    GetProductByIdQuery query, CancellationToken ct) { ... }

// ✅ CancellationToken en alt katmana kadar iletilir
await _repository.GetByIdAsync(id, ct);
await _collection.FindAsync(filter, cancellationToken: ct);

// ❌ .Result veya .Wait() kullanımı yasak
var result = _repository.GetByIdAsync(id).Result; // YASAK
```

### Record vs Class

```csharp
// Command ve Query modelleri için record tercih edilir (immutable)
public sealed record CreateProductRequest(
    string Code,
    string Name,
    Guid CategoryId) : IRequest<Response<Guid>>;

// Entity'ler class olarak yazılır
public sealed class Product : EntityBase { ... }
```

---

## 🚫 Magic String Yasağı (Domain Kodları)

Lookup/reference tablosundaki durum kodları (lifecycle state, status, category code vb.) iş mantığında **asla hardcoded string olarak kullanılamaz**. Domain katmanında bir enum tanımlanır ve kontrol noktalarında bu enum kullanılır.

```csharp
// ❌ YANLIŞ — Magic string
if (lifecycleState.Code == "DRAFT") { ... }
var allowed = currentCode switch { "ACTIVE" => new[] { "BLOCKED" }, ... };

// ✅ DOĞRU — Domain enum
if (lifecycleState.Code == ProductLifecycleStateCode.Draft.ToString()) { ... }
var allowed = currentState switch { ProductLifecycleStateCode.Active => new[] { ... }, ... };
```

> **Kural:** Domain'de enum yoksa önce enum oluştur, sonra iş mantığını yaz.
> Bkz: `erp-architecture.md § Domain Enum Zorunluluğu`

---

## 🗂️ ImplicitUsings

```xml
<!-- Her .csproj'da zorunlu -->
<ImplicitUsings>enable</ImplicitUsings>
```

Global using'ler proje bazlı `GlobalUsings.cs` dosyasında toplanabilir:

```csharp
// Application/GlobalUsings.cs
global using MediatR;
global using FluentValidation;
global using Diten.Shared.Core;
```

---

---

## 🏗️ Backend & Frontend Tutarlılığı

1. **Property Case:** 
   - **Backend (C#):** PascalCase (örn: `ModuleVersion`)
   - **Frontend (JSON/JS):** camelCase (örn: `moduleVersion`)
   - **Razor:** `@Model.PropertyName` (C# ile aynı case)

2. **Yeniden Adlandırma (Renaming):**
   Bir property adı değiştiğinde tüm yığın (stack) kontrol edilmelidir:
   - Domain Entity
   - Application DTOs & Mappings
   - Validators
   - Frontend ViewModels & Controllers
   - Frontend Razor Views (`asp-for`, `@Model`)
   - Frontend JavaScript (DataTable `data` fields, API calls)

> [!WARNING]
> Sadece Backend'de yapılan bir rename, Frontend'de sessizce başarısız olan (silent failure) veya validasyon hatalarına (400 Bad Request) sebep olan kırılmalara yol açar. Her rename sonrası mutlaka **hem Backend hem Frontend** projeleri derlenmelidir.

## ✅ Kontrol Listesi

- [ ] Kod içi yorumlar ve log mesajları İngilizce mi?
- [ ] Tüm property'ler PascalCase mi? (`isActive` → `IsActive`)
- [ ] Private field'lar `_camelCase` mi?
- [ ] Async method'lar `Async` suffix içeriyor mu?
- [ ] File-scoped namespace kullanılıyor mu?
- [ ] "Infrastructure" doğru yazılmış mı? (`Infrastucture` typo yok)
- [ ] Nullable açık mı? (`<Nullable>enable</Nullable>`)
- [ ] String property'ler `= string.Empty` ile başlatılıyor mu?
- [ ] `.Result` veya `.Wait()` kullanımı var mı? (varsa ihlal)
- [ ] Lookup/referans kodları (lifecycle, status) Domain enum ile mi temsil ediliyor? (string literal yasak)
