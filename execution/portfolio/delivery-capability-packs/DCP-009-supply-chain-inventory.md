---
id: DCP-009
slug: supply-chain-inventory
name: Supply Chain Inventory & Execution
type: Delivery Capability Pack
standard: CAP-001
status: approved
status_note: "approved (CT 2026-09-11, per CAP-001 §5). Owner decisions RESOLVED (scope=Inventory+Warehouse; service=Diten.SupplyChainService; port=5061). runtime_code_allowed=false: DCP approval alone does NOT authorize code — each member module pack must reach its own ready-for-dev gate first (CAP-001 §7). Member module packs: nine written under execution/domains/supply-chain-execution/module-packs/ — seven ready-for-dev (MOD-0183…0187, 0190, 0192), two draft (MOD-0147, MOD-0148); measured 2026-10-05, Q450 (each team's local CT authors its own)."
approved_by: control-tower
approved_on: 2026-09-11
owner_domain: supply-chain-execution
owner: supply-chain-execution / control-tower
created: 2026-09-11
authoring_branch: feature/inventory
canonical_source: "docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.1.xlsx#Blueprint_Data (Supply Chain Execution Suite + Planning + Manufacturing)"
canonical_modules: [MOD-0173, MOD-0174, MOD-0175, MOD-0176, MOD-0177, MOD-0178, MOD-0180, MOD-0181, MOD-0182, MOD-0188, MOD-0189, MOD-0190, MOD-0191, MOD-0192, MOD-0193]
runtime_code_allowed: false
runtime_code_scope: "MVP-6 runtime code EXISTS (status measured 2026-10-05, Q450; record docs/records/audits/2026-10/mvp6-q450-dcp009-status-01/). Seven member module packs are ready-for-dev: MOD-0183, 0184, 0185, 0186, 0187, 0190, 0192 (each pack's frontmatter). Diten.SupplyChainService is in HEAD (363 tracked files). Five module manifest providers are registered in services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs:97-106 (Shipments 0183, Returns 0186, Carriers 0184, Loads 0185, Claims 0187). The same five modules have a tenant UI: 33 Razor views under frontend/Diten.Web/Views/SupplyChain/ (Shipments 10 and Returns 6 committed; Carriers 6, Loads 5, Claims 6 in the working tree only). Golden flows recorded live on the branch: Shipments docs/records/audits/2026-10/mvp6-q366-scope-chain-registration-01/ §D (real tree; Q362 had run it on a counterfactual stack only), Returns mvp6-r2-returns-ui-01/, Carriers mvp6-r4a-carriers-ui-01/, Loads mvp6-r4b-loads-ui-01/, Claims mvp6-r4c-claims-ui-01/ (the last three untracked at this writing). They are pre-integration evidence: Q435 (mvp6-q435-merge-origin-main-01/, untracked) did not perform the origin/main merge and classes all five as VALID_PRE_MERGE / INTEGRATION_STALE; no integrated PASS exists. S&OP (0190) and Capacity (0192) are ready-for-dev but not composed in Program.cs and have no UI. Agent records, not CT acceptance. MVP-6-FIRST decision (user 2026-09-15): MVP-6 is the first lane to code, contract-first vs mocks. Diten.SupplyChainService scaffold trigger = an approved MOD-0183 module pack + @orchestrator /add-module (was MOD-0173; OD-4 resolved). MVP-1 (0173) later joins the same service. runtime_code_allowed flips per-module as each module pack reaches ready-for-dev."
inputs:
  - "docs/analysis/inventory-capability-scope-and-dependency-report.md (v2.1 decision-complete, 19 DEC-INV)"
  - "docs/analysis/contracts/*.openapi.yaml (8 frozen Wave-0 contracts)"
  - "execution/domains/supply-chain-execution/domain-config.md"
  - "execution/registries/module-id-registry.md (MOD-0173…0197 rows)"
  - ".antigravity/scripts/verify_module_id.py (DCP-002 gate — MOD-0173/0174/0178 exit 0)"
  - ".antigravity/rules/capability-pack-standard.md (CAP-001)"
