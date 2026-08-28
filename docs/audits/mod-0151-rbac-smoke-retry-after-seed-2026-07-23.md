# MOD-0151 — RBAC Smoke Retry After Seed (Platform recovery + AuthService reseed + live claims)

> **Tarih:** 2026-07-25 · **Tür:** Canlı RBAC doğrulama (geliştirme değil) · **Tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
> **User/Role:** bestepullukcu / 97c5 Admin · **Verdict:** **PASS**
> Platform/Auth/CRM healthy · reseed applied · 5 `crm.territory.*` claim token'da · contract+model-list **403 kalktı** ·
> published-values **12/12 set, 73/73 value**. **Kod/DataSeeder/Mongo:** DEĞİŞTİRİLMEDİ.

---

## 1. Preflight

**Files reviewed:** [permission seed report](./mod-0151-territory-permission-catalog-seed-97c5-grant-2026-07-23.md) ·
[RBAC assignment report](./mod-0151-rbac-role-assignment-2026-07-23.md) ·
[FU01 live smoke report](./mod-0151-fu01-live-smoke-2026-07-23.md) ·
[correct-tenant publish report](./mod-0151-territory-reference-correct-tenant-publish-2026-07-23.md) ·
[FU01 implementation report](./mod-0151-fu01-contract-territory-model-node-backend-2026-07-23.md) ·
AuthService `LoginCommandHandler` · `DataSeeder` (grant precedent) · memory `platform-partial-index-ne-crash`.

**Health status before:** İlk ölçümde Platform (5057) = **000 (DOWN)**; kök neden (memory `platform-partial-index-ne-crash`):
Platform, `document_management_controlled_document_registrations` üzerindeki `$ne(field,null)` partial index'inde
başlangıçta çöküyordu → login "Login is temporarily unavailable" (401 red herring). **Fix zaten kaynağa uygulanmış**
(`MongoDbIndexConfigurations.cs` satır 186/200/1322/1399 → `Filter.Type(BsonType.Binary)`), bu task kod değiştirmedi.

**No-code-change confirmation:** Bu task hiçbir runtime kod, DataSeeder, MOD-0151/MOD-0048 kodu/data, reference set/value,
gateway, UI, registry, Mongo'ya dokunmadı. Yalnız health probe + login/endpoint/published-values smoke + bu rapor.

**Target:** tenant 97c59330 · user bestepullukcu · role 97c5 Admin · beklenen 5 `crm.territory.*` (read/model.read/
model.manage/node.read/node.manage).

---

## 2. Fleet / Reseed Summary

