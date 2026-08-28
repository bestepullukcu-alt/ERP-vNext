# MOD-0151 — RBAC Role Assignment (Territory Smoke Permissions)

> **Tarih:** 2026-07-24 · **Tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
> **Verdict:** **PARTIAL — RBAC assignment blocked by missing prerequisite** (crm.territory.* keys AuthService
> permission catalog'unda **yok**; eklenmesi seed değişikliği gerektirir → bu task'ta yasak). Ayrıca live ortam şu an
> Platform (5057) kesintisi nedeniyle authenticate edilemiyor.
> **Runtime/seed/Mongo:** DEĞİŞTİRİLMEDİ · **Assignment:** YAPILAMADI (ön koşul eksik)

---

## 1. Preflight

**Files reviewed:** [FU01 live smoke report](./mod-0151-fu01-live-smoke-2026-07-23.md) ·
[correct-tenant publish report](./mod-0151-territory-reference-correct-tenant-publish-2026-07-23.md) ·
[FU01 implementation report](./mod-0151-fu01-contract-territory-model-node-backend-2026-07-23.md) ·
[MOD-0151 pack §17](../../execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md) ·
AuthService `RolesController` · `PermissionsController` · `DataSeeder.cs` · `DefaultRolePermissionTemplate.cs` ·
`LoginCommandHandler` · `TenantResolutionMiddleware`.

**Tenant confirmation:** Hedef `97c59330-dbc4-4665-b29c-0c26dbb5cc93`. Login/CRM tenant'ı **`X-Tenant-Id` header'ından**
çözülür (JWT'den değil); assignment/smoke çağrılarında header şart, payload TenantId **yok**.

**No-code-change confirmation:** Hiçbir runtime/MOD-0151/MOD-0048 kod, permission seed dosyası, gateway, UI, registry,
reference set/value'ya dokunulmadı; Mongo hand-edit yapılmadı. Yalnız bu evidence raporu oluşturuldu.

**Service health (bu task sırasında):** gateway(5000)=**200** · authsvc(5056)=**200** · platform(5057)=**000 (DOWN)** ·
crm(5061)=**200**. Platform önceki task'tan beri düşük ve toparlanmadı.

---

## 2. Kök Bulgu — neden assignment yapılamıyor

### 2.1 RBAC mekanizması (koddan doğrulandı)
- Permission atama endpoint'i: **`POST /api/roles/{roleId}/permissions`** (`RolesController.cs:73`,
  `[HasPermission("auth.roles.assign-permission")]`) → gövde: `{ "permissionId": "<GUID>" }`
  (`AssignPermissionCommand(roleId, permissionId)`). **permissionId, catalog'da mevcut bir iznin GUID'idir.**
- Permission **oluşturma API'si YOK**: `PermissionsController` yalnız GET (`/api/permissions`,
  `/by-module/{module}`, `/tenant-assignable`). Yani yeni bir permission **yalnızca seed (DataSeeder) ile** catalog'a girer.

### 2.2 crm.territory.* catalog'da YOK
- Permission catalog `DataSeeder.cs`'te tanımlı; ör. MOD-0149:
  `new("crm","account","read","Read Account",…, moduleOverride:"crm-account")`, MOD-0150: `new("crm","contact",…)`.
- **`crm.territory` için catalog'da hiçbir kayıt yok** (grep `crm\.territory` → AuthService kaynaklarında **0 eşleşme**).
- Sonuç: 5 territory permission'ının **permissionId'si yok** → `POST /api/roles/{id}/permissions` ile **atanamaz**.
- Bu, FU01 report + pack §17 ile tutarlı: pack "hiçbir permission SEED ETMEZ"; FU01 permission'ları **definition-only**
  (CrmService `TerritoryPermissions` sabitleri) bıraktı; catalog'a ekleme bilinçli bir follow-up'tı.

### 2.3 Precedent (MOD-0149/0150) — assignment nasıl yapılmıştı
`DataSeeder.cs`'te `SeedTenant97c5CrmContactGrantAsync` (satır ~856) crm.contact.* / crm.account-*.* key'lerini
**seed zamanında** 97c5 Admin role'una idempotent olarak grant ediyor ("no Mongo hand-edit"). Yani doğru mekanizma
**seed dosyası** üzerinden. Bu task o dosyayı değiştiremez → precedent yol bu task'ta kapalı.

