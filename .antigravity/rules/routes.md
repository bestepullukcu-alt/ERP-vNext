# Route Naming Standard

## Amaç
Tüm servisler için tek tip gateway route standardı.
Case farklarından ve “Mdm/MDM/mdm” karmaşasından kurtulmak.

## Upstream (Gateway) Standard
- Tüm upstream path’ler **lowercase** olmalıdır:
  - `/services/<module>/{everything}`
- `<module>`: servis adı (lowercase), ör: `mdm`, `finance`, `crm`

### Örnek (MDM)
- Upstream:
  - `http://localhost:5001/services/mdm/{everything}`
- Downstream:
  - `http://localhost:5050/{everything}`

## Downstream API Standard
- Servis içi API prefix:
  - `/api/...`
- Health:
  - `/health` (public, tenant header gerektirmez)

## Header Standard
- Multi-tenant header:
  - `X-Tenant-Id: <GUID>`
- Auth:
  - `Authorization: Bearer <token>` (şimdilik dev’de opsiyonel)

## Location Header Standard (Gateway Arkasında)
- Servis 201 Created dönerken Location **gateway üzerinden** görünmelidir.
- Bunun için servis config:
  - `PublicBaseUrl = http://localhost:<gatewayPort>/services/<module>`
- Örn MDM:
  - `PublicBaseUrl = http://localhost:5001/services/mdm`