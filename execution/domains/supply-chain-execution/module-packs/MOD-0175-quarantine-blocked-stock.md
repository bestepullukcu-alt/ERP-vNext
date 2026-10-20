---
id: MOD-0175
name: Quarantine / Blocked Stock
domain: supply-chain-execution
service: Diten.InventoryService (5066, in-process with the MOD-0173 ledger and MOD-0174 lots — domain-config OD-5) + frontend/Diten.Web
shell: tenant
golden_reference: compact
data_mode: server
screens:
  - module: QuarantineQueue
    data_mode: server
  - module: BlockedStock
    data_mode: server
  - module: RetestQueue
    data_mode: server
entity_base: EntityBase
status: ready-for-dev
status_note: "draft — WP-0175-P0 (CT-MVP1-0175-P0 v1), 2026-10-07. Owner decisions KQ-A…KQ-I (2026-10-07) recorded in §19 as DECISIONS. CT decisions CT-Q1…CT-Q14 TAKEN 2026-10-07 (§19; CT-Q2/CT-Q3 owner). Precondition WP-0023-FX (owner: we fix MOD-0023). WP-0175-P0 CT-ACCEPTED 2026-10-07. ready-for-dev (owner) 2026-10-08. QUARANTINE-BUNDLE v1 proposal: docs/analysis/contracts/drafts/quarantine-bundle-v1-proposal.md (no quarantine contract exists — K12). Two measured prerequisites outside this module: MOD-0023 SoD defect (FU-0175-WF-SOD) and PlatformAdmin-scoped workflow runtime keys (FU-0175-WF-KEYS) — §8 / §19. DCP-002 verifier not run (no Python — waiver W-0175-01, W-0173-01 pattern)."
owner: supply-chain-execution / mvp-1 control-tower
branch: feature/sce/mod-0175-quarantine
started: 2026-10-07
target: 2026-10-31
form_field_count: 9
dependencies: [MOD-0173, MOD-0173-FU01, MOD-0173-FU02, MOD-0174, MOD-0022, MOD-0023, INVENTORY-BUNDLE-v1, TRACE-v1.2, LOCATION-v1.1, ESIGN-BUNDLE-v1, DEV-0001]
---

# MOD-0175 — Quarantine / Blocked Stock

