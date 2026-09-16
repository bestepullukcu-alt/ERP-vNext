---
id: MOD-0140
name: Supplier Onboarding (KYC/Sanctions/Docs)
domain: procurement
service: Diten.ProcurementService
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: ready-for-dev
owner: procurement / control-tower
branch: feature/procurement-mvp-2
started: 2026-09-16
target: 2026-10-15
form_field_count: 11
---

# MOD-0140 — Supplier Onboarding (KYC/Sanctions/Docs)

> **Status guard.** `ready-for-dev` — DCP-010 `approved` (Ali, 2026-09-16) + bu pack `ready-for-dev` → kod yetkili (CAP-001 §7). **FAZ 1: ilk scaffold edilecek modül** (`Diten.ProcurementService` doğar). runtime_code_allowed bu modül için AÇIK.
> **Contract authority.** Entity alanları `docs/analysis/contracts/supplier.openapi.yaml`'dan türetilir; uydurma yasak (K12). Ürün/tedarikçi kimliği consume edilir, yaratılmaz.

## 1. Module Summary
Tedarikçi master + onboarding (KYC / sanctions / doküman / onay) SoR'u. Hedef kullanıcı: procurement operatörü + onaylayan. Supplier kimliği + durum + iletişim yüzeyi MVP-6 (0147 performance, 0148 portal) tarafından tüketilir (frozen slice). Blueprint W-1, Procurement Suite / P2P.

## 2. Ownership and Boundaries
- **In-scope:** supplier master (identity/status/contacts), onboarding case (KYC outcome, sanctions outcome, documents-by-reference, approval), supplier lifecycle (Active/OnHold/Blocked/Inactive).
- **Out-of-scope:** supplier **performance/risk skoru** (MOD-0147, MVP-6), **portal** (MOD-0148, MVP-6), ürün kimliği (MOD-0290/MDM), doküman binary saklama (MOD-0029/0031 Evidence — yalnız `evidenceRef` referansı tutulur), ödeme/AP (Finance/Treasury).
- **Owned contract:** `SUPPLIER` (`supplier.openapi.yaml`, x-owner MOD-0140). Producer surface additive; MVP-6 consumer slice'ı **superset** korunur (daraltma yasak, K16).

