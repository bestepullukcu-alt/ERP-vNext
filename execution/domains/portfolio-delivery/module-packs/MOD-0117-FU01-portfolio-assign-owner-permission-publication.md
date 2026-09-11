---
id: MOD-0117-FU01
name: Portfolio Assign-Owner Permission Publication
domain: portfolio-delivery
service: Diten.PpmService
shell: none
golden_reference: none
entity_base: EntityBase
status: approved
owner: Portfolio Governance Process Owner / infrastructure control tower
branch: codex/ppm-portfolio-first-delivery
started: 2026-09-11
target: 2026-09-11
form_field_count: 0
parent: MOD-0117
production_authority: none
---

# MOD-0117-FU01 — Portfolio Assign-Owner Permission Publication

> **APPROVED / NON-EXECUTABLE.** This is a narrow follow-up under MOD-0117, not a product module,
> UI/CRUD slice, or implementation approval. The parent remains `review`; this FU does not make it
> `approved` or `ready-for-dev`, and `production_authority: none` remains in force.

## 1. Module Summary

Publish exactly one canonical permission, `ppm.portfolios.assign-owner`, so the existing PPM consumer
can be reconciled with Platform manifest discovery and the Auth canonical PPM list. It applies only to
Portfolio **Assign** and **Transfer**. It neither grants read, metadata edit, assessment nor general
record access, and creates no assign-owner permission for another PPM resource.

Identity preflight passed:

    python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0117-FU01 --name "Portfolio Assign-Owner Permission Publication" --parent MOD-0117
    OK  MOD-0117-FU01: proven against Blueprint/registry.

