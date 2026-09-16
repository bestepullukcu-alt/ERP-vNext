---
id: MOD-0144
name: Contracting & Clause Library
domain: procurement
service: Diten.ProcurementService
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: draft
owner: procurement / control-tower
branch: feature/procurement-mvp-2
started: 2026-09-16
target: 2026-10-20
form_field_count: 10
---

# MOD-0144 — Contracting & Clause Library

> **Status guard.** `draft` — DCP-010 `approved`/`ready-for-execution` + bu pack `ready-for-dev` olmadan kod YOK (CAP-001 §7). DCP-010 §8 sırasında **son dikey dilim** (0140→0145→0141→0142→0143→0144); MVP-2 içinde mi yoksa fast-follow mu Ali onayında netleşir (OD-4). Bu pack ready-for-dev **hedefli** yazılmıştır.
> **Contract authority.** Entity alanları `docs/analysis/contracts/contracting.openapi.yaml`'dan (x-owner MOD-0144, `x-min-contract: CLM-CONTRACT-BUNDLE`) türetilir; uydurma yasak (K12). Supplier (0140) ve award/rfx (0145) **consume edilir, yaratılmaz** — bilinmeyen supplier/rfx `fail-closed` (404 UNKNOWN_REFERENCE). Doküman binary burada saklanmaz; yalnız `evidenceRef` → MOD-0029/0031.

## 1. Module Summary
Procurement/sourcing sözleşmeleri + clause library + clause deviation + onay (approval trail) SoR'u. Hedef kullanıcı: procurement operatörü + sözleşme onaylayan. Sözleşme yaşam döngüsü (Draft→InReview→Active→Expired/Terminated) ve standart clause kütüphanesi burada yönetilir; supplier kimliği (0140) ve award (0145) referans olarak bağlanır. Blueprint W-2, Procurement Suite / P2P; kanonik ad "Contracting & Clause Library"; Min Integration Contract = CLM-CONTRACT-BUNDLE.

