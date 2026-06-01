---
id: MOD-0018-FU10
name: Authorization Decision Contract Extension
domain: platform-shared-services
service: Diten.Platform.Common
shell: none
golden_reference: none
entity_base: GlobalEntity
status: ready-for-dev
owner: platform-team
branch: feature/pss/mod-0018-fu10-authz-foundation-extension
started: 2026-05-21
target: 2026-06-04
form_field_count: 0
---

# MOD-0018-FU10 — Authorization Decision Contract Extension

> **Draft note:** Bu pack, MOD-0018 production wiring üzerine kurulan **contract-only foundation extension** isidir. Kod yazimi pack `approved` veya `ready-for-dev` yapilmadan baslamaz. Mevcut MOD-0018 davranisi degismez; tum yenilikler **additive ve backward-compatible**'dir.

> **Two-Step Implementation (orchestrator review 2026-05-21):** Implementation iki ardisik PR olarak yapilir:
> - **FU10a — Pure Contract Extension** (`feature/pss/mod-0018-fu10a-decision-contract-extension`): Yalniz `Diten.Platform.Common/Authorization` altinda contract/enum/typed payload/`IDataScopeResolver`+`NoOpDataScopeResolver` ve `Diten.Platform.Application/DependencyInjection.cs` DI registration. **Behavior change: SIFIR.**
> - **FU10b — EntitlementChecker ResolvedFrom Mapping** (`feature/pss/mod-0018-fu10b-resolved-from-mapping`): Yalniz `Diten.Platform.Application/Services/EntitlementChecker.cs` `ResolvedFrom`/`ResolvedAtUtc` set noktasi. Behavior degisikligi yalniz allow result'in metadata alanlarinda; karar (allow/deny) ayni.
>
> Iki PR bagimsiz review edilir. FU10a merge olmadan FU10b baslamaz. FU11/FU12 her ikisi de merge olmadan baslamaz.

> **Step 0 (prerequisite, doc-only):** Bu pack `ready-for-dev` yapilmadan **once** MOD-0018 pack'inin §16 acceptance criteria #2 ve §19 Implementation Notes "EntitlementCheckResult Extension Policy" revize'si merge olmus olmalidir. Step 0 doc PR'i FU10a code PR'inin **on-kosulu**dur. Bu revize 2026-05-21 tarihli orchestrator review sonucu MOD-0018 pack body'sine islenmistir.

> **Golden Reference karari:** Bu is UI/DataTable modulu degildir. `shell: none`, `golden_reference: none`, `form_field_count: 0`. Razor layout, DataTable verifier, RESX ve frontend dosya seti bu pack icin N/A'dir.

> **entity_base alani:** Frontmatter zorunlulugu nedeniyle `GlobalEntity` yazildi; FU10 yeni entity eklemez. Mevcut MOD-0018 pack'inin ayni konvansiyonunu izler.

## 1. Module Summary

MOD-0018-FU10, mevcut MOD-0018 authorization foundation'ini enterprise authorization senaryolarini surdurulebilir hale getirecek **decision contract extension**'i ekler. Amac; HR/CRM/BPM/OrgHierarchy modulleri geldiginde **`EntitlementCheckResult` imzasi**, **`EntitlementDataScopeKind` enum'u** ve **data scope cozumleyici contract**'i yeniden refactor gerektirmesin. Hicbir runtime behavior degismez; NoOp implementation default kalir.

Pack su 4 amaca odaklanir:
1. `EntitlementCheckResult`'a additive alanlar ekleyerek explainability ve scope propagation icin tasiyici alani acmak (`EffectiveScopes`, `ResolvedFrom`, `ResolvedAtUtc`).
2. `EntitlementDataScopeKind` enum'unu enterprise senaryolari karsilayacak sekilde genisletmek (`OrgUnit`, `Department`, `Team`, `Position`, `LegalEntity`, `Region`, `ManagerChain`, `RecordOwner`).
3. `EntitlementDataScope` value-tipini sertlestirerek typed payload tasimasini saglamak (`string?` yerine struct'lu).
4. `IDataScopeResolver` contract'ini ve `NoOpDataScopeResolver` default impl'ini koyarak ileri pack'lerin (MOD-0040/MOD-0018-FU15) bagli kalacagi DI seam'ini hazirlamak.

## 2. Ownership and Boundaries

**In scope:**
- `EntitlementCheckResult` record'una additive alanlar (default-valued; mevcut consumer'lar etkilenmez).
- `EntitlementDataScopeKind` enum genisletmesi (mevcut 5 deger korunur; yeni 8 deger eklenir).
- `EntitlementDataScope` typed payload refinement.
- `IDataScopeResolver` contract + `NoOpDataScopeResolver` + DI registration.
- `EntitlementChecker` icinde `ResolvedFrom` set noktasi (davranis degismeden, sebep alanlari doldurulur).
- Mevcut testlerin additive alanlar gelmesine ragmen kirilmadan gecmesi.
- Yeni unit testler: enum exact value, default value semantics, NoOp resolver empty davranisi, DI smoke.