> **Identity (DCP-002):** `MOD-0175 · Quarantine / Blocked Stock` is a Blueprint module (Blueprint 8.1 `Blueprint_Data`
> row 176: "Quarantine / Blocked Stock · W-3 · Lot/Batch/Serial; Workflow Designer · Quarantine Console; Release Approval
> Trace; Evidence Pack · QUARANTINE-BUNDLE (quarantine states, release approvals, audit hooks, evidence export)"),
> registered `reserved / planned` (`execution/registries/module-id-registry.md:274`: "DCP-002 gate OK 2026-09-11. DCP-009.
> Status lifecycle only; no second balance"). **Not re-measured:** no Python on the MVP-1 machine → waiver **W-0175-01**
> (§18), W-0173-01 pattern. No registry row is changed by this pack.
>
> **Authority:** this pack > `execution/domains/supply-chain-execution/domain-config.md` > `AGENTS.md` > `.antigravity/`.
> Owner decisions **KQ-A…KQ-I (2026-10-07)** are binding (§19); where the mockup "Karantina Bloke Stok (offline).html"
> disagrees, KQ-* wins (§21.3). **DEC-INV-08 is kept without deviation**
> (`docs/analysis/inventory-capability-scope-and-dependency-report.md:307`: "StockStatus = MOD-0173 dimension; MOD-0175 =
> status lifecycle/governance owner. Quarantine release'de Workflow HARD, e-Signature CONDITIONAL").
>
> **Contract (K12):** the report marks QUARANTINE-BUNDLE "✓" (`…dependency-report.md:229`), but **no quarantine contract
> exists** in `docs/analysis/contracts/` (README `:7-42` lists none). This WP writes the proposal
> `docs/analysis/contracts/drafts/quarantine-bundle-v1-proposal.md`; it becomes `quarantine.openapi.yaml` (FROZEN) only
> after CT acceptance (Q1, single writer). Consumed frozen contracts are **not changed**: INVENTORY-BUNDLE v1.0.1
> (`inventory-bundle.openapi.yaml:9-10`), TRACE v1.2.0 (`trace.openapi.yaml:8-9`), LOCATION v1.1.0
> (`location.openapi.yaml:8-9`). The two additive notes that 0175 needs (one error code, one description note) are listed
> separately in the proposal §9 and need the owners' single-writer step.
>
> **No BLOCKED condition.** Every decision is expressed with existing INVENTORY-BUNDLE movement types and stock statuses
> (§8 stock-effect table; MovementType `inventory-bundle.openapi.yaml:252`, StockStatus `:255`). The Workflow consumer
> pattern applies to InventoryService: it already forwards the caller's bearer token to the Gateway
> (`services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Upstream/ForwardCallerHeadersHandler.cs:19-38`).
> The mockup was read. Two **measured defects outside this module** limit KQ-C and who can act; both are listed as
> prerequisites, not hidden (§8 "Workflow — measured limits").

## 1. Module Summary

MOD-0175 is the **quality decision layer on top of the stock ledger**. It does not keep a balance (report `:141-142`,
`:182-183`). It turns stock that sits in `QUARANTINE` / `QUALITY_INSPECTION` / `BLOCKED` into decisions:
- release, reject, block (in part or in full, KQ-A);
- disposition of blocked stock: destroy, return to supplier, rework, keep as sample/reference (KQ-E);
- unblock;
- the hard block of retest-due lots (KQ-H);
- the approval of lot releases that MOD-0174 hands over through `ILotStatusChangeGate` (KQ-I).

Every release, rejection, disposition and unblock goes through a **Workflow instance** (KQ-F) that a second person
approves in WorkCenter with an **e-signature** (KQ-G, MOD-0022 E4). Only after approval does 0175 post the stock movement
through the ledger's own in-process posting path: `STATUS_TRANSFER`, `SCRAP`, `SUPPLIER_RETURN` or `LOCATION_TRANSFER`.

MVP-1 chain: `0290 → 0173 → 0174 → {0175 ∥ 0176 ∥ 0177}` (DCP-009 `:63`). 0175 also needs MOD-0022 (ready-for-dev,
`MOD-0022-e-signature-service.md` frontmatter) and MOD-0023 (live, §8).

### Slice plan (each slice = its own Work Package, CT-verified before the next)

| Slice | Scope | Contract | Closes |
|---|---|---|---|
| **Q1 — Records, decisions, apply engine, contract** | `QuarantineHold`, `QuarantineDecision` (+ events, insert-only), `BlockedStock`, `QuarantineSettings`, `BlockReason`; intake observer + intake sweep; decision validation (KQ-A, CoA, reason, block reason); **apply engine** (decision → ledger movements, idempotent) driven by an internal approval port; "Karantinaya Al" (manual); manual block; review start/complete; manifest `QUARANTINE-BLOCKED-STOCK`. Freeze `quarantine.openapi.yaml` v1.0.0. | QUARANTINE-BUNDLE v1 | KQ-A, KQ-D, KQ-E (movement), report §5 objects |
| **Q2 — Workflow integration** | `GatewayWorkflowClient` (user token), template JSON + "prepare template" action (tenant Admin publishes, CT-D6), start with idempotency + DisplayContext, completion-event consumer + inbox (transport switch, off by default), **read-time reconciliation** + "Durumu yenile", outcome → apply / reopen, withdraw (cancel). | QUARANTINE-BUNDLE §4.6–4.10 | KQ-F, KQ-C (with FU-0175-WF-SOD) |
| **Q3 — e-signature integration** | signable-subject provider endpoint (CT-D2 field list §8), signable-action registration (MOD-0022 CT-D12), decider signature verification on submit, template step `signatureRequired` + meaning. | ESIGN-BUNDLE §4.4, §5, §6 (consumer) | KQ-B, KQ-G |
| **Q4 — Retest hard block + lot gate** | `LOT_RETEST_DUE` posting rule + availability exclusion + retest sweeper (position adjust), retest queue, release-with-new-retest-date; `ILotStatusChangeGate` implementation (`PendingApproval` → 202) + approved lot status apply. | INVENTORY additive §9.1; TRACE 202 (already reserved) | KQ-H, KQ-I |
| **Q5 — UI** | 6 mockup screens + decide/disposition pages + settings page (§11). | — | mockup 1:1 except §21.3 |
| **INT-QUARANTINE** | Gateway `/api/quarantine` + `/api/quarantine/{everything}` → 5066 + route coverage test (integration-agent). | — | external consumers (Platform e-sign provider calls, 0142/0176/0177) |
| **L10n** | 7 languages for every key of §11.6 + block-reason names (data). | — | language parity |

Order: **Q1 → INT → Q2 → Q3 → Q4 → Q5 → L10n**. Q5 may start once Q2 is accepted, using the contract mock. L10n runs per
file with Q5.

Dependencies on other modules:
- Q2 needs **FU-0175-WF-SOD** (MOD-0023) before KQ-C can be claimed, and **FU-0175-WF-KEYS** or CT-Q2 (interim grant)
  before a non-Admin QA user can act.
- Q3 needs MOD-0022 **E2** (signature API + provider registry) live. The approver-side signature enforcement needs
  MOD-0022 **E4**. G1 runtime proof needs E1 + E2 + E4 + E5.

## 2. Ownership and Boundaries

**Owns (single writer, K15 — domain-config `:48`):**
- Quarantine holds: the record of stock waiting for a quality decision.
- Quality decisions and their lines.
- Blocked-stock records: block reason, pending disposition, review state.
- Disposition decisions.
- The retest hard-block rule (KQ-H).
- The approval of gated lot status changes (KQ-I).
- Quarantine settings per company: wait target, four-eyes, approver positions, sample locations.
- The tenant block-reason list.
- The workflow template JSON `INV-QUARANTINE-RELEASE` (seed content only — the template record is MOD-0023's).
- Permission namespace **`quarantine.*`** (proposed, PKS-001 §4; measured: no `quarantine.` key exists — namespaces in
  InventoryService are `inventory.*` and `trace.*`, `…/Infrastructure/Authorization/InventoryPermissions.cs:12-23`,
  `TracePermissions.cs:34-53`).

**Consumes:**
- MOD-0173 ledger: in-process posting and balance/transaction reads (same service, OD-5; the in-process precedent is
  MOD-0174's `ILotMasterClient`).
- MOD-0173-FU02 locations (sample locations).
- MOD-0174 lots: status, `RetestDate`, `coaReference`, `ILotStatusChangeGate`.
- MOD-0023 Workflow, over HTTP through the Gateway with the user's token.
- MOD-0022 e-signature, over HTTP through the Gateway, plus the provider endpoint that Platform calls.
- MDM legal entity (company selector W-0173-02).

**Does not own / must not do:**
- No balance, no stock-status values, no movement types (DEC-INV-08; report `:141`). Every stock change is a ledger movement.
- **No new stock status** (KQ-E): "Numune / Referans" is a *location* plus a decision kind, never a status.
- No release, rejection, disposition or unblock **without a Workflow instance** (KQ-F; prompt "YAPMA").
- **No own signature records** (KQ-G). Signatures live in MOD-0022; 0175 stores only `signatureId` references.
- No `/Platform/` route.
- No `inventory.*` or `trace.*` key in the 0175 manifest (FU-0174-CAT-01 lesson, `MOD-0174-lot-serial-tracking.md:841-851`).
- No change to INVENTORY-BUNDLE / TRACE / LOCATION wire shapes. Additive notes are owner steps (proposal §9).
- Does not release, cancel or edit other modules' reservations (CT-Q6).
- Does not edit MOD-0173 / MOD-0174 / MOD-0022 / MOD-0023 packs. The code seams it needs in their code are listed in
  §5 per slice, and the CT records them on those modules.

## 3. Owned Objects

| Object | Kind | Notes |
|---|---|---|
| `QuarantineHold` | entity | `quarantine_holds`; number `KRN-{yyyy}-{nnnn}`; never deleted; state machine §8 |
| `QuarantineDecision` | entity, **insert-only** | `quarantine_decisions`; number `QKR-{yyyy}-{nnnn}`; immutable after submit (it is the signed subject) |
| `QuarantineDecisionEvent` | entity, insert-only | `quarantine_decision_events`; Submitted, WorkflowStarted, StartFailed, Approved, Rejected, Cancelled, TimedOut, Applied, ApplyFailed, FourEyesViolation, Withdrawn |
| `BlockedStock` | entity | `quarantine_blocked_stock`; number `BLK-{yyyy}-{nnnn}`; never deleted; Open / Closed / SampleRetained |
| `QuarantineHistoryEntry` | entity, insert-only | `quarantine_history` ("Karar geçmişi" on hold and blocked records) |
| `QuarantineSettings` | entity | `quarantine_settings` per (Tenant, LegalEntity) |
| `BlockReason` | entity | `quarantine_block_reasons` per tenant; code + 7 names + active |
| `QuarantineEventInbox` | entity | `quarantine_event_inbox` (`_id` = EventId) |
| `QuarantineIntakeObserver` | port implementation | `IPostedMovementObserver` (new port on the MOD-0173 posting handler, post-commit) |
| `QuarantineIntakeSweeper` | hosted service | creates missed holds from ledger transactions (idempotent) |
| `RetestHoldSweeper` | hosted service | applies / removes the retest hold on SKU positions at day rollover |
| `QuarantineApplyEngine` | application service | decision lines → `LedgerPosting`s through the ledger's internal posting service |
| `QuarantineLotStatusChangeGate` | `ILotStatusChangeGate` impl | replaces `AlwaysAllowLotStatusChangeGate` (Q4) |
| `GatewayWorkflowClient`, `GatewayESignClient` | infrastructure | typed HttpClients with `ForwardCallerHeadersHandler` |
| `QuarantineWorkflowReconciler` | application service | read-time `instances/by-objects` (≤ 100) |
| `QuarantineWorkflowOutcomeConsumer` | MassTransit consumer | registered only when `Eventing:Transport = RabbitMQ` (off on this machine) |
| `QuarantineController`, `QuarantineESignSubjectsController` | API | `/api/quarantine/**` |
| `QuarantineBlockedStockManifestProvider` | self-registration | `ModuleCode: "QUARANTINE-BLOCKED-STOCK"` |
| MVC `QuarantineController` + views + JS | Web | `/Quarantine/**` |
| Permissions | `quarantine.*` | §14 |

## 4. Entity Fields

**Entity base decision.** InventoryService entities derive from its own `EntityBase`
(`services/Diten.InventoryService/src/Diten.InventoryService.Domain/Entities/EntityBase.cs:9-27`: Id, TenantId?,
LegalEntityId?, CreatedAt, UpdatedAt, Version, IsDeleted, DeletedAt). That is the same base MOD-0173/0174 use. There is
**no delete** for any 0175 entity. Insert-only collections follow the ledger pattern (repository with insert + reads only,
pinned by an architecture test like `tests/…/Architecture/LedgerImmutabilityArchitectureTests.cs:13-40`).

### QuarantineHold

| Field | Type | Rule |
|---|---|---|
| `HoldNumber` | string | `KRN-{yyyy}-{nnnn}` per tenant (atomic counter) |
| `LegalEntityId` | Guid | from the intake movement / company selector (W-0173-02) |
| `ItemId`, `SkuId`, `SkuLevel` | ids | from the movement |
| `LotId` | string? | null for non-lot items |
| `WarehouseId`, `LocationId` | ids | balance bucket |
| `StockStatus` | `QUARANTINE` \| `QUALITY_INSPECTION` | bucket status at intake (mockup "Karantina" / "Kalite Kontrol") |
| `Quantity`, `BaseUomId` | decimal, id | quantity held by this record (decisions must total it, KQ-A) |
| `Source` | enum | `GoodsReceipt` \| `Production` \| `CustomerReturn` \| `Manual` \| `Retest` \| `Rework` \| `OtherInbound` |
| `SourceMovementId` | string? | ledger transaction id that created the bucket quantity (unique per tenant when set) |
| `SourceDocument` | {module, type, id, number}? | e.g. `MOD-0142 / GRN / …`; free text for Manual ("Hasar şüphesi · bidon sızıntısı") |
| `EnteredAtUtc`, `CreatedBy`, `CreatedByName` | | "Kaydı oluşturan" |
| `ReviewState` | `Waiting` \| `InReview` | "Bekliyor" / "İncelemede" |
| `ReviewStartedAtUtc`, `ReviewStartedBy` | | "İncelemeye alındı" |
| `AnalysisNote` | string? ≤ 2000 | "Analiz sonucu notu" |
| `Attachments` | list<{name, dmsRef}> ≤ 20 | references only ("yalnız referans"); DMS ids, no files stored by 0175 |
| `Status` | `Open` \| `DecisionPending` \| `Decided` | "Karar Verildi" = Decided |
| `ActiveDecisionId` | Guid? | the decision waiting for approval |
| `DecidedAtUtc` | DateTime? | |
| `RetestDateAtIntake` | DateTime? | for Retest holds |
| `Version` | long | optimistic concurrency (If-Match) |

Derived, not stored: `WaitDays` = days(EnteredAt, DecidedAt ?? today); `TargetDays` = settings `WaitTargetDays` (KQ-D);
`Late`; `ExpiryDate` / `coaReference` / `retestDate` read from the lot (MOD-0174 in-process).

Indexes:
- unique `(TenantId, HoldNumber)`;
- unique partial `(TenantId, SourceMovementId)` where SourceMovementId is set;
- `(TenantId, LegalEntityId, Status, EnteredAtUtc)`;
- `(TenantId, LotId)`.

### QuarantineDecision (insert-only, immutable after submit)

| Field | Type | Rule |
|---|---|---|
| `DecisionNumber` | string | `QKR-{yyyy}-{nnnn}` |
| `Kind` | enum | `HoldDecision` (Karar Ver) \| `Disposition` \| `Unblock` \| `LotRelease` (KQ-I) |
| `HoldId` / `BlockedStockId` / `LotId` | | target (one of) |
| `LegalEntityId`, `ItemId`, `SkuId`, `LotId`, `WarehouseId`, `LocationId`, `FromStockStatus`, `BaseUomId` | snapshot | the bucket the decision acts on |
| `AvailableQuantityAtDecision` | decimal | hold/blocked quantity at draft time |
| `Lines` | list<{lineNo, decision, quantity, toStockStatus}> | HoldDecision: 1 line, or 2 lines when partial (mockup); decision ∈ Release \| Reject \| Block |
| `BlockReasonCode` | string? | required if any line Reject/Block (mockup) |
| `Reason` | string 10–1000 | "Gerekçe" |
| `CoaChecked` | bool | must be true if any line Release |
| `CoaReference` | string? | snapshot from the lot (`trace.openapi.yaml:298`) |
| `Disposition` | {kind: Destroy \| ReturnToSupplier \| Rework \| Sample, quantity, supplierId?, supplierName?, returnDocumentNo?, destructionMethod?, destructionDate?, sampleLocationId?} | Disposition only |
| `NewRetestDate` | date? | required when releasing a Retest hold (KQ-H) |
| `LotStatusTarget` | `Released`? | LotRelease, or a hold release on a Draft lot (CT-Q5) |
| `DeciderUserId`, `DeciderName`, `DecidedAtUtc` | | from JWT |
| `DeciderSignatureId` | string? | MOD-0022 signature (policy-driven, KQ-B) |
| `WorkflowInstanceId`, `WorkflowIdempotencyKey` | | set by the `WorkflowStarted` event (a projection field written once) |
| `IdempotencyKey` | string | unique per (TenantId, DeciderUserId) |

Current state = last `QuarantineDecisionEvent`:
`Draft → Submitted → PendingApproval → Approved → Applied` | `Rejected` | `Cancelled` | `TimedOut` | `ApplyFailed` |
`FourEyesViolation` | `StartFailed` | `Withdrawn`.

### Other entities

- **QuarantineDecisionEvent** (insert-only): `DecisionId`, `Event`, `AtUtc`, `ByUserId?`, `ByName?`,
  `WorkflowOutcome?`, `ApproverUserId?`, `ApproverSignatureId?` (from the workflow history), `MovementIds[]` (Applied),
  `ErrorCode?`, `Detail?`.
- **BlockedStock:**
  - Fields: `BlockedNumber`, `LegalEntityId`, `ItemId`, `SkuId`, `LotId?`, `WarehouseId`, `LocationId`, `Quantity`
    (remaining), `BaseUomId`, `BlockReasonCode`, `Note?`, `BlockedAtUtc`.
  - `Origin`: `QuarantineDecision` (+ decision/hold refs) | `ManualBlock` | `Rework` | `LotBlocked` (**virtual row**, read
    from MOD-0174, never stored).
  - `PendingDisposition` (true after Reject), `Reviewed` (true after Reject or "İncelemeyi tamamla"),
    `Status` (`Open` | `Closed` | `SampleRetained`), `ActiveDecisionId?`, `Version`.
- **QuarantineHistoryEntry** (insert-only): `SubjectType` (`Hold` | `Blocked`), `SubjectId`, `AtUtc`, `By`, `Action`
  (Karantinaya alındı · İncelemeye alındı · Karar gönderildi · Onaylandı · Reddedildi · Uygulandı · …), `Reason`,
  `SignatureId?`, `ApproverSignatureId?`, `DecisionId?`.
- **QuarantineSettings** (per LegalEntity; tenant default row with `LegalEntityId = null`):
  - `WaitTargetDays` (1–60, default **10**, KQ-D);
  - `FourEyes` (bool, default **true**, KQ-C);
  - `ApproverCandidates` (list of `position:{guid}` / `user:{id}`, 1–10; required before any submit);
  - `SampleLocations` (map warehouseId → locationId, KQ-E);
  - `ReconcileAfterSeconds` (default 30);
  - `Version`.
- **BlockReason:** `Code` (`^[A-Z][A-Z0-9_]{1,31}$`), `Names` {en,tr,fr,es,zh,ar,ru}, `IsActive`, `FirstUsedAtUtc?`.
  Seeded: `QUALITY_REJECT` (Kalite ret), `DEVIATION` (Sapma), `DAMAGE` (Hasar), `EXPIRY` (SKT), `OTHER` (Diğer) — mockup
  `REASONS` (`kar.app.js:33`). Decision CT-Q8.

**Form field count (§6):**
- Karar Ver = **9**: decision, partial switch, line 1 decision, line 1 quantity, line 2 decision, line 2 quantity, block
  reason, reason, CoA check. That makes it `compact` (frontmatter).
- Disposition = **7**: kind, quantity, reason, supplier, return doc no, destruction method, destruction date. The sample
  location is resolved from settings, not typed.
- Unblock = 1 (reason).
- Settings = 5.
- The e-signature step is MOD-0022's shared dialog and is not counted.

## 5. Repo Scope

- **Q1:**
  - `services/Diten.InventoryService/src/**` — new `Features/Quarantine/**` (Application), `Domain/Quarantine/**`,
    `Persistence/Quarantine/**`, `Api/Controllers/Quarantine/**`, `Api/ModuleRegistration/QuarantineBlockedStockManifestProvider.cs`,
    `Infrastructure/Authorization/QuarantinePermissions.cs`.
  - **MOD-0173 seam (additive):**
    - `IPostedMovementObserver` port called post-commit by `PostMovementHandler` (`…/Handlers/CommandHandlers/PostMovementHandler.cs:27,52`);
    - an internal posting service extracted from `PostMovementHandler` so 0175 can post with an explicit LegalEntity +
      actor (same validators, same `IInventoryLedgerRepository.PostAsync`, `…/Domain/Repositories/IInventoryLedgerRepository.cs:27`).
  - `Program.cs` registration.
  - Tests under `services/Diten.InventoryService/tests/**`.
  - Contract `docs/analysis/contracts/quarantine.openapi.yaml` + README row (after CT acceptance).
- **Q2:** `Infrastructure/Workflow/**` (client, settings), `Infrastructure/Eventing/**` (consumer, transport switch — same
  shape as CrmService `DependencyInjection.cs:191-227`), `Persistence/Quarantine/QuarantineEventInboxRepository.cs`,
  template JSON resource `Application/Features/Quarantine/Workflow/inv-quarantine-release.v1.json`.
- **Q3:** `Api/Controllers/Quarantine/QuarantineESignSubjectsController.cs`, `Infrastructure/ESign/**` (signature read
  client, action registration on start — internal S2S like `ModuleRegistrationHostedService.cs:83-89`).
- **Q4:**
  - **MOD-0174 seams:**
    - `Domain/Traceability/Rules/LotPostingRules.cs` (new `RetestDue` violation with the 0175 exceptions);
    - `Application/Common/InventoryErrorCodes.cs` (`LOT_RETEST_DUE`);
    - `ChangeLotStatusHandler.cs:84-88` (`PendingApproval` → 202 instead of `NotSupportedException`);
    - `TraceabilityServiceCollectionExtensions.cs:26` (gate registration);
    - `Persistence/Traceability/LotHeldStock.cs` (retest hold);
    - `GetAvailabilityHandler.cs:84-93` (retest-due lots excluded);
    - an internal `ApplyApprovedLotStatusChange` command.
  - Web Lots page: 202 handling → submit page.
- **Q5:** `frontend/Diten.Web/Controllers/QuarantineController.cs`, `Views/Quarantine/**`, `wwwroot/assets/js/Quarantine/**`,
  `Resources/Views/Quarantine/**` (7), `Resources/SharedResource.*.resx` (nav keys only).
- **INT:** `gateway/Diten.ApiGateway/ocelot.json` + tests — integration-agent only.
- **This WP (P0):** only the two new files.

## 6. Protected Paths

- `.antigravity/**`, `AGENTS.md`, `CLAUDE.md`, `execution/registries/**`, DCP files, `domain-config.md`, the inventory
  report, other packs (MOD-0173/0174/0022/0023/0142).
- All frozen contracts (`inventory-bundle`, `trace`, `location`, `grn-event`, …) — only the owners write the additive
  notes of proposal §9.
- `_Layout.cshtml` (FROZEN), Archive folders, `_LayoutPlatformAdmin.cshtml`.
- Diten.Platform code (MOD-0023 / MOD-0022 changes are their own slices: FU-0175-WF-SOD, FU-0175-WF-KEYS, MOD-0022 E2/E4).
- `ModulePageDescriptorNormalizer.ScopeFromRoute` and its tests.

## 7. Dependencies

| Dependency | State (measured at 6e4d670e5) | Effect |
|---|---|---|
| MOD-0173 ledger | in-progress, 75 % (`execution/registries/module-implementation-status.md:94`) | in-process posting; STATUS_TRANSFER has no allow-list (`…/Domain/Rules/MovementDirectionRules.cs:96-107`), so the transition rules are 0175's |
| MOD-0173 STATUS_TRANSFER ownership | "STATUS_TRANSFER is not a manual action here — status lifecycle belongs to MOD-0175" (`MOD-0173-inventory-ledger-valuation.md:519-520`; FU01 `:71,788`) | 0175 is the only STATUS_TRANSFER caller |
| MOD-0173-FU02 locations | 85 %, LOCATION v1.1.0 FROZEN; `LocationType` = Site/Warehouse/StorageLocation/Zone/Bin only (`location.openapi.yaml:205`); no purpose field; a location cannot force a status (`MovementMasterDataValidator.cs:243`) | KQ-E sample location = a normal stockable location chosen in 0175 settings |
| MOD-0174 lots | 90 %, all slices CT-accepted (`module-implementation-status.md:95`) | `ILotStatusChangeGate` (`…/Traceability/Ports/ILotStatusChangeGate.cs:12-21`, enum Allow/PendingApproval), CT-D1, CT-D4, K-F |
| MOD-0022 e-signature | ready-for-dev 2026-10-07; E1…E6 not built | Q3 waits for E2; approver signature waits for E4 |
| MOD-0023 workflow | live (CRM claims accepted, `WP-CL-BE-3-workflow-cross-service.md:214-225`) — **two measured limits** (§8) | Q2 + prerequisites |
| RabbitMQ | not running on this machine (memory `restart-all-services-watch.md:15`; `docs/records/audits/2026-10/mod-0173-g1-closure-measurement-2026-10-02.md:82`) | read-time reconciliation is the working path |
| MOD-0048 reference data | no inventory reason set (`MOD-0173-FU01-inventory-ledger-ui.md:199`, BG-03); MOD-0048 excludes tenant ERP lookups (`MOD-0048-lookups-reference-data.md:76-78`) | local block-reason list (CT-Q8) |
| MOD-0142 GRN | ready-for-dev, procurement (`module-id-registry.md:309`); `ToStockStatus` AVAILABLE/QUALITY_INSPECTION/QUARANTINE (`MOD-0142-receiving-grn.md:57,119`; `grn-event.openapi.yaml:73`) | extension point — intake observer picks up its postings automatically |
| MOD-0194 production | reserved / planned (`module-id-registry.md:293`) | extension point (same observer) |
| MOD-0176 / 0177 | reserved / planned (`:275-276`), no packs | read consumers of QUARANTINE-BUNDLE |

## 8. Runtime Constraints

**Service placement — why in-process in InventoryService.**
- OD-5 assigns MOD-0173…0177 to `Diten.InventoryService` (5066) (`domain-config.md:64`; DCP-009 `:78`). Note that
  `domain-config.md:62-63` still names `Diten.SupplyChainService` 5061 — stale (§20).
- The quarantine apply step must post several ledger movements and update 0175 records consistently. The ledger posts in
  one Mongo transaction (`InventoryLedgerRepository.cs:162-217`), and MOD-0174 already integrates in-process through
  ports (`ILotMasterClient`, `ILotStatusChangeGate`).
- An HTTP hop would add a second idempotency layer and a cross-service failure mode without any isolation benefit, since
  it is the same database and the same deploy unit.
- The domain-config "contract-first" rule (`:49`) is kept: 0175 uses **ports**, not ledger collections. External
  consumers use QUARANTINE-BUNDLE over HTTP.

**Company (LegalEntity).** Every 0175 request needs the company context of W-0173-02 (header `X-Legal-Entity-Id`
validated against MDM; `MOD-0173-inventory-ledger-valuation.md:387-399`, `InventoryScopeResolutionMiddleware.cs:19-23,122-160`).
Records carry `LegalEntityId`. Cross-LE access → 403 `CROSS_LE_FORBIDDEN`.

**Where holds come from (MVP-1 real vs extension point).**

| Source | MVP-1 | How |
|---|---|---|
| Manual "Karantinaya Al" on AVAILABLE stock | **real** | 0175 posts `STATUS_TRANSFER AVAILABLE→QUARANTINE`, then creates the hold (Source Manual) |
| Retest (KQ-H) "Karantinaya Al" | **real** | same, Source Retest, for every AVAILABLE bucket of the lot |
| Goods receipt into QUARANTINE / QUALITY_INSPECTION | **real** through the MOD-0173 manual inbound (FU01 allows initial status AVAILABLE · QUALITY_INSPECTION · QUARANTINE, `MOD-0173-FU01-inventory-ledger-ui.md:349`) | intake observer: any posted inbound leg with `toStockStatus ∈ {QUARANTINE, QUALITY_INSPECTION}` and `sourceModule ≠ MOD-0175` creates a hold (Source GoodsReceipt / OtherInbound) |
| Customer return (`CUSTOMER_RETURN` into QUARANTINE) | **real** through the MOD-0173 manual movement | observer, Source CustomerReturn |
| Rework disposition (`BLOCKED→QUARANTINE`) | **real** | 0175 posts and creates the hold (Source Rework) |
| MOD-0142 GRN | **extension point** (pack ready, not built) | same observer when 0142 posts `GOODS_RECEIPT_PO` with QUARANTINE/QI; no 0175 change |
| MOD-0194 production | **extension point** | same observer (`GOODS_RECEIPT_PROD`) |

**Intake observer and sweep.**
- The observer runs post-commit. A crash between commit and observer is covered by `QuarantineIntakeSweeper` (hosted
  service, every 5 min). It scans ledger transactions with a QUARANTINE/QI destination leg that have no hold, and creates
  the hold. The unique `SourceMovementId` makes it idempotent.
- Note: the backend does not enforce FU01's inbound status limit (`MovementDirectionRules.cs:56-61`). A movement into
  DAMAGED / BLOCKED does not create a hold. Blocked inbound appears as a `BlockedStock` record (Origin `OtherInbound`).

**Hold states.**
- Main path: `Open/Waiting` —(İncelemeye al)→ `Open/InReview` —(submit decision)→ `DecisionPending` —(approved+applied)→
  `Decided`.
- If the decision is rejected / cancelled / timed out / withdrawn, or fails to start or apply, the hold goes back to
  `Open` with its previous review state. The history explains why. **A hold is never deleted.**

**Stock-effect table (every decision → ledger movements; INVENTORY-BUNDLE types and statuses only):**

| Decision (UI) | Movement(s) posted by 0175 after approval | Stock status | Location | Availability (CT-D1) | Reservations |
|---|---|---|---|---|---|
| Karantinaya Al — manual / retest (no workflow, §8 KQ-F) | `STATUS_TRANSFER` per AVAILABLE bucket | AVAILABLE → QUARANTINE | same | ↓ | if reserved stock would be uncovered the ledger refuses `INSUFFICIENT_STOCK` (`ReservationPositionOperations.cs:84-93`) → 0175 409 `QUARANTINE_STOCK_RESERVED` with the reservation list (CT-Q6) |
| Serbest Bırak (Release) | `STATUS_TRANSFER` | QUARANTINE / QUALITY_INSPECTION → AVAILABLE | same | ↑, unless the lot is Blocked/Expired (lot hold, CT-D1) or retest-due (KQ-H) | none |
| Reddet (Reject) | `STATUS_TRANSFER` | QUARANTINE / QI → BLOCKED | same | — (already excluded) | none |
| Bloke Et (Block) | `STATUS_TRANSFER` | QUARANTINE / QI → BLOCKED | same | — | none |
| Kısmi karar (KQ-A) | one `STATUS_TRANSFER` per line, same `documentGroupId` = decision id | per line | same | per line | none |
| Disposition · İmha (Destroy) | `SCRAP` (outbound, `fromStockStatus = BLOCKED`) | BLOCKED → out | same | — | none |
| Disposition · Tedarikçiye İade | `SUPPLIER_RETURN` (`fromStockStatus = BLOCKED`) | BLOCKED → out | same | — | none |
| Disposition · Yeniden İşleme (Rework) | `STATUS_TRANSFER` | BLOCKED → QUARANTINE (new hold, Source Rework) | same | — | none |
| Disposition · Numune / Referans (KQ-E) | `LOCATION_TRANSFER` (status kept, `MovementDirectionRules.cs:82-94`) | BLOCKED stays BLOCKED | → company's sample location for that warehouse | — | none |
| Blokeyi Kaldır (Unblock) | `STATUS_TRANSFER` | BLOCKED → AVAILABLE | same | ↑ (lot rules as above) | none |
| Stoğu bloke et (manual block, no workflow) | `STATUS_TRANSFER` | AVAILABLE → BLOCKED | same | ↓ | as Karantinaya Al |
| Lot release (KQ-I) | no movement; lot status via `ApplyApprovedLotStatusChange` | lot Draft/Blocked → Released (stock status unchanged) | — | ↑ for AVAILABLE rows of the lot (`LotHeldStock`) | none |

- **Every movement** carries:
  - `sourceModule = "MOD-0175"`;
  - `sourceType = "QUARANTINE_DECISION"` (or `QUARANTINE_HOLD` / `QUARANTINE_BLOCK` for the no-workflow actions);
  - `sourceDocumentId = decision/hold number`, `sourceLineId = lineNo`, `documentGroupId = decision id`;
  - `movementReason = block reason code / disposition kind` (free text ≤ 200, `MovementRequestParser.cs:71,78`);
  - `idempotencyKey = quarantine:{decisionId}:{lineNo}`.
  These are existing fields of `MovementRequest` (`inventory-bundle.openapi.yaml:256-283`), so there is **no INVENTORY
  change**.
- **Lot rules (CT-D4) still apply.** On a lot whose *lot status* is Blocked or Expired, only `SCRAP` / `WRITE_OFF` /
  `REVERSAL` post (`LotPostingRules.cs:35-36,45-63`). So:
  - Release, Unblock, Rework, Sample and Supplier-return of a **lot-blocked** lot are refused (`LOT_BLOCKED` /
    `LOT_EXPIRED`).
  - 0175 checks this before submit (409 `QUARANTINE_LOT_HELD`), so no workflow starts for an impossible decision.
  - The Bloke Stok list shows lot-blocked lots as virtual rows (Origin "Bloke lot", mockup `BLK-2026-0076`) with only
    Destroy enabled.
  - Supplier return of expired lots is CT-Q7.
- **No new status or movement type.** "Numune / Referans" (mockup `ST.sample`, `kar.app.js:14`) is KQ-E: BLOCKED at the
  sample location plus `BlockedStock.Status = SampleRetained` plus a decision of kind Sample. The mockup's "Kalite Kontrol"
  (`ST.qc`) is the existing `QUALITY_INSPECTION`. "Mal kabul" / "Stok dışı" are display labels of movement endpoints, not
  statuses.

**Approval → apply.**
- When the decision's workflow outcome is `approved`, `QuarantineApplyEngine` posts the lines in order, each idempotent.
- Then it writes `Applied` (movement ids), updates the hold / blocked record, and creates the new `BlockedStock` / hold
  records:
  - Reject → BlockedStock (PendingDisposition = true, Reviewed = true);
  - Block → BlockedStock (Reviewed = false);
  - Rework → new hold.
- The engine runs **in-process with an explicit LegalEntity and actor**: the actor is the *approver* from the workflow
  history, with "applied by reconciliation/event" in the audit. It does not need the viewer's `inventory.movements.create`.
- If a line fails (stock moved meanwhile, lot became Blocked, period closed):
  - the engine stops and writes `ApplyFailed` with the ledger error code;
  - lines already posted stay posted, and the remaining lines are retried by "Yeniden uygula" with the same idempotency
    keys;
  - the CT may choose all-or-nothing instead (CT-Q9).
- **Retest release:** `NewRetestDate` is applied to the lot first (in-process MOD-0174 update, `LotChangeEntry` source
  "MOD-0175 {decision}") so the release `STATUS_TRANSFER` is not refused as retest-due.
- **Release on a Draft lot:** the approved decision also sets the lot Draft → Released (CT-Q5) before the movement.

**KQ-F — what needs Workflow and what does not.**
- **Needs Workflow:** Release, Reject, Block (inside a hold decision), every Disposition, Unblock, and gated lot releases
  (KQ-I). DEC-INV-08: no release path without workflow.
- **Immediate (no workflow, safety direction, KQ-B signature only if the policy row requires it):**
  - Karantinaya Al;
  - manual block;
  - İncelemeye al;
  - İncelemeyi tamamla.

  These only take stock *out of* use.
- One decision = one workflow instance, even when partial (KQ-A). Lines wait together; the stock is already not available
  while waiting.

**Workflow integration (CRM pattern, measured side by side).**

| Step | CRM claims (measured) | MOD-0175 design |
|---|---|---|
| Client | `GatewayClaimWorkflowClient` → Gateway `/api/v1/workflow/instances` (`services/Diten.CrmService/src/Diten.CrmService.Infrastructure/Workflow/GatewayClaimWorkflowClient.cs:49-100`); copies `Authorization` + `X-Tenant-Id` from the request (`:228-245`); no service token (`:11-18`) | `GatewayWorkflowClient` with InventoryService's existing `ForwardCallerHeadersHandler` (`Upstream/ForwardCallerHeadersHandler.cs:19-38`) and base URL `Inventory:Upstream:Gateway` (`UpstreamServicesOptions.cs:9-24`) |
| Start body | `StartWorkflowInstanceRequest` (`services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/WorkflowModels.cs:240-253`); CRM sends empty candidates (`GatewayClaimWorkflowClient.cs:58-59`) | `templateCode = INV-QUARANTINE-RELEASE`, `objectType = quarantine.decision`, `objectId = decisionId`, `objectRef = decision number`, `candidatePrincipalIds = settings.ApproverCandidates` (used only because the seeded template has none — template candidates win, `StartWorkflowInstanceHandler.cs:121-123`), `reasonCode = QUARANTINE_{KIND}`, `commentRequired = false`, `evidenceRequired = false`, `dueAt = enteredAt + WaitTargetDays` |
| Idempotency | code `crm:{objectType}:{id:D}:r{round}` (`…/Claims/ClaimReviewCore.cs:33`); Platform returns the existing instance (`StartWorkflowInstanceHandler.cs:61-71`); ≤ 128 chars (`StartWorkflowInstanceValidator.cs:37-39`) | `quarantine:quarantine.decision:{decisionId:D}:r1` (one round per decision; a new decision after a rejection is a new object) |
| DisplayContext | built at `ClaimReviewCore.cs:28-31`, chips `ClaimReviewHandlers.cs:147`; rules `WorkflowModels.cs:111-151` (title ≤ 200, subtitle ≤ 300, chips ≤ 5, relative deep link) | title "Karantina kararı {KRN} · {kind}", subtitle "{product} · Lot {lot} · {qty} {uom} · {company}", chips [decision kind, lot, warehouse], `deepLinkUrl = /Quarantine/Details/{holdId}` (or `/Quarantine/Blocked/Details/{id}`) — WorkCenter shows title/summary/tags/link (`WorkItemProjectionService.cs:70,85-86,160-163`) |
| Start after local state | round row written only after Platform accepts (`ClaimReviewHandlers.cs:148-163`); error map TemplateMissing 409 / Forbidden 403 / Rejected 400 / else 503 (`:49-59`) | decision `Submitted` → start → `WorkflowStarted` (instance id) / `StartFailed` (code). Hold → DecisionPending only after `WorkflowStarted`. Errors: template missing → 409 `QUARANTINE_WORKFLOW_TEMPLATE_MISSING`; 403 → `QUARANTINE_WORKFLOW_FORBIDDEN`; other 4xx → 400 `QUARANTINE_WORKFLOW_REJECTED`; 5xx/transport → 503 `QUARANTINE_WORKFLOW_UNAVAILABLE`. "Tekrar gönder" retries the start with the same key |
| Outcome event | `platform.workflow.instance.completed.v1`, one per instance, outbox in the terminal transaction (`WorkflowTerminalTransitionWriter.cs:80-98,112-153`); outcomes approved/rejected/cancelled/timed-out (`WorkflowModels.cs:83-99`) | `QuarantineWorkflowOutcomeConsumer` (filter name + `objectType = quarantine.decision`) → inbox `TryInsert` → applier; trust rule as CRM (`ClaimWorkflowOutcomeConsumer.cs:15-19`): apply only if the event's instance id equals the decision's `WorkflowInstanceId` |
| Inbox | `crm_event_inbox`, `_id` = EventId, duplicate → false (`CrmEventInboxRepository.cs:14,21-38`) | `quarantine_event_inbox`, same shape |
| Transport | MassTransit registered only if `Eventing:Transport = RabbitMQ` (`services/Diten.CrmService/src/Diten.CrmService.Infrastructure/DependencyInjection.cs:199-202`) | same switch; InventoryService has no MassTransit today (`Program.cs`; outbox "no transport is wired", `InventoryOutboxMessage.cs:4-6`) → new dependency in Q2, default off |
| Reconciliation | `ClaimReviewReconciler` — batches of 100, rounds older than `ReconcileAfterSeconds`, `instances/by-objects`, errors swallowed (`ClaimReviewCore.cs:382-490`); called from read handlers (`ClaimQueryHandlers.cs:55,…`) | `QuarantineWorkflowReconciler` called from the queue / detail / blocked list / retest reads and from "Durumu yenile": decisions in `PendingApproval` older than `ReconcileAfterSeconds` → `GET /api/v1/workflow/instances/by-objects?objectType=quarantine.decision&objectIds=…` (≤ 100, `GetWorkflowInstancesByObjectsQuery.cs:15`) → terminal outcome → same applier; approver + approver signature read from `GET instances/{id}/history` |
| SoD | "submitter cannot approve" only (`WorkflowTaskTransitionSupport.cs:118-127`) | KQ-C, with the measured defect below |
| Withdraw | find open task, cancel as caller, apply cancelled (`ClaimReviewHandlers.cs:316-363`) | "Kararı geri çek" (decider only) → `POST /api/v1/workflow/tasks/{id}/cancel` → `Withdrawn` |
| Template | created manually through the API by a logged-in Admin (`WP-CL-CFG-1-mlr-workflow-templates-grants.md:39,133-169`); **no template seeding exists anywhere** | CT-D6 of MOD-0022: 0175 ships the JSON. **Settings → "Onay şablonunu hazırla"** run by the tenant Admin calls `POST /api/v1/workflow/definitions` (create Draft, 409 `DUPLICATE_TEMPLATE_CODE` = already there) + `POST definitions/{id}/publish` with the Admin's own token (`WorkflowDefinitionsController.cs:31-37,55-64`). Idempotent; shows the published version. |

**RabbitMQ off (this machine) — what still works.**
- Measured:
  - Platform Dev config uses transport RabbitMQ with 5 retries (`services/Diten.Platform/src/Diten.Platform.API/appsettings.Development.json:59-73`).
  - The broker is down, so each completion event fails ~5 times and is **dead-lettered**.
  - Dead-lettered rows are never retried (`OutboxEvent.cs:129-136`, `OutboxEventRepository.cs:~85-96`).
- So events are **lost** here, and reconciliation is the only working path. It is therefore mandatory and tested with the
  consumer disabled:
  1. The approver approves in WorkCenter.
  2. The next read of any quarantine screen by a user who holds `platform.workflow.instances.view` (or "Durumu yenile")
     reconciles.
  3. The approved decision is applied.
- If nobody opens a 0175 screen, the stock stays in QUARANTINE until someone does. This is safe (fail-closed) and is shown
  as "Onaylandı · uygulanmayı bekliyor" in WorkCenter-linked views after reconciliation.
- A background reconciler is **not** possible without a service identity: there is no service-token path, by design
  (CRM `:11-18`). Recorded as CT-Q11.

**Workflow — measured limits (prerequisites, not hidden).**
1. **SoD defect (FU-0175-WF-SOD, owner MOD-0023).**
   - `StartedBy` is written as `_currentUserContext.ActorName` (`StartWorkflowInstanceHandler.cs:139,164`), which is the
     **e-mail claim first** (`services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/CurrentUserContext.cs:59-71`).
   - The tenant token carries `email` (`services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/TokenService.cs:35`).
   - SoD compares `instance.StartedBy` with `actorId`, which is the user **GUID** (`WorkflowTaskTransitionSupport.cs:109,118-127`;
     WorkCenter actor `WorkflowApprovalWorkItemActionDispatcher.cs:67`).
   - For tenant users these never match, so **"submitter cannot approve" does not fire**. The SoD tests set `StartedBy`
     directly (`tests/…/WorkflowCrossServiceTests.cs:743`, `WorkflowTaskTransitionTests.cs:540`).
   - Fix (MOD-0023): store `StartedByUserId` and compare ids.
   - **Defence in depth in 0175 (Q2):** when `FourEyes` is on, the applier compares the approver user id (history) with
     `DeciderUserId`. If they are equal, it writes `FourEyesViolation`, posts **nothing**, and shows the record as needing
     a new decision. **Sabotage proof in §16.**
2. **Runtime keys are PlatformAdmin (FU-0175-WF-KEYS, owner MOD-0023 / MOD-0018).**
   - Affected keys: `platform.workflow.instances.start`, `.instances.view`, `.tasks.approve`
     (`services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Authorization/permission-scope-baseline.csv:277-279`).
     The scope comes from the `/Platform/Workflow` routes (MOD-0022 §8 measurement).
   - They cannot be granted to a custom tenant role (`AssignPermissionCommandHandler.cs:57-61`).
   - Entitlement sync gives them to the tenant's built-in **Admin** role (`ModulePermissionResolver.cs:46-47,82-88`).
   - So today **both the QA decider and the QA approver must be in the tenant Admin role**. CT-Q2 decides the MVP-1
     interim; the fix is MOD-0023's.

**e-signature (KQ-B, KQ-G) — MOD-0022 only.**
- **Signable actions registered by 0175** (MOD-0022 CT-D12, `POST api/internal/esign/actions/register`, module
  `quarantine`, own prefix):

  | actionCode | When | Default meaning | Default visual mode |
  |---|---|---|---|
  | `quarantine.hold.release` | decision with a Release line (decider) | RELEASE | SavedAccepted |
  | `quarantine.hold.reject` | decision with a Reject line | APPROVE | DrawRequired (mockup `krn_reject: 'draw'`) |
  | `quarantine.hold.block` | Block line / manual block | APPROVE | Optional |
  | `quarantine.blocked.dispose` | any disposition | APPROVE | DrawRequired (mockup `krn_disp: 'draw'`) |
  | `quarantine.blocked.unblock` | unblock | RELEASE | None |
  | `quarantine.hold.create` | Karantinaya Al | APPROVE | None, `defaultRequired = false` |
  | `quarantine.lot.release` | KQ-I lot release | RELEASE | SavedAccepted |

  The tenant changes these in the MOD-0022 policy (per company, KS-5). A partial decision uses the strictest row of its
  lines (Release > Reject > Block).
- **Decider (mockup step 2).**
  1. The decision is saved as Draft.
  2. `ESign.open({module:'quarantine', subjectType:'quarantine.decision', subjectId: decisionId, actionCode})`.
  3. `POST /decisions/{id}/submit {signatureId}`.
  4. 0175 reads the signature (`GET /api/v1/esign/signatures/{id}`, user token) and checks: signer = caller; subject =
     this decision; meaning = policy meaning; not invalidated.
  5. If the policy row says *not required*, submit without `signatureId` is accepted (KQ-B; mockup "politika kapalı · bu
     adım atlanır").
- **Approver (KQ-F, mandatory).** The template step has `requirements.signatureRequired: true` and
  `signatureMeaning: APPROVE` (CT-Q4). The WorkCenter approve opens the dialog with the **same subject** (objectType /
  objectId of the instance = the decision) and context `{workflowInstanceId, workflowTaskId, stepCode, decision:"approve"}`.
  MOD-0022 E4 refuses approval without it (`MOD-0022-e-signature-service.md:547-561`).
- **Canonical subject fields (CT-D2 — this list is 0175's contract obligation, field names stable, versioned in
  QUARANTINE-BUNDLE).**
  - `subjectType = quarantine.decision`, version `qd-v1`.
  - `fields`:
    - `decisionNumber`, `decisionKind`, `holdNumber`, `blockedNumber`;
    - `legalEntityId`, `itemId`, `skuId`, `skuCode`, `lotId`, `lotNumber`;
    - `warehouseCode`, `locationCode`, `fromStockStatus`;
    - `line1.decision`, `line1.quantity`, `line1.toStockStatus`, `line2.decision`, `line2.quantity`, `line2.toStockStatus`;
    - `baseUom`, `blockReasonCode`, `reason`, `coaChecked`, `coaReference`;
    - `dispositionKind`, `dispositionQuantity`, `supplierId`, `returnDocumentNo`, `destructionMethod`, `destructionDate`,
      `sampleLocationCode`;
    - `newRetestDate`, `lotStatusTarget`, `deciderUserId`.
  - Quantities are invariant-culture decimals and dates are UTC "O" (ESIGN §7). Absent values are `null`.
  - Summary rows (≤ 20) are the Turkish/English labels of the mockup "İmzalanacak karar" box.
  - Because the decision is **insert-only after submit**, the subject hash never changes, so verification stays `valid`.
    The workflow and approval state are not part of the hash, on purpose.
- Provider endpoint `GET /api/quarantine/esign-subjects/quarantine.decision/{decisionId}`:
  - Called by Platform with the signer's JWT.
  - 404 if the caller cannot read quarantine (`quarantine.holds.read`) or the company does not match.
  - **The approver therefore also needs `quarantine.holds.read`** (§14).
  - Operator config: `ESign:SubjectProviders:quarantine:BaseUrl = http://localhost:5000/api/quarantine` (through the
    Gateway, INT-QUARANTINE).
- 0175 stores `DeciderSignatureId` and `ApproverSignatureId` only. It keeps no signature data (KQ-G).

**KQ-H — retest hard block.**
- Today MOD-0174 only warns (K-F, `MOD-0174-lot-serial-tracking.md:700`). The service has no retest check
  (`LotPostingRules` checks SKU / Blocked / Expired only, `:45-63`), and there is no retest sweeper.
- Q4 adds:
  - **Posting rule** `RetestDue` in `LotPostingRules`: `RetestDate < postingDate` (UTC day) on a lot ⇒ refuse with
    `LOT_RETEST_DUE` (422), **except**:
    - `STATUS_TRANSFER` from AVAILABLE into QUARANTINE / QUALITY_INSPECTION / BLOCKED;
    - `SCRAP`, `WRITE_OFF`, `REVERSAL`;
    - 0175 apply movements (`sourceModule = MOD-0175` with a decision id) after the new retest date is set.
    Sales issue, production issue, transfer and supplier return are refused.
  - **Availability:** `GetAvailabilityHandler` excludes retest-due lots like held lots (`:84-93`).
  - **Retest sweeper:** at day rollover (hosted, like `LotExpirySweeperHostedService.cs:13,37-44`) it applies
    `LotHeldStock` to SKU positions of newly due lots, so **reservations are refused** (`CreateReservationHandler.cs:13-19`
    guards `AvailableOnHand`). When the retest date is extended, the hold is removed again. Existing reservations are not
    released (CT-Q6).
  - These are **semantic changes of frozen contracts**, like CT-D1: one new error code value and one description note
    (proposal §9). Owner decision CT-Q3.
- Retest queue: lots with `RetestDate < today + 30` and AVAILABLE stock (mockup "Tarihi geçmiş ya da 30 gün içinde
  gelecek lotlar"). States: "Yaklaşıyor", "Tarihi geçti · kullanıma kapalı", "Karantinada".
- "Karantinaya Al" quarantines every AVAILABLE bucket of the lot, one hold per bucket. A release decision on a Retest hold
  requires `NewRetestDate` (> today, ≤ expiry, `LotDateRules.cs:14-25`).

**KQ-I — lot release gate (scope and order).**
- Scope: MOD-0174 manual transitions **Draft → Released** and **Blocked → Released** (`LotStatusRules.cs:20-23`).
  **Released → Blocked stays immediate** (safety direction). Expired is system-only.
- **Q4 changes `ChangeLotStatusHandler`:**
  1. When the gate answers `PendingApproval`, the handler commits no lot change and returns **202 `PendingApproval {lotId,
     pendingRequestId, contractVersion}`**. This is already in TRACE 1.2.0 (`trace.openapi.yaml:153,168,432-437`), so it
     is not a contract change. Today the handler throws (`ChangeLotStatusHandler.cs:84-88`).
  2. The gate implementation creates a `QuarantineDecision{Kind: LotRelease}` Draft inside the lot transaction and returns
     its id as `pendingRequestId`.
  3. The Lots UI (MOD-0174 page) follows 202 to `/Quarantine/Decisions/{id}/Submit`, where the requester signs (policy
     `quarantine.lot.release`) and submits. Submitting starts the workflow, as for any decision.
  4. On approval, `ApplyApprovedLotStatusChange` (internal, bypasses the gate, needs the approved decision id) sets the
     status and writes a `LotChangeEntry` (source "MOD-0175 approval {QKR}").
- Policy switch per company: `LotReleaseRequiresApproval` (default **true** for GxP; CT-Q10). When false the gate answers
  `Allow`, which is today's behaviour.
- Order: after Q2 and Q3 (it reuses their flow). Release of a quarantine hold whose lot is Draft includes the lot release
  in the same approval (CT-Q5), so a QA user does not need two approvals for one batch.

**Block reasons.** These are a local tenant list (`BlockReason`, CT-Q8):
- MOD-0048 has no inventory reason set and excludes tenant ERP lookups (`MOD-0048-lookups-reference-data.md:76-78`;
  `.antigravity/rules/platform-lookups-reference-data.md:47-52`).
- The MOD-0173 precedent hid its reason field instead of hard-coding one (BG-03).
- 0175 needs a *required* reason (mockup), and this pack is the module pack that owns it.
- Migration path: MOD-0048-FU01 business reference-data set later (FU-0175-BRD).

**Idempotency.**
- Every write takes `Idempotency-Key`.
- Decision drafts are unique per (TenantId, DeciderUserId, key).
- Submit is idempotent per decision (state check).
- Apply uses the ledger keys `quarantine:{decisionId}:{lineNo}`; a replay returns the posted transaction
  (`PostMovementHandler.cs:62-66`).

**Concurrency.** Holds and blocked records use `Version` / If-Match. Only **one active decision per hold / blocked
record** is allowed (409 `QUARANTINE_DECISION_ALREADY_PENDING`).

**Audit.**
- Every 0175 command writes an InventoryService audit row (same pattern as the ledger audit,
  `InventoryLedgerRepository.cs:211`) plus a `QuarantineHistoryEntry`.
- Decision events carry who, when and which signature.

## 9. Layout & Shell Contract

- All pages use `_LayoutTenantShell`. Users log in at `/account/login`. Every route is under `/Quarantine/**`; **none
  starts with `/Platform`**, so scope is Tenant by `ScopeFromRoute`.
- The company selector (W-0173-02, `MOD-0173-FU01-inventory-ledger-ui.md:644-660`) sits in the header partial. The mockup
  shows "Şirket: Grand Medical Sağlık A.Ş.".
- UAS-001: one explanation, no skeleton, no redirect.
- 7 languages, RTL for `ar`.
- Breadcrumb "Kalite / {page}" (mockup).
- Nav module "Kalite" → items Karantina Kuyruğu, Bloke Stok, Yeniden Test Kuyruğu, Karantina Ayarları.

## 10. Backend File Convention

```text
services/Diten.InventoryService/src/
  Diten.InventoryService.Domain/Quarantine/{Entities,Enums,Rules,Repositories}/…        (QuarantineDecisionRules: KQ-A sum, CoA, reason, block reason, lot-held check)
  Diten.InventoryService.Application/Features/Quarantine/{Commands,Queries,Handlers,Validators,Models,Services,Workflow,ESign,Ports}/…
  Diten.InventoryService.Application/Features/Inventory/Ports/IPostedMovementObserver.cs   (MOD-0173 seam, Q1)
  Diten.InventoryService.Application/Features/Inventory/Services/InternalMovementPostingService.cs (extracted from PostMovementHandler, Q1)
  Diten.InventoryService.Persistence/Quarantine/{Repositories,QuarantineCollections,QuarantineIndexConfiguration,QuarantineIntakeSweeperHostedService,RetestHoldSweeperHostedService}.cs
  Diten.InventoryService.Infrastructure/{Workflow,ESign,Eventing}/…
  Diten.InventoryService.Infrastructure/Authorization/QuarantinePermissions.cs
  Diten.InventoryService.Api/Controllers/Quarantine/{QuarantineController,QuarantineESignSubjectsController}.cs
  Diten.InventoryService.Api/ModuleRegistration/QuarantineBlockedStockManifestProvider.cs
```

Rules:
- Same as MOD-0173/0174: MediatR + validators, `Response<T>`, pipeline behaviours, error codes in `InventoryErrorCodes`
  plus a new `QuarantineErrorCodes`.
- Tenant from the token; LE from the scope middleware.
- Architecture tests pin the insert-only repositories and "0175 posts only through the internal posting service, never
  writes ledger collections" (a source scan like `SingleSystemOfRecord`).

## 11. Frontend File Contract

### 11.1 Mockup screen → route (all Scope = Tenant, all `_LayoutTenantShell`)

| Mockup screen | PageCode | Route | Kind | Page permission | Nav |
|---|---|---|---|---|---|
| Karantina Kuyruğu | `QUARANTINEQUEUE` | `/Quarantine` | list (slim DataTable) | `quarantine.holds.read` | yes |
| Karantina Kaydı Detayı | `QUARANTINEDETAILS` | `/Quarantine/Details/{id}` | single record | `quarantine.holds.read` | no (parent QUEUE) |
| Karar Ver (karar → e-imza) | `QUARANTINEDECIDE` | `/Quarantine/Decide/{holdId}` | compact form + summary + MOD-0022 dialog | `quarantine.decisions.create` | no (parent QUEUE) |
| Bloke Stok | `BLOCKEDSTOCK` | `/Quarantine/Blocked` | list (slim DataTable) | `quarantine.holds.read` | yes |
| Disposition | `BLOCKEDDISPOSITION` | `/Quarantine/Blocked/Disposition/{blockedId}` | compact form + summary + dialog | `quarantine.blocked.dispose` | no (parent BLOCKED) |
| Yeniden Test Kuyruğu | `RETESTQUEUE` | `/Quarantine/Retest` | list (slim DataTable) | `quarantine.holds.read` | yes |
| (submit page for KQ-I / resubmit) | `DECISIONSUBMIT` | `/Quarantine/Decisions/{id}/Submit` | summary + dialog | `quarantine.decisions.create` | no |
| (settings — not in mockup) | `QUARANTINESETTINGS` | `/Quarantine/Settings` | form + block reasons + template | `quarantine.settings.manage` | yes |
| Lot Detayı (mockup placeholder) | — | MOD-0174 `/Lots/Details?id=` | link only | `trace.lots.read` (0174's) | — |

No route starts with `/Platform`. `QuarantineManifestRouteTests` asserts `ScopeFromRoute(route) == "Tenant"` for every page.

### 11.2 Files

```text
frontend/Diten.Web/Controllers/QuarantineController.cs          [Authorize][Route("Quarantine")] + api proxy → gateway /api/quarantine/**
frontend/Diten.Web/Views/Quarantine/{Index,Details,Decide,Blocked,Disposition,Retest,Submit,Settings}.cshtml
frontend/Diten.Web/Views/Quarantine/_QuarantineL10n.cshtml, _DecisionSummary.cshtml, _Stepper.cshtml, _UnblockModal.cshtml
frontend/Diten.Web/wwwroot/assets/js/Quarantine/{queue,details,decide,blocked,disposition,retest,submit,settings,quarantine.l10n}.js
frontend/Diten.Web/Resources/Views/Quarantine/Quarantine.{en,tr,fr,es,zh,ar,ru}.resx
```

The e-signature step uses MOD-0022's `_ESignatureDialog` / `ESign.open(...)`. 0175 builds no password field (KQ-G).

### 11.3 Screens (mockup, 1:1 except §21.3)

- **Karantina Kuyruğu:**
  - KPI cards: Karar bekleyen satır (+ "{n} satır incelemede"); Toplam miktar · birim bazında; Gecikme hedefini aşan
    (+ "SKT'si 90 gün içinde dolacak: n").
  - Filters: search (ürün, SKU, lot, KRN), Tüm depolar, Tüm stok durumları (Karantina / Kalite Kontrol), Tüm kaynaklar
    (Mal kabul / Üretim / Elle / İade / Yeniden test / Yeniden işleme), "Yalnız gecikenler", Temizle.
  - Columns: Ürün (name, SKU · KRN) · Lot No (link to Lot Detayı) · SKT (+ "n gün kaldı" ≤ 90) · Depo / Lokasyon ·
    Stok durumu · Miktar · Giriş tarihi · Bekleme (n gün, "Hedef {t} gün · şirket") · Kaynak / Belge · Durum (Bekliyor /
    İncelemede / **Onay bekliyor** / Karar Verildi).
  - Row actions: Karar Ver, Detay.
  - Late rows are highlighted. Sort: open first, then most overdue. Footer: "{n} kayıt gösteriliyor"; legends "Bekleme
    hedefi aşıldı", "SKT ≤ 90 gün".
- **Karantina Kaydı Detayı:**
  - Header (name, stock status, review state, SKU · KRN); "Kuyruğa dön", "Lot'a Git", "Karar Ver"; late alert.
  - Facts: Lot, SKT, Miktar, Depo/Lokasyon, Bekleme süresi, Kaynak, Kaydı oluşturan.
  - "Kalite bilgileri": CoA referansı (Lot'tan), CoA tarihi, Analiz sonucu notu (editable while open,
    `quarantine.holds.review`), Ek belgeler (references only).
  - "Karar geçmişi" timeline with signature chips (decider and approver signature ids → MOD-0022 Doğrulama link).
  - "Stok hareketleri" table: Hareket no · Tarih · Hareket tipi · Durum değişikliği (from → to chips) · Miktar · Kullanıcı.
  - New: "İş akışı" box (instance status, approver, "Durumu yenile", "Kararı geri çek", "Yeniden uygula" on ApplyFailed).
  - Action "İncelemeye al".
- **Karar Ver (2 steps):**
  - Step 1 "Karar":
    - Four-eyes note (settings).
    - "Kısmi karar" switch. Single mode: Serbest Bırak → Kullanılabilir / Reddet → Bloke / Bloke Et → Bloke cards with
      the target status chip. Partial mode: 2 lines (decision select + quantity, auto-fill of line 2), "Toplam / mevcut
      miktar" sum.
    - Bloke nedeni (when Reject/Block), Gerekçe (counter "n / en az 10"), "CoA / analiz sonucu kontrol edildi" (when
      Release; shows the lot's CoA ref).
    - New: "Yeni yeniden test tarihi" for Retest holds.
  - Step 2 "Elektronik imza": the "İmzalanacak karar" summary (lines → target status, reason) + MOD-0022 dialog. If the
    policy says not required: "politika kapalı · bu adım atlanır".
  - Buttons: Geri / Vazgeç / "İleri: Elektronik imza" | "İmzala ve gönder" (mockup "İmzala ve kaydet" → now *sends for
    approval*, §21.3).
- **Bloke Stok:**
  - Filters: search, "Tüm bloke nedenleri", "Yalnız disposition bekleyenler".
  - Columns: Ürün (name, SKU · origin) · Lot · Depo / Lokasyon · Miktar · Bloke nedeni (chip + note) · Bloke tarihi ·
    Karar bekliyor mu ("Evet · disposition bekliyor" / "Hayır · inceleme tamamlandı" / "Hayır · inceleme sürüyor").
  - Row actions: Disposition Kararı; Blokeyi Kaldır (disabled with tooltip "İnceleme tamamlanmadan bloke kaldırılamaz");
    New: "İncelemeyi tamamla".
  - Toolbar New: "Stoğu bloke et".
  - Info: "Blokeyi Kaldır yalnız incelemesi tamamlanmış kayıtlarda açılır."
  - Virtual "Bloke lot" rows: Destroy only.
- **Disposition (2 steps):**
  - Kinds: İmha (Hurda çıkışı) / Tedarikçiye İade (İade çıkışı) / Yeniden İşleme (Bloke → Karantina) / Numune / Referans
    olarak ayır (Bloke → numune lokasyonu, **status stays Bloke**, KQ-E).
  - Quantity ("En fazla … · kalan miktar Bloke'de kalır"); Tedarikçi + İade belge no (return); İmha yöntemi + İmha
    tarihi (destroy); Gerekçe.
  - Box "Onaylanınca Stok Defteri'nde oluşacak hareket" (movement type + quantity + location).
  - Step 2 e-signature as above.
- **Blokeyi Kaldır (modal):**
  - Text "Stok Bloke → Kullanılabilir durumuna geçer…". Gerekçe ≥ 10 + MOD-0022 dialog (policy
    `quarantine.blocked.unblock`).
  - Submits an Unblock decision (**workflow**, KQ-F) — the stock does not move until approval (§21.3).
- **Yeniden Test Kuyruğu:**
  - Info line (KQ-H wording: "Yeniden test tarihi geçmiş lot, kalite kararı verilene kadar satış, rezervasyon ve çıkışta
    kullanılamaz.").
  - Columns: Ürün · Lot · Yeniden test tarihi (+ "n gün geçti / kaldı") · Eldeki stok · Durum (Yaklaşıyor /
    Tarihi geçti · kullanıma kapalı / Karantinada) · action "Karantinaya Al" or link "{KRN} →".
- **Karantina Ayarları** (new, not in mockup):
  - Bekleme hedefi (gün); Dört göz; Onaylayıcı adaylar (position picker — `GET /api/v1/workflow/lookups/positions`);
    Numune lokasyonları per warehouse; Lot serbest bırakma onayı.
  - Bloke nedenleri (code + 7 names, active).
  - "Onay şablonunu hazırla" (shows template code, published version; Admin only).

### 11.4 Lot pages (MOD-0174, Q4 change)

- `CHANGELOTSTATUS` (Draft/Blocked → Released) handles 202: toast "Onaya gönderildi" plus a link to
  `/Quarantine/Decisions/{pendingRequestId}/Submit`.
- Lot detail shows an "Onay bekliyor" badge (read from QUARANTINE-BUNDLE `GET /lots/{lotId}/quality-status`).

### 11.5 Not built (mockup demo only)

- "Mockup kararları" panel;
- the active-user switcher;
- the Tweaks props;
- the placeholder Lot page (it links to the real MOD-0174 page).

### 11.6 Resx keys (all 7 languages)

`Quarantine.Queue.*`, `Quarantine.Details.*`, `Quarantine.Decide.*` (decisions, partial, sums, CoA, errors), `Quarantine.Blocked.*`,
`Quarantine.Disposition.*` (4 kinds + movement texts), `Quarantine.Unblock.*`, `Quarantine.Retest.*`, `Quarantine.Settings.*`,
`Quarantine.Workflow.*` (Onay bekliyor, Onaylandı · uygulanmayı bekliyor, Reddedildi, Geri çekildi, Zaman aşımı, Uygulanamadı,
Dört göz ihlali), `Quarantine.Source.*`, `Quarantine.Status.*` (stock statuses reuse INVENTORY labels), `Quarantine.Errors.{CODE}`
(every error code of the contract), `Nav.Module.QUARANTINE-BLOCKED-STOCK`, `Nav.Page.{PageCode}`. Block-reason names are data
(7 languages per row).

## 12. Validation Rules

| # | Field / rule | Rule | Server error |
|---|---|---|---|
| V1 | decision lines (single) | exactly 1 line, decision ∈ Release/Reject/Block, quantity = hold quantity | 400 `QUARANTINE_DECISION_INVALID` |
| V2 | decision lines (partial, KQ-A) | 2 lines, different decisions, each > 0, **sum = hold quantity** | 400 `QUARANTINE_PARTIAL_SUM_MISMATCH` |
| V3 | reason | trimmed 10–1000 | 400 `REASON_REQUIRED` (same code as TRACE, `trace.openapi.yaml:155`) |
| V4 | CoA | any Release line ⇒ `coaChecked = true` | 400 `QUARANTINE_COA_NOT_CONFIRMED` |
| V5 | block reason | any Reject/Block line ⇒ active `BlockReason` code | 400 `QUARANTINE_BLOCK_REASON_REQUIRED` |
| V6 | hold state | Open, no active decision | 409 `QUARANTINE_DECISION_ALREADY_PENDING` / `QUARANTINE_HOLD_CLOSED` |
| V7 | bucket | current balance of the bucket ≥ decision quantity (re-checked at apply by the ledger) | 409 `QUARANTINE_STOCK_CHANGED` |
| V8 | lot held | lot status Blocked/Expired ⇒ only Destroy allowed (CT-D4) | 409 `QUARANTINE_LOT_HELD` |
| V9 | retest release | Retest hold + Release ⇒ `newRetestDate` > today and ≤ expiry | 400 `QUARANTINE_RETEST_DATE_REQUIRED` / `LOT_DATES_INVALID` |
| V10 | disposition quantity | 0 < q ≤ blocked quantity | 400 `QUARANTINE_QUANTITY_INVALID` |
| V11 | return to supplier | supplier + return document no required | 400 `QUARANTINE_SUPPLIER_REQUIRED` |
| V12 | destroy | method required; date ≥ today − 30 | 400 `QUARANTINE_DESTRUCTION_METHOD_REQUIRED` |
| V13 | sample (KQ-E) | the warehouse has a configured sample location, Active + stockable, same warehouse | 409 `QUARANTINE_SAMPLE_LOCATION_NOT_SET` |
| V14 | unblock | blocked record `Reviewed = true` (mockup) | 409 `QUARANTINE_REVIEW_NOT_COMPLETED` |
| V15 | signature (KQ-B) | policy requires ⇒ `signatureId` present, signer = caller, subject = this decision, meaning = policy meaning, valid | 400 `QUARANTINE_SIGNATURE_REQUIRED` / 409 `QUARANTINE_SIGNATURE_MISMATCH` |
| V16 | settings | approver candidates present before submit | 409 `QUARANTINE_APPROVERS_NOT_CONFIGURED` |
| V17 | four eyes (KQ-C, apply time) | approver ≠ decider when `FourEyes` | event `FourEyesViolation` (no movement) |
| V18 | quarantine / block from AVAILABLE | no reservation would be uncovered | 409 `QUARANTINE_STOCK_RESERVED` (+ reservations) |
| V19 | wait target | 1–60 | 400 |
| V20 | block reason code | `^[A-Z][A-Z0-9_]{1,31}$`, 7 names, immutable after use | 400 / 409 `QUARANTINE_BLOCK_REASON_IN_USE` |
| V21 | withdraw | only the decider, only PendingApproval | 403 / 409 `QUARANTINE_DECISION_NOT_PENDING` |
| V22 | idempotency | same key + different body | 400 `IDEMPOTENCY_KEY_REUSED` |
| V23 | company | record LE = context LE | 403 `CROSS_LE_FORBIDDEN` |
| V24 | KQ-H posting (ledger, Q4) | retest-due lot outside the exceptions | 422 `LOT_RETEST_DUE` (INVENTORY additive) |

UI-only (mockup): line 2 auto-fills to the remainder until touched; the sum turns green when equal; four-eyes note; character
counters.

## 13. Failure Path to Verify

1. Partial decision 700 + 400 on a 1 200 hold → 400 `QUARANTINE_PARTIAL_SUM_MISMATCH`.
2. Release without CoA confirmation → 400 `QUARANTINE_COA_NOT_CONFIRMED`.
3. Reject without block reason → 400 `QUARANTINE_BLOCK_REASON_REQUIRED`.
4. Reason of 9 characters → 400 `REASON_REQUIRED`.
5. Second decision while one is pending → 409 `QUARANTINE_DECISION_ALREADY_PENDING`.
6. Submit when the policy requires a signature, without `signatureId` → 400 `QUARANTINE_SIGNATURE_REQUIRED`. Another user's
   signature → 409 `QUARANTINE_SIGNATURE_MISMATCH`.
7. Submit with no published template → 409 `QUARANTINE_WORKFLOW_TEMPLATE_MISSING`; hold stays Open, decision `StartFailed`.
8. **No stock moves before approval:** after submit, balance and transactions unchanged (test reads the ledger).
9. Workflow `rejected` / `cancelled` / `timed-out` → no movement; hold back to Open; history entry.
10. Workflow `approved` with the **event consumer disabled** → the next queue read reconciles and applies exactly once
    (second read: no second movement — idempotency keys).
11. Approver = decider (FourEyes on, SoD defect simulated) → `FourEyesViolation`, no movement.
12. Approved release, but the lot became Blocked before apply → `ApplyFailed` `LOT_BLOCKED`; "Yeniden uygula" after unblock
    posts once.
13. Disposition Sample without a configured sample location → 409 `QUARANTINE_SAMPLE_LOCATION_NOT_SET`.
14. Sample applied → `LOCATION_TRANSFER`; stock status still BLOCKED (balance read); no new status anywhere.
15. Unblock on a not-reviewed record → 409 `QUARANTINE_REVIEW_NOT_COMPLETED`.
16. Release on a lot-blocked lot → 409 `QUARANTINE_LOT_HELD` (no workflow started).
17. Karantinaya Al on AVAILABLE stock covered by reservations → 409 `QUARANTINE_STOCK_RESERVED`; nothing posted.
18. Retest-due lot: sales issue → 422 `LOT_RETEST_DUE`; reservation → refused (position); "Karantinaya Al" → posts.
19. Release of a Retest hold without a new retest date → 400 `QUARANTINE_RETEST_DATE_REQUIRED`.
20. Lot Draft → Released via TRACE with the gate on → **202** `PendingApproval`; lot status unchanged; after approval →
    Released + history entry "MOD-0175 approval".
21. Another company's hold → 403 `CROSS_LE_FORBIDDEN`; another tenant's → 404.
22. E-signature provider: a user without `quarantine.holds.read` → 404 (Platform signs nothing).
23. Same intake movement observed twice (observer + sweep) → one hold (unique `SourceMovementId`).
24. A user without the page key opens `/Quarantine` → UAS-001 surface.

## 14. Authorization Convention

**Module code `QUARANTINE-BLOCKED-STOCK`** (domain practice: `INVENTORY-LEDGER`, `LOT-SERIAL-TRACKING` —
`InventoryLedgerManifestProvider.cs:34`, `LotSerialTrackingManifestProvider.cs:26`; the standard's lowercase rule :16 is a
known domain deviation, CT-Q12). **Namespace `quarantine.*`** (PKS-001 §4, one owner). Tier-3 verbs registered here
(PKS-001 §5): `decide` (not used — decisions are `create`), `review`, `dispose`, `unblock`, `block`, `withdraw`, `manage`.

| Key | Tier | Gate |
|---|---|---|
| `quarantine.holds.read` | 1 | QUEUE, DETAILS, BLOCKED, RETEST pages; all GETs; e-sign subject provider (approver needs it too) |
| `quarantine.holds.create` | 1 | "Karantinaya Al" (manual / retest) |
| `quarantine.holds.review` | 3 | "İncelemeye al", analysis note, "İncelemeyi tamamla" (blocked) |
| `quarantine.decisions.create` | 1 | DECIDE page, decision draft + submit, DECISIONSUBMIT (KQ-I resubmit) |
| `quarantine.decisions.withdraw` | 3 | "Kararı geri çek" (own decision) |
| `quarantine.decisions.apply` | 3 | "Yeniden uygula" after ApplyFailed |
| `quarantine.blocked.dispose` | 3 | DISPOSITION page |
| `quarantine.blocked.unblock` | 3 | "Blokeyi Kaldır" |
| `quarantine.blocked.block` | 3 | "Stoğu bloke et" (manual block) |
| `quarantine.settings.manage` | 3 | SETTINGS page, block reasons, "Onay şablonunu hazırla" |

All keys are Tenant scope. **No ApiPermissions entry is needed**: every key has a page or a button. Consumer reads (0142 /
0176 / 0177) use `quarantine.holds.read`. The manifest declares **only `quarantine.*` keys** (CAT-01). Links to Lot pages
are not actions of this manifest.

**Manifest (`QuarantineBlockedStockManifestProvider`):**

| PageCode | RoutePath | RequiredPermission | Nav-visible | Parent |
|---|---|---|---|---|
| `QUARANTINEQUEUE` | `/Quarantine` | `quarantine.holds.read` | true | — |
| `QUARANTINEDETAILS` | `/Quarantine/Details` | `quarantine.holds.read` | false | `QUARANTINEQUEUE` |
| `QUARANTINEDECIDE` | `/Quarantine/Decide` | `quarantine.decisions.create` | false | `QUARANTINEQUEUE` |
| `DECISIONSUBMIT` | `/Quarantine/Decisions/Submit` | `quarantine.decisions.create` | false | `QUARANTINEQUEUE` |
| `BLOCKEDSTOCK` | `/Quarantine/Blocked` | `quarantine.holds.read` | true | — |
| `BLOCKEDDISPOSITION` | `/Quarantine/Blocked/Disposition` | `quarantine.blocked.dispose` | false | `BLOCKEDSTOCK` |
| `RETESTQUEUE` | `/Quarantine/Retest` | `quarantine.holds.read` | true | — |
| `QUARANTINESETTINGS` | `/Quarantine/Settings` | `quarantine.settings.manage` | true | — |

**Screen → button → action → permission (every mockup button):**

| Screen | Button | ActionCode | Placement | PermissionKey | Dangerous |
|---|---|---|---|---|---|
| Karantina Kuyruğu | Karar Ver | `DECIDE` | RowAction | `quarantine.decisions.create` | no |
| Karantina Kuyruğu | (row) Detay | `VIEWHOLD` | RowAction | `quarantine.holds.read` | no |
| Karantina Kuyruğu | Lot No link | — (link to 0174) | — | 0174 page key | no |
| Detay | Karar Ver | `DECIDE` | Toolbar | `quarantine.decisions.create` | no |
| Detay | Kuyruğa dön / Lot'a Git | — (nav) | — | — | no |
| Detay | İncelemeye al (new) | `STARTREVIEW` | Toolbar | `quarantine.holds.review` | no |
| Detay | Durumu yenile (new) | `REFRESHWORKFLOW` | Toolbar | `quarantine.holds.read` | no |
| Detay | Kararı geri çek (new) | `WITHDRAWDECISION` | Toolbar | `quarantine.decisions.withdraw` | yes |
| Detay | Yeniden uygula (new) | `REAPPLYDECISION` | Toolbar | `quarantine.decisions.apply` | no |
| Karar Ver | İleri: Elektronik imza / İmzala ve gönder | `SUBMITDECISION` | form | `quarantine.decisions.create` | no |
| Karar Ver | Geri / Vazgeç | — (client) | — | — | no |
| Bloke Stok | Disposition Kararı | `DISPOSE` | RowAction | `quarantine.blocked.dispose` | no |
| Bloke Stok | Blokeyi Kaldır | `UNBLOCK` | RowAction | `quarantine.blocked.unblock` | yes |
| Bloke Stok | İncelemeyi tamamla (new) | `COMPLETEREVIEW` | RowAction | `quarantine.holds.review` | no |
| Bloke Stok | Stoğu bloke et (new) | `BLOCKSTOCK` | Toolbar | `quarantine.blocked.block` | yes |
| Disposition | İleri / İmzala ve gönder | `SUBMITDISPOSITION` | form | `quarantine.blocked.dispose` | yes (Destroy) |
| Yeniden Test | Karantinaya Al | `QUARANTINELOT` | RowAction | `quarantine.holds.create` | no |
| Yeniden Test | {KRN} → | `VIEWHOLD` | RowAction | `quarantine.holds.read` | no |
| Ayarlar | Kaydet | `SAVESETTINGS` | Toolbar | `quarantine.settings.manage` | no |
| Ayarlar | Onay şablonunu hazırla | `PREPARETEMPLATE` | Toolbar | `quarantine.settings.manage` (+ caller's Platform workflow keys) | no |

**Outside keys a quarantine user also needs (not declared here):**
- `platform.workflow.instances.start` (decider) and `platform.workflow.tasks.approve` (approver), both PlatformAdmin
  scope → FU-0175-WF-KEYS / CT-Q2.
- `platform.workflow.instances.view` (reconcile; best effort).
- `esign.signatures.sign` (default grant, MOD-0022 CT-D10).
- `trace.lots.change-status` (KQ-I requester, 0174's key).

## 15. Gateway / API Routing Decision

**Change REQUIRED, owned by a separate integration-agent WP (INT-QUARANTINE).**

| Operation | Route | Source |
|---|---|---|
| QUARANTINE-BUNDLE | `/api/quarantine` + `/api/quarantine/{everything}` → 5066 | new; same shape as `/api/trace` (`ocelot.json:877-903`) |
| e-sign subject provider | `/api/quarantine/esign-subjects/{type}/{id}` | covered by the route above; Platform config `ESign:SubjectProviders:quarantine:BaseUrl` |
| Workflow (outbound) | `/api/v1/workflow/{everything}` → 5057 | existing (`ocelot.json:1696-1712`) |
| e-signature (outbound) | `/api/v1/esign/{everything}` → 5057 | MOD-0022 INT-ESIGN |
| esign action registration (outbound S2S) | Platform `api/internal/esign/actions/register` direct, `X-Internal-Api-Key` | as module registration (`ModuleRegistrationHostedService.cs:83-89`) |

## 16. Acceptance Criteria

**Q1 — records, decisions, apply engine**
- [ ] Intake: a manual inbound into QUARANTINE / QI creates exactly one hold (observer), and the sweep never duplicates it.
- [ ] Decision drafts validate V1–V14. Decisions are insert-only after submit (architecture test). **Sabotage:** add an
      update method to the decision repository → red.
- [ ] Apply engine (driven by a test approval port): each row of the stock-effect table posts exactly the listed movement,
      with the listed from/to status and location. Balances are checked through the ledger read API. A replay posts
      nothing.
- [ ] **No new status / movement:** a source scan finds no StockStatus or MovementType value outside the INVENTORY enum
      (`StockStatus.cs`, MovementType) in 0175 code. **Sabotage:** add `SAMPLE` to a 0175 enum mapping → red.
- [ ] Karantinaya Al / manual block refused with `QUARANTINE_STOCK_RESERVED` when reservations would be uncovered.
- [ ] Manifest: only `quarantine.*` keys (namespace test like `ManifestNamespaceOwnershipTests.cs:12`); routes Tenant.
      **Sabotage:** route `/Platform/Quarantine` → red; add `inventory.stock.read` → red.

**Q2 — workflow**
- [ ] Submit starts exactly one instance (idempotency key replay → same instance); DisplayContext link opens the hold.
- [ ] **No movement before approval** (ledger unchanged). **Sabotage:** apply on submit → test red.
- [ ] Outcome via event (consumer on) **and** via read-time reconciliation (consumer off) — both apply once.
- [ ] rejected / cancelled / timed-out → no movement, hold reopened.
- [ ] Four-eyes defence: approver = decider → `FourEyesViolation`, no movement. **Sabotage:** remove the check → red.
- [ ] "Onay şablonunu hazırla" creates + publishes `INV-QUARANTINE-RELEASE` once (second run reports existing version).
- [ ] Template-missing / forbidden / unavailable mapped as §8.

**Q3 — e-signature**
- [ ] Provider returns the canonical field list `qd-v1` (golden test: same decision → same hash on every call; a changed
      field → different hash).
- [ ] Policy-required signature enforced on submit (V15). Policy off → submit without signature accepted.
- [ ] Template step carries `signatureRequired: true`. With MOD-0022 E4 live, WorkCenter approve without a signature →
      409 `ESIGN_SIGNATURE_REQUIRED`.
- [ ] 0175 stores only signature ids (no signature data; source scan).

**Q4 — retest + lot gate**
- [ ] `LOT_RETEST_DUE` refuses sales / production issue / transfer / supplier return of a retest-due lot. Exceptions post.
      Availability and reservation exclude it.
- [ ] Lot Draft/Blocked → Released with the gate on → 202 + decision. After approval the lot is Released, with history.
      Gate off → 200 as today.
- [ ] **Sabotage:** make the gate return Allow → the 202 test is red; drop the `RetestDue` rule → the posting test is red.

**Q5 — UI**
- [ ] 6 mockup screens + Submit + Settings as §11, with the two-step decide/disposition and the MOD-0022 dialog. UAS-001.
      DataTable v2 verifier green for the 3 lists.

**INT / L10n**
- [ ] Gateway route + coverage test.
- [ ] 7-language resx parity. **Sabotage:** delete one `zh` key → red.

## 17. Test Expectations

- DCP-002 verifier exit 0, or waiver W-0175-01 (environment-blocked, never a fabricated PASS).
- Unit:
  - `QuarantineDecisionRules` (KQ-A matrix, CoA, block reason, lot-held, retest date);
  - stock-effect mapping (table-driven, one case per row of §8);
  - canonical subject (golden);
  - DisplayContext builder (limits);
  - idempotency key format (≤ 128).
- Handler / API: failure paths 1–24 (§13).
- Integration (InventoryService test factory, `TestInfrastructure/InventoryApiFactory`):
  - apply engine against the real ledger repository;
  - observer + sweep;
  - retest posting rule;
  - lot gate 202.
- Workflow client: stubbed Gateway (WireMock-style) for start / by-objects / history / cancel / definitions. Error mapping.
- **No-bus test:** consumer disabled, reconciliation applies.
- Architecture:
  - insert-only repositories;
  - 0175 never references ledger collections (source scan, like `SingleSystemOfRecord`);
  - manifest namespace ownership;
  - route scope;
  - DocsPathGuard (`tests/architecture/TenantArchitecture.ArchitectureTests/DocsPathGuardTests.cs`).
- Contract: `quarantine.openapi.yaml` schema validation (`Contract/ContractSchemaValidator`) + prism mock smoke.
- Runtime G1 (owner, live): quarantine → decide (sign) → second user approves in WorkCenter (sign) → reconcile → stock
  AVAILABLE. Needs MOD-0022 E1/E2/E4/E5 and FU-0175-WF-SOD.
- Sabotage proofs as §16.

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name present in the registry (`module-id-registry.md:274`, "DCP-002 gate OK 2026-09-11").
- [x] DCP-002 verifier re-run exit 0 — **waived W-0175-01** (CT 2026-10-08; run before the MVP-1 PR):
  - owner: Ali;
  - reason: no Python on the MVP-1 machine;
  - compensating control: registry row + DCP-009;
  - expiry: before the MVP-1 push/PR.
- [x] Owner decisions KQ-A…KQ-I recorded (§19).
- [x] QUARANTINE-BUNDLE v1 proposal accepted by CT 2026-10-08 (frozen in Q1).
- [x] **CT-Q1** (taken 2026-10-07, §19) workflow template content (one template, approver step meaning).
- [x] **CT-Q2** (taken 2026-10-07, §19) who can decide/approve until FU-0175-WF-KEYS (MVP-1 interim).
- [x] **CT-Q3** (taken 2026-10-07, §19) KQ-H frozen-contract semantics (`LOT_RETEST_DUE` + availability note) — owner decision like CT-D1.
- [x] **CT-Q4** (taken 2026-10-07, §19) approver step meaning.
- [x] **CT-Q5** (taken 2026-10-07, §19) hold release on a Draft lot also releases the lot.
- [x] **CT-Q6** (taken 2026-10-07, §19) reserved stock on quarantine / block / retest.
- [x] **CT-Q7** (taken 2026-10-07, §19) supplier return of expired / lot-blocked stock.
- [x] **CT-Q8** (taken 2026-10-07, §19) block-reason list local vs MOD-0048.
- [x] **CT-Q9** (taken 2026-10-07, §19) partial apply failure (continue vs all-or-nothing).
- [x] **CT-Q10** (taken 2026-10-07, §19) lot-release gate default per company.
- [x] **CT-Q11** (taken 2026-10-07, §19) no background reconciler (read-time only) accepted.
- [x] **CT-Q12** (taken 2026-10-07, §19) module code case.
- [x] **CT-Q13** (taken 2026-10-07, §19) KQ-C four-eyes switch while MOD-0023 SoD is fixed.
- [x] **CT-Q14** (taken 2026-10-07, §19) product-level wait target (mockup) vs company-only (KQ-D).
- [x] FU-0175-WF-SOD (MOD-0023) CLOSED by WP-0023-FX (CT-ACCEPTED cd43a4533) — was: scheduled before the Q2 runtime proof — **hard prerequisite for KQ-C**.
- [ ] ≥ 4 failure paths (24 in §13). ✔
- [x] **Owner approval → `ready-for-dev`** (Ali, 2026-10-08).

## 19. Implementation Notes

### Owner decisions (2026-10-07) — DECISIONS

| ID | Decision | Where in this pack |
|---|---|---|
| **KQ-A** | Partial decision exists: part released, the rest rejected/blocked; line total = available quantity. | §4 `Lines`; §8 stock table "Kısmi karar"; V2; §13 #1 |
| **KQ-B** | e-signature by policy (MOD-0022 policy, per meaning). Mockup two steps: decision → e-signature. | §8 e-signature (signable actions, decider); V15; §11.3 step 2; §13 #6 |
| **KQ-C** | Four eyes = Workflow SoD ("submitter cannot approve"); switched on/off by policy (mockup `fourEyes`). | §4 settings `FourEyes`; §8 measured limit 1 + 0175 defence; V17; §13 #11; CT-Q13 |
| **KQ-D** | Wait target = company setting, default 10 days. | §4 `WaitTargetDays`; §11.3 queue; CT-Q14 |
| **KQ-E** | "Numune / Referans" is **not a new stock status**: stock stays BLOCKED and moves to the sample location (LOCATION_TRANSFER); decision kept as "Numune"; INVENTORY-BUNDLE unchanged. | §8 stock table; V13; §13 #14; §16 Q1 sabotage |
| **KQ-F** | Workflow mandatory (DEC-INV-08, no deviation): release / reject / disposition start a Workflow instance (template `INV-QUARANTINE-RELEASE`, seeded by 0175, published by the tenant Admin — MOD-0022 CT-D6); approval is a WorkCenter task (DisplayContext); after approval 0175 applies the stock movement. The approval step is "signature required" (MOD-0022 E4). | §8 KQ-F + workflow table + approval → apply; §16 Q2 |
| **KQ-G** | e-signature through MOD-0022 (ESIGN-BUNDLE); 0175 keeps no signature record; canonical fields listed (CT-D2). | §8 canonical field list `qd-v1`; §16 Q3 |
| **KQ-H** | Retest-due lot: **hard block** in 0175 (hard form of MOD-0174 K-F) — sales / reservation / issue refused until decided; "Karantinaya Al" moves the stock to Quarantine. | §8 KQ-H; V24; §13 #18–19; CT-Q3 |
| **KQ-I** | Lot release (MOD-0174 K-E): Draft→Released and Blocked→Released go through the same Workflow + e-signature path via `ILotStatusChangeGate` (202 PendingApproval). | §8 KQ-I (scope + order); §11.4; §13 #20; CT-Q5, CT-Q10 |

### KQ trace table

| KQ | Sections |
|---|---|
| KQ-A | §4 · §8 · V1–V2 · §11.3 Karar Ver · §13 #1 · §16 Q1 |
| KQ-B | §8 e-signature · V15 · §11.3 step 2 · §13 #6 · §16 Q3 |
| KQ-C | §4 · §8 measured limits · V17 · §13 #11 · §16 Q2 · CT-Q13 · FU-0175-WF-SOD |
| KQ-D | §4 · §11.3 · CT-Q14 |
| KQ-E | §8 · V13 · §11.3 Disposition · §13 #14 · §16 Q1 |
| KQ-F | §1 · §2 · §8 · §13 #7–10 · §16 Q2 |
| KQ-G | §2 · §8 · §16 Q3 |
| KQ-H | §8 · V9, V24 · §11.3 Retest · §13 #18–19 · §16 Q4 · CT-Q3 |
| KQ-I | §8 · §11.4 · §13 #20 · §16 Q4 · CT-Q5 · CT-Q10 |

### Mockup rules → pack

| Mockup rule (`kar.app.js`) | Pack |
|---|---|
| reason ≥ 10 (`:233,273,307`) | V3 |
| CoA check required when releasing (`:234`) | V4 |
| block reason required for Reject / Block (`:235`) | V5 |
| partial: two lines, different decisions, > 0, sum = quantity (`:226-231`) | V2 |
| four eyes: creator cannot release (`:218,232`) | replaced by KQ-C (decider ≠ approver); §21.3 #2 |
| unblock only when reviewed (`:171-172`) | V14 + "İncelemeyi tamamla" |
| disposition quantity ≤ blocked, rest stays blocked (`:272`) | V10 |
| return: supplier + document no (`:274-275`); destroy: method (`:276`) | V11–V12 |
| wait target: product, else company (`:138`) | KQ-D company only → CT-Q14 |
| retest: past = closed for use; "Karantinaya Al" (`:176-191`) | KQ-H |
| e-sign step skipped when policy off (`:211,260`) | KQ-B |

### Measured facts

| # | Fact | Evidence |
|---|---|---|
| F1 | QUARANTINE-BUNDLE contract file does not exist; report says ✓ | `docs/analysis/contracts/README.md:7-42`; report `:229` |
| F2 | DEC-INV-08 text | report `:307`; `:48` |
| F3 | 0175 objects | report `:182-183`, `:516` |
| F4 | MovementType / StockStatus enums | `inventory-bundle.openapi.yaml:252,255` |
| F5 | MovementRequest has sourceModule/Type/DocumentId/LineId, documentGroupId, movementReason | `inventory-bundle.openapi.yaml:256-283` |
| F6 | STATUS_TRANSFER: same wh/loc, statuses differ, no allow-list | `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Rules/MovementDirectionRules.cs:96-107` |
| F7 | LOCATION_TRANSFER keeps status | `MovementDirectionRules.cs:82-94` |
| F8 | Outbound (SCRAP, SUPPLIER_RETURN) any fromStockStatus | `MovementDirectionRules.cs:63-68` |
| F9 | Lot Blocked/Expired: only SCRAP/WRITE_OFF/REVERSAL | `…/Domain/Traceability/Rules/LotPostingRules.cs:35-36,45-63`; MOD-0174 CT-D4 `:748` |
| F10 | Availability = AVAILABLE only; QUARANTINE/BLOCKED/DAMAGED/EXPIRED in "Blocked" bucket; held lots excluded | `…/Domain/Rules/BalanceBuckets.cs:13-25`; `GetAvailabilityHandler.cs:84-93` |
| F11 | Reservations only on AVAILABLE; moving out of AVAILABLE refuses if reservations uncovered | `CreateReservationHandler.cs:13-19`; `ReservationPositionOperations.cs:84-93` |
| F12 | No location purpose / sample type | `location.openapi.yaml:205`; `Domain/Enums/LocationType.cs:4-11` |
| F13 | `ILotStatusChangeGate` (Allow/PendingApproval); default AlwaysAllow; PendingApproval throws today | `…/Traceability/Ports/ILotStatusChangeGate.cs:12-21`; `AlwaysAllowLotStatusChangeGate.cs:7-11`; `ChangeLotStatusHandler.cs:84-88` |
| F14 | TRACE 202 PendingApproval reserved | `trace.openapi.yaml:153,168,432-437` |
| F15 | Lot fields RetestDate / coaReference; no retest sweeper; K-F UI-only | `…/Traceability/Entities/Lot.cs:35`; `trace.openapi.yaml:298`; MOD-0174 pack `:700,787-788` |
| F16 | No MassTransit / Hangfire in InventoryService; outbox not transported | `Program.cs`; `InventoryOutboxMessage.cs:4-6` |
| F17 | Caller-token forwarding exists | `Upstream/ForwardCallerHeadersHandler.cs:19-38` |
| F18 | One key `inventory.movements.create` covers every movement type | `InventoryController.cs:27-28` |
| F19 | STATUS_TRANSFER is 0175's (not a 0173 manual action) | `MOD-0173-inventory-ledger-valuation.md:519-520`; FU01 `:71,788` |
| F20 | Workflow start / by-objects / history endpoints and limits | `WorkflowDefinitionsController.cs:84-128`; `GetWorkflowInstancesByObjectsQuery.cs:15` |
| F21 | Completion event outbox; DeadLettered after retries, never re-sent; RabbitMQ down here | `WorkflowTerminalTransitionWriter.cs:80-153`; `OutboxEvent.cs:129-136`; Platform `appsettings.Development.json:59-73` |
| F22 | No workflow template seeding exists; CRM templates created by Admin via API | grep; `WP-CL-CFG-1-mlr-workflow-templates-grants.md:39,133-169` |
| F23 | Template candidates win over request candidates | `StartWorkflowInstanceHandler.cs:121-123` |
| F24 | `position:` / `user:` candidate prefixes | `WorkflowCandidateResolver.cs:26-27,45-81` |
| F25 | **SoD compares e-mail (StartedBy) with GUID (actor)** | `StartWorkflowInstanceHandler.cs:139,164`; `CurrentUserContext.cs:59-71`; `TokenService.cs:35`; `WorkflowTaskTransitionSupport.cs:109,118-127` |
| F26 | Workflow runtime keys PlatformAdmin, not grantable to custom roles | `permission-scope-baseline.csv:277-279`; `AssignPermissionCommandHandler.cs:57-61` |
| F27 | DisplayContext → WorkCenter mapping | `WorkflowModels.cs:111-151`; `WorkItemProjectionService.cs:70,85-86,160-163` |
| F28 | No MOD-0048 inventory reason set; MOD-0048 excludes tenant ERP lookups | `MOD-0173-FU01-inventory-ledger-ui.md:199`; `MOD-0048-lookups-reference-data.md:76-78` |
| F29 | Company selector W-0173-02 | `MOD-0173-inventory-ledger-valuation.md:387-399`; `InventoryScopeResolutionMiddleware.cs:19-23,122-160` |
| F30 | Domain-config service contradiction (5061 vs 5066) | `domain-config.md:62-64` |
| F31 | ESIGN provider obligations for 0175 | `esign-bundle-v1-proposal.md:436-463`; MOD-0022 pack `:1283-1285` |
| F32 | Manifest namespace lesson (CAT-01) | `MOD-0174-lot-serial-tracking.md:841-851,887-888` |
| F33 | Mockup: 6 screens, 2-step decide/disposition, props eSign / fourEyes / companyTarget = 10 | "Karantina Bloke Stok (offline).html" template script `kar.app.js:99-100,133,209-213,257-260,318-319` |

### CT decisions — TAKEN 2026-10-07 (CT acceptance of WP-0175-P0)

All 14 are closed; the table below ("Open CT decisions") keeps the options for the record.

| # | Decision | By |
|---|---|---|
| CT-Q1 | (a) one template `INV-QUARANTINE-RELEASE`, candidates sent by 0175, `signatureRequired: true`. | CT |
| CT-Q2 | **Superseded:** the owner decided that WE fix MOD-0023 (WP-0023-FX, below) — runtime workflow keys move to Tenant scope, so a QA role can decide/approve. Until WP-0023-FX is CT-accepted, Q2 (workflow) does not start; no grant script, no "QA in the Admin role" workaround. | **Owner (Ali), 2026-10-07** |
| CT-Q3 | **(a) server-side hard block:** additive error code `LOT_RETEST_DUE` + availability excludes retest-due lots + INVENTORY-BUNDLE description note (same pattern as MOD-0174 CT-D1). | **Owner (Ali), 2026-10-07** |
| CT-Q4 | APPROVE for the approver step; the decider's own signature carries the kind-specific meaning. | CT |
| CT-Q5 | (a) the same approval also releases a Draft lot. | CT |
| CT-Q6 | (a) refuse with the reservation list `QUARANTINE_STOCK_RESERVED`. | CT |
| CT-Q7 | Keep MOD-0174 CT-D4 (Destroy only from lot-blocked / expired lots) in MVP-1. | CT |
| CT-Q8 | (a) local tenant list (7 languages, five seeded) + FU-0175-BRD. | CT |
| CT-Q9 | All-or-nothing if one ledger transaction can carry all lines (Q1 measures); otherwise idempotent continue + retry. | CT |
| CT-Q10 | Per-company switch, default on. | CT |
| CT-Q11 | Accept read-time reconcile + "Durumu yenile" for MVP-1. | CT |
| CT-Q12 | `QUARANTINE-BLOCKED-STOCK` (domain practice). | CT |
| CT-Q13 | (a) 0175 apply-time four-eyes check stays even after WP-0023-FX (defence in depth). | CT |
| CT-Q14 | Company-level target only; FU-0175-PRODUCT-TARGET. | CT |

**Precondition WP-0023-FX (owner decision 2026-10-07: we fix MOD-0023 ourselves, MVP-1 branch), CT-verified defects:**
1. SoD bypass for tenant users — `StartedBy` = `ActorName` = e-mail (`StartWorkflowInstanceHandler.cs:139,164`,
   `CurrentUserContext.cs` `ActorName`), approver = user GUID (`WorkflowApprovalWorkItemActionDispatcher.cs:67`) → the
   comparison in `WorkflowTaskTransitionSupport.cs:118-127` never matches (FU-0175-WF-SOD).
2. **Actor impersonation on the direct API (found by the CT):** `tasks/{id}/approve|reject|delegate|cancel` take the actor
   from the request body (`ApproveWorkflowTaskHandler.cs:42`); the validators only check non-empty
   (`ApproveWorkflowTaskValidator.cs:11`); nothing binds it to the authenticated caller → a holder of the approve key can
   act as another assignee. The WorkCenter path uses the caller's id and is safe.
3. Runtime workflow keys are PlatformAdmin-scoped (route `/Platform/Workflow`) and cannot be granted to a custom role
   (FU-0175-WF-KEYS).
Order: WP-0023-FX → MOD-0022 E4 (approve + `signatureId` must use the bound actor) and MOD-0175 Q2.

**WP-0023-FX result (agent result 2026-10-07, pending CT — evidence
`docs/records/audits/2026-10/mod-0023-fx-evidence-2026-10-07.md`):**
1. SoD fixed: the starter's user id is stored (`WorkflowInstance.StartedByUserId`) and compared as an id; instances
   started before the fix are checked against their Start log row (no backfill). FU-0175-WF-SOD → closed by FX (CT).
2. Actor binding: on the direct task API the actor is the authenticated caller; a body actor naming anyone else →
   403 `WORKFLOW_ACTOR_MISMATCH`. WorkCenter unchanged (already bound).
3. Runtime keys `instances.start`, `instances.view`, `tasks.approve|reject|delegate|request-info|cancel` are **Tenant**
   (Workflow manifest `ApiPermissions` + allowlisted, audited AuthService correction); design / publish / SLA /
   escalation / transition-gate keys stay PlatformAdmin. A tenant administrator can grant the runtime keys to a QA role.
   FU-0175-WF-KEYS → closed by FX (CT).
4. For this pack: the QA decider needs `instances.start` and the approver `tasks.approve`, both grantable to a QA role.
   CT-Q13 still holds — "submitter cannot approve" is not switchable per template (FU-0023-FX-SOD-SWITCH) — and the 0175
   apply-time four-eyes check stays (defence in depth).
5. Runtime probe (own ports, isolated DB) 34/34: QA role granted the 7 keys (204), design keys refused (403), A approving
   own instance → 409 `SOD_VIOLATION`, A approving B's task with B's id → 403 `WORKFLOW_ACTOR_MISMATCH`, B → 200.
**CT acceptance (K13, 2026-10-07):** re-measured at `db0ede5a2` — Platform workflow/registration/pages/aggregation filter
454/454; AuthService `Diten.AuthService.Application.Tests` 1023/1023 (owner fleet PIDs identical before/after); source
files = Workflow feature + controller + manifest + `TenantRouteScopeCorrections` (allowlist = exactly the 7 runtime
keys) + `DataSeeder`. CT sabotage: `WorkflowActorBinding` accepting any body actor → 5 red, restored. **FU-0175-WF-SOD and
FU-0175-WF-KEYS CLOSED.** Follow-ups: FU-0023-FX-WEB (free-text "Actor Id" field on `/Platform/Workflow` task forms,
`Tasks.cshtml:46-47`), FU-0023-FX-SOD-SWITCH. **Machine finding (CT):** `Diten.AuthService.AccountKindAcceptanceHost.Tests`
contains POSIX process-group kill code (`KillGroup`, SIGKILL, python3 listeners); a CT run of that project hung and the
owner's whole `dotnet watch` fleet was found dead afterwards — do NOT run it on this Windows machine (FU-0023-FX-ACCEPTHOST).

### Open CT decisions

| # | Question | Options | Recommendation |
|---|---|---|---|
| **CT-Q1** | Template shape | (a) **one template** `INV-QUARANTINE-RELEASE` for every decision kind (KQ-F wording); (b) one per kind | (a): one stage "QA", one step "QA onayı", type approval, **no candidates in the template** (0175 sends `settings.ApproverCandidates`), `requirements.signatureRequired: true`, `signatureMeaning: APPROVE` (CT-Q4). |
| **CT-Q2** | Who can decide / approve until FU-0175-WF-KEYS | (a) QA users in the tenant Admin role (works today); (b) manual grant script to a QA role (bypasses the 403 guard — escalation risk); (c) wait for MOD-0023 to move runtime keys to Tenant scope | (a) for MVP-1, with (c) as a follow-up. No grant script. |
| **CT-Q3** | KQ-H server-side hard block changes frozen-contract semantics | (a) new error code `LOT_RETEST_DUE` (additive) + availability description note (like CT-D1); (b) UI-only block (K-F as today) | (a). KQ-H says "kesin engel". Owner decision because INVENTORY is frozen. |
| **CT-Q4** | Approver step meaning | APPROVE for all kinds · RELEASE (release) / APPROVE (others) with two templates | APPROVE (one template). The decider's own signature carries the kind-specific meaning (RELEASE for releases). |
| **CT-Q5** | Releasing a hold whose lot is Draft | (a) the same approval also sets the lot Released; (b) separate lot release (two approvals) | (a). |
| **CT-Q6** | Reserved stock when quarantining / blocking | (a) refuse with the reservation list (`QUARANTINE_STOCK_RESERVED`); (b) 0175 releases reservations | (a). Reservations belong to their source module. Retest: existing reservations stay; new ones are refused. |
| **CT-Q7** | Supplier return of lot-blocked / expired lots (CT-D4 refuses) | keep CT-D4 (Destroy only) · extend the CT-D4 exceptions with SUPPLIER_RETURN | keep CT-D4 in MVP-1. |
| **CT-Q8** | Block reasons | (a) **local** tenant list in 0175 (7-language names, seeded with the mockup's five); (b) wait for a MOD-0048-FU01 set | (a) + FU-0175-BRD migration. |
| **CT-Q9** | Partial apply failure | continue remaining lines on retry (idempotent) · all-or-nothing (one ledger transaction for all lines) | all-or-nothing if the internal posting service can post several movements in one transaction (Q1 measures it); otherwise continue + retry. |
| **CT-Q10** | KQ-I gate default | on for every company · per-company switch, default on | per-company switch, default on. |
| **CT-Q11** | No background reconciler (no service identity) | accept read-time + "Durumu yenile" · add a service-token path to MOD-0023 | accept for MVP-1. |
| **CT-Q12** | Module code case | `QUARANTINE-BLOCKED-STOCK` (domain practice) · `quarantine` (standard :16) | domain practice, for consistency with 0173/0174. |
| **CT-Q13** | KQ-C switch while SoD is broken | (a) FourEyes on: 0175 apply-time check (built); off: no 0175 check — and Workflow SoD (once fixed) is not switchable, so "off" only skips 0175's own check; (b) MOD-0023 adds a per-template SoD flag | (a) + note: turning four-eyes truly off needs MOD-0023 work. |
| **CT-Q14** | Wait target per product (mockup) | company only (KQ-D) · product override later | company only; product override is FU-0175-PRODUCT-TARGET (needs a product master field). |

### Assumptions

- **ASSUMPTION-0175-01:** UI folders `Views/Quarantine/**`, `js/Quarantine/**`; MVC route prefix `/Quarantine`.
- **ASSUMPTION-0175-02:** one hold per intake leg; several receipts into the same bucket give several holds, each decided
  separately.
- **ASSUMPTION-0175-03:** the internal posting service posts with an explicit LegalEntity and actor and reuses every ledger
  validator. No validator is bypassed.
- **ASSUMPTION-0175-04:** the approver and the approver signature id are read from `GET instances/{id}/history`
  (the transition log carries the user id, `StartWorkflowInstanceHandler.cs:214`, and the signature id after MOD-0022 E4).
- **ASSUMPTION-0175-05:** the "Stok hareketleri" table reads the ledger in-process by `sourceDocumentId`, because the
  public transactions API has no such filter (`inventory-bundle.openapi.yaml:155-175`).
- **ASSUMPTION-0175-06:** "Elle" mockup source = Manual; "İade" = CustomerReturn.

### WP-0175-Q1 (agent result 2026-10-08, pending CT) — records, decisions, apply engine, contract

Branch `feature/sce/mod-0175-q1` from `2bdedbb26` (isolated worktree): `0d1c66012` freezes `quarantine.openapi.yaml` **1.0.0**
(single writer; = proposal, measured by `QuarantineContractTests`; reconciliations R-1 envelope = measured INVENTORY/TRACE
wire, R-2 optional `bucket.lotId`, R-3 settings of the scope company, R-4 `Decision.disposition.sampleLocationId`), `06a86918d`
the slice: the seven records in-process (`quarantine_*`, decisions / events / history insert-only), the MOD-0173 seam exactly
as §5 Q1 (`IPostedMovementObserver` post-commit + `InternalMovementPostingService` = the `PostMovementHandler` body moved
unchanged), intake observer + sweep (one record per ledger transaction), V1–V14, the apply engine for every stock-effect row
except KQ-I (Q4), the no-workflow Karantinaya Al / Stoğu bloke et with CT-Q6 `QUARANTINE_STOCK_RESERVED`, manifest
`QUARANTINE-BLOCKED-STOCK` (`quarantine.*`, tenant routes), 17 contract operations. **CT-Q9 measured:** `PostAsync` opens its
own Mongo transaction per posting and Mongo has no nested transactions → idempotent continue + retry (ledger keys
`quarantine:{decisionId}:{lineNo}`, committed keys answered before posting, ApplyFailed keeps the lock — contract §5).
Approval in Q1: production default fails closed (submit → 503 `QUARANTINE_WORKFLOW_UNAVAILABLE`, nothing moves); the test
approval port + its endpoint exist only on a DB-010 test database (startup gate). Tests: Inventory 530 → **592/592** (the
530 baseline tests all present and green), architecture 18/18, build 0 warnings; four existing guards narrowed by name (PUT
allow-list of the two master-data replacements, `quarantine_*` collections, the moved posting hook, the ledger manifest
scan). K3 sabotages (a) update method on the decision repository, (b) `"SAMPLE"` in a 0175 mapping, (c) `/Platform/Quarantine`,
(d) `inventory.stock.read` in the manifest, (e) engine key removed → red, restored clean. Runtime probe (own Prism 4036 +
InventoryService 5176, `diten_inventory_q1_itest`) 22/22 + Mongo read; owner fleet PIDs unchanged. Open: contracts README row
(O-1), virtual LotBlocked rows (O-2), serial-tracked items in the engine (O-3), Q2–Q4 parts by plan (O-4), master-data names
(O-5), KPI performance (O-6), §8 ApplyFailed wording vs contract §5 (O-7), W-0175-01. Evidence
`docs/records/audits/2026-10/mod-0175-q1-evidence-2026-10-08.md`.
**CT acceptance (K13, 2026-10-08):** re-measured at `26b25c947` — Inventory 592/592 (the 530 MOD-0173/0174 tests unchanged),
architecture 18/18; ledger side = only the two pack §5 seams (`IPostedMovementObserver`, `InternalMovementPostingService`;
`PostAsync` unchanged). CT sabotage: apply-time four-eyes check disabled (`QuarantineApplyEngine.cs:386`) → 1 red, restored.
CT decisions on the opens: **O-7** — the contract wins: an `ApplyFailed` decision keeps the hold locked and "Yeniden uygula"
continues with the same keys (§8 wording superseded); **O-3 → Q2 scope (FU-0175-Q1-SER):** the apply engine must carry the
hold's serial numbers for serial-tracked items (finished goods are ITS-serialised — a release that always ends ApplyFailed is
not acceptable); **O-2 → Q4** (with the lot gate); **O-5 → Q5** (labels via the existing label resolvers); **O-6** measured in
Q5 with the queue page; **O-1** contracts README row = CT records step at MVP-1 closure; CT-Q9 = idempotent continue + retry
(measured: `PostAsync` opens its own transaction) — accepted.

### WP-0175-Q2 (CT-ACCEPTED 2026-10-08) — Workflow integration + FU-0175-Q1-SER

**CT acceptance (K13):** v1 re-measured at `7bf32be2e` (613/613) but REWORK requested: the two concurrent-apply guards of `417dc7b24` had no failing test (CT sabotages S1 `if (false)` and S2 `InFlightGrace = Zero` both left 83/83 green). v1.1 (`de6b92ca1`, `162c604da`) adds deterministic tests (`QuarantineApplyRaceTests.cs`, injectable `TimeProvider`); CT re-ran both sabotages → each 1 red, restored; Inventory 617/617. **FU-0175-Q1-SER CLOSED.** CT decisions on the opens: **O-Q2-3 → Q3 scope** — submit is refused when no approver candidate other than the decider exists (otherwise an un-approvable task is created); O-Q2-2 (withdraw 400/404/503 not in the contract) → next additive contract version; O-Q2-4 lot number labels → Q5; **O-Q2-5** Q1 test approval port is removed in Q5; O-Q2-1 RabbitMQ event path runtime proof → environment follow-up (machine has no broker); O-Q2-6 serial split for manual quarantine/block → Q4 (with the lot gate). MassTransit.RabbitMQ 8.3.6 added to InventoryService Infrastructure, bus registered only with `Eventing:Transport=RabbitMQ` (default InMemory) — accepted.

Branch `feature/sce/mod-0175-q2` from `9d93a7dbc` (isolated worktree): `e2d2eb7d4` code + tests, `467dc08da` K3 (a) measure,
`417dc7b24` concurrent-apply fix. `GatewayWorkflowClient` (Gateway → MOD-0023 with the **caller's** bearer via
`ForwardCallerHeadersHandler`, `X-Tenant-Id` from the scope; no service token) and `WorkflowApprovalPort` as the production
port (template `INV-QUARANTINE-RELEASE`, key `quarantine:quarantine.decision:{id}:r1`, candidates from settings — none →
409 `QUARANTINE_APPROVERS_NOT_CONFIGURED`, DisplayContext with `/Quarantine/Details/{holdId}`; start error → StartFailed, no
movement). Read-time reconciliation on holds / blocked / decision reads + refresh (only PendingApproval / Approved decisions,
own instance, approver = Workflow history actor) → Q1 applier once; withdraw cancels the task with the decider's token;
"Onay şablonunu hazırla" create + publish, idempotent. Consumer + `quarantine_event_inbox` behind `Eventing:Transport`
(default InMemory = no MassTransit; `MassTransit.RabbitMQ 8.3.6` added). **Template measurement:** Platform stores the
definition verbatim incl. `signatureRequired` / `signatureMeaning` (accepted, not enforced until MOD-0022 E4).
**FU-0175-Q1-SER:** holds / blocked records carry the intake serials; lines split them in intake order
(ASSUMPTION-0175-Q2-01); movements post with serials. Tests 592 → **613/613**, architecture 18/18, 0 warnings. K3 (a) reconcile
filter, (a2) + all apply guards, (b) no user token, (c) reject → approve, (d) serials dropped → red, restored clean; (a) shows
re-ask, not double movement (ledger keys + once-only events hold). Runtime probe (own Auth / Platform / Gateway with temp
ocelot / Inventory / Prism / LE stub, own mongod, login tokens only) **25/25**; it found a concurrency bug (parallel reads →
Applied then ApplyFailed ×5, stock moved once) — fixed in the 0175 applier, re-probed clean. MOD-0023 assigns ONE resolved
candidate: the starter is refused 403 `WORKFLOW_ACTOR_DENIED` when another candidate is assigned, 409 `SOD_VIOLATION` when the
starter is the assignee. Open: RabbitMQ runtime (O-Q2-1), withdraw response codes vs contract (O-Q2-2), single candidate =
decider (O-Q2-3), DisplayContext labels (O-Q2-4 → Q5), test port removal (O-Q2-5), serials on the wire / manual serial
quarantine (O-Q2-6). Evidence `docs/records/audits/2026-10/mod-0175-q2-evidence-2026-10-08.md`.
**CT v1.1 düzeltmesi (`de6b92ca1`): S1/S2 testleri + sabotajlar.** The two concurrent-apply guards had no sabotage proof
(CT: both stayed green when disabled). Deterministic tests (`QuarantineApplyRaceTests`, no concurrency, no waiting): S1 a staged
loser (stale 0175 view + stale ledger key lookup, real ledger refuses `INSUFFICIENT_STOCK` — control test) after the winner
applied → Applied, no ApplyFailed, 1 movement; S2 with an injected `TimeProvider` (only the grace clock; `TimeProvider.System`
in DI): fresh Approved → Approved, nothing posted; older than the grace → Applied. Sabotages s1 (both Applied checks), s1f
(RecordFailureAsync only), `InFlightGrace = Zero`, `= MaxValue` → each 1 red, restored clean. Inventory **617/617**, architecture 18/18.

### WP-MVP1-INT-QE — INT-QUARANTINE (CT-ACCEPTED 2026-10-08)

**CT acceptance (K13, 2026-10-08):** gateway tests 101/102 re-run at `75c981df6` (only the known FU-INT1-01 red); ocelot diff = additions only; CT sabotage: quarantine `{everything}` block port 5066 → 5067 → 3 red, restored. Body limit (Kestrel default 30 MB) sufficient for the 1 MB PNG; `/internal/*` stays unrouted (404).

Branch `feature/sce/mvp1-int-qe` from `720a58ede` (isolated worktree), commit `ae2055739`: four ocelot blocks after
`/api/trace/{everything}`, cloned from the INT-TRACE blocks (only path / port / methods changed, no gateway auth, headers passed
through): `/api/quarantine[/{everything}]` → 5066 GET/POST/PATCH/PUT/OPTIONS and `/api/v1/esign[/{everything}]` → 5057
GET/POST/PUT/OPTIONS (the contracts' methods; no DELETE in either). +74 lines, no existing line changed. Gateway tests 88/89 →
**101/102** (13 new green; the known red FU-INT1-01 grows by exactly the two 5066 lines, 128 → 130); architecture 18/18. Every
contract operation is matched by exactly one route (tests); K3 port / narrowed upstream → red, restored clean. Runtime (own
gateways on 15290 / 15291): requests reach `localhost:5066/api/quarantine/…` and `localhost:5057/api/v1/esign/…` (gateway log);
1 MiB PNG as base64 (1 398 223 B) and 2× forwarded byte-identical with all pass-through headers; 31 MB → 413 (Kestrel default
30 000 000, sufficient). Evidence `docs/records/audits/2026-10/mvp1-int-qe-evidence-2026-10-08.md`.

### WP-0175-Q3 (agent result 2026-10-08, pending CT) — e-signature integration

Branch `feature/sce/mod-0175-q3` from `6454d3204` (isolated worktree), commit `f81451a80`; contracts unchanged. Provider
`getSignableSubject` (`qd-v1` = the contract's closed field list, **35 keys incl. `subjectVersion`**; 404 without
`quarantine.holds.read` / other company; a claim-less token with no company header takes the record's company — MOD-0022 sends
none, ASSUMPTION-0175-Q3-02). Submit: effective MOD-0022 policy of the decision's action (strictest line); required + none →
400 `QUARANTINE_SIGNATURE_REQUIRED`; a signature (read with the caller's token) must bind signer, decision, policy meaning, not
invalidated, current `qd-v1` hash (ASSUMPTION-0175-Q3-01) → else 409 `QUARANTINE_SIGNATURE_MISMATCH`; ids only on the
`Submitted` / `Approved` events. Approver signature id read from the MOD-0023 history. **O-Q2-3:** the decider is never sent
as a candidate; none left → 409 `QUARANTINE_APPROVERS_NOT_CONFIGURED`. The 7 actions registered at start-up (S2S, best
effort). Platform: `ESign:SubjectProviders:quarantine:BaseUrl` (Development) only. Template measurement: E4 reads the step
flags at every start from the pinned version — no new version needed; pre-E4 tasks stay unsigned (O-Q3-3). Tests 617 →
**637/637**, Platform filtered 580/580, architecture 18/18, 0 warnings. K3 (a) signer, (b) subject (hash layer behind it),
(c) provider field, (d) O-Q2-3 → red, restored clean. Runtime probe (own Auth / Platform E4 / Gateway temp ocelot / Inventory /
Prism / LE stub, own mongod, real login + re-auth) **20/20**: unsigned submit 400 → A signs → 202 → B unsigned WorkCenter
approve 409 → B signs the same decision subject with the task context → 200 (**CT-D17 proven live**) → refresh Applied, 1
movement, both signatures `valid`, Platform hash = 0175 hash; only candidate = A → 409; no signature data in `quarantine_*`.
Open: O-Q3-1 the decider needs `esign.signatures.read` (not in the default signer set / §14), O-Q3-2 manual quarantine / block
signatures, O-Q3-3 pre-E4 tasks, O-Q3-4 event path keeps no approver signature id. Evidence
`docs/records/audits/2026-10/mod-0175-q3-evidence-2026-10-08.md`.

**Q3 — CT acceptance (2026-10-08).** CT re-ran Inventory **637/637** and architecture **18/18** in the agent worktree; read the
scope-middleware exception, the provider controller, `DecisionCompanyAsync` (tenant-filtered) and the decider check. CT sabotages:
S1 drop the controller check "named company = record company" → **stays green** (107/107): the effective guard is the
company-scoped repository (an other-company token resolves its own scope, the decision is not found → 404); the controller check
is a redundant second layer, kept. S2 drop it AND always take the scope from the record (a real cross-company read) →
`The_provider_is_404_without_holds_read_for_another_company_and_for_another_type` red, restored, porcelain clean. Provider
bypass risk = W-0173-02 (company comes from a tenant-filtered record; no user↔LE data exists) — accepted. Fast-forwarded into
the MVP-1 branch. Decisions on the opens:
- **CT-Q15 (O-Q3-1):** `esign.signatures.read` joins the MOD-0022 CT-D10 default signer set (delivered in E5, with the other
  default-grant work) and §14 external keys; existing tenants = manual grant like the other `esign.*` keys.
- **O-Q3-2** signatures on Karantinaya Al / manual block → **Q4** (with the serial split, same commands).
- **O-Q3-3** tasks created before E4 stay unsigned-approvable — dev data only, accepted, no migration.
- **O-Q3-4** RabbitMQ event path without approver signature id → with O-Q2-1 (environment follow-up); reconcile is the live path.
- ASSUMPTION-0175-Q3-01…04 accepted for MVP-1 (`skuCode` null until MDM MOD-0290 serves it).

### WP-0175-Q4 (agent result 2026-10-08, pending CT) — retest hard block + lot release gate

Branch `feature/sce/mod-0175-q4` from `967a50fde` (isolated worktree), commit `03b4d1e95`; QUARANTINE 1.0.0 and TRACE 1.2.0
unchanged; INVENTORY-BUNDLE 1.0.1 → **1.0.2 description-only (CT-Q3)**: `LOT_RETEST_DUE` example + availability note "retest-due lots
excluded", README row.

**CT kararı — CT-Q16 (v1.1, 2026-10-08):** option A, one stored hold. `LotPostingRules.HoldsStock(Lot) = Blocked || Expired ||
RetestHeldAtUtc != null`; the status-only overload is removed and every reader (ledger branch, `SkuPositionRebuilder`,
`InventoryLotDirectory.HeldLotIdsAsync`, `ManualLotMigrationRepository` → `LedgerProjectionReKey`, `LotHeldStock`) asks it. A date
is compared only in the posting rule and in the sweeper's selection. `RetestHoldSweeper` (TimeProvider, at start + periodic) sets /
clears the mark and moves the SKU position in ONE transaction; the ledger writes the lot (`StockTouchSeq`) inside its own
transaction, so the two conflict and one re-runs (measured; race test: live == rebuilder in every round). Seams used: exactly the
CT-Q16 list + pack §5 Q4 (`TraceabilityServiceCollectionExtensions` and `GetAvailabilityHandler` needed no change).
**CT kararı — ASSUMPTION-0175-Q4-01:** quarantineStock / blockStock keep refusing `signatureId` → FU-0175-Q4-SIGN (§20).

Built: `LOT_RETEST_DUE` 422 (direction-based exceptions, never the claimed `sourceModule` — ASSUMPTION-0175-Q4-01b); `listRetest`;
Karantinaya Al mode Retest (one hold per AVAILABLE bucket, Source Retest; ASSUMPTION-0175-Q4-03); V9 also for any hold of a
retest-due lot; retest release = the approved new retest date on the lot, the hold cleared, then the release. KQ-I:
`QuarantineLotStatusChangeGate` (Draft / Blocked → Released only, `LotReleaseRequiresApproval`) → **202 `{lotId, pendingRequestId,
contractVersion}`**, Draft `LotRelease` decision (`LotStatusFrom`) opened after the read-only lot transaction (ASSUMPTION-0175-Q4-05);
internal `ApplyApprovedLotStatusChange` (no route, one sender; approved decision of this lot from its `LotStatusFrom` only), lot
history "MOD-0175 approval {QKR}: …" in `reason` (TRACE `source` is a closed enum — ASSUMPTION-0175-Q4-04), actor = approver;
Released → Blocked immediate; CT-Q5. Serials of the manual actions (O-Q2-6): system-chosen, intake order, stored on the record
(ASSUMPTION-0175-Q4-02). Tests 637 → **683/683** (the 4 Prism smoke tests green in this run), architecture 18/18, 0 warnings.
Sabotages (a)–(g) red → restored, diff empty. Runtime probe **27/27** (own Auth / Platform E4 / Gateway / Inventory / Prism / LE stub,
own mongod): 202 → signed submit → B signed WorkCenter approve → "Durumu yenile" Applied, lot Released + history; retest-due sales
issue 422; hosted sweeper hold; availability −100; new reservation 422, existing Active; listRetest DueClosed; Karantinaya Al
Retest. Open: **O-Q4-1** a retest date PATCH (`trace.lots.update-released`) frees the lot without a quality decision (MOD-0174, not in
the seam list); **O-Q4-2** `getLotQualityStatus` + Lots page 202 handling → Q5; **O-Q4-3** two simultaneous LotRelease submits of one
lot. Evidence `docs/records/audits/2026-10/mod-0175-q4-evidence-2026-10-08.md`.

**Q4 — CT acceptance (2026-10-08).** CT re-ran Inventory **683/683** (the 4 Prism smoke tests green) and architecture **18/18** in the
agent worktree; checked every non-test file against the CT-Q16 seam list (inside it; no Web / Platform / Gateway, quarantine and
trace contracts untouched); read `HoldsStock(Lot)`, the ledger branch (lot read in the posting transaction) and
`ApplyApprovedLotStatusChange`. CT sabotage (own): drop the "approved decision is about THIS lot" check →
`The_command_past_the_gate_refuses_without_an_approved_decision_of_this_lot` red, restored, porcelain clean. CT corrected the
INVENTORY-BUNDLE 1.0.2 changelog wording to match ASSUMPTION-0175-Q4-01b (exception by movement direction, never by
`sourceModule`). Fast-forwarded into the MVP-1 branch. Decisions:
- ASSUMPTION-0175-Q4-01b, -04, -05 accepted.
- **CT-Q17 (O-Q4-1):** on a non-Draft lot, `updateLot` may move `retestDate` EARLIER (safe direction) but not later or to null →
  400 `LOT_KEY_IMMUTABLE` (CT first wrote 409; the existing TRACE mapping is 400 — Q4R CT acceptance) (already in TRACE 1.2.0 `updateLot`); a later retest date comes only from an approved MOD-0175 retest
  release. Delivered in **WP-0175-Q4R** (before Q5).
- **O-Q4-3** two concurrent LotRelease decisions for one lot → **WP-0175-Q4R** (one open LotRelease per lot, unique index + test).
- O-Q4-2 (`getLotQualityStatus`, Lots page 202) → Q5.

### WP-0175-Q4R (agent result 2026-10-08, pending CT) — CT-Q17 retest PATCH guard + one open LotRelease per lot

Branch `feature/sce/mod-0175-q4r` from `58177127e`, commit `f4f8642b6`; contracts unchanged. **CT-Q17:** `updateLot` on a non-Draft lot
moves `retestDate` only earlier; later or null → **400 `LOT_KEY_IMMUTABLE`** (the code's existing status, `TraceRequestReader`; the CT-Q17
line above says 409 — CT to confirm); Draft unchanged; the only later move is the approved retest release (`ApplyApprovedLotStatusChange`,
not through updateLot). An earlier date blocks postings at once; the stored hold follows on the sweeper's run (CT-Q16). **O-Q4-3:**
`LotReleaseClaim` (`quarantine_lot_release_claims`, unique `ux_quarantine_lot_release_open` Tenant + LE + lot) inserted in the same 0175
transaction as the LotRelease decision; the loser answers the open decision (202, same `pendingRequestId`); closed / stale claims are
replaced in one transaction; pre-Q4R open decisions are adopted. Index on a new collection — measured: builds over two open legacy
decisions; a duplicate claim would stop the startup gate (fail-closed). Tests 683 → **691/691**, architecture 18/18, 0 warnings; sabotage
(a) direction check off → 2 red, (b) claim off → parallel (4 decisions) + staged race red; restored, diff empty. **FU-0175-Q4-RETEST-EDIT
CLOSED.** Evidence `docs/records/audits/2026-10/mod-0175-q4r-evidence-2026-10-08.md`.

**Q4R — CT acceptance (2026-10-08).** CT re-ran Inventory **691/691** and architecture **18/18** in the agent worktree; read the
`UpdateLotHandler` guard and `LotDateRules.RetestMovesLater`. CT sabotage (own): drop the "removed (null)" branch so only "later"
is refused → `A_non_Draft_lots_retest_date_cannot_move_later_or_be_removed_by_an_edit` red (Released + Blocked), restored, porcelain
clean. **400** `LOT_KEY_IMMUTABLE` accepted (existing TRACE mapping; CT-Q17 text corrected). Claim collection
`quarantine_lot_release_claims` + `ux_quarantine_lot_release_open` accepted as "quarantine repo" scope. Accepted residual: after an
edit moves the retest date earlier, new reservations may still count the lot until the next sweep (≤ 900 s); postings are refused
at once. Fast-forwarded into the MVP-1 branch.

### WP-0175-Q5 (agent result 2026-10-09, pending CT) — screens, Lots 202 + badge, getLotQualityStatus, test port out

Branch `feature/sce/mod-0175-q5` from `562afeaa5` (isolated worktree). Commits:

1. `5c453f347` — §17.3 UI pattern table, committed BEFORE any code.
2. `d55a4397c` — code + tests.
3. Evidence + this note.

Contracts unchanged (QUARANTINE 1.0.0, TRACE, ESIGN).

**Web (§11.1).** `QuarantineController` under `/Quarantine` serves 8 pages:

- **Pages:** queue, details, decide, blocked, disposition, retest, submit (`/Quarantine/Decisions/{id}/Submit`), settings.
- **Proxy:** `api/{**}` → gateway `/api/quarantine/**`. Writes carry antiforgery; `Idempotency-Key` and `If-Match` are passed through; bodies are not logged.
- **Lists:** DataTable v2 in **server** mode (pack `data_mode: server` wins over the prompt's "golden slim").
- **Decide / Disposition:** two steps — draft first, then `ESign.open` + submit.
  - 0175 draws no password field and keeps only the `signatureId`.
  - A draft carries no `signatureRequired`, so the client reads the MOD-0022 effective policy through the E5W1 proxy. An unreadable policy counts as required.
- **Details:** workflow box with refresh / withdraw / reapply.
- **Blocked stock:** "Stoğu bloke et", "İncelemeyi tamamla", and Unblock as a decision.
- **UAS-001** on every page.
- **Resx:** 7 languages × 420 keys; 5 nav keys (the 2 baseline-red nav guard tests are now green).

**Lots (MOD-0174 Web files only).** A 202 shows "Onaya gönderildi" with a link to the Submit page. The "Onay bekliyor" badge comes from `quality-status`.

**Inventory (NE-4 only).**

- `getLotQualityStatus` is served; the contract-test route count is now 22.
- **O-Q2-5 closed:** the test approval port is removed from production. Tests plug in their own port; a source-scan + single-DI guard protects this.
- DisplayContext labels: lot number, product `code · name`, warehouse / location codes, blocked number link, readable disposition kind.

**Runtime findings, fixed with tests:**

- (1) The disposition title read "Disposition · Destroy".
- (2) `quality-status` did not reconcile, so after approval the lot page stayed "Taslak" until someone opened the decision. It now reconciles the decisions it touches, like every other quarantine read (§8).

**Tests.**

| suite | result |
|---|---|
| Web | 630/632 → **649/649** |
| Inventory | 687/691 → **688/692** (the same 4 Prism smoke tests; Prism did not listen within 90 s — environment) |
| Quarantine subset | 141/141 |
| Architecture | 18/18 |
| Warnings | Web 17 = baseline; Inventory 0 |

**Sabotages** — each red, then restored with md5 equal: (a) `/Platform/Quarantine` route; (b) Decide UAS check off; (c) zh key deleted; (d) `InternalTestApprovalPort` re-added; plus (e) the disposition label and (f) the `quality-status` reconcile.

**Runtime probe** (own stack, browser):

- Partial decision signed → B approves in WorkCenter (readable chips) → Applied with ledger movements.
- Disposition İmha (DrawRequired) → applied: 60 → 20.
- Retest Karantinaya Al.
- Lots Draft → 202 → Submit signed → B approves → lot Released.
- Review complete → Unblock decision signed.
- User C (no role) sees only the access explanation.
- 375 px views have no horizontal overflow.
- The owner fleet PIDs are identical before and after; the password appears 0 times in the logs.

**Opens:**

- **O-Q5-1** — `updateHold` (PATCH) is not served, so the analysis note is read-only.
- **O-Q5-2** — people are shown as user ids (no names in the contract).
- **O-Q5-3** — `listBySource` is not served.
- **W-0175-01** — no Python, so the DataTable verifier was not run.

Evidence: `docs/records/audits/2026-10/mod-0175-q5-evidence-2026-10-09.md`, `-scripts-2026-10-09.md`, `mod-0175-q5-shots/`.

**Q5 — CT acceptance (2026-10-09).** CT re-ran Web **649/649** (the 2 quarantine `Nav.Page.*` reds of the MVP-1 branch closed),
Inventory **692/692** (the Prism smoke tests green in the CT run) and architecture **18/18** in the agent worktree; §17.3 table
committed before code (5c453f347). Read `QuarantineController` (E5W1 proxy pattern: cookie token, tenant from the token, bodies
not logged) and the two Upstream changes (`canonicalCode` / `name` display labels only, no rule reads them). CT sabotage (own):
Disposition page UAS-001 gate → `@if (false)` → `UAS_001_…(view: "Disposition")` red, restored, porcelain clean. Screenshots 03 / 04
reviewed against the mockup. Merged into the MVP-1 branch. Decisions on the opens:
- **O-Q5-1** `updateHold` (analysis note PATCH) in the contract but not served → **FU-0175-Q5-01**, before G1.
- **O-Q5-2** creator / approver / candidates shown as user ids → **FU-0175-Q5-02**: the Web resolves display names through the existing
  person / user label resolver (no contract change), before G1.
- **O-Q5-3** `listHoldsBySource` not served → **FU-0175-Q5-03**, with MOD-0177 recall (its first consumer).
- W-0175-01 (no Python, DataTable verifier) stays open to MVP-1 closure.

**ITEM — `Item` in QUARANTINE, updateHold, people by name (WP-0174-ITEM, CT-MVP1-0174-ITEM v1, 2026-10-09; agent result, pending CT).**
- **QUARANTINE-BUNDLE 1.1.0** (additive): `SkuLevel` + `Item`, nothing else. The 1.0.0 file is pinned and the diff is measured.
- **Item stock.** Item-level stock is quarantined (intake and manual), blocked, decided and applied.
  - The `qd-v1` field list is unchanged; for Item, `itemId == skuId` (tested).
  - O-PM1-1 is closed.
- **FU-0175-Q5-01 CLOSED — `updateHold`.**
  - `PATCH /holds/{id}`: key `quarantine.holds.review`, If-Match, contract idempotency.
  - Refusals: 409 `QUARANTINE_HOLD_CLOSED` and 409 `QUARANTINE_CONFLICT` (handler check + version-conditioned replace).
  - Writes one `inventory_audit` row (`QUARANTINE_HOLD_UPDATED`, before / after).
  - **Owner decision K-1:** no history row, because the frozen `HistoryAction` has no such value → **O-ITEM-1** (`NoteUpdated` in a
    future additive version).
  - The architecture test now allows `InventoryCollections.Audit` (Find / InsertOneAsync) and nothing else of the ledger.
  - Kayıt Detayı edits the note in place. Drawn only for the review key (UAS-001); the JS uses the `STARTREVIEW` code, same key.
- **FU-0175-Q5-02 CLOSED — people by name.**
  - The Web fills `UserRef.name` (creator, history, decider, approver) and the "user:" approver candidates (`lookups/users`).
  - Source: AuthService `GET /api/users/{id}/display-label`, called with the caller's token. No new endpoint, no contract change
    (ASSUMPTION-0174-ITEM-02; resolver measurement in evidence §6).
  - An unresolved id shows the short id + "unknown user".
  - **O-ITEM-2:** names need `auth.users.lookup`, which only tenant Admin holds by default → role template decision.
  - O-ITEM-3: `byUserName` on movements stays empty (not served).
- **Measured.** Inventory 800 → **829** (own replica set); Web 685 → **694**; gateway 107/108 (known 5065); architecture 18/18.
  - Warnings 0 / 21 / 7, unchanged.
  - 21 red-first cases on the base code, run in a separate base worktree.
  - Sabotages (a)–(e) red with 7 / 3 / 6 / 1 / 3 failures.
  - Runtime probe on own ports, built-in browser: Lots → lot on an Item raw material → lotted receipt into QUARANTINE → note edit →
    signed decision (ESIG-2026-000001) → approval by B in WorkCenter (ESIG-2026-000002) → Applied; people shown by name.
- **Evidence.** `docs/records/audits/2026-10/mod-0174-item-evidence-2026-10-09.md`, `-scripts-2026-10-09.md`, `mod-0174-item-shots/`.

**ITEM — CT acceptance (2026-10-10).** The CT merged the MVP-1 branch (FUB1) into `feature/sce/mod-0174-item` (clean) and measured
the merged tree: Inventory **829/829** (the suite started and stopped its own ephemeral replica set — 27017 untouched), Web **696/696**,
gateway **107/108** (known 5065), architecture **18/18**. CT sabotage (own): the two `quarantine.holds.review` gates of the analysis-note
editor on Kayıt Detayı → `@if (true)` → `The_note_editor_is_drawn_only_for_the_review_key_and_patches_with_the_page_ETag` red, restored,
porcelain clean. **TRACE 1.3.0, QUARANTINE 1.1.0, INVENTORY 1.2.1 FROZEN.** FU-0175-Q5-01 and FU-0175-Q5-02 CLOSED. Fast-forwarded
into the MVP-1 branch. Decisions on the opens:
- **O-ITEM-1:** a note change is in `inventory_audit` with old / new values (Part 11 audit trail met); its row in the decision history
  → **FU-0175-ITEM-01** (history entry kind in the next additive QUARANTINE version).
- **O-ITEM-2 → FU-0175-ITEM-02:** display names need `auth.users.lookup`, today only on the tenant Admin role. Add it to the default
  quarantine / QA roles for new tenants (AuthService default-role template, CT-D10 pattern); existing tenants = manual grant (owner told).
- **O-ITEM-5 → FU-0175-ITEM-03 (before G1):** the analysis note stays editable while a decision is PendingApproval — lock the edit
  (409) until the decision is decided / withdrawn, so the approver signs what the decider saw. (The note is not a `qd-v1` field, so
  signatures do not break; the rule is GxP clarity.)
- **O-ITEM-3** (movement rows without user name) and **O-ITEM-4** (`updateHold` replay returns the current state) → small FU batch.
- O-ITEM-6: known gateway 5065 red (procurement).

### FUB2 — implementation note (WP-MVP1-FUB2, 2026-10-10, pending CT)

- **Branch and commits:** branch `feature/mvp1-fub2` from `fa4c4e188`, local only, no push. Commits `f35197c5a` (contracts),
  `52176c83e` (InventoryService), `6b5382e23` (Web). Evidence: `docs/records/audits/2026-10/mvp1-fub2-evidence-2026-10-10.md`
  (+ `-scripts`, `mvp1-fub2-shots/`).
- **QUARANTINE 1.2.0 (additive).**
  - `HistoryAction` + `NoteUpdated`.
  - Optional `HistoryEntry.noteChange` (`NoteChange`: note length / attachment count before → after; the text stays in `inventory_audit`).
  - `updateHold` lists the existing `QUARANTINE_DECISION_ALREADY_PENDING`.
  - Diff validator against the new 1.1.0 fixture (byte-pinned).
- **FU-0175-ITEM-01 CLOSED.**
  - A saved `updateHold` writes one `NoteUpdated` row (by, atUtc, noteChange) in the same transaction as the hold and the
    unchanged audit row.
  - Kayıt Detayı shows "Analiz notu güncellendi · Not: a → b karakter · Ek: c → d".
- **FU-0175-ITEM-03 CLOSED.**
  - While `ActiveDecisionId` is set (decision waiting for approval, or approved and not yet applied), `updateHold` → 409
    `QUARANTINE_DECISION_ALREADY_PENDING`, nothing written.
  - Editable again when the decision ends (rejected / cancelled / timed out / withdrawn). An applied decision closes the hold (409
    `QUARANTINE_HOLD_CLOSED`, unchanged). A draft does not lock.
  - Kayıt Detayı draws the lock explanation instead of "Notu düzenle" (review key only, UAS-001).
- **O-ITEM-4 CLOSED.**
  - The same key answers the first result (version, note, attachments from the first call's audit row; the rest is the current
    read, the decision-replay pattern) even after a later edit. Another body → 400 `IDEMPOTENCY_KEY_REUSED`.
  - O-FUB2-3: a full snapshot would need a result store.
- **FU-0175-ITEM-02 BLOCKED (O-FUB2-1).**
  - Viewer is the only default role with quarantine keys.
  - `RoleProvisioningService.EnsureDefaultRolesAsync` re-applies the template additively also on `tenant-admin-invited` (any
    existing tenant) and `tenant-activated`. `DataSeeder` tops up the seed tenant. So a template line reaches existing tenants.
  - The same path already carries the CT-D10 signer keys.
  - Options A (new-tenant-only set), B (accept the spread) and C (explicit grant at quarantine set-up) are in evidence §2.1.
    No AuthService change.
- **O-ITEM-3 BLOCKED (O-FUB2-2).**
  - `MovementLink` has only `byUserName` (no user id; the service has no names); INVENTORY `TransactionItem` has no user.
  - Needs QUARANTINE `MovementLink.by: UserRef` and/or INVENTORY `TransactionItem.createdBy` — not an allowed 1.2.0 change.
- **Measured.** Inventory 829 → **839** (own replica set); Web 696 → **699**; gateway 107/108 (known 5065); architecture 18/18;
  AuthService Application 1063 (unchanged).
  - Red-first: lock 200 → 409, replay version 2 → 1, history row missing.
  - Sabotages (a), (b), (d) and (a-web) red, restored clean. (c) and (e) have no code (BLOCKED).
- **Runtime probe** (own stack, own pm-stub on 14036, built-in browser):
  - A edits the note → NoteUpdated row.
  - A's signed decision (ESIG-2026-000001) → "Notu düzenle" locked, API 409.
  - B rejects in WorkCenter → editable again.
  - B approves QKR-2026-0002 in WorkCenter (ESIG-2026-000003) → Applied, hold Decided.
  - Fleet PIDs unchanged.

**FUB2 — CT acceptance (2026-10-10).** CT re-ran Inventory **839/839** (own ephemeral replica set; 27017 untouched), Web **699/699**,
architecture **18/18** in the agent worktree (MVP-1 still at the base — no merge needed). CT sabotage (own): the request-fingerprint
comparison of `updateHold` replay forced to "equal" → `The_same_key_answers_the_first_result_even_after_a_later_edit_and_a_different_body_is_400`
red, restored, porcelain clean (the CT's first run used a `~Quarantine` filter that misses `Api/HoldNoteLockTests` — re-run with the right
filter). **QUARANTINE 1.2.0 and ESIGN 1.0.1 FROZEN; PRODUCT-MASTER files at draft.11 (`00a4d4268`).** FU-0175-ITEM-01, FU-0175-ITEM-03,
O-ITEM-4, FU-0022-CT-01 CLOSED. Fast-forwarded into the MVP-1 branch. The two BLOCKED items:
- **FU-0175-ITEM-02 — not changed (CT):** `auth.users.lookup` also opens the tenant user search and account verification, broader than
  display names, and the default-role template reaches existing tenants (below). It is NOT added to Viewer. Owner grants it manually to
  the QA roles that need names; a narrow display-name key is **FU-0018-NAMES-01** (AuthService owner, post-MVP-1).
- **Finding (security, owner decision pending) — FU-0022-SEED-02:** `RoleProvisioningService.EnsureDefaultRolesAsync` re-applies the
  default-role template (adds missing keys) on `tenant-admin-invited`, `tenant-activated` and at every AuthService start for the seed
  tenant. CT-D10's "new tenants only" therefore does not hold: the E3 / E5B e-signature signer keys reach existing tenants at those events.
- **O-ITEM-3 → FU-0173-UI-USER-01 (post-G1):** the movement rows carry no user (QUARANTINE `MovementLink` has a name field only, INVENTORY
  `TransactionItem` none) — needs an additive contract field; the audit trail already records the user.

**ROLE1 — the FUB2 finding FU-0022-SEED-02 is closed (WP-0018-ROLE1, 2026-10-10; agent result, pending CT).**
- The default role template now reaches a tenant's roles only when the role is created: at first set-up, or when a tenant lacks that
  default role.
- `tenant-admin-invited`, `tenant-activated` and AuthService start-ups no longer add keys to existing roles. A change to the template
  therefore reaches **new** tenants only.
- **Consequence for FU-0175-ITEM-02** (`auth.users.lookup` on Viewer, FUB2 §2.1): option (A) "new tenants only" is now what any
  template change does by itself.
- Caveat: `auth.users.lookup` is also one of the four Admin self-service keys that the startup reconciler re-adds to every tenant's
  Admin role (O-ROLE1-1, not changed).
- **Measured.**
  - AuthService Application 1063 → **1072** (+9); Web 699/699; architecture 18/18. Warnings 6 / 21, unchanged.
  - 8 red-first cases on the base, run in a separate base worktree.
  - Sabotages (a) invited-completes / (b) activated-completes / (c) seed-completes / (d) always-new → 4 / 3 / 1 / 6 red.
  - Runtime probe (own AuthService + own mongod): a removed Viewer key does NOT come back through invitation, re-activation or
    restart; the invitee is assigned Admin; 5 audit rows in total, one per created role.
- **Evidence.** `docs/records/audits/2026-10/mod-0018-role1-evidence-2026-10-10.md`, `-scripts-2026-10-10.md`.

**SYNC1 — the MVP-1 lane is merged with `origin/main` (WP-MVP1-SYNC1, 2026-10-12; agent result, pending CT).**
- **Owner decision B (2026-10-12) — the FX acceptance item is NARROWED by owner decision: on old open tasks the starter can
  approve their own workflow (a known gap the owner accepted).** — *"Sahip kararıyla FX kabul maddesi daraltıldı: eski açık
  görevlerde başlatan kendi onayını verebilir (bilinen açık, sahip kabul etti)."* An instance from before `StartedByUserId` keeps main's old
  comparison (main test `An_instance_from_before_the_field_keeps_the_old_comparison` → 200). Our Start-log fallback and its
  test were removed. New instances: SoD by user id on both sides, unchanged.
- One actor binding (`WorkflowActorBinding`, ours); main's `TryBindActor` removed — every input main accepted is accepted.
  One starter check (`IsStarter`, main's). `StartedByUserId` is main's `Guid?`, read tolerantly (text or Guid; no migration).
- **For this module:** MOD-0175 still removes the decider from the approver candidates (runtime: decider = only candidate →
  409 `QUARANTINE_APPROVERS_NOT_CONFIGURED`), so a quarantine decision never reaches the Workflow-level SoD; both guards hold.
  WorkCenter approval of a quarantine decision: main's note window, then our e-signature dialog; unsigned 409
  `ESIGN_SIGNATURE_REQUIRED`, signed 200 → decision Applied (runtime probe).
- **Port (OD-P1):** InventoryService 5066 → **5067**; `/api/quarantine` and the other three route families follow.
- **Evidence.** `docs/records/audits/2026-10/mvp1-sync1-evidence-2026-10-12.md`, `-scripts-2026-10-12.md`.

**SYNC1 — CT acceptance (2026-10-12).** `feature/mvp1-sync1` = MVP-1 branch + `origin/main` (`a814bad0f`); `HEAD..origin/main` = 0 at
acceptance (fresh fetch). The CT re-measured the merged tree: Platform filtered (ESign + Workflow + WorkAggregation + WorkCenter +
ModuleRegistration + ModulePages) **699/699** with main's test environment (`DITEN_TEST_MONGOD` + `DITEN_PLATFORM_TEST_MONGO_URI` on an own
replica set with `enableTestCommands=1`; without the URI the same 9 Mongo tests fail at 1 ms — environment), Inventory **839/839**, Web
**1429/1429**, gateway 110/111 (the known 5064 / 5065 port red, FU-INT1-01), architecture 69/77 — the SAME 8 reds measured on `origin/main`
itself (DocsPathGuard × 5, MongoTestDatabaseGuard, AuditTrailStandard × 2). Ports: 8 MVP-1 routes on 5067, main's 2 `/api/shipment-bundle`
routes on 5066. CT sabotage (own): the WorkCenter approval dispatcher stops passing `payload.SignatureId` → 2 red in
`WorkflowESignatureGateTests`, restored, porcelain clean. Accepted: owner decision B (old instances keep main's starter comparison), one
actor binding (ours), tolerant `StartedByUserId` map, `SchemaProfile.ESignature = 13`, OD-P1 Inventory 5067, the Windows guard in main's
`DisposableStandaloneMongo` (c7314b856). Fast-forwarded into the MVP-1 branch. Follow-ups: **FU-SYNC1-AUDIT** (main's audit-trail standard
now also counts 33 MVP-1 write commands: Inventory 27, Auth 3, Platform 3), **FU-SYNC1-ENV** (main's tests need `DITEN_PLATFORM_TEST_MONGO_URI`,
`AUTH_COMPOSITION_GUARD_MONGO_URI`, `DevSeeds:MockUsers` for seeded users — every future prompt states them).

### FIX-LIVE1 — Karantina Ayarları sample location / lot picker (WP-MVP1-FIX-LIVE1, 2026-10-14, agent result, pending CT) — branch `fix/mvp1-live1` from `acff1f7be`, commit `6c9ac921b`, local only. Evidence `docs/records/audits/2026-10/mvp1-fix-live1-evidence-2026-10-14.md`.

- **Fix.**
  - The sample warehouse / location options had value null (they read `id`) and now use `LocationOptions` (`locationId`).
  - Found while measuring: the lot picker read `id`, but TRACE `LotRef.lotId` (trace.openapi.yaml:291) → fixed to `lotId`.
- **Tests and sabotage.** (b) ×3; sabotage s2 → red.
- **FU-0175-UI-MANUALHOLD-01 (not done).** There is no UI button that quarantines arbitrary stock; `quarantineStock` exists only in
  the retest queue (Retest.cshtml:14, retest.js:4).

### SEC-PROXY1 — `/Quarantine/api/{**everything}` path boundary (WP-MVP1-SEC-PROXY1, 2026-10-14, agent result, pending CT) — branch `fix/mvp1-sec-proxy1` from `9362c0402`, commits `717092d0b` (fix) + `655103dca` (tests), local only. Evidence `docs/records/audits/2026-10/mvp1-sec-proxy1-evidence-2026-10-14.md`.

- **Finding.** Same pattern as MOD-0176: `/Quarantine/api/..%5Clocations` reached `/api/locations`. `QuarantineController.ProxyAsync` now builds
  the path with `ProxyPath.TryBuild` (1 line). All 20 contract paths of `quarantine.openapi.yaml` + `lots/{lotId}/quality-status` still forwarded.
- **Helper.** `frontend/Diten.Web/Services/Proxy/ProxyPath.cs` `TryBuild(prefix, everything, out path)`: bounded repeated percent-decode (5),
  refuses `.`/`..` segments, `\`, `%2F`/`%5C`/`%2E`, control chars, `//`/scheme/`@`, anything outside `A-Z a-z 0-9 - _ ~ . /`, and a final
  `Uri` prefix check → 400 `INVALID_PROXY_PATH` (`details.reason`), nothing sent to the gateway. Query string forwarded unchanged.
- **Tests.** `Diten.Web.Tests/Security/**`: 528 attack rows (6 proxies, CT handoff 19 + 29) → 400 and no gateway call; 154 legitimate rows read
  from the contracts / Platform routes → forwarded under the prefix; helper units; architecture guard over every `{**x}` action. Web 1468 → **2199 / 2199**,
  architecture 69 / 77 (same 8). Sabotages s1–s4 red, restored green.
- **Probe.** Own Web 5104 (real Kestrel) → gateway copy 5105 → fake downstream 5106, no signed token: BEFORE the `..%5C` request reached
  `/api/locations`; AFTER 400 `backslash` and the downstream saw only the legitimate request.

### FIX-QUI1 — Bloke Stok Web düzeltmeleri (WP-MVP1-FIX-QUI1, 2026-10-14, agent result, pending CT) — dal `fix/mvp1-qui1` (`1f904aa83`), commit'ler `6ff3c638b` (fix) + `4a08ad452` (test), yalnız yerel. Kanıt `docs/records/audits/2026-10/mvp1-fix-qui1-evidence-2026-10-14.md`.

- **(1) Blokeyi kaldır penceresi.** Gövde `subtext` yerine pencerenin işaretleme kapısı `subtextHtml` ile veriliyor (sabit şablon, konu `textContent` ile); ham `<div class=…>` görünmüyor. Aynı ailede 6 çift kaçış (`subtext: esc(…)`: İncelemeyi tamamla, Yeniden uygula, Geri çek, İncelemeye al, Yeniden test, Şablon) düzeltildi. Quarantine dışı aynı hata: `Traceability/Lots/status.js:86` → FU-QUI1-LOTSTATUS.
- **(2) Stoğu bloke et.** Satır seçimi yazılan miktarı değiştirmiyor (boşsa satır miktarı öneri); "Kullanılabilir: 200 EA" ipucu; satır miktarını aşan miktar alan hatasıyla engelleniyor, istek gitmiyor.
- **(3) Liste.** `list/blocked` varsayılan `status=Open`; Durum filtresi Açık / Kapandı / Numune saklandı / Tümü; Open dışı kayıtta durum etiketi, işlem yok; sekme rozeti yalnız açık kayıtlar. 6 yeni anahtar 7 dilde.
- **Sonuç.** Web 2272 → **2280 / 2280**; build 0 hata, 0 yeni uyarı. Sabotaj s1–s4 kırmızı, özgün baytlar geri yazıldı, yeşil. Önizleme kendi portunda (5011 + sahte gateway 5112) HTTP ile doğrulandı; ekran görüntüsü alınamadı (kanıt §7).
- **FIX-QUI1 CT-ACCEPTED (2026-10-14).** Read: unblock body via `subtextHtml` from the constant template with `textContent` values (no XSS path); the global confirm escapes `subtext` itself (`_GlobalConfirmation.cshtml:260`), so dropping the six `esc(...)` wrappers is correct, not a hole. CT trial merge on the MVP-1 tip `9729409e0`: Web **2280/2280**. **CT sabotage:** `retest.js:44` back to `subtext: esc(…` (and, by accident, an unbalanced parenthesis) → all 40 Quarantine Web tests stay GREEN — the six double-escape fixes are untested and `retest.js` is loaded by no test (a syntax error passes) → **FU-QUI1-ESC-TEST** (a guard: no `subtext: esc(` in Quarantine JS + every Quarantine page script parses). The three requested fixes are each pinned (agent s1–s4). D-1 (one line in the shared vitest list) accepted; D-3 accepted; FU-QUI1-LOTSTATUS (`Traceability/Lots/status.js:86` same outerHTML bug). Merged `--no-ff` into MVP-1; live after the owner Web restart (approval).

**WP-MVP1-FU-CLEAN1 — agent result, pending CT (2026-10-19).** Kanıt `docs/records/audits/2026-10/mvp1-fu-clean1-evidence-2026-10-19.md`.
- **`3edb89a65` FU-QUI1-ESC-TEST + FU-QUI1-LOTSTATUS bekçisi (kapandı):** `web-scripts-guard.test.mjs` (köprü `WebScriptsGuardNodeTests`) — her sayfa script'i ayrışır (742 klasik + 5 modül, paket yok), MVP-1 modüllerinde onay METİN seçenekleri kaçışsız ve işaretlemesiz, gövdeler `subtextHtml`.
- **`9be3f5767` FU-0175-UI-THEME-01 (kapandı):** pasif sekme `var(--bs-paper-bg)` (koyu temada #2b2c40, beyaz değil — önizlemede ölçüldü); liste filtrelerinde `data-placeholder`.
- **`2e11996bc` + `909918b79` FU-0175-UI-MANUALHOLD-01 (kapandı):** kuyrukta "Stoğu karantinaya al" yalnız `quarantine.holds.create` ile (UAS-001); quarantineStock mode Manual tek kova; miktar ≤ eldeki; onay gövdesi `subtextHtml`, gerekçe ≥ 10 zorunlu; UXH-001 yardımı 7 dilde; 375 px taşma yok. Önizleme bulgusu: kuyruk sayfasında antiforgery belirteci yoktu (POST 400) → modal partial'ında; önizlemede KRN-2026-0004 açıldı.
- **`fff2bfe0f` FU-WEB-CONTRACT-FAKES:** `QuarantinePeopleTests` sahtesi bağlandı — tutma ayrıntısı `QH-…` numarası ve eksik 18 üye, ayarlar `{}` sözleşmeye aykırıydı → tam HoldDetail (`KRN-2026-0001`) ve tam Settings.
- **Paylaşılan kişi seçici:** `QuarantinePeople.SearchPath / Options` (MOD-0177 plan sorumlusu için eklendi; AuthService lookup, kiracı token'dan).

**WP-MVP1-NAMES1 — hareket izinde kaydeden (agent result, pending CT, 2026-10-19).** Kanıt `docs/records/audits/2026-10/mvp1-names1-evidence-2026-10-19.md`.
- **`72a36e273`:** tutma ve bloke ayrıntısının hareket izi `MovementLink.byUserName` (var olan isteğe bağlı QUARANTINE alanı; bugüne dek hiç dolmuyordu) = kayıt anındaki ad (merkezi denetim kuyruğu, aynı correlation + kişi + şirket), iz başına tek okuma. QUARANTINE sözleşmesi değişmedi.
- **`f1f2d990a` Web:** ad yoksa hücre 7 dilde "Bilinmeyen kullanıcı" (`Quarantine.Details.Moves.UnknownUser`). O-ITEM-3 / FU-0173-UI-USER-01 kapandı (MOD-0173-FU01 notu).

**WP-MVP1-FU-CLEAN2 — kişi kuralı ve yardım paneli (agent result, pending CT, 2026-10-19).** Dal `feature/sce/wp-mvp1-fu-clean2` (MVP-1 ucu `60f5d2b2f`, SYNC3), yerel, push yok. Kanıt `docs/records/audits/2026-10/mvp1-fu-clean2-evidence-2026-10-19.md`.
- **Sahip kararı D-KAR-1 (2026-10-19) → A:** Karantina'da bilinmeyen kişi 7 dilde "Bilinmeyen kullanıcı"; kimlik yalnız `title`'da; kısa kimlik gösterimi (FU-0175-Q5-02) kaldırıldı; tüm ekranlarda tek kural — `QuarantineUi.person / personHtml`, ortak `StockMovementPeople` kuralının (people.js) üstünde, kopya yok.
- Hareket izi `MovementLink.byUserName` artık saklı adı önce okur (FU-0173-MOV-ACTORNAME); karantina giriş süpürücüsünün kaydettiği hareket girişin kaydedeninin adını taşır.
- Yardım paneli: 8 sayfanın metni (7 dil), her görünüm kendi kodunu söyler. "Stoğu karantinaya al" modal yardımı (CLEAN1 g) modalda kaldı.
- **Sayılar:** Web 3780 / 3782 → 3929 / 3931 (yalnız 2 SafetyStock kırmızısı, main'den), Inventory 1302 + 4 → 1310 + 4, mimari aynı 7, gateway 298 / 298, yeni-denetimsiz 0; 7 / 7 sabotaj.
**DCP-002 (CT, 2026-10-20).** The verifier ran with Python 3.12.10 + openpyxl 3.1.5: `verify_module_id.py . --check-id MOD-0175 --name "Quarantine & Blocked Stock"` → OK (proven against Blueprint/registry) → **W-0175-01 closed**.


**WP-MVP1-FIX-LIVE3 — liste ve sekme metinleri (agent result, pending CT, 2026-10-20).** Dal `feature/sce/wp-mvp1-fix-live3` (MVP-1 ucu `03c697e1a`), yerel, push yok. Kanıt `docs/records/audits/2026-10/mvp1-fix-live3-evidence-2026-10-20.md`.
- Kuyruk, Bloke, Yeniden test ve Ayarlar listeleri ortak liste sözcüklerini 7 dilde alır (arama ipucu artık "Search..." değil); `_Header` sekmeleri `text-none`. Karantina gün hesabı (yeniden test / imha tarihi) UTC — MOD-0176 ASSUMPTION-0176-05 ile aynı çizgi, değişmedi.
- **Sayılar:** Web 3929 / 3931 → 3962 / 3964 (yalnız 2 SafetyStock kırmızısı), Inventory 1310 + 4 → 1311 + 4, mimari aynı 7, gateway 298 / 298, yeni-denetimsiz 0; 6 / 6 sabotaj.

**WP-MVP1-FIX-LIVE3 (ek) — karar uçlarının anahtarı öznitelikte (agent result, pending CT, 2026-10-20).** Dal `feature/sce/wp-mvp1-fix-live3` (ek maddeler e / f), yerel, push yok. Kanıt `docs/records/audits/2026-10/mvp1-fix-live3-evidence-2026-10-20.md` (§7).
- `createDecision` ve `submitDecision` `[HasAnyPermission(decisions.create, blocked.dispose, blocked.unblock)]` taşır; gövdedeki "üçünden biri" kontrolü kalktı. `createDecision`'da hangi anahtarın gerektiği kararın türüne bağlı (§4.6) — adıyla istisna (`AuthorizationGuardTests.BodyKeyedActions`). Davranış aynı: her anahtar geçer, anahtarsız 403 (aynı gövde), anonim 401.

**WP-MVP1-FIX-LIVE4 — FU-0175-BLOCK-QTY kapandı, FU-0175-HELP-CHIPS ölçüldü (agent result, pending CT, 2026-10-20).** Dal `feature/sce/wp-mvp1-fix-live4` (MVP-1 ucu `c654429ca`), yerel, push yok. Kanıt `docs/records/audits/2026-10/mvp1-fix-live4-evidence-2026-10-20.md`.
- "Stoğu bloke et": satır seçimi miktarı yazmaz (alan boş kalır, "Kullanılabilir: …" altında); "Tümü" (7 dil) tam miktarı yalnız tıklanınca yazar. "Stoğu karantinaya al" doldurmuyordu; karar penceresinin kalan hesabı satır ön doldurması değil.
- Yardım etiketleri: sunucu çizimi tırnaklı adları gerçek metin düğümü olarak yazıyor, CSS gizlemiyor; erişilebilirlik ağacında düşen metin aracın rolsüz `span` okuması. Ortak bileşen — DUR-SOR (A: değişmez / B: `PageHelpCatalog.Labelled`'de anlamlı öğe).
- **Sayılar:** Web 3962 / 3964 → 3967 / 3969 (yalnız 2 SafetyStock kırmızısı), Inventory 1335 + 4 (servis değişmedi), mimari aynı 7, gateway 298 / 298, yeni-denetimsiz 0; 4 / 4 sabotaj.


## 20. Follow-up Items

- **FU-0175-WF-SOD (MOD-0023):** store `StartedByUserId` and compare user ids in SoD; add an integration test that starts
  an instance through the API (not by setting `StartedBy`). **Prerequisite for KQ-C.**
- **FU-0175-WF-KEYS (MOD-0023 / MOD-0018):** Tenant scope for the runtime keys `instances.start`, `instances.view`,
  `tasks.approve/reject/cancel`. Scope is route-derived today.
- FU-0175-WF-REDRIVE (MOD-0023): dead-lettered outbox events are never re-sent (`OutboxEvent.cs:129-136`).
- FU-0175-BRD: move block reasons to a MOD-0048-FU01 business reference-data set.
- FU-0175-PRODUCT-TARGET: product-level wait target (mockup).
- FU-0175-SVC-DOC: `domain-config.md:62-63` still names SupplyChainService 5061 for 0173…0177 (OD-5 says 5066).
- FU-0175-REPORT: report `:229` "QUARANTINE-BUNDLE ✓" and DCP-009 `:69` "8 OpenAPI frozen" overstate. Correct them after
  this proposal is frozen.
- FU-0175-STATUS: `module-implementation-status.md:105` says MOD-0023 "not built (0 %)", but MOD-0023 is live (CRM).
- FU-0175-EVIDENCE: Blueprint pages "Release Approval Trace" / "Evidence Pack" (`Blueprint_Data` row 176) — PDF evidence
  export of a hold with its decisions and signature verification (after MOD-0022 E2 export).
- FU-0175-0142: MOD-0142 GRN posts into QUARANTINE/QI → holds appear (no 0175 change; integration test when 0142 lands).
- **FU-0175-Q4-SIGN** (CT decision ASSUMPTION-0175-Q4-01, WP-0175-Q4): KQ-B signatures on "Karantinaya Al" / "Stoğu bloke et"
  (O-Q3-2). quarantineStock / blockStock still refuse `signatureId` (`StockActionHandlers.cs:66-70`); verify it like the decider
  signature (policy of `quarantine.hold.create` / `quarantine.hold.block`, subject per request) in a later slice.
- ~~FU-0175-Q4-RETEST-EDIT (O-Q4-1)~~ **CLOSED by WP-0175-Q4R (CT-Q17)**: a retest date moved forward through TRACE PATCH frees a retest-held lot without a quality
  decision — route the extension of a due lot through 0175 (MOD-0174 `UpdateLotHandler`), owner decision.

## 21. UI input — approved mockup

### 21.1 How it was read

- File: "C:\Users\Ali\Downloads\Karantina Bloke Stok (offline).html".
- Method: Node, no Python. Parse the `__bundler/template` JSON, take the longest `<script type="text/x-dc">` (346 lines of
  app logic, cited as `kar.app.js:<line>`), and read the markup text.
- `data-props`: `eSign` (default true), `fourEyes` (true), `companyTarget` (10 days, 1–60), `activeUser`.

### 21.2 Screens → pack

| Mockup | Pack |
|---|---|
| Karantina Kuyruğu (tab) | QUARANTINEQUEUE |
| Kayıt detayı (page `detail`) | QUARANTINEDETAILS |
| Karar ver panel (2 steps) | QUARANTINEDECIDE |
| Bloke Stok (tab) + Blokeyi Kaldır modal | BLOCKEDSTOCK |
| Disposition kararı panel (2 steps) | BLOCKEDDISPOSITION |
| Yeniden Test (tab) | RETESTQUEUE |
| Lot Detayı placeholder | link to MOD-0174 `/Lots/Details` |

### 21.3 Differences

| # | Mockup | Pack | Why |
|---|---|---|---|
| 1 | "İmzala ve kaydet" changes stock immediately (`commitDecide`, `:102-123`) | "İmzala ve gönder": a workflow instance starts; stock moves only after approval; new state "Onay bekliyor" | KQ-F, DEC-INV-08 |
| 2 | four eyes = record creator cannot release (`:218,232`) | decider ≠ approver (Workflow SoD + 0175 apply check) | KQ-C |
| 3 | "Blokeyi Kaldır" unblocks immediately (`:310-314`) | Unblock decision → workflow → apply | KQ-F (no release without workflow) |
| 4 | status "Numune / Referans" (`:14,45`) | stays BLOCKED at the sample location; "Numune" is a decision kind / blocked-record state | KQ-E |
| 5 | wait target per product, else company (`:138`) | company setting only | KQ-D; CT-Q14 |
| 6 | meanings "Onaylıyorum / İnceledim" in the panel (`:213`) | MOD-0022 dialog with tenant meanings + visual signature | KQ-G |
| 7 | decision / disposition as side panels | dedicated pages (compact rule, > 8 fields) with the same two-step layout | AGENTS.md §6 golden reference |
| 8 | no way to mark a block as reviewed | "İncelemeyi tamamla" action | needed for the "inceleme tamamlandı" rule (`:171`) |
| 9 | "Elle bloke" origin exists, no button | "Stoğu bloke et" toolbar action | the origin needs an action |
| 10 | no settings screen; no workflow/apply states | Settings page; "İş akışı" box; Durumu yenile / Geri çek / Yeniden uygula | KQ-C/D/E, KQ-F operations |
| 11 | release of a retest lot needs nothing more | "Yeni yeniden test tarihi" required | KQ-H (otherwise the release is refused as retest-due) |
| 12 | Tweaks / mockup notes / user switcher | not built | demo only |