### 2.4 Live doğrulama neden yapılamadı
Platform (5057) düşük olduğundan `tenant-auth/login` **401** döndü (`LoginCommandHandler`, tenant login settings
Platform'dan okunur; Platform yoksa "Login is temporarily unavailable"). Token alınamadı → catalog/role/assignment/smoke
canlı doğrulanamadı. Statik bulgu (2.2) yine de kesindir.

---

## 3. RBAC Assignment Summary

| PermissionKey | Before | After | Action | Notes |
|---|---|---|---|---|
| crm.territory.read | **NOT IN CATALOG** | NOT IN CATALOG | **BLOCKED** | catalog'da permissionId yok → API ile atanamaz |
| crm.territory.model.read | NOT IN CATALOG | NOT IN CATALOG | **BLOCKED** | — |
| crm.territory.model.manage | NOT IN CATALOG | NOT IN CATALOG | **BLOCKED** | — |
| crm.territory.node.read | NOT IN CATALOG | NOT IN CATALOG | **BLOCKED** | — |
| crm.territory.node.manage | NOT IN CATALOG | NOT IN CATALOG | **BLOCKED** | — |

Fazladan/yasak key: **hiçbiri eklenmedi** (`crm.micro-zone.manage`, `crm.territory.delete`, assign-rep/assign-account,
assignment/resource/approval/evidence — **yok**).

---

## 4. User / Role Summary

| User | Role | Tenant | Status | Notes |
|---|---|---|---|---|
| bestepullukcu@gmail.com | 97c5 Admin (muhtemel) | 97c59330 | **Token alınamadı (Platform down)** | Live smoke için hedef kullanıcı; reference-data perm'leri tam, ancak crm.territory.* claim'i yok (catalog boş) |

Hedef role/user **belirlendi** (97c5 Admin / bestepullukcu, MOD-0149/0150 precedent'iyle aynı), ancak izinler catalog'da
olmadığı için atama yapılamadı.

---

## 5. Permission Smoke

| Endpoint | Expected | Actual | Result | Notes |
|---|---|---|---|---|
| `POST /api/tenant-auth/login` (X-Tenant-Id) | 200 + token | **401** | BLOCKED | Platform down → tenant login settings okunamıyor |
| `GET /api/permissions` (catalog) | crm.territory.* listede | — | BLOCKED | token yok; statik: catalog'da yok |
| `GET /api/crm/territory-management/contract` | 200 (403 değil) | — | BLOCKED | token + published-values (Platform) gerekir |
| `GET /api/crm/territory-models` | 200 (403 değil) | — | BLOCKED | token gerekir; RBAC atanmadığından 403 beklenir |

---

## 6. Created / Updated Files

| File | Action | Notes |
|---|---|---|
| `docs/audits/mod-0151-rbac-role-assignment-2026-07-23.md` | Created | Bu evidence raporu |

Runtime kod / permission seed / DataSeeder / Mongo / gateway / UI / registry: **dokunulmadı**.

---

## 7. Guard Checks

| Check | Result |
|---|---|
| Runtime code touched? | **no** |
| MOD-0151 code touched? | **no** |
| MOD-0048 data touched? | **no** |
| Reference set/value touched? | **no** |
| Gateway touched? | **no** |
| UI touched? | **no** |
| Registry touched? | **no** |
| Permission seed file touched? | **no** |
| Mongo hand-edit used? | **no** |
| Local seed used? | **no** |
| Correct tenant? | **yes** (97c59330) |
| X-Tenant-Id used? | **yes** (login denemesi) |
| Payload TenantId used? | **no** |
| Token tenant claim correct? | **unverified** (Platform down → login 401) |
| Target role identified? | **yes** (97c5 Admin) |
| Target user identified? | **yes** (bestepullukcu) |
| 5 required permissions assigned? | **no** (catalog'da yok → atanamaz) |
| Extra territory permissions assigned? | **no** |
| `crm.micro-zone.manage` introduced? | **no** |
| `crm.territory.delete` introduced? | **no** |
| Assignment/resource/approval/evidence permissions assigned? | **no** |
| Contract endpoint 403 cleared? | **no** (blocked) |
| Model list endpoint 403 cleared? | **no** (blocked) |
| Published-values dependency changed? | **no** |
| Hardcoded fallback introduced? | **no** |

---

## 8. Final Verdict

**PARTIAL — RBAC assignment blocked by missing prerequisite.**

5 `crm.territory.*` permission'ı AuthService permission catalog'unda **tanımlı değildir** (FU01 seed etmedi; pack §17
gereği). RBAC assign API'si cataloged bir `permissionId` gerektirir ve permission oluşturmanın tek yolu **seed
(DataSeeder)**'dir — bu task ise permission seed dosyasını değiştirmeyi ve Mongo hand-edit'i **yasaklar**. Dolayısıyla
atama bu task'ın yetkisi dışındaki bir ön koşula (catalog seed) bağlıdır. Ek olarak, live ortam şu an Platform (5057)
kesintisi nedeniyle authenticate edilemiyor. Hiçbir güvensiz workaround uygulanmadı; hiçbir sınır ihlal edilmedi.

---

## 9. Next Recommended Prompt

1. **MOD-0151 Territory Permission Catalog Seed + 97c5 Grant** (asıl blocker'ı çözer, MOD-0149/0150 precedent'iyle):
   `services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs` içine
   (a) 5 key'i catalog listesine ekle: `new("crm","territory","read",…, moduleOverride:"crm-territory")`,
   `crm.territory.model.read/.model.manage/.node.read/.node.manage` (PKS-001 nested; `moduleOverride:"crm-territory"`,
   Scope=Tenant), (b) `SeedTenant97c5CrmTerritoryGrantAsync` ekle — `crm.territory.*` key'lerini 97c5 Admin role'una
   idempotent grant (Mongo hand-edit yok), (c) exact-literal seed testleri
   (`services/Diten.AuthService/tests/**/Authorization/*DocumentManagement*` deseni). Reseed/restart sonrası
   bestepullukcu yeniden login → JWT'de crm.territory.* claim'leri gelir.
   > Not: Bu bir **seed task'ıdır**; mevcut task'ın "permission seed değiştirme" yasağı nedeniyle ayrı yetkilendirme ister.
2. **Fleet Platform (5057) restart** — live login/smoke için Platform ayağa kaldırılmalı (fleet `watch-diten-bg.ps1`
   Platform servisi düşük; kullanıcı yeniden başlatmalı).
3. Yukarıdakiler sonrası: **MOD-0151 FU01 Live Smoke Retry** (`smoke-mod-0151-fu01-territory.ps1 -Token <crm-token>`)
   ve **published-values tam sertifika** (`smoke-mod-0151-territory-publishedvalues.ps1`).
