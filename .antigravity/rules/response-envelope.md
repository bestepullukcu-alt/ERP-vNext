---
description: "RESPONSE-001 — Diten ERP vNext Response<T> Envelope ve CustomBaseController Standardı"
---

# Response Envelope Standardı (Diten ERP vNext)

Bu doküman, tüm mikroservislerde handler'ların dönüş tipini ve controller'ların HTTP yanıt üretme şeklini tanımlar.

---

## 🎯 Temel Felsefe

Handler'lar exception fırlatmak yerine `Response<T>` döndürür.
Controller'lar `CreateActionResultInstance()` ile HTTP status kodunu bu envelope'dan türetir.

> `throw Exception` → sadece kritik infrastructure hatalarında kullanılabilir (örn. veritabanına ulaşılamadı).
> İş mantığı hataları (kayıt yok, doğrulama hatası, yetki eksikliği) her zaman `Response<T>.Fail()` ile döner.

---

## 📦 Response\<T\> Sınıf Şablonu

```csharp
namespace Diten.Shared.Core;

public sealed class Response<T>
{
    public T? Data { get; private set; }
    public int StatusCode { get; private set; }
    public bool IsSuccessful { get; private set; }
    public IReadOnlyList<string> Errors { get; private set; } = [];

    private Response() { }

    public static Response<T> Success(T data, int statusCode = 200)
        => new() { Data = data, StatusCode = statusCode, IsSuccessful = true };

    public static Response<T> Success(int statusCode = 200)
        => new() { StatusCode = statusCode, IsSuccessful = true };

    public static Response<T> Fail(string error, int statusCode = 400)
        => new() { StatusCode = statusCode, IsSuccessful = false, Errors = [error] };

    public static Response<T> Fail(IReadOnlyList<string> errors, int statusCode = 400)
        => new() { StatusCode = statusCode, IsSuccessful = false, Errors = errors };
}
```

### Yaygın Kullanım Kalıpları

| Senaryo | Dönüş |
|---------|-------|
| Başarılı oluşturma | `Response<Guid>.Success(newId, 201)` |
| Başarılı okuma | `Response<ProductDto>.Success(dto)` |
| Kayıt bulunamadı | `Response<ProductDto>.Fail("Product not found.", 404)` |
| Validation hatası | `Response<NoContent>.Fail(errors, 400)` |
| Yetki eksikliği | `Response<NoContent>.Fail("Insufficient permissions.", 403)` |
| Çakışma | `Response<NoContent>.Fail("Code already exists.", 409)` |

---

## 🏛️ CustomBaseController Şablonu

```csharp
namespace Diten.{Service}.Api.Controllers;

[ApiController]
public abstract class CustomBaseController : ControllerBase
{
    protected IActionResult CreateActionResultInstance<T>(Response<T> response)
    {
        return response.StatusCode switch
        {
            200 => Ok(response),
            201 => Created(string.Empty, response),
            204 => NoContent(),
            400 => BadRequest(response),
            403 => StatusCode(403, response),
            404 => NotFound(response),
            409 => Conflict(response),
            _   => StatusCode(response.StatusCode, response)
        };
    }
}
```

Tüm controller'lar `ControllerBase` yerine `CustomBaseController`'dan miras alır:

```csharp
// ✅ DOĞRU
public sealed class ProductsController : CustomBaseController { ... }

// ❌ YANLIŞ
public sealed class ProductsController : ControllerBase { ... }
```

---

## ⚡ Handler Dönüş Tipi Kuralı

Handler'ların MediatR request tipi `IRequest<Response<T>>` formatında olmalıdır.

> **Naming standardı (Golden Reference):**
> - Command record'u: `{Verb}{Module}Command` suffix
> - Query record'u: `Get{Module}{Qualifier}Query` suffix
> - Handler class'ı: `{Verb}{Module}Handler` (Command/Query suffix **YOK**)

```csharp
// Create command — yeni kaydın ID'sini döndürür
public sealed record CreateProductCommand : IRequest<Response<Guid>>;

// Update / Delete / Patch command — veri döndürmez, NoContent kullanılır
public sealed record UpdateProductCommand : IRequest<Response<NoContent>>;
public sealed record DeleteProductCommand(Guid Id) : IRequest<Response<NoContent>>;
public sealed record ChangeProductLifecycleCommand : IRequest<Response<NoContent>>;

// Query — DTO döndürür
public sealed record GetProductByIdQuery(Guid Id) : IRequest<Response<ProductDetailDto>>;
public sealed record GetProductListQuery : IRequest<Response<IReadOnlyList<ProductListItemDto>>>;
```

> **Kural:** Update / Delete / Patch komutları **asla** `Response<bool>` döndürmez. `bool` dönüş tipi anlamsızdır — başarı durumu HTTP status kodu ile (`204 NoContent`) ifade edilir.

