---
id: MOD-0176
name: Expiry / FEFO Management
domain: supply-chain-execution
service: Diten.InventoryService (5067, in-process with the MOD-0173 ledger, MOD-0174 lots and MOD-0175 quarantine — domain-config OD-5 / OD-P1) + frontend/Diten.Web
shell: tenant
golden_reference: compact
data_mode: server
screens:
  - module: ExpiryDashboard
    data_mode: server
  - module: ExpiryApproaching
    data_mode: server
  - module: ExpiredStock
    data_mode: server
  - module: FefoOrder
    data_mode: server
  - module: ExpiryExceptionInbox
    data_mode: server
  - module: ExpiryPolicy
    data_mode: server
  - module: ExpiryEvidence
    data_mode: server
entity_base: EntityBase
status: ready-for-dev
status_note: "ready-for-dev — WP-0176-P0 CT-ACCEPTED 2026-10-12 (CT decisions CT-F1…CT-F14 taken, §19.7). Was: draft — WP-0176-P0 (CT-MVP1-0176-P0 v1), 2026-10-12. Owner decisions KF-A…KF-H (2026-10-12) recorded in §19 as DECISIONS. Open CT decisions CT-F1…CT-F14 (§19). FEFO-BUNDLE v1 proposal: docs/analysis/contracts/drafts/fefo-bundle-v1-proposal.md (no FEFO contract exists — K12). Additive contract needs on other owners (INVENTORY 1.3.0, TRACE 1.4.0) are proposals in §8.9, not edits. DCP-002 verifier not run (no Python — waiver W-0176-01, W-0173-01 pattern)."
owner: supply-chain-execution / mvp-1 control-tower
branch: feature/sce/mod-0176-expiry-fefo
started: 2026-10-12
target: 2026-11-14
form_field_count: 11
dependencies: [MOD-0173, MOD-0174, MOD-0175, MOD-0022, MOD-0290, INVENTORY-BUNDLE-v1.2.1, TRACE-v1.3.0, QUARANTINE-v1.2.0, ESIGN-BUNDLE-v1.0.1, PRODUCT-MASTER-1.1.0-draft.11, DEV-0001]
---

# MOD-0176 — Expiry / FEFO Management

