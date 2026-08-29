# MOD-0151 — FU01 Contract + Core TerritoryModel/TerritoryNode Backend

> **Tarih:** 2026-07-23 · **Runtime scope:** `FU01-territory-model-node-backend-only`
> **Service:** Diten.CrmService (port 5061, Gateway-only) · **Verdict:** **PASS**
> **Tests:** 45 yeni Territory testi + tüm CrmService suite **214/214 PASS**

---

## 1. Preflight

**Files reviewed:** MOD-0151 pack (`execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md`
§2/§4/§7–§13/§16/§17/§20–§24) · FU00 closeout (`docs/audits/mod-0151-fu00-pack-approval-closeout-2026-07-23.md`) ·
F1 reference authoring template (`.../reference-data/mod-0151-territory-required-reference-authoring-template.json`) ·
MOD-0149/0150 runtime precedent'leri (Account/Contact entity, command/handler/mapper/validator, `AccountReferenceValidation`,
`Response<T>`, `CustomBaseController`, `HasPermissionAttribute`, `AccountController`, repository + Mongo index pattern,
`GatewayReferenceDataValidator` + `IReferenceMetadataReader` + `IReferenceDataCatalogReader`, `AccountFoundationTests`
+ `GatewayReferenceDataValidatorTests`) · Application/Infrastructure/Persistence DI · Program.cs · gateway `ocelot.json`
CRM route pattern.

**Runtime scope confirmation:** Yalnız `Diten.CrmService` FU01 sınırında kod yazıldı: Contract endpoint,
TerritoryModel + TerritoryNode aggregate'leri, repository/query altyapısı, hierarchy + level-rank + tarih validasyonu,
MOD-0048 published-values reference validator (attributes string-metadata parsing), permission **tanımları** (seed yok),
unit/integration testleri, minimal gateway route. Bunun dışında **hiçbir şey** eklenmedi.

**Published-values status:** F10 operator publish henüz beklemede olabilir. FU01 kodu publish olmadan yazıldı; create/update
validation eksik required set durumunda **kontrollü 400** döner (fail-closed). Testler hem published-ready hem
missing-reference senaryolarını kapsar. **Canlı create smoke F10 publish sonrasına bırakıldı.**

**No-out-of-scope confirmation:** assignment rule/apply/preview yok · resource assignment yok · workflow/activation yok ·
change request yok · evidence yok · import/export yok · UI/Razor/JS/resx yok · MOD-0155 readiness API yok · reference
publish/seed yok · Mongo hand-edit yok · hardcoded fallback yok · `crm.micro-zone.manage` / `crm.territory.delete` yok ·
Account/Contact entity'sine territory alanı eklenmedi · `EntitlementDataScopeKind` değiştirilmedi.

---

## 2. Implementation Summary

| Area | Implemented | Notes |
|---|---|---|
| Contract endpoint | ✅ | `GET /api/crm/territory-management/contract`; feature flags + 10-set readiness matrix + `isReady` |
| TerritoryModel aggregate | ✅ | Draft-only lifecycle (FU01); ModelCode tenant-unique; `VersionNumber` business field ≠ concurrency `Version` |
| TerritoryNode aggregate | ✅ | Tek node tipi + `TerritoryLevel`; MicroZone ayrı aggregate değil; `MicroZoneProfile` VO |
| Repository + query altyapısı | ✅ | Mongo repos, tenant-scoped filters, soft-delete aware, cross-tenant → not found |
| Hierarchy validation | ✅ | child rank > parent rank; level-skip serbest; backward blok; cycle guard; date containment |
| Level reference validation | ✅ | `territory-level` value + **rank metadata** fail-closed resolve |
| MOD-0048 published-values validator | ✅ | `ITerritoryReferenceValidator` — mevcut 3 consumer seam'i besler; hardcoded fallback yok |
| Permission definitions (seed değil) | ✅ | `TerritoryPermissions` — 5 anahtar, definition-only |
| Unit/integration tests | ✅ | 45 test (contract/model/node/reference/permission/guard) |
| Gateway route | ✅ | 3 minimal CRM-pattern route; protected path kurallarına uygun; 5061 doğrudan expose edilmedi |

---

## 3. Created / Updated Files

