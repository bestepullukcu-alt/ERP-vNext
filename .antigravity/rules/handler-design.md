---
description: "HANDLER-001 — Diten ERP vNext Handler Tasarım Kuralları ve Sorumluluk Sınırları"
---

# Handler Tasarım Standardı (Diten ERP vNext)

Bu doküman, handler sınıflarının ne yapıp yapamayacağını, sorumluluk sınırlarını ve dış servis erişim kurallarını tanımlar.

---

> **Naming standardı:** Handler isimlerinde `Command` / `Query` / `Request` suffix **YOKTUR**.
> Sadece `{Verb}{Module}Handler` formatı kullanılır (Golden Reference: `CreateGoldenReferenceSlimHandler`).
> Request tipi `Command` / `Query` suffix'li record olarak Application'da tanımlıdır.
> Folder: `Handlers/CommandHandlers/` ve `Handlers/QueryHandlers/` ayrı klasörlerde tutulur.

## 🎯 Temel Kural: Tek Sorumluluk

Bir handler şunu yapar:
1. Guard clause'ları çalıştır (null, duplicate, tenant kontrolü)
2. Entity kur veya güncelle
3. Repository üzerinden persist et
4. `Response<T>` döndür

**Bunların dışında olan her şey** ayrı bir servise aittir.

---

## 🚫 Handler'a Giremeyen Sorumluluklar

| Yasak | Doğru Yer |
|-------|-----------|
| Email / SMS gönderme | `INotificationService` (Infrastructure) |
| Dış servis HTTP çağrısı | `IUserServiceClient`, `ITenantServiceClient` interface'leri |
| Child entity upsert + parent entity persist (birlikte) | Alt servis veya ayrı command |
| OpenAI / AI servisi çağrısı | `IAiService` interface (Infrastructure) |
| Dosya/blob yükleme | `IStorageService` interface (Infrastructure) |
| Domain event dispatch | `IDomainEventDispatcher` |

---

## 👤 ICurrentUserContext — CreatedBy / UpdatedBy Zorunluluğu

İş modüllerinde (`Products`, `SampleModule`, vb.) `CreatedBy` ve `UpdatedBy` handler'da set edilir.
Bunun için `ICurrentUserContext` inject edilir — DTO'dan asla alınmaz.

### ICurrentUserContext Interface Şablonu

```csharp
// Application/Interfaces/ICurrentUserContext.cs
namespace Diten.{Service}Service.Application.Interfaces;

public interface ICurrentUserContext
{
    Guid UserId { get; }
    string UserName { get; }
}
```

### Handler'da Kullanım (Create)

```csharp
public sealed class CreateProductHandler
    : IRequestHandler<CreateProductCommand, Response<Guid>>
{
    private readonly IProductRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public CreateProductHandler(
        IProductRepository repository,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<Guid>> Handle(
        CreateProductCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await _repository.ExistsByCodeAsync(request.Code.Trim(), null, ct))
            return Response<Guid>.Fail("A product with this code already exists.", 409);

        var entity = new Product
        {
            Code           = request.Code.Trim(),
            Name           = request.Name.Trim(),
            CreatedBy      = _currentUser.UserId,  // ZORUNLU — iş modülü
        };

        var created = await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(created.Id, 201);
    }
}
```

### Handler'da Kullanım (Update)

```csharp
public async Task<Response<NoContent>> Handle(
    UpdateProductCommand request, CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(request);

    var existing = await _repository.GetByIdAsync(request.Id, ct);
    if (existing is null)
        return Response<NoContent>.Fail("Product not found.", 404);

    existing.Name      = request.Name.Trim();
    existing.UpdatedBy = _currentUser.UserId;  // ZORUNLU — iş modülü

    await _repository.UpdateAsync(existing, ct);  // UpdatedAt → RepositoryBase set eder
    return Response<NoContent>.Success(204);
}
```

> **Kural:** `CreatedBy` → sadece Create handler'da set edilir.
> `UpdatedBy` → sadece Update handler'da set edilir.
> `UpdatedAt` → `RepositoryBase.UpdateAsync` içinde otomatik set edilir, handler'da tekrar yazılmaz.

---

## ✅ İzin Verilen Handler Yapısı (Tam Şablon)

---

## ❌ Yasak Handler Yapısı

