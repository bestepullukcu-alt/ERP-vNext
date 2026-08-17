---
id: MOD-0018-FU12
name: Tenant Authorization Context Foundation
domain: platform-shared-services
service: Diten.Platform.Common
shell: none
golden_reference: none
entity_base: GlobalEntity
status: done
owner: platform-team
branch: feature/pss/mod-0018-fu12-tenant-authz-context
started: 2026-05-21
target: 2026-06-12
form_field_count: 0
---

# MOD-0018-FU12 — Tenant Authorization Context Foundation

> **Historical draft note — superseded by §19 reconciliation entry:** Bu pack, FU10a/FU10b uzerine kurulan **runtime context consolidation foundation** isidir. Kod yazimi pack `approved` veya `ready-for-dev` yapilmadan baslamaz. Mevcut authorization karari (`allow/deny`) degismez; handler'larin claim parse ettigi noktalar `ITenantAuthorizationContext` arkasina toplanir.

> **Golden Reference karari:** Bu is UI/DataTable modulu degildir. `shell: none`, `golden_reference: none`, `form_field_count: 0`. Razor layout, DataTable verifier, RESX ve frontend dosya seti bu pack icin N/A'dir.

> **entity_base alani:** Frontmatter zorunlulugu nedeniyle `GlobalEntity` yazildi; FU12 yeni entity eklemez. MOD-0018 ve FU10 ile ayni konvansiyon.

> **Two-Step Implementation (orchestrator review 2026-05-21):**
> - **FU12a — Context Contract + Default Implementation** (`feature/pss/mod-0018-fu12a-tenant-authz-context-foundation`): `ITenantAuthorizationContext` contract'i Common'da; `JwtTenantAuthorizationContext` Infrastructure'da; DI registration; testler. **Handler'lar acilmaz, davranis degismez.** **MVF eshigini bu adim karsilar.**
> - **FU12b — Handler Refactor + Bypass Metadata** (`feature/pss/mod-0018-fu12b-handler-context-refactor`): `TenantModuleAuthorizationHandler` ve `TenantFeatureAuthorizationHandler` claim parse boilerplate'ini `ITenantAuthorizationContext` arkasina toplar. `platform_admin` bypass kolu icin opsiyonel `EntitlementCheckResult.Allowed(..., resolvedFrom: PlatformAdminBypass)` audit-friendly emit noktasi eklenir.
>
> Iki PR bagimsiz review edilir. FU12a merge olmadan FU12b baslamaz. **MVF eshigi yalniz FU12a ile saglanir**; FU12b kalite temizlemesi olarak ayri pencere.

> **Step 0 (prerequisite):** Bu pack `ready-for-dev` yapilmadan **once** FU10a + FU10b merge olmus olmalidir. `EntitlementResolutionSource.PlatformAdminBypass` enum degeri ve `IDataScopeResolver` contract'i FU10a tarafindan saglanmis durumda; FU12 bu sembollere bagimli.

## 1. Module Summary

MOD-0018-FU12, mevcut authorization handler'larinin (`TenantModuleAuthorizationHandler` ve `TenantFeatureAuthorizationHandler`) dogrudan `HttpContext.User.FindFirst("tenant_id")` / `actor_type` / `permission` claim parsing yapmasini engelleyen **scoped runtime context** foundation'ini ekler. Amac;
- Handler'lar JWT semantigine sertce bagli kalmaktan kurtulur (gelecek service-to-service actor, signed claim envelope veya per-request resolved context senaryolarinda refactor gerektirmez).
- `platform_admin` bypass karari tek noktada (`IsPlatformAdmin`) cozulur; future-proof `ResolvedFrom = PlatformAdminBypass` metadata emit noktasi acilir.
- `IDataScopeResolver` (FU10a contract'i) ileri pack'lerde `OrgUnitIds`/`PositionIds`/`ManagerChain` doldurmak icin context icine bagli kalir; FU12'de NoOp resolver empty doner.
- HR/CRM/BPM/OrgHierarchy modullerinden `ITenantAuthorizationContext` tek SSOT olarak okunur — JWT'de olmayan org alanlari tek bir yerde server-side resolve edilir.

Pack su 4 amaca odaklanir:
1. `ITenantAuthorizationContext` contract'ini `Diten.Platform.Common.Authorization` altinda tanimlamak.
2. `JwtTenantAuthorizationContext` default impl'ini `Diten.Platform.Infrastructure/Authorization/` altinda kurmak (mevcut `CurrentUserContext.cs` pattern'i ile birebir uyumlu).
3. DI seam'ini Scoped lifetime ile acmak; mevcut handler'lara dokunmadan paralel calismasini saglamak.
4. (FU12b — opsiyonel kalite kapsami) Handler'larin claim parse boilerplate'ini bu context arkasina toplamak ve `platform_admin` bypass kolunda emisyon noktasi acmak.

## 2. Ownership and Boundaries

**In scope (FU12a):**
- `ITenantAuthorizationContext` interface (yeni).
- `JwtTenantAuthorizationContext` class (yeni; HttpContext-based default impl).
- `AnonymousTenantAuthorizationContext` (opsiyonel; test/fallback icin yeni — eger HttpContext yoksa boş döner).
- `ITenantAuthorizationContext` Scoped DI registration (`Diten.Platform.Infrastructure/DependencyInjection.cs`).
- JWT claim'lerinden `TenantId`, `UserId`, `ActorType`, `IsAuthenticated`, `IsPlatformAdmin`, `PermissionKeys`, `RoleIds (empty for now)` hidrasyonu.
- Org context alanlari icin **placeholder property'ler** (`OrgUnitIds`, `PositionIds`, `LegalEntityId`, `Country`, `ManagerChain`) — FU12'de `IDataScopeResolver` (NoOp) cagrilir; gerçek resolution MOD-0040/MOD-0018-FU15'e birakilir.
- Yeni unit testler: context hidrasyonu, NoOp resolver entegrasyonu, anonymous fallback, DI smoke.