| File | Action | Notes |
|---|---|---|
| `Domain/Entities/TerritoryModel.cs` | Created | Aggregate root (FU01 alanları) |
| `Domain/Entities/TerritoryNode.cs` | Created | Model-scoped node + `MicroZoneProfile?` |
| `Domain/Entities/MicroZoneProfile.cs` | Created | VO (yalnız microzone) |
| `Domain/Repositories/ITerritoryModelRepository.cs` | Created | — |
| `Domain/Repositories/ITerritoryNodeRepository.cs` | Created | cycle-walk dahil |
| `Application/Common/ReferenceValidation/ReferenceMetadata.cs` | Created | string→int/bool metadata parse (fail-closed) |
| `Application/Features/Territory/TerritoryReferenceSets.cs` | Created | 10 required set kodu + readiness descriptor'ları |
| `Application/Features/Territory/TerritoryPermissions.cs` | Created | 5 permission **tanımı** (seed yok) |
| `Application/Features/Territory/ITerritoryReferenceValidator.cs` | Created | issue enum + readiness DTO |
| `Application/Features/Territory/TerritoryReferenceValidator.cs` | Created | seam kompozisyonu; hardcoded fallback yok |
| `Application/Features/Territory/Contract/*` (3) | Created | DTO + query + handler |
| `Application/Features/Territory/Models/*` (DTO, commands, queries, validators, 3 handler) | Created | Model CQRS |
| `Application/Features/Territory/Nodes/*` (DTO, commands, queries, validators, validation helper, 3 handler) | Created | Node CQRS + hierarchy validation |
| `Application/DependencyInjection.cs` | Updated | `ITerritoryReferenceValidator` scoped kaydı |
| `Persistence/Repositories/TerritoryModelRepository.cs` | Created | — |
| `Persistence/Repositories/TerritoryNodeRepository.cs` | Created | — |
| `Persistence/DependencyInjection.cs` | Updated | repo kaydı + Guid-as-string class map + 4 index |
| `Api/Controllers/CRM/TerritoryContractController.cs` | Created | contract route |
| `Api/Controllers/CRM/TerritoryModelsController.cs` | Created | model + node endpoint'leri |
| `gateway/Diten.ApiGateway/ocelot.json` | Updated | 3 territory route (CRM pattern, port 5061) |
| `tests/.../Territory/*` (6 dosya) | Created | 45 test + fakes |

---

## 4. API Summary

| Endpoint | Permission | Status | Notes |
|---|---|---|---|
| `GET /api/crm/territory-management/contract` | `crm.territory.read` | ✅ | readiness matrix; eksik set → 200 + `isReady=false` |
| `GET /api/crm/territory-models` | `crm.territory.model.read` | ✅ | search/status/page filtresi |
| `POST /api/crm/territory-models` | `crm.territory.model.manage` | ✅ | draft; dup code → 409; eksik status set → 400 |
| `GET /api/crm/territory-models/{id}` | `crm.territory.model.read` | ✅ | cross-tenant → 404 |
| `PUT /api/crm/territory-models/{id}` | `crm.territory.model.manage` | ✅ | yalnız draft; non-draft → 409 |
| `GET /api/crm/territory-models/{id}/nodes` | `crm.territory.node.read` | ✅ | hierarchy |
| `POST /api/crm/territory-models/{id}/nodes` | `crm.territory.node.manage` | ✅ | rank/cycle/date/microzone validasyonu |
| `PUT /api/crm/territory-models/{id}/nodes/{nodeId}` | `crm.territory.node.manage` | ✅ | yalnız draft; cycle → 400 |

**Kasıtlı olarak YOK:** delete, activate, submit-approval, approval-trace, evidence-pack, rules, preview-assignments,
account-assignments, resource-assignments, export, import, coverage-rollup (her biri sonraki FU).

---

## 5. Domain / Persistence Summary

- **TerritoryModel** (`territory_models`): `Id` (=TerritoryModelId), `TenantId` (server-side), `ModelCode`,
  `Name`, `CountryScope?`, `DivisionScope?`, `EffectiveFrom/To`, `Status` (draft), `VersionNumber`, `BasedOnModelId?`,
  `ChangeReason?`, `CorrelationId?` + soft-delete/audit metadata. Aktivasyon/supersede/approval **yok**.