## 2. Ownership and Boundaries
- **In-scope:** procurement/sourcing sözleşmesi (`Contract`: identity/status/lifecycle, supplier/award bağı, clause set, deviation, effective aralığı, currency), clause library (`Clause`: standart clause'lar), clause deviation kaydı (reference), sözleşme onay/aktifleştirme (approval trail via MOD-0023), evidenceRef linkage.
- **Out-of-scope:** doküman **binary** saklama (MOD-0029/0031 Evidence — yalnız `evidenceRef` referansı tutulur), supplier master/onboarding (MOD-0140), sourcing/RFx/bid/award **kararı** (MOD-0145), ödeme/AP (Finance/Treasury), supplier performance/portal (MOD-0147/0148 → MVP-6), envanter (n/a — bu modül stok tutmaz).
- **Owned contract:** `CONTRACTING` (`contracting.openapi.yaml`, x-owner MOD-0144). Tek-yazıcı seam (K15); producer surface additive-only (K16).

## 3. Owned Objects
- **Entity:** `Contract` (EntityBase) + `ClauseRef` (embedded: clauseId/deviation/deviationText), `Clause` (clause library, EntityBase).
- **Commands:** CreateContract, ActivateContract, (soft) DeleteContract, BulkDeleteContract, CreateClause. *(Update/soft-delete/bulk-delete → ASSUMPTION-0144-01.)*
- **Queries:** GetContractList, GetContractById, GetClauseLibraryList.
- **DTOs:** `contracting.openapi.yaml` şemaları (Contract, ContractUpsert, ClauseRef, ContractStatus, Clause, ClauseUpsert, Error).
- **API endpoints (server `/api/contracts`):** `POST /` (createContract), `GET /` (listContracts), `GET /{contractId}` (getContract), `POST /{contractId}/activate` (activateContract), `GET /clauses` (listClauseLibrary), `POST /clauses` (createClause).
- **Frontend route:** `/Procurement/Contracts` (tenant shell).
- **Permissions:** `procurement.contracts.{read|create|update|activate|delete|bulk-delete}`, `procurement.clauses.{read|create}`.

## 4. Entity Fields
| Field | Type | Rules | Index |
|---|---|---|---|
| ContractId | string (public code) | server-assigned; unique/tenant+LE | Unique (Tenant+LE) |
| SupplierId | string | required; **MOD-0140 consume**, fail-closed (unknown → 404) | Index |
| RfxId | string | nullable; **MOD-0145 award consume**, fail-closed | Index |
| Title | string | required, trim, max 200 | text |
| Status | enum(Draft/InReview/Active/Expired/Terminated) | required, default Draft | Index |
| EffectiveFrom | date | required | — |
| EffectiveTo | date | nullable; ≥ EffectiveFrom | — |
| Currency | string | nullable; **LE base currency** (boşsa server default → ASSUMPTION-0144-04) | — |
| Clauses[] | ClauseRef {clauseId, deviation?, deviationText?} | clauseId clause library referansı; deviation=true ise deviationText önerilir | — |
| WorkflowInstanceId | string | nullable; MOD-0023 approval instance (activate'te) | — |
| EvidenceRefs[] | string | nullable; **evidenceRef → MOD-0029/0031; binary YOK** | — |
| ContractVersion | string | contract shape sürümü (payload'da döner) | — |
| TenantId, LegalEntityId | Guid | **server-resolved, payload'da YOK**; her sorguda filtre | Compound |
| Id, IsDeleted, DeletedAt, CreatedAt, UpdatedAt | (EntityBase/audit) | soft-delete zorunlu | — |

**Clause (library) entity:**
| Field | Type | Rules | Index |
|---|---|---|---|
| ClauseId | string | server-assigned; unique/tenant+LE | Unique (Tenant+LE) |
| Category | string | required | Index (+Title unique-partial) |
| Title | string | required, trim | Unique(Category+Title, Tenant+LE) |
| Body | string | required | — |
| TenantId, LegalEntityId, EntityBase/audit | — | server-resolved; soft-delete | Compound |

> `form_field_count: 10` (supplierId, rfxId, title, effectiveFrom, effectiveTo, currency, clause satırı: clauseId/deviation/deviationText, evidenceRef) > 8 → `golden_reference: compact`. Clause create formu (category/title/body = 3) ikincil form; birincil sayım Contract create/edit formudur.

## 5. Repo Scope
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Application/Features/Contract/**`
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Application/Features/Clause/**`
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Domain/**` (Contract, ClauseRef, Clause entities)
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Persistence/**` (repositories, Mongo index config)
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Api/Controllers/ContractsController.cs`
- `frontend/Diten.Web/Views/Procurement/Contracts/**` + `wwwroot/assets/js/Procurement/Contracts/**` + `Resources/Views/Procurement/Contracts/**`

## 6. Protected Paths
- `.antigravity/**`
- `services/Diten.SupplyChainService/**`, `services/Diten.MdmService/**` ve diğer domain servisleri
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration-agent owned — ayrı task)
- `frontend/Diten.Web/Views/Archive/**`, `Views/Shared/_Layout.cshtml`
- `docs/analysis/contracts/supplier.openapi.yaml` (CONSUMED reference — SUPPLIER, redefine YASAK), `docs/analysis/contracts/sourcing.openapi.yaml` (CONSUMED reference — SOURCING award, redefine YASAK)
- `docs/analysis/contracts/inventory-bundle.openapi.yaml`, `product-master-bundle.openapi.yaml`, `location.openapi.yaml` (CONSUMED — redefine YASAK)
- `docs/analysis/contracts/contracting.openapi.yaml` **OWNED** (yazılabilir; additive-only, K16)

## 7. Dependencies
- **Backbone:** JWT/RBAC (MOD-0018), Audit (MOD-0021), Workflow/Approvals (MOD-0023, sözleşme onayı + clause deviation onayı), Evidence/Docs (MOD-0029/0031, doküman referansı — binary yok), Notification (MOD-0027).
- **Consumed contracts (owned-by-others, redefine YASAK):** `SUPPLIER` (MOD-0140 — `supplierId` doğrulama, fail-closed), `SOURCING` (MOD-0145 — `rfxId` award bağı, fail-closed). Consume-don't-own: yalnız okur/referanslar; lokal supplier/rfx kimliği açmaz.
- **Downstream consumers:** yok (sözleşme yaşam döngüsü şimdilik terminal SoR; MVP-6/raporlama ileride tüketebilir).

## 8. Runtime Constraints
- Gateway port **5062**; frontend yalnız Gateway (5000) üzerinden çağırır.
- **Tenant + Legal-Entity izolasyonu her sorguda** (server-resolved; cross-LE fail-closed → 404).
- Soft delete (`IsDeleted`/`DeletedAt`).
- Idempotent create/activate (`Idempotency-Key` zorunlu header); replay yan-etki üretmez.
- Duplicate clause (Category+Title, Tenant+LE) → **409 DUPLICATE_CLAUSE**.
- Geçersiz durum geçişi (ör. aktif sözleşmeyi tekrar aktifleştirme) → **409 INVALID_STATE**.
- Bilinmeyen supplier/rfx → **404 UNKNOWN_REFERENCE** (fail-closed; consume-don't-own).
- Para = `Decimal` string (`^-?\d+(\.\d+)?$`), **float YASAK**; currency = LE base currency (AD-6).
- Mongo V3 GUID subtype-4.

## 9. Layout & Shell Contract
- `shell: tenant` → tüm `Views/Procurement/Contracts/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"` **AÇIKÇA**.
- View klasörü: `Views/Procurement/Contracts/` · Frontend route: `/Procurement/Contracts`.

## 10. Backend File Convention
Golden Reference **Compact** birebir: `Features/Contract/` (ve `Features/Clause/`) altında Commands/, Queries/, Handlers/CommandHandlers/, Handlers/QueryHandlers/, Validators/, `ContractModels.cs` (+ `ClauseModels.cs`). Naming: Command/Query record; Handler/Validator class **suffix YOK** (`CreateContractHandler`, `CreateContractValidator`, `ActivateContractHandler`, `CreateClauseHandler`).

## 11. Frontend File Contract
Compact seti: `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml` (`data-dt-standard="v2"`), `_IndexL10n.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml`, `{Module}Index.cs` + `index.js` + `index.l10n.js`. **YASAK:** `_CreateEditOffcanvas`, `_DetailsQuickView` (compact). L10n: tenant modülü → **7 dil** (en, tr, fr, es, zh, ar, ru).

## 12. Validation Rules
| Field | Required | Rule | DB | Pre-check |
|---|---|---|---|---|
| SupplierId | Evet | var olan supplier (0140); fail-closed | — | ValidateSupplier (SUPPLIER contract) |
| RfxId | Hayır | verilirse var olan award (0145); fail-closed | — | ValidateRfx (SOURCING contract) |
| Title | Evet | trim, max 200 | — | — |
| EffectiveFrom | Evet | date, zorunlu | — | — |
| EffectiveTo | Hayır | date; ≥ EffectiveFrom | — | — |
| Currency | Hayır | boşsa LE base currency; float yok | — | — |
| Clauses[].clauseId | Evet (satırda) | clause library'de var | — | ExistsClauseAsync |
| Clauses[].deviationText | Koşullu | deviation=true ise önerilir | — | — |
| Clause.category / title / body | Evet | trim; (category+title) unique | Unique-partial | ExistsByCategoryTitleAsync |

## 13. Failure Path to Verify
- **Duplicate clause** (aynı Category+Title, Tenant+LE) → 409 DUPLICATE_CLAUSE + field-level error + kayıt yok + reload temiz.
- **Missing Title/EffectiveFrom** → 400/422 VALIDATION_FAILED + validator mesajı + save engellenir.
- **Unknown supplier/rfx** (consume fail-closed) → 404 UNKNOWN_REFERENCE + kayıt oluşmaz.
- **Invalid state transition** (aktif sözleşmeyi tekrar activate) → 409 INVALID_STATE + sessiz overwrite YOK.
- **Unauthorized actor** (izin yok) → 403 + action disabled/permission-denied (UAS-001: iskelet çizilmez, yönlendirme yok).
- **Cross-tenant/LE erişim** → 404 (sızıntı yok).

## 14. Authorization Convention
- Policy: `[Authorize]` (tenant actor).
- Permission format (PKS-001, lowercase-dotted, ≥3 segment): `procurement.contracts.{action}`, `procurement.clauses.{action}`.
- Actions (contracts): read, create, update, activate, delete, bulk-delete. Actions (clauses): read, create.
- Actor: tenant_user (procurement rolü); server-side `[HasPermission]` zorunlu.

## 15. Gateway / API Routing Decision
- Karar: Gateway değişikliği **gerekli** (`/api/contracts` → 5062). `ocelot.json` protected; bu pack yazmaz — explicit Upstream/Downstream (`/api/contracts`, `/api/contracts/{everything}`) + OPTIONS içeren **integration-agent task**'ı olarak ayrı yürütülür.

## 16. Acceptance Criteria
- [ ] `POST /api/contracts` idempotent create → 201; `GET` list/by-id tenant+LE filtreli; bilinmeyen supplier/rfx → 404 UNKNOWN_REFERENCE (fail-closed).
- [ ] `POST /api/contracts/{contractId}/activate` idempotent; Draft/InReview→Active geçişi + approval trail (MOD-0023, `workflowInstanceId` set); aktif→activate → 409 INVALID_STATE.
- [ ] Sözleşme yaşam döngüsü Draft→InReview→Active→Expired/Terminated izlenebilir; clause deviation (deviation/deviationText) kaydedilir; `evidenceRef` linkage (MOD-0029/0031) tutulur, binary saklanmaz.
- [ ] `GET/POST /api/contracts/clauses`: duplicate (category+title) → 409 DUPLICATE_CLAUSE; clause library sayfalı (cursor) döner.
- [ ] Tenant+LE izolasyon: cross-LE 404; unauthorized 403 (UAS-001); soft delete.
- [ ] L3 persistence (contracts/approvals) + E4 evidence (state-changing: create/activate).
- [ ] Tüm `Views/Procurement/Contracts/*.cshtml` → `Layout = "_LayoutTenantShell"` açık; `verify_datatable_page.py --area Procurement --module Contracts --reference compact` PASS.
- [ ] RESX 7-dil parite PASS.

## 17. Test Expectations
- Unit: validator (duplicate clause, missing title/effectiveFrom, effectiveTo≥from), handler (create/activate/createClause).
- Integration: tenant isolation (cross-LE 404), soft-delete, idempotency replay (create/activate yan-etki yok), invalid state transition 409, unknown supplier/rfx fail-closed 404.
- Contract: `contracting.openapi.yaml` mock (Prism) ile şekil uyumu; CONSUMED SUPPLIER/SOURCING doğrulama mock'a karşı.
- Frontend: browser smoke + DataTable verifier PASS.
- Build: ProcurementService + frontend + gateway 0-error.

## 18. Ready-for-dev Checklist
- [ ] Golden Reference Compact okundu
- [ ] Frontmatter tam (service/shell/golden_reference/entity_base)
- [ ] Layout & Shell Contract Razor Layout açık
- [ ] Backend/Frontend file convention golden reference ile birebir
- [ ] Validation her field için yazılı
- [ ] Failure Path ≥4 senaryo (duplicate/missing/unauthorized/state-conflict) + unknown-reference + cross-tenant
- [ ] Authorization permission listesi + policy + actor
- [ ] Gateway routing kararı (integration-agent task)
- [ ] Acceptance test edilebilir + consume-don't-own (supplier 0140 / award 0145) fail-closed posture
- [ ] Test expectations build/verifier/RESX/smoke/contract-mock kapsıyor
- [ ] DCP-010 approved + bu pack ready-for-dev onayı (kod kapısı)

## 19. Implementation Notes
DCP-010 §8 sırasında **son dikey dilim** (0140→0145→0141→0142→0143→0144). OD-4 gereği Contracting MVP-2 içinde mi yoksa **fast-follow** mu Ali onayında netleşir (şu an DCP üyesi, sıra sonuncu). Servis zaten 0140 ile scaffold edilmiş olacağından bu pack yeni servis doğurmaz; yalnız `Features/Contract` + `Features/Clause` eklenir (JWT + Mongo V3 GUID + TenantId+LE izolasyonu + port 5062 + gateway route integration-agent task). CONTRACTING contract `x-status: REVIEW` — freeze owner+consumer review sonrası. Approval MOD-0023 üzerinden; `workflowInstanceId` activate'te doldurulur.

**ASSUMPTION satırları (contract boşlukları — additive-safe, contract-bozan değil):**
- **ASSUMPTION-0144-01:** `contracting.openapi.yaml` yalnız `POST/GET /`, `GET /{id}`, `POST /{id}/activate`, `GET/POST /clauses` tanımlar; update/soft-delete/bulk-delete endpoint'i YOK. Permission seti (`update|delete|bulk-delete`) + compact CRUD deseni gereği `PATCH /{contractId}`, soft `DELETE`, bulk-delete **additive** eklenir; owned contract'a freeze öncesi additive yansıtılır (K16, superset korunur). Uydurma veri değil — CRUD yüzeyi.
- **ASSUMPTION-0144-02:** Lifecycle enum beş durum verir (Draft/InReview/Active/Expired/Terminated) ama endpoint yalnız `activate` (→Active). Geçiş tetikleri: `InReview` = onaya gönderme (update/submit), `Active` = activate, `Expired` = `EffectiveTo` geçince türetilen/scheduled durum, `Terminated` = additive terminate aksiyonu (`procurement.contracts.update` altında). Additive-safe modellenir.
- **ASSUMPTION-0144-03:** Clause library `GET/POST` (read|create) — update/delete YOK. Clause **immutable** kabul edilir (düzeltme = yeni clause/versiyon); permission seti bununla tutarlı (clause delete yok).
- **ASSUMPTION-0144-04:** `currency` contract'ta nullable; AD-6 gereği boş bırakılırsa server LE base currency ile doldurur (kullanıcı uydurması değil).
- **ASSUMPTION-0144-05:** `workflowInstanceId` (contract'ta nullable) MOD-0023 approval instance'ına bağlanır; activate approval trail'i başlatır/ilişkilendirir.

## 20. Follow-up Items
- CONTRACTING contract freeze (owner+consumer review) → `x-status: FROZEN`; update/delete/bulk-delete + terminate additive yansıması.
- Contract clause AI-extraction (DCP-010 §19 future) → follow-up.
- Sözleşme yenileme/hatırlatma (renewal/expiry notification via MOD-0027) → follow-up.
- Contract raporlama/analitik tüketici seam'i (MVP-6 olası) → follow-up.
