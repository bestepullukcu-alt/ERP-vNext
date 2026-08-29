---
id: MOD-0149-PREREQ
name: Diten.CrmService Runtime Scaffold (MOD-0149 Prerequisite)
domain: commercial-suite
service: Diten.CrmService
shell: none
golden_reference: none
entity_base: n/a
status: ready-for-dev
owner: module-pack-author
branch: feature/crm/crm-service-scaffold
started: 2026-07-14
target: 2026-08-22
runtime_code_allowed: true
runtime_code_scope: scaffold-only (§3/§4 — 5-layer skeleton; NO Account/CRM business logic)
approved_by: user (approval gate 2026-07-14)
prerequisite_of: MOD-0149
---

# MOD-0149-PREREQ — Diten.CrmService Runtime Scaffold

> **DRAFT prerequisite / infrastructure pack.** MOD-0149 implementation'ının runtime ön koşuludur — bir iş modülü değildir.
> **`id: MOD-0149-PREREQ` non-executable, non-Blueprint bir prerequisite identity'sidir** (MOD-0149'a bağlı; yeni Blueprint
> MOD id **icat edilmedi**). EA tercih ederse bu, bir **Delivery Capability Pack (DCP-NNN)** olarak da çerçevelenebilir —
> PPM service scaffold'ının DCP-003 ile gate'lenmesiyle aynı desen. Bu pack **kod/scaffold üretmez**; scaffold, status
> `approved`/`ready-for-dev` + açık kullanıcı onayı sonrası ayrı bir implementation task'ında yapılır.

## 1. Module Summary

MOD-0149 Customer 360 / Account Hierarchy (ve sonraki CRM core modülleri) için hedef runtime servisi `Diten.CrmService`
henüz yok (`services/Diten.CrmService/**` mevcut değil). Bu pack, **yalnız boş/çalışabilir servis iskeletini** (5-katman
+ CQRS + envelope + tenant/Mongo/logging convention + DI + Swagger + health) planlar; **hiçbir Account/CRM iş mantığı
içermez**. Repo pattern'i (AGENTS.md §2; DCP-003 PPM örneği) yeni production service scaffold'ının **açık kullanıcı onayı
ve approved/ready-for-dev pack** olmadan açılmamasını gerektirir — bu pack o kapıyı sağlar.

## 2. Service Boundary Decision

- **`Diten.CrmService` = Commercial Suite CRM core başlangıç runtime boundary'sidir.** CRM core modülleri — **MOD-0149,
  MOD-0150, MOD-0151, MOD-0152, MOD-0153, MOD-0154, MOD-0155** — bu servis altında başlar.
- **CPQ / O2C / Service / Business Development adjacent modüller (MOD-0156…0163, 0168…0172, 0282…0284): EA-TBD** — aynı
  serviste mi ayrı serviste (örn. `Diten.CommercialService`) mi olacağı ileride EA kararıdır; bu scaffold onları kapsamaz.
- **MOD-0149 etkisi:** MOD-0149 implementation `Diten.CrmService` açılmadan **başlamamalıdır**; frontmatter `service:
  Diten.CrmService` bu boundary ile tutarlıdır.

## 3. Scaffold Scope (yalnız iskelet — iş mantığı YOK)

| Layer/Area | Included? | Notes |
|---|---|---|
| `Diten.CrmService.Api` | ✅ | Program.cs, DI, Swagger, `CustomBaseController` taban, health endpoint (repo standardı) |
| `Diten.CrmService.Application` | ✅ | MediatR + 4 pipeline behavior (Validation/Logging/Exception/Performance); `Response<T>` envelope; **feature YOK** |
| `Diten.CrmService.Domain` | ✅ | `EntityBase` taban erişimi (Building.Blocks); **entity YOK** |
| `Diten.CrmService.Persistence` | ✅ | Mongo context/registration; TenantId filter altyapısı; **collection/entity YOK** |
| `Diten.CrmService.Infrastructure` | ✅ | Tenant context accessor, correlation-id/logging, config binding |
| Tenant context / TenantId enforcement | ✅ | Server-side resolve convention; cross-tenant 404 altyapısı |
| Soft delete convention | ✅ | `IsDeleted`/`DeletedAt` taban (entity gelince kullanılacak) |
| DI registration / appsettings / Program.cs | ✅ | MdmService/HcmService pattern'i birebir |
| Swagger / health endpoint | ✅ | Boot doğrulaması için |
| Local dev port | ✅ | **Öneri: 5061** (§5) |
| Test project (`Diten.CrmService.*.Tests`) | ✅ | boots/DI/health/tenant-guard/build smoke; **account business test YOK** |
| Frontend account pages | ❌ | MOD-0149 implementation task'ı |
| Gateway `/api/crm/accounts*` route | ❌ | MOD-0149 + integration-agent |
| Gateway service registration / downstream base | ⚠️ | Gerekliyse **integration-agent scope** olarak ayrı; bu pack ocelot.json yazmaz |

## 4. Out-of-Scope

| Item | Reason | Future owner/task |
|---|---|---|
| Account entity / CRUD / hierarchy | İş mantığı | MOD-0149 implementation |
| AccountCode generator | İş mantığı | MOD-0149 implementation |
| AccountExternalReference | İş mantığı | MOD-0149 implementation |
| MOD-0048 validator binding | İş mantığı | MOD-0149 implementation |
| `crm.account.*` permission seed | RBAC seed | MOD-0149 implementation |
| Account UI / Accounts menu item | Frontend | MOD-0149 implementation (+ Adım 9) |
| Gateway `/api/crm/accounts*` | Routing | MOD-0149 + integration-agent |
| Import/export | İş mantığı | MOD-0149 implementation |
| Visit/Territory/Contact/Lead/Opportunity/Campaign | Diğer modüller | MOD-0150…0155, 0164…0167 |