The parent [canonical owner record](MOD-0117-project-portfolio-management.md#portfolio-owner-explicit-grant-decision)
records the approved decision: the new key is never automatically granted, including to SuperAdmin;
it requires an authorized person's explicit permission assignment. Existing grants are not changed.

## 2. Ownership and Boundaries

| Work | Owner | Boundary |
|---|---|---|
| Existing `PpmPermissions` literal and PPM consumer checks | PPM | Already materialized in the current Portfolio delivery worktree; preserve and verify it, do not re-implement it. |
| Manifest/catalog publication and canonical-list mirror | Infrastructure CT | Owns Platform/Auth protected paths and their tests. PPM has no write authority there. |
| BL-359 automatic-grant exclusion | Infrastructure CT | Excludes this key from full-catalog/SuperAdmin, module-sync and default role-template grants without changing other permissions. |
| BL-360 `PPM`/`ppm` case consistency | Infrastructure CT | Keeps exact canonical module acceptance; no resolver fail-open or broadened normalization. |
| Named-human eligibility and SOP-0029 record access | Policy/identity owners; PPM consumer | Not permission publication; remain fail-closed prerequisites for actual owner use. |

## 3. Owned Objects

No entity, collection, endpoint, UI page, migration, seed, generic access engine, or new PPM resource
is owned by this FU. Its sole owned governance object is the literal-to-action publication contract.

## 4. Entity Fields

Not applicable: this FU creates no entity or persisted field. `entity_base: EntityBase` records the
parent PPM convention only; it does not authorize an EntityBase subtype.

## 5. Repo Scope

### PPM exact paths — existing consumer evidence, not new FU implementation

| Exact path | Current evidence / permitted FU treatment |
|---|---|
| `services/Diten.PpmService/src/Diten.PpmService.Application/Common/PpmPermissions.cs` | Current worktree already declares `PortfoliosAssignOwner = "ppm.portfolios.assign-owner"`; preserve exact literal. No further PPM source delta is planned by this FU. |
| `services/Diten.PpmService/tests/Diten.PpmService.Tests/ApplicationContractTests.cs` | Current worktree already asserts the closed PPM permission set including the literal; retain as consumer-parity evidence. |

### CT tarafından kesinleştirilecek aday temaslar — PPM implementation allowlist'i dışında

| Mevcut koddan doğrulanan aday yol | CT'nin değerlendireceği dar amaç |
|---|---|
| `services/Diten.Platform/src/Diten.Platform.Application/Features/Ppm/SelfRegistration/PpmManifestProvider.cs` | Add one `PORTFOLIOS` `ASSIGN_OWNER` action with the exact literal; no other PPM page/action change. |
| `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Ppm/PpmManifestProviderTests.cs` | Assert 24→25 canonical permissions, only Portfolio actions 3→4, and no extra PPM resource action. |
| `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Authorization/PpmPermissionCatalog.cs` | Add the exact literal once to the canonical PPM list. |
| `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Authorization/PpmEntitlementPermissionPolicy.cs` | Catalog-derived canonical list and shared exact `Applies` behavior; no independent literal list is expected. |
| `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Authorization/TenantEffectivePermissionResolver.cs` | Resolver'ın mevcut canonical module kabulünü korur; shared `Applies` değişirse ayrı davranışını gösterir. |
| `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs` | BL-360: `GrantModuleAsync` ve `GrantModuleWithKeysAsync` için `PPM`/`ppm`/`Ppm` ve normalize-sync girişlerinde PPM toplu grant oluşmasını engeller. |
| `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Auth/TenantEffectivePermissionResolverTests.cs` | Role grant ∩ entitlement canonical list, missing/ambiguous entitlement ve resolver canonical kabulünü doğrular. |
| `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs` | BL-360 grant/sync retlerini doğrular; shared `Applies` değişirse resolver testlerinden ayrı tüketici farkını gösterir. |
| `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs` | BL-359: prevent create/reactivate sync from auto-granting this key. |
| `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IFullCatalogPermissionGrantService.cs` | Refine only if needed to express the explicit exclusion without weakening other keys. |
| `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/FullCatalogPermissionGrantService.cs` | Preserve other full-catalog behavior while excluding this literal. |
| `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Permissions/FullCatalogPermissionGrantServiceTests.cs` | Negative proof that this key is not auto-granted; existing permission behavior remains covered. |
| `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Permissions/InternalPermissionsControllerTests.cs` | Create/reactivate-sync negative proof for this key. |
| `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs` | BL-359: default-role template candidate source; bu key için explicit exclusion tasarımı CT'ye aittir. |
| `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/RoleProvisioningService.cs` | Default/startup role şablonunu tüketen aday yol; diğer role davranışını genişletmeden incelenir. |
| `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRolePermissionTemplateTests.cs` | Default/startup template negatif kanıtı, SuperAdmin dahil. |

Bu tablo CT'nin onaylanmış exact write scope'u değildir. Hiçbir yol örtük olarak dahil değildir; ek yol
CT'nin kendi onaylı kapsamını gerektirir. FU PPM'ye Auth/Platform yazma yetkisi vermez veya CT
implementation tasarımını dayatmaz.

## 6. Protected Paths

`services/Diten.AuthService/**`, `services/Diten.Platform/**`, Platform DI, manifest registration,
configuration, secrets, Gateway, frontend, persistence and `.antigravity/**` are protected from PPM
implementation. CT-owned paths above are dependency evidence only. See [AGENTS.md](../../../../AGENTS.md)
and [module self-registration](../../../../.antigravity/rules/module-self-registration-standard.md).

## 7. Dependencies

1. Approved explicit-grant exclusion in the parent pack.
2. **Başlama:** Kullanıcının 2026-09-11 tarihli FU01 izin-yayımı kapsam onayı; buna ek olarak CT'nin
   kendi kesinleştirilmiş write scope'u ve gerekli yetki girdileri.
   Claude'un 2026-09-11 teyidine göre parent `review` kalabilir; own `approved` veya
   `ready-for-dev` FU origin'e push edilmiş SHA ile CT tarafından okunabilir. Bu teyit FU onayı veya
   implementation yetkisi değildir.
3. **Tamamlanma:** CT uygulaması, gerçek test kanıtları ve Platform/Auth katalog uyumu.
4. **Gerçek owner kullanımı:** ayrı named-human/assignable target ve SOP-0029 record-access sözleşmeleri.
   Bunlar permission yayımı başlama veya tamamlanma engeli değildir; browser/production kabulünü engeller.

## 8. Runtime Constraints

Until CT publication is complete, no manifest/catalog record, grant, provisioning, role change, endpoint
or runtime authority is implied. A catalog record does not grant a user access. PPM still requires the
permission gate plus record/operation/target evidence; unavailable or indeterminate dependencies remain
fail-closed. [PKS-001](../../../../.antigravity/rules/permission-key-standard.md),
[SEC-001](../../../../.antigravity/rules/security-jwt.md), [BME-001](../../../../.antigravity/rules/business-module-enforcement-standard.md)
and [RULE-002](../../../../.antigravity/rules/multi-tenancy.md) apply by reference.

## 9. Layout & Shell Contract

Not applicable. `shell: none` and `golden_reference: none` are intentional: this FU creates no Razor,
frontend, DataTable or browser surface.

## 10. Backend File Convention

Not applicable to new PPM folders: no command, handler, validator, controller or entity is created.
CT follows its own protected-service convention in separately authorized work.

## 11. Frontend File Contract

Not applicable. Platform manifest publication is CT-owned discovery metadata, not a browser surface or
user grant.

## 12. Validation Rules

| Rule | Required result |
|---|---|
| Literal | Exactly lowercase `ppm.portfolios.assign-owner` in PPM, Platform and Auth; no alias, wildcard or second spelling. |
| Resource/action | Portfolio `ASSIGN_OWNER` only; covers Assign/Transfer but not generic resource action. |
| Grant path | Explicit assignment only; full-catalog/SuperAdmin, module-sync and default templates exclude the key. |
| Other resources | No assign-owner descriptor or canonical entry for Initiative, Program, Project, Investment Case or Benefit Commitment. |
| Effective permission | Explicit role grant ∩ confirmed entitlement canonical list; absence or uncertainty yields no effective PPM key. |

## 13. Failure Paths to Verify

- Missing role grant + entitlement → no effective permission.
- Role grant + missing, unavailable, wrong-case or ambiguous entitlement → no effective permission.
- Both valid → only the permission gate may pass; PPM record, operation and target eligibility still decide mutation.
- Create/reactivate catalog sync, full-catalog/SuperAdmin and default-template flows → do not grant this key.
- `GrantModuleAsync`/`GrantModuleWithKeysAsync` with `PPM`, `ppm`, `Ppm` or normalized-sync inputs → no PPM bulk grant.
- Resolver canonical module acceptance remains intact. If shared `Applies` changes, both the resolver and
  sync consumer differences are proven by separate tests; no silent authority widening.

## 14. Authorization Convention

The key satisfies PKS-001 Tier-3 naming. Its scope is frozen here: Portfolio Assign/Transfer only. It is
not an alias for `ppm.portfolios.update`, assessment permission or read permission. Permission action
and record visibility remain orthogonal.

## 15. Gateway / API Routing Decision

No Gateway or API route is created. Existing PPM owner assignment consumption is outside this FU’s
publication scope.

## 16. Acceptance Criteria

1. PPM constant, Platform manifest action and Auth canonical list use one exact literal.
2. Platform exposes one additional Portfolio action and no assign-owner action elsewhere.
3. BL-359 negatives prove no automatic grant through full-catalog/SuperAdmin, module sync or default
   templates; unrelated permissions preserve behavior.
4. BL-360 tests prove no case-normalization route silently broadens PPM authority.
5. Effective permission requires explicit role grant and confirmed entitlement; it is not record access,
   operation authorization or target-human eligibility.
6. No actual grant, provisioning, browser acceptance or production activation is claimed.

## 17. Test Expectations

Only CT-owned unit tests named in §5 are expected after separate CT implementation authorization. PPM
tests are consumer-parity evidence only. Their completion evidence is a completion gate, not a
Ready-for-Dev prerequisite. No build, runtime, DB or browser test is authorized by this draft.

## 18. Ready-for-Dev Checklist

- [x] FU01 preflight passed against Blueprint/registry with parent MOD-0117.
- [x] Existing PPM consumer and actual Platform/Auth paths were read-only verified.
- [x] Approved automatic-grant exclusion is linked without treating publication as complete.
- [x] User, FU01'in yalnız `ppm.portfolios.assign-owner` izin-yayımı kapsamını onayladı; gerçek
  grant/provisioning, kimlik/kayıt erişimi politikaları, browser kabulü ve production kapsam dışıdır.
- [ ] CT supplies its own approved write scope.
- [ ] Gerekli yetki girdileri başlangıç kapsamı için doğrulanır.

**Completion gate, Ready-for-Dev değildir:** CT implementation, gerçek test kanıtları ve exact
Platform/Auth katalog uyumu tamamlanır. Named-human/assignable target ile SOP-0029 record access
ayrıca gerçek owner kullanım kapısıdır; permission yayımını tamamlanmış saydırmaz, browser veya
production kabulü de vermez.

## 19. Implementation Notes

The current worktree already contains PPM’s consumer literal and contract expectation. They are prior
Portfolio delivery work, not FU work to repeat. Platform currently has 24 PPM permissions and three
Portfolio actions; Auth derives its PPM canonical list from six resources × four actions. Auth’s existing
full-catalog grant service targets default-tenant SuperAdmin on catalog create/reactivate, making BL-359
a required CT change rather than a hypothetical risk. `PpmEntitlementPermissionPolicy.Applies` is shared
by both resolver and entitlement sync; BL-360 must preserve observable differences with tests if that
shared predicate changes.

## 20. Follow-up Items

- CT may read this `approved` FU after it is pushed to origin, using
  `git show <sha>:execution/domains/portfolio-delivery/module-packs/MOD-0117-FU01-portfolio-assign-owner-permission-publication.md`;
  checkout and parent-pack promotion are not prerequisites for review.
- The PPM governance record reaches main before or with CT’s related Auth/Platform commits.
- This FU travels with the meaningful Portfolio delivery, not a separate small PR.
- Named-human/assignable target proof and SOP-0029 record access block real owner use, not permission
  publication alone.
