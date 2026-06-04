---
id: MOD-0009-FU02
name: Tenant Manager (Backend-Only)
domain: platform-shared-services
status: in-progress
owner: ai-orchestrator
branch: feature/pss/mod-0044-tenant-manager
golden_reference: none
form_field_count: 0
started: 2026-04-17
target: 2026-08-15
---

# MOD-0009-FU02 — Tenant Manager (Backend-Only)

> **Canonicalization (DCP-002):** Canonical ID is now **MOD-0009-FU02**, a child/FU of **MOD-0009 Tenant / Environment Management** (Blueprint canonical). Prior repo ID **MOD-0044** is a deprecated alias retained for traceability; repo MOD-0044 had drifted onto a Blueprint ID reserved for "Backup & Restore". Body text below predates canonicalization and may reference MOD-0044; scope and meaning are unchanged. Ref: `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`.

## Module Summary
Tenant Manager modulu, Platform tenant registry akisini backend katmaninda fazli MVP olarak genisletir. Referans ekran akisi: Registry -> Create -> Provisioning/Success -> Detail Tabs. Bu modulde UI gelistirme kapsam disidir; frontend add-module akisi ayrica acilacaktir.

## Locked Decisions
- Kapsam: Fazli MVP
- Delete politikasi: Hard delete yok (MVP'de suspend/deactivate)
- UI: Out-of-scope (bu modul backend + API contract seviyesindedir)

## Ownership and Boundaries
- SoR:
  - Tenant Registry API contract (list/filter/paging/sort, stats, detail)
  - Create + provisioning state baslatma
  - Lifecycle operations (suspend/reactivate)
  - Detail tab backend contracts (modules/users/settings)
- In-scope:
  - `services/Diten.Platform/**`
  - `execution/domains/platform-shared-services/module-packs/MOD-0009-FU02-tenant-manager.md`
- Out-of-scope:
  - Frontend Razor/JS ekran implementasyonu (ayri add-module)
  - Hard delete endpoint
  - `gateway/Diten.ApiGateway/**` (mevcut route varsayimi ile)

## Repo Scope
- `execution/domains/platform-shared-services/module-packs/MOD-0009-FU02-tenant-manager.md`
- `services/Diten.Platform/src/**`
- `services/Diten.Platform/tests/**` (eklenirse)

## Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `gateway/Diten.ApiGateway/**/ocelot.json`
- Domain disi servisler (`services/Diten.MdmService/**`, `services/Diten.EnterpriseStrategyService/**`)

## Runtime Constraints
- MongoDB single DB, multi-tenant policy korunur.
- JWT + RBAC policy (`PlatformActor`) zorunlu.
- Soft delete policy korunur, hard delete endpoint eklenmez.
- API contract envelope hizasi: `Response<T>`.

## Public Interface (MVP)
- `GET /api/admin/tenants` (paged/filter/sort + list item DTO)
- `GET /api/admin/tenants/stats`
- `POST /api/admin/tenants` (minimal create contract)
- `GET /api/admin/tenants/{id}` (detail overview DTO)
- `POST /api/admin/tenants/{id}/suspend`
- `POST /api/admin/tenants/{id}/reactivate`
- `GET /api/admin/tenants/{id}/modules`
- `GET /api/admin/tenants/{id}/users/summary`
- `GET /api/admin/tenants/{id}/settings`
- `PUT /api/admin/tenants/{id}/settings`

## Acceptance Criteria
- [ ] Tenant list endpoint filter/query sozlesmesi (`search,status,region,page,pageSize,sort`) ile calisiyor.
- [ ] List DTO kolonlari: `code,name/displayName,domain,region,environment,status,provisioningStatus,createdAt,updatedAt,createdBy`.
- [ ] Create minimal zorunlu alanla tenant olusturuyor; code sistem uretimli.
- [ ] Create sonrasi provisioning durumu + activity timeline kaydi olusuyor.
- [ ] Detail endpoint overview + provisioning adimlari + recent activity donuyor.
- [ ] Suspend/Reactivate lifecycle guardlari gecersiz gecisleri engelliyor.
- [ ] Modules/Users/Settings tab contract endpointleri readonly/update (settings) seviyesinde hazir.
- [ ] Mongo index/unique kurallari (`code`, `domain`, status sorgu performansi) uygulanmis.

## Test Expectations
- Unit: create validation, uniqueness, default derivation, lifecycle transition guard.
- Integration: policy 403, list filter/paging, create->detail provisioning/activity, lifecycle audit.
- Contract: DTO shape stabilitesi ve response envelope dogrulamasi.

## Notes
- Gateway varsayimi: `/api/admin/tenants` ve `/api/admin/tenants/{everything}` route'lari mevcut.
- Frontend bu contract'i tuketecek sekilde ayri add-module ile ele alinacaktir.