---

# DCP-009 — Supply Chain Inventory & Execution (Delivery Capability Pack)

> **Artifact guard.** CAP-001 Delivery Capability Pack — governance/orchestration contract. **NOT** a runtime entity, **NOT** a Module Pack, **NOT** a Capability Group.
> **`status: approved`** (CT 2026-09-11). Kararlar (scope/service/port) alındı. **`runtime_code_allowed: false`** — DCP onayı TEK BAŞINA kod yetkilendirmez (CAP-001 §7); her üye module pack kendi `ready-for-dev` kapısından geçmeli.
> **Identity guard.** Yeni `MOD-xxxx` basmaz. Üyeler Blueprint-canonical MOD'lardır; her biri kendi module pack'i yazılırken DCP-002 preflight'ından geçer.

## 1. Identity and status
| Field | Value |
|---|---|
| ID | DCP-009 · slug `supply-chain-inventory` |
| Type | Delivery Capability Pack (CAP-001) |
| Status | **approved** (CT 2026-09-11; runtime_code_allowed=false) |
| Owner domain | supply-chain-execution |
| Canonical source | Blueprint 8.1 (Supply Chain Execution Suite + Planning + Manufacturing) |
| Service | `Diten.SupplyChainService` · port **5061** |
| runtime_code_allowed | **false** (DCP-level value, unchanged here; seven member packs are ready-for-dev and MVP-6 code exists — see `runtime_code_scope`, measured 2026-10-05, Q450) |

## 2. Business outcome
Denetlenebilir tek stok gerçeği + izlenebilirlik + kalite-kontrollü serbest bırakma + FEFO + recall + depo yürütme. GxP/pharma uyumlu, çok-tüzel-kişilikli, dış-sisteme entegre olabilir stok platformu.

## 3. Problem statement
vNext'te inventory YOK (greenfield). Mevcut `InventoryGovernanceController` hardcoded shell (K1). Blueprint modülleri (0173-0197) registry'de değil, domain yok, servis yok. Bu pack o governance boşluğunu kapatır.

## 4. Capability boundary
**Owns:** stok balance/valuation (0173) · lot/serial (0174) · quarantine (0175) · FEFO (0176) · recall (0177) · warehouse (0178-0182) · planning (0188-0192) · BOM/mfg (0193-0197) · logistics (0183-0187).
**Consumes (reference):** MOD-0290 (MDM item), MOD-0172 (Commercial ATP), Location master (DEC-INV-03), MOD-0142 GRN, backbone.
**Does NOT own:** product identity, ATP decision, procurement transaction, physical-location ownership decision (open).

## 5. Member modules and follow-ups
MVP-1 core: **0173 · 0174 · 0175 · 0176 · 0177** (+ Location master, DEC-INV-03). Warehouse: 0178/0180/0181/0182. Planning: 0188-0192. Manufacturing: 0193-0197. Logistics: 0183-0187. Her üye kendi module pack + FU'larıyla açılır (bu pack basmaz).

## 6. Ownership map
SoR ownership = rapor §3 SoR matrix. Özet: her obje tek owner; balance→0173; identity→0290(MDM); allocation→0172(Commercial); location→TBD master.

## 7. Dependency graph
Rapor §2 + §15 (full block dependency). Kritik: `0290+Location → 0173 → 0174 → {0175∥0176∥0177}` → G1 → Procurement∥BOM∥Warehouse.

## 8. Ordered delivery sequence
Rapor §10 + §13: W0 backbone → W1A 0290 identity → W1B 0290 hardening → W2 Location → W3 **0173** → W4 0174 → W5-7 quarantine/FEFO/recall → W8 warehouse. Downstream MVP-2..6 (§13.5).