```csharp
// ❌ Çok fazla sorumluluk — bu handler reddedilir
public async Task<Response<Guid>> Handle(CreateTaskCommand request, CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(request);

    // 1. Ana entity kur
    var task = new WorkTask { ... };
    await _taskRepository.InsertAsync(task, ct);

    // 2. Alt entity'leri de burada upsert et — YASAK
    foreach (var sub in request.SubTasks)
    {
        var subTask = new SubTask { ParentId = task.Id, ... };
        await _subTaskRepository.InsertAsync(subTask, ct);
    }

    // 3. Email gönder — YASAK
    await _emailService.SendAsync(new TaskCreatedEmail(task), ct);

    // 4. Dış servisi çağır — YASAK
    var user = await _httpClient.GetAsync($"/users/{request.AssigneeId}");

    return Response<Guid>.Success(task.Id, 201);
}
```

**Bu handler 4 ayrı sorumluluğu taşıyor. Şöyle bölünmeli:**
- `CreateTaskHandler` → sadece task entity'yi oluşturur
- `CreateSubTasksHandler` → alt görevleri oluşturur (ayrı command)
- `INotificationService.NotifyTaskCreatedAsync()` → email notification
- `IUserServiceClient.GetByIdAsync()` → kullanıcı bilgisi

---

## 🔌 Dış Servis Client Interface'leri

Başka bir mikroservise HTTP ile ulaşılacaksa doğrudan `HttpClient` kullanılmaz.
`Application` katmanında bir interface tanımlanır, `Infrastructure`'da implement edilir.

```csharp
// Application/Abstractions/IUserServiceClient.cs
public interface IUserServiceClient
{
    Task<UserDto?> GetByIdAsync(Guid userId, CancellationToken ct = default);
}

// Infrastructure/Clients/UserServiceClient.cs
public sealed class UserServiceClient : IUserServiceClient
{
    private readonly HttpClient _httpClient;

    public async Task<UserDto?> GetByIdAsync(Guid userId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetFromJsonAsync<UserDto>(
            $"/api/users/{userId}", ct);
        return response;
    }
}
```

Handler sadece interface'i kullanır, implementasyonu bilmez:

```csharp
public sealed class AssignTaskHandler
    : IRequestHandler<AssignTaskCommand, Response<NoContent>>
{
    private readonly ITaskRepository _repository;
    private readonly IUserServiceClient _userClient; // ✅ Interface kullanıyor

    public async Task<Response<NoContent>> Handle(
        AssignTaskCommand request, CancellationToken ct)
    {
        var user = await _userClient.GetByIdAsync(request.AssigneeId, ct);
        if (user is null)
            return Response<NoContent>.Fail("Assignee not found.", 404);

        // ...
        return Response<NoContent>.Success(204);
    }
}
```

---

## 🛡️ Guard Clause Şablonu (Her Handler'da Zorunlu)

```csharp
// 1. Null check — her handler'ın ilk satırı
ArgumentNullException.ThrowIfNull(request);

// 2. İlişkili ID varsa varlık + tenant kontrolü
if (!await _categoryRepository.ExistsAsync(request.CategoryId, ct))
    return Response<T>.Fail("Category not found.", 404);

// 3. Duplicate kontrolü
if (await _repository.ExistsByCodeAsync(request.Code, ct))
    return Response<T>.Fail($"Code '{request.Code}' already exists.", 409);
```

---

## ✅ Kontrol Listesi

- [ ] Handler tek bir aggregate/entity üzerinde çalışıyor mu?
- [ ] Email/SMS/bildirim işlemleri `INotificationService` üzerinden mi?
- [ ] Dış servis çağrısı interface üzerinden mi? (doğrudan `HttpClient` yok)
- [ ] Child entity upsert ayrı command'a mı bırakıldı?
- [ ] İlk satır `ArgumentNullException.ThrowIfNull(request)` mi?
- [ ] İlişkili ID'lerin tenant kontrolü yapıldı mı?
- [ ] Handler başarı/hata için `Response<T>` döndürüyor mu? (`throw` değil)
- [ ] Update/Delete/Patch command'ları `Response<NoContent>` döndürüyor mu? (`Response<bool>` değil)
- [ ] İş modülü ise Create'de `entity.CreatedBy = _currentUser.UserId` set ediliyor mu?
- [ ] İş modülü ise Update'de `entity.UpdatedBy = _currentUser.UserId` set ediliyor mu?
- [ ] `ICurrentUserContext` inject edildi mi? (DTO'dan UserId alınmıyor)
- [ ] Yardımcı metodlar (Helper sınıfları) `throw` yerine tuple döndürüyor mu?
- [ ] FluentValidation'da zaten olan kontroller handler/helper'da tekrarlanmıyor mu?