- **TerritoryNode** (`territory_nodes`): `Id` (=TerritoryId), `ModelId`, `ParentTerritoryId?`, `TerritoryCode`,
  `Name`, `TerritoryLevel`, level code alanları, `Status` (draft), `EffectiveFrom/To`, `SortOrder`,
  `MicroZoneProfile?`, `CorrelationId?`.
- **Indexes:** `ux_territory_models_tenant_code` (partial, IsDeleted=false), `ix_territory_models_tenant_status`,
  `ux_territory_nodes_tenant_model_code` (partial), `ix_territory_nodes_tenant_model_parent`. Uniqueness ayrıca
  repository/application katmanında da doğrulanır (dup → 409).
- **Tenant isolation:** tüm sorgular `TenantId` + `!IsDeleted` filtreli; cross-tenant erişim not-found (404) döner;
  `TenantId` yalnız `ITenantContext`'ten (JWT) çözülür, payload'da alan yok.
- **Guid-as-string** class map'leri MOD-0149/0150 konvansiyonuyla aynı (subtype uyumsuzluğu / login kırılması riski yok).

---

## 6. Reference Validator Summary

- **MOD-0048 entegrasyonu:** `TerritoryReferenceValidator`, mevcut `IReferenceDataValidator` (single value),
  `IReferenceMetadataReader` (per-value attributes) ve `IReferenceDataCatalogReader` (whole set) seam'lerini besler.
  Hepsi Gateway üzerinden tenant `scope_key` ile okur (`GatewayReferenceDataValidator`). CRM local seed / hardcoded
  liste **yok**.
- **Attributes string-metadata parsing:** `ReferenceMetadata.TryGetInt/TryGetBool` — MOD-0048 attributes'ı
  `Dictionary<string,string>` döndürdüğü için `rank`/`sortOrder` int, `requiresTerritoryId`/`isSalesScopeDefault` vb.
  bool **string'den** parse edilir; native JSON tipi beklenmez.
- **Fail-closed:** eksik set → `SetMissing` · eksik/deprecated value → `InvalidValue` · eksik metadata anahtarı →
  `MetadataMissing` · parse edilemeyen metadata → `MetadataInvalid`. Hiçbir durumda default rank/level üretilmez;
  exception leak olmaz (controlled validation error → 400).
- **Contract readiness:** 10 required set için existence + expected/actual value count + zorunlu metadata coverage
  (`territory-level`, `territory-coverage-scope`, `territory-resource-role`, `business-scope-type`) raporlanır.

---

## 7. Validation Rules Implemented

| Rule | Result |
|---|---|
| Hierarchy cycle yasak | ✅ Block 400 (repo cycle-walk) |
| `TerritoryCode` model içinde unique | ✅ Block 409 |
| Level sequence (child rank > parent; skip serbest, geri yasak) | ✅ Block 400 |
| `ValidFrom <= ValidTo` (model + node) | ✅ Block 400 (FluentValidation) |
| Node tarihleri model aralığında | ✅ Block 400 |
| Child node tarihleri parent aralığında | ✅ Block 400 |
| `MicroZoneProfile` yalnız `level=microzone` | ✅ Block 400 |
| Zorunlu MOD-0048 set eksikken create/update | ✅ Block 400 (fail-closed, no fallback) |
| Invalid level / node-status value | ✅ Block 400 |
| Eksik/parse-edilemeyen level rank metadata | ✅ Block 400 |
| `ModelCode` tenant-unique | ✅ Block 409 |
| Non-draft model mutasyonu (model + node) | ✅ Block 409 |
| Model/node not found | ✅ 404 |
| Parent başka model/tenant'ta | ✅ 404 |
| Cross-tenant erişim | ✅ 404 (metadata sızıntısı yok) |
| `TenantId` payload'dan gelemez | ✅ Command'larda TenantId alanı yok |
| Hard delete | ✅ Endpoint yok (403/409 gerekmez — surface yok) |

---

## 8. Tests

