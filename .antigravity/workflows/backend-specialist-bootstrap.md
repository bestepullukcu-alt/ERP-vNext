---
description: "WORKFLOW-003 — Diten ERP vNext .NET 8 Mikroservis Bootstrap ve Mimari Kurulum Akışı"
---

# Workflow: Backend Servis Bootstrap

Bu akış, yeni bir mikroservisin (Api, Application, Domain, Persistence, Infrastructure) sıfırdan ve standartlara %100 uyumlu şekilde ayağa kaldırılmasını sağlar.

## 🏗️ 1. Mimari Katmanlar (Folder Structure)

Her servis aşağıdaki 5 katmanlı yapıyla kurulur:

- **<Service>.Api:** Host, Middleware (TenantResolution, GlobalException), Controllers.
- **<Service>.Application:** CQRS (Commands, Queries, Handlers), Validators, DTOs, Mapping.
- **<Service>.Domain:** Entities (ITenantDocument), IRepositories, Domain Exceptions.
- **<Service>.Persistence:** MongoDbContext, Repository Impl (Tenant Enforced), Indexing.
- **<Service>.Infrastructure:** External Services (Mail, Auth Client, etc.).

---

## 🛡️ 2. Kesin Gereksinimler (Mühürlü)

1. **Tenant İzolatörü:** `X-Tenant-Id` (GUID) header'ı zorunludur. `TenantResolutionMiddleware` bu header'ı okur ve `Scoped` olan `ITenantContext` nesnesini doldurur.
2. **Sessiz Tenant Yönetimi:** `TenantId` alanı Request DTO/Body içinde **ASLA** yer almaz. Bu bilgi `Persistence` katmanındaki `RepositoryBase` tarafından yazma anında otomatik set edilir, okuma anında otomatik filtrelenir.
3. **CQRS & Klasör Yapısı (WORKFLOW-001):** Handler sınıfları `Handlers/CommandHandlers` ve `Handlers/QueryHandlers` klasörlerinde toplanır.
4. **JWT & Güvenlik:** Tüm servisler `JwtBearer` ile donatılır