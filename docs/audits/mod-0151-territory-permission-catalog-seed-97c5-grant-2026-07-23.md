# MOD-0151 — Territory Permission Catalog Seed + 97c5 Grant

> **Tarih:** 2026-07-24 · **Tür:** RBAC seed (permission catalog + tenant-97c5 Admin grant)
> **Tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93` · **Role:** 97c5 Admin · **Smoke user:** bestepullukcu
> **Verdict:** **PARTIAL — seed pass, live smoke blocked by Platform (5057) health.**
> Seed catalog + 97c5 grant + tests **PASS (475/475)**; canlı login/claim/endpoint smoke Platform kesintisi nedeniyle bloklu.

---

## 1. Preflight

**Files reviewed:** [RBAC assignment report](./mod-0151-rbac-role-assignment-2026-07-23.md) ·
[FU01 live smoke report](./mod-0151-fu01-live-smoke-2026-07-23.md) ·
[correct-tenant publish report](./mod-0151-territory-reference-correct-tenant-publish-2026-07-23.md) ·
[FU01 implementation report](./mod-0151-fu01-contract-territory-model-node-backend-2026-07-23.md) ·
MOD-0151 pack §17 · AuthService `DataSeeder.cs` (catalog + `SeedTenant97c5CrmContactGrantAsync` precedent) ·
`Permission.cs` (Key/Scope üretimi) · `DefaultRolePermissionTemplate.cs` (`ClassifyScope`, `AdminModules`) ·
`RolesController`/`PermissionsController` · `DocumentManagementPermissionSeedTests.cs` (test precedent).

**Existing blocker:** RBAC assignment raporu — `crm.territory.*` key'leri AuthService permission catalog'unda yoktu;
RBAC assign API cataloged `permissionId` ister; permission oluşturmanın tek yolu **seed**. Bu task **seed dosyasını
değiştirmeye açıkça yetkilendirildi**.

**Seed scope confirmation:** Yalnız (a) 5 FU01 model/node key'ini catalog'a ekleme, (b) tenant-97c5 Admin role'una
idempotent grant, (c) seed testleri. Reference publish / Mongo hand-edit / runtime kod / gateway / UI / registry —
**yapılmadı**.

**No-runtime-code confirmation:** MOD-0151 / MOD-0048 runtime kodu, TerritoryModel/Node, controller, migration, UI,
gateway, registry — **dokunulmadı**. Değişiklik yalnız AuthService seed katmanında.

**Key/Scope doğrulaması (koddan):** `Permission` ctor → `Key = "{module}.{resource}.{action}".ToLowerInvariant()`;
`Scope = ClassifyScope(moduleOverride)`. `ClassifyScope`: PlatformAdmin iff module ∈ {platform, reference-data}, else
**Tenant**. `crm-territory` → **Tenant scope** (crm-account/crm-contact precedent'iyle aynı). `crm-territory`
`AdminModules`'a **eklenmedi** → sadece 97c5'e explicit grant (başka tenant'lara yayılmaz).

---

## 2. Seed Changes Summary

| Area | Change | Notes |
|---|---|---|
| Permission catalog (`SeedPermissionsAsync`) | +5 `new("crm","territory[...]","...", moduleOverride:"crm-territory")` | account-relationship bloğundan sonra; Tenant scope |
| Grant method | +`SeedTenant97c5CrmTerritoryGrantAsync` | crm.contact grant precedent'i; **explicit 5-key allowlist**; idempotent (varsa skip); no Mongo hand-edit |
| Wiring (`SeedAsync`) | +`await SeedTenant97c5CrmTerritoryGrantAsync(database);` | contact grant çağrısından sonra |
| Tests | +`TerritoryPermissionSeedTests` (21 case) | catalog literal + forbidden absence + grant wiring |
| AdminModules | **değiştirilmedi** | crm-territory eklenmedi → 97c5 dışına grant yok |

---

## 3. Permission Catalog Summary

| PermissionKey | Before | After | Result |
|---|---|---|---|
| crm.territory.read | NOT IN CATALOG | **IN CATALOG** (`new("crm","territory","read",…)`) | ✅ |
| crm.territory.model.read | NOT IN CATALOG | **IN CATALOG** (`new("crm","territory.model","read",…)`) | ✅ |
| crm.territory.model.manage | NOT IN CATALOG | **IN CATALOG** (`new("crm","territory.model","manage",…)`) | ✅ |
| crm.territory.node.read | NOT IN CATALOG | **IN CATALOG** (`new("crm","territory.node","read",…)`) | ✅ |
| crm.territory.node.manage | NOT IN CATALOG | **IN CATALOG** (`new("crm","territory.node","manage",…)`) | ✅ |

**Forbidden/later-FU (eklenmedi, testle doğrulandı):** `crm.micro-zone.manage`, `crm.territory.delete`,
`crm.territory.assign-rep`, `crm.territory.assign-account`, `crm.territory.assignment.manage`,
`crm.territory.resource.manage`, `crm.territory.approval.submit`, `crm.territory.evidence.export` — **hiçbiri yok**.

> **Not:** Catalog kaydı seed CODE düzeyinde eklendi ve testlerle doğrulandı. Canlı DB'ye yansıması, AuthService'in
> yeniden başlayıp `SeedAsync`'i çalıştırmasıyla olur (fleet `dotnet watch` DataSeeder değişikliğini algılar → rebuild
> → restart → seed). Canlı doğrulama Platform down olduğu için (login bloklu) şu an yapılamadı.

---

## 4. 97c5 Admin Grant Summary

| Role | PermissionKey | Before | After | Result |
|---|---|---|---|---|
| 97c5 Admin | crm.territory.read | not granted | **granted (seed)** | ✅ code |
| 97c5 Admin | crm.territory.model.read | not granted | **granted (seed)** | ✅ code |
| 97c5 Admin | crm.territory.model.manage | not granted | **granted (seed)** | ✅ code |
| 97c5 Admin | crm.territory.node.read | not granted | **granted (seed)** | ✅ code |
| 97c5 Admin | crm.territory.node.manage | not granted | **granted (seed)** | ✅ code |

Grant idempotent: her key için `rolePermissions` içinde `(TenantId=97c5, RoleId=Admin, PermissionId, !IsDeleted)`
varlığı kontrol edilir; yoksa `RolePermission.SystemGrant(...)` eklenir. İkinci seed çalışmasında duplicate oluşmaz.
`bestepullukcu`'nun 97c5 Admin role'una user-role binding'i **mevcuttur** (önceki crm.contact grant + login bunun
üzerinden çalışıyordu) → yeni claim'ler bir sonraki login'de token'a girer.

---

## 5. Smoke Results

| Smoke | Expected | Actual | Result | Notes |
|---|---|---|---|---|
| Gateway health | 200 | 200 | ✅ | — |
| AuthService health | 200 | 200 | ✅ | — |
| Platform health | 200 | **000 (DOWN)** | ⛔ | fleet Platform servisi düşük |
| CrmService health | 200 | 200 | ✅ | — |
| Login (X-Tenant-Id 97c59330) | 200 + token | **401 "Login is temporarily unavailable"** | ⛔ BLOCKED | `LoginCommandHandler`: tenant login settings Platform'dan okunur; Platform yok → 401 |
| Token claim `crm.territory.*` (5) | claim'lerde var | — | ⛔ BLOCKED | token alınamadı |
| `GET /api/crm/territory-management/contract` | 200 (403 değil) | — | ⛔ BLOCKED | token yok + Platform (readiness) gerekir |
| `GET /api/crm/territory-models` | 200 (403 değil) | — | ⛔ BLOCKED | token yok |

Canlı 403→200 doğrulaması **Platform ayağa kalkıp AuthService reseed olduktan sonra** yapılmalı (blocker seed değil,
altyapı).

---

## 6. Tests

| Test Suite | Result | Notes |
|---|---|---|
| `TerritoryPermissionSeedTests` (21) | ✅ **21/21 PASS** | 5 catalog literal + crm-territory moduleOverride + 8 forbidden absence + 7 grant-wiring |
| AuthService `Authorization` namespace | ✅ **243/243 PASS** | doc-management vb. dahil; regresyon yok |
| AuthService full suite | ✅ **475/475 PASS** | tüm proje; regresyon yok |

> **Build notu:** fleet AuthService 5056'yı çalışır tuttuğu için bin kilitli; testler **repo-içi** geçici output'a
> (`-p:BaseOutputPath=./.authtest-out/`) derlenip koşuldu (kaynak-okuyan seed testleri repo path'i gerektirir), sonra
> tüm `.authtest-out` dizinleri silindi. Fleet'e dokunulmadı; kaynak ağacına artefakt bırakılmadı.

---

## 7. Created / Updated Files

| File | Action | Notes |
|---|---|---|
| `services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs` | **Updated (additive)** | +5 catalog key + `SeedTenant97c5CrmTerritoryGrantAsync` + SeedAsync wiring; başka aktif akışların değişiklikleri korundu (hunk bazlı, sadece ekleme) |
| `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Authorization/TerritoryPermissionSeedTests.cs` | **Created** | 21 seed testi |
| `docs/audits/mod-0151-territory-permission-catalog-seed-97c5-grant-2026-07-23.md` | **Created** | Bu evidence raporu |

Runtime kod / MOD-0151 / MOD-0048 / gateway / UI / registry / Mongo: **dokunulmadı**.

---

## 8. Guard Checks

| Check | Result |
|---|---|
| Runtime code touched? | **no** |
| MOD-0151 code touched? | **no** |
| MOD-0048 code/data touched? | **no** |
| Gateway touched? | **no** |
| UI touched? | **no** |
| Registry touched? | **no** |
| Permission seed touched? | **yes — bu task'ta yetkili (additive)** |
| Mongo hand-edit used? | **no** |
| Local/manual DB insert used? | **no** (grant seed mekanizması üzerinden) |
| Correct tenant? | **yes** (97c59330) |
| X-Tenant-Id used? | **yes** (login denemesi) |
| Payload TenantId used? | **no** |
| 5 required permissions in catalog? | **yes** (seed + test) |
| 5 required permissions granted to 97c5 Admin? | **yes** (seed code + test); canlı DB reseed sonrası aktif |
| Duplicate permission created? | **no** (idempotent) |
| Duplicate grant created? | **no** (existence check) |
| Extra territory permissions added? | **no** |
| `crm.micro-zone.manage` introduced? | **no** |
| `crm.territory.delete` introduced? | **no** |
| `crm.territory.assign-rep` introduced? | **no** |
| `crm.territory.assign-account` introduced? | **no** |
| assignment/resource/approval/evidence permissions added? | **no** |
| bestepullukcu token has permissions? | **unverified** (Platform down → login 401) |
| contract endpoint 403 cleared? | **unverified** (blocked) |
| model list endpoint 403 cleared? | **unverified** (blocked) |
| Platform health? | **DOWN (000)** |
| Tests passed? | **yes (475/475)** |

---

## 9. Final Verdict

**PARTIAL — seed pass, live smoke blocked by Platform health.**

MOD-0151 FU01'in 5 `crm.territory.*` model/node permission'ı AuthService permission catalog'una eklendi ve tenant-97c5
Admin role'una idempotent grant edildi (MOD-0149/0150 precedent'iyle birebir; explicit allowlist; forbidden/later-FU
key'ler eklenmedi). Seed + grant + 21 yeni test **475/475 PASS**, regresyon yok, hiçbir sınır ihlali/güvensiz workaround
yok. Canlı doğrulama (login → JWT crm.territory.* claim → contract/model 403→200) **Platform (5057) kesintisi**
nedeniyle yapılamadı (login "temporarily unavailable" 401 döndü). Bu bir seed kusuru değil, altyapı durumu; ayrıca
seed'in canlı DB'ye yansıması için AuthService'in reseed olması (watch restart) gerekir.

---

## 10. Next Recommended Prompt

1. **Fleet Platform (5057) restart + AuthService reseed doğrulama** — fleet'i (özellikle Platform servisini) yeniden
   çalıştır; AuthService reseed olunca (SeedAsync) catalog + 97c5 grant canlıya yansır.
2. **MOD-0151 RBAC Smoke Retry (login/claim)** — Platform ayağa kalkınca `X-Tenant-Id: 97c59330…` ile bestepullukcu
   login → JWT'de 5 `crm.territory.*` claim'i doğrula; `GET /api/crm/territory-management/contract` ve
   `GET /api/crm/territory-models` artık **403 vermemeli**.
3. **MOD-0151 FU01 Live Smoke Retry** — `smoke-mod-0151-fu01-territory.ps1 -Token <crm-token>` (RBAC + Platform hazır)
   → contract isReady=true + model/node pozitif + 7 negatif PASS; ayrıca `smoke-mod-0151-territory-publishedvalues.ps1`
   ile 12/73 published-values tam sertifikası.