## 5. Repository / Solution Integration

- **Kopyalanacak pattern:** en yeni 5-katmanlı tenant servisleri **`Diten.MdmService`** / **`Diten.HcmService`**
  (`.Api/.Application/.Domain/.Persistence/.Infrastructure`). CRM aynı yapıyı birebir izler.
- **csproj naming:** `Diten.CrmService.Api`, `Diten.CrmService.Application`, `Diten.CrmService.Domain`,
  `Diten.CrmService.Persistence`, `Diten.CrmService.Infrastructure`.
- **Test project naming:** repo standardına göre `Diten.CrmService.Application.Tests` (+ gerekiyorsa `.Api.Tests`).
- **Solution:** yeni projeler mevcut `.sln`'e eklenir (MdmService/HcmService referans).
- **launchSettings / appsettings / DI:** MdmService pattern'i; `Diten.Building.Blocks` + `Diten.Platform.Contracts` referansları.
- **Local dev port — ÖNERİ: `5061`.** Mevcut: Gateway 5000, Web 5001, Auth 5056, Platform 5057, DevEnablement 5058,
  **Mdm 5059, Hcm 5060** (launchSettings + ocelot downstream doğrulandı). **5061 boş** ve çakışmasız.
- **Gateway registration:** downstream base (`localhost:5061`) gerekiyorsa **integration-agent** ekler; `/api/crm/accounts*`
  route'ları MOD-0149 implementation'a bırakılır. Bu pack `ocelot.json` **yazmaz**.
- **Docker/compose:** varsa fleet script'ine (`watch-diten-bg.ps1`) 5061 eklenmesi implementation/integration task notu.

## 6. Future Implementation Golden Flow

**Golden flow:**
1. Geliştirici `Diten.CrmService`'i lokalde başlatır (port 5061).
2. Servis **DI/config hatası olmadan boot eder**.
3. Health/basic endpoint beklenen host üzerinden **success** döner.
4. `dotnet build` (yeni servis + solution) **PASS**.
5. Scaffold smoke testleri **PASS** (boots/DI resolves/health/tenant-context guard).
6. **Henüz hiçbir Account/CRM business endpoint yoktur** (yalnız iskelet).

**Failure path:**
- Missing tenant context, missing configuration, DI failure, Mongo configuration failure veya port conflict → **controlled
  startup/config error** ile fail edilmeli.
- **Partial/fake Account API bulunmamalı** — iskelet, iş mantığı olmadan çalışır; yarı-implemented endpoint yasak.

## 7. Acceptance Criteria (scaffold implementation için, ileride)

- [ ] `services/Diten.CrmService/src/` altında 5 katman MdmService/HcmService ile birebir folder/naming.
- [ ] `dotnet build` yeni servis + solution PASS.
- [ ] Servis port **5061**'de boot eder; health/basic endpoint success.
- [ ] MediatR + 4 pipeline behavior + `Response<T>` + `CustomBaseController` kayıtlı.
- [ ] Tenant context accessor + Mongo registration + soft-delete taban mevcut; **entity/collection YOK**.
- [ ] Scaffold smoke testleri (boots/DI/health/tenant-guard) PASS; **account business test YOK**.
- [ ] Hiçbir `crm.account.*` endpoint / entity / seed / route yok.
- [ ] `ocelot.json` bu pack'te değişmedi (gerekliyse integration-agent ayrı task).

## 8. Protected Paths

- `.antigravity/**`, `AGENTS.md`
- `frontend/**` (bu scaffold frontend üretmez)
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration-agent)
- Diğer domain servisleri: `Diten.AuthService`, `Diten.Platform`, `Diten.MdmService`, `Diten.HcmService`, `Diten.EnterpriseStrategyService`, `Diten.DevEnablementService`
- `execution/registries/**`, `execution/portfolio/**` (bu pack değiştirmez)
- MOD-0149 pack (`MOD-0149-customer-360-account-hierarchy.md`) — bu pack onu değiştirmez

## 9. Status / Governance

- **status: ready-for-dev** (approval gate 2026-07-14, kullanıcı açık onayı) · **runtime_code_allowed: true**, ancak
  `runtime_code_scope: scaffold-only` — yazılabilecek runtime kod **yalnız §3/§4 kapsamındaki 5-katman iskelettir**;
  hiçbir Account/CRM iş mantığı, entity, endpoint, seed veya gateway route bu pack ile yetkilendirilmez.
- Lifecycle: `draft → **ready-for-dev**` (module-pack-standard §16; `approved`/`ready-for-dev` doğrudan geçiş serbest,
  "approved-first" zorunluluğu yok). Scaffold, ayrı bir implementation task'ında (`@orchestrator` + bu pack) üretilir.
- **DCP alternatifi:** EA, service scaffold'ı DCP-003 deseniyle bir `DCP-NNN` Delivery Capability Pack olarak yürütmeyi
  tercih ederse, bu pack o DCP'nin girdi/scope tanımı olur; MOD-0149-PREREQ identity'si repo-local prerequisite olarak kalır.

## 10. Blockers / Follow-up

- **Açık kullanıcı/EA onayı** — yeni production service scaffold açmak için zorunlu (AGENTS.md §2 / DCP-003 deseni).
- **Adjacent service kararı (EA-TBD)** — CPQ/O2C/Service/BizDev aynı serviste mi ayrı `Diten.CommercialService` mı; MOD-0149'u bloklamaz.
- **integration-agent** — 5061 downstream gateway registration (route'lar MOD-0149'a ait).
- **Fleet script** — `watch-diten-bg.ps1`'e 5061 eklenmesi (implementation/integration notu).