| Service/Step | Before | After | Result | Notes |
|---|---|---|---|---|
| Gateway (5000) | 200 | 200 | ✅ | — |
| AuthService (5056) | 200 | 200 | ✅ | reseed uygulandı (aşağıda claim kanıtı) |
| Platform (5057) | **000 (DOWN)** | **200** | ✅ | fixli kaynakla (zaten mevcut $type fix'i) yeniden başlayıp bağlandı; PID 30232 listener. **Manuel restart/kod fix yapılmadı** — servis toparlanmış durumda bulundu |
| CrmService (5061) | 200 | 200 | ✅ | — |
| AuthService reseed | pending | **applied** | ✅ | `SeedAsync` çalışmış; catalog + 97c5 grant canlı (token claim'leri ile doğrulandı) |

---

## 3. Permission Verification

| PermissionKey | Catalog | RoleGrant (97c5 Admin) | TokenClaim | Result |
|---|---|---|---|---|
| crm.territory.read | ✅ | ✅ | **PRESENT** | ✅ |
| crm.territory.model.read | ✅ | ✅ | **PRESENT** | ✅ |
| crm.territory.model.manage | ✅ | ✅ | **PRESENT** | ✅ |
| crm.territory.node.read | ✅ | ✅ | **PRESENT** | ✅ |
| crm.territory.node.manage | ✅ | ✅ | **PRESENT** | ✅ |

**Login:** HTTP **200**; `tenant_id` claim = `97c59330-dbc4-4665-b29c-0c26dbb5cc93` (doğru). Payload TenantId gönderilmedi.
**Forbidden keys** (micro-zone.manage, territory.delete, assign-rep, assign-account, assignment/resource/approval/evidence):
token'da **NONE** (temiz).

---

## 4. Endpoint Permission Smoke

| Endpoint | Expected | Actual | Result | Notes |
|---|---|---|---|---|
| `GET /api/crm/territory-management/contract` | 200 (403/401 yok) | **200** | ✅ | moduleId=MOD-0151, runtimeScope=FU01-territory-model-node-backend-only, tenantId=97c59330, **isReady=True**, missingRequiredReferenceSets **boş** |
| — feature flags | models/nodes true, rest false | models=T, nodes=T, rules/resource/workflow/evidence/import/ui=F | ✅ | FU01 scope doğru |
| `GET /api/crm/territory-models` | 200 (403/401 yok) | **200** | ✅ | total=0 (henüz model yok — beklenen) |

**403 tamamen kalktı** — RBAC seed + grant canlıda çalışıyor.

---

## 5. Published-values Quick Check (full 12-set aggregate)

| SetCode | Expected | Actual | Result |
|---|---:|---:|---|
| territory-level | 6 | 6 | ✅ |
| territory-model-status | 6 | 6 | ✅ |
| territory-node-status | 4 | 4 | ✅ |
| territory-coverage-scope | 7 | 7 | ✅ |
| territory-assignment-status | 4 | 4 | ✅ |
| territory-assignment-source | 4 | 4 | ✅ |
| business-scope-type | 7 | 7 | ✅ |
| territory-resource-role | 11 | 11 | ✅ |
| territory-rule-type | 9 | 9 | ✅ |
| territory-conflict-policy | 4 | 4 | ✅ |
| planning-period-type (opt) | 4 | 4 | ✅ |
| territory-change-type (opt) | 7 | 7 | ✅ |
| **TOTAL** | **73** | **73** | ✅ |

Contract `isReady=True` + bu tablo → **PUBLISHED_VALUES_READY tam sertifikalı** (önceki task'ta Platform kesintisiyle
yarım kalan aggregate smoke şimdi tamamlandı: 10 required = 62 + 2 optional = 11 → 73/73).

---

## 6. Evidence Table

| Step | Expected | Actual | Result | Notes |
|---|---|---|---|---|
| Platform health | 200 | 200 | ✅ | fixli kaynak zaten mevcut; servis toparlanmış |
| Login (X-Tenant-Id) | 200 + token | 200 | ✅ | tenant_id=97c59330 |
| Token 5 territory claims | 5/5 PRESENT | 5/5 PRESENT | ✅ | forbidden NONE |
| Contract | 200, isReady=true | 200, isReady=True | ✅ | flags FU01-correct |
| Model list | 200 (no 403) | 200 | ✅ | total=0 |
| Published-values | 73/73 | 73/73 | ✅ | 12/12 set |

---

## 7. Created / Updated Files

| File | Action | Notes |
|---|---|---|
| `docs/audits/mod-0151-rbac-smoke-retry-after-seed-2026-07-23.md` | Created | Bu evidence raporu |

Runtime kod / DataSeeder / MOD-0151 / MOD-0048 / gateway / UI / registry / Mongo: **dokunulmadı**.

---

## 8. Guard Checks

| Check | Result |
|---|---|
| Runtime code changed? | **no** |
| DataSeeder changed? | **no** (bu task) |
| MOD-0151 code changed? | **no** |
| MOD-0048 data changed? | **no** |
| Reference set/value touched? | **no** (yalnız read) |
| Gateway route changed? | **no** |
| UI changed? | **no** |
| Registry changed? | **no** |
| Mongo hand-edit used? | **no** |
| Local/manual DB insert used? | **no** |
| Platform health 200? | **yes** |
| AuthService health 200? | **yes** |
| CrmService health 200? | **yes** |
| Correct tenant? | **yes** (97c59330) |
| X-Tenant-Id used? | **yes** |
| Payload TenantId used? | **no** |
| Login 200? | **yes** |
| Token tenant claim correct? | **yes** |
| 5 permissions in catalog? | **yes** |
| 5 permissions in token/claims? | **yes** |
| 5 permissions granted to 97c5 Admin? | **yes** |
| Forbidden permissions present? | **no** |
| Contract endpoint 403 cleared? | **yes** |
| Model list endpoint 403 cleared? | **yes** |
| Contract isReady true? | **yes** |
| Published-values quick check pass? | **yes (73/73)** |
| Hardcoded fallback introduced? | **no** |

---

## 9. Final Verdict

**PASS.**

Platform/Auth/CRM healthy; AuthService reseed uygulandı; bestepullukcu (97c5 Admin) token'ında 5 `crm.territory.*`
permission'ı **mevcut**, forbidden key yok; `GET /api/crm/territory-management/contract` ve `GET /api/crm/territory-models`
**403 vermiyor (200)**; contract **isReady=True** (missingRequiredReferenceSets boş); published-values **12/12 set,
73/73 value** tam sertifikalı. Hiçbir kod/seed/Mongo değişikliği yapılmadı, hiçbir sınır ihlali yok. MOD-0151 FU01 artık
**tam live-smoke-ready**.

---

## 10. Next Recommended Prompt

1. **MOD-0151 FU01 Live Smoke Retry** — `smoke-mod-0151-fu01-territory.ps1 -Token <crm-token>` (RBAC + published-values +
   Platform hazır) → contract isReady=true + TerritoryModel create/get/list/update + TerritoryNode root/child/microzone
   + 7 negatif validation → tam FU01 canlı PASS beklenir (bu task RBAC/readiness'i çözdü; kalan yalnız create/node akışı).
2. PASS sonrası: **MOD-0151 FU02 Territory Hierarchy UI / Territory Model Viewer** veya **FU03 Assignment Rules + Preview**.