> **Identity (DCP-002):** `MOD-0176 · Expiry / FEFO Management` is a Blueprint module, registered `reserved / planned`
> (`execution/registries/module-id-registry.md:277`: "Blueprint W-4 (Inventory & Traceability). DCP-002 gate OK
> 2026-09-11. DCP-009."). Roster: `docs/analysis/inventory-capability-scope-and-dependency-report.md:93` ("Expiry / FEFO
> Management | W-4 | Core | expiry policies | YOK"). **Not re-measured:** no Python on the MVP-1 machine → waiver
> **W-0176-01** (§18), W-0173-01 pattern. No registry row is changed by this pack.
>
> **Authority:** this pack > `execution/domains/supply-chain-execution/domain-config.md` > `AGENTS.md` > `.antigravity/`.
> Owner decisions **KF-A…KF-H (2026-10-12)** are binding (§19.1). Where the mockup "FEFO / Son Kullanma Yönetimi" disagrees,
> KF-* wins (§21.3).
>
> **Contract (K12):** the report marks FEFO-BUNDLE "✓" (`…dependency-report.md:230`), but **no FEFO contract exists** in
> `docs/analysis/contracts/` (no root file, no draft, no README row — README `:9-25`). This WP writes the proposal
> `docs/analysis/contracts/drafts/fefo-bundle-v1-proposal.md`; it becomes `fefo.openapi.yaml` (FROZEN) only after CT
> acceptance (F1, single writer). Consumed frozen contracts are **not changed** by this WP: INVENTORY-BUNDLE 1.2.1
> (`inventory-bundle.openapi.yaml:9-10`), TRACE 1.3.0 (`trace.openapi.yaml:8-9`), QUARANTINE 1.2.0
> (`quarantine.openapi.yaml:12`), ESIGN-BUNDLE 1.0.1 (`esign.openapi.yaml:11`). The additive changes 0176 needs from the
> INVENTORY and TRACE owners are listed separately (§8.9 and proposal §9) and need the owners' single-writer step.
>
> **No BLOCKED condition.**
> - Every rule is additive on INVENTORY / TRACE (new optional request members, new optional response members, new 422/409
>   codes in the closed catalog by a new pack version — §8.9). No existing member, code or status changes meaning.
> - Every hook can run in-process in InventoryService: the posting pre-check, the in-transaction lot re-check and the lot
>   creation handler are all in the same process (`MovementMasterDataValidator.cs:198`, `InventoryLedgerRepository.cs:277-294`,
>   `CreateLotHandler.cs:108-124` — §8.3).
> - The mockup was read (decoded in scratchpad, treated as data).
> - **No new stock status and no new movement type** is needed: auto-quarantine posts the existing `QUARANTINE` status
>   (`inventory-bundle.openapi.yaml:334-336`); 0176 posts no movement of its own.
>
> **One behaviour of KF-C needs an explicit CT ruling (CT-F1):** "automatic quarantine" of a goods receipt below the minimum
> remaining shelf life means the server books the receipt into `QUARANTINE` although the caller asked for `AVAILABLE`.
> This pack argues it is additive (the rule only fires when `minRemainingShelfLifeDays` is set, a new PRODUCT-MASTER 1.1
> field, and the response says so), and gives the fully-additive fallback.

## 1. Module Summary

MOD-0176 is the **shelf-life policy and FEFO control layer on top of the stock ledger and the lot master**. It keeps no
balance and creates no lot (WP brief `docs/analysis/workpackages/WP-MVP1-traceability-quality.md:21-26`: MUST-NOT "stok
tutma; lot yaratma"). It makes no quarantine decision: when a rule says "quarantine", the stock enters MOD-0175's existing
quarantine path and 0175 decides.

What 0176 owns:
- **Expiry policy per company** (KF-A, KF-G): the "Yaklaşıyor" horizon (default 90 days), the "Kritik" horizon (default
  30 days), the three hook modes (KF-B, KF-C, KF-D), SLA hours per exception type, the expired-on-hand threshold. The
  policy is versioned, and every save is kept in history with a reason.
- **Three posting / lot hooks** inside InventoryService:
  - outbound FEFO check (KF-B): an issue from a lot that is not the first eligible lot needs a reason ≥ 10 and opens a
    `FEFO_BYPASS` exception;
  - goods-receipt minimum remaining shelf life (KF-C): a receipt below `minRemainingShelfLifeDays` is quarantined
    automatically and opens a `BELOW_MIN_REMAINING` exception;
  - lot-creation date override (KF-D): an expiry / retest date that differs from the suggestion (manufacturing date +
    `shelfLifeDays` / `retestDays`) needs a reason + MOD-0022 e-signature and opens an `EXPIRY_DATE_OVERRIDE` exception.
- **The expired-on-hand watch:** a sweeper opens an `EXPIRED_ON_HAND` exception for stock still on hand N days after expiry.
- **FEFO exception cases** (`FefoExceptionCase`): an in-module state machine (no MOD-0023 Workflow — §8.7), with SLA and
  policy-driven e-signature on resolution (KF-E).
- **Read models:** the panel KPIs, approaching / expired lists, the read-only **FEFO order** (KF-F) that 0178 / 0172 / 0188
  consume, and the evidence pack export.

MVP-1 chain: `0290 → 0173 → 0174 → {0175 ∥ 0176 ∥ 0177}` (DCP-009 `:63`). 0176 also needs MOD-0022 (signature API +
provider registry) and the PRODUCT-MASTER 1.1 shelf-life fields on `getSku` (`product-master-bundle.openapi.yaml:729-732`).

### Slice plan (each slice = its own Work Package, CT-verified before the next)

| Slice | Scope | Contract | Closes |
|---|---|---|---|
| **F1 — Policy, exception cases, contract** | `ExpiryPolicy` (+ history, If-Match, reason), `FefoExceptionCase` + events (insert-only), state machine, SLA, manual open, take, comment, resolve (signature verification stubbed behind a port — F3 fills it), close; number sequence `FX-YYYY-NNNN`; `ExpiredOnHandSweeper`; SKU shelf-life parse (`SkuReference` + `TraceSku` gain `ShelfLifeDays`, `RetestDays`, `MinRemainingShelfLifeDays`); manifest `EXPIRY-FEFO-MANAGEMENT`; freeze `fefo.openapi.yaml` v1.0.0. | FEFO-BUNDLE v1 | KF-A, KF-E (state machine), KF-G, report §4 objects |
| **F2 — Hooks** | Pure Domain rules `FefoIssueRule`, `MinRemainingShelfLifeRule`, `LotDateSuggestionRule`; outbound pre-check + in-transaction re-check; GR auto-quarantine; lot-creation suggestion + override gate; `FefoIssueSnapshot` written in the posting transaction; exception opening in the same transaction. INVENTORY 1.3.0 + TRACE 1.4.0 additive (single-writer step of each owner). | INVENTORY 1.3.0, TRACE 1.4.0 (§8.9) | KF-B, KF-C, KF-D (reason part), CT-Q17 kept |
| **F3 — e-signature** | Signable-action registration (module `expiry`), subject provider (`expiry.exception` field list `fx-v1`, `expiry.date-override` field list `do-v1`), signature verification on resolve and on lot-date override (direct signature, not E4 — §8.6). | ESIGN-BUNDLE §4.23, §6 (consumer) | KF-D (signature part), KF-E |
| **F4 — Panel, lists, FEFO order (UI)** | FEFO Paneli, Son Kullanma Yaklaşanlar, Süresi Dolmuş Stok, FEFO Sırası (read-only, KF-F). | — | mockup 1:1 except §21.3 |
| **F5 — Exception inbox, detail, settings, evidence (UI) + bands** | İstisna Kutusu, İstisna Detayı (+ decision drawer two-step + MOD-0022 dialog), Raf Ömrü Kuralları ve Ayarlar (+ history), Kanıt Paketi (+ export log); the three bands on other screens (movement form outbound + GR, lot create / quick-create). Remove the fake `InventoryGovernance` screen (CT-F12). | — | mockup 1:1 except §21.3; 375 px measurement (§21.3 D-9) |
| **INT-EXPIRY** | Gateway `/api/expiry` + `/api/expiry/{everything}` → 5067 + route coverage test (integration-agent). | — | external consumers (Platform e-sign provider calls, 0178 / 0172 / 0188) |
| **L10n** | 7 languages (en, tr, fr, es, zh, ar, ru) for every key of §11.6, exception type / status / resolution names, e-sign action names (SevenNames). | — | language parity |

Order: **F1 → INT → F2 → F3 → F4 → F5 → L10n**. F4 may start once F1 is accepted (contract mock). L10n runs per file with
F4 / F5. F2's INVENTORY / TRACE edits are each owner's single-writer step (MOD-0173 / MOD-0174 owners — in practice the
same InventoryService WP, but a separate commit per contract).

Dependencies on other modules:
- F1 needs PRODUCT-MASTER 1.1 `getSku` shelf-life fields **live** in MDM (draft.11 declares them,
  `product-master-bundle.openapi.yaml:729-732`); until then every rule reads `null` → "Tanımsız" (rule not applied).
- F3 needs MOD-0022 E2 (signature API + provider registry) live. No E4 dependency (§8.6).
- F2 GR auto-quarantine needs MOD-0175 Q1 intake (`QuarantineIntake.cs:41-65`) — present on main.

## 2. Ownership and Boundaries

### Owns
- Expiry policy per company (horizons, hook modes, SLA, thresholds) and its history (report `:185-186` `ExpiryPolicy`,
  `ShelfLifeRule` — see §3 on the name).
- FEFO exception cases (`FEFOExceptionCase`, report `:186`) and their events.
- The FEFO allocation **constraint as a read** (`FEFOAllocationConstraint`, report `:186`; SoR row `:148`) — in MVP-1 the
  read-only FEFO order (KF-F), consumed by 0178 / 0172 / 0188 (report `:123`, `:230`).
- The FEFO issue snapshot (for the compliance KPI) and the evidence export log.
- The three hook rules (pure Domain functions) — called by the ledger and the lot handler, but owned here.

### Does not own
| Thing | Owner | How 0176 touches it |
|---|---|---|
| Stock balance, movements, reservations | MOD-0173 (report `:143`) | reads balances by lot; hooks run inside the 0173 posting path; posts nothing |
| Lots and their dates | MOD-0174 (report `:147`) | reads lots; suggestion + override gate inside `createLot`; never writes a lot field itself |
| Shelf-life master data (`shelfLifeDays`, `retestDays`, `minRemainingShelfLifeDays`) | MOD-0290 (report `:146`) | reads `getSku` (no local copy — domain-config `:38`) |
| Quarantine holds and release decisions | MOD-0175 | auto-quarantine books the receipt into `QUARANTINE`; 0175's intake opens the hold; 0175 decides |
| Signatures, meanings, signature policy | MOD-0022 | registers `expiry.*` actions, provides subjects, verifies `signatureId`; keeps **no** signature record (KF-E) |
| Lot-based reservation / pick allocation | MOD-0178 (KF-F) | FU-0176-RES-01 |
| Product-level / market / customer shelf-life rules | — (KF-G, KF-H) | FU-0176-PROD-01, FU-0176-MKT-01 |

### Boundary rules
1. 0176 never writes a balance row, a movement, a lot, a hold or a signature.
2. 0176 rules are called **in-process** by the posting path; they decide "allow / allow + exception / refuse /
   re-route to `QUARANTINE`". They do not post a second movement.
3. Every 0176 read is tenant + company scoped. `LotExpirySweeper` is global (`LotExpirySweeper.cs:40-47`, no tenant
   filter); 0176's own sweeper is tenant + company scoped by construction (§8.5).

## 3. Owned Objects

| Object (report name) | Pack entity | Collection | Notes |
|---|---|---|---|
| `ExpiryPolicy` + `ShelfLifeRule` | `ExpiryPolicy` | `expiryPolicies` | One current row per tenant + company, versioned. `ShelfLifeRule` (report `:186`) is **not** a separate entity in MVP-1: the shelf-life numbers live on the SKU (MOD-0290, report `:146`), the rules that use them are the policy modes (KF-G: company scope only). |
| — | `ExpiryPolicyHistory` | `expiryPolicyHistory` | Insert-only, one row per changed field (mockup L:95, settings history). |
| `FEFOExceptionCase` | `FefoExceptionCase` | `expiryExceptions` | Mutable head with `Version`; state machine §8.4. |
| — | `FefoExceptionEvent` | `expiryExceptionEvents` | Insert-only (opened, taken, commented, resolved, closed, cancelled, sla-breached). |
| `FEFOAllocationConstraint` | `FefoIssueSnapshot` (+ the computed FEFO order, not stored) | `fefoIssueSnapshots` | Written in the posting transaction for every in-scope issue: issued lot, first eligible lot, compliant flag. Source of the compliance KPI. |
| — | `LotDateOverride` | `expiryDateOverrides` | The pending signable subject of KF-D (the lot does not exist yet when the user signs — §8.6). |
| — | `ExpiryEvidenceExport` | `expiryEvidenceExports` | Export log (at, who, scope, file name, SHA-256 of the file). |
| — | number sequence | `expiryCounters` | `FX-YYYY-NNNN` per tenant (mockup id format). |

## 4. Entity Fields

All entities derive from `EntityBase` (Id, TenantId, CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy, IsDeleted).
Every entity carries `LegalEntityId` (company scope, W-0173-02).

### 4.1 `ExpiryPolicy`
| Field | Type | Rule |
|---|---|---|
| LegalEntityId | Guid | unique per tenant (active row) |
| ApproachingDays | int | 1–730, default **90** (KF-A) |
| CriticalDays | int | 1–730, `< ApproachingDays`, default **30** (KF-A) |
| FefoOutboundMode | enum `Warn | RequireReason | Block` | default **RequireReason** (KF-B) |
| BelowMinRemainingMode | enum `Warn | AutoQuarantine | Reject` | default **AutoQuarantine** (KF-C) |
| DateOverrideMode | enum `Reason | ReasonAndSignature` | default **ReasonAndSignature** (KF-D; CT-F6) |
| SlaHours | map type → int | `FEFO_BYPASS` 24, `BELOW_MIN_REMAINING` 72, `EXPIRY_DATE_OVERRIDE` 48, `EXPIRED_ON_HAND` 120 (mockup L:95 — ASSUMPTION-0176-03); 1–2160 |
| ExpiredOnHandThresholdDays | int | 0–365, default 14 (mockup L:95 — ASSUMPTION-0176-04) |
| Version | long | optimistic concurrency (If-Match) |

No row stored = defaults apply (the read returns `isDefault: true`); the first save creates the row.

### 4.2 `ExpiryPolicyHistory`
`PolicyId, LegalEntityId, Field, OldValue, NewValue, Reason (10–1000), ChangedBy {userId, name}, ChangedAtUtc, PolicyVersion`.

### 4.3 `FefoExceptionCase`
| Field | Type | Rule |
|---|---|---|
| Number | string | `FX-YYYY-NNNN` |
| Type | enum `FEFO_BYPASS | BELOW_MIN_REMAINING | EXPIRY_DATE_OVERRIDE | EXPIRED_ON_HAND` | mockup L:7 |
| Origin | enum `System | Manual` | Manual = "İstisna aç" |
| Status | enum `Open | InReview | Accepted | Fixed | Closed | Cancelled` | mockup L:8; §8.4 |
| Resolution | enum `AcceptedWithReason | Fixed | Cancelled` \| null | mockup L:9 |
| Item / Sku | `ItemId, SkuId, SkuLevel` + display snapshot (code, name) | |
| Lot | `LotId, LotNumber, ExpiryDate, RetestDate` (snapshot at opening) | |
| Warehouse | `WarehouseId` + code/name snapshot | null for `EXPIRY_DATE_OVERRIDE` |
| Quantity, BaseUomId | decimal, Guid | the issued / received / on-hand quantity |
| SourceMovement | `TransactionId, MovementType, SourceModule, SourceDocumentId` \| null | hook-opened cases |
| Facts | map string → string | per type (mockup L:278-282): FEFO_BYPASS → firstEligibleLot, its expiry, bypass reason; BELOW_MIN → remaining days, required days, applied status; OVERRIDE → suggested / entered date, reason, signatureId; EXPIRED_ON_HAND → days since expiry |
| OpenedAtUtc, OpenedBy | | `system` for hooks / sweeper |
| DueAtUtc | DateTime | `OpenedAtUtc + SlaHours[type]` (policy at opening) |
| AssigneeUserId | Guid \| null | set by "İncele" (take) |
| ResolutionReason | string 10–1000 \| null | |
| ResolutionSignatureId | string \| null | MOD-0022 reference only (KF-E) |
| ResolvedAtUtc / ResolvedBy, ClosedAtUtc / ClosedBy | | |
| Version | long | If-Match |
| IdempotencyKey | string | dedupe of manual opens |
| DedupKey | string | system opens: `{type}:{transactionId}` or `{type}:{lotId}:{warehouseId}` (sweeper) — unique among active cases |

### 4.4 `FefoExceptionEvent`
`CaseId, Seq, Action (Opened | Taken | Commented | Resolved | Closed | Cancelled | SlaBreached), Actor, AtUtc, Text, SignatureId?`.

### 4.5 `FefoIssueSnapshot`
`TransactionId (unique), LegalEntityId, WarehouseId, ItemId, SkuId, SkuLevel, MovementType, IssuedLotId, IssuedExpiry,
FirstEligibleLotId, FirstEligibleExpiry, Compliant (bool), Bypassed (bool), BypassReason?, ExceptionId?, PostedAtUtc,
PostingDate`.

### 4.6 `LotDateOverride`
`LegalEntityId, ItemId, SkuId, SkuLevel, LotNumber, ManufacturingDate, SuggestedExpiry?, EnteredExpiry?, SuggestedRetest?,
EnteredRetest?, Reason (10–1000), Status (Pending | Consumed | Expired), ConsumedByLotId?, ExpiresAtUtc (+24 h),
IdempotencyKey, Version`.

### 4.7 `ExpiryEvidenceExport`
`LegalEntityId, Period {from, to, preset q|d30|ytd}, Scope {warehouseIds, types}, Format (Csv | Pdf), FileName, Sha256 (of
the produced file bytes — mockup shortcut not copied, §21.3 D-8), RowCount, ExportedBy, ExportedAtUtc, DmsRef?`.

## 5. Repo Scope

Allowed (new files only, unless listed as a seam):
- `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Expiry/**` (entities, enums, pure rules)
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Expiry/**`
- `services/Diten.InventoryService/src/Diten.InventoryService.Persistence/Expiry/**`
- `services/Diten.InventoryService/src/Diten.InventoryService.Api/Expiry/**` (controller, DI extension, manifest provider)
- `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Authorization/ExpiryPermissions.cs`
- `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/ESign/**` (generalise registration — §8.6)
- `services/Diten.InventoryService/tests/**/Expiry/**`
- `frontend/Diten.Web/Controllers/ExpiryController.cs`, `frontend/Diten.Web/Views/Expiry/**`, `frontend/Diten.Web/wwwroot/assets/js/Expiry/**`, `Resources/**` (7 languages)
- `docs/analysis/contracts/fefo.openapi.yaml` (F1, after CT acceptance), README row

**Seams (existing files, minimal, listed exactly):**
| File | Line (measured) | Change | Slice |
|---|---|---|---|
| `…Application/…/MovementMasterDataValidator.cs` | after `:198-204` (`LotPostingRules.Evaluate`) | call `IExpiryPostingPolicy.PreCheckAsync` (§8.3) | F2 |
| `…Persistence/…/InventoryLedgerRepository.cs` | `ResolvePostingLotAsync` `:277-294` (re-check `:286`) | call the same pure rule inside the transaction + write snapshot / exception with the session | F2 |
| `…Application/…/InternalMovementPostingService.cs` | fingerprint `:79`; outcome mapping `:139-180` | include `fefoBypassReason` in the fingerprint; map new outcomes | F2 |
| `…/Traceability/Handlers/CommandHandlers/CreateLotHandler.cs` | after `:108-124` | suggestion + override gate | F2/F3 |
| `…/Traceability/Ports/ITraceSkuDirectory.cs` `:21-22`, `SkuReference.cs:31-38`, parsers `TraceSkuDirectoryHttpClient.cs:36-37`, `ProductMasterHttpClient.cs:55-76` | | add the three nullable day fields | F1 |
| `…Application/Common/InventoryErrorCodes.cs` | `All` set `:67-75` | new codes (with INVENTORY 1.3.0) | F2 |
| `…/Traceability/…/TraceErrorCodes.cs` | `:12-46` | new codes (with TRACE 1.4.0) | F2/F3 |
| `services/Diten.InventoryService/src/Diten.InventoryService.Api/Program.cs` | `:41-46` (manifest registrations) | register `ExpiryFefoManifestProvider` + `AddExpiry()` | F1 |
| `frontend/Diten.Web/Views/Inventory/StockMovements/_Form.cshtml` + `wwwroot/assets/js/Inventory/StockMovements/form.js` | lot block `:285`, retest warn pattern `:304-311`, `onLotChanged` `:315-326`, `check()` `:645-647` | FEFO / min-remaining bands | F5 |
| `frontend/Diten.Web/Views/Traceability/Lots/_Form.cshtml` `:132-151`, `Lots/form.js:184`, `_LotQuickCreateModal.cshtml:38-44` | | suggestion + override band | F5 |
| `frontend/Diten.Web/Controllers/InventoryGovernanceController.cs`, `Views/InventoryGovernance/**`, `_Layout.cshtml:217-222`, `ManagementGovernanceRegistry.cs:207` | | removal / redirect (CT-F12) | F5 |

## 6. Protected Paths

Not touched by any 0176 slice: frozen contracts other than by their owner's additive single-writer step
(`inventory-bundle.openapi.yaml`, `trace.openapi.yaml`, `quarantine.openapi.yaml`, `esign.openapi.yaml`,
`product-master-bundle*.yaml`); `ocelot*.json` (integration-agent only, domain-config `:65`); registry / DCP / domain-config
(proposals only, §20); MOD-0175 apply engine and decision code; `LotExpirySweeper` (read as a model, not changed);
Platform e-signature code; `AccountKindAcceptanceHost.Tests` (not run).

## 7. Dependencies

| Dependency | Kind | State (measured) | Used for |
|---|---|---|---|
| MOD-0173 ledger | HARD (in-process) | live | posting path, balances by lot (`getBalance` `lotId` `inventory-bundle.openapi.yaml:150`) |
| MOD-0174 lots | HARD base (report `:122`, `:399`) | live, TRACE 1.3.0 | lot dates, `listLots(expiryBefore, sort=expiryDate)` (`trace.openapi.yaml:64,72`), extension points (MOD-0174 pack `:265-266`, `:1037`) |
| MOD-0175 quarantine | HARD for KF-C | live (Q1–Q5), QUARANTINE 1.2.0 | intake opens hold on `QUARANTINE` GR (`QuarantineIntake.cs:41-65,109-115`) |
| MOD-0022 e-signature | HARD for F3 | ready-for-dev / E2 needed | `signSubject`, `getSignature` (`esign.openapi.yaml:111,154`; F3-A: not `verifySignature` `:176`), registration (`:552-571`), provider contract |
| MOD-0023 Workflow | **not used** | — | §8.7 (report never states Workflow HARD for 0176 — `:121`, `:399`, `:569`) |
| MOD-0290 product master | HARD data | 1.1.0-draft.11 (not frozen) | `getSku` `shelfLifeDays` / `retestDays` / `minRemainingShelfLifeDays` (`:729-732`) |
| MOD-0188 Demand | consumer only (report `:123`, `:569` "DEC: base=0174") | not started | — |
| MOD-0178 / 0172 | consumers of FEFO order | not started | FEFO-BUNDLE `getFefoOrder` |
| MOD-0183 shipment (MVP-6) | posts `GOODS_ISSUE_SALES` through the same path | integration open (`mvp1-mvp6-integration-2026-10-12.md:23,36`) | outbound hook applies unchanged (§8.3.1) |

## 8. Runtime Constraints

### 8.1 Measured baseline (what exists today)

- **Lot expiry is enforced at posting by date, not by sweeper.** `LotPostingRules.IsExpiredOn` (`LotPostingRules.cs:60`:
  Status Expired or expiry day < posting day) → `LOT_EXPIRED`; violations at `:8-25`; evaluated in
  `MovementMasterDataValidator.cs:198` and re-evaluated in the ledger transaction (`InventoryLedgerRepository.cs:286`).
- **CT-D4 exemptions** (`LotPostingRules.cs:42` `IsCtD4Exception`: Scrap, WriteOff, Reversal) — reused by 0176.
- **Held lots:** `HoldsStock` (`LotPostingRules.cs:50/53`: Blocked or Expired or `RetestHeldAtUtc` set); the SKU position
  is moved only when the lot does not hold stock (`InventoryLedgerRepository.cs:205`).
- **`LotExpirySweeper`** sets `Expired` once a day boundary passes: filter `ExpiryDate < startOfToday & Status != Expired`
  (`LotExpirySweeper.cs:40-47`, global), runs at start and every 900 s (`LotExpirySweeperHostedService.cs:37-38`,
  `LotExpirySweeperOptions.cs:8,10`). It does **not** move stock into the `EXPIRED` stock status
  (`BalanceBuckets.cs:18` groups Blocked/Quarantine/Damaged/Expired as Blocked; nothing posts to `EXPIRED`).
- **No FEFO logic exists** anywhere. Lot pickers sort by expiry (`frontend/Diten.Web/Controllers/LotsController.cs:304-315`,
  `LotFieldRules.cs:43-62`); TRACE `listLots` sorts by expiry and exposes `expiryState` with a fixed 90-day window
  (`trace.openapi.yaml:72,306`).
- **Reservations carry no lot** (`InventoryReservation.cs:11-51`; `ReservationRequest` `inventory-bundle.openapi.yaml:434-449`);
  the lot is chosen at issue.
- **`IPostedMovementObserver` cannot refuse** a posting (`IPostedMovementObserver.cs:7,10`); its only implementation is
  `QuarantineIntakeObserver` (`QuarantineIntake.cs:160-170`).
- **The error catalog is closed:** `InventoryErrorCodes.All` (`InventoryErrorCodes.cs:67-75`, "no other code without new
  pack version" `:11`; test `InventoryBundleDescriptionNoteTests.cs:118-120`).
- **Shelf-life fields are not parsed** today: `TraceSku` (`ITraceSkuDirectory.cs:21-22`) and `SkuReference`
  (`SkuReference.cs:31-38`) have none; PRODUCT-MASTER draft.11 declares them on `SkuRef` (`:729-732`, units = days,
  null = undefined `:708-710`).

### 8.2 FEFO order rule (KF-F read; KF-B check)

**Candidate set** = lots of the **same company + warehouse + SKU** (`LegalEntityId, WarehouseId, ItemId, SkuId, SkuLevel`)
with `AVAILABLE` on-hand > 0 in that warehouse (sum of balance rows over locations, `BalanceView.lotId`
`inventory-bundle.openapi.yaml:365-385`).

**Excluded lots, each with its reason** (shown in FEFO Sırası "Hariç tutulanlar"; mockup L:251-260 labels):
| Reason code | Test | Label (tr) |
|---|---|---|
| `HELD` | `LotPostingRules.HoldsStock(lot)` (`:50`) and not otherwise listed | Bloke |
| `EXPIRED` | `IsExpiredOn(lot, today)` (`:60`) — by date, even before the sweeper | Süresi doldu |
| `RETEST_DUE` | `IsRetestDueOn(lot, today)` (`:57`) | Retest tarihi geçti |
| `BLOCKED` | lot `Status = Blocked` | Bloke |
| `QUARANTINED` | lot has no `AVAILABLE` on-hand but `QUARANTINE` / `QUALITY_INSPECTION` on-hand (shown for context, quantity 0 eligible) | Karantinada |
| `BELOW_MIN_REMAINING` | `minRemainingShelfLifeDays` set and `expiry − today < min` | Minimum kalan raf ömrünün altında |

**Order** (eligible lots): `ExpiryDate` ascending, lots without expiry **last**; ties → `ManufacturingDate` ascending
(nulls last) → `RetestDate` ascending (nulls last) → `LotNumber` ordinal → `LotId`. The mockup has no secondary key
(§21.3 D-6); a deterministic order is needed for the read, but **compliance (§8.3.1) compares expiry dates only**, so the
tie-break never creates a bypass.

**Allocation** (optional `quantity`): greedy over the ordered eligible lots using `AVAILABLE` on-hand. Reservations are not
lot-bound, so the per-lot figure is on-hand, not "free" (the response says so: `basis: "onHandAvailable"`); the SKU-level
free quantity (`getAvailability`) is returned alongside.

### 8.3 Hook design

Common shape:
- **Pure Domain rules** (no I/O): `FefoIssueRule.Evaluate(issuedLot, candidates, policy, movement)`,
  `MinRemainingShelfLifeRule.Evaluate(lot, sku, policy, movement)`, `LotDateSuggestionRule.Evaluate(request, sku, policy)`.
- **Port** `IExpiryPostingPolicy` (Application, `…/Features/Inventory/Ports/`): `PreCheckAsync(ctx)` (outside the
  transaction, in `MovementMasterDataValidator` after `:198-204`) and `ReCheckAsync(ctx, session)` (inside the
  transaction, in `ResolvePostingLotAsync` after `:286`). Default implementation = "no policy" (allow), registered by
  InventoryLedger; 0176 replaces it (the `ILotStatusChangeGate` pattern: default `AlwaysAllowLotStatusChangeGate`, 0175
  replacement wired at `QuarantineServiceCollectionExtensions.cs:67-71`).
- **Why not `IPostedMovementObserver`:** it runs after commit and cannot refuse (`IPostedMovementObserver.cs:7,10`);
  Block / Reject and the auto-quarantine re-route must act **before** the write, and the exception + snapshot must commit
  **with** the movement (a crash between commit and observer would lose the exception).
- **Refusal** inside the transaction reuses `LotPostingRefusedException` → 422 (`InventoryLedgerRepository.cs:229-231`,
  service `:165-168`) with the new code.
- **Synchronous:** all three hooks are synchronous in the request. No queue.
- **Exempt movement types (CT-D4 + 0175):** `REVERSAL` (never reaches master data — `InternalMovementPostingService.cs:91,210-250`;
  also skipped in the re-check), `SCRAP`, `WRITE_OFF` (`LotPostingRules.cs:42`), `STATUS_TRANSFER`, `LOCATION_TRANSFER`,
  `COUNT_GAIN`, `COUNT_LOSS`, `SUPPLIER_RETURN` (+ `WAREHOUSE_TRANSFER`, CT-F3). MOD-0175's movements are exempt **by TYPE**: it posts
  only `STATUS_TRANSFER`, `SCRAP`, `SUPPLIER_RETURN`, `LOCATION_TRANSFER` (`QuarantineStockEffects.cs`, through
  `QuarantineMovementPoster.cs:43,103`). **`sourceModule` is caller data and never exempts** — CT finding O-F2-2, WP-0176-F2-FIX1
  (guarded by `ExpiryQuarantineTypeExemptionTests`).

#### 8.3.1 Outbound — FEFO check (KF-B)

| Item | Design |
|---|---|
| Movement types in scope | `GOODS_ISSUE_SALES`, `GOODS_ISSUE_PROD` from `AVAILABLE` with a `lotId` (CT-F3: `WAREHOUSE_TRANSFER`) |
| Class / line | pre-check: `MovementMasterDataValidator.cs` after `:198-204`; re-check: `InventoryLedgerRepository.ResolvePostingLotAsync` `:277-294` (after `:286`) |
| Compliant | no eligible lot (§8.2) in the same company + warehouse + SKU has an **earlier** `ExpiryDate` with `AVAILABLE` on-hand > 0. Equal expiry = compliant. Issued lot without expiry while an eligible lot with expiry exists = bypass. |
| Request member | `fefoBypassReason` (string 10–1000, optional) — INVENTORY 1.3.0 |
| Mode **Uyar** | post; snapshot `Bypassed=true`; exception `FEFO_BYPASS` opened (reason if sent, else "—"); response `expiryChecks.fefo = "Bypassed"` |
| Mode **Gerekçe iste** (default) | no / short reason → **422 `FEFO_BYPASS_REASON_REQUIRED`** (details: firstEligibleLotId, lotNumber, expiry); reason ≥ 10 → post + `FEFO_BYPASS` exception in the same transaction |
| Mode **Engelle** | **422 `FEFO_ORDER_VIOLATION`** (details as above); nothing posted, no exception (refusal is the control; audit row `expiry.fefo.refused`) |
| Idempotency | `fefoBypassReason` joins the request fingerprint (`InternalMovementPostingService.cs:79`); replay (`:198-208`) returns the first result and opens nothing; the exception dedup key is `FEFO_BYPASS:{transactionId}` |
| Race | between pre-check and commit another posting may change the candidate set; the in-transaction re-check uses the transaction's reads and is authoritative; a changed outcome refuses with the same 422 (caller retries) |
| Reservation effect | none: reservations hold no lot (§8.1); consumption by source triple (`ReservationPositionOperations.cs:215-275`) is unchanged |
| REVERSAL effect | a reversal of a bypassing issue is exempt and does not close the exception; QA closes it as "Düzeltildi" (detail shows the reversal link — CT-F4) |
| Snapshot | every in-scope issue writes `FefoIssueSnapshot` (compliant or not) in the transaction — KPI source |
| MVP-6 | the shipment issue (`GOODS_ISSUE_SALES`, `sourceModule=MOD-0183`, `mvp1-mvp6-integration-2026-10-12.md:36`) goes through the **same** `postMovement` path, so the same hook applies: the caller either picks the first FEFO lot (`getFefoOrder`) or sends `fefoBypassReason`. No separate path. |

#### 8.3.2 Goods receipt — minimum remaining shelf life (KF-C)

| Item | Design |
|---|---|
| Movement types in scope | `GOODS_RECEIPT_PO` into `AVAILABLE` or `QUARANTINE`/`QUALITY_INSPECTION`, with a lot that has `ExpiryDate` (CT-F5: `GOODS_RECEIPT_PROD`, `CUSTOMER_RETURN`) |
| Rule | `remainingDays = ExpiryDate.Date − PostingDate.Date`; violation when `remainingDays < sku.minRemainingShelfLifeDays` |
| `minRemainingShelfLifeDays` null | **rule not applied** (KF-C); product view badge "Tanımsız"; response `expiryChecks.minRemaining = "NotDefined"` |
| Lot without expiry | rule not applied ("SKT takibi yok"); ASSUMPTION-0176-06 |
| Class / line | same two points as §8.3.1 (the lot is known after `UNKNOWN_LOT` `:189-196`) |
| Mode **Uyar** | post as requested; `BELOW_MIN_REMAINING` exception |
| Mode **Otomatik karantina** (default) | **CT-F1 (fallback, decided):** requested `toStockStatus = AVAILABLE` → **422 `LOT_BELOW_MIN_REMAINING_SHELF_LIFE`** with `details.requiredToStockStatus = "QUARANTINE"`, `remainingDays`, `requiredDays`; the server **never turns the requested status** and nothing is posted; the caller (GR UI) resubmits into `QUARANTINE`. Requested `QUARANTINE` / `QUALITY_INSPECTION` → posted as requested, MOD-0175 intake opens the hold (Source `GoodsReceipt`, `QuarantineIntake.cs:41-65`), `BELOW_MIN_REMAINING` exception; response `expiryChecks.minRemaining = "Warned"`. No `appliedToStockStatus`. |
| Mode **Reddet** | **422 `LOT_BELOW_MIN_REMAINING_SHELF_LIFE`** (details remainingDays, requiredDays); nothing posted |
| Idempotency | fingerprint unchanged (no new request member); dedup `BELOW_MIN_REMAINING:{transactionId}` |
| Reservation effect | none (inbound) |
| REVERSAL | exempt; reversing the GR leaves the exception for QA |
| Why not post AVAILABLE then `quarantineStock` | two movements, a window where the stock is AVAILABLE and reservable → 0175 would answer 409 `QUARANTINE_STOCK_RESERVED` (`StockActionHandlers.cs:229`); measured path rejected |
| Fully-additive fallback | **chosen by CT-F1** (see Mode Otomatik karantina): the server-side re-route is not built. |

`HoldSource` has no expiry value (`quarantine.openapi.yaml:397`); 0176 needs none — the hold **is** a goods-receipt
hold. The exception links the hold (`facts.holdId`) by reading 0175's hold by source transaction. Optional QUARANTINE
1.3.0 `HoldRef.originReason` is CT-F9 (not required).

#### 8.3.3 Lot creation — date suggestion and override (KF-D)

| Item | Design |
|---|---|
| Class / line | `CreateLotHandler.cs` after the SKU read and checks `:94-124` (dates parsed at `:71-72`, `LotDateRules.Validate` `LotAttributes.cs:30-45`) |
| Suggestion | `suggestedExpiry = manufacturingDate + sku.shelfLifeDays`; `suggestedRetest = manufacturingDate + sku.retestDays` (getSku; `shelfLifeDaysSource` Own/Gsku irrelevant — effective value) |
| No suggestion | `manufacturingDate` null or the day field null → no suggestion, no exception (KF-D) |
| Differs | entered date ≠ suggested date (by day; empty entered while a suggestion exists = differs — ASSUMPTION-0176-07) |
| Mode **Gerekçe + e-imza** (default) | client first calls FEFO `POST /api/expiry/date-overrides` (reason, dates) → `overrideId`; signs subject `expiry.date-override/{overrideId}` in the MOD-0022 dialog; sends `createLot` with `dateOverride {overrideId, signatureId}` (TRACE 1.4.0). Missing → **422 `LOT_DATE_OVERRIDE_REQUIRED`**; signature missing → **400 `LOT_DATE_OVERRIDE_SIGNATURE_REQUIRED`**; wrong signer / subject / hash / meaning → **409 `LOT_DATE_OVERRIDE_SIGNATURE_MISMATCH`**; override does not match the request dates → 409 `LOT_DATE_OVERRIDE_MISMATCH` |
| Mode **Gerekçe** | `dateOverride {overrideId}` without signature |
| Effect | lot inserted (Draft, `:129-164`) and the override consumed **in one transaction**; `EXPIRY_DATE_OVERRIDE` exception opened with suggested / entered / reason / signatureId; TRACE history row `ExpiryOverridden` (1.4.0) |
| Idempotency | createLot's own key (`:193-195` 409 path unchanged); an override is consumed once (`Status=Consumed`, `ConsumedByLotId`) — a replay of the same createLot returns the first result |
| Quick-create | `_LotQuickCreateModal.cshtml:38-44` uses the same API → same rule |
| `updateLot` | out of KF-D's words (lot creation). **CT-Q17 kept unchanged** (`UpdateLotHandler.cs:24-28,81-84`: later retest on non-Draft → 400 `LOT_KEY_IMMUTABLE`). Whether a Draft `updateLot` of expiry also needs the override is **CT-F7** |
| Reservation / REVERSAL | n/a (no stock) |

#### 8.3.4 Hook table (complete)

| Hook | Uyar | Gerekçe | Engelle / Reddet | Otomatik karantina | Exception | HTTP |
|---|---|---|---|---|---|---|
| Outbound (KF-B) | post + exception | reason ≥ 10 → post + exception; else 422 | 422 `FEFO_ORDER_VIOLATION`, no post | n/a | `FEFO_BYPASS` | 201 / 422 |
| GR (KF-C) | post + exception | n/a | 422 `LOT_BELOW_MIN_REMAINING_SHELF_LIFE`, no post | into `AVAILABLE`: 422 + `requiredToStockStatus = QUARANTINE`, no post (CT-F1, the server never turns the status); into `QUARANTINE` / `QI`: post + 0175 hold + exception | `BELOW_MIN_REMAINING` | 201 / 422 |
| Lot creation (KF-D) | n/a | override (reason) → create + exception; else 422 | n/a | n/a | `EXPIRY_DATE_OVERRIDE` | 201 / 422 / 400 / 409 |
| Lot creation (KF-D) Gerekçe + e-imza | n/a | override + signature → create + exception; signature missing 400, mismatch 409 | n/a | n/a | `EXPIRY_DATE_OVERRIDE` | 201 / 422 / 400 / 409 |
| Exempt types (§8.3) | — | — | — | — | none | unchanged |

### 8.4 Exception case state machine (in-module)

```
Open ──take──▶ InReview
Open / InReview ──resolve(AcceptedWithReason)──▶ Accepted ──close──▶ Closed
Open / InReview ──resolve(Fixed)──────────────▶ Fixed    ──close──▶ Closed
Open / InReview ──resolve(Cancelled)──────────▶ Cancelled (terminal)
```
- Decision from `Open` directly is allowed (mockup).
- `resolve` needs reason ≥ 10 and, **when the MOD-0022 effective policy for the action says required**, a `signatureId`
  (KF-E). Default policy: all three required (`defaultRequired: true` at registration).
- `close` needs no signature (mockup "İstisnayı kapat"); permission `expiry.exceptions.close`.
- Every transition: If-Match, event row, audit row. SoD: none in MVP-1 (ASSUMPTION-0176-08; CT-F10 asks whether the
  resolver may be the person whose posting opened the case).
- SLA: `late = active && now > DueAtUtc`; "yaklaşıyor" (yellow) when < 6 h left (mockup). The sweeper writes one
  `SlaBreached` event per case (no notification in MVP-1 — FU-0176-NOTIF-01).

### 8.5 `ExpiredOnHandSweeper`

- Hosted service modelled on `LotExpirySweeperHostedService.cs:13,37-38` (at start + `PeriodicTimer`, interval 900 s,
  options section `Inventory:Expiry`, disabled → warning like `:31`).
- **Tenant + company scoped**: iterates `(TenantId, LegalEntityId)` pairs that have balance rows; per pair reads the
  policy threshold.
- Selects lots with `ExpiryDate < today − ExpiredOnHandThresholdDays` and on-hand > 0 in any status in a warehouse; opens
  `EXPIRED_ON_HAND` per (lot, warehouse) unless an active case with dedup key `EXPIRED_ON_HAND:{lotId}:{warehouseId}`
  exists (unique partial index).
- Not triggered on read; the panel shows "son tarama" time.
- Also writes `SlaBreached` events (§8.4).

### 8.6 e-signature (KF-D, KF-E)

**Signable actions** (registered by `POST /api/internal/esign/actions/register`, module **`expiry`**, prefix enforced by
Platform `CatalogHandlers.cs:262-266`; registration hosted service generalised from the quarantine-specific
`ESignActionRegistrationHostedService.cs` to one call per module):

| actionCode | Names (tr) | defaultRequired | defaultMeaningCode | defaultVisualMode | Subject |
|---|---|---|---|---|---|
| `expiry.lot-date.override` | Lot tarih sapması | true | `APPROVE` | `savedAccepted` | `expiry.date-override` |
| `expiry.exception.accept` | İstisnayı gerekçeyle kabul et | true | `APPROVE` | `savedAccepted` | `expiry.exception` |
| `expiry.exception.fix` | İstisnayı düzeltildi olarak kapat | true | `APPROVE` (CT-F8; was `REVIEW`, mockup "Gözden geçirdim") | `savedAccepted` | `expiry.exception` |
| `expiry.exception.cancel` | İstisnayı iptal et | true | `APPROVE` | `savedAccepted` | `expiry.exception` |

Meanings are tenant-defined (KS-12, MOD-0022 pack `:508-512`); CT-F8: all four register `APPROVE` (a tenant may map another
meaning in its policy). Names in 7 languages (SevenNames). F3: `ExpirySignableActions.cs`; one registration call per module
(`ESignActionRegistrationHostedService`, quarantine call byte-identical to the pre-F3 one).

**Subject provider (CT-D2):** `GET /api/expiry/esign-subjects/{subjectType}/{subjectId}` (provider contract
`esign.openapi.yaml` `x-subject-provider-contract`; config `ESign:SubjectProviders:expiry:BaseUrl`, operator config,
never a manifest). Canonical form `{"module","subjectType","subjectId","fields":{sorted}}`, SHA-256 (as
`QuarantineSignableSubjects.cs:133-153`).
- `expiry.exception` field list **`fx-v1`**: `number, type, status, legalEntityId, itemId, skuId, skuLevel, skuCode,
  lotId, lotNumber, expiryDate, retestDate, warehouseId, quantity, baseUom, sourceTransactionId, sourceMovementType,
  openedAtUtc, facts.* (flattened, sorted), resolution, resolutionReason, version`.
- `expiry.date-override` field list **`do-v1`**: `overrideId, legalEntityId, itemId, skuId, skuLevel, lotNumber,
  manufacturingDate, suggestedExpiry, enteredExpiry, suggestedRetest, enteredRetest, reason, shelfLifeDays, retestDays`.
- `resolution` + `resolutionReason` are part of the hash: the client signs **the intended decision** (the drawer's second
  step sends them to the provider as a pending decision on the case — `PendingResolution`, cleared on resolve / timeout
  15 min). Same pattern as 0175's decider signature (`QuarantineDeciderSignature.cs:10-105`).

**Direct signature, not E4.** E4 (`ESignatureGate.cs:28-53`, MOD-0022 pack `:533-565`) gates the **approve transition of
a MOD-0023 workflow task**. 0176 has no workflow task (§8.7), so the deciding user signs directly and 0176 verifies with
**`getSignature` (`esign.openapi.yaml:654-684`) + a local `fx-v1` / `do-v1` hash comparison** (CT decision F3-A, the MOD-0175
`QuarantineDeciderSignature` pattern — `verifySignature` returns no signer / action / meaning / `signedAtUtc` and needs
`esign.signatures.verify`, so it is not called): same tenant, signer = actor, action code = the resolution's, meaning = the
effective policy's, subject = {expiry, type, id}, subject hash = the CURRENT hash from the provider's own function, not
invalidated, signed at or after `PendingResolution` was set (an override: its creation), not used before (unique
`ResolutionSignatureId`; an override is consumed once). MOD-0022 unreadable → refused (fail-closed). 0176 stores **only**
`signatureId` (KF-E: no own signature record).

Policy: 0176 reads `getEffectivePolicy` (`esign.openapi.yaml:297-311`) per resolution; `required=false` → resolve without a
signature. KF-D under `ReasonAndSignature` requires the signature **regardless** of the MOD-0022 row (the owner decision
fixes it; the MOD-0022 row supplies meaning + visual mode only) — CT-F6.

### 8.7 Workflow decision — in-module state machine (recommended)

Measured: the report never makes Workflow HARD for 0176 (`…dependency-report.md:121` HARD only for 0175, `:399`
"0176←0174", `:569` 0174 + 0188 [INT]). The exception case has **one decider** (QA), a short life (SLA hours), no
multi-step approval. A MOD-0023 workflow would add the WF-SOD / WF-KEYS prerequisites that 0175 needed (MOD-0175 pack
§8 "Workflow — measured limits") without a rule that needs them.

Recommendation: **in-module state machine** (§8.4) with an inbox screen; no WorkCenter task. Later WorkCenter visibility
is FU-0176-WC-01 (a read-only task feed, not a decision path). CT-F11 confirms.

### 8.8 KPIs, sources, indexes

| KPI (panel) | Formula | Source |
|---|---|---|
| FEFO uyum oranı (period, default 30 days) | `count(snapshot where Compliant) ÷ count(snapshot)` over `PostedAtUtc` in period; company (+ optional warehouse) | `fefoIssueSnapshots` |
| Kritik (≤ CriticalDays) | lots with on-hand `AVAILABLE` > 0 and `today ≤ expiry ≤ today + CriticalDays` | lots + balances |
| Yaklaşıyor (≤ ApproachingDays) | same with ApproachingDays (includes Kritik — shown as "of which critical") | lots + balances |
| Süresi dolmuş stokta | lots with `expiry < today` and on-hand > 0 in any status | lots + balances |
| Açık istisna / SLA aşan | `count(Status ∈ {Open, InReview})`, of which `DueAtUtc < now` | `expiryExceptions` |
| Ortalama çözüm süresi | `avg(ResolvedAtUtc − OpenedAtUtc)` for cases resolved in period | `expiryExceptions` |
| Grafik: aylık son kullanma (12 ay) | on-hand quantity by expiry month (AVAILABLE vs held split) | lots + balances |

The mockup counts quarantined / blocked lots in the 30 / 90-day counts; the pack splits `AVAILABLE` from held quantity
(columns "Kullanılabilir" / "Bekleyen"), ASSUMPTION-0176-09. No money values (mockup has none; valuation stays MOD-0173).

**Indexes (new):**
- `fefoIssueSnapshots`: `(TenantId, LegalEntityId, PostedAtUtc)`, `(TenantId, LegalEntityId, WarehouseId, PostedAtUtc)`, unique `(TenantId, TransactionId)`.
- `expiryExceptions`: `(TenantId, LegalEntityId, Status, Type, OpenedAtUtc)`, `(TenantId, LegalEntityId, Status, DueAtUtc)`, unique `(TenantId, Number)`, unique partial `(TenantId, DedupKey)` where active.
- `expiryExceptionEvents`: `(TenantId, CaseId, Seq)` unique.
- `expiryPolicies`: unique `(TenantId, LegalEntityId)` active; `expiryPolicyHistory`: `(TenantId, LegalEntityId, ChangedAtUtc)`.
- `expiryDateOverrides`: `(TenantId, Status, ExpiresAtUtc)`; `expiryEvidenceExports`: `(TenantId, LegalEntityId, ExportedAtUtc)`.
- Existing lot indexes are reused: `(TenantId, SkuId, SkuLevel, ExpiryDate)`, `(TenantId, Status, ExpiryDate)` (MOD-0174
  pack `:148-149`). **Needed on balances:** `(TenantId, LegalEntityId, WarehouseId, SkuId, SkuLevel, LotId, StockStatus)`
  for the FEFO candidate read — F2 measures whether an existing balance index covers it before adding (CT-F13).

Cost: the FEFO check reads the SKU's lots in one warehouse (bounded by lots with on-hand); measured target p95 ≤ 50 ms
added per posting with 200 lots / SKU (F2 acceptance).

### 8.9 Contract impact (additive only — proposals for the owners' single-writer step)

**INVENTORY-BUNDLE 1.3.0 (owner MOD-0173)**
- `MovementRequest.fefoBypassReason`: string 10–1000, optional.
- `MovementResponse.expiryChecks` (optional object): `fefo: Compliant | Bypassed | NotApplicable`, `minRemaining: Ok |
  NotDefined | Warned | Quarantined | NotApplicable`, `appliedToStockStatus`, `exceptionIds[]`.
- New 422 codes (closed `All` set `InventoryErrorCodes.cs:67-75` grows with the version): `FEFO_BYPASS_REASON_REQUIRED`,
  `FEFO_ORDER_VIOLATION`, `LOT_BELOW_MIN_REMAINING_SHELF_LIFE`.
- Description note on `postMovement`: company expiry policy may book a `GOODS_RECEIPT_PO` into `QUARANTINE` (KF-C;
  CT-F1).
- Description-only fix of a measured gap: `LOT_EXPIRED` / `LOT_BLOCKED` examples in `UnprocessableMovement`
  (`inventory-bundle.openapi.yaml:487-499` lists neither; MOD-0174 pack `:484-485` defines them).

**TRACE 1.4.0 (owner MOD-0174)**
- `CreateLotRequest.dateOverride` (optional): `{ overrideId, signatureId? }`.
- `LotRef.expiryOverride` (optional, read): `{ reason, signatureId, suggestedExpiry, suggestedRetest, by, atUtc }`.
- `LotRef.suggestedExpiryDate` / `suggestedRetestDate` (optional, read; null when no suggestion).
- History action `ExpiryOverridden`.
- New codes on `createLot`: 422 `LOT_DATE_OVERRIDE_REQUIRED`, 400 `LOT_DATE_OVERRIDE_SIGNATURE_REQUIRED`, 409
  `LOT_DATE_OVERRIDE_SIGNATURE_MISMATCH`, 409 `LOT_DATE_OVERRIDE_MISMATCH`.
- Unchanged: `expiryState` keeps its fixed 90-day window (`trace.openapi.yaml:306`); 0176's company horizons (KF-A) live in
  FEFO-BUNDLE reads. Description note only.

**QUARANTINE** — no change needed. Optional 1.3.0 `HoldRef.originReason` (CT-F9).
**ESIGN** — no change (consumer: registration + provider).
**PRODUCT-MASTER** — no change; 0176 needs draft.11 fields frozen (dependency, §7).
**New stock status / movement type** — **none** (explicit check: auto-quarantine uses `QUARANTINE`; 0176 posts nothing).

### 8.10 Audited events (AUD-001)

The standard lists "Audited Events" as its 21st section (`.antigravity/rules/module-pack-standard.md:256-284`); 0174 / 0175
packs use §21 for UI input and this pack keeps that numbering (the WP brief references §21.3), so the audit list lives
here:

| Event | When | Payload |
|---|---|---|
| `expiry.policy.updated` | policy save | fields changed, reason, version |
| `expiry.exception.opened` | hook / sweeper / manual | number, type, origin, lot, warehouse, source transaction |
| `expiry.exception.taken` / `.commented` | | caseId, actor |
| `expiry.exception.resolved` | resolve | resolution, reason, signatureId |
| `expiry.exception.closed` / `.cancelled` | | |
| `expiry.fefo.refused` | Engelle / missing reason 422 | sku, lot, firstEligibleLot, warehouse |
| `expiry.receipt.quarantined` / `.refused` | KF-C | transactionId or request key, remaining / required days |
| `expiry.date-override.prepared` / `.consumed` | KF-D | overrideId, lotId |
| `expiry.evidence.exported` | export | scope, file name, SHA-256 |

## 9. Layout & Shell Contract

- Tenant shell only: `/account/login`, `_LayoutTenantShell`, company selector (`X-Legal-Entity-Id`). **No `/Platform/`
  route** (manifest route test, §17).
- Golden reference per screen: lists = slim DataTable (server paging); panel = KPI cards + chart + table; exception
  detail = single record; decision = two-step drawer + MOD-0022 dialog; settings = form + history.
- UAS-001: a user without the page key gets no page skeleton and no redirect (403 view of the shell).

## 10. Backend File Convention

```
Domain/Expiry/{Entities,Enums,Rules}/…           ExpiryPolicy, FefoExceptionCase, FefoIssueRule, MinRemainingShelfLifeRule, LotDateSuggestionRule, FefoOrderRule
Application/Features/Expiry/{Commands,Queries,Handlers,Ports}/…
Application/Features/Inventory/Ports/IExpiryPostingPolicy.cs   (default allow, in InventoryLedger)
Persistence/Expiry/…                              repositories, ExpiredOnHandSweeper(+HostedService, Options), index builder
Api/Expiry/ExpiryController.cs, ExpiryESignSubjectsController.cs, ExpiryServiceCollectionExtensions.cs, ExpiryFefoManifestProvider.cs
Infrastructure/Authorization/ExpiryPermissions.cs
```
Controllers pass HTTP status explicitly per handler (`CustomBaseController.cs:15-39`).

## 11. Frontend File Contract

### 11.1 Mockup screen → route (all Scope = Tenant, all `_LayoutTenantShell`)

| Mockup screen | PageCode | Route | Kind | Page permission | Nav |
|---|---|---|---|---|---|
| FEFO Paneli | `EXPIRYDASHBOARD` | `/Expiry` | KPI + chart + table | `expiry.dashboard.read` | yes |
| Son Kullanma Yaklaşanlar | `EXPIRYAPPROACHING` | `/Expiry/Approaching` | list (slim DataTable) | `expiry.dashboard.read` | yes |
| Süresi Dolmuş Stok | `EXPIREDSTOCK` | `/Expiry/Expired` | list | `expiry.dashboard.read` | yes |
| FEFO Sırası | `FEFOORDER` | `/Expiry/FefoOrder` | filter + ordered list + excluded list (read-only, KF-F) | `expiry.fefo-order.read` | yes |
| İstisna Kutusu | `EXPIRYEXCEPTIONS` | `/Expiry/Exceptions` | list | `expiry.exceptions.read` | yes |
| İstisna Detayı (+ karar çekmecesi) | `EXPIRYEXCEPTIONDETAILS` | `/Expiry/Exceptions/Details/{id}` | single record + drawer + MOD-0022 dialog | `expiry.exceptions.read` | no (parent EXCEPTIONS) |
| Raf Ömrü Kuralları ve Ayarlar | `EXPIRYPOLICY` | `/Expiry/Policy` | form + history | `expiry.policies.read` | yes |
| Kanıt Paketi | `EXPIRYEVIDENCE` | `/Expiry/Evidence` | filter + preview + export log | `expiry.evidence.export` | yes |
| "Diğer ekranlara eklenenler" — movement form bands | — | MOD-0173 `/StockMovements/Create` (`StockMovementsController.cs:22,57`) | band | (0173's) | — |
| — lot create / quick-create band | — | MOD-0174 `/Lots/Create` (`LotsController.cs:20,58`), quick-create modal | band | (0174's) | — |
| — product view shelf-life badges | — | MOD-0290 product screens (MDM) | **not built here** (§11.5) | — | — |

### 11.2 Files
`Controllers/ExpiryController.cs`; `Views/Expiry/{Index,Approaching,Expired,FefoOrder,Exceptions,ExceptionDetails,Policy,Evidence}.cshtml`, `_DecisionDrawer.cshtml`, `_OpenExceptionModal.cshtml`; `wwwroot/assets/js/Expiry/*.js`; band partials `Views/Shared/Expiry/_FefoBand.cshtml`, `_MinRemainingBand.cshtml`, `_DateOverrideBand.cshtml`.

### 11.3 Screen → button → action → permission

| Screen | Button / control | Action code | Permission |
|---|---|---|---|
| Panel | KPI card links, period filter | — | `expiry.dashboard.read` |
| Panel / Yaklaşanlar / Dolmuş | "İstisna aç" | `EXPIRY_EXCEPTION_OPEN` | `expiry.exceptions.create` |
| Yaklaşanlar / Dolmuş | "Karantinaya al" | (0175 page `/Quarantine` action) | `quarantine.holds.create` (0175's, link only) |
| Yaklaşanlar / Dolmuş | "FEFO sırasını gör" | — | `expiry.fefo-order.read` |
| Yaklaşanlar / Dolmuş | Dışa aktar (CSV) | `EXPIRY_LIST_EXPORT` | `expiry.dashboard.read` (list export, no evidence log) — CT-F14 |
| FEFO Sırası | "Sırayı hesapla" (filter submit) | — | `expiry.fefo-order.read` |
| İstisna Kutusu | "İstisna aç" | `EXPIRY_EXCEPTION_OPEN` | `expiry.exceptions.create` |
| İstisna Detayı | "İncelemeye al" | `EXPIRY_EXCEPTION_TAKE` | `expiry.exceptions.review` |
| İstisna Detayı | "Yorum ekle" | `EXPIRY_EXCEPTION_COMMENT` | `expiry.exceptions.comment` |
| İstisna Detayı | "Karar ver" → drawer step 2 "İmzala ve kaydet" | `EXPIRY_EXCEPTION_RESOLVE` | `expiry.exceptions.resolve` (+ `esign.signatures.sign`, MOD-0022) |
| İstisna Detayı | "İstisnayı kapat" | `EXPIRY_EXCEPTION_CLOSE` | `expiry.exceptions.close` |
| Ayarlar | "Kaydet" (reason modal) | `EXPIRY_POLICY_SAVE` | `expiry.policies.manage` |
| Ayarlar | "Geçmiş" tab | — | `expiry.policies.read` |
| Kanıt Paketi | "CSV indir" / "PDF indir" | `EXPIRY_EVIDENCE_EXPORT` | `expiry.evidence.export` |
| Movement form band | reason field (Gerekçe mode) | — | (posting key, 0173's) |
| Lot form band | "Önerilen tarihi kullan" / "Sapmayı imzala" | — (calls `POST /api/expiry/date-overrides`) | `expiry.date-overrides.create` (API) + `esign.signatures.sign` |

### 11.5 Not built here
Product view "Tanımsız" / "Uygulanmaz" / "SKT takibi yok" badges (MOD-0290 screens — FU-0176-MDM-01); lot reservation
button "Bu lotu rezerve et" (KF-F → FU-0176-RES-01); market / customer rules (KF-H).

### 11.6 Resx
`Expiry.*` keys (screens, columns, filters, buttons, exception types / statuses / resolutions, band texts, errors, the
excluded-lot reasons), `Nav.Module.EXPIRY-FEFO-MANAGEMENT`, `Nav.Page.*` for the 7 nav pages — **7 languages**
(tenant module). Wording fixes from §21.3 apply ("Dolduğundan beri", not "Dolalı").

## 12. Validation Rules

| Rule | Code | HTTP |
|---|---|---|
| CriticalDays ≥ ApproachingDays | `EXPIRY_POLICY_INVALID` | 400 |
| day / hour fields out of range | `EXPIRY_POLICY_INVALID` | 400 |
| policy save reason < 10 | `REASON_REQUIRED` | 400 |
| resolve reason < 10 | `REASON_REQUIRED` | 400 |
| resolve without signature when policy requires | `EXPIRY_SIGNATURE_REQUIRED` | 400 |
| signature mismatch (signer, action, subject hash, meaning, reused) | `EXPIRY_SIGNATURE_MISMATCH` | 409 |
| transition not allowed from current status | `EXPIRY_INVALID_TRANSITION` | 409 |
| stale If-Match | `EXPIRY_CONFLICT` | 409 |
| manual open: lot not in company / warehouse | `UNKNOWN_LOT` / `CROSS_LE_FORBIDDEN` | 404 / 403 |
| date override expired / consumed | `EXPIRY_DATE_OVERRIDE_NOT_USABLE` | 409 |
| hook codes | §8.3 | 422 / 400 / 409 |

## 13. Failure Path to Verify

1. Gerekçe mode, issue from a later lot without reason → 422 `FEFO_BYPASS_REASON_REQUIRED`, no movement, no exception, no snapshot.
2. Same request with reason ≥ 10 → 201, one movement, one `FEFO_BYPASS` exception, one snapshot; replay → same result, still one exception.
3. Engelle mode → 422 `FEFO_ORDER_VIOLATION`; audit row written.
4. GR below minimum, Otomatik karantina → movement in `QUARANTINE`, 0175 hold opened, exception opened; `minRemaining` null → AVAILABLE, no exception.
5. GR below minimum, Reddet → 422, nothing posted.
6. Lot with differing expiry without override → 422; with override but foreign signature → 409; correct → lot + exception, override consumed; replay → same lot.
7. Resolve without signature (policy required) → 400; signature of another case → 409; policy row `required=false` → resolves without.
8. Company B user reads company A exception → 403 `CROSS_LE_FORBIDDEN`; tenant B → 404.
9. REVERSAL of a bypassing issue → no FEFO check, exception unchanged.
10. 0175 `STATUS_TRANSFER` (`sourceModule=MOD-0175`) from a later lot → no FEFO check.
11. Sweeper run twice → one `EXPIRED_ON_HAND` case per (lot, warehouse).

## 14. Authorization Convention

### 14.1 Namespace
New namespace **`expiry.*`** (PKS-001 §1 `:28-50`, §4 `:90-92`: one owner per namespace) owned by MOD-0176 / manifest
`EXPIRY-FEFO-MANAGEMENT`. Not `fefo.*` (narrower than the module: policy, exceptions and evidence are not FEFO-only), not
`inventory.*` (MOD-0173's, `MOD-0173 pack :267`). **FU-0174-CAT-01 lesson** (MOD-0174 pack `:841-856`): the manifest
declares only `expiry.*` keys; external keys used by 0176 screens (`quarantine.holds.create`, `trace.lots.read`,
`esign.signatures.sign`, inventory posting keys) are **never** declared here; a namespace test asserts it
(`ManifestNamespaceOwnershipTests.cs:7` extended).

### 14.2 Keys
| Key | Tier | Meaning |
|---|---|---|
| `expiry.dashboard.read` | 1 | panel, approaching, expired lists |
| `expiry.fefo-order.read` | 1 | FEFO order read (also consumers 0178 / 0172 / 0188) |
| `expiry.exceptions.read` | 1 | inbox, detail |
| `expiry.exceptions.create` | 1 | manual "İstisna aç" |
| `expiry.exceptions.comment` | 3 | comment on a case |
| `expiry.exceptions.review` | 3 | take into review |
| `expiry.exceptions.resolve` | 3 | accept / fix / cancel |
| `expiry.exceptions.close` | 3 | close accepted / fixed |
| `expiry.policies.read` | 1 | settings + history read |
| `expiry.policies.manage` | 3 | settings save |
| `expiry.evidence.export` | 2 | evidence pack + export log |
| `expiry.date-overrides.create` | 1 | API-only (`ApiPermissions`, Scope Tenant) — prepare a KF-D override |

Tier-3 actions `comment`, `review`, `resolve`, `close`, `manage` are registered here (PKS-001 §5 `:110-135`).

### 14.3 Default role mapping (proposal — owner decision ROLE1: only new tenants get templates)

| Role (mockup L:17-21) | Keys |
|---|---|
| Depo / stok operatörü | `dashboard.read`, `fefo-order.read`, `exceptions.read`, `exceptions.create`, `exceptions.comment`, `date-overrides.create` |
| Kalite QA | operator keys + `exceptions.review`, `exceptions.resolve`, `exceptions.close`, `policies.read` |
| Kalite sistem yöneticisi | `dashboard.read`, `exceptions.read`, `policies.read`, `policies.manage`, `evidence.export` |

Mockup `can` (L:183): decide = QA; settings / export = system admin; "Karantinaya al" / send = not system admin (0175 key).
Comments and filters are ungated in the mockup — the pack gates comments with a key (write action).

### 14.4 Manifest
| Field | Value |
|---|---|
| ModuleCode | `EXPIRY-FEFO-MANAGEMENT` |
| SortOrder | 176 |
| Pages | the 7 nav pages + `EXPIRYEXCEPTIONDETAILS` (§11.1) |
| Actions | §11.3 action codes, one per real button |
| ApiPermissions | `expiry.date-overrides.create` (Tenant) |

## 15. Gateway / API Routing Decision

`/api/expiry` and `/api/expiry/{everything}` → Diten.InventoryService **5067** (INT-EXPIRY, integration-agent, route
coverage test). In-process with 0173 / 0174 / 0175 because the hooks need the posting transaction (§8.3) and the lot
handler — a separate service would need a distributed transaction or a refusal-incapable observer. Platform calls the
e-sign provider through the gateway route (`ESign:SubjectProviders:expiry:BaseUrl`).

Port note: 5067 per domain-config `:64` and DCP-009 `:42`; DCP-009 `:98`, registry `:272,:275` and 0174 / 0175 frontmatter
still say 5066 — doc drift, FU-0176-DOC-01 (not fixed here: YAPMA).

## 16. Acceptance Criteria

| Slice | Criteria | Sabotage (must turn a test red) |
|---|---|---|
| F1 | policy defaults (90 / 30 / RequireReason / AutoQuarantine / ReasonAndSignature) read without a row; save needs If-Match + reason ≥ 10, writes one history row per changed field; state machine transitions §8.4 only; SLA due time; company + tenant isolation; sweeper idempotent; `getSku` day fields parsed (null kept null); manifest namespace test; contract test against `fefo.openapi.yaml` | (a) remove `LegalEntityId` filter in exception repository → cross-company test red; (b) skip If-Match → concurrency test red; (c) allow `Cancelled → Closed` → state test red; (d) parse null `minRemainingShelfLifeDays` as 0 → "Tanımsız" test red; (e) add a `quarantine.*` key to the manifest → namespace test red |
| F2 | §8.3 hook table, every cell, measured by production code via the HTTP posting endpoint; snapshot written for every in-scope issue; exemptions (CT-D4, 0175, status / location transfers); idempotent replay; in-transaction re-check; p95 added latency ≤ 50 ms at 200 lots / SKU; CT-Q17 test still green | (a) remove the pre-check call → bypass-without-reason test red; (b) remove the in-transaction re-check → race test red; (c) drop `REVERSAL` exemption → reversal test red; (d) compare tie-break instead of expiry → equal-expiry test red; (e) ignore `minRemaining` null guard → null test red; (f) write exception after commit → crash-injection test red |
| F3 | registration of 4 actions (prefix `expiry.`), provider returns `fx-v1` / `do-v1` with golden SHA-256 vectors; resolve verifies signer / action / meaning / hash / reuse; KF-D signature required under `ReasonAndSignature` even if the policy row says not required; 0176 stores only `signatureId` | (a) skip hash comparison → stale-subject test red; (b) accept a signature of another case → reuse test red; (c) change a field name without version → golden vector red |
| F4 | 4 screens 1:1 with mockup except §21.3; KPIs equal the formulas on a seeded data set; FEFO order shows excluded lots with reasons; no lot-reserve button; UAS-001 | (a) render skeleton without key → UAS test red; (b) drop company scope in panel query → red |
| F5 | inbox / detail / drawer / settings / evidence 1:1 except §21.3; buttons hidden without key; evidence file SHA-256 of the produced bytes; export log row; bands on movement / lot forms; InventoryGovernance removed or redirected (CT-F12); 375 px layout measured | (a) show "Karar ver" without `resolve` → red; (b) hash the parameters instead of the file → red |
| INT | gateway routes + coverage test | remove route → coverage red |
| L10n | 7 languages, parity test | delete one `ar` key → red |

## 17. Test Expectations

- Unit: pure rules (`FefoOrderRule`, `FefoIssueRule`, `MinRemainingShelfLifeRule`, `LotDateSuggestionRule`) table tests incl. nulls, ties, exempt types, date boundaries (posting date vs today).
- Integration (own ephemeral Mongo replica set via `INVENTORY_TEST_MONGO`, never 27017): posting endpoint with hooks, transaction rollback, idempotent replay, sweeper, provider canonical hash.
- Contract: `fefo.openapi.yaml` vs controllers; INVENTORY 1.3.0 / TRACE 1.4.0 fixture tests updated by owners.
- Architecture: manifest namespace, no `/Platform/` route, tenant filter guard, no signature record entity.
- Web: Razor page tests per route, permission hiding, 7-language parity (`NavManifestL10nGuardTests`).
- Not run: `AccountKindAcceptanceHost.Tests`.

## 18. Ready-for-dev Checklist

| # | Item | State |
|---|---|---|
| 1 | Owner decisions KF-A…KF-H recorded | ✅ §19.1 |
| 2 | FEFO-BUNDLE v1 proposal | ✅ draft — CT acceptance pending |
| 3 | CT-F1 (GR re-route additive?) decided | ✅ §19.7 — fallback path (422 + resubmit) |
| 4 | INVENTORY 1.3.0 / TRACE 1.4.0 additive proposals accepted by owners | ⏳ |
| 5 | PRODUCT-MASTER 1.1 shelf-life fields live on `getSku` | ⏳ (draft.11) |
| 6 | MOD-0022 E2 live (signature API + provider registry) | ✅ live on main (ESIGN 1.0.1 FROZEN, E2…E5 CT-accepted) |
| 7 | CT-F2…CT-F14 decided | ✅ §19.7 |
| 8 | Failure path ≥ 4 scenarios | ✅ §13 (11) |
| 9 | Screen → route, button → action → key | ✅ §11.1, §11.3 |
| 10 | Namespace + manifest | ✅ §14 |
| 11 | Mockup differences listed | ✅ §21.3 |
| 12 | DCP-002 verifier | waiver **W-0176-01** (no Python) |
| 13 | Audited events | ✅ §8.10 (numbering note) |
| 14 | Port 5067 consistent in pack | ✅ (drift elsewhere → FU-0176-DOC-01) |

## 19. Implementation Notes

### 19.1 Owner decisions (2026-10-12) — DECISIONS

| ID | Decision |
|---|---|
| **KF-A** | "Yaklaşıyor" horizon = company setting, default **90** days; "Kritik" = company setting, default **30** days. |
| **KF-B** | FEFO outbound default **Gerekçe iste**: an issue not from the first eligible FEFO lot needs a reason ≥ 10 and opens `FEFO_BYPASS`. Options Uyar / Gerekçe iste / Engelle. |
| **KF-C** | GR below minimum remaining shelf life, default **Otomatik karantina** (via the MOD-0175 quarantine path) + `BELOW_MIN_REMAINING`. Options Uyar / Otomatik karantina / Reddet. `minRemainingShelfLifeDays` null → rule NOT applied ("Tanımsız"). |
| **KF-D** | Expiry / retest at lot creation different from the suggestion (manufacturing date + `shelfLifeDays` / `retestDays`, getSku) → **Gerekçe + e-imza** (MOD-0022) + `EXPIRY_DATE_OVERRIDE`. No value → no suggestion, no exception. CT-Q17 kept. |
| **KF-E** | Exception-closing e-signature **by policy** (MOD-0022 policy, meaning per resolution type). Default: all three resolutions require a signature. 0176 keeps NO own signature record. |
| **KF-F** | FEFO order = read-only list. NO "Bu lotu rezerve et" (needs an INVENTORY contract change → MOD-0178, FU). |
| **KF-G** | Rule scope = company only; product-level exception rules out of scope (FU). |
| **KF-H** | Market / customer minimum remaining shelf life out of scope (FU; MVP-6 / sales). |

### 19.2 KF trace table

| KF | Where in pack | Contract | Slice | Test |
|---|---|---|---|---|
| KF-A | §4.1, §8.8 KPIs | FEFO `getPolicy` / `updatePolicy`, `listApproaching` | F1, F4 | policy defaults, horizon counts |
| KF-B | §8.3.1, §8.3.4 | INVENTORY 1.3.0 `fefoBypassReason`, 422 codes; FEFO exception | F2, F5 band | hook table row 1, failure 1–3 |
| KF-C | §8.3.2 | INVENTORY 1.3.0 note + 422; QUARANTINE unchanged | F2 | failure 4–5, null rule |
| KF-D | §8.3.3, §8.6 | TRACE 1.4.0 `dateOverride`; FEFO `createDateOverride`; ESIGN consumer | F2, F3, F5 band | failure 6, CT-Q17 test |
| KF-E | §8.4, §8.6 | FEFO `resolveException` + `signatureId`; ESIGN `getEffectivePolicy` | F1, F3 | failure 7, no signature entity |
| KF-F | §8.2, §11.5 | FEFO `getFefoOrder` (read only) | F4 | no reserve button / endpoint |
| KF-G | §4.1 (company key) | policy per `LegalEntityId` | F1 | no product rule entity |
| KF-H | §11.5, §20 | — | — | — |

### 19.3 Mockup rules → pack

| Mockup rule | Pack |
|---|---|
| Lot status order exp, retest, quar, blocked, crit, soon, ok (L:139-148) | §8.2 reasons + list badges |
| FEFO order expiry asc, eligible ok/soon/crit, excluded labels (L:251-260) | §8.2 (+ tie-break, D-6) |
| Exception types / statuses / resolutions (L:7-9) | §4.3, §8.4 |
| Decision from open; accepted / fixed → close without signature | §8.4 |
| SLA late / yellow < 6 h | §8.4 |
| Settings save reason ≥ 10, history per field, no e-sign | §4.1-4.2, §12 |
| Evidence period q3 / d30 / ytd, CSV header `istisna_no;tur;urun;lot;depo;acilis;durum;cozum;imza` | proposal §4 evidence |
| Bands: new lot suggested SKT + override reason + e-sign modal; GR four cases; outbound four cases | §8.3, §11.3 |

### 19.4 Measured facts (selection — every line read in this worktree at 77cd61a44)

`LotPostingRules.cs:8-25,42,50,53,57,60,78-92,94-112` · `LotExpirySweeper.cs:40-47,58-100` ·
`LotExpirySweeperHostedService.cs:13,31,37,38` · `LotExpirySweeperOptions.cs:8,10` · `MovementMasterDataValidator.cs:82-270,198` ·
`InventoryLedgerRepository.cs:153-241,176,177,205,229-231,277-294,286` · `InternalMovementPostingService.cs:67-70,79,81-85,91,108,139-180,183,198-208,210-250` ·
`IPostedMovementObserver.cs:7,10` · `QuarantineIntake.cs:41-65,109-115,160-170` · `QuarantineMovementPoster.cs:43,103` ·
`BalanceBuckets.cs:18` · `InventoryReservation.cs:11-51` · `ReservationPositionOperations.cs:215-275` · `GetAvailabilityHandler.cs:82-89` ·
`InventoryErrorCodes.cs:11,18,31,43-50,67-75` · `TraceErrorCodes.cs:12-46` · `CreateLotHandler.cs:16-29,71-72,94-124,129-164,193-195` ·
`UpdateLotHandler.cs:24-28,46,69-79,81-84` · `ILotStatusChangeGate.cs:6-21` · `QuarantineServiceCollectionExtensions.cs:55,67-71` ·
`QuarantineESignPort.cs:39-69` · `QuarantineSignableSubjects.cs:27-166` · `QuarantineDeciderSignature.cs:10-105` · `ESignatureGate.cs:28-53` ·
`CatalogHandlers.cs:262-266` · `HoldSource QuarantineEnums.cs:7-16` · `InventoryGovernanceController.cs:8-21,75-115` · `_Layout.cshtml:217-222` ·
`ManagementGovernanceRegistry.cs:207` · contracts: `inventory-bundle.openapi.yaml:9-10,112,330-336,337-361,396,434-449,487-499`,
`trace.openapi.yaml:8-9,64,68,72,82,306,397-412`, `quarantine.openapi.yaml:12,54-66,397,578-587`, `esign.openapi.yaml:11,176,297-311,552-571`,
`product-master-bundle.openapi.yaml:19,708-710,729-732` · report `:49,93,122-123,146-148,185-186,213,230,399,569`.

### 19.5 Open CT decisions

| ID | Question | Recommendation |
|---|---|---|
| **CT-F1** | KF-C "Otomatik karantina": may the server book a `GOODS_RECEIPT_PO` into `QUARANTINE` when the caller asked `AVAILABLE` (INVENTORY 1.3.0 behaviour note + `appliedToStockStatus`)? | Yes — fires only when `minRemainingShelfLifeDays` (new 1.1 field) is set, response states it; fallback = 422 + caller resubmits (§8.3.2) |
| **CT-F2** | Hook port shape: one `IExpiryPostingPolicy` (pre + in-tx) vs two ports | One port, default allow |
| **CT-F3** | Is `WAREHOUSE_TRANSFER` an outbound FEFO issue? | No in MVP-1 (internal move; MOD-0178 later) |
| **CT-F4** | Reversal of a bypassing issue: auto-close the exception or leave to QA? | Leave to QA ("Düzeltildi"), show reversal link |
| **CT-F5** | KF-C scope: `GOODS_RECEIPT_PROD`, `CUSTOMER_RETURN` too? | PO only in MVP-1 |
| **CT-F6** | KF-D under `ReasonAndSignature`: signature required even if the MOD-0022 policy row says not required? Is `Reason` (no signature) an allowed option at all? | Yes required; keep `Reason` as option (mockup had it) |
| **CT-F7** | Draft `updateLot` of expiry / retest: same override rule? | Yes for Draft (else KF-D is bypassable by create-then-update) |
| **CT-F8** | Meaning code `REVIEW` for "Düzeltildi" (tenant-defined meanings) | Register `APPROVE` default; tenants may map |
| **CT-F9** | QUARANTINE 1.3.0 `HoldRef.originReason` | Not now |
| **CT-F10** | SoD on resolve (resolver ≠ poster) | Not in MVP-1 |
| **CT-F11** | Workflow: in-module state machine (no MOD-0023) | Yes (§8.7) |
| **CT-F12** | Old `InventoryGovernance` fake screen (no `[Authorize]`, hardcoded rows): remove + redirect `mod-risk-management` to `/Expiry`? | Remove controller / view / menu; registry entry → `/Expiry` (also answers MOD-0173-FU01 OD-UI-3 `:536-538`) |
| **CT-F13** | Balance index for FEFO candidates | F2 measures, adds only if no existing index covers |
| **CT-F14** | List CSV export on Yaklaşanlar / Dolmuş: separate key or under `dashboard.read` | Under `dashboard.read`, not logged as evidence |

### 19.6 Assumptions

- **ASSUMPTION-0176-01:** "company" = `LegalEntityId` (W-0173-02 scope).
- **ASSUMPTION-0176-02:** "first eligible lot" compares expiry dates only (equal = compliant).
- **ASSUMPTION-0176-03:** SLA defaults 24 / 72 / 48 / 120 h from the mockup (not an owner decision).
- **ASSUMPTION-0176-04:** expired-on-hand threshold default 14 days from the mockup.
- **ASSUMPTION-0176-05:** "today" = UTC date (same as `LotExpirySweeper`); posting rules use `PostingDate`.
- **ASSUMPTION-0176-06:** lot without expiry → KF-C not applied.
- **ASSUMPTION-0176-07:** empty entered date while a suggestion exists counts as "differs".
- **ASSUMPTION-0176-08:** no SoD on resolve in MVP-1 (CT-F10).
- **ASSUMPTION-0176-09:** KPI counts split AVAILABLE vs held quantity (mockup mixes them).
- **ASSUMPTION-0176-10:** manual "İstisna aç" may pick any of the four types; origin `Manual`.

### 19.7 CT acceptance — WP-0176-P0 (2026-10-12)

**CT-ACCEPTED.** Commit `752cfaa36` on `feature/sce/mod-0176-expiry-fefo` (base `77cd61a44` = origin/main after PR #139),
`git diff --stat 77cd61a44..752cfaa36` = the two allowed files only (+1175), `HEAD..origin/main` = 0. The CT re-read the measured
facts in the worktree: `LotPostingRules.cs:42,50,57,60`, `LotExpirySweeperHostedService.cs:37-38` + `LotExpirySweeperOptions.cs:10`
(900 s), `IPostedMovementObserver.cs:7` (post-commit, cannot refuse), `InventoryErrorCodes.cs:11,67-75` (closed set),
`MovementMasterDataValidator.cs:198-204`, `CatalogHandlers.cs:262-266` (own-prefix rule), `quarantine.openapi.yaml:397` (`HoldSource`
has no expiry value) — all exact. **One correction:** `InventoryGovernanceController` has no `[Authorize]` attribute, but the page is
NOT anonymous — measured `GET http://localhost:5001/InventoryGovernance` → 302 to `/account/login` (global authentication). The
defect is hard-coded rows + no permission key, not open access; CT-F12 stands for that reason.

**CT decisions:**

| ID | Decision |
|---|---|
| CT-F1 | **Fallback path, not the server re-route.** A `GOODS_RECEIPT_PO` into `AVAILABLE` below the minimum (mode Otomatik karantina) is refused 422 `LOT_BELOW_MIN_REMAINING_SHELF_LIFE` with `details.requiredToStockStatus = "QUARANTINE"`, `remainingDays`, `requiredDays`; the GR screen resubmits with `QUARANTINE` automatically (same idempotency key family, new key) and 0175 intake opens the hold. Reason: the single-writer ledger books exactly what the caller asked — no silent status change behind a FROZEN contract; other callers (MVP-6, 0142) get an explicit instruction; the end state is the owner's KF-C. Mode Reddet = the same code without `requiredToStockStatus`. INVENTORY 1.3.0 note in §8.9 changes accordingly (no re-route note; `appliedToStockStatus` dropped). |
| CT-F2 | One port `IExpiryPostingPolicy` (pre-check + in-transaction re-check), default allow. |
| CT-F3 | `WAREHOUSE_TRANSFER` not an outbound FEFO issue in MVP-1. |
| CT-F4 | Reversal leaves the exception to QA ("Düzeltildi"), detail shows the reversal link. |
| CT-F5 | KF-C applies to `GOODS_RECEIPT_PO` only in MVP-1 (customer returns enter quarantine anyway — MVP-6 plan P1). |
| CT-F6 | Under `ReasonAndSignature` the KF-D signature is required regardless of the MOD-0022 row (row = meaning + visual mode); `Reason` stays a selectable mode. |
| CT-F7 | Draft `updateLot` of expiry / retest follows the same override rule (else create-then-update bypasses KF-D); CT-Q17 unchanged for non-Draft. |
| CT-F8 | Default meaning `APPROVE` for all four actions (tenant meanings are tenant-defined); the §8.6 table's `REVIEW` for `expiry.exception.fix` is superseded — F3 registers `APPROVE`. |
| CT-F9 | No QUARANTINE change (`originReason` not now). |
| CT-F10 | **SoD ON (CT deviates from the pack's recommendation):** the resolver may not be the user whose action opened the case — the poster of the bypassing issue (`FEFO_BYPASS`), the creator of the overriding lot (`EXPIRY_DATE_OVERRIDE`), the opener of a manual case. System-opened cases (`BELOW_MIN_REMAINING`, `EXPIRED_ON_HAND`) have no origin user. New FEFO-BUNDLE code 409 `EXPIRY_SOD_VIOLATION` (F1 adds it to §4.21 when freezing). Reason: GxP four-eyes for an accepted deviation, same principle as KQ-C; cost is one comparison. ASSUMPTION-0176-08 is withdrawn. |
| CT-F11 | In-module state machine, no MOD-0023 workflow (§8.7). |
| CT-F12 | Remove `InventoryGovernance` controller / view / menu entry in F5; `ManagementGovernanceRegistry.cs:207` entry → `/Expiry`. |
| CT-F13 | F2 measures the balance indexes and adds one only if none covers the FEFO candidate read. |
| CT-F14 | List CSV under `expiry.dashboard.read`, not logged as evidence. |

ASSUMPTION-0176-03 / -04 (SLA 24 / 72 / 48 / 120 h, expired-on-hand 14 days) accepted as company-setting defaults. Remaining
ready-for-dev items: #4 INVENTORY 1.3.0 / TRACE 1.4.0 — the owner step runs inside F2 (same Inventory service, single writer); #5
PRODUCT-MASTER day fields — MDM draft.11, served by the CT dev bridge (4010) until MDM is live. Order: **F1 → INT → F2 → F3 → F4 → F5
→ L10n**. Port drift FU-0176-DOC-01 stays a CT docs item.

### 19.8 WP-0176-F1 — agent result, pending CT (2026-10-12)

Commit `e4ceac240` on `feature/sce/mod-0176-expiry-fefo` (base `2a25207dc`). Evidence
`docs/records/audits/2026-10/mod-0176-f1-evidence-2026-10-12.md` (+ scripts). Delivered: company `ExpiryPolicy` (+ history, If-Match `"0"`
first save, reason ≥ 10), `FefoExceptionCase` state machine §8.4 with insert-only events + `inventory_audit` rows, CT-F10 SoD (409
`EXPIRY_SOD_VIOLATION`), the KF-E signature port **fail-closed** until F3 (no resolution passes in production), prepare / resolve 15-minute
pairing, `FX-YYYY-NNNN` counter, `ExpiredOnHandSweeper` (tenant + company scoped, §13 #11), getSku day fields nullable (never 0), manifest
`EXPIRY-FEFO-MANAGEMENT` with the eight F1 keys as `ApiPermissions` (no page), `fefo.openapi.yaml` 1.0.0 FROZEN (reconciliations R-1…R-7
in its changelog). Inventory tests 839 → **905 / 905**; architecture the same 8 reds. Sabotage a–g (+ h, the contract validator) red →
restored green, `git diff` empty. Probe on own port 5167 + own mongod: `/health` 200, every `/api/expiry` F1 route 401 without a token.

Open for CT (evidence §9): **O-F1-1** AuditTrailStandard list 92 → 99 (+7 expiry commands, audited by an equivalent trail but InventoryService
has no audit ledger — crediting needs a ledger + `AcceptedTrails` change outside 0176); **O-F1-2** seams beyond the WP list
(`InventoryScopeResolutionMiddleware.ScopedBasePaths` — required for tenant scope; controller in `Api/Controllers/Expiry/`; six test-infra /
architecture-test seams); **O-F1-3** collection names `expiry_*`; **O-F1-4** contract reconciliations R-1…R-7; **O-F1-5** manual case
quantity without a unit until F4.

**F1 — CT acceptance (2026-10-12).** Commits `e4ceac240` + `226b397b5` on `feature/sce/mod-0176-expiry-fefo` (base `2a25207dc`,
`HEAD..origin/main` = 0, tree clean). The CT re-measured in the worktree: Inventory **905/905** (839 + 66), architecture **69/77** — the
same 8 reds as main (DocsPathGuard × 5, MongoTestDatabaseGuard, AuditTrailStandard × 2). CT sabotage (own): `ExpiryContract.BasePath`
removed from `InventoryScopeResolutionMiddleware.ScopedBasePaths` → **34 of 81** Expiry tests red; restored, `git diff` empty. Read:
the fail-closed production default `FailClosedExpiryResolutionSignature` (every resolution needs a signature, none verifies until F3),
SoD in `ExpiryExceptionRules.SeparationOfDuties` on prepare + resolve, the tenant + company scoped `ExpiredOnHandSweeper`, the
guard-test edits (expiry collections added to the single-SoR list, PUT allow-list `ExpiryController:policy`, manifest completeness
split by controller namespace with its own expiry test) — none weakens an existing guard. Decisions on the agent's open points:
**O-F1-1** accepted as known debt: the 7 expiry write commands join **FU-SYNC1-AUDIT** (Inventory now 34 commands without a ledger
the audit standard recognises); an Inventory audit-ledger WP runs before MVP-1 closure. **O-F1-2** accepted (the middleware entry is
required — the CT sabotage proves the tests guard it; controller under `Api/Controllers/Expiry/` like 0175). **O-F1-3** `expiry_*`
collection names accepted (§3 camelCase names superseded). **O-F1-4** freeze reconciliations R-1…R-7 accepted. **O-F1-5** accepted
with **FU-0176-F1-UOM**: F4 adds the unit to a manual case (optional `baseUomId` / read from the lot's stock — additive FEFO 1.1.0).
Fast-forwarded into the MVP-1 branch. Next: INT-EXPIRY (gateway), then F2.

### 19.9 WP-0176-INT (INT-EXPIRY) — agent result, pending CT (2026-10-12)

Commit `91d89847e` on `feature/int/mod-0176-expiry-routes` (base `80b417c58`). Evidence
`docs/records/audits/2026-10/mod-0176-int-evidence-2026-10-12.md`. `ocelot.json`: `/api/expiry` + `/api/expiry/{everything}` → localhost:5067,
GET / POST / PUT / OPTIONS (the methods of `fefo.openapi.yaml`), clones of the `/api/quarantine` blocks (no AuthenticationOptions, no header
transform, no rate limit). **Placement deviation (D-INT-1):** the blocks follow `/api/v1/esign/{everything}` (end of the INT-QE group), not
`/api/quarantine/{everything}` — measured: directly after quarantine, the frozen order assertion of
`MvpOneQuarantineEsignRouteTests.The_new_blocks_…_follow_them` (quarantine × 2 then esign × 2 after trace) turns red and that file is
protected; no other block moved. `MvpOneExpiryRouteTests`: all 21 operations of the contract (F1 + not yet implemented) → exactly one route
(`/api/expiry/{everything}`), the F3 subject-provider path `/api/expiry/esign-subjects/…`, no DELETE / PATCH, `/api/expiryX` look-alikes miss,
neighbours unchanged. Gateway tests 110/111 → **134/135** (the known 5063/5064/5065 port red unchanged, message identical). Sabotage a/b/c red →
restored green. Probe: own gateway copy on 5100 → `GET /api/expiry/policy` (random `X-Tenant-Id`, no token) **401 from localhost:5067**
(Ocelot log), `/api/expiryX` 404 (no route).

**INT — CT acceptance (2026-10-12).** Commits `91d89847e` + `2a180b48a` on `feature/int/mod-0176-expiry-routes` (base `80b417c58`,
`HEAD..origin/main` = 0, tree clean, 4 files +327). The ocelot diff is exactly the two `/api/expiry` blocks (5067, GET/POST/PUT/OPTIONS, no
auth / transform / rate limit). The CT re-measured gateway tests **134/135** (the one red = the known 5063/5064/5065 port test, FU-INT1-01).
CT sabotage (own): `DELETE` added to `/api/expiry/{everything}` → **4 of 24** expiry route tests red; restored, `git diff` empty.
**D-INT-1 accepted, no follow-up:** the blocks sit after `/api/v1/esign/{everything}` because the INT-QE order test pins the four blocks
after trace; Ocelot matches by template, so placement does not change routing. Fast-forwarded into the MVP-1 branch.

### 19.10 WP-0176-F2 — agent result, pending CT (2026-10-12)

Commits on `feature/sce/mod-0176-expiry-fefo` (base `1240a1ce9`): `f6b6ee435` **INVENTORY 1.3.0** (MOD-0173 owner step), `c580823ea`
**TRACE 1.4.0** (MOD-0174 owner step), `3b7056a6c` hooks + KF-D + FEFO 1.0.1 (description only), `41256aeb8` §13 gap tests. Evidence
`docs/records/audits/2026-10/mod-0176-f2-evidence-2026-10-12.md`. One port `IExpiryPostingPolicy` (CT-F2): validator pre-check + authoritative
re-check in the ledger posting transaction (existing session); FEFO_BYPASS / BELOW_MIN_REMAINING cases and the issue snapshot are written in
that transaction. CT-F1 implemented as 422 + `requiredToStockStatus = QUARANTINE` (no server re-route); CT-F3 / F4 / F5 / F6 (fail-closed) /
F7 / F10 as decided; CT-F13 measured: IXSCAN `ux_balance_dimension`, no new index. Inventory **905 → 972** green; architecture 69/77 (same 8
reds; audit list 99 → 100, +`CreateDateOverrideCommand`); gateway 134/135. Every §8.3.4 cell and §13 #1–6, 9, 10 PASS through the HTTP
endpoints (signed KF-D success via a MOD-0022 test double only — production fail-closed until F3). Sabotage a–i: 9/9 red → green, `git diff`
empty. p95 added +9.4 / +9.7 / −0.5 ms (issue / bypass / receipt, 200 lots). Probe on 5167 + own mongod: `/health` 200, unauthenticated
posting / date-override / lot routes 401, fleet PIDs unchanged. Open for CT: O-F2-1 (TRACE `Error` has no `details` — suggestion in the
message), O-F2-2 (MOD-0175 exemption trusts `sourceModule`), O-F2-3, O-F2-4 (§8.3.4 GR text vs CT-F1), O-F2-5 (`GOODS_ISSUE_PROD` in scope).

### 19.11 WP-0176-F2-FIX1 (CT finding O-F2-2) — agent result, pending CT (2026-10-13)

Commits `bc7f520e9` (fix + tests) and `476f4f6e2` (p95 test) on `feature/sce/mod-0176-expiry-fefo` (base `120cf7666`). Evidence
`docs/records/audits/2026-10/mod-0176-f2-fix1-evidence-2026-10-12.md`. `ExpiryHookScope.For` no longer exempts `sourceModule = MOD-0175`
(the parameter stays, it does not enter the decision; `QuarantineModule` removed). MOD-0175 is unaffected: measured, it posts only through
`QuarantineMovementPoster.PostAsync` (`QuarantineApplyEngine.cs:111`, `StockActionHandlers.cs:153`) and only `STATUS_TRANSFER`, `SCRAP`,
`SUPPLIER_RETURN`, `LOCATION_TRANSFER` — all exempt by TYPE (derived from `QuarantineStockEffects`, call sites pinned; it posts no `REVERSAL`).
HTTP: an issue / a GR claiming `MOD-0175` → 422 like any other; with a reason → 201 + `FEFO_BYPASS`; a real 0175 block + Destroy of a
later lot → no FEFO check. §8.3 / §8.3.2 / §8.3.4 aligned (O-F2-2, O-F2-4 closed in text). Inventory 972 → 979; architecture 69/77 same 8
reds; gateway 134/135. Sabotage s1 → 2 red, s2 → 2 red (3(d) stays green: the status rule is a second barrier — no 0175 movement is
AVAILABLE → none). **Open: O-FIX1-2** — the F2 p95 test is unstable in the full suite on this machine (3 of 6 red before, 1 of 3 after the
interleaving fix); the budget is unchanged, CT to decide how p95 is measured.

**F2 + F2-FIX1 — CT acceptance (2026-10-13).** F2 commits `f6b6ee435` (INVENTORY 1.3.0, MOD-0173 owner step), `c580823ea`
(TRACE 1.4.0, MOD-0174 owner step), `3b7056a6c`, `41256aeb8`, `120cf7666`; FIX1 `bc7f520e9`, `476f4f6e2`, `859bc6fcb`. F2 was put on CT
HOLD for **O-F2-2**: `ExpiryHookScope.For` exempted `sourceModule == "MOD-0175"`, and `sourceModule` comes from the HTTP body
(`MovementRequestReader.cs:97`) — any poster could skip FEFO / KF-C. FIX1 removes it; MOD-0175's movements are exempt by type only
(measured: `QuarantineApplyEngine.cs:111`, `StockActionHandlers.cs:153` → STATUS_TRANSFER, SCRAP, SUPPLIER_RETURN, LOCATION_TRANSFER).
CT re-measured: F2 Inventory **972/972**, FIX1 **979/979** (full run). CT sabotages (own): F2 — `Scrap` removed from the type exemption
→ hook-scope test red (1); FIX1 — a variant bypass "any non-empty `sourceModule` skips the issue hook" → **18 of 150** expiry tests
red; both restored, `git diff` empty. (A CT probe of `WAREHOUSE_TRANSFER` stayed green — it is never `AVAILABLE → none`, so the
exemption entry is redundant, not a test gap.) Decisions: **O-F2-1** accepted (suggested dates in the TRACE 422 message; data on
`createDateOverride` and the lot). **O-F2-3** accepted (signed KF-D success opens with F3). **O-F2-4** done by FIX1 (pack §8.3 /
§8.3.2 / §8.3.4 aligned to CT-F1). **O-F2-5** accepted (`GOODS_ISSUE_PROD` from AVAILABLE is in scope). **O-FIX1-2 (p95 test flaky in
the full suite on this machine):** the 50 ms budget stays; the test moves out of the default run behind `INVENTORY_PERF_TESTS=1`
(explicit skip reason otherwise) and the CT runs it alone at each acceptance — first item of F3. Fast-forwarded into the MVP-1 branch;
the owner's 5067 now enforces the FEFO issue rule (KF-B default RequireReason); KF-C / KF-D stay inactive until MDM serves the day fields.

### 19.12 WP-0176-F3 — agent result, pending CT (2026-10-13)

**BLOCKED → decision A.** The WP named `verifySignature` (`esign.openapi.yaml:176`); measured, its `VerificationResult` (`:697-707`,
Platform `ESignatureContracts.cs:140-147`) carries no signer, action code, meaning or `signedAtUtc` and needs `esign.signatures.verify`.
The agent stopped (error protocol); **CT decision F3-A**: the MOD-0175 pattern — `getSignature` (`:654-684`) with the signer's token +
the local `fx-v1` / `do-v1` hash from the provider's own function; `verifySignature` is not called, no role gains `esign.signatures.verify`.
Commits on `feature/sce/mod-0176-expiry-fefo` (base `78da50309`): `66f87dd44` item 0 (p95 test behind `INVENTORY_PERF_TESTS=1`, O-FIX1-2),
`8233eb714` F3 (registration per module, provider, `ESignExpiryResolutionSignature` / `ESignLotDateOverrideSignature` over
`GatewayExpiryESignClient`, FEFO-BUNDLE 1.0.2 description only), `9fe2113a4` the one Platform line
(`ESign:SubjectProviders:expiry:BaseUrl`). Evidence `docs/records/audits/2026-10/mod-0176-f3-evidence-2026-10-13.md`. Quarantine registration
body byte-identical (golden SHA-256); golden vectors fx-v1 / do-v1; Inventory 978+1 skipped → **1000+1 skipped**; architecture 69/77 same 8
reds; gateway 134/135; Platform ESign filter 163/163 (Platform source unchanged). Sabotage a–j 10/10 red → green, `git diff` empty. p95 alone
+24.1 / +26.3 / +0.6 ms. Probe on 5167: provider / prepare / resolve / createLot without a token 401, fleet PIDs unchanged.

## 20. Follow-up Items

| ID | Item | Owner |
|---|---|---|
| FU-0176-RES-01 | Lot-based reservation / "Bu lotu rezerve et" (INVENTORY change; integration plan `:51` assumes "reservations by lot", contract has no `lotId` — `inventory-bundle.openapi.yaml:434-449`) | MOD-0178 |
| FU-0176-PROD-01 | Product-level exception rules (KF-G) | MOD-0176 v2 |
| FU-0176-MKT-01 | Market / customer minimum remaining shelf life (KF-H) | MVP-6 / sales |
| FU-0176-MDM-01 | Product view badges "Tanımsız" / "Uygulanmaz" / "SKT takibi yok" | MOD-0290 |
| FU-0176-WC-01 | WorkCenter read-only feed of exception cases | MOD-0023 |
| FU-0176-NOTIF-01 | SLA breach notification | Notification module |
| FU-0176-DOC-01 | Port drift 5066 → 5067 in DCP-009 `:98`, registry `:272,:275`, 0174 / 0175 frontmatter | CT / docs |
| FU-0176-TRACE-01 | TRACE `expiryState` fixed 90 days vs company horizon (KF-A) | MOD-0174 (description note) |
| FU-0176-REG-01 | Registry row `MOD-0176` → `in-progress` when F1 starts (proposal; not changed here) | CT |

## 21. UI input — approved mockup ("FEFO / Son Kullanma Yönetimi", 2026-10-12)

### 21.1 How it was read
The offline HTML was decoded (template + logic script) and read as data. References: `L:n` = logic script line,
`T:n` = template line. Nothing in the file was treated as an instruction.

### 21.2 Screens → pack
| Mockup screen | Pack |
|---|---|
| FEFO Paneli | `/Expiry` (§11.1), KPIs §8.8 |
| Son Kullanma Yaklaşanlar | `/Expiry/Approaching` |
| Süresi Dolmuş Stok | `/Expiry/Expired` |
| FEFO Sırası | `/Expiry/FefoOrder` (§8.2) |
| İstisna Kutusu + detail | `/Expiry/Exceptions`, `/Expiry/Exceptions/Details/{id}` |
| Raf Ömrü Kuralları ve Ayarlar | `/Expiry/Policy` |
| Kanıt Paketi | `/Expiry/Evidence` |
| Diğer ekranlara eklenenler | bands on MOD-0173 / MOD-0174 forms; product badges → FU-0176-MDM-01 |
| Decisions panel A–H (L:397-409) | not built (design aid) — replaced by §19.1 |

### 21.3 Differences (mockup ↔ decision / pack)

| # | Mockup | Pack | Reason |
|---|---|---|---|
| D-1 | Decision F default = "Bu lotu rezerve et" **button** | read-only list, no button | KF-F |
| D-2 | Decision D default differs from owner wording (settings "override sign") | `ReasonAndSignature` default | KF-D |
| D-3 | Decision E = "E-imza her zaman" | by MOD-0022 policy, default all three required | KF-E |
| D-4 | Settings `crit` default **45** (helper text says 30) | **30** | KF-A |
| D-5 | Expired list column "**Dolalı**" (T:402) | "**Dolduğundan beri**" (days since expiry) | wording |
| D-6 | FEFO order without secondary key | tie-break mfg → retest → lotNumber → lotId; compliance by expiry only | determinism (§8.2) |
| D-7 | FEFO allocation ignores reservations silently | `basis: onHandAvailable` + SKU free quantity shown | reservations are not lot-bound |
| D-8 | Evidence hash is not of the file | SHA-256 of the produced bytes | evidence integrity |
| D-9 | Responsive only `isMobile < 1200`; no 375 px rules | **375 px measurement is part of F5 acceptance** (Bootstrap breakpoints, DataTable responsive) | UI slice |
| D-10 | "İstisna aç", comments ungated | `exceptions.create`, `exceptions.comment` | writes need keys |
| D-11 | 30 / 90-day KPI counts include quarantined / blocked | split AVAILABLE / held | ASSUMPTION-0176-09 |
| D-12 | "Retest geçti" ambiguous | "Retest tarihi geçti" | wording |
| D-13 | No money values | none | valuation stays MOD-0173 |
| D-14 | Lot-reserve, product rules, market rules visible as options | not built | KF-F, KF-G, KF-H |