**In scope (FU12b — opsiyonel, MVF disi):**
- `TenantModuleAuthorizationHandler` ve `TenantFeatureAuthorizationHandler` `_context` consume eder; `context.User.FindFirst("...")` cagrilari kaldirilir.
- `platform_admin` bypass kolu `EntitlementCheckResult.Allowed(..., resolvedFrom: PlatformAdminBypass)` emit eder (audit/explain icin); `context.Succeed(requirement)` ayni anda.
- Handler unit testleri `ITenantAuthorizationContext` mock'lanmis olarak yeniden yazilir (mevcut davranis testleri korunur).

**Out of scope:**
- Gercek org hierarchy data (OrgUnit/Position/UserOrgAssignment) — MOD-0040.
- Gercek `IDataScopeResolver` impl — MOD-0018-FU15.
- `UserOrgAssignment` persistence — MOD-0040.
- BPM runtime / `ITemporaryAccessProvider` pipeline binding — FU11.
- CRM/HR query filtering / repository tarafi.
- Tenant Users / Roles / Permissions CRUD — MOD-AUTH-001.
- AuthService rewrite veya yeni claim turetimi (`role_id` claim eklenmesi FU13'e kalir).
- UI / RBAC Admin UI.
- Full ABAC engine / OPA / Cedar / policy DSL.
- Runtime SQL/MongoDB row-level filtering.
- Distributed cache user-keyed eviction — FU13.

## 3. Owned Objects

**Genisletilecek mevcut tipler:**
- (Sadece FU12b) `TenantModuleAuthorizationHandler` ([services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/TenantModuleAuthorizationHandler.cs](../../../../services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/TenantModuleAuthorizationHandler.cs)) — `ITenantAuthorizationContext` injection + claim parse boilerplate kaldirilir.
- (Sadece FU12b) `TenantFeatureAuthorizationHandler` ([services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/TenantFeatureAuthorizationHandler.cs](../../../../services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/TenantFeatureAuthorizationHandler.cs)) — ayni refactor.

**Yeni contract / impl:**
- `ITenantAuthorizationContext` interface — `Diten.Platform.Common.Authorization` namespace (handler'lar buradan tuketebilsin).
- `JwtTenantAuthorizationContext` class — `Diten.Platform.Infrastructure/Authorization/` (depends on `IHttpContextAccessor` + `IDataScopeResolver`).
- `AnonymousTenantAuthorizationContext` sealed class — `Diten.Platform.Common/Authorization/` (HttpContext olmayan senaryolar icin null-object; testler ve background jobs icin).

**DI:**
- [services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs](../../../../services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs) — `ICurrentUserContext` satirinin (line 128) hemen altina `ITenantAuthorizationContext` Scoped registration.

**Yeni test dosyalari (FU12a):**
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TenantAuthorizationContextHydrationTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/AnonymousTenantAuthorizationContextTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TenantAuthorizationContextRegistrationTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TenantAuthorizationContextDataScopeIntegrationTests.cs`

**Yeni test dosyalari (FU12b, MVF disi):**
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/AuthorizationHandlerContextConsumptionTests.cs`
- Mevcut `TenantModuleAuthorizationHandlerTests` ve `TenantFeatureAuthorizationHandlerTests` claim-mock'tan context-mock'a refactor edilir (davranis testleri 1:1 korunur).

## 4. Entity Fields

Bu pack persisted entity eklemez. Asagidaki tablo **contract sema**'sini belgeler.

| Object | Field | Type | Required | Rule |
|---|---|---|---|---|
| `ITenantAuthorizationContext` | `TenantId` | `Guid` | yes | JWT `tenant_id` claim parse; yoksa `Guid.Empty` |
| `ITenantAuthorizationContext` | `UserId` | `Guid` | yes | JWT `sub` claim parse; yoksa `Guid.Empty` |
| `ITenantAuthorizationContext` | `ActorType` | `string?` | no | JWT `actor_type` claim degeri (`platform_admin`, `partner_admin`, `tenant_user`, `service`); yoksa null |
| `ITenantAuthorizationContext` | `IsAuthenticated` | `bool` | yes | `HttpContext.User.Identity?.IsAuthenticated == true` |
| `ITenantAuthorizationContext` | `IsPlatformAdmin` | `bool` | yes | Computed: `string.Equals(ActorType, "platform_admin", OrdinalIgnoreCase)` |
| `ITenantAuthorizationContext` | `PermissionKeys` | `IReadOnlyList<string>` | yes | JWT `permission` claim'lerin toplami; default empty array; null asla |
| `ITenantAuthorizationContext` | `RoleIds` | `IReadOnlyList<Guid>` | yes | FU12'de empty (JWT `ClaimTypes.Role` claim'i isim tasiyor, ID tasimiyor); FU13 ile birlikte gelir |
| `ITenantAuthorizationContext` | `RoleNames` | `IReadOnlyList<string>` | yes | JWT `ClaimTypes.Role` claim degerleri; default empty |
| `ITenantAuthorizationContext` | `OrgUnitIds` | `IReadOnlyList<Guid>` | yes | `IDataScopeResolver.ResolveAsync` cagrilir; NoOp empty doner; MOD-0018-FU15 ile dolar |
| `ITenantAuthorizationContext` | `PositionIds` | `IReadOnlyList<Guid>` | yes | Ayni; NoOp empty |
| `ITenantAuthorizationContext` | `LegalEntityId` | `Guid?` | no | `IDataScopeResolver`'dan; NoOp null |
| `ITenantAuthorizationContext` | `Country` | `string?` | no | `IDataScopeResolver`'dan; NoOp null |
| `ITenantAuthorizationContext` | `ManagerChain` | `IReadOnlyList<Guid>` | yes | `IDataScopeResolver`'dan; NoOp empty |
| `JwtTenantAuthorizationContext` | dependencies | (IHttpContextAccessor, IDataScopeResolver) | yes | DI scoped |
| `AnonymousTenantAuthorizationContext` | (no deps) | — | yes | Tum alanlar empty/null/false; testler ve background job senaryolari icin |

**Hidrasyon stratejisi:**
- JWT alanlari (`TenantId`, `UserId`, `ActorType`, `IsAuthenticated`, `PermissionKeys`, `RoleNames`) — `HttpContext.User` claim'lerinden **synchronous** okunur; performans icin per-request memoization gerekirse iniat ihtimal.
- Org alanlari (`OrgUnitIds`, `PositionIds`, `LegalEntityId`, `Country`, `ManagerChain`) — `IDataScopeResolver.ResolveAsync` **async** cagrilir. Bu nedenle context property'leri **`ValueTask<>` veya `Task<>` dondurmek zorunda kalmamak icin** lazy initialization pattern'i kullanilir: ilk erisimde resolver bir kere cagrilir, sonuc memoize edilir.

> **Karar (B12-1, FU12a lock):** `ITenantAuthorizationContext` property'leri **synchronous** dondurur (`IReadOnlyList<Guid>` direk). Async resolver per-request bir kere `Task.GetAwaiter().GetResult()` ile bekletilmek yerine, **`ITenantAuthorizationContext` icinde explicit `InitializeAsync()` method'u** acilir. Handler ve consumer'lar await ederek hazirlar. FU12a tests'i bunu kanitlar.

## 5. Repo Scope

**Step 0 (doc-only PR, prerequisite — yapilacak):**
- `execution/domains/platform-shared-services/module-packs/MOD-0018-FU12-tenant-authorization-context-foundation.md` (bu pack)
- `docs/platform/master-plan.md` (§12 Track G-prime altina FU12 madde guncellemesi — FU10b done, FU12a in-progress)

**FU12a — Context Contract + Default Implementation PR:**
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/ITenantAuthorizationContext.cs` (yeni)
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/AnonymousTenantAuthorizationContext.cs` (yeni)
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Authorization/JwtTenantAuthorizationContext.cs` (yeni)
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs` (Scoped registration ekleme)
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TenantAuthorizationContextHydrationTests.cs` (yeni)
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/AnonymousTenantAuthorizationContextTests.cs` (yeni)
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TenantAuthorizationContextRegistrationTests.cs` (yeni)
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TenantAuthorizationContextDataScopeIntegrationTests.cs` (yeni)

**FU12b — Handler Refactor PR (MVF disi):**
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/TenantModuleAuthorizationHandler.cs` (refactor)
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/TenantFeatureAuthorizationHandler.cs` (refactor)
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TenantModuleAuthorizationHandlerTests.cs` (claim mock → context mock refactor; davranis testleri 1:1 korunur)
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TenantFeatureAuthorizationHandlerTests.cs` (ayni)
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/AuthorizationHandlerContextConsumptionTests.cs` (yeni)

## 6. Protected Paths

- `.antigravity/**` — global engineering system.
- `services/Diten.AuthService/**` — Token uretimi ve role/permission CRUD bu pack disinda; FU13 alani.
- `frontend/Diten.Web/**` — UI yok.
- `gateway/Diten.ApiGateway/**/ocelot.json` — gateway degisikligi yok.
- `services/Diten.MdmService/**`, `services/Diten.EnterpriseStrategyService/**`, `services/Diten.DevEnablementService/**` — diger domain servisleri.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` — FROZEN.
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/TemporaryAccessGrant.cs` — FU11 alani.
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/ITemporaryAccessProvider.cs` — FU11 alani.
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/EntitlementCheckResult.cs` — **FU10a sonrasi donmustur**; FU12 imzayi degistirmez, sadece tuketir.
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/EntitlementDataScopeKind.cs` — FU10a donmustur.
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/EntitlementDataScope.cs` — FU10a donmustur.
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/EntitlementResolutionSource.cs` — FU10a; FU12 yalniz `PlatformAdminBypass` degerini FU12b'de tuketir.
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/IDataScopeResolver.cs` — FU10a; FU12 tuketir, degistirmez.
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/NoOpDataScopeResolver.cs` — FU10a; FU12 tuketir.
- `services/Diten.Platform/src/Diten.Platform.Application/Services/EntitlementChecker.cs` — FU10b sonrasi; FU12 dokunmaz.
- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/ICurrentUserContext.cs` — degismez; `ITenantAuthorizationContext` onun ust katmani olarak yasar.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/CurrentUserContext.cs` — degismez.

## 7. Dependencies

**Onceden tamamlanmis (FU12 baslamadan once merge olmali):**
- **MOD-0018-FU10a:** `EntitlementResolutionSource` enum, `IDataScopeResolver` contract, `NoOpDataScopeResolver` DI'da Scoped registered. FU12 bu sembollere bagimli.
- **MOD-0018-FU10b:** `EntitlementChecker.ResolvedFrom` mapping. FU12 dogrudan tuketmez ama Acceptance Criteria #26'da FU12b'nin tutarlilik testleri FU10b mapping davranisini varsayar.

**Eszamanli/paralel (bagimsiz):**
- **MOD-0018-FU11:** `ITemporaryAccessProvider` pipeline binding. FU12 ile bagimsiz; iki pack ayri pencerelerde ilerleyebilir. Eger FU11 once gelirse, FU12b handler refactor sirasinda temp grant evaluation call-site'i `_context.IsPlatformAdmin` kolunun ardina yerlesir.

**Bagimli olunan altyapilar:**
- JWT issuance: `Diten.AuthService.Infrastructure.Services.TokenService` ([services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/TokenService.cs](../../../../services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/TokenService.cs)) — `tenant_id`, `actor_type`, `permission`, `ClaimTypes.Role`, `JwtRegisteredClaimNames.Sub` claim'lerini uretiyor. FU12 bu claim isimlerine bagimlidir; AuthService tarafindan claim isimleri degistirilirse FU12 testleri FAIL eder (governance gate).
- `IHttpContextAccessor` — mevcut [services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/CurrentUserContext.cs](../../../../services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/CurrentUserContext.cs) pattern'i.

**Downstream (FU12 tamamlanmadan baslamamali veya kalitesi dusebilir):**
- **MOD-0018-FU13:** Permission key convention + role-permission cache invalidation event'leri. FU13'un `ITenantAuthorizationContext` user-keyed cache invalidation hook'larina ihtiyaci olabilir.
- **MOD-0018-FU14:** Effective access explain endpoint — `ITenantAuthorizationContext` user-centric explain query'sini besler.
- **MOD-0040 / MOD-0018-FU15:** Org master data + real `IDataScopeResolver` — `ITenantAuthorizationContext`'in org alanlarini gercek degerlerle doldurur.
- **MOD-AUTH-001 (Track G):** Tenant Users/Roles CRUD — `ITenantAuthorizationContext` uzerinden mevcut user context'i okur.

## 8. Runtime Constraints

- **Existing authorization behavior degismez.** FU12a tamamlandiktan sonra handler'lar hala `HttpContext.User.FindFirst("tenant_id")` ile calisir; davranisa etkisi yoktur. FU12b refactor handler'lari `ITenantAuthorizationContext`'e gecirir; **davranissal eksen testleri 1:1 korunur**.
- `ITenantAuthorizationContext` **Scoped** lifetime (`ICurrentUserContext` ile ayni pattern). Singleton **yasak** — per-request claim degerleri tasinir.
- `IsAuthenticated == false` durumunda `TenantId`, `UserId`, `Guid.Empty` doner; `ActorType` null; `PermissionKeys` empty; org alanlari empty/null. Boylece anonymous endpoint'ler null-check yapmak zorunda kalmaz.
- `IsPlatformAdmin` computed: `string.Equals(ActorType, "platform_admin", OrdinalIgnoreCase)`. Bypass karari handler'da hala lokal kalir; context sadece bilgi tasir.
- `IDataScopeResolver.ResolveAsync` **per-request bir kere** cagrilir (memoization); ayni context instance'i icinde tekrar cagrilmaz. Sonuc cache scope bitinceye kadar yasar; FU13 invalidation event'leri context'i etkilemez (yeni request yeni context).
- `JwtTenantAuthorizationContext` `IHttpContextAccessor.HttpContext is null` durumunda **anonymous semantics** doner (`AnonymousTenantAuthorizationContext` impl'inin ayni kontratiyla); ayri exception atilmaz.
- `partner_admin` ve `service` actor_type'lari icin `IsPlatformAdmin == false`; bu actor'lerin bypass'si bu pack tarafindan eklenmez (FU8 / S2S follow-up alani).
- `Country` claim degeri JWT'de yok; FU12a icin `IDataScopeResolver`'dan gelir, NoOp ile null.
- `LegalEntityId` claim degeri JWT'de yok; ayni davranis.
- `PermissionKeys` JWT'den okunur; FU13'te buyumesi ihtimal eden permission claim sayisi nedeniyle **yapisi tasinabilir** olmali (FU13'te server-side resolution'a tasinabilir).
- `JwtTenantAuthorizationContext` `IHttpContextAccessor`'a bagimli; background job veya consumer context'inde bu accessor null doner — bu durumda anonymous semantics gecerli.
- `InitializeAsync()` cagrilmadan `OrgUnitIds`/`PositionIds`/`LegalEntityId`/`Country`/`ManagerChain` erisilirse **empty/null** doner (lazy default). Karar §4 B12-1.

## 9. Layout & Shell Contract

`shell: none`. Bu pack backend/shared-contract isidir.

- Razor view yok.
- `Layout = "_LayoutPlatformAdmin"` veya `_LayoutTenantShell` kullanilmaz.
- Frontend route, DataTable, RESX ve Ctrl+K search registry N/A.
- `golden_reference: none` bu nedenle dogrudur.

## 10. Backend File Convention

Bu pack CRUD/DataTable modulu olmadigi icin Golden Reference CQRS klasor seti birebir uygulanmaz. Mevcut authorization/infrastructure klasorlerine minimal ekleme.

Kurallar:
- Her yeni public type kendi dosyasinda olur.
- Existing namespace pattern korunur:
  - **Contract:** `Diten.Platform.Common.Authorization` (handler'lar burdan tuketebilsin).
  - **Impl (HttpContext-bound):** `Diten.Platform.Infrastructure.Authorization` (mevcut `PermissionAuthorizationHandler.cs` AuthService ile karistirilmaz; bizim klasor `Diten.Platform.Infrastructure/Authorization/`).
- DI registration mevcut `services.AddScoped<ICurrentUserContext, CurrentUserContext>();` satirinin ([services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs:128](../../../../services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs)) hemen altina eklenir.
- `AnonymousTenantAuthorizationContext` ayni namespace icinde (`Diten.Platform.Common.Authorization`) dosyasi `AnonymousTenantAuthorizationContext.cs`.
- FU12b handler refactor mevcut handler dosyalarinin **tamamini yeniden yazmaz**; sadece claim parse satirlari `_context.TenantId`/`_context.ActorType`/`_context.IsPlatformAdmin` ile degistirilir. Audit sink ve `IEntitlementChecker` dependency'leri ayni kalir.

## 11. Frontend File Contract

Frontend dosyasi yoktur.

- RBAC Admin UI yok (MOD-0018-FU9 ayri pack).
- DataTable yok.
- Razor partial yok.

## 12. Validation Rules

| Rule | Applies to | Expected |
|---|---|---|
| Required (non-null) | `ITenantAuthorizationContext.PermissionKeys` | Default empty array; null asla |
| Required (non-null) | `ITenantAuthorizationContext.RoleIds` | Default empty array; null asla; FU12'de her zaman empty |
| Required (non-null) | `ITenantAuthorizationContext.RoleNames` | Default empty array |
| Required (non-null) | `ITenantAuthorizationContext.OrgUnitIds` | Default empty; NoOp resolver empty doner |
| Required (non-null) | `ITenantAuthorizationContext.PositionIds` | Default empty |
| Required (non-null) | `ITenantAuthorizationContext.ManagerChain` | Default empty |
| Empty Guid | `TenantId`/`UserId` | Authenticated degilse `Guid.Empty`; null asla |
| Computed | `IsPlatformAdmin` | `string.Equals(ActorType, "platform_admin", OrdinalIgnoreCase)`; null actor_type icin false |
| Lazy | Org alanlari | `InitializeAsync()` cagrilmadan empty/null; bir kere `InitializeAsync()` cagrildiktan sonra resolver sonucu sabit kalir |
| DI lifetime | `ITenantAuthorizationContext` | Scoped (Singleton **yasak**) |
| Anonymous semantics | `IHttpContextAccessor.HttpContext is null` | Tum alanlar empty/null/false; exception atilmaz |
| Karar (B12-1) | Property'ler sync; org resolution explicit `InitializeAsync()` | Async resolver per-request bir kere; memoized; `Task.GetAwaiter().GetResult()` **yasak** |

## 13. Failure Path to Verify

- **Anonymous request** (no JWT) → `IsAuthenticated=false`, `TenantId=Guid.Empty`, `UserId=Guid.Empty`, `ActorType=null`, `IsPlatformAdmin=false`, `PermissionKeys=empty`, org alanlari empty/null. Exception **atilmaz**.
- **JWT `tenant_id` claim missing veya invalid GUID** → `TenantId=Guid.Empty`. Mevcut handler davranisi degismez (FU12b'de handler `IsAuthenticated && TenantId == Guid.Empty` icin "valid tenant_id required" deny yazar).
- **JWT `actor_type` claim missing** → `ActorType=null`; `IsPlatformAdmin=false`.
- **JWT `actor_type = "platform_admin"`** → `IsPlatformAdmin=true`. (FU12b'de bypass kolu bu degeri okur.)
- **`HttpContext is null`** → Anonymous semantics; testler `AnonymousTenantAuthorizationContext` ile karsilastirir.
- **`IDataScopeResolver.ResolveAsync` throws** → `InitializeAsync()` exception fail-fast; **org alanlari empty/null kalir, sync property'ler default**. Hata loglanir; authorization karari etkilenmez. (Karar: resolver hata fail-safe — empty doner, exception loglu sessizce yutulur. Bu, MOD-0021 audit ile takip edilir.)
- **`InitializeAsync()` cagrilmadan org property okuma** → Default empty/null doner; bu beklenen davranis. Test bunu kanitlar.
- **`PermissionKeys` JWT'de yok** → Empty array.
- **`platform_admin` + sahte `tenant_id` claim** → `TenantId` claim parse edilir ama `IsPlatformAdmin` true oldugu icin handler bypass'i tetikler; tenant scope ihlali handler katmaninda kontrol edilir (mevcut davranis).
- **Service-to-service actor (`actor_type=service`)** → `IsAuthenticated=true`, `IsPlatformAdmin=false`; bu actor handler tarafindan reddedilir (mevcut davranis FU12b'de korunur).

## 14. Authorization Convention

- Bu pack runtime authorization karari uretmez; sadece kararin tuketim noktasini consolidate eder.
- Permission gate mevcut `[HasPermission("Platform.X.Y")]` sistemi ile devam eder.
- Module gate: `[RequiresModule("MODULE_CODE")]` — degismez.
- Feature gate: `[RequiresFeature("FEATURE_CODE")]` — degismez.
- `EntitlementResolutionSource.PlatformAdminBypass` FU12b'de handler bypass kolunda emit edilir; FU10a/FU10b'de sadece tanimliydi.
- Permission naming convention degisikligi **bu pack disinda** (FU13'te).
- Yeni permission, yeni endpoint, yeni controller, yeni action **yoktur**.

## 15. Gateway / API Routing Decision

Gateway route degisikligi gerekli degildir.

- Yeni public endpoint acilmaz.
- Mevcut `AuthorizationProbeController` test/dev-only yuzey olarak kalir; bu pack tarafindan modifiye edilmez (FU14 alani).
- `gateway/Diten.ApiGateway/**/ocelot.json` bu pack tarafindan degistirilmez.

## 16. Acceptance Criteria

### Step 0 (doc-only prerequisite — MUST merge before FU12a code work)

1. **FU10a + FU10b merge** olmus durumda; `EntitlementResolutionSource` enum ve `IDataScopeResolver` contract Common'da mevcut.
2. **Master-plan §12 Track G-prime** FU12a/FU12b sira durumu yansitilmis.

### FU12a — Context Contract + Default Implementation (MVF gate)

3. **Existing authorization behavior degismez.** `AuthorizationProbeController` uzerinden mevcut tum 200/401/403 senaryolari ayni davranir; mevcut `TenantModuleAuthorizationHandlerTests` ve `TenantFeatureAuthorizationHandlerTests` **degismeden** gecer.
4. **`ITenantAuthorizationContext`** `Diten.Platform.Common.Authorization` namespace'inde tanimli; sema §4'teki tabloyla birebir.
5. **`JwtTenantAuthorizationContext`** `Diten.Platform.Infrastructure.Authorization` namespace'inde tanimli; constructor `(IHttpContextAccessor, IDataScopeResolver)` alir; Scoped DI ile register.
6. **`AnonymousTenantAuthorizationContext`** sealed sinif olarak Common.Authorization'da; constructor parametresiz; tum alanlar default empty/null/false.
7. **DI registration:** `IServiceCollection.AddInfrastructure()` (veya hangi infrastructure DI metodu register ediyorsa) cagrildiktan sonra `using var scope = sp.CreateScope(); scope.ServiceProvider.GetRequiredService<ITenantAuthorizationContext>()` `JwtTenantAuthorizationContext` instance'i doner.
8. **`InitializeAsync()` sync property'leri etkilemez.** `TenantId`, `UserId`, `ActorType`, `IsAuthenticated`, `IsPlatformAdmin`, `PermissionKeys`, `RoleNames`, `RoleIds` ilk constructor cagrisinda hidrate olur.
9. **`InitializeAsync()` org alanlarini bir kere doldurur.** Ikinci cagri `IDataScopeResolver.ResolveAsync`'i tekrar **cagirmaz** (memoization). NoOp resolver ile sonuc empty.
10. **`HttpContext is null` durumu anonymous semantics doner** (tum alanlar default; exception atilmaz).
11. **`IDataScopeResolver.ResolveAsync` throws** → exception fail-safe yutulur (log gerekirse), org alanlari default kalir. Authorization karari etkilenmez. (Test bunu kanitlar; logging assertion gerekmez.)
12. **`PermissionKeys`** JWT `permission` claim'lerinden hidrate; null gelirse empty.
13. **`RoleNames`** JWT `ClaimTypes.Role` claim'lerinden hidrate; null gelirse empty.
14. **`RoleIds`** her durumda **empty** (FU12 kapsami).
15. **`IsPlatformAdmin`** sadece `actor_type == "platform_admin"` (case-insensitive) icin true.
16. **DI smoke:** ayri scope'lar farkli `JwtTenantAuthorizationContext` instance'i; ayni scope ayni instance.
17. **Mevcut tum testler degismeden gecer** (`dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests` — 447 → 451+).
18. **4 yeni unit test sinifi eklenir** (bkz §17 FU12a).
19. **Build PASS:** `Diten.Platform.Common`, `Diten.Platform.API`, `Diten.Platform.Application.Tests`.
20. **Handler'lar acilmaz** (FU12b'nin isi); davranis degismez.
21. **AuthService claim isimleri degismez** (`tenant_id`, `actor_type`, `permission`, `sub`, `ClaimTypes.Role`). AuthService dosyalarina dokunulmaz.

### FU12b — Handler Refactor + Bypass Metadata (MVF disi)

22. **`TenantModuleAuthorizationHandler` ve `TenantFeatureAuthorizationHandler` `_context: ITenantAuthorizationContext` injection alir.** `context.User.FindFirst("tenant_id")`/`actor_type` cagrilari kaldirilir.
23. **`platform_admin` bypass davranisi birebir korunur.** `context.Succeed(requirement)` cagrisi ayni anda kalir; ek olarak (opsiyonel) `EntitlementCheckResult.Allowed(..., resolvedFrom: PlatformAdminBypass)` audit sink'e gonderilir (audit allow sinki FU14'te eklenir; FU12b emit cagrisini hazirlar ama no-op sink mevcut).
24. **`partner_admin` fail-closed davranisi korunur.**
25. **`tenant_user` icin `TenantId == Guid.Empty` deny korunur.**
26. **Karar (allow/deny) FU12b oncesi/sonrasi birebir ayni;** fixture comparison testi.
27. **Mevcut handler unit testleri claim-mock'tan context-mock'a refactor edilir;** davranissal assertion'lar 1:1 korunur (audit deny mesaji format'i, deny reason).
28. **1 yeni unit test sinifi eklenir** (`AuthorizationHandlerContextConsumptionTests` — bkz §17 FU12b).
29. **Build PASS:** `Diten.Platform.Common`, `Diten.Platform.API`, `Diten.Platform.Application.Tests`.

## 17. Test Expectations

### FU12a — Context Contract + Default Implementation (4 yeni test sinifi)

- **`TenantAuthorizationContextHydrationTests`**
  - Full JWT (`tenant_id`, `actor_type=tenant_user`, `sub`, 3 permission, 2 role) → tum sync alanlar dogru hidrate.
  - JWT `actor_type=platform_admin` → `IsPlatformAdmin=true`.
  - JWT `tenant_id` missing → `TenantId=Guid.Empty`.
  - JWT `tenant_id` invalid format → `TenantId=Guid.Empty`.
  - `HttpContext.User.Identity?.IsAuthenticated == false` → `IsAuthenticated=false`; tum alanlar default.
  - `PermissionKeys` JWT'de yok → empty.
  - `RoleNames` JWT'de yok → empty.
  - `RoleIds` her durumda empty (FU12 kapsami).
- **`AnonymousTenantAuthorizationContextTests`**
  - Constructor parametresiz instance: tum alanlar default empty/null/false.
  - `InitializeAsync()` cagrildiktan sonra hala empty/null.
- **`TenantAuthorizationContextRegistrationTests`**
  - `AddInfrastructure()` sonrasi `ITenantAuthorizationContext` Scoped descriptor.
  - Resolved instance `JwtTenantAuthorizationContext`.
  - Ayri scope'lar farkli instance.
  - Ayni scope ayni instance.
- **`TenantAuthorizationContextDataScopeIntegrationTests`**
  - `InitializeAsync()` `IDataScopeResolver.ResolveAsync`'i bir kere cagirir.
  - Ikinci `InitializeAsync()` resolver'i tekrar cagirmaz (memoization).
  - NoOp resolver ile `OrgUnitIds=empty`, `PositionIds=empty`, `LegalEntityId=null`, `Country=null`, `ManagerChain=empty`.
  - Resolver `throws` → `InitializeAsync()` exception yutar; org alanlari default kalir; sync alanlar etkilenmez.
  - `InitializeAsync()` cagrilmadan org property okuma → empty/null (lazy default).

### FU12b — Handler Refactor (1 yeni + 2 mevcut refactor)

- **`AuthorizationHandlerContextConsumptionTests`**
  - `TenantModuleAuthorizationHandler` `_context.IsPlatformAdmin=true` icin context.Succeed + (opsiyonel) audit allow emit.
  - `_context.IsAuthenticated=false` icin deny.
  - `_context.ActorType="partner_admin"` icin fail-closed deny.
  - `_context.ActorType="tenant_user"` + `_context.TenantId=Guid.Empty` icin deny.
  - `_context.ActorType="tenant_user"` + valid TenantId + `IEntitlementChecker` allow → context.Succeed.
  - `_context.ActorType="tenant_user"` + valid TenantId + `IEntitlementChecker` deny → audit deny + fail.
  - Mevcut `TenantModuleAuthorizationHandlerTests` ve `TenantFeatureAuthorizationHandlerTests` claim-mock'tan context-mock'a refactor edilir; davranissal assertion'lar 1:1 korunur.

**Mevcut testler degismez (FU12a):**
- `TenantModuleAuthorizationHandlerTests`, `TenantFeatureAuthorizationHandlerTests`, `EntitlementAuthorizationPolicyProviderTests`, `EntitlementCheckerFailureSemanticsTests`, `EntitlementResolutionSourcePropagationTests`, `EntitlementCacheInvalidationConsumerTests`, `AuthorizationProbeControllerIntegrationTests`, `TemporaryAccessFoundationTests`, `EntitlementDataScopeKindExpansionTests`, `EntitlementCheckResultExtensionTests`, `NoOpDataScopeResolverTests`, `DataScopeResolverRegistrationTests`, `PlatformEntitlementAuditSinkTests`, `PlatformEntitlementAuditSinkIntegrationTests`, `EntitlementCacheInvalidationConsumerRegistrationTests` — hepsi.

**Mevcut testler refactor (FU12b — davranis korunur):**
- `TenantModuleAuthorizationHandlerTests` ve `TenantFeatureAuthorizationHandlerTests` — claim setup'i `ITenantAuthorizationContext` mock'a degisir. Test isimleri ve assertion'lar birebir korunur.

**Build / Smoke komutlari:**
- `dotnet build services/Diten.Platform.Common/src/Diten.Platform.Common/Diten.Platform.Common.csproj -c Debug`
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter FullyQualifiedName~Authorization`
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests`

**Frontend / Browser smoke:** N/A (UI yok).
**RESX parity:** N/A.
**DataTable verifier:** N/A (`golden_reference: none`).

## 18. Ready-for-dev Checklist

> **Historical authored checklist retained for audit.**
> Runtime completion is reconciled in §19.

### Step 0 prerequisites (MUST be done before FU12a code work)

- [ ] Kullanici bu draft pack'i inceleyip scope'u onayladi.
- [ ] FU10a + FU10b merge oldu (Step 0 prerequisite #1).
- [ ] **B12-1 (sync property + explicit `InitializeAsync()` karari)** onaylandi.
- [ ] `JwtTenantAuthorizationContext`'in `IHttpContextAccessor`'a bagli Infrastructure katmaninda yasamasi onaylandi (mevcut `CurrentUserContext.cs` pattern'i).
- [ ] Master-plan §12 Track G-prime FU12a/FU12b status guncellemesi onaylandi.

### FU12a — Context Contract + Default Implementation Code Work

- [ ] Status `approved` veya `ready-for-dev` yapildi.
- [ ] `ITenantAuthorizationContext` field set onaylandi (§4 tablo).
- [ ] `AnonymousTenantAuthorizationContext` null-object pattern onaylandi.
- [ ] DI lifetime Scoped onaylandi.
- [ ] `IDataScopeResolver` integration (`InitializeAsync()` memoization + fail-safe) onaylandi.
- [ ] `IsPlatformAdmin` computed semantic (`OrdinalIgnoreCase`) onaylandi.
- [ ] AuthService claim isimlerinin (`tenant_id`, `actor_type`, `permission`, `sub`, `ClaimTypes.Role`) bu pack'te degismeyecegi onaylandi.
- [ ] AuthService Roles/Permissions CRUD out-of-scope kalacak (FU13/MOD-AUTH-001).
- [ ] Gercek tenant controller wiring sonraki pilot tenant modulune kalacak.
- [ ] Handler refactor (FU12b) ayri PR'a birakildigi onaylandi.

### FU12b — Handler Refactor Code Work (MVF disi)

- [ ] FU12a code PR'i merge oldu (FU12b on-kosulu).
- [ ] Handler claim parse satirlarinin `ITenantAuthorizationContext` arkasina toplanma karari onaylandi.
- [ ] `platform_admin` bypass kolunda `EntitlementCheckResult.Allowed(..., resolvedFrom: PlatformAdminBypass)` emit etmenin (allow audit hazirligi) **opsiyonel** oldugu onaylandi; FU14 allow audit sink eklemeden no-op kalir.
- [ ] Mevcut handler test'lerinin claim-mock'tan context-mock'a refactor edilebileceginin onaylandi (test isimleri/assertion'lar 1:1 korunur).

### Downstream coordination

- [ ] FU13 / FU14 / MOD-0040 / MOD-0018-FU15 / MOD-AUTH-001 ile yasama sirasi konfirme edildi.
- [ ] **MVF eshigi:** FU12a + MOD-0040 minimal tamamlandiginda Tenant Users/Roles development guvenli baslar; FU12b/FU13/FU14/FU11/MOD-0018-FU15 paralel pencerelere dagilir.

## 19. Implementation Notes

**B12-1 karar metni (Async resolver vs Sync property):**

`ITenantAuthorizationContext` property'leri **synchronous** dondurur (`IReadOnlyList<Guid>` direk; `Task<>` veya `ValueTask<>` degil). Async `IDataScopeResolver.ResolveAsync` per-request bir kere `InitializeAsync()` ile bekletilir; memoize edilir. Handler ve consumer'lar `await context.InitializeAsync(ct);` cagirir, sonra sync property'ler okur.

Gerekce:
- `Task.GetAwaiter().GetResult()` ASP.NET Core'da deadlock riski tasir; **yasaktir**.
- Property getter'larin `Task<>` dondurmesi consumer code'da ergonomic kayba neden olur (her property erisimi `await`).
- Lazy `InitializeAsync()` pattern handler'lara `await context.InitializeAsync(ct);` tek satir maliyetiyle gelir; mevcut authorization handler `Task` doner zaten.

**Alternative tartisildi:**
- Tum property'leri `ValueTask<T>` yapmak — bir kere init sonrasi sync olur ama API yine `await` gerektirir; ergonomik dezavantaj.
- Property'leri sync birakip resolver'i ctor'da `.GetAwaiter().GetResult()` ile cagirmak — yasak (deadlock + senkron HttpContext erisimi MVC pipeline'i bloklar).

**Hidrasyon zamanlamasi:**
- **Constructor**: yalniz JWT claim erisimi (lightweight, sync).
- **InitializeAsync(ct)**: yalniz org alanlari icin resolver cagrisi; bir kere; memoize.
- **Property getter**: sync; org alanlari init edilmediyse default empty/null.

**Future-proofing notlari:**
- Service-to-service actor (`actor_type=service`) JWT'siz/signed-envelope ile gelirse, ileride `ServiceTokenTenantAuthorizationContext` impl'i eklenir; `JwtTenantAuthorizationContext` IHttpContextAccessor'a bagli kalir.
- Partner admin signed scope claims (MOD-0018-FU8) `JwtTenantAuthorizationContext`'e `AllowedTenantIds` alani ekleyebilir; mevcut contract degismez.
- Permission claims JWT'den server-side resolve'a tasirsa (FU13'te ihtimal), `JwtTenantAuthorizationContext.PermissionKeys` getter'i resolver'a yonlendirilir; contract degismez.

**FU12b bypass emit notu:**
FU12b sirasinda `platform_admin` bypass kolu su yapida olur:
```text
if (_context.IsPlatformAdmin)
{
    // future: optional allow audit emit with EntitlementCheckResult.Allowed(... resolvedFrom: PlatformAdminBypass)
    context.Succeed(requirement);
    return;
}
```
Audit allow sink (`IEntitlementAuditSink.LogAllowedAsync`) FU14'te eklendiginde, FU12b'nin bu noktada hazirladigi emit cagrisi aktif olur. FU14 olmadan no-op.

**Master-plan baglantisi:**
- Bu pack `docs/platform/master-plan.md` §12 Track G-prime alt bolumundeki **11d. MOD-0018-FU12** maddesinin uygulamasidir.
- MVF eshigi FU12a + MOD-0040 minimal'in tamamlanmasini gerektirir. Tenant Users/Roles development bu eshik gelmeden baslatilmamali.

**Risk notlari:**
- FU12a runtime davranisi degismediginden cok dusuk risklidir; sadece yeni DI ekler.
- FU12b en riskli yer: handler refactor sirasinda audit deny mesaji format'inin degismemesi (`FormatDenyReason`). Mevcut testlerin string assertion'lari korunmali.
- `JwtTenantAuthorizationContext` `IDataScopeResolver.ResolveAsync` calls'unu **constructor'da degil InitializeAsync'te** yapar; sync ctor garantisi.
- `InitializeAsync()` test cagrisi yapilmadan org alanlari empty/null oldugundan, FU12'yi tuketen ileri pack'ler (MOD-AUTH-001) **handler/middleware tarafinda `await context.InitializeAsync(ct)` cagrisini unutmamali**. Bu kontrat §17 testleri ile kanitlanir; ayrica downstream pack'lerin Implementation Notes'una eklenir.

**Reconciliation note (2026-06-02) — `status: draft → done` (Access Governance Foundation Planning milestone):**

This pack's lifecycle is reconciled to match runtime reality that is **already merged on `main`**. The FU12
runtime was confirmed by a strict read-only inspection of the current branch; **no runtime or test code was
modified by this reconciliation**. Evidence:

- **FU12a — context contract + default implementation (merged):**
  - `ITenantAuthorizationContext` — `Diten.Platform.Common.Authorization` (contract).
  - `AnonymousTenantAuthorizationContext` — `Diten.Platform.Common.Authorization` (null-object fallback).
  - `JwtTenantAuthorizationContext` — `Diten.Platform.Infrastructure.Authorization` (HttpContext-bound default impl).
  - DI registration: `ITenantAuthorizationContext → JwtTenantAuthorizationContext` registered **Scoped**
    (`services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs`).
- **FU12b — handler refactor (merged):**
  - `TenantModuleAuthorizationHandler` consumes `ITenantAuthorizationContext` (constructor injection).
  - `TenantFeatureAuthorizationHandler` consumes `ITenantAuthorizationContext` (constructor injection).
- **FU12 test evidence present:**
  - `TenantAuthorizationContextHydrationTests`
  - `TenantAuthorizationContextRegistrationTests`
  - `TenantAuthorizationContextDataScopeIntegrationTests`
  - `AnonymousTenantAuthorizationContextTests`

This is **governance reconciliation of merged runtime reality. No new production implementation is introduced.**
The §1–§18 design sections are preserved as the original authored contract and are intentionally not rewritten;
the earlier "Draft note" blockquote is superseded by this reconciliation entry. This closes DCP-001 §8 ordered
step 2 (FU12 governance reconciliation) and DCP-001 acceptance criterion 2. The registry row `MOD-0018-FU12` is
updated `draft → done` in the same milestone.

**Placeholder reconciliation (2026-06-02).** Two non-canonical placeholder IDs appear in this pack's design
sections and are clarified here without inventing replacement module IDs:

- `MOD-0018-FU8` — historical placeholder; current partner_admin runtime-scope hardening is tracked as GAP-13-1.
- `MOD-AUTH-001` — historical placeholder; canonical Tenant User / Tenant Role IDs will be reserved after
  MOD-0040 shape lock.

The bare `FU8` / `MOD-0018-FU8` and `MOD-AUTH-001` tokens elsewhere in this pack resolve to these clarifications.

## 20. Follow-up Items

- **MOD-0018-FU12-FU1**: `JwtTenantAuthorizationContext.PermissionKeys` icin server-side resolver tasimasi (JWT'de permission claim listesini buyutmemek icin). FU13 paralel.
- **MOD-0018-FU13**: Permission key convention + role-permission cache invalidation event'leri. `ITenantAuthorizationContext` user-keyed cache invalidation hook'larina baglanir.
- **MOD-0018-FU14**: Effective access explain + `IEntitlementAuditSink.LogAllowedAsync`. FU12b emit noktasini aktive eder.
- **MOD-0040**: Tenant Org Master Data (`OrgUnit`, `Position`, `UserOrgAssignment`); `ITenantAuthorizationContext.OrgUnitIds`/`PositionIds` icin gercek backing data.
- **MOD-0018-FU15**: Real `IDataScopeResolver` impl; `JwtTenantAuthorizationContext.InitializeAsync()` MOD-0040'a query atan resolver'i tuketir. `NEW-MOD-0041` deprecated alias'tir ve MOD-0041 ile collision olusturdugu icin kullanilmaz.
- **MOD-0018-FU8**: Partner scope claims; `JwtTenantAuthorizationContext`'e `AllowedTenantIds` alani additive ekler.
- **MOD-AUTH-001 (Track G)**: Tenant Users/Roles CRUD — bu pack'in tukettigi `ITenantAuthorizationContext` SSOT'u.
- **S2S Actor Foundation** (ileri pack, ID yok): `ServiceTokenTenantAuthorizationContext` impl ve actor_type=service icin signed envelope/HMAC karari.
- **master-plan.md §12 Track G-prime guncellemesi**: FU12a + FU12b merged. Lifecycle reconciled to done in the Access Governance Foundation Planning milestone.