## 9. Prerequisites
Backbone (0040/0018/0021/0023/0048/0029, Event Bus, Gateway) mevcut. MOD-0290 (MDM) hardening (materialType/UoMConv/GTIN/shelf-life/Lot-Serial). Location master kararı (DEC-INV-03). Contract'lar frozen (8 OpenAPI).

## 10. Architecture decisions
19 DEC-INV (rapor §9). Öne çıkanlar: transaction-first + balance-projection (02/15) · Tenant+LE scoping (18) · vNext SoR + dış entegrasyon (19) · costing per-LE/product (06) · MovementType canonical (16) · Model A identity (17) · shared Location master (03).

## 11. Scope
Pharma/GxP + Manufacturing (DEC-INV-11). Inventory + Warehouse çekirdek (DEC-INV-01). Planning/mfg/logistics domain'de rezerve, sonraki wave.

## 12. Explicit exclusions
Model B (Product/mdm_products) — dokunulmaz (DEC-INV-17). ATP ikinci balance. Procurement/AP/payment (Treasury). Product identity (MDM). External-integration adapter (follow-up slice).

## 13. Governance drift risks
Registry↔code drift (K11); iki paralel product SoR (Model A/B, FK yok — dual-SoR); shadow stock riski (warehouse/procurement); contract churn Faz-0 sonrası (K16).

## 14. Review questions
Location master owner (Option A/B alt-kırılım)? BOM'un pharma composition'a göre sırası (rapor §23.5)? MVP-1 kaç module pack'e bölünür? Servis scaffold zamanı?

## 15. Gate criteria
Rapor §20 gate checklist (G1-G5). G1 = 0290 identity + 0173 movement/availability freeze + data-quality (tek SoR). Her gate CT acceptance (agent PASS ≠ CT ACCEPTED, K13).

## 16. Acceptance criteria
DCP `approved` = kullanıcı onayı + üye module pack'ler kendi kapılarından geçmeye hazır. Her modül DoD = rapor §20 ilgili gate + evidence E4/E5.

## 17. Downstream business-module impacts
Procurement (0142 GRN → 0173 post) · Commercial ATP (0172 ← 0173 availability) · MRP (0189 ← 0173) · Finance (valuation → GL) · MDM (0290 hardening consumer).