**Out of scope:**
- Gercek data scope resolution (MOD-0040 + MOD-0041'in isi).
- Org hierarchy / OrgUnit / Position / UserOrgAssignment entity'leri (MOD-0040).
- `ITemporaryAccessProvider` authorization pipeline'a baglama (MOD-0018-FU11).
- `ITenantAuthorizationContext` runtime context consolidation (MOD-0018-FU12).
- Permission cache invalidation event'leri (MOD-0018-FU13).
- Effective access explain endpoint ve allow-with-reason audit (MOD-0018-FU14).
- Tenant Users / Roles / Permissions CRUD (MOD-AUTH-001 / Track G).
- HR module / BPM runtime / CRM modulu.
- Repository tarafinda query filtering veya gercek enforcement davranisi degisikligi.
- UI / Admin panel.

## 3. Owned Objects

**Genisletilecek mevcut tipler (additive):**
- `EntitlementCheckResult` ([services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/EntitlementCheckResult.cs](../../../../services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/EntitlementCheckResult.cs)) — yeni alanlar: `EffectiveScopes`, `ResolvedFrom`, `ResolvedAtUtc`.
- `EntitlementDataScopeKind` ([services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/EntitlementDataScopeKind.cs](../../../../services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/EntitlementDataScopeKind.cs)) — 8 yeni enum degeri.
- `EntitlementDataScope` ([services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/EntitlementDataScope.cs](../../../../services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/EntitlementDataScope.cs)) — typed payload (`ScopeId`, `ScopeCode`, `IsInclude`); mevcut `Value` alani backward-compatible deprecated hale gelir ya da computed property ile esleser (karar pack'te netlestirilir, AC #5).

**Yeni contract-only tipler:**
- `EntitlementResolutionSource` enum (new) — `Plan | Override | Addon | Trial | Temporary | Permission | PlatformAdminBypass | Unknown`.
- `IDataScopeResolver` interface (new).
- `NoOpDataScopeResolver` class (new) — default DI registration.

**Etkilenen mevcut tipler (sebep alani doldurma):**
- `EntitlementChecker` ([services/Diten.Platform/src/Diten.Platform.Application/Services/EntitlementChecker.cs](../../../../services/Diten.Platform/src/Diten.Platform.Application/Services/EntitlementChecker.cs)) — `ResolvedFrom` ve `ResolvedAtUtc` set noktalari eklenir; davranis ayni.
- `EntitlementCheckResult.Allowed` / `EntitlementCheckResult.Denied` static factory overload'lari — yeni alanlar opsiyonel parametre olarak eklenir, mevcut imzalar korunur.
- DI: [services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs](../../../../services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs:50) — `IDataScopeResolver` -> `NoOpDataScopeResolver` registration eklenir.

**Yeni test dosyalari:**
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/EntitlementDataScopeKindExpansionTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/EntitlementCheckResultExtensionTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/NoOpDataScopeResolverTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/DataScopeResolverRegistrationTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/EntitlementResolutionSourcePropagationTests.cs`

## 4. Entity Fields

Bu pack persisted entity eklemez. Asagidaki tablo **contract sema**'sini belgeler.

| Object | Field | Type | Required | Rule |
|---|---|---|---|---|
| `EntitlementResolutionSource` | values | enum | yes | `Plan = 0`, `Override = 1`, `Addon = 2`, `Trial = 3`, `Temporary = 4`, `Permission = 5`, `PlatformAdminBypass = 6`, `Unknown = 99` |
| `EntitlementCheckResult` | `EffectiveScopes` | `IReadOnlyList<EntitlementDataScope>` | yes | Default empty array; null asla |
| `EntitlementCheckResult` | `ResolvedFrom` | `EntitlementResolutionSource` | yes | Default `Unknown`; `EntitlementChecker` allow path'larda dolu set eder |
| `EntitlementCheckResult` | `ResolvedAtUtc` | `DateTimeOffset` | yes | Default `DateTimeOffset.UtcNow`; cache miss anini gosterir |
| `EntitlementDataScopeKind` | yeni degerler | enum | yes | Mevcut 5 deger korunur (`Company=0`, `Country=1`, `Own=2`, `Assigned=3`, `ProcessRelatedRecord=4`). Yeni degerler: `OrgUnit=5`, `Department=6`, `Team=7`, `Position=8`, `LegalEntity=9`, `Region=10`, `ManagerChain=11`, `RecordOwner=12` |
| `EntitlementDataScope` | `Kind` | `EntitlementDataScopeKind` | yes | Mevcut korunur |
| `EntitlementDataScope` | `ScopeId` | `Guid?` | no | OrgUnit/Department/Position/LegalEntity ID'leri icin |
| `EntitlementDataScope` | `ScopeCode` | `string?` | no | Country/Region kodlari icin (ISO 3166 / region key) |
| `EntitlementDataScope` | `IsInclude` | `bool` | yes | Default `true`; ileride exclusion scope'lari icin altyapi |
| `EntitlementDataScope` | `Value` | `string?` | no | **Backward-compatible bagimsiz alan** (B1 lock 2026-05-21). Mevcut consumer'lar icin korunur; yeni alanlar (`ScopeId`/`ScopeCode`/`IsInclude`) **bagimsiz** tasinir. Constructor body'sinde `Value <-> ScopeCode` otomatik remap **yapilmaz**; mapping kararini tuketici verir. Yeni kod `ScopeCode`/`ScopeId` kullanir; `Value` deprecation isaretlemesi follow-up "deemphasize phase"'e (FU10-FU1) birakilir. |
| `IDataScopeResolver` | `ResolveAsync` | `Task<IReadOnlyList<EntitlementDataScope>>` | yes | Imza: `(Guid tenantId, Guid userId, string moduleCode, string? featureCode, CancellationToken ct)` |
| `NoOpDataScopeResolver` | `ResolveAsync` | impl | yes | Her zaman `Array.Empty<EntitlementDataScope>()` doner |

**Backward compatibility kurallari:**
- `EntitlementCheckResult.Allowed(EntitlementKind, string, DateTimeOffset?)` ve `EntitlementCheckResult.Denied(...)` mevcut overload'lari korunur.
- Yeni overload'lar: `EntitlementCheckResult.Allowed(... , IReadOnlyList<EntitlementDataScope>? scopes, EntitlementResolutionSource resolvedFrom, DateTimeOffset? resolvedAtUtc)`. Tum yeni parametreler default'lu.
- `EntitlementDataScope(EntitlementDataScopeKind kind, string? value)` mevcut constructor korunur; yeni constructor `(Kind, ScopeId, ScopeCode, IsInclude)` eklenir.

## 5. Repo Scope

**Step 0 (doc-only PR, prerequisite):**
- `execution/domains/platform-shared-services/module-packs/MOD-0018-rbac-abac-authorization.md` (§16.2 + §18 checklist + §19 "EntitlementCheckResult Extension Policy" revize)
- `execution/domains/platform-shared-services/module-packs/MOD-0018-FU10-authorization-decision-contract-extension.md` (bu pack)
- `docs/platform/master-plan.md` (§12 Track G-prime alt bolumu + Track G gating notu)

**FU10a — Pure Contract Extension PR:**
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/EntitlementCheckResult.cs` (additive alanlar)
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/EntitlementDataScopeKind.cs` (8 yeni enum degeri)
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/EntitlementDataScope.cs` (typed payload — B1 lock)
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/EntitlementResolutionSource.cs` (yeni)
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/IDataScopeResolver.cs` (yeni)
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/NoOpDataScopeResolver.cs` (yeni)
- `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs` (`IDataScopeResolver` -> `NoOpDataScopeResolver` Scoped registration ekleme)
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/EntitlementDataScopeKindExpansionTests.cs` (yeni)
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/EntitlementCheckResultExtensionTests.cs` (yeni)
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/NoOpDataScopeResolverTests.cs` (yeni)
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/DataScopeResolverRegistrationTests.cs` (yeni)

**FU10b — EntitlementChecker ResolvedFrom Mapping PR:**
- `services/Diten.Platform/src/Diten.Platform.Application/Services/EntitlementChecker.cs` (yalniz `ResolvedFrom`/`ResolvedAtUtc` set noktalari; `EntitlementSource -> EntitlementResolutionSource` mapping)
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/EntitlementResolutionSourcePropagationTests.cs` (yeni)

## 6. Protected Paths

- `.antigravity/**` — global engineering system, kullanici onayi olmadan degismez.
- `services/Diten.AuthService/**` — Roles/Permissions CRUD ve token claim uretimi bu pack disinda; FU13'e birakilir.
- `frontend/Diten.Web/**` — UI degisikligi yok.
- `gateway/Diten.ApiGateway/**/ocelot.json` — gateway degisikligi yok.
- `services/Diten.MdmService/**`, `services/Diten.EnterpriseStrategyService/**`, `services/Diten.DevEnablementService/**` — diger domain servisleri.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` — FROZEN.
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/TemporaryAccessGrant.cs` — FU11'in alani; bu pack imzayi degistirmez.
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/ITemporaryAccessProvider.cs` — FU11.
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/TenantModuleAuthorizationHandler.cs` ve `TenantFeatureAuthorizationHandler.cs` — handler'lar bu pack'te degismez (FU11+FU12 isi).
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/IEntitlementAuditSink.cs` — FU14'un alani.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Eventing/EntitlementCacheInvalidationConsumer.cs` — FU13 baglami.

## 7. Dependencies

- **MOD-0018** RBAC / Entitlement Production Wiring — bu pack'in temeli; **§16.2 acceptance criteria revize** edilmeden FU10'a baslanmaz (bkz. §19 Implementation Notes).
- **MOD-0035** Event Bus — degismez; FU10 event publish/consume yapmaz.
- **PSS-007** Subscription Feature Management — feature catalog kaynagi; etkilenmez.
- **MOD-0298** Tenant Module Entitlement — etkilenmez.
- **MOD-0021** Audit Trail — etkilenmez (allow audit FU14'te).
- **MOD-0297** Tenant Subscription Lifecycle — etkilenmez.

**Downstream bagimliliklari (FU10 tamamlanmadan baslamamali):**
- MOD-0018-FU11 (`ITemporaryAccessProvider` pipeline binding) — `EffectiveScopes` ve `ResolvedFrom` alanlarini doldurur.
- MOD-0018-FU12 (`ITenantAuthorizationContext`) — `IDataScopeResolver`'i tuketir.
- MOD-0040 (Tenant Org Master Data) — `EntitlementDataScopeKind` yeni degerlerini consume eder.
- MOD-0018-FU15 (Real `IDataScopeResolver`) — `NoOpDataScopeResolver` yerine gercek impl gelir.

## 8. Runtime Constraints

- **Existing authorization behavior degismez.** `[RequiresModule]`/`[RequiresFeature]` decoration'lari ayni davranisi gosterir.
- `EntitlementCheckResult` public imzasinin **mevcut yuzeyleri** korunur; sadece additive alan eklenir. Mevcut overload'lar deprecated bile isaretlenmez (bu adim sonraya, deemphasize asamasina birakilir).
- `EntitlementDataScopeKind` mevcut 5 enum degerinin **numeric degeri** korunur; yeni degerler 5'ten baslayarak eklenir. Serileme/deserileme breaking degil.
- `IDataScopeResolver` default DI registration **singleton degil scoped** olarak yapilir (ileride request-scoped cache eklenmesine hazirlik).
- `EntitlementChecker.ResolveModuleEntitlementAsync` / `ResolveFeatureEntitlementAsync` `ResolvedFrom` setlerken **detail.Source** veya plan/override semantic'inden cevirir; transient hatada `ResolvedFrom = Unknown` doner.
- `platform_admin` bypass davranisi degismez; bypass durumunda `ResolvedFrom = PlatformAdminBypass` set edilir.
- `partner_admin` fail-closed davranisi degismez.
- Cache key formati ([services/Diten.Platform/src/Diten.Platform.Application/Services/EntitlementCacheService.cs](../../../../services/Diten.Platform/src/Diten.Platform.Application/Services/EntitlementCacheService.cs)) degismez; cache item icindeki `EntitlementCheckResult` yeni alanlari tasir, ama key shape ayni.
- `EntitlementCheckResult.IsCacheable=false` semantik'i degismez; transient hata path'inde `ResolvedFrom = Unknown`.

## 9. Layout & Shell Contract

`shell: none`. Bu pack backend/shared-contract isidir.

- Razor view yok.
- `Layout = "_LayoutPlatformAdmin"` veya `_LayoutTenantShell` kullanilmaz.
- Frontend route, DataTable, RESX ve Ctrl+K search registry N/A.
- `golden_reference: none` bu nedenle dogrudur.

## 10. Backend File Convention

Bu pack CRUD/DataTable modulu olmadigi icin Golden Reference CQRS klasor seti birebir uygulanmaz. Degisiklikler mevcut authorization/service klasorlerine **minimal ek** olarak yapilir.

Kurallar:
- Her yeni public type kendi dosyasinda olur (`EntitlementResolutionSource.cs`, `IDataScopeResolver.cs`, `NoOpDataScopeResolver.cs`).
- Existing namespace pattern korunur: `Diten.Platform.Common.Authorization`.
- Runtime kodu Application/Common/Infrastructure sinirlarina gore yerlesir:
  - shared contracts: `Diten.Platform.Common/Authorization`
  - app service/DI: `Diten.Platform.Application`
- Mevcut record/struct dosyalarinin imza dosya basinda yer alan tek `public sealed record`/`public sealed class` kurali korunur.
- Yeni overload'lar mevcut tip dosyasi icinde eklenir (`EntitlementCheckResult.cs` ayni dosya).
- DI registration mevcut `AddSingleton<ITemporaryAccessProvider, NoOpTemporaryAccessProvider>()` satirinin ([services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs](../../../../services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs:50)) hemen altina eklenir.

## 11. Frontend File Contract

Frontend dosyasi yoktur.

- RBAC Admin UI yok (MOD-0018-FU9 ayri pack).
- DataTable yok.
- Razor partial yok.
- `golden_reference: none` bu nedenle dogrudur.

## 12. Validation Rules

| Rule | Applies to | Expected |
|---|---|---|
| Enum exact set | `EntitlementDataScopeKind` | Mevcut 5 deger + yeni 8 deger; tam **13** deger; numeric assignment kararliligi (Company=0, ..., RecordOwner=12) |
| Enum exact set | `EntitlementResolutionSource` | 8 deger (`Plan`, `Override`, `Addon`, `Trial`, `Temporary`, `Permission`, `PlatformAdminBypass`, `Unknown`) |
| Required (non-null) | `EntitlementCheckResult.EffectiveScopes` | Default empty array; null asla. Constructor null gelirse empty'e cevirir |
| Required | `EntitlementCheckResult.ResolvedFrom` | Default `Unknown`; non-null |
| Required | `EntitlementCheckResult.ResolvedAtUtc` | `DateTimeOffset.UtcNow` default; `default(DateTimeOffset)` izinli degil |
| Mutually exclusive | `EntitlementDataScope` | `ScopeId` ve `ScopeCode` ayni anda dolu olabilir; biri null olabilir; ikisi de null durumunda `Kind=Own` veya `Kind=ManagerChain` gibi ID gerektirmeyen kind'lar icin gecerli |
| Backward compat (B1 lock) | `EntitlementDataScope.Value` | Mevcut `string?` Value alani korunur; yeni alanlar `ScopeId` (`Guid?`), `ScopeCode` (`string?`), `IsInclude` (`bool=true`) **bagimsiz** tasinir. Constructor body'sinde otomatik remap **yapilmaz**. Yeni kod `ScopeCode`/`ScopeId` kullanir; mapping kararini tuketici verir. Mevcut `new EntitlementDataScope(kind, value)` cagrilari derlenmeli; mevcut consumer testleri degismeden gecmeli. |
| NoOp resolver | `NoOpDataScopeResolver.ResolveAsync` | Her zaman `Array.Empty<EntitlementDataScope>()`; cancellation token honored |
| DI lifetime | `IDataScopeResolver` | `Scoped` (singleton degil, request-scope ileri cache hazirligi) |
| Acceptance criteria revize | MOD-0018 §16.2 | "EntitlementCheckResult imzasi degismez" maddesi **"additive-only, default-valued, backward-compatible extension allowed"** seklinde revize edilir; revize maddesi MOD-0018 pack'inde Implementation Notes uzerinden duzenlenir (kodla degil, dokumanla) |

## 13. Failure Path to Verify

- **Mevcut consumer `EntitlementCheckResult.Allowed(EntitlementKind.Module, "X")` cagrisi** → derleme PASS, `EffectiveScopes=empty`, `ResolvedFrom=Unknown`, `ResolvedAtUtc=now`. Mevcut testler degismez.
- **Mevcut `[RequiresModule]` decorated probe endpoint** → davranis birebir ayni (200/401/403); audit deny payload degismez.
- **NoOp resolver disabled / yanlislikla register edilmedi** → `IDataScopeResolver` DI resolve `InvalidOperationException` atar (default registration acceptance criteria #8 ile garanti); startup smoke testi yakalar.
- **Cancellation token honoured** → `NoOpDataScopeResolver.ResolveAsync` cancellation'da `OperationCanceledException` atar (best-effort; cancellation token destekli).
- **Yeni `EntitlementResolutionSource` degeri eklemeye kalkilirsa** → enum exact value test FAIL eder ve PR review'a dusurur (governance gate).
- **Mevcut `EntitlementDataScope(kind, value)` constructor cagrisi** → derleme PASS, `Value` set olur, `ScopeCode` null, `ScopeId` null, `IsInclude=true`.
- **`EntitlementCheckResult` JSON serileme** → eski clientlar (ileride consumer service'ler) yeni alanlari yok sayar; yeni clientlar default'lu okur; **breaking degil**.
- **Cache miss → factory exception** → `IsCacheable=false`, `ResolvedFrom=Unknown`, `ResolvedAtUtc=now`; mevcut transient hata davranisi korunur.

## 14. Authorization Convention

- Bu pack runtime authorization karari uretmez; sadece kararin sebep/ scope tasiyici alanlarini ekler.
- Permission gate mevcut `[HasPermission("Platform.X.Y")]` sistemi ile devam eder.
- Module gate: `[RequiresModule("MODULE_CODE")]` — degismez.
- Feature gate: `[RequiresFeature("FEATURE_CODE")]` — degismez.
- `EntitlementResolutionSource.PlatformAdminBypass` yalniz `platform_admin` actor_type bypass path'inden donmelidir; consumer'lar bu degeri "tum allow yetkisini gosterir" semantigi olarak okur.
- Permission naming convention degisikligi **bu pack disinda**; FU13'te `module.resource.action[.scope]` konvansiyonu netlestirilir.
- Yeni permission, yeni endpoint, yeni controller, yeni action **yoktur**.

## 15. Gateway / API Routing Decision

Gateway route degisikligi gerekli degildir.

- Yeni public endpoint acilmaz.
- Mevcut `AuthorizationProbeController` test/dev-only yuzey olarak kalir; bu pack tarafindan modifiye edilmez (FU14 alani).
- `gateway/Diten.ApiGateway/**/ocelot.json` bu pack tarafindan degistirilmez.

## 16. Acceptance Criteria

### Step 0 (doc-only prerequisite — MUST merge before FU10a code work)

1. **MOD-0018 §16.2 revize:** MOD-0018 pack'inin §16 acceptance criteria #2 maddesi "**additive-only, default-valued, backward-compatible extension allowed**" semantik kuralina cevrilir; tam politika metni MOD-0018 §19 "EntitlementCheckResult Extension Policy" bolumune islenir.
2. **MOD-0018 §18 checklist:** "imza degismeyecegi" maddesi yeni politika ile guncellenir.
3. **Master-plan §12:** "Track G-prime — Authorization Foundation Extension" alt bolumu eklenir; Track G (Tenant IAM Baseline) gating bu Track'in MVF eshigi (FU10a + FU12 + minimal MOD-0040) tamamlanmasina baglanir.

### FU10a — Pure Contract Extension

4. **Existing authorization behavior degismez.** `AuthorizationProbeController` uzerinden mevcut tum 200/401/403 senaryolari ayni davranir.
5. **`EntitlementCheckResult` mevcut public yuzeyi korunur** (mevcut `Allowed`/`Denied` static factory overload'lari kirilmaz; mevcut positional constructor cagrilari derlenir). Yeni alanlar additive ve default'lu.
6. **`EntitlementCheckResult.EffectiveScopes`** non-null, default empty array; null constructor argumani empty'e cevrilir.
7. **`EntitlementCheckResult.ResolvedFrom`** non-null, default `EntitlementResolutionSource.Unknown`.
8. **`EntitlementCheckResult.ResolvedAtUtc`** `default(DateTimeOffset)` degil; constructor default'u `DateTimeOffset.UtcNow`.
9. **`EntitlementDataScopeKind`** tam **13** degeri icerir; mevcut 5 degerin numeric ID'si degismez (Company=0..ProcessRelatedRecord=4); yeni degerler 5'ten basla (OrgUnit=5..RecordOwner=12).
10. **`EntitlementDataScope` (B1 lock):** Mevcut `(Kind, string? Value)` constructor'i korunur; yeni alanlar `Guid? ScopeId`, `string? ScopeCode`, `bool IsInclude=true` **bagimsiz** olarak eklenir; constructor body'sinde `Value <-> ScopeCode` otomatik remap **yapilmaz**. Mevcut `new EntitlementDataScope(kind, value)` cagrisi degismeden derlenir.
11. **`IDataScopeResolver`** contract'i `Diten.Platform.Common.Authorization` namespace'inde tanimli; imza: `Task<IReadOnlyList<EntitlementDataScope>> ResolveAsync(Guid tenantId, Guid userId, string moduleCode, string? featureCode, CancellationToken ct)`.
12. **`NoOpDataScopeResolver`** default impl olarak DI'a `Scoped` lifetime ile register edilir; her cagrida `Array.Empty<EntitlementDataScope>()` doner.
13. **DI smoke:** `IServiceCollection.AddApplication()` cagrildiktan sonra `using var scope = sp.CreateScope(); scope.ServiceProvider.GetRequiredService<IDataScopeResolver>()` `NoOpDataScopeResolver` instance'i doner; ayri scope'lar farkli instance verir.
14. **Mevcut tum testler degismeden gecer** (`dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests`).
15. **4 yeni unit test sinifi eklenir** (bkz §17 FU10a).
16. **Build PASS:** `Diten.Platform.Common`, `Diten.Platform.API`, `Diten.Platform.Application.Tests`.
17. **Platform admin endpoint'lerine fake gate eklenmez.**
18. **Gercek tenant business controller wiring yapilmaz.**
19. **Yeni event publish / consume yapilmaz.**
20. **`ITemporaryAccessProvider` pipeline bagi kurulmaz** (FU11'in isi).
21. **`ITenantAuthorizationContext` eklenmez** (FU12'nin isi).
22. **`EntitlementChecker` `ResolvedFrom` mapping yapilmaz** (FU10b'nin isi). FU10a sonrasi tum `EntitlementCheckResult` instance'lari `ResolvedFrom=Unknown` doner; davranissal etkisi yoktur.

### FU10b — EntitlementChecker ResolvedFrom Mapping

23. **`EntitlementChecker.ResolveModuleEntitlementAsync` allow path'i** `ResolvedFrom`'u `EntitlementSource`'tan map eder: `System` → `Plan`, `ManualOverride (enabled)` → `Override`, `Addon` → `Addon`, `Trial` → `Trial`. Plan-source allow (physical row yok) → `Plan`. `ResolvedAtUtc` set edilir.
24. **`EntitlementChecker.ResolveFeatureEntitlementAsync` allow path'i** plan-based oldugu icin `ResolvedFrom = Plan` doner; `ResolvedAtUtc` set edilir.
25. **Transient hata path'i (try/catch fallback)** `ResolvedFrom = Unknown` + `IsCacheable=false` doner; mevcut transient davranisi korunur.
26. **`platform_admin` bypass `ResolvedFrom` set noktasi FU10b kapsami DISIDIR.** `EntitlementChecker` actor_type'a erisemez; bypass `TenantModuleAuthorizationHandler`/`TenantFeatureAuthorizationHandler` icinde yapilir ve `ResolvedFrom = PlatformAdminBypass` set noktasi **FU12**'de gelir. FU10b sonrasi platform_admin bypass durumunda `ResolvedFrom = Unknown` kalir; bu testlerle belgelenir.
27. **Karar (allow/deny) degismez:** FU10b oncesi ve sonrasi `EntitlementCheckResult.IsAllowed` degeri **birebir** ayni; yalnizca `ResolvedFrom`/`ResolvedAtUtc` metadata alanlari farkli.
28. **1 yeni unit test sinifi eklenir** (`EntitlementResolutionSourcePropagationTests`, bkz §17 FU10b).
29. **Mevcut `EntitlementCheckerFailureSemanticsTests` degismeden gecer.**
30. **Build PASS:** `Diten.Platform.API`, `Diten.Platform.Application.Tests`.

## 17. Test Expectations

### FU10a — Pure Contract Extension (4 yeni test sinifi)

- **`EntitlementDataScopeKindExpansionTests`**
  - Enum exact value set test (13 deger; numeric assignment kararliligi).
  - Mevcut 5 degerin numeric ID'sinin degismedigi (Company=0..ProcessRelatedRecord=4).
  - Yeni 8 degerin numeric ID'si 5..12.
- **`EntitlementCheckResultExtensionTests`**
  - `Allowed(EntitlementKind, string)` overload'i — `EffectiveScopes=empty`, `ResolvedFrom=Unknown`, `ResolvedAtUtc≈now`.
  - `Denied(...)` overload'i — ayni default'lar; `DenyReason` korunur.
  - Yeni overload `Allowed(kind, code, expires, scopes, source, resolvedAt)` — alanlar dogru atanir.
  - `EffectiveScopes` null gelirse empty'e cevirir.
  - Mevcut positional `new EntitlementCheckResult(true, kind, code, null, expires, true)` cagrisi derlenebilir (compile-time gate).
  - In-memory cache yazma/okuma testi: cache'lenmis result yeni alanlari koruyarak okunur (cache deserialization gerektirmez; reference esitligi).
- **`NoOpDataScopeResolverTests`**
  - `ResolveAsync` her `(tenantId, userId, moduleCode, featureCode)` kombinasyonunda `Array.Empty<EntitlementDataScope>()` doner.
  - Cancellation token cancel state'inde aktif cancellation kontrolu test edilir (best-effort; framework default davranisi).
- **`DataScopeResolverRegistrationTests`**
  - `IServiceCollection.AddApplication()` sonrasi `using var scope = sp.CreateScope(); scope.ServiceProvider.GetRequiredService<IDataScopeResolver>()` `NoOpDataScopeResolver` instance'i doner.
  - Ayri scope'lar farkli instance uretir (Scoped lifetime kanitlanir).

### FU10b — EntitlementChecker ResolvedFrom Mapping (1 yeni test sinifi)

- **`EntitlementResolutionSourcePropagationTests`**
  - Module allow path'i `detail.Source = "ManualOverride"` icin `ResolvedFrom = Override` doner.
  - Module allow path'i `detail.Source = "System"` icin `ResolvedFrom = Plan` doner.
  - Module allow path'i `detail.Source = "Addon"`/`"Trial"` icin `ResolvedFrom = Addon`/`Trial`.
  - Plan-only allow (physical row yok) icin `ResolvedFrom = Plan`.
  - Feature allow path'i icin `ResolvedFrom = Plan`.
  - Transient hata icin `ResolvedFrom = Unknown` + `IsCacheable=false`.
  - `ResolvedAtUtc` her sonucta `default(DateTimeOffset)` degil; `<=DateTimeOffset.UtcNow.AddSeconds(2)` aralikta.
  - `platform_admin` bypass FU10b kapsami DISI: test bu durumda EntitlementChecker'in cagrilmadigini varsayar ve `ResolvedFrom = Unknown` kalir notunu acik birakir (FU12 ileri test).
  - Karar (allow/deny) FU10b oncesi/sonrasi birebir ayni — fixture comparison testi.

**Mevcut testler degismez:**
- `TenantModuleAuthorizationHandlerTests` — PASS.
- `TenantFeatureAuthorizationHandlerTests` — PASS.
- `EntitlementAuthorizationPolicyProviderTests` — PASS.
- `EntitlementCheckerFailureSemanticsTests` — PASS (yeni alanlar default'lu set olur).
- `EntitlementCacheInvalidationConsumerTests` — PASS.
- `AuthorizationProbeControllerIntegrationTests` — PASS (HTTP davranis ayni).
- `TemporaryAccessFoundationTests` — PASS.
- `PlatformEntitlementAuditSinkTests` / `PlatformEntitlementAuditSinkIntegrationTests` — PASS.

**Build / Smoke komutlari:**
- `dotnet build services/Diten.Platform.Common/src/Diten.Platform.Common/Diten.Platform.Common.csproj -c Debug`
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests`

**Frontend / Browser smoke:** N/A (UI yok).

**RESX parity:** N/A.

**DataTable verifier:** N/A (`golden_reference: none`).

## 18. Ready-for-dev Checklist

### Step 0 prerequisites (MUST be done before FU10a code work)

- [x] Kullanici bu draft pack'i inceledi, scope'u onayladi (2026-05-21).
- [x] **MOD-0018 §16.2 acceptance criteria revize** MOD-0018 pack body'sine islendi (§16 #2 + §18 checklist + §19 "EntitlementCheckResult Extension Policy"); doc-only revize 2026-05-21 tarihinde uygulandi.
- [x] **B1 (`EntitlementDataScope` backward-compat) lock'landi:** `Value` ve yeni alanlar bagimsiz; constructor remap yapilmaz.
- [x] **FU10a / FU10b two-step split onaylandi** ve §1 + §5 + §16 + §17 + §20 bolumlerine islendi.
- [x] Master-plan §12 Track G-prime alt bolumu eklendi; Track G gating MVF eshigine baglandi.

### FU10a — Pure Contract Extension Code Work

- [x] Status `approved` veya `ready-for-dev` yapildi (2026-05-21).
- [x] Contract-only first step karari onaylandi (2026-05-21).
- [x] `EntitlementDataScopeKind` yeni 8 deger seti final onaylandi (`OrgUnit`, `Department`, `Team`, `Position`, `LegalEntity`, `Region`, `ManagerChain`, `RecordOwner`) (2026-05-21).
- [x] `EntitlementResolutionSource` 8 deger seti final onaylandi (`Plan`, `Override`, `Addon`, `Trial`, `Temporary`, `Permission`, `PlatformAdminBypass`, `Unknown`) (2026-05-21; FU10a kullanmaz, FU10b'de ResolvedFrom mapping'inde kullanilir; FU12'de PlatformAdminBypass set noktasi).
- [x] `EntitlementDataScope` typed payload sema'si onaylandi (`ScopeId`/`ScopeCode`/`IsInclude` + backward-compat bagimsiz `Value` — B1 lock, otomatik remap yok) (2026-05-21).
- [x] `IDataScopeResolver` imzasi onaylandi (`tenantId`, `userId`, `moduleCode`, `featureCode?`, `CancellationToken`) (2026-05-21).
- [x] DI lifetime karari onaylandi (`Scoped` — `using var scope = sp.CreateScope()` smoke pattern'i) (2026-05-21).
- [x] AuthService Roles/Permissions CRUD out-of-scope kalacak (FU13/MOD-AUTH-001) (2026-05-21).
- [x] RBAC Admin UI out-of-scope kalacak (MOD-0018-FU9) (2026-05-21).
- [x] Gercek tenant controller wiring sonraki pilot tenant modulune kalacak (2026-05-21).
- [x] `NoOpDataScopeResolver` default empty donecegi onaylandi (2026-05-21).
- [x] `EntitlementCheckResult` additive-only genisletme + mevcut overload/call-site davranisi korunmasi onaylandi (2026-05-21).
- [x] Mevcut 5 `EntitlementDataScopeKind` numeric ID degerinin korunmasi onaylandi (2026-05-21).

### FU10b — EntitlementChecker ResolvedFrom Mapping Code Work

- [ ] FU10a code PR'i merge oldu (FU10b on-kosulu).
- [ ] `EntitlementChecker.ResolvedFrom` mapping karari onaylandi (`EntitlementSource -> EntitlementResolutionSource` esleme tablosu — bkz §19).
- [ ] FU10b PR'inin yalniz `EntitlementChecker.cs` + 1 yeni test sinifi kapsamasi onaylandi.
- [ ] `platform_admin` bypass `ResolvedFrom` set noktasinin FU12'ye birakildigi onaylandi; FU10b sonrasi bypass durumunda `Unknown` kalmasi acceptance.

### Downstream coordination

- [ ] FU11 / FU12 / FU13 / FU14 / MOD-0040 / MOD-0041 ile yasama sirasi konfirme edildi.
- [ ] MVF eshigi (FU10a + FU12 + minimal MOD-0040) Tenant Users/Roles development gating'i icin onaylandi.

## 19. Implementation Notes

**MOD-0018 §16.2 revize onerisi:**

Mevcut MOD-0018 acceptance criteria #2 (`EntitlementCheckResult` imzasi degismeyecek) FU10'u bloke ediyor. Revize teklifi:

> "**`EntitlementCheckResult` mevcut public yuzeyi korunur. Additive, default-valued, backward-compatible extension'lara izin verilir.** Mevcut `Allowed(EntitlementKind, string, DateTimeOffset?)` ve `Denied(...)` static factory overload'lari kirilmaz. Yeni alanlar ya record positional parameter olarak default ile eklenir ya da `init` accessor'li property olarak gelir; mevcut consumer call site'larin tamami compile eder ve testler degismeden gecer."

Bu revize MOD-0018 pack'inin **§19 Implementation Notes** veya **§16 Acceptance Criteria** bolumune islenmelidir. Revize, MOD-0018 freeze'i bozmaz; sadece "additive-only" agirlikta yeniden yorumlar.

**`EntitlementSource → EntitlementResolutionSource` mapping:**
- `EntitlementSource.System` → `EntitlementResolutionSource.Plan` (core module + plan-included)
- `EntitlementSource.ManualOverride` (enabled) → `EntitlementResolutionSource.Override`
- `EntitlementSource.Addon` → `EntitlementResolutionSource.Addon`
- `EntitlementSource.Trial` → `EntitlementResolutionSource.Trial`
- Plan-source allow (physical row yok) → `EntitlementResolutionSource.Plan`
- Platform admin bypass → `EntitlementResolutionSource.PlatformAdminBypass` (handler tarafinda set edilir; bu pack handler'a dokunmaz, FU12'de set noktasi gelir — bu pack'te `EntitlementChecker` tarafinda set edilmez, default `Unknown` doner ve FU12'de override edilir)
- Transient hata → `EntitlementResolutionSource.Unknown`

**Roadmap bagi:**
- Bu pack `docs/platform/master-plan.md` §12'ye **Track G-prime — Authorization Foundation Extension** alt bolumu eklenecek planinin **birinci** adimidir.
- Sonraki adimlar: MOD-0018-FU11 (`ITemporaryAccessProvider` pipeline) ∥ MOD-0018-FU12 (`ITenantAuthorizationContext`) → MOD-0018-FU13 → MOD-0018-FU14 → MOD-0040 → MOD-0041 → MOD-AUTH-001.
- **MVF eshigi (kullanici onayli):** Tenant Users/Roles development ancak FU10 + FU12 + minimal MOD-0040 tamamlandiginda baslar.

**Risk notlari:**
- FU10a runtime davranisi degismediginden cok dusuk risklidir.
- FU10b en riskli yer: `EntitlementChecker`'da `ResolvedFrom` doldurma; yanlis mapping cache'lenir. Bu nedenle `EntitlementResolutionSourcePropagationTests` kapsami onemli. FU10b ayri PR olarak izole edildi.
- `EntitlementCheckResult` record positional parameter eklemesi C# record imzasini gorunurde degistirir; **mevcut tum `with` ifadeleri ve constructor call'lar derlenmelidir** — FU10a build gate.
- `EntitlementDataScope` yeni alanlari (B1 lock) `Value` ile **bagimsiz** tasinir; constructor body'sinde otomatik remap yapilmaz. Tuketici tarafi yanlisligi cikarmaktan sorumlu; bu nedenle FU11/FU12 pack'leri tuketim konvansiyonunu netlestirmeli.

**Step 0 (doc-only) yapildi mi?**

Bu pack `ready-for-dev` yapilmadan onceki Step 0 doc revize'leri 2026-05-21 tarihinde tamamlandi:
1. MOD-0018 pack body §16 #2 + §18 + §19 — done.
2. MOD-0018-FU10 pack revize (B1 lock + Two-Step split + AC duzeltmeleri) — done.
3. master-plan §12 Track G-prime + Track G gating — done.

Step 0 sonrasi `module-pack-author` revize gerekmedi; pack body bu revize ile `ready-for-dev` gate'i icin uygun durumda. Status hala `draft`; kullanici onayi (Ready-for-dev checklist FU10a bolumu) sonrasi `ready-for-dev`'e cekilebilir.

## 20. Follow-up Items

- **MOD-0018-FU10-FU1**: `EntitlementDataScope.Value` deprecated isaretleme kararinin **deemphasize phase**'inde verilmesi (FU10 sonrasi 2-3 pack).
- **MOD-0018-FU11**: `ITemporaryAccessProvider` authorization pipeline binding — handler'larda `IDataScopeResolver` + temp grant fallback path'i.
- **MOD-0018-FU12**: `ITenantAuthorizationContext` scoped runtime context; `platform_admin` bypass burada `ResolvedFrom = PlatformAdminBypass` set eder.
- **MOD-0018-FU13**: Permission key convention (`module.resource.action[.scope]`) + `UserRoleChangedV1` / `RolePermissionChangedV1` event'leri + `AuthorizationCacheInvalidationConsumer`.
- **MOD-0018-FU14**: `GetEffectiveAccessForUserQuery` explain endpoint + `IEntitlementAuditSink.LogAllowedAsync` additive method.
- **MOD-0040**: Tenant Org Master Data (`OrgUnit`, `Position`, `UserOrgAssignment`); FU10'un yeni enum degerleri burada anlam kazanir. `NEW-MOD-0040` eski planlama alias'i olarak kalir; canonical ID `MOD-0040`tir.
- **MOD-0018-FU15**: Real `IDataScopeResolver` impl; `NoOpDataScopeResolver` korunur (test/dev fallback). `NEW-MOD-0041` deprecated alias'tir ve MOD-0041 ile collision olusturdugu icin kullanilmaz.
- **MOD-AUTH-001 (Track G)**: Tenant Users/Roles/Permissions CRUD with scope-aware Role assignment.
- **master-plan.md §12 Track G-prime ekleme**: dokuman PR'i; FU10 implementation PR'i ile birlikte veya oncesinde.
- **master-plan.md §11.1 satir ekleme**: `MOD-0018 FU10..FU14`, `MOD-0040`, `MOD-0041` foundation tablosuna eklenir.
