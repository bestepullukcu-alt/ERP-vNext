# Multi-Tenant (Single DB) — KESİN KURALLAR

## Standart
- Tenant header: `X-Tenant-Id`
- Format: GUID string
- Her Mongo dokümanında ZORUNLU alan: `Guid TenantId`

## Pazarlık yok (hard rules)
1) TenantId asla request body / DTO / query param üzerinden kabul edilmez.
2) TenantId sadece `X-Tenant-Id` header’dan, middleware ile çözülür.
3) Her okuma/sorgu TenantId ile filtrelenmek ZORUNDADIR.
4) Her yazma (insert/update) TenantId’yi TenantContext’ten (server-side) set etmek ZORUNDADIR.
5) Tenant filtresi olmadan Mongo sorgusu yapmak BUG’dır.
6) `Diten.Web` projesinde `HttpClient` ile dış servislere (Gateway/Backend) giden tüm isteklerde `X-Tenant-Id` header bilgisi zorunludur. Geliştirme aşamasında bu değer varsayılan olarak `1` atanmalıdır. Gelecekte üretilecek tüm `Controller` ve `Service` sınıfları bu header'ı içerecek şekilde kodlanmalıdır.

## Zorunlu uygulatma (enforcement)
- MongoDB driver kullanımı sadece Persistence katmanında serbesttir.
- Data access sadece tenant-enforcing repository üzerinden yapılır.
- RepositoryBase tenant filtresini otomatik uygular (insana bırakılmaz).

## Hata davranışı
- `X-Tenant-Id` yok -> 400 Bad Request (ProblemDetails)
- GUID geçersiz -> 400 Bad Request (ProblemDetails)
