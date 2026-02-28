# Workflow: Backend Servis Bootstrap (.NET 8 + CQRS + Mongo + MultiTenant + JWT)

## Amaç
Aşağıdaki projelerle .NET 8 servis iskeleti kur:
- <Service>.Api (veya <Service> Web host)
- <Service>.Application
- <Service>.Domain
- <Service>.Persistence
- <Service>.Infrastructure

## Kesin Gereksinimler
- Tenant header: X-Tenant-Id (GUID)
- TenantContext (scoped) + TenantResolutionMiddleware
- Her Mongo dokümanında Guid TenantId zorunlu
- RepositoryBase her sorguda tenant filtresi uygular ve yazmalarda TenantId set eder
- TenantId request DTO/body içinde ASLA olmayacak
- MongoDB.Driver sadece Persistence’te
- CQRS: MediatR
- JWT scaffolding: JwtBearer (config placeholders)
- Controller: iş kuralı yok, sadece MediatR çağrısı
- Önce plan (dosya dosya), sonra implement

## Girdiler (eksikse sor)
- Servis adı (default: Diten.MdmService)
- Mongo connection string (default: mongodb://localhost:27017)
- Mongo database name (default: diten_mdm)

## Çıktı
- GET /health (public) -> { status: "ok" }
- POST /sample (authorize) -> SampleEntity oluşturur, TenantId otomatik set edilir
- X-Tenant-Id ve Authorization içeren örnek curl komutları
