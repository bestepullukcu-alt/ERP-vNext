---
description: "RULE-002 — Multi-Tenant (Single DB) Kesin Uygulama Kuralları"
---

# 🛡️ Multi-Tenant (Single DB) — KESİN KURALLAR

Bu kurallar, Diten ERP vNext ekosistemindeki veri izolasyonunun ve kiracı güvenliğinin anayasasıdır.

---

## 📋 Standartlar
- **Tenant Header:** `X-Tenant-Id` (Case-sensitive)
- **Format:** Standart GUID string (Örn: `550e8400-e29b-41d4-a716-446655440000`)
- **Mongo Şeması:** Varsayılan olarak her tenant-owned dokümanda `Guid TenantId` alanı bulunması **ZORUNLUDUR**. Sadece aşağıdaki Platform global catalog istisnası bunun dışındadır.

### Platform Global Catalog İstisnası

Tenant'a ait olmayan, Platform seviyesinde tekil system-of-record olan katalog dokümanları `GlobalEntity` kullanabilir. Bu istisna yalnızca module pack içinde açıkça yazılırsa geçerlidir:

- `Entity Fields` içinde base type `GlobalEntity` olarak belirtilir.
- `Runtime Constraints` içinde kaydın tenant-owned olmadığı ve neden global olduğu açıklanır.
- Repo scope Platform-owned servisle sınırlı kalır.
- DTO/request/form payload içinde `TenantId` yine yasaktır.
- Normal iş/tenant verileri için bu istisna kullanılamaz.

---

## ⚖️ Pazarlık Yok (Hard Rules)

1. **Giriş Yasak:** `TenantId` asla Request Body, DTO veya Query Parameter üzerinden kabul edilemez.
2. **Tek Kaynak:** `TenantId` sadece `X-Tenant-Id` header'ından, `TenantResolutionMiddleware` aracılığıyla çözülür.
3. **Zorunlu Filtre:** Her tenant-owned okuma/sorgu (Select/Find) `TenantId` ile filtrelenmek **ZORUNDADIR**. Module pack ile onaylanmış `GlobalEntity` kataloglarında tenant filtresi aranmaz; bunun yerine `IsDeleted=false` ve global erişim/RBAC kontrolleri aranır.
4. **Server-Side Set:** Her tenant-owned yazma (Insert/Update) işlemi, `TenantId` bilgisini `ITenantContext` üzerinden sunucu tarafında set etmek **ZORUNDADIR**.
5. **Güvenlik İhlali:** Filtre içermeyen herhangi bir MongoDB sorgusu "Kritik Bug" ve "Güvenlik İhlali" olarak kabul edilir.
6. **HttpClient Entegrasyonu:** `Diten.Web` projesinde `HttpClient` ile giden tüm isteklerde bu header zorunludur. Geliştirme/Test aşamasında (seed data yoksa) varsayılan değer olarak `00000000-0000-0000-0000-000000000000` (Guid.Empty) kullanılmalıdır. **Asla '1' veya 'admin' gibi string değerler kullanılamaz.**
7. **CORS Bypass:** `OPTIONS` (Preflight) isteklerinde tarayıcılar custom header göndermediği için, middleware bu metodu doğrulamadan muaf tutmalıdır.

---

## 🏗️ Zorunlu Uygulatma (Enforcement)

- **Katman İzolasyonu:** MongoDB Driver kullanımı sadece **Persistence** katmanında serbesttir.
- **Repository Pattern:** Veri erişimi sadece `Tenant-Enforcing` olan repository metodları üzerinden yapılır.
- **Otomasyon:** `RepositoryBase`, `TenantFilter`'ı otomatik uygular; filtreleme işlemi geliştiricinin inisiyatifine bırakılamaz.

---

## 🚨 Hata Davranışı ve Status Kodları

- **Header Eksik:** `400 Bad Request` (ProblemDetails - "Missing Tenant Configuration")
- **Format Hatalı:** `400 Bad Request` (ProblemDetails - "Invalid Tenant Identity Format")
- **Cross-Tenant Erişim:** Başka kiracıya ait ID ile işlem denemesinde `403` yerine **`404 Not Found`** dönülmelidir (Bkz: `ARCHITECTURE.md` - Security Section).

---

## 🗑️ Güvenli Silme (Soft Delete) ve İzolasyon

- **Çift Kontrol:** Bir veri silinirken (Soft Delete), filtrede hem `Id` hem de `TenantId` bulunması zorunludur.
- **Audit:** `IsDeleted = true` yapılan kayıtlar, kiracı bazlı denetim raporları dışında standart listelemelerde (FindAll) görünmemelidir.
- **Timestamp:** Silme anında `DeletedAt` alanı UTC olarak set edilmelidir.

---
Diten ERP vNext Multi-Tenancy Standard - 2024
