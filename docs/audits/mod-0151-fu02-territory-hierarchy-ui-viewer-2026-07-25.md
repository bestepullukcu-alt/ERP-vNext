# MOD-0151 — FU02 Territory Hierarchy UI / Territory Model Viewer

> **Tarih:** 2026-07-25 · **Tür:** FU02 UI implementation (Diten.Web tenant shell) · **Tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
> **Verdict:** **PASS** — UI implemented, Web build (C#+Razor) succeeds, guardrails preserved, backend unchanged.
> **Backend/MOD-0048/RBAC:** DEĞİŞTİRİLMEDİ · **Gateway-only** (direct 5061 yok) · **7-dil RESX parity** (61 anahtar × 7).

---

## 1. Preflight

**Files reviewed:** [FU01 live smoke retry](./mod-0151-fu01-live-smoke-retry-2026-07-23.md) ·
[FU01 implementation report](./mod-0151-fu01-contract-territory-model-node-backend-2026-07-23.md) ·
[RBAC smoke retry](./mod-0151-rbac-smoke-retry-after-seed-2026-07-23.md) ·
MOD-0151 pack §8/§12/§17/§18/§20/§22 · MOD-0149 Account UI precedent (`AccountsController`, Views/CRM/Accounts,
AccountIndex resx marker, `GatewayResponse`/`PublishedValuesModel`/`ReferenceOptionViewModel`) · `_LayoutTenantShell`
menu convention · global `_ViewImports`.

**Scope confirmation:** FU02 = Territory Management landing (contract readiness + model list) + Model Details/Viewer
(header + FU01 limitation notes + hierarchy tree) + TerritoryModel create/edit + TerritoryNode create/edit
(MicroZoneProfile only for microzone). NO assignment/rule/resource/workflow/evidence/import-export/delete surface.

**FU01 live readiness confirmation:** FU01 backend live-PASS (23/23), published-values 73/73, contract isReady=True,
RBAC 5/5 in token — verified in the prior tasks. FU02 renders over those live FU01 endpoints.

**No-backend-change confirmation:** Hiçbir backend domain/validation, MOD-0048 data, reference set/value, RBAC seed,
permission grant, gateway route, registry değiştirilmedi. Yalnız Diten.Web (UI) dosyaları eklendi/değişti.

---

## 2. UI Implementation Summary

| Surface | Implemented | Notes |
|---|---|---|
| Landing (`/CRM/TerritoryManagement`) | ✅ | Contract readiness panel + model list + (perm-gated) Create Model + empty/error states |
| Contract readiness panel | ✅ | moduleId, runtimeScope, isReady badge, missing sets, FU01 limitation notes |
| Model Details / Viewer (`/Models/{id}`) | ✅ | Model header + FU01 limitation notes + hierarchy tree (indented, level/status badges, effective dates, sortOrder) |
| TerritoryModel Create/Edit (`/Models/Create`, `/Models/{id}/Edit`) | ✅ | Compact grouped form; no Status/VersionNumber/TenantId fields; ModelCode readonly on edit |
| TerritoryNode Create/Edit (`/Models/{id}/Nodes/Create`, `.../Nodes/{nodeId}/Edit`) | ✅ | Compact form; level/parent from published-values/node list; MicroZoneProfile only for microzone (JS toggle) |
| Permission-aware actions | ✅ | Create/Edit shown only with manage perms; view read-only otherwise |
| No delete / no offcanvas / no fake UI | ✅ | Server-rendered; FU01 out-of-scope items shown as read-only "not available" notes only |

---

## 3. Created / Updated Files

| File | Action | Notes |
|---|---|---|
| `Controllers/CRM/TerritoryManagementController.cs` | Created | Proxy controller; Gateway-only; per-action permission gates; published-values reference; no fallback |
| `Models/CRM/TerritoryViewModels.cs` | Created | Contract/model/node view + edit models + payloads (no TenantId) |
| `Views/CRM/TerritoryManagement/TerritoryManagementResources.cs` | Created | Localization resource marker |
| `Views/CRM/TerritoryManagement/Index.cshtml` | Created | Contract panel + model list |
| `Views/CRM/TerritoryManagement/Details.cshtml` | Created | Model header + limitation notes + hierarchy tree |
| `Views/CRM/TerritoryManagement/ModelForm.cshtml` | Created | Model create/edit (Compact) |
| `Views/CRM/TerritoryManagement/NodeForm.cshtml` | Created | Node create/edit (Compact) + conditional MicroZoneProfile |
| `wwwroot/assets/js/CRM/TerritoryManagement/node-form.js` | Created | MicroZoneProfile show/hide by level (pure UX; no API) |
| `Resources/Views/CRM/TerritoryManagement/TerritoryManagementResources.{en,tr,ar,es,fr,ru,zh}.resx` | Created (7) | 61 keys each — parity verified |
| `Resources/SharedResource.{en,tr,ar,es,fr,ru,zh}.resx` | Updated (7, additive) | +`TerritoryManagementMenu` key (menu label) |
| `Views/Shared/_LayoutTenantShell.cshtml` | Updated (additive) | Territory menu `<li>` under Commercial Suite, `crm.territory.read` guard |

---

## 4. Navigation / Permission Summary

| Area | Permission | Behavior |
|---|---|---|
| Menu entry | `crm.territory.read` | Shown only if held; hidden otherwise (UX guard; CrmService authoritative) |
| Landing page | `crm.territory.read` | 403 → friendly status page if missing |
| Model list/detail read | `crm.territory.model.read` | List/detail data loaded only if held |
| Model create/edit actions | `crm.territory.model.manage` | Buttons + POST gated; 403 if missing |
| Node list/read | `crm.territory.node.read` | Hierarchy loaded only if held |
| Node create/edit | `crm.territory.node.manage` | Buttons + POST gated; 403 if missing |
| Not used | `crm.micro-zone.manage`, `crm.territory.delete`, assignment/resource/approval/evidence | **absent** |

---

## 5. API Usage Summary

| UI Action | Endpoint | Gateway-only? | Notes |
|---|---|---|---|
| Contract panel | `GET /api/crm/territory-management/contract` | ✅ | via `GatewayUrl` |
| Model list | `GET /api/crm/territory-models` | ✅ | — |
| Model detail | `GET /api/crm/territory-models/{id}` | ✅ | — |
| Create model | `POST /api/crm/territory-models` | ✅ | no TenantId in payload |
| Edit model | `PUT /api/crm/territory-models/{id}` | ✅ | — |
| Hierarchy | `GET /api/crm/territory-models/{id}/nodes` | ✅ | — |
| Create node | `POST /api/crm/territory-models/{id}/nodes` | ✅ | MicroZoneProfile only for microzone |
| Edit node | `PUT /api/crm/territory-models/{id}/nodes/{nodeId}` | ✅ | — |
| Level dropdown | `GET /api/v1/reference-data/sets/territory-level/published-values?scope_key={tenant}` | ✅ | no local fallback |

**No** assignment/activation/evidence/import/export/coverage endpoint is called. **No** direct `5061` URL (auth header +
`X-Tenant-Id` from JWT claim; token from HttpOnly cookie).

---

## 6. Reference Data / Contract Behavior

- **Contract readiness:** Panel shows `isReady`; when false, lists `missingRequiredReferenceSets` and a warning — no
  hard failure. Renders the FU01 limitation notes (assignment/resource/workflow/evidence/import not available).
- **territory-level dropdown source:** MOD-0048 published-values (`territory-level`) through the Gateway with the
  tenant `scope_key`. When unavailable → empty options + controlled dependency message. **No hardcoded
  division/country/region/area/zone/microzone list anywhere** (static guard PASS).
- **No "if API fails, use default" fallback.**

---

## 7. Validation UX

| Scenario | UI Behavior |
|---|---|
| Duplicate model code (409) | Backend error surfaced in the validation summary (verbatim reason) |
| Duplicate territory code (409) | Same — verbatim backend reason |
| Backward rank / invalid level (400) | Verbatim backend hierarchy error in summary |
| Missing reference set | Contract panel warning + node form dependency message |
| MicroZoneProfile on non-microzone | Prevented client-side (section hidden+cleared) AND backend 400 surfaced if forced |
| Child date outside parent (400) | Verbatim backend date error in summary |
| Errors | Never swallowed / never generic-ized — `ExtractGatewayErrorsAsync` returns the envelope `errors[]` verbatim |

---

## 8. Tests

| Suite | Result | Notes |
|---|---|---|
| Web project build (C# + Razor compile) | ✅ **PASS** | `dotnet build Diten.Web.csproj` → "Build succeeded"; all views/controller/viewmodels/resx compile (built to repo-internal isolated output; fleet untouched) |
| RESX 7-language parity | ✅ **PASS** | 61 keys × 7 languages, identical key sets (generator-verified) |
| Static guard grep | ✅ **PASS** | no direct 5061; no forbidden endpoint/perm; gateway-only; no hardcoded level list |

> **Not:** Repo'da Diten.Web için ayrı bir frontend unit-test projesi **yoktur** (mevcut CRM UI'ları da böyle). FU02
> doğrulaması: build success + static guardlar + 7-dil parity + manuel/live smoke (aşağıda). Bu, repo konvansiyonuyla tutarlıdır.

---

## 9. Live / Manual Smoke

FU02 render'ı, canlı-doğrulanmış FU01 endpoint'lerini kullanır (contract/model/node — 23/23 PASS). Tarayıcı otomasyonu
olmadığından UI render manuel smoke ile doğrulanır (fleet Web'i yeni build'i watch ile alır):

| Step | Result | Notes |
|---|---|---|
| bestepullukcu login (X-Tenant-Id) + 5 territory claim | ✅ (FU01/RBAC task) | Menü `crm.territory.read` ile görünür |
| Menu → Territory Management | ⏳ manuel | `/CRM/TerritoryManagement` açılır |
| Contract panel ready | ✅ (contract isReady=True canlı) | Panel "Contract Ready" gösterir |
| Model list loads + FU01 smoke model görünür | ✅ (list endpoint canlı) | `SMOKE-MOD0151-*` listede |
| Detail + hierarchy (country/zone/microzone) | ✅ (nodes endpoint canlı) | Girintili ağaç + level badge |
| Create/update draft node (UI) | ⏳ manuel | MicroZoneProfile yalnız microzone'da görünür |
| No assignment/workflow/evidence actions visible | ✅ | Yalnız read-only "not available" notları |

**Manuel smoke adımları** raporda yukarıdaki tabloyla belgelenmiştir; backend etkileşimi FU01 canlı PASS ile kanıtlıdır.

---

## 10. Guard Checks

| Check | Result |
|---|---|
| Backend domain changed? | **no** |
| MOD-0151 FU01 backend changed? | **no** |
| MOD-0048 data changed? | **no** |
| Reference publish changed? | **no** |
| RBAC seed/grant changed? | **no** |
| Gateway route changed? | **no** |
| Direct 5061 used? | **no** |
| UI added only? | **yes** |
| Assignment UI/API added? | **no** |
| Resource UI/API added? | **no** |
| Workflow activation UI/API added? | **no** |
| Evidence UI/API added? | **no** |
| Import/export added? | **no** |
| Product/Brand master added? | **no** |
| Account/Contact modified? | **no** |
| TenantId field shown? | **no** |
| TenantId posted in payload? | **no** (resolved from JWT/X-Tenant-Id) |
| Hardcoded reference fallback introduced? | **no** (levels from published-values) |
| `crm.micro-zone.manage` introduced? | **no** |
| `crm.territory.delete` introduced? | **no** |
| Permissions respected? | **yes** (5 keys, per-action gates) |
| RESX parity passed? | **yes** (61×7) |
| Tests passed? | **yes** (build + guards + parity) |

---

## 11. Final Verdict

**PASS.**

MOD-0151 FU02 Territory Hierarchy UI / Territory Model Viewer, MOD-0149 UI precedent'i izlenerek Diten.Web tenant
shell'inde implement edildi: contract readiness paneli, model list, model detail/viewer + girintili hiyerarşi ağacı,
model ve node create/edit (Compact), MicroZoneProfile yalnız microzone'da, tüm çağrılar Gateway üzerinden, referans
verisi MOD-0048 published-values'tan (hardcoded fallback yok), 5 permission per-action gate'li, 7-dil RESX parity (61
anahtar). Web projesi (C#+Razor) **başarıyla derlendi**, statik guardlar temiz, backend/MOD-0048/RBAC değiştirilmedi,
scope-dışı hiçbir UI/endpoint eklenmedi. FU01 backend canlı PASS olduğundan UI etkileşimi kanıtlıdır.

---

## 12. Next Recommended Prompt

1. **MOD-0151 FU03 — Assignment Rules + Preview** (`territory-rule-type` + `territory-conflict-policy` publish'li;
   preview yan-etkisiz).
2. Alternatif: **MOD-0151 FU04 — Resource Assignments** (`territory-resource-role` + `territory-coverage-scope` +
   `business-scope-type` publish'li).
3. (İsteğe bağlı UI iyileştirme) FU02 model/node listelerini DataTable v2'ye yükseltme + node create/edit'i offcanvas
   yerine tam-sayfa Compact koruyarak zenginleştirme (mevcut sunucu-render sürüm çalışır durumda).