## 3. Owned Objects
- **Entity:** `Supplier` (EntityBase), `SupplierContact` (embedded), `OnboardingCase` (KYC/sanctions/docs/approval — Supplier'a bağlı).
- **Commands:** CreateSupplier, UpdateSupplier, SubmitOnboardingCase, (soft) DeleteSupplier, BulkDeleteSupplier.
- **Queries:** GetSupplierList, GetSupplierById, GetOnboardingCase, ValidateSuppliers (bulk id doğrulama — consumer fail-closed).
- **DTOs:** `supplier.openapi.yaml` şemaları (Supplier, SupplierUpsert, OnboardingCase, OnboardingSubmit, KycOutcome, SanctionsOutcome).
- **API endpoints:** `GET/POST /api/suppliers`, `GET/PATCH /api/suppliers/{supplierId}`, `POST /api/suppliers/validate`, `GET/POST /api/suppliers/{supplierId}/onboarding`.
- **Frontend route:** `/Procurement/Suppliers` (tenant shell).
- **Permissions:** `procurement.suppliers.read|create|update|delete|bulk-delete`, `procurement.suppliers.onboard`.

## 4. Entity Fields
| Field | Type | Rules | Index |
|---|---|---|---|
| SupplierId | string (public code) | server-assigned; unique/tenant+LE | Unique (Tenant+LE) |
| Name | string | required, trim, max 200 | text |
| Status | enum(Active/OnHold/Blocked/Inactive) | required, default Active | — |
| Country | string(lowercase code) | nullable | — |
| TaxId | string | nullable; unique/tenant+LE aktifken | Unique-partial |
| Contacts[] | SupplierContact | type∈{primary,billing,quality,logistics}, email/phone nullable | — |
| OnboardingStatus | enum(Draft/InReview/Approved/Rejected) | onboarding case | — |
| KycOutcome | enum(Pending/Passed/Failed/ManualReview) | onboarding | — |
| SanctionsOutcome | enum(Pending/Clear/Hit/Override) | onboarding | — |
| Documents[] | {type, evidenceRef} | evidenceRef → MOD-0029/0031; binary yok | — |
| SourceSystem/ExternalRef | string | nullable (DEC-INV-19 dış besleme) | — |
| TenantId, LegalEntityId | Guid | **server-resolved, payload'da YOK**; her sorguda filtre | Compound |
| Id, IsDeleted, DeletedAt, CreatedAt, UpdatedAt | (EntityBase/audit) | soft-delete zorunlu | — |

> `form_field_count: 11` (Name, Status, Country, TaxId, primary Contact email/phone/type, KYC legalName/registrationNo, Document type, submitForApproval) > 8 → `golden_reference: compact`.

## 5. Repo Scope
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Application/Features/Supplier/**`
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Domain/**` (Supplier, OnboardingCase entities)
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Persistence/**` (repositories, Mongo index config)
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Api/Controllers/SuppliersController.cs`
- `frontend/Diten.Web/Views/Procurement/Suppliers/**` + `wwwroot/assets/js/Procurement/Suppliers/**` + `Resources/Views/Procurement/Suppliers/**`

## 6. Protected Paths
- `.antigravity/**`
- `services/Diten.SupplyChainService/**`, `services/Diten.MdmService/**` ve diğer domain servisleri
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration-agent owned — ayrı task)
- `frontend/Diten.Web/Views/Archive/**`, `Views/Shared/_Layout.cshtml`
- `docs/analysis/contracts/inventory-bundle.openapi.yaml`, `product-master-bundle.openapi.yaml`, `location.openapi.yaml` (CONSUMED — redefine YASAK)

## 7. Dependencies
- **Backbone:** JWT/RBAC (MOD-0018), Audit (MOD-0021), Workflow/Approvals (MOD-0023, onboarding onayı), Evidence/Docs (MOD-0029/0031, doküman referansı), Notification (MOD-0027).
- **Consumed contracts:** yok (SUPPLIER upstream master; ürün/tedarikçi consume etmez — bu modül supplier'ın kendisidir).
- **Downstream consumers:** MOD-0141 PO (`supplierId`), MOD-0143 Invoice-Match, MOD-0145 Sourcing (`invitedSupplierIds`/`bid.supplierId`), MOD-0144 Contracting, MVP-6 0147/0148.

## 8. Runtime Constraints
- Gateway port **5062**; frontend yalnız Gateway (5000) üzerinden çağırır.
- **Tenant + Legal-Entity izolasyonu her sorguda** (server-resolved; cross-LE fail-closed → 404).
- Soft delete (`IsDeleted`/`DeletedAt`).
- Idempotent create/onboarding (`Idempotency-Key`); update optimistic concurrency (`If-Match`/rowVersion) → 409.
- Mongo V3 GUID subtype-4.

## 9. Layout & Shell Contract
- `shell: tenant` → tüm `Views/Procurement/Suppliers/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"` **AÇIKÇA**.
- View klasörü: `Views/Procurement/Suppliers/` · Frontend route: `/Procurement/Suppliers`.

## 10. Backend File Convention
Golden Reference **Compact** birebir: `Features/Supplier/` altında Commands/, Queries/, Handlers/CommandHandlers/, Handlers/QueryHandlers/, Validators/, `SupplierModels.cs`. Naming: Command/Query record; Handler/Validator class **suffix YOK** (`CreateSupplierHandler`, `CreateSupplierValidator`).

## 11. Frontend File Contract
Compact seti: `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml` (`data-dt-standard="v2"`), `_IndexL10n.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml`, `{Module}Index.cs` + `index.js` + `index.l10n.js`. **YASAK:** `_CreateEditOffcanvas`, `_DetailsQuickView` (compact). L10n: tenant modülü → **7 dil** (en, tr, fr, es, zh, ar, ru).

## 12. Validation Rules
| Field | Required | Rule | DB | Pre-check |
|---|---|---|---|---|
| Name | Evet | trim, max 200 | — | — |
| TaxId | Hayır | format; aktif supplier'da unique (Tenant+LE) | Unique-partial | ExistsByTaxIdAsync |
| Status | Evet | enum | — | — |
| Contact.type | Evet (satırda) | enum | — | — |
| Onboarding.submit | — | KYC legalName NotEmpty; en az 1 document | — | — |

## 13. Failure Path to Verify
- **Duplicate active TaxId** → 409 + field-level error + kayıt yok + reload temiz.
- **Missing Name** → 400 + validator mesajı + save engellenir.
- **Concurrency conflict** (stale update) → 409 + "kayıt değişti, yeniden yükle" + sessiz overwrite YOK.
- **Unauthorized actor** (izin yok) → 403 + action disabled/permission-denied (UAS-001: iskelet çizilmez, yönlendirme yok).
- **Cross-tenant/LE erişim** → 404 (sızıntı yok).

## 14. Authorization Convention
- Policy: `[Authorize]` (tenant actor).
- Permission format (PKS-001, lowercase-dotted, ≥3 segment): `procurement.suppliers.{action}`.
- Actions: read, create, update, delete, bulk-delete, onboard.
- Actor: tenant_user (procurement rolü); server-side `[HasPermission]` zorunlu.

## 15. Gateway / API Routing Decision
- Karar: Gateway değişikliği **gerekli** (`/api/suppliers` → 5062). `ocelot.json` protected; bu pack yazmaz — explicit Upstream/Downstream + OPTIONS içeren **integration-agent task**'ı olarak ayrı yürütülür.

## 16. Acceptance Criteria
- [ ] `POST /api/suppliers` idempotent create → 201; `GET` list/by-id tenant+LE filtreli.
- [ ] `PATCH` optimistic concurrency → stale update 409.
- [ ] `POST /api/suppliers/validate` bilinmeyen id'leri `known:false` işaretler (consumer fail-closed).
- [ ] Onboarding: KYC/sanctions outcome + document evidenceRef + approval (MOD-0023) kaydedilir; binary saklanmaz.
- [ ] SUPPLIER contract consumer slice (listSuppliers/getSupplier/validateSuppliers) şekli **değişmeden** yanıt verir (MVP-6 uyumu).
- [ ] Tenant+LE izolasyon: cross-LE 404; unauthorized 403 (UAS-001).
- [ ] Tüm `Views/Procurement/Suppliers/*.cshtml` → `Layout = "_LayoutTenantShell"` açık; `verify_datatable_page.py --area Procurement --module Suppliers --reference compact` PASS.
- [ ] RESX 7-dil parite PASS.

## 17. Test Expectations
- Unit: validator (duplicate taxId, missing name), handler (create/update/onboarding).
- Integration: tenant isolation (cross-LE 404), soft-delete, idempotency replay (duplicate side-effect yok), concurrency 409.
- Contract: `supplier.openapi.yaml` mock (Prism) ile consumer slice uyumu.
- Frontend: browser smoke + DataTable verifier PASS.
- Build: ProcurementService + frontend + gateway 0-error.

## 18. Ready-for-dev Checklist
- [ ] Golden Reference Compact okundu
- [ ] Frontmatter tam (service/shell/golden_reference/entity_base)
- [ ] Layout & Shell Contract Razor Layout açık
- [ ] Backend/Frontend file convention golden reference ile birebir
- [ ] Validation her field için yazılı
- [ ] Failure Path ≥4 senaryo (duplicate/missing/unauthorized/concurrency) + cross-tenant
- [ ] Authorization permission listesi + policy + actor
- [ ] Gateway routing kararı (integration-agent task)
- [ ] Acceptance test edilebilir + G2A zincirinde supplier upstream rolü
- [ ] Test expectations build/verifier/RESX/smoke/contract-mock kapsıyor
- [x] DCP-010 approved (Ali, 2026-09-16) + bu pack ready-for-dev onayı (kod kapısı AÇIK)

## 19. Implementation Notes
İlk scaffold edilecek modül (DCP-010 §8 sıra: 0140→0145→0141→0142→0143→0144). Servis scaffold bu pack + `@orchestrator /add-module` ile doğar (JWT + Mongo V3 GUID + TenantId izolasyonu + port 5062 + gateway route). SUPPLIER contract producer surface `x-producer-status: REVIEW` — freeze owner+consumer review sonrası; MVP-6 slice superset korunur.

## 20. Follow-up Items
- Supplier performance/risk (0147) ve portal (0148) → MVP-6 (bu modül değil).
- Supplier bank/payment detayları → Finance/Treasury sınırı (EA-TBD).
- Dış ERP supplier senkron adapter (SourceSystem/ExternalRef; DEC-INV-19 deseni) → follow-up.