### Handler içinde kullanım

```csharp
public sealed class CreateProductHandler
    : IRequestHandler<CreateProductCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(
        CreateProductCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Duplicate kontrolü
        if (await _repository.ExistsByCodeAsync(request.Code, ct))
            return Response<Guid>.Fail("A product with this code already exists.", 409);

        var product = new Product { Code = request.Code, ... };
        product.TenantId = _tenantContext.TenantId; // server-side set

        var id = await _repository.InsertAsync(product, ct);
        return Response<Guid>.Success(id, 201);
    }
}
```

### Controller içinde kullanım

```csharp
[HttpPost]
[HasPermission(ProductPermissions.Products.Create)]
public async Task<IActionResult> Create(
    [FromBody] CreateProductCommand request, CancellationToken ct)
{
    var response = await _mediator.Send(request, ct);
    return CreateActionResultInstance(response);
}
```

---

## 🔗 Yardımcı Metotlar (Helper / Service Sınıfları)

Handler, iş mantığını yardımcı bir sınıfa (`ProductLogicHelper` vb.) delege ediyorsa, **bu yardımcı sınıflar da `throw` yerine sonuç döndürmek zorundadır.**

`throw` kural dışına çıkmaz — `ExceptionHandlingBehavior` tüm exception'ları `500` olarak sarar. Doğru status kodu (`409`, `404`, `400`) sadece `Response<T>.Fail()` ile mümkündür.

### Yardımcı metot dönüş pattern'i

```csharp
// ✅ DOĞRU — tuple ile hata bilgisi döndür
internal static class ProductLogicHelper
{
    public static async Task<(bool IsValid, string? Error, int StatusCode)> ValidateUpsertAsync(
        ProductUpsertRequestBase request,
        Guid? excludeId,
        IProductRepository repository,
        CancellationToken ct)
    {
        if (await repository.ExistsByCodeAsync(request.Code.Trim(), excludeId, ct))
            return (false, "A product with this code already exists.", 409);

        if (!ProductCatalog.IsCategoryValidForProductType(request.CategoryId, request.ProductType))
            return (false, "Selected category does not belong to the selected product type.", 400);

        return (true, null, 0);
    }
}

// Handler içinde kullanım:
var (isValid, error, statusCode) = await ProductLogicHelper.ValidateUpsertAsync(request, null, _repository, ct);
if (!isValid)
    return Response<Guid>.Fail(error!, statusCode);
```

> **Not:** FluentValidation'da zaten kontrol edilen alanlar (örn. `ProductType.IsInEnum()`) yardımcı metotta **tekrar kontrol edilmez.** `ValidationBehavior` handler'dan önce çalışır — duplicate kontrol gereksiz ve yanıltıcıdır.

---

## 🚫 Yasak Kullanımlar

```csharp
// ❌ Handler'dan null dönmek
return null;

// ❌ Handler'dan false dönmek — başarı HTTP 204 ile ifade edilir
return false;

// ❌ Write command'larda Response<bool>
public sealed class UpdateProductCommand : IRequest<Response<bool>>  // YANLIŞ
public sealed class DeleteProductCommand : IRequest<Response<bool>>  // YANLIŞ

// ❌ İş mantığı hatası için exception fırlatmak (handler veya helper'da)
throw new KeyNotFoundException("Product not found.");    // 404 yerine 500 döner
throw new InvalidOperationException("Code exists.");    // 409 yerine 500 döner

// ❌ FluentValidation'da zaten olan kontrolü helper'da tekrar yapmak
if (!Enum.IsDefined(request.ProductType))               // Validator'da IsInEnum() zaten var
    throw new InvalidOperationException("...");

// ❌ Controller'da manuel status code
return NotFound();
return Ok(result);

// ❌ ControllerBase'ten miras almak
public sealed class ProductsController : ControllerBase
```

---

## ✅ Kontrol Listesi

- [ ] Servis içinde `Response<T>` sınıfı tanımlandı mı?
- [ ] `CustomBaseController` oluşturuldu mu?
- [ ] Tüm controller'lar `CustomBaseController`'dan miras alıyor mu?
- [ ] Tüm command/query dönüş tipleri `IRequest<Response<T>>` formatında mı?
- [ ] Update/Delete/Patch command'ları `Response<NoContent>` döndürüyor mu? (`Response<bool>` değil)
- [ ] Handler'larda `throw` yerine `Response<T>.Fail()` kullanılıyor mu?
- [ ] Handler'dan çağrılan yardımcı metotlar da `throw` yerine tuple döndürüyor mu?
- [ ] FluentValidation'da zaten olan kontroller handler/helper'da tekrarlanmıyor mu?
- [ ] Controller'larda `return CreateActionResultInstance(response)` kullanılıyor mu?