## 18. Open decisions
**OD-1** Location master owner alt-kırılım (Option A split vs B shared — B seçildi, MOD-# reservation + collision-check pending). **OD-2** BOM (0193) sırası — pharma composition foundation'a yakın (rapor §23.5). **OD-3** MVP-1 module pack bölünmesi (tek pack mi FU'lar mı). **OD-4 RESOLVED (user 2026-09-15):** MVP-6-first. Servis scaffold tetik = onaylı MOD-0183 module pack + `@orchestrator /add-module`; MVP-1 (0173) sonra aynı servise katılır. MVP-6'nın consumed seam'leri (WAREHOUSE-OUTBOUND sahibi MVP-5, SUPPLIER sahibi MVP-2) merkezi CT tarafından öne çekilip frozen edildi (`docs/analysis/contracts/`), böylece MVP-6 mock'a karşı beklemeden geliştirir.

## 19. Future follow-ups
External-integration adapter (DEC-INV-19) · special-stock/ownership (P2) · material ledger/costing advanced · Transport/TMS (MVP-6) · S&OP (MVP-6).

## 20. Audit and reconciliation notes
DCP-002 preflight: MOD-0173/0174/0178 exit 0 (proven). Registry rows added 2026-09-11 (identity-only). Reconciliation `reconciled` statüsünde doldurulacak. Contract'lar `docs/analysis/contracts/` — frozen-ready, owner+consumer review pending.

## 21. Follow-up — Supply Chain module self-registration foundation

Approved: `docs/records/decisions/2026-09/mvp6-self-registration-patches-signoff-owner-decision-01.md`

**Authority:** `docs/records/decisions/2026-09/mvp6-self-registration-design-owner-decision-01.md` (D1–D5 = A; design for pack/DCP preparation only).
**Design package:** [`mvp6-self-registration-prep-01`](../../../docs/roadmap/plans/mvp6-self-registration-prep-01/README.md) (MANIFESTS.md, NAV-L10N-KEYS.tsv, TEST-PLAN.md, OWNED-PATHS.md, EFFORT.md).
**Standard:** `.antigravity/rules/module-self-registration-standard.md`. Answers compliance finding AG-01: `services/Diten.SupplyChainService` has no `ModuleManifestProvider`.

This section is a **specification**. It authorizes no code, `Program.cs`, `.csproj`, appsettings, `SharedResource`, Platform or gateway change, no new permission key or ID, and no commit, push or stash. Code belongs to the single CT-appointed integration owner once an integrated target (Q14/Q15) exists.

### 21.1 Identity (D3 = A)

| Field | Value |
|---|---|
| Domain | `SupplyChainExecution` (new in the Platform catalog; `Nav.Domain.SUPPLYCHAINEXECUTION` becomes mandatory with the first provider) |
| Service | `DitenSupplyChainService` |
| ModuleCodes | `shipment-tracking-pod` (MOD-0183) · `carrier-management` (MOD-0184) · `routing-load-planning` (MOD-0185) · `reverse-logistics` (MOD-0186) · `claims-management` (MOD-0187) · `sop-workflow-signoffs` (MOD-0190) · `capacity-planning` (MOD-0192) |
| Common manifest values | `ModuleVersion` `1.0.0`, `IsTenantAssignable` true, `IsBaseline` false; every RoutePath starts with `/SupplyChain/` → Tenant scope |
| Excluded | None. MOD-0190 S&OP and MOD-0192 Capacity were excluded (no UI scope, no manifest) until their UI scope (pack §23) and self-registration (pack §24) were approved: `docs/records/decisions/2026-09/mvp6-sop-capacity-ui-pack-signoff-owner-decision-01.md` (SHA-256 `37f3ff0d08f52391e2f8e605917874c41146b1eba91e928f4b65b2fb18f80bea`). The design package `MANIFESTS.md` predates this and is not changed; for these two modules the pack §24 controls. |

Each module's pages, actions, permission keys and nav keys are defined in that module pack's "Self-registration" section, not here.

### 21.2 Shared foundation (one capability-level item, single integration owner)

| Part | Path (repo-relative, proposed) | Specification |
|---|---|---|
| Manifest interface | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/ModuleRegistration/IModuleManifestProvider.cs` | Returns one `ModuleManifestDocument` (from `Diten.BuildingBlocks.ModuleRegistration.Abstractions`) per module |
| Options | `…/ModuleRegistration/PlatformRegistrationOptions.cs` | `SectionName = "PlatformRegistration"`; `BaseUrl`, `InternalApiKey`. No per-service credential fields (D2 = A) |
| Registration hosted service | `…/ModuleRegistration/ModuleRegistrationHostedService.cs` | On start, POSTs every registered provider's manifest to Platform `/api/internal/module-catalog/register-manifest` with header `X-Internal-Api-Key` (existing legacy path, checked against `AuthService:InternalApiKey`; no Platform change). Fails closed (log, no request) when `BaseUrl` or key is empty; never blocks startup; 5xx / connection refused → retry with backoff through a testable delay seam; 4xx → stop; one provider's failure does not block the others. Pattern: MDM / DevEnablement services and the unapproved Carrier candidate |
| Project reference (SPECIFICATION only) | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Diten.SupplyChainService.Api.csproj` (preimage in the common checkout `538e96c6…`) | `<ProjectReference Include="../../../Diten.Building.Blocks/src/Diten.BuildingBlocks.ModuleRegistration.Abstractions/Diten.BuildingBlocks.ModuleRegistration.Abstractions.csproj" />` |
| `Program.cs` lines (SPECIFICATION only) | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` (common checkout `7fdb5ef0…` is an older baseline; the preimage is re-taken from the integrated target) | `builder.Services.Configure<PlatformRegistrationOptions>(builder.Configuration.GetSection(PlatformRegistrationOptions.SectionName));` · one `builder.Services.AddSingleton<IModuleManifestProvider, …ManifestProvider>();` per **shipped** module (§21.4) · `builder.Services.AddHostedService<ModuleRegistrationHostedService>();` — each line exactly once |
| Settings (SPECIFICATION only) | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/appsettings.json` (`8bd8b168…`) | `"PlatformRegistration": { "BaseUrl": "", "InternalApiKey": "" }` — empty in the base file, local values only in a Development file, no secret committed |
| Hosted-service tests | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/ModuleRegistration/ModuleRegistrationHostedServiceTests.cs` | F-01…F-06 below |

### 21.3 Foundation tests (TEST-PLAN.md §1)

| ID | Pass condition |
|---|---|
| F-01 | With N registered providers, exactly N POSTs to `/api/internal/module-catalog/register-manifest`, one per ModuleCode |
| F-02 | Missing `BaseUrl` or key: no request, a warning is logged, startup is not blocked |
| F-03 | Provider 1 gets 500×5 and provider 2 gets 200 → provider 2 is still registered |
| F-04 | 5xx / connection refused → retry with backoff (fake delay); 4xx → stop without retry |
| F-05 | `X-Internal-Api-Key` present; no credential headers (D2 = A) |
| F-06 | The host resolves every provider and the hosted service; the `Program.cs` lines exist exactly once |

Shared guards and reconcile-state tests that land with the first module (integration owner): W-01 view routes derived from `SupplyChain*Controller`, W-02 Razor `Perms.Has` keys = manifest keys (minus the allow-listed conjunction keys), W-03 existing `NavManifestL10nGuardTests` green without edits, W-04 cross-module uniqueness; R-01 Platform reconcile fixture (push A then B → pruned), R-02 runtime restart idempotence, R-03 409 `MODULE_MANAGED_BY_CODE`, R-04 nav smoke in 7 cultures. No module counts as closed without them.

### 21.4 Ship rule (D4 = A) and sequencing

- The foundation lands with the **first** module whose UI reaches the integrated target, together with `Nav.Domain.SUPPLYCHAINEXECUTION` in all seven languages.
- Each provider, its `AddSingleton` line and its `Nav.Module.*` / `Nav.Page.*` keys ship **with that module's UI**, never ahead of it: `NavManifestL10nGuardTests` parses every `*ManifestProvider.cs` under `services/`, so a provider without its keys fails `dotnet test`, and a provider without its UI registers dead routes that reconcile would later prune.
- MOD-0185 ships only after the Loads UI is approved and built.
- Effort reference: 38 / 70 / 127 person-hours (O/M/P, D2 = A; EFFORT.md). Not yet in the CT effort ledger.

### 21.5 Open gaps (carried, not solved)

1. UI source is not in the common checkout (Shipment in the A12 successor archive; Carrier v2 source not archived; Loads HELD; Returns/Claims approved, not built).
2. `ReturnPermissions.cs` and `ClaimPermissions.cs` exist only in the accepted isolated source.
3. Returns target keys are MAP-ONLY (`ReturnPermissions.ForTarget`); D5 = A — tests reflect the mapping, no Returns backend change.
4. The single-key action model cannot express the `supplychain.returns.transition` and `supplychain.shipments.read` (G-SHIPREAD) conjunctions; the backend and UI keep enforcing them.
5. The Shipment RoutePath keeps the `:guid` constraint verbatim; the platform normalizer's handling is confirmed by R-01.
6. Supply Chain is a new domain in the Platform catalog.
7. Guard coupling: a provider merged without its seven-language keys fails `NavManifestL10nGuardTests`.
8. Reference inconsistencies noted, not changed: `GoldenSlimManifestProvider` uses the uppercase ModuleCode `GOLDENSLIM`; `add-module.md` names `NavL10nContractTests`.
9. Runtime tests R-02…R-04 need a native executor and the integrated target (Q14/Q15).
