---
description: "WORKFLOW-001 — Diten ERP vNext Yeni Endpoint ve CQRS Geliştirme Akışı"
---

# Workflow: Endpoint Ekle (CQRS)

Bu akış, sistemde yeni bir API ucu oluşturulurken izlenecek standart operasyon adımlarını ve klasör hiyerarşisini tanımlar.

## 📥 1. Gerekli Inputlar
- **HTTP Method + Route:** (Örn: POST `/api/legal-entities`)
- **Request/Response DTO Şeması:** Giriş ve çıkış veri modelleri.
- **Auth Gereksinimi:** (Public / Authorized / Policy)
- **Validation Kuralları:** Alan zorunlulukları ve formatlar.
- **Mongo Entity/Collection:** Verinin kaydedileceği hedef.

---

## ⚖️ 2. Kesin Kurallar

### 🛡️ Handler Savunma Hattı (Guard Clauses)
- **Null Check:** `Handle` metodunun en başında `ArgumentNullException.ThrowIfNull(request);` kullanılmalıdır.
- **Zorunlu Alan Doğrulaması:** FluentValidation dışında, Handler içinde iş mantığına başlamadan önce kritik alanlar için manuel kontroller (Örn: `string.IsNullOrWhiteSpace`) yapılmalıdır.
- **Veri Tutarlılığı & Tenant Shield:** Eğer bir `ParentId` veya ilişkili bir ID geliyorsa, işleme başlamadan önce bu ID'nin veritabanında var olduğu ve işlem yapan **TenantId**'ye ait olduğu `repository.ExistsAsync` ile doğrulanmalıdır.

### 🗑️ Veri Silme Mantığı (Soft Delete)
- Sistemde fiziksel silme (Hard Delete) **KESİNLİKLE YASAKTIR**.
- Silme işlemi, entity üzerindeki `IsDeleted = true` ve `DeletedAt = DateTime.UtcNow` alanlarının güncellenmesiyle yapılmalıdır.
- Repository katmanındaki tüm sorgular (Find/List) otomatik olarak `IsDeleted == false` filtresini içermelidir.

### 🚨 Hata Yönetimi ve Statü Kodları

Handler içinde hata oluştuğunda `return null`, `return false` ve `throw Exception` YASAKTIR.
Tüm iş mantığı hataları `Response<T>.Fail(message, statusCode)` ile döndürülür.
Bkz: `response-envelope.md`.

| Senaryo | Dönüş |
|---------|-------|
| Kayıt bulunamadı | `Response<T>.Fail("... not found.", 404)` |
| Duplicate kayıt | `Response<T>.Fail("Code already exists.", 409)` |
| Yetki eksikliği | `Response<T>.Fail("Insufficient permissions.", 403)` |
| Validation hatası | `ValidationBehavior` pipeline'ı otomatik döndürür (`400`) |
| Beklenmedik sistem hatası | `ExceptionHandlingBehavior` pipeline'ı otomatik yakalar (`500`) |

> **Güvenlik Notu:** Başka bir kiracıya ait ID ile işlem yapılmaya çalışıldığında sistemin varlığını açık etmemek için `403` yerine `404` döndürülür.

Controller tarafında:
```csharp
var response = await _mediator.Send(request, ct);
return CreateActionResultInstance(response); // CustomBaseController metodu
```

### 🌐 HTTP Method Standartları
- **Create:** `POST` kullanılır. Dönüş: `Response<Guid>.Success(id, 201)`.
- **Update:** `PUT /{id}` kullanılır. Dönüş: `Response<NoContent>.Success(204)`.
- **Partial Update / State:** `PATCH /{id}/{action}` kullanılır.
- **Delete:** `DELETE /{id}` kullanılır. Dönüş: `Response<NoContent>.Success(204)`.
- **Read (liste):** `GET` kullanılır. Dönüş: `Response<IReadOnlyList<T>>.Success(list)`.
- **Read (tekil):** `GET /{id}` kullanılır. Dönüş: `Response<TDto>.Success(dto)` veya `Response<TDto>.Fail("not found", 404)`.

### 🚫 Controller Temizliği
- Controller dosyası içerisinde **ASLA** `record` veya `class` (Request/Response DTO) tanımı yapılamaz.
- Controller sadece API uçlarını yöneten, MediatR'a komut gönderen "ince" bir katman olarak kalmalıdır.

### 📁 Klasör Hiyerarşisi (Zorunlu)
Her bir feature (Örn: `SampleModule`) altında şu yapı kurulmalıdır:

- **`Requests/`**: API'den gelen ham istek modelleri.
- **`Commands/`**: MediatR `IRequest` modelleri (Sadece veri taşır).
- **`Queries/`**: MediatR sorgu modelleri.
- **`Handlers/`**: İş mantığının (logic) döndüğü yer.
  - **`CommandHandlers/`**: Yazma (Insert/Update/Delete) sınıfları.
  - **`QueryHandlers/`**: Okuma (Get) sınıfları.
- **`Validators/`**: FluentValidation sınıfları.

### 🔒 Güvenlik ve Tenant
- **DTO’lar TenantId İçermez:** Kiracı bilgisi her zaman header (`X-Tenant-Id`) üzerinden alınır. DTO içine asla `TenantId` alanı eklenmez.
- **Repository:** Veriye erişim sadece `tenant enforced` olan repository metodları üzerinden yapılmalıdır.

---

## 🚀 3. Uygulama Sıralaması

1. **Ön Kontrol:** `Application/Behaviors/` klasörü ve 4 pipeline behavior mevcut mu? `CustomBaseController` mevcut mu? Eksikse önce kur.
2. **Önce Plan:** Kod yazmadan önce dosya yapısını ve akış planını sun ve onay al.
3. **Requests & Commands:** `IRequest<Response<T>>` formatında istek modellerini oluştur.
4. **Validators:** FluentValidation sınıflarını yaz. Validator olmadan Handler yazılamaz.
5. **Handlers:** `Handlers/CommandHandlers` veya `Handlers/QueryHandlers` altına yaz. `Response<T>.Fail()` / `Response<T>.Success()` kullan. Guard clause'ları ilk satırlara yaz.
6. **Controller:** `CustomBaseController`'dan miras alan controller yaz. `return CreateActionResultInstance(response)` kullan. `[HasPermission(...)]` ekle.

---
Diten ERP vNext Endpoint Standard - WORKFLOW-001