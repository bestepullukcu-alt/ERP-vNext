---
id: MOD-0043
name: Tenant Architecture Foundation
domain: platform-shared-services
status: done
owner: ai-orchestrator
branch: feature/pss/mod-0043-tenant-architecture-foundation
started: 2026-04-16
target: 2026-08-06
---

# MOD-0043 — Tenant Architecture Foundation

## Module Summary
Faz 1 (Sprint 1-8) tenant-aware foundation teslimidir. Kapsam: tenant context canonical contract, gateway tenant resolution chain, event envelope + outbox sözleşmesi, auth cache fail-closed davranış standardı, architecture/tenancy gate hazırlığı.

## Ownership and Boundaries
- SoR:
  - Tenant context standardı (`X-Tenant-Id`, `tenant_id`, propagation, scope)
  - Tenant resolution priority (JWT > Header > Subdomain)
  - Event envelope ve outbox contract
  - Authorization cache contract (versioned key, invalidation)
- In-scope:
  - `Diten.AuthService`, `Diten.Platform`, `Diten.MdmService`, `Diten.ApiGateway` için foundation sözleşmeleri
  - Faz kapıları için audit ve release checklist izleme
- Out-of-scope:
  - Workflow/Document/Notification servislerinin tam ürünleşmesi
  - Repo fiziksel yeniden organizasyonu (`src/...`)
  - Domain feature geliştirme (procurement/inventory functional scope)

## Repo Scope
- `execution/domains/platform-shared-services/module-packs/MOD-0043-tenant-architecture-foundation.md`
- `docs/audits/pss-mod-0043-tenant-architecture-foundation-audit.md`
- `gateway/Diten.ApiGateway/**`
- `services/Diten.AuthService/src/**`
- `services/Diten.Platform/src/**`
- `services/Diten.MdmService/src/**`

## Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- Domain dışı servisler (ESBP iç kodları)

## Dependencies
- MOD-0018-rbac-abac-authorization
- MOD-0021-audit-trail-service
- MOD-0032-api-gateway
- MOD-0035-event-bus-message-queue

## Runtime Constraints
- Tenant taşıyıcıları zorunlu: HTTP `X-Tenant-Id`, JWT `tenant_id`, event envelope `tenant_id`
- JWT authoritative; header/subdomain conflict security log ile izlenir
- Domain default fail-closed auth cache politikası
- Cross-DB direct access yasak

## Acceptance Criteria
- [x] Gateway tenant resolution chain: JWT > Header > Subdomain uygulanmış ve conflict log üretiyor.
- [x] Auth/Platform/MDM tenant middleware canonical contract ile hizalı.
- [x] Event envelope + outbox skeleton tipi kod tabanında ortak sözleşme olarak mevcut.
- [x] Auth cache contract (key/version/ttl/invalidation event modeli) kodlanmış.
- [x] Foundation build: Auth, Platform, MDM, Gateway ve Audit projeleri Debug derleniyor.
- [x] Faz 1 audit raporu `docs/audits/pss-mod-0043-tenant-architecture-foundation-audit.md` altında güncel.

## Test Expectations
- Tenant mismatch log ve resolution precedence doğrulama
- Header eksik tenant endpoint -> 400
- Public endpoint header olmadan erişim (login/health) doğrulama
- Event envelope validation unit test (Faz 1 iskelet)
- Outbox idempotency/resilience test backlog'a bağlı takip

## Implementation Notes
- Faz 1 uygulaması incremental ilerler; sprint bazlı kapılar audit raporunda tutulur.
- Ocelot rota içerik değişiklikleri yalnız integration scope içinde yapılır.
- Service domain kodları taşınmadan sözleşme katmanı eklenir.

## Follow-up Items
- `Diten.AuditService` bağımsız servis iskeleti (Sprint 5 genişletmesi)
- ArchUnit/Analyzer tabanlı cross-db ve manual tenant filter enforce
- Tenancy + architecture CI build-break kapısı
