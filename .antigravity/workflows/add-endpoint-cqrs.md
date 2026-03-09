---
description: "WORKFLOW-001 — Diten ERP vNext Yeni Endpoint ve CQRS Geliştirme Akışı"
---

# Workflow: Endpoint Ekle (CQRS)

Bu akış, sistemde yeni bir API ucu oluşturulurken izlenecek standart operasyon adımlarını ve klasör hiyerarşisini tanımlar.

## 📥 1. Gerekli Inputlar
- **HTTP Method + Route:** (Örn: POST `/api/v1/legal-entities`)
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
Handler içinde hata oluştuğunda asla `return null` veya `return false` dönülmez. Uygun exception fırlatılır (throw). Bu exception'lar Global Exception Middleware tarafından şu HTTP kodlarına dönüştürülür:

- **ArgumentNullException / ValidationException:** `400 Bad Request`.
- **KeyNotFoundException:** `404 Not Found` (Kayıt yok veya başka bir Tenant'a ait veriye erişim denemesi).
- **UnauthorizedAccessException:** `403 Forbidden`.
- **ConflictException:** `409 Conflict`.

> **Güvenlik Notu:** Başka bir kiracıya ait ID ile işlem yapılmaya çalışıldığında, sistemin varlığını açık etmemek için `403` yerine `404 Not Found` fırlatılmalıdır.

### 🌐 HTTP Method Standartları
- **Create:** `POST` kullanılır.
- **Update:** Aksiyon bazlı tutarlılık ve firewall/proxy uyumluluğu açısından **POST** tercih edilir. (Örn: `POST /api/legal-entities/{id}`)
- **Delete:** `DELETE` veya `POST /delete` kullanılabilir.
- **Read:** `GET` kullanılır.

### 🚫 Controller Temizliği
- Controller dosyası içerisinde **ASLA** `record` veya `class` (Request/Response DTO) tanımı yapılamaz.
- Controller sadece API uçlarını yöneten, MediatR'a komut gönderen "ince" bir katman olarak kalmalıdır.

### 📁 Klasör Hiyerarşisi (Zorunlu)
Her bir feature (Örn: `LegalEntities`) altında şu yapı kurulmalıdır:

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

1. **Önce Plan:** Kod yazmadan önce dosya yapısını ve akış planını sun ve onay al.
2. **Requests & Commands:** İstek modellerini ve MediatR komutlarını oluştur.
3. **Handlers:** İlgili `Handlers/` klasörü altına iş mantığını ve **Soft Delete/Guard Clause** kontrollerini yaz.
4. **Validation & Index:** Gerekli doğrulamaları ekle ve veritabanı performansını (index) kontrol et.
5. **Controller:** API ucunu tanımla ve MediatR çağrısını yap.

---
Diten ERP vNext Endpoint Standard - WORKFLOW-001