# Workflow: Endpoint Ekle (CQRS)

## Gerekli input
- HTTP method + route
- Request/response DTO şeması
- Auth gereksinimi (public/authorized/policy)
- Validation kuralları
- Mongo entity/collection

## Kurallar
- Controller sadece MediatR çağırır
- Command veya Query + Handler oluştur
  - **ÖNEMLİ CQRS KLASÖR YAPISI:** 
    - Handler sınıfları `Commands` veya `Queries` klasörlerinin içinde **OLMAYACAKTIR**.
    - Bunun yerine ilgili feature altında ayrı bir `Handlers` klasörü olacak.
    - Bu klasörün altında `CommandHandlers` ve `QueryHandlers` bulunacak.
    - İşleyen sınıflar (Handlers) bu yeni klasörlere; veriyi taşıyan modeller (Command/Query) ise eski yerlerine (`Commands` / `Queries`) konulacaktır.
- DTO’lar TenantId içermez
- Validation ekle
- Repository method kullan (tenant enforced)
- Gerekirse index ekle
- Önce plan, sonra implement