| Test Suite | Result | Notes |
|---|---|---|
| `TerritoryReferenceValidatorTests` (11) | ✅ PASS | metadata string int/bool parse; fail-closed (SetMissing/InvalidValue/MetadataMissing/MetadataInvalid); no-fallback; readiness |
| `TerritoryModelTests` (8) | ✅ PASS | create draft; dup 409; unpublished status 400; TenantId alan yok; date-range validator; update draft; non-draft 409; cross-tenant 404 |
| `TerritoryNodeTests` (16) | ✅ PASS | root/child; level-skip; backward blok; unpublished/invalid level; dup code; cross-model code OK; foreign parent 404; cycle; date-out-of-model/parent; microzone allow/block; non-draft 409 |
| `TerritoryContractTests` (6) | ✅ PASS | module identity/scope; flags model+node only; 10 required set; missing → not ready (no crash); 5 permission |
| `TerritoryScopeGuardTests` (4) | ✅ PASS | 5 permission tam; superseded key yok; no delete endpoint; no out-of-scope route |
| **Territory toplam** | **45/45 PASS** | — |
| **Tüm CrmService suite** | **214/214 PASS** | mevcut 169 test + 45 yeni; regresyon yok (ilk koşuda 1 geçici flake, temiz koşuda yeşil) |

> **Build notu:** dev fleet (`dotnet watch run`) 5061'i çalışır tuttuğu için Api.exe/dll kilitli; testler izole output
> dizinine (`-p:BaseOutputPath`) derlenip koşuldu — fleet'e dokunulmadı, kaynak ağacına build artefaktı sızmadı.

---

## 9. Live Smoke Status

- **PUBLISHED_VALUES_PENDING** (F10 operator publish tamamlanana kadar varsayılan)
- **LIVE_SMOKE_BLOCKED_BY_F10_PUBLISH** — 10 required set (özellikle `territory-level`, `territory-model-status`,
  `territory-node-status`) tenant `97c59330-dbc4-4665-b29c-0c26dbb5cc93` scope'unda publish edilmeden canlı
  create/update kontrollü 400 döner. Bu **doğru fail-closed davranıştır** (MOD-0149/0150 precedent'iyle aynı).
  Publish sonrası contract `isReady=true` olur ve create smoke koşulabilir.

---

## 10. Guard Checks

| Check | Result |
|---|---|
| Runtime changes limited to Diten.CrmService FU01? | **yes** (+ gateway route, + izole test) |
| Assignment apply added? | **no** |
| Assignment rules added? | **no** |
| Resource assignment added? | **no** |
| Workflow activation added? | **no** |
| Evidence added? | **no** |
| UI changed? | **no** |
| Import/export added? | **no** |
| MOD-0155 readiness API added? | **no** |
| Product/Brand master added? | **no** |
| Employee/Position master added? | **no** |
| MOD-0018 `EntitlementDataScopeKind` changed? | **no** |
| Reference set publish/seed done? | **no** |
| Hardcoded reference fallback introduced? | **no** |
| Mongo hand-edit suggested? | **no** |
| Local seed suggested? | **no** |
| `crm.micro-zone.manage` introduced? | **no** |
| `crm.territory.delete` introduced? | **no** |
| Account entity modified with ZoneId/MicroZoneId/TerritoryId? | **no** |
| Contact entity modified with TerritoryId? | **no** |
| Gateway route direct 5061 exposed to browser? | **no** (Gateway-only, downstream 5061) |
| Tenant isolation enforced? | **yes** |
| CorrelationId preserved? | **yes** (model + node alanı) |
| Tests passed? | **yes** (214/214) |

---

## 11. Final Verdict

**PASS:** FU01 implemented within scope, tests pass (214/214), no out-of-scope runtime added, no hardcoded fallback,
no boundary broken. Live create smoke intentionally deferred to F10 publish (correct fail-closed behaviour).

---

## 12. Next Recommended Prompt

Öncelik sırasıyla:

1. **MOD-0048 Territory Reference Set Publish Execution (F10)** — canlı create smoke isteniyorsa önce bu; 10 required
   set'i tenant `97c59330-…` scope'unda maker-checker ile publish et, sonra FU01 create smoke koş.
2. **MOD-0151 FU02 — Territory Hierarchy UI / Territory Model Viewer** — backend hazır; Golden Reference Compact,
   7 dil resx, `_LayoutTenantShell` menü `<li>` (`crm.territory.read` guard).
3. **MOD-0151 FU03 — Assignment Rules + Preview** (UI kasıtlı ertelenirse alternatif).

> **Not:** FU01 permission'ları yalnız **tanımdır**; RBAC seed / `crm-rbac-integration-plan.md` supersede (F2) hâlâ
> ayrı governance follow-up'ıdır. Territory menüsünün görünmesi için ilgili permission'ların role atanması gerekir.
