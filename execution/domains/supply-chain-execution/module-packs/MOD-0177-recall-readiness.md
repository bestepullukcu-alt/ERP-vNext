---
id: MOD-0177
name: Recall Readiness
domain: supply-chain-execution
service: Diten.InventoryService (5067, in-process with the MOD-0173 ledger, MOD-0174 lots / genealogy, MOD-0175 quarantine and MOD-0176 expiry hooks — domain-config OD-5 / OD-P1) + frontend/Diten.Web
shell: tenant
golden_reference: compact
data_mode: server
screens:
  - module: RecallDashboard
    data_mode: server
  - module: RecallTrace
    data_mode: server
  - module: RecallCases
    data_mode: server
  - module: RecallCaseDetails
    data_mode: server
  - module: RecallDrills
    data_mode: server
  - module: RecallEvidence
    data_mode: server
  - module: RecallSettings
    data_mode: server
entity_base: EntityBase
status: ready-for-dev
status_note: "ready-for-dev — WP-0177-P0 CT-ACCEPTED 2026-10-13 (CT-R1…CT-R18 decided, §19.7). Was: draft — WP-0177-P0 (CT-MVP1-0177-P0 v1), 2026-10-13. Owner decisions KR-A…KR-H (2026-10-13) recorded in §19.1 as DECISIONS. Open CT decisions CT-R1…CT-R18 (§19.5). RECALL-BUNDLE v1 proposal: docs/analysis/contracts/drafts/recall-bundle-v1-proposal.md (no RECALL contract exists although the report marks it ✓ — K12). Additive needs on other owners (INVENTORY 1.4.0, QUARANTINE 1.3.0, SHIPMENT-BUNDLE request to MVP-6) are proposals in §8.11, not edits. DCP-002 verifier not run (no Python — waiver W-0177-01, W-0173-01 pattern)."
owner: supply-chain-execution / mvp-1 control-tower
branch: feature/sce/mod-0177-recall-readiness
started: 2026-10-13
target: 2026-11-28
form_field_count: 9
dependencies: [MOD-0173, MOD-0174, MOD-0175, MOD-0176, MOD-0022, MOD-0290, INVENTORY-BUNDLE-v1.2.1 (+1.3.0 on the 0176 F2 branch), TRACE-v1.3.0 (+1.4.0 on the 0176 F2 branch), QUARANTINE-v1.2.0, ESIGN-BUNDLE-v1.0.1, FEFO-BUNDLE-v1.0.0, SHIPMENT-BUNDLE-v3.2.0 (MVP-6, read only), DEV-0001]
---

# MOD-0177 — Recall Readiness

> **Identity (DCP-002):** `MOD-0177 · Recall Readiness` is a Blueprint module, registered `reserved / planned`
> (`execution/registries/module-id-registry.md:278`: "Recall Readiness | recall-readiness | Module | reserved / planned | … |
> Blueprint W-4 (Inventory & Traceability). DCP-002 gate OK 2026-09-11. DCP-009. Support."). Roster:
> `docs/analysis/inventory-capability-scope-and-dependency-report.md:94` ("Recall Readiness | W-4 | Support | recall cases |
> YOK"). DCP-009 lists it in `canonical_modules` (`DCP-009-supply-chain-inventory.md:16`), on InventoryService 5067 (`:42`),
> in the MVP-1 core (`:57`) and in the chain `0174 → {0175 ∥ 0176 ∥ 0177}` (`:63`). **Not re-measured:** no Python on the MVP-1
> machine → waiver **W-0177-01** (§18), W-0173-01 pattern. No registry row is changed by this pack.
>
> **Authority:** this pack > `execution/domains/supply-chain-execution/domain-config.md` > `AGENTS.md` > `.antigravity/`.
> Owner decisions **KR-A…KR-H (2026-10-13)** are binding (§19.1). Where the mockup "Geri Çağırma / Recall" disagrees, KR-*
> wins (§21.3). The mockup's decisions panel defaults (`L:52`: A=modul, B=oner, C=block, D=tam, E=defter, F=kayit, G=ayar,
> H=mod) are **not** binding.
>
> **Contract (K12):** the report marks RECALL-BUNDLE "✓" (`…dependency-report.md:232`: "Recall trace | MOD-0177 | evidence |
> RECALL-BUNDLE ✓ | —"), but **no recall contract exists** in `docs/analysis/contracts/` (no root file, no draft, no README
> row; "RECALL" does not occur anywhere under `docs/analysis/contracts/`). This WP writes the proposal
> `docs/analysis/contracts/drafts/recall-bundle-v1-proposal.md`; it becomes `recall.openapi.yaml` (FROZEN) only after CT
> acceptance (R1, single writer). Consumed frozen contracts are **not changed** by this WP: INVENTORY-BUNDLE 1.2.1
> (`inventory-bundle.openapi.yaml:9`), TRACE 1.3.0 (`trace.openapi.yaml:8`), QUARANTINE 1.2.0 (`quarantine.openapi.yaml:12`),
> ESIGN-BUNDLE 1.0.1 (`esign.openapi.yaml:11`), FEFO-BUNDLE 1.0.0 (`fefo.openapi.yaml:12`), SHIPMENT-BUNDLE 3.2.0
> (`shipment-bundle.openapi.yaml:15`, MVP-6). The 0176 F2 branch (`feature/sce/mod-0176-expiry-fefo`, tip `bc7f520e9`) proposes
> INVENTORY 1.3.0 / TRACE 1.4.0 — not on this base; 0177's INVENTORY need is therefore numbered **1.4.0** (§8.11).
>
> **No BLOCKED condition.**
> - **Trace is possible with existing reads, in-process.** The HTTP `getGenealogy` stops at depth 2 (`trace.openapi.yaml:120`
>   `maximum: 2`; `GetGenealogyHandler.cs:22-38` — one extra hop, no closure), but the repository reads it uses are batched
>   and unbounded (`ILotRepository.ParentLinksAsync` / `ChildLinksAsync`, `LotRepository.cs:151-155,283-291`). 0177 runs in
>   the same process and walks them level by level (§8.2). No TRACE change is needed. Ledger reads by lot exist
>   (`InventoryLedgerRepository.cs:130` `QueryTransactionsAsync`, index `ix_tenant_le_lot` `InventoryIndexConfiguration.cs:39`).
> - **The exit block can sit inside the posting transaction**, next to the 0176 F2 port (same two seams:
>   `MovementMasterDataValidator.cs` pre-check, `InventoryLedgerRepository.cs` in-transaction re-check — §8.5).
> - **Every contract need is additive** (a new 422 code, an optional enum value / optional origin on a hold, new optional
>   filters; §8.11). The SHIPMENT lot field is a request to MVP-6, not a need: the trace works without it (KR-E).
> - **No new stock status and no new movement type.** Auto-quarantine goes through 0175 `quarantineStock`
>   (`STATUS_TRANSFER AVAILABLE→QUARANTINE`, `quarantine.openapi.yaml:54-65`); returns are `CUSTOMER_RETURN` into `QUARANTINE`
>   (existing, MOD-0175 pack `:343`). 0177 posts no movement.
> - The mockup was read (decoded in scratchpad, treated as data — §21.1).

## 1. Module Summary

MOD-0177 is the **recall layer on top of the stock ledger, the lot master / genealogy and the quarantine module**. It keeps
no balance, creates no lot or genealogy link, makes no quarantine decision and writes no shipment or return
(report `:124`: base 0174 + Evidence; `:189` objects `RecallCase · RecallDrillPlan · TraceForwardQuery · TraceBackwardQuery ·
EvidencePack`; `:214` screens "Recall Readiness Dashboard · Evidence Pack Generator · Drill Planner").

What 0177 owns:
- **The trace engine** (KR-D): full multi-level genealogy backward and forward from a lot, then on-hand stock and outbound
  movements of every lot in the closure, then shipments and customers (KR-E). Cycle-safe, with trace gaps as rows and
  non-lot-controlled inputs as "Takip dışı".
- **Recall cases** (`RecallCase`): an in-module state machine with MOD-0022 e-signature (KR-A) — draft scope from a trace,
  include / exclude marks with reason, scope approval with a **frozen, hashed trace snapshot**, active phase, effectiveness
  check, two-step closure.
- **Consequences of an approved scope:** automatic quarantine requests to MOD-0175 for in-scope on-hand stock (KR-B), and
  the **exit block** on in-scope lots inside the posting transaction (KR-C).
- **Records:** customer notification records (KR-F — record only, no e-mail), consumption declarations, return links (KR-H
  — MOD-0186 later, manual `CUSTOMER_RETURN` in MVP-1), physical segregation confirmations.
- **Drills** (`RecallDrillPlan` + runs): a read-only rehearsal with a server timer, steps, result and signature (KR-G).
- **Evidence pack** (7 sections, PDF / CSV, SHA-256 of the file bytes, export log) — produced by the module because MOD-0029
  does not exist (report `:124`, `:570` `[BB]`; FU-0177-EVID-01).
- **Company settings** + history (drill target, drill frequency warning, effectiveness targets by class).

MVP-1 chain: `0290 → 0173 → 0174 → {0175 ∥ 0176 ∥ 0177}` (DCP-009 `:63`). 0177 also needs MOD-0022 (signature API + provider
registry, live on main) and reads MVP-6 shipments when reachable (KR-E).

### Slice plan (each slice = its own Work Package, CT-verified before the next)

| Slice | Scope | Contract | Closes | Acceptance + sabotage |
|---|---|---|---|---|
| **R1 — Case, settings, contract** | `RecallCase` + events (insert-only), state machine §8.7 (signature port **fail-closed** until R4, 0176 F1 pattern), scope items (include / exclude + reason), notifications, consumption declarations, segregation confirmations (records only); `RecallSettings` (+ history, If-Match, reason); counters `RC-YYYY-NNNN` / `RC-YYYY-DNNN`; `getLotRecallStatus`; manifest `RECALL-READINESS` with **API-only keys, no page** (0176 F1 "sayfasız ilk dilim"); freeze `recall.openapi.yaml` v1.0.0. | RECALL-BUNDLE v1 | KR-A (state machine part), KR-F, KR-G (settings), report §4 objects | §16 R1 |
| **INT-RECALL** | Gateway `/api/recall` + `/api/recall/{everything}` → 5067 + route coverage test (integration-agent). | — | external consumers (Platform e-sign provider calls, 0174 badge, 0176 / 0178) | §16 INT |
| **R2 — Trace engine** | `TraceEngine` (BFS, cycle guard, limits), gap rules, "Takip dışı", snapshot canonical form + SHA-256, live re-trace diff ("kapsam dışı bulgu"), shipment reader (KR-E, degrade), `runTrace`, CSV; open case from trace; submit; indexes §8.9. | RECALL-BUNDLE (reads) | KR-D, KR-E | §16 R2 |
| **R3 — Quarantine requests, exit block, return linking** | `IRecallQuarantineRequester` → 0175 (in-process), request records, retry; `IRecallPostingGate` (pre-check + in-transaction re-check) + `recall_lot_blocks`; 422 `LOT_UNDER_RECALL` (INVENTORY 1.4.0, owner step); manual `CUSTOMER_RETURN` link + candidates; QUARANTINE 1.3.0 origin (owner step). **Starts after 0176 F2 is merged** (same two seams). | INVENTORY 1.4.0, QUARANTINE 1.3.0 (§8.11) | KR-B, KR-C, KR-H | §16 R3 |
| **R4 — e-signature** | Signable-action registration (module `recall`), subject provider (`recall.case` `rc-v1`, `recall.drill-run` `dr-v1`, `recall.settings-change` `rs-v1`), pending action + verification on scope approval / effectiveness completion / closure / drill result (direct signature, not E4 — §8.8); four-eyes (CT-R1). | ESIGN-BUNDLE §4.23, §6 (consumer) | KR-A (signature part) | §16 R4 |
| **R5 — Drills** | `RecallDrillPlan`, `RecallDrillRun`, server timer, steps, result + signature, frequency warning; **read-only guarantee** (architecture test). | RECALL-BUNDLE (drills) | KR-G | §16 R5 |
| **R6 — UI 1** | Hazırlık Paneli, İzleme (tree + table + CSV), Vakalar. | — | mockup 1:1 except §21.3 | §16 R6 |
| **R7 — UI 2 + bands** | Vaka Detayı (6 tabs, closure two-step + MOD-0022 dialog), Tatbikat Planlayıcı, Kanıt Paketi (+ export log), Ayarlar (+ history); bands: lot detail badge (0174), quarantine record source (0175), movement form exit-block band (0173); 375 px measurement. | — | mockup 1:1 except §21.3 | §16 R7 |
| **L10n** | 7 languages (en, tr, fr, es, zh, ar, ru) for §11.6 keys, statuses, classes, levels, node kinds, gap kinds, e-sign action names (SevenNames). | — | language parity | §16 L10n |

Order: **R1 → INT → R2 → R3 → R4 → R5 → R6 → R7 → L10n**. R6 may start once R2 is accepted (contract mock earlier). R3's
INVENTORY / QUARANTINE edits are each owner's single-writer step (same InventoryService, separate commit per contract). R3
waits for 0176 F2 + FIX1 to be merged into the MVP-1 branch (it extends the same validator / ledger lines; doing both in
parallel would conflict on `MovementMasterDataValidator.cs` and `InventoryLedgerRepository.cs`).

Dependencies on other modules:
- R3 needs 0176 F2 merged (port pattern, INVENTORY 1.3.0 already applied) — CT-accepted F2 is the base of 1.4.0.
- R4 needs MOD-0022 E2 live (✅ on main) — no E4 dependency (§8.8).
- R2 shipment reads need MVP-6 `GET /shipments/{id}` reachable **and** a token MVP-6 accepts (`legal_entity_id` claim —
  integration plan); without it the trace degrades (KR-E, §8.4). Not a blocker.

## 2. Ownership and Boundaries

### Owns
- Recall cases (`RecallCase`, report `:189`) with scope, events, signatures-by-reference, records.
- Drill plans and runs (`RecallDrillPlan`, report `:189`).
- The forward / backward trace (`TraceForwardQuery` / `TraceBackwardQuery`, report `:189`) **as a computation** over
  0173 / 0174 data — not a store of genealogy.
- The evidence pack (`EvidencePack`, report `:189`) and its export log — until MOD-0029 exists (FU-0177-EVID-01).
- The recall lot blocks (which lots are under an active recall) and the exit-block rule (pure Domain function, called by the
  ledger).
- Recall settings per company + history.

### Does not own
| Thing | Owner | How 0177 touches it |
|---|---|---|
| Stock balance, movements, reservations | MOD-0173 | reads balances / transactions in-process; the exit block runs inside the 0173 posting path; posts nothing |
| Lots, serials, genealogy links | MOD-0174 | reads lots, serials, parent / child links (repository, in-process); never writes a lot or link (`linkGenealogy` is MOD-0194's, `trace.openapi.yaml:126-130`) |
| Quarantine holds and their decisions | MOD-0175 | requests `quarantineStock` (in-process port, KR-B); reads hold status; 0175 decides release / reject / block |
| Expiry / FEFO hooks | MOD-0176 | shares the posting seams; recall gate runs **before** the expiry hook (§8.5) |
| Signatures, meanings, signature policy | MOD-0022 | registers `recall.*` actions, provides subjects, verifies `signatureId`; keeps **no** signature record |
| Shipments, returns, customers | MVP-6 (MOD-0183 / MOD-0186) | reads `GET /shipments/{id}` at read time (KR-E); links returns (KR-H); writes nothing |
| Customer communication (e-mail) | Notification module (later) | none — record only (KR-F, FU-0177-NOTIF-01) |
| Evidence storage / DMS | MOD-0029 / MOD-0030 | none in MVP-1 (FU-0177-EVID-01) |

### Boundary rules
1. 0177 never writes a balance row, a movement, a lot, a genealogy link, a hold, a decision, a shipment, a return or a
   signature.
2. The exit-block rule is called **in-process** by the posting path and only says "allow / refuse"; it never re-routes.
3. KR-B quarantine goes **through MOD-0175's own command** (its checks, its posting, its hold). 0177 records the request and
   reads the result.
4. Every 0177 read and write is tenant + company scoped (`LegalEntityId`, W-0173-02). Genealogy links carry no
   `LegalEntityId` (MOD-0174 pack `:170-173`) — §8.2 rule 6 handles it.
5. Drills are read-only: no drill code path can reach a write port (§8.10, architecture test).

## 3. Owned Objects

| Object (report name) | Pack entity | Collection (proposed, `recall_*` like `expiry_*` O-F1-3) | Notes |
|---|---|---|---|
| `RecallCase` | `RecallCase` | `recall_cases` | Mutable head with `Version`; state machine §8.7. |
| — | `RecallCaseEvent` | `recall_case_events` | Insert-only timeline (mockup "Zaman çizelgesi"). |
| — | `RecallScopeItem` | `recall_scope_items` | One row per scope node (lot, stock line, shipment) with include / exclude + reason, per scope revision. |
| `TraceForwardQuery` / `TraceBackwardQuery` | `RecallTraceSnapshot` | `recall_trace_snapshots` | Insert-only; the trace result at submit / approval (canonical JSON + SHA-256). Later data never changes it. |
| — | `RecallLotBlock` | `recall_lot_blocks` | Active exit blocks (lotId, caseId); read in the posting transaction (§8.5). |
| — | `RecallQuarantineRequest` | `recall_quarantine_requests` | KR-B request per stock bucket: status, 0175 hold ids, error. |
| — | `RecallCustomerNotification` | `recall_notifications` | KR-F record (insert-only; correction = new row). |
| — | `RecallConsumptionDeclaration` | `recall_consumption_declarations` | Customer declares "consumed / used" quantity (insert-only). |
| — | `RecallReturnLink` | `recall_return_links` | KR-H: MOD-0186 return or manual `CUSTOMER_RETURN` transaction ↔ case. |
| — | `RecallSegregationConfirmation` | `recall_segregations` | Warehouse confirms physical separation of a stock line. |
| `RecallDrillPlan` | `RecallDrillPlan` | `recall_drill_plans` | Mutable, `Version`. |
| — | `RecallDrillRun` | `recall_drill_runs` | Timer, steps, result, signature reference. |
| `EvidencePack` | `RecallEvidenceExport` | `recall_evidence_exports` | Export log (at, who, scope, file name, SHA-256 of the file bytes). |
| — | `RecallSettings` + `RecallSettingsHistory` | `recall_settings`, `recall_settings_history` | Company settings, history per changed field. |
| — | number sequences | `recall_counters` | `RC-YYYY-NNNN` (cases), `RC-YYYY-DNNN` (drill runs), per tenant + year. |

## 4. Entity Fields

All entities derive from `EntityBase` (Id, TenantId, CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy, IsDeleted). Every
entity carries `LegalEntityId` (company scope, W-0173-02). Quantities are decimal strings in the **base unit** of their SKU;
quantities of different SKUs / units are never summed (MOD-0174 ASSUMPTION-0174-12, pack `:798`).

### 4.1 `RecallCase`
| Field | Type | Rule |
|---|---|---|
| Number | string | `RC-YYYY-NNNN` |
| Status | enum `Draft | PendingScopeApproval | Active | EffectivenessCheck | Closed | Cancelled` | mockup `L:7` ("Taslak", "Kapsam onayı bekliyor", "Aktif", "Etkinlik kontrolü", "Kapandı", "İptal") |
| Class | int 1–3 | mockup `L:8`; drives effectiveness target (KR-G) |
| Level | enum `Wholesale | Pharmacy | Patient` | mockup "Toptan / Eczane / Hasta" (`L:189-196`) |
| RootLot | `LotId, LotNumber, ItemId, SkuId, SkuLevel` + display snapshot | the lot the case starts from |
| Direction | enum `Forward | Backward | Both` | trace parameter of the case (mockup case uses forward for scope, `L:258`) |
| Reason | string 10–1000 | "Neden" |
| ReasonReference | string ≤ 100 \| null | e.g. "DEV-SUP-2026-031" (deviation / complaint number; free text, no link in MVP-1) |
| OwnerUserId | Guid | opener (QA) |
| ScopeRevision | int | 1 at open; +1 per scope revision (CT-R5) |
| SubmittedSnapshotId / ApprovedSnapshotId | Guid \| null | §8.3 |
| ScopeSubmittedBy / At, ScopeApprovedBy / At, ScopeApprovalSignatureId | | KR-A |
| EffectivenessCompletedBy / At, EffectivenessSignatureId, EffectivenessResult | | `{ shipped, returned, consumed, open, percent, target, met }` per SKU / unit |
| ClosedBy / At, ClosureSummary (10–2000), OpenQuantityAcceptance (10–1000 \| null), ClosureSignatureId | | mockup closure (`L:301`) |
| CancelledBy / At, CancelReason | | only from Draft / PendingScopeApproval |
| PendingAction | `{ action, userId, preparedAtUtc, payloadHash }` \| null | signing handshake (§8.8), 15 min |
| Version | long | If-Match |
| IdempotencyKey | string | dedupe of open |

### 4.2 `RecallCaseEvent`
`CaseId, Seq, Kind (Opened | TraceRun | ScopeMarked | ScopeSubmitted | ScopeReturned | ScopeApproved | QuarantineRequested |
QuarantineRequestFailed | QuarantineRequestResolved | SegregationConfirmed | CustomerNotified | ConsumptionDeclared |
ReturnLinked | EffectivenessStarted | EffectivenessCompleted | Closed | Cancelled | OutOfScopeFinding | ExitRefused), Actor
{userId, name} | system, AtUtc, Text, Refs {…}, SignatureId?`. The mockup timeline icons map to `Kind` (`L:279`).

### 4.3 `RecallScopeItem`
`CaseId, Revision, NodeKey (stable: kind + ids), Kind (Lot | Stock | Shipment | Movement | Customer), LotId?, WarehouseId?,
LocationId?, StockStatus?, ShipmentId? / SourceDocumentId?, Quantity?, BaseUomId?, Included (bool), ExclusionReason (10–1000
when excluded), MarkedBy, MarkedAtUtc`. Only `Stock`, `Shipment` and derived `Lot` nodes can be excluded (mockup `L:259`
`canIncl` for stock / ship; the pack adds lot nodes — excluding a lot removes its subtree, CT-R6). The root lot cannot be
excluded.

### 4.4 `RecallTraceSnapshot`
`CaseId?, DrillRunId?, Revision, Purpose (Submitted | Approved | Drill | Evidence), Parameters {rootLotId, direction,
asOfUtc, limits}, Nodes[] (§8.2 node shape), Edges[], Gaps[], Summary, CanonicalVersion ("rt-v1"), Sha256, ComputedAtUtc,
DurationMs, Truncated (bool)`. Insert-only.

### 4.5 `RecallLotBlock`
`LotId, CaseId, CaseNumber, Active (bool), ActivatedAtUtc, ReleasedAtUtc?, ReleasedReason (Closed | Cancelled |
ScopeRevision)`. Unique `(TenantId, LotId, CaseId)`. Read in the posting transaction by `(TenantId, LotId, Active)`.
Tenant-level by lot (a lot id is tenant-unique); `LegalEntityId` = the case's company, but the block applies to the lot in
**every** company of the tenant (CT-R7).

### 4.6 `RecallQuarantineRequest`
| Field | Type | Rule |
|---|---|---|
| CaseId, Revision, ScopeItemId | | the stock node |
| Bucket | `WarehouseId, LocationId, LotId, ItemId, SkuId, SkuLevel, Quantity, BaseUomId, SerialIds?` | the AVAILABLE on-hand at approval |
| Status | enum `Pending | Held | PartiallyHeld | BlockedByReservation | NothingAvailable | Failed | NotNeeded` | §8.4 |
| NotNeededReason | enum `AlreadyQuarantined | AlreadyBlocked | LotBlocked | LotExpired | Excluded` \| null | KR-B "durumu gösterilir" |
| HoldIds | string[] | 0175 holds created |
| Error | `{ code, message, details }` \| null | e.g. `QUARANTINE_STOCK_RESERVED` + reservation lines (`quarantine.openapi.yaml:914-918`) |
| Attempts | int | |
| IdempotencyKey | string | `recall:{caseId}:{revision}:{nodeKey}` — stable across retries (§8.4) |
| RequestedBy (approver), LastAttemptAtUtc | | |

### 4.7 `RecallCustomerNotification`
`CaseId, CustomerRef (shipToReference or document party text, §8.4), ShipmentIds[], NotifiedOn (date), Channel (Phone |
ExternalEmail | Fax | InPerson | Other), Note (≤ 1000), RecordedBy, RecordedAtUtc, SupersedesId?`. Mockup modal
(`T` "Bilgilendirme kaydı": Tarih, Kanal Telefon / E-posta (harici) / Faks / Yüz yüze, Not; "Yalnız kayıt tutulur; sistem
e-posta göndermez").

### 4.8 `RecallConsumptionDeclaration`
`CaseId, CustomerRef, SkuId, SkuLevel, BaseUomId, Quantity (> 0), DeclaredOn (date), Evidence (≤ 1000, e.g. "müşteri
yazısı …"), RecordedBy, RecordedAtUtc`. Insert-only; a correction is a negative-free new row that supersedes (`SupersedesId`).

### 4.9 `RecallReturnLink`
`CaseId, Source (Mod0186Return | ManualCustomerReturn), ReturnReference (MOD-0186 return id \| null), TransactionId (the
`CUSTOMER_RETURN` ledger row), TransactionNumber, LotId, SkuId, SkuLevel, Quantity, BaseUomId, ToStockStatus, HoldId?,
CustomerRef, LinkedBy, LinkedAtUtc`. Unique `(TenantId, TransactionId)` — a return counts for one case only.

### 4.10 `RecallSegregationConfirmation`
`CaseId, QuarantineRequestId | ScopeItemId, WarehouseId, LocationId, Quantity, Note?, ConfirmedBy, ConfirmedAtUtc`.

### 4.11 `RecallDrillPlan`
`Code (DP-NN), Name, Frequency (Yearly1 | Yearly2 | Quarterly | Custom days), ScopeKind (RandomFinishedGoodLot |
RawMaterialLot | SelectedLot), NextDueOn (date), OwnerUserId, Active, Version`. Mockup `L:45-49`.

### 4.12 `RecallDrillRun`
| Field | Type | Rule |
|---|---|---|
| Number | string | `RC-YYYY-DNNN` |
| PlanId | Guid | |
| Lot | `LotId, LotNumber` | `SelectionMode Auto` = server picks at random from the plan's candidate set (audited); `Manual` = user picks |
| Status | enum `Running | Completed | Cancelled` | |
| StartedAtUtc | server time | "Başlat" |
| Steps | `[{ step: Trace | Scope | CustomerList, completedAtUtc }]` | in order |
| EndedAtUtc | server time | completion of step 3 |
| DurationMinutes | int | `ceil((EndedAtUtc − StartedAtUtc) / 1 min)` |
| TargetHours | int | settings value **at start** |
| Passed | bool | `DurationMinutes ≤ TargetHours × 60` |
| GapCount | int | from the run's trace snapshot |
| SnapshotId | Guid | trace at step 1 |
| ResultNote | string ≤ 2000 | |
| ResultSignatureId | string \| null | KR-G result signature |
| Version | long | |

### 4.13 `RecallEvidenceExport`
`LegalEntityId, Subject (Case caseId | DrillRun runId | Period {from, to}), Format (Pdf | Csv), FileName, Sha256 (of the
produced bytes — mockup `fakeHash` `L:341` not copied, §21.3), SizeBytes, Sections (1–7), ExportedBy, ExportedAtUtc,
DmsRef? (null until MOD-0029)`.

### 4.14 `RecallSettings` / `RecallSettingsHistory`
| Field | Type | Rule |
|---|---|---|
| DrillTargetHours | int 1–72 | default **4** (KR-G) |
| DrillFrequencyWarningDays | int 30–730 | default **180** (KR-G) |
| EffectivenessTargetPercent | map class → decimal 0–100 | default **1: 100, 2: 95, 3: 90** (KR-G) |
| TraceMaxDepth | int 2–50 | default 20 (§8.2 limits; ASSUMPTION-0177-04) |
| TraceMaxNodes | int 100–20000 | default 5000 (ASSUMPTION-0177-04) |
| Version | long | If-Match |

No row stored = defaults (`isDefault: true`). History: `Field, OldValue, NewValue, Reason (10–1000), ChangedBy, ChangedAtUtc,
SettingsVersion, SignatureId?`. **Not a setting:** KR-B mode (always automatic — mockup "Öner / Otomatik iste" toggle not
built) and the signature policy (MOD-0022's policy rows — mockup "E-imza politikası" not built here, §21.3).

## 5. Repo Scope

Allowed (new files only, unless listed as a seam):
- `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Recall/**` (entities, enums, pure rules)
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Recall/**`
- `services/Diten.InventoryService/src/Diten.InventoryService.Persistence/Recall/**`
- `services/Diten.InventoryService/src/Diten.InventoryService.Api/Controllers/Recall/**`, `Api/Recall/**` (DI, manifest provider)
- `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Authorization/RecallPermissions.cs`
- `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Shipments/**` (MVP-6 shipment HTTP reader)
- `services/Diten.InventoryService/tests/**/Recall/**`
- `frontend/Diten.Web/Controllers/RecallController.cs`, `frontend/Diten.Web/Views/Recall/**`, `frontend/Diten.Web/wwwroot/assets/js/Recall/**`, `Resources/**` (7 languages)
- `docs/analysis/contracts/recall.openapi.yaml` (R1, after CT acceptance), README row

**Seams (existing files, minimal, listed exactly; lines measured at `1240a1ce9` unless marked F2):**
| File | Line (measured) | Change | Slice |
|---|---|---|---|
| `…Application/Features/Inventory/Services/MovementMasterDataValidator.cs` | after the lot rules `:197-205` (HEAD) — on F2 **before** the expiry pre-check `:212` | call `IRecallPostingGate.PreCheckAsync` | R3 |
| `…Persistence/Repositories/InventoryLedgerRepository.cs` | in `WithTransactionAsync` `:166` after `ResolvePostingLotAsync` `:176` (HEAD) — on F2 **before** `_expiry.ReCheckAsync` `:185` | call `IRecallPostingGate.ReCheckAsync` with the session; refusal → `LotPostingRefusedException` (`:186-189` F2 pattern) | R3 |
| `…Persistence/DependencyInjection.cs` | next to `:37` (F2 `TryAddScoped<IExpiryPostingPolicy, AllowAll…>`) | default `AllowAllRecallPostingGate` | R3 |
| `…Application/Common/InventoryErrorCodes.cs` | `All` set (F2 `:67-75` + `:69,72,75`) | `LOT_UNDER_RECALL` (with INVENTORY 1.4.0) | R3 |
| `…Application/Features/Quarantine/Handlers/StockActionHandlers.cs` | `QuarantineStockHandler` `:229` / `Handle :248`; hold `Source` `:341`, `SourceDocument` `:344` | accept an optional origin (module / type / id / number) from an in-process caller; `HoldSource.Recall` (QUARANTINE 1.3.0) — **0175 owner step** | R3 |
| `…Domain/Quarantine/Enums/QuarantineEnums.cs` | `:7-16` | `Recall` value (QUARANTINE 1.3.0, CT-R3) | R3 |
| `…Persistence/InventoryIndexConfiguration.cs` | transactions `:28-55` | index `(TenantId, LegalEntityId, SourceModule, SourceDocumentId)` (§8.9) | R2 |
| `services/Diten.InventoryService/src/Diten.InventoryService.Api/Program.cs` | manifest registrations `:42-49`, hosted `:51` | register `RecallReadinessManifestProvider` + `AddRecall()` | R1 |
| `…Api/…/InventoryScopeResolutionMiddleware.cs` (`ScopedBasePaths`) | (0176 O-F1-2 precedent) | add `RecallContract.BasePath` | R1 |
| `frontend/Diten.Web/Views/Traceability/Lots/Details*` (route `LotsController.cs:55`) | lot header | "Açık geri çağırma: RC-…" badge + "İzle" link | R7 |
| `frontend/Diten.Web/Views/Quarantine/Details*` (route `QuarantineController.cs:72`) | source block | "Kaynak: Geri çağırma RC-…" | R7 |
| `frontend/Diten.Web/Views/Inventory/StockMovements/_Form.cshtml` + `form.js` (route `StockMovementsController.cs:57`) | lot block (0176 F5 bands) | exit-block band | R7 |

## 6. Protected Paths

Not touched by any 0177 slice: frozen contracts other than by their owner's additive single-writer step
(`inventory-bundle.openapi.yaml`, `trace.openapi.yaml`, `quarantine.openapi.yaml`, `esign.openapi.yaml`, `fefo.openapi.yaml`,
`shipment-bundle.openapi.yaml` and the MVP-6 semantics files — never); `ocelot*.json` (integration-agent only); registry /
DCP / domain-config / report (proposals only, §20); MOD-0175 decision / apply engine; MOD-0174 genealogy writer
(`LinkGenealogyHandler.cs`); MOD-0176 expiry rules (`FefoRules.cs`, `ExpiryPostingPolicy.cs`); the existing 0174 trace screen
`LotTraceController.cs` (read as a model); Platform e-signature code; `AccountKindAcceptanceHost.Tests` (not run).

## 7. Dependencies

| Dependency | Kind | State (measured) | Used for |
|---|---|---|---|
| MOD-0173 ledger | HARD (in-process) | live, INVENTORY 1.2.1 | transactions by lot (`InventoryLedgerRepository.cs:130`), balances by lot (`getBalance lotId` `inventory-bundle.openapi.yaml:150`), posting seams |
| MOD-0174 lots / genealogy | HARD base (report `:124`, `:570` `[INT]`) | live, TRACE 1.3.0 | links (`LotRepository.cs:151-155`), lot detail + source (`trace.openapi.yaml:187-191,343-349`), serials (`:199-214`), migration links (`:259-267`) |
| MOD-0175 quarantine | HARD for KR-B | live (Q1–Q5), QUARANTINE 1.2.0 | `quarantineStock` (`quarantine.openapi.yaml:54-65`), holds by source (`:67-69`), hold read (`:77-79`) |
| MOD-0176 expiry hooks | shares seams | F1 + INT on MVP-1 branch; F2 on its branch (CT-HOLD, FIX1 running) | port pattern `IExpiryPostingPolicy.cs:53-60` (F2), order recall → expiry (§8.5) |
| MOD-0022 e-signature | HARD for R4 | live on main (ESIGN 1.0.1 FROZEN) | `signSubject` / `verifySignature` (`esign.openapi.yaml:109-111,174-176`), registration (`:552-554`), provider contract (`:1160-1161`) |
| MOD-0023 Workflow | **not used** | — | KR-A (§8.7) |
| MOD-0290 product master | HARD data | live | `getSku` `LotControlled` / `SerialControlled` (validator `MovementMasterDataValidator.cs:144,159`) — "Takip dışı" and gap rules |
| MVP-6 MOD-0183 shipments | soft read (KR-E) | contract 3.2.0, service not on this machine | `getShipment` (`shipment-bundle.openapi.yaml:119-122`) |
| MVP-6 MOD-0186 returns | soft (KR-H) | contract only | return link when available; RECALL reason not defined (§8.11.4) |
| MOD-0029 / 0030 Evidence | not present | — | FU-0177-EVID-01 |

## 8. Runtime Constraints

### 8.1 Measured baseline (what exists today)

- **Genealogy is a link table without closure.** `LotGenealogyLink` (`LotGenealogyLink.cs:9`: ParentLotId, ChildLotId,
  TransformationReference, SourceModule, QuantityConsumed (string), UomId), collection `trace_genealogy_links`
  (`TraceCollections.cs:17`), indexes `ux_trace_genealogy_link` (Tenant, Parent, Child, TransformationReference) and
  `ix_trace_genealogy_child` (Tenant, Child) (`TraceIndexConfiguration.cs:92-109`). **No `LegalEntityId`** on a link.
  Writer: `linkGenealogy` (MOD-0194 later, `trace.genealogy.link`), cycle check walks ≤ 5000 ancestors
  (`LinkGenealogyHandler.cs:26,79` → 422 `GENEALOGY_CYCLE`). Cycles are refused on write — but 0177 still guards (data
  written before the check, migrations).
- **HTTP genealogy read stops at depth 2** (`trace.openapi.yaml:120`; `GetGenealogyHandler.cs:22-38`: depth 2 = one extra
  hop each way). The repository methods behind it take **lists of lot ids** and have no limit
  (`ILotRepository.cs:33,35`; `LotRepository.cs:151-155,283-291`, sorted CreatedAt / Id) — exactly what a level-by-level walk
  needs.
- **MVP-1 reality of genealogy (MOD-0174 pack `:388-391`):** backward = the lot's receipt / opening source until MOD-0194
  writes genealogy; forward = children from links + outbound ledger documents. So in MVP-1 most traces are 1–2 levels; the
  engine is built for N.
- **Lot source:** `getLotDetail` → `LotSource` (`trace.openapi.yaml:343-349`: kind GoodsReceipt / ProductionReceipt /
  OpeningBalance / Manual / Migration; documentNumber; partyName = supplier).
- **Ledger by lot:** `QueryTransactionsAsync` (`InventoryLedgerRepository.cs:130`) filters item, lot, location, posting date
  only (`TransactionQuery.cs:8-15`); no movement-type or source-document filter; HTTP `getTransactions` the same
  (`inventory-bundle.openapi.yaml:170-181`). Indexes: `ix_tenant_le_lot` (Tenant, LE, LotId) `:39`,
  `ix_tenant_le_item_postingdate` `:37`; **none on SourceDocumentId or MovementType**. `TransactionItem`
  (`:401-424`) carries movementType, lotId, sourceModule, sourceDocumentId, sourceLineId, postingDate; **no serialIds, no
  from-warehouse** in the contract shape (the entity has them; 0177 reads the entity in-process).
- **Typed-lot history:** `getMovementLotLinks` (`trace.openapi.yaml:259-267`; handler `ManualLotMigrationQueryHandlers.cs:62`,
  `MaxLinks = 1000` `:64`) maps migrated typed-lot movements to lots (`trace_movement_lot_links`, `TraceCollections.cs:20`).
- **Lot / serial control is a SKU fact**, not a lot fact: `sku.LotControlled` / `SerialControlled` from PRODUCT-MASTER
  (`MovementMasterDataValidator.cs:144,159`). `Lot.TrackingMode {Lot, LotAndSerial}` (`Lot.cs:44`, `LotTrackingMode.cs:4-8`).
  Serials: `SerialNumber.cs:12-36` (LotId, Number, Status, WarehouseId, LocationId, ReceivedTransactionId,
  LastTransactionId), index `ix_trace_serial_lot` (`TraceIndexConfiguration.cs:78-90`).
- **Lot posting rules:** a Blocked or Expired lot refuses every movement except SCRAP / WRITE_OFF / REVERSAL
  (`LotPostingRules.cs:42-43` `IsCtD4Exception`, `:78-93`, `:95-111`) — including `STATUS_TRANSFER` into QUARANTINE and
  `CUSTOMER_RETURN` (consequence for KR-B / KR-H: §8.4, §8.6, CT-R10).
- **Quarantine command:** `quarantineStock` posts `STATUS_TRANSFER AVAILABLE→QUARANTINE` per bucket and opens holds
  (`StockActionHandlers.cs:99,229,248,322`); reserved stock is checked **per SKU before the first posting** and refuses the
  whole call 409 `QUARANTINE_STOCK_RESERVED` (`:125-142`, `:213-226`; contract `:58,:63,:371`, details `ReservationLine`
  `:914-918`); a lot that already holds stock is skipped from that check (`:128-130`). The hold gets `Source = Retest |
  Manual` and `SourceDocument.Module = MOD-0175` **hard-coded** (`:341,:344`). `HoldSource` has no recall value
  (`QuarantineEnums.cs:7-16`; `quarantine.openapi.yaml:397`); `SourceDocument {module, type, id, number, text}` exists on the
  wire (`:438-440`) and `listHoldsBySource(sourceModule, sourceDocumentId)` reads by it (`:67-69`, ≤ 200). There is **no
  "request" concept** in 0175: a module either posts inbound into `QUARANTINE` (intake observer) or calls `quarantineStock`.
- **Posting hook pattern (0176 F2, not on this base):** port `IExpiryPostingPolicy` (`IExpiryPostingPolicy.cs:53-60`,
  default `AllowAllExpiryPostingPolicy :63`, registered `DependencyInjection.cs:37`, replaced
  `ExpiryServiceCollectionExtensions.cs:29-30`); pre-check `MovementMasterDataValidator.cs:212` right after the lot rules;
  in-transaction re-check `InventoryLedgerRepository.cs:185` → `LotPostingRefusedException` 422. Exemptions **by movement
  type** (`FefoRules.cs:172-186`); `sourceModule` never exempts (FIX1 `bc7f520e9`, CT O-F2-2).
- **Shipments (MVP-6):** `getShipment` (`shipment-bundle.openapi.yaml:119-122`); `ShipmentLine` has **no lot, serial or
  skuLevel** (`:3042-3052`); customer only as opaque `shipToReference` (`:141`, e.g. `CUST-100/ADDR-2`); delivered time is
  `actualDeliverAt` (`ShipmentSummary :3101-3112`); status enum `:3036`; base path `/api/shipment-bundle`, bearer +
  `LegalEntityId` parameter. Returns: `CreateReturnCommand.reasonCode` is free text (`:3246-3254`), no RECALL reason
  (`returns-semantics-v3.0.0.md`). Integration plan: trace via ledger GOODS_ISSUE `sourceDocumentId = shipmentId`
  (`mvp1-mvp6-integration-2026-10-12.md:44-47`), events `ShipmentDelivered` / `PodCaptured` (`:48`), recall return = MOD-0186
  return into quarantine (`:49`, P1 `:35`); the GOODS_ISSUE_SALES poster at Dispatched is still open (`:36`).
- **Existing trace UI (0174):** `/LotTrace` (`frontend/Diten.Web/Controllers/LotTraceController.cs:15`) reads lot detail,
  `genealogy?depth=2` (`:49`) and `transactions?lotId` (`:67`) under `trace.lots.read` (`:40`). It stays (lot-centred quick
  view); 0177's İzleme is the recall trace (CT-R15).
- **e-signature in InventoryService:** MOD-0175 has the live client (`GatewayESignClient.cs:14`, `EffectivePolicyAsync :29`,
  `SignatureAsync :44`) and the registration hosted service; 0176 F1 runs a fail-closed port
  (`IExpiryResolutionSignature.cs:38`) until F3.

### 8.2 Trace engine (KR-D)

**Input:** `rootLotId`, `direction ∈ {Backward, Forward, Both}`, `asOfUtc` (default now), company (header), limits
(`TraceMaxDepth`, `TraceMaxNodes` from settings).

**Node kinds** (mockup `L:33` `KIND`, labels in §11.6): `RawMaterialLot` / `FinishedGoodLot` / `Lot` (kind from the lot's
source: ProductionReceipt → finished good, GoodsReceipt → raw material / purchased; neutral otherwise), `Input` (backward
leaf: receipt document or "Takip dışı" material), `Stock` (on-hand bucket), `Shipment` (outbound document), `Customer`,
`Gap`.

**Algorithm (pure walk over in-process reads; no HTTP inside InventoryService):**
1. Read the root lot (`ILotRepository.FindAsync`); unknown → 404 `UNKNOWN_LOT`.
2. **Backward** (if requested): frontier = {root}; per level `ParentLinksAsync(frontier)` (one query per level, batched);
   new parents not in `visited` → nodes + edges (`TransformationReference`, `QuantityConsumed`, `UomId` on the edge); parents
   already visited → edge only, marked `cycle: true` when the parent is a descendant on the current path (cycle guard). For
   every lot reached with **no** parent link, add its source as an `Input` node (`getLotDetail` source: GRN number, supplier
   `partyName`, date). Stop at `TraceMaxDepth`.
3. **Forward** (if requested): same with `ChildLinksAsync(frontier)`.
4. **"Takip dışı" inputs:** for each lot in the closure whose source is `ProductionReceipt` with a document number, read the
   `GOODS_ISSUE_PROD` transactions of the same company with that `sourceDocumentId` (new index §8.9) and list issued SKUs
   whose `getSku.LotControlled = false` as `Input` nodes badged **"Takip dışı"** (not a gap). Lot-controlled issued lots
   without a genealogy link to the produced lot → **gap `MISSING_GENEALOGY_LINK`**.
5. **Stock:** for every lot node in the forward closure (and the root): balance rows by lot in the company (in-process
   `getBalance lotId`), one `Stock` node per (warehouse, location, stock status) with on-hand > 0, badge from status
   (AVAILABLE "Eldeki stok", QUARANTINE / QI "Karantinada", BLOCKED "Bloke", …) and the 0175 hold id when one exists.
6. **Outbound movements:** for every lot node in the forward closure and the root: `QueryTransactionsAsync(lotId)` (index
   `ix_tenant_le_lot`), keep `GOODS_ISSUE_SALES`, `GOODS_ISSUE_PROD`, `WAREHOUSE_TRANSFER` (out leg), `SUPPLIER_RETURN`,
   `SCRAP`, `WRITE_OFF`; subtract their `REVERSAL`s (`ReversalOf`); group `GOODS_ISSUE_SALES` by `sourceDocumentId`:
   - `sourceModule = MOD-0183` (MVP-6) → `Shipment` node with `shipmentId = sourceDocumentId`; §8.4 read adds status,
     `actualDeliverAt`, `shipToReference` → `Customer` node.
   - other source modules / manual → `Shipment` node "Belge" with the document number only; customer = "Bilinmiyor (belge
     dışı)" — **gap `NO_CUSTOMER_REFERENCE`** only when `sourceDocumentId` is empty.
   - `GOODS_ISSUE_PROD` is already covered by step 3 children; when it has **no** child link, it is gap
     `CONSUMED_WITHOUT_OUTPUT_LINK` (stock left the company's sight inside production).
   - `SCRAP` / `WRITE_OFF` / `SUPPLIER_RETURN` → informational nodes ("İmha edildi", "Tedarikçiye iade") — not open quantity.
7. **Serials** (lot `TrackingMode = LotAndSerial`): per `Stock` and `Shipment` node the serial numbers (`ix_trace_serial_lot`;
   shipment serials from the issue transaction's `SerialIds`); shown as a collapsible list and in CSV; the case scope is
   still the lot (serial-only recall FU-0177-SER-01).
8. **Gap rules** (each a red row, mockup "İzleme boşluğu" `L:103`):
   | Code | Rule |
   |---|---|
   | `LOTLESS_OUTBOUND` | an outbound movement (types of step 6) of a SKU of the closure whose `getSku.LotControlled = true`, with `LotId = null`, posted between the earliest receipt of any closure lot of that SKU and `asOfUtc`, in the company (index `ix_tenant_le_item_postingdate`) — "lot alanı boş" (mockup `L:27`) |
   | `UNRESOLVED_TYPED_LOT` | a migrated typed-lot movement of the SKU whose typed text has no `MovementLotLink` (`getMovementLotLinks`) |
   | `MISSING_GENEALOGY_LINK` | step 4 |
   | `CONSUMED_WITHOUT_OUTPUT_LINK` | step 6 |
   | `NO_CUSTOMER_REFERENCE` | step 6 |
   | `LIMIT_REACHED` | depth or node limit hit → result `truncated: true`; never silent |
   | `OTHER_COMPANY_ACTIVITY` | a lot of the closure has movements in another company of the tenant (count only, no detail — rule 6 below) |
   A **shipment that cannot be read** (§8.4) is **not** a gap: the node keeps ledger data and carries `shipmentRead:
   Unavailable | Forbidden | NotFound` ("eksik alan").
   **"Takip dışı" is not a gap** (KR-D).
9. **Rule 6 — company:** genealogy is tenant-wide (no `LegalEntityId` on links); stock, movements and shipments are read in
   the **selected company only**; other-company activity is reported as a count (gap `OTHER_COMPANY_ACTIVITY`) so a
   multi-company tenant knows to run the trace there too (CT-R8).
10. **Determinism:** nodes ordered by (level, kind order, lot number, warehouse code, document number, id); the same data
    gives the same node list → same snapshot hash.

**Summary** (mockup `L:115-124`): finished-good lot count, on-hand by warehouse count and quantity per SKU + unit, customers and
shipped quantity per SKU + unit, in-transit shipments, gap count. Never a cross-unit total.

**Performance target (R2 acceptance):** p95 ≤ **2 s** for 3 levels / 500 nodes and ≤ **8 s** at the 5000-node limit, measured
on the own ephemeral Mongo with seeded data; queries per level are batched (one parent / child query per level, one
transaction query per lot batch of ≤ 200 ids with `$in`). The UI shows the measured duration (mockup "1,8 sn").

**Index needs:** §8.9.

### 8.3 Scope trail — frozen snapshot (KR-A, "kapsam onayı anındaki sonuç değişmez")

1. **Open from trace** ("Vaka aç", mockup `L:236`): creates a `Draft` case with the root lot and direction; R2 computes the
   live trace; scope items are created from its nodes (`Included = true`).
2. **Marking** (Draft only, QA): include / exclude `Stock`, `Shipment` and derived `Lot` nodes; exclusion reason 10–1000
   (mockup `L:289` "en az 10 karakter").
3. **Submit** ("Kapsam onayına gönder"): recomputes the trace, stores `RecallTraceSnapshot(Purpose=Submitted)`, canonical
   JSON `rt-v1` (sorted keys, the node order of §8.2 rule 10, decimals as strings, timestamps UTC ISO-8601), **SHA-256**;
   items not in the new trace are dropped, new nodes are added as included and flagged "yeni"; status
   `PendingScopeApproval`.
4. **Approve** ("Kapsamı onayla" + e-signature): the server recomputes the trace; if its hash differs from the submitted
   snapshot → 409 `RECALL_SCOPE_CHANGED` with the diff (the approver never signs a stale scope; the case returns to Draft
   via "Geri gönder" or the submitter resubmits). Equal → the submitted snapshot becomes `ApprovedSnapshotId`, lot blocks
   (§8.5) and quarantine requests (§8.4) are created **in the approval transaction** (requests as `Pending`), status
   `Active`.
5. **After approval** every read of the case shows the **approved snapshot**; a live re-trace (button "Yeniden izle")
   compares node keys: nodes present live but not in the snapshot are listed as **"Kapsam dışı bulgu"** (event
   `OutOfScopeFinding`, yellow rows), e.g. a shipment posted between snapshot and block (§8.5 race) or a newly written
   genealogy link. They change nothing by themselves. Adding them = **scope revision** (CT-R5: revision r+1 → back to
   `PendingScopeApproval` for the delta only, same signature rule; the case stays `Active` and existing blocks stay).
6. Snapshots are insert-only; the evidence pack prints the approved snapshot and its hash.

### 8.4 KR-B — automatic quarantine requests

| Item | Design |
|---|---|
| Trigger | scope approval commit (§8.3 step 4). One `RecallQuarantineRequest` per included `Stock` node of the approved snapshot. |
| Which 0175 operation | `quarantineStock` **mode Manual, one bucket per call** (`quarantine.openapi.yaml:54-65,578-587`) — through an in-process port `IRecallQuarantineRequester` implemented by 0175's application layer (calls `QuarantineStockHandler` `StockActionHandlers.cs:229`, not HTTP) with `reason = "Geri çağırma RC-… · <case reason>"` and the **origin** (QUARANTINE 1.3.0, §8.11.2). |
| Why one bucket per call | the reserved check is per SKU for the whole call and refuses all of it (`StockActionHandlers.cs:125-142`); one call per bucket gives **partial success** — free buckets are held, reserved ones are reported. |
| Execution | after the approval commit, by `RecallQuarantineDispatcher` (in the request, then a hosted retry sweeper every 60 s for `Pending` older than 2 min — crash between commit and dispatch is recovered; no message bus, RabbitMQ is off on this machine). |
| Idempotency | key `recall:{caseId}:r{revision}:{nodeKey}` reused on every attempt → 0175 replays (200) instead of a second posting; replay returns the same holds. |
| Actor | the approver (`RequestedBy`) — recorded on the hold as creator; permission: the approver's `recall.cases.approve` authorises the request (0175 does not re-check `quarantine.holds.create` for this in-process caller — CT-R2). |
| Already quarantined / QI / blocked stock | **no request** (KR-B): request row `NotNeeded` + reason `AlreadyQuarantined` / `AlreadyBlocked` and the existing hold id (by `listHolds lotId` / balance status) — "durumu gösterilir". |
| Lot `Blocked` / `Expired` (0174 status) | `quarantineStock` would refuse `LOT_BLOCKED` / `LOT_EXPIRED` (`quarantine.openapi.yaml:58`; `LotPostingRules.cs:78-93`); the stock is already out of use (a held lot cannot be issued) → `NotNeeded` + `LotBlocked` / `LotExpired`, no call. |
| Retest-due lot | allowed (AVAILABLE→QUARANTINE is the permitted out-of-use move, `LotPostingRules.cs:89-91`). |
| 409 `QUARANTINE_STOCK_RESERVED` | status `BlockedByReservation` + the reservation lines (`ReservationLine :914-918`: reservationId, sourceModule, sourceDocumentId, remainingQuantity) shown on "Eldeki stok"; **0177 never cancels a reservation** (MOD-0175 pack `:128`, CT-Q6). The stock cannot leave anyway (KR-C). QA asks the reservation owner; "Yeniden dene" re-sends with the same key family (`…:a{n}` suffix, because the first key's body is now replay-bound — CT-R2). |
| 409 `QUARANTINE_NOTHING_AVAILABLE` | the bucket moved meanwhile → `NothingAvailable`; the live "Eldeki stok" tab shows where it is. |
| Other errors (period closed, 503) | `Failed` + error; retry button; sweeper retries `Pending` only, never `Failed`. |
| Partial quantity | when on-hand dropped below the snapshot quantity: 0175 quarantines what is AVAILABLE in that bucket (Manual bucket quantity = current AVAILABLE ≤ snapshot quantity) → `PartiallyHeld`, difference shown. |
| Status read | per request: own status + 0175 hold status (`getHold` `:77-79`: Open / InReview / Released / Rejected / Blocked …) read at display time, no copy. |
| Stock appearing later (transfer in, return) | not automatic: the live "Eldeki stok" tab lists in-scope AVAILABLE stock without a request ("istek yok") with "Karantina isteği gönder" (QA, same port, same key scheme). Returns enter `QUARANTINE` anyway (KR-H). |
| Physical segregation | warehouse confirms per held line ("Fiziksel ayırma teyit", mockup `L:268`) → `RecallSegregationConfirmation`; record only, no movement (a `LOCATION_TRANSFER` is the user's own 0173 posting). |
| 0175 decision | stays in 0175 ("Karar (serbest bırak / reddet / bloke) MOD-0175'te verilir; MOD-0177 yalnız isteği oluşturur ve durumu okur" — mockup links screen). A 0175 release of recalled stock to AVAILABLE is allowed by 0175; the stock still cannot leave (KR-C) — CT-R9 asks whether 0175 should warn. |

### 8.5 KR-C — exit block

**Rule (pure Domain, `RecallExitRule.Evaluate(movementType, fromStatus, toStatus, lotId, activeBlocks)`):** a movement whose
`lotId` has an **active** `RecallLotBlock` and whose type is in the **blocked set** is refused with 422 `LOT_UNDER_RECALL`
(details `lotId, lotNumber, recallCaseId, recallCaseNumber, movementType`). **No reason / signature override exists** (KR-C;
mockup option "Gerekçe + e-imza ile izin ver" not built).

| Movement type (`inventory-bundle.openapi.yaml:333`, complete) | Under recall | Why |
|---|---|---|
| `GOODS_ISSUE_SALES` | **blocked** | the recalled goods must not leave (MVP-6 shipment issue `sourceModule=MOD-0183` included — same path) |
| `GOODS_ISSUE_PROD` | **blocked** | must not be consumed into new product |
| `WAREHOUSE_TRANSFER` | **blocked** (CT-R4) | moves recalled goods to another site / in transit; segregation is a `LOCATION_TRANSFER` |
| `REVERSAL` | exempt | correction of a posted row (CT-D4) |
| `SCRAP`, `WRITE_OFF` | exempt | destruction / disposal (CT-D4) |
| `STATUS_TRANSFER` | exempt | 0175's quarantine / block / release moves; KR-B itself |
| `LOCATION_TRANSFER` | exempt | physical segregation |
| `SUPPLIER_RETURN` | exempt | 0175 disposition "return to supplier" |
| `COUNT_GAIN`, `COUNT_LOSS` | exempt | count truth |
| `CUSTOMER_RETURN` | exempt | inbound; KR-H needs it |
| `GOODS_RECEIPT_PO`, `GOODS_RECEIPT_PROD`, `OPENING_BALANCE` | exempt | inbound |

Exemption is decided **only by movement type** — `sourceModule` is caller data and is never read (0176 CT finding O-F2-2,
`FefoRules.cs:169-170`); an architecture test asserts `RecallExitRule` has no `sourceModule` parameter.

**Which lots are blocked:** the root lot and every **included forward** lot node of the approved snapshot (derived lots
reached through child links). Backward nodes (input lots of the root) are **not** blocked — the recall is of the root and
what was made from it; a raw-material root therefore blocks itself and the finished-good lots made from it.

**Port:** `IRecallPostingGate` (Application, `…/Features/Recall/Ports/`), **separate from `IExpiryPostingPolicy`**:
- `PreCheckAsync(lotId, movementType, from, to)` — in `MovementMasterDataValidator` right after the lot rules
  (`:197-205` HEAD; on F2 **before** the expiry pre-check `:212`); refusal → `MasterDataCheck.Refused` 422.
- `ReCheckAsync(transaction, lot, session)` — in `InventoryLedgerRepository.WithTransactionAsync` right after
  `ResolvePostingLotAsync` (`:176` HEAD; on F2 **before** `_expiry.ReCheckAsync` `:185`), reading `recall_lot_blocks` with
  the posting session; refusal → `LotPostingRefusedException` → 422 (authoritative).
- Default `AllowAllRecallPostingGate` registered by InventoryLedger (`TryAddScoped`, next to `DependencyInjection.cs:37`);
  0177 replaces it (`RemoveAll` + `AddScoped`, 0176 pattern `ExpiryServiceCollectionExtensions.cs:29-30`).

**Why a separate port, not a link in the expiry chain:** different owners and lifecycles (0176 may be disabled per company
policy; recall never is), different exemption sets, and a chain would make 0176's port the owner of recall behaviour. Two
ports cost two calls at the same two lines (CT-R11).

**Order: recall first, then FEFO.** A recalled lot is refused before the FEFO rule runs, so (a) no `FEFO_BYPASS` exception or
FEFO snapshot is written for a posting that is refused anyway, (b) the user is not asked for a FEFO bypass reason for a lot
that cannot leave, (c) the FEFO candidate set is unaffected — recalled lots should also be **excluded from the FEFO order**
(0176 `getFefoOrder` reason `RECALLED`; additive FEFO 1.1.0 request to 0176, CT-R12). Lot rules (`LOT_BLOCKED`, `LOT_EXPIRED`)
still run first (existing order).

**Lots without a registered lot id:** typed-lot movements (`lotId` text without a lot — migration window) cannot be matched
by id; the pre-check also compares the typed text with the recalled lots' numbers of the same SKU and refuses the same way
(ASSUMPTION-0177-06).

**Race with approval:** a posting that read `recall_lot_blocks` before the approval commit goes through (no write-write
conflict on a read). It appears in the next live re-trace as **"Kapsam dışı bulgu"** (§8.3 step 5) — visible, not silent.
Approval also records `ApprovedAtUtc`; the evidence pack lists movements of in-scope lots posted after it (should be none).

**Duration:** blocks become active at approval; stay through `Active` and `EffectivenessCheck`; released on `Closed` /
`Cancelled` (cancel is only possible before approval, so in practice on `Closed`) and on scope revision for excluded lots.
A lot in two active cases stays blocked until both release.

**Refusal record:** each refusal writes `ExitRefused` on the case timeline (outside the aborted transaction, 0176
`FlushRefusalAuditAsync` pattern) and audit `recall.exit.refused`.

**MVP-6:** the shipment issue goes through `postMovement` → same gate; the MVP-6 caller receives 422 `LOT_UNDER_RECALL` and
must pick another lot (FEFO order already excludes it, CT-R12).

### 8.6 KR-E / KR-F / KR-H — shipments, customers, returns, effectiveness

**Shipment read (KR-E):** `IShipmentReader` (Infrastructure, HTTP via gateway `GET /api/shipment-bundle/shipments/{id}`,
`shipment-bundle.openapi.yaml:119-122`), with the user's token + `LegalEntityId`; **no cache** (read at display / snapshot
time); timeout 2 s per call, at most 8 in parallel per trace. Fields used: `status`, `actualDeliverAt`, `shipToReference`,
`lines[] {itemId, skuId, quantity, uomId}`, `pod` present. Outcomes:
| MVP-6 answer | Node shows | Trace continues |
|---|---|---|
| 200 | status, delivered at, customer ref | yes |
| 401 / 403 (our tokens have no `legal_entity_id` claim — integration plan) | "Sevkiyat okunamadı — yetki" | yes, ledger data (document number, quantity, date) |
| 404 | "Sevkiyat bulunamadı" | yes |
| timeout / 5xx / no route (MVP-6 down) | "Sevkiyat servisi erişilemiyor" | yes |
The snapshot records the read outcome per node; effectiveness uses **ledger quantities** (issued per lot), never shipment line
quantities (they carry no lot). `ShipmentDelivered` / `PodCaptured` events: not consumed in MVP-1 (no bus on this machine);
read-time status replaces them (FU-0177-EVT-01).

**Customer identity:** `CustomerRef` = `shipToReference` when the shipment was read, else `"DOC:" + sourceDocumentId` with
the document number as label. No customer master link in MVP-1 (FU-0177-CUST-01: MVP-6 party id). The mockup shows names
(seed data); the pack shows the reference + whatever text the shipment returns.

**Notification (KR-F):** record only (§4.7); "Bilgilendirildi / Bilgilendirilmedi" per customer row; no e-mail, no template
(FU-0177-NOTIF-01).

**Returns (KR-H):**
- **MVP-1 manual path:** the user posts a `CUSTOMER_RETURN` into `QUARANTINE` (or `QUALITY_INSPECTION`) with the in-scope lot
  through the MOD-0173 movement form (exempt from the block, §8.5); MOD-0175 intake opens the hold (Source `CustomerReturn`,
  MOD-0175 pack `:343`). In the case, tab "Geri dönüşler" lists **candidates** — `CUSTOMER_RETURN` transactions of in-scope
  lots posted after `ScopeApprovedAt`, not yet linked — and "İadeyi bağla" (warehouse) creates `RecallReturnLink` with a
  customer reference (picked from the case's customers). A `CUSTOMER_RETURN` into `AVAILABLE` cannot be linked (422
  `RECALL_RETURN_NOT_QUARANTINED` — returned recalled goods must be held). 0177 creates **no** movement (mockup
  `addManual` `L:296` creates one — not built, §21.3).
- **Known constraint:** if the lot is `Blocked` / `Expired` in 0174, `CUSTOMER_RETURN` is refused by the lot rules
  (`LotPostingRules.cs:78-93`) → the return cannot be booked. CT-R10 (recommend: allow `CUSTOMER_RETURN` into `QUARANTINE`
  for held lots — a MOD-0174 lot-rule change, owner step; until then the warehouse records the physical receipt as a
  consumption-free note and QA keeps the lot Released-but-recalled, which KR-C already protects).
- **MOD-0186 path (when MVP-6 is live):** a return with reason RECALL + case reference (requested, §8.11.4) posts
  `CUSTOMER_RETURN` (P1 `:35`); 0177 links it automatically by the reference (or by candidate list as above).

**Effectiveness (mockup `L:277-278`):** per (customer, SKU, base unit):
`shipped` = net outbound quantity of in-scope lots to that customer in the approved snapshot (issues − their reversals);
`returned` = Σ linked returns; `consumed` = Σ consumption declarations; `open = shipped − returned − consumed` (shown; a
negative value = over-return, flagged, counted as 0 open); `percent = (returned + consumed) ÷ shipped × 100` (1 decimal);
case total per SKU + unit; **target** = settings `EffectivenessTargetPercent[class]` (KR-G); `met = percent ≥ target`.
Customers not notified are listed. Completion ("Etkinlik kontrolünü tamamla") requires e-signature (§8.8) and stores the
result; closure with `open > 0` requires "Kabul gerekçesi" ≥ 10 (mockup `L:301`).

### 8.7 KR-A — case state machine (in-module, no MOD-0023)

```
Draft ──submit──▶ PendingScopeApproval ──approve(+sig)──▶ Active ──startEffectiveness──▶ EffectivenessCheck
  ▲                      │                                                        │
  └────────return────────┘                                                        ├─completeEffectiveness(+sig)─▶ (EffectivenessCompleted flag)
Draft | PendingScopeApproval ──cancel──▶ Cancelled                                └─close(+sig, needs flag)─▶ Closed
Active ──reviseScope──▶ (revision r+1 PendingScopeApproval for the delta; case stays Active) ──approve(+sig)──▶ Active
```
- Every transition: If-Match, event row, audit row.
- `submit` / `return` / marking: `recall.cases.update`; `approve`: `recall.cases.approve`; `startEffectiveness` /
  `completeEffectiveness`: `recall.effectiveness.complete`; `close`: `recall.cases.close`; `cancel`: `recall.cases.cancel`.
- Mockup steps bar (`L:257`): Taslak → Kapsam onayı bekliyor → Aktif → Etkinlik kontrolü → Kapandı.
- **Four-eyes (CT-R1, 0176 CT-F10 principle):** recommendation —
  - scope approval: **approver ≠ the user who submitted the scope** (409 `RECALL_SOD_VIOLATION`). Rationale: a recall scope
    decides which stock is blocked and which customers are contacted; GxP four-eyes like CT-F10 / KQ-C. Cost: a second QA
    user must exist; for a class-1 recall in a one-QA company the CT may allow a company setting "single approver allowed
    for class 1 with reason" — not recommended in MVP-1.
  - closure: **closer ≠ the user who completed the effectiveness check**.
  - effectiveness completion, drill result, settings: no SoD.
  The mockup has one QA user doing everything (Jane Smith) — demo, not a rule (§21.3).
- **Workflow (MOD-0023) not used** (KR-A): one decider per step, short chain, no routing; WorkCenter visibility is a read-only
  feed later (FU-0177-WC-01).

### 8.8 e-signature (KR-A, KR-G)

**Signable actions** (registered via `POST /api/internal/esign/actions/register`, `esign.openapi.yaml:552-554`, module
**`recall`**, own-prefix rule `CatalogHandlers.cs:262-266`; registration through the generalised InventoryService
registration hosted service, as 0176 F3):

| actionCode | Names (tr) | defaultRequired | defaultMeaningCode | Subject |
|---|---|---|---|---|
| `recall.scope.approve` | Geri çağırma kapsamını onayla | true | `APPROVE` | `recall.case` |
| `recall.effectiveness.complete` | Etkinlik kontrolünü tamamla | true | `APPROVE` | `recall.case` |
| `recall.case.close` | Geri çağırma vakasını kapat | true | `APPROVE` | `recall.case` |
| `recall.drill.result` | Tatbikat sonucunu imzala | true | `APPROVE` (mockup offers "İnceledim / Onay" `L:330` — tenant may map `REVIEW`, CT-F8 precedent) | `recall.drill-run` |
| `recall.settings.update` | Geri çağırma ayarlarını değiştir | **false** (reason mandatory; tenant may require) | `APPROVE` | `recall.settings-change` |

Not signable: open, mark, submit, return, cancel, notification, consumption, segregation, return link, plan edits, exports
(reason / record only). The mockup's settings field "E-imza politikası: Kapsam onayı + kapanış / Yalnız kapanış" is **MOD-0022's
policy** (row `recall.scope.approve` required true / false), edited on the MOD-0022 policy screen, not here (§21.3).

**Subject provider:** `GET /api/recall/esign-subjects/{subjectType}/{subjectId}` (ESIGN provider contract
`esign.openapi.yaml:1160-1161`; config `ESign:SubjectProviders:recall:BaseUrl`, operator config, never a manifest).
Canonical form `{"module","subjectType","subjectId","fields":{sorted}}`, SHA-256 (as `QuarantineSignableSubjects.cs`).
- `recall.case` field list **`rc-v1`**: `number, status, class, level, legalEntityId, rootLotId, rootLotNumber, skuId,
  direction, reason, reasonReference, scopeRevision, submittedSnapshotSha256, approvedSnapshotSha256, excludedCount,
  includedStockCount, includedShipmentCount, pendingAction, pendingActionPayloadSha256, effectiveness.* (flattened per SKU +
  unit when the pending action is completeEffectiveness / close), closureSummary, openQuantityAcceptance, version`.
  The **snapshot hash is part of the signed subject**: signing the scope signs exactly the frozen trace.
- `recall.drill-run` **`dr-v1`**: `number, planCode, lotId, lotNumber, startedAtUtc, endedAtUtc, durationMinutes,
  targetHours, passed, gapCount, snapshotSha256, resultNote, version`.
- `recall.settings-change` **`rs-v1`**: `legalEntityId, settingsVersion, changes.* (field → old|new), reason`.

**Pending action handshake** (0176 `PendingResolution` pattern): the dialog's step 1 calls `prepare…` with the decision
payload (approval note / effectiveness numbers / closure summary + acceptance) → server stores `PendingAction` (15 min) and
returns `{signatureRequired, actionCode, meaningCode, subject}` from `getEffectivePolicy`; step 2 signs
(`signSubject` `esign.openapi.yaml:109-111`, MOD-0022 dialog); the command sends `signatureId`; the server verifies
(`verifySignature` `:174-176`): same tenant, signer = actor, action code, meaning = effective policy's, subject hash =
current, not invalidated, signed after `PendingAction`, never used before (unique per case + action). 0177 stores **only**
`signatureId` (no own signature record — YAPMA).

**Direct signature, not E4:** E4 gates the approve transition of a MOD-0023 workflow task (MOD-0022 pack `:533-564`); 0177
has no workflow task (KR-A), so the deciding user signs directly and 0177 verifies — same reasoning as 0176 §8.6. Until R4 the
port is **fail-closed** (no approval / completion / closure passes in production — 0176 F1 precedent
`IExpiryResolutionSignature.cs:38`).

### 8.9 KPIs, sources, indexes

| KPI (Hazırlık Paneli, mockup `L:209-217`) | Formula | Source |
|---|---|---|
| Açık gerçek vaka | count(status ∉ {Closed, Cancelled}), split by class | `recall_cases` |
| Son tatbikat süresi | last `Completed` drill run duration vs `DrillTargetHours` (badge Hedefte / Hedef aşıldı) | `recall_drill_runs` |
| Lot kapsamı (90 gün) | outbound movements of lot-controlled SKUs with a lot ÷ all outbound movements of lot-controlled SKUs, last 90 days (mockup "%99,9 · 1.524 çıkışın 1.522'si lot / serili") | `inventory_transactions` + getSku (per distinct SKU, request-scoped cache) |
| İzleme boşluğu (90 gün) | count of `LOTLESS_OUTBOUND` rows in 90 days, with the movement numbers | same |
| Geri dönen (açık vakalar) | Σ linked return quantity per SKU + unit over open cases | `recall_return_links` |
| Tatbikat grafiği | last 12 completed drills, minutes vs target line | `recall_drill_runs` (+ imported history — none in MVP-1; mockup `HIST` is seed) |
| Sıklık uyarısı | days since last completed drill > `DrillFrequencyWarningDays` → band | runs + settings |

**Indexes (new):**
- `inventory_transactions`: `(TenantId, LegalEntityId, SourceModule, SourceDocumentId)` (production-document inputs §8.2
  step 4, return candidates); `(TenantId, LegalEntityId, MovementType, PostingDate)` for the 90-day coverage KPI — R2
  measures `ix_tenant_le_item_postingdate` first and adds only what is not covered (0176 CT-F13 rule).
- Genealogy: existing `ux_trace_genealogy_link` (Tenant, Parent, …) serves `ChildLinksAsync`-by-parent and
  `ix_trace_genealogy_child` serves parent lookups — no new index (R2 verifies with `explain`).
- `recall_lot_blocks`: unique `(TenantId, LotId, CaseId)`, partial `(TenantId, LotId)` where `Active` (posting read).
- `recall_cases`: unique `(TenantId, Number)`; `(TenantId, LegalEntityId, Status, OpenedAtUtc)`; partial unique
  `(TenantId, LegalEntityId, RootLotId)` where status active (one active case per root lot — 409
  `RECALL_CASE_ALREADY_OPEN`).
- `recall_case_events`: unique `(TenantId, CaseId, Seq)`. `recall_scope_items`: `(TenantId, CaseId, Revision, NodeKey)` unique.
- `recall_quarantine_requests`: unique `(TenantId, IdempotencyKey)`; `(TenantId, Status, LastAttemptAtUtc)` (sweeper).
- `recall_return_links`: unique `(TenantId, TransactionId)`. `recall_drill_runs`: unique `(TenantId, Number)`, `(TenantId,
  LegalEntityId, Status, EndedAtUtc)`. `recall_evidence_exports`: `(TenantId, LegalEntityId, ExportedAtUtc)`.

### 8.10 KR-G — drills (read-only rehearsal)

- **Plan** (§4.11) → "Başlat": server creates a `Running` run; `StartedAtUtc` = server time (timer start; mockup clock
  `L:323`); lot by `SelectionMode` (Auto: server random pick from the plan's candidate set — finished-good lots with
  outbound movements in the last 365 days / raw-material lots with children; Manual: user picks); at most one running run
  per company (409 `RECALL_DRILL_ALREADY_RUNNING`).
- **Steps** (mockup `L:317`: İzleme, Kapsam, Müşteri listesi), in order (409 `RECALL_DRILL_STEP_ORDER`):
  1. İzleme — runs the trace (both directions, full depth), stores a `Drill` snapshot.
  2. Kapsam — the user reviews the scope summary (no marks stored as a case scope).
  3. Müşteri listesi — the customer list from the snapshot; completing this step sets `EndedAtUtc` (**timer stop**).
- **Result:** duration, target (settings at start), pass / fail, gap count, note → **signature** `recall.drill.result`
  (§8.8) → `Completed`. "İptal" → `Cancelled`, kept for audit (mockup "kayıt oluşturulmadı" — the pack keeps the cancelled
  run; §21.3). Mockup "+30 dk" demo button not built.
- **Read-only guarantee:** a drill never creates a case, scope item, lot block, quarantine request, notification,
  consumption, return link or movement. Enforced by structure — drill handlers live in `Features/Recall/Drills/**` and may
  depend only on `ITraceEngine` (read), `IRecallDrillRunRepository`, `IRecallSettingsReader`, the e-sign port and the audit
  writer — and by an **architecture test**: no type in `…Recall.Drills` references `IRecallQuarantineRequester`,
  `IRecallCaseRepository` write methods, `IRecallLotBlockRepository`, `IInventoryLedgerRepository.PostAsync`,
  `IInternalMovementPostingService` or any 0175 command; plus an integration test that counts writes to every non-drill
  collection before / after a full drill (must be equal).
- Drill runs appear in "Vakalar" with type "Tatbikat" (mockup `L:192-195`) — a union read, not a case.
- Panel frequency band from the last completed run date.

### 8.11 Contract impact (additive only — proposals for the owners' single-writer step)

#### 8.11.1 INVENTORY-BUNDLE 1.4.0 (owner MOD-0173; after 0176's 1.3.0)
- New 422 code `LOT_UNDER_RECALL` (details `lotId, lotNumber, recallCaseId, recallCaseNumber, movementType`) on
  `postMovement`; added to the closed `InventoryErrorCodes.All` with the version.
- `postMovement` description note: a lot under an active recall (RECALL-BUNDLE `getLotRecallStatus`) refuses
  `GOODS_ISSUE_SALES`, `GOODS_ISSUE_PROD`, `WAREHOUSE_TRANSFER`; exemptions by movement type only.
- Optional (not needed by 0177, which reads in-process): `getTransactions` query filters `movementType` (multi) and
  `sourceDocumentId` — useful for MVP-6 and external consumers; CT-R13 decides whether to add now.

#### 8.11.2 QUARANTINE 1.3.0 (owner MOD-0175)
- `HoldSource` gains **`Recall`** (`quarantine.openapi.yaml:397`; `QuarantineEnums.cs:7-16`).
- Holds created for a recall carry `sourceDocument = { module: "MOD-0177", type: "RECALL_CASE", id: <caseId>, number:
  "RC-YYYY-NNNN" }` (existing shape `:438-440`), so `listHoldsBySource("MOD-0177", caseId)` (`:67-69`) answers "which holds
  did this case cause" and the 0175 detail shows "Kaynak: Geri çağırma RC-…" (mockup links screen).
- Internal (not wire): `QuarantineStockHandler` accepts an origin from an in-process caller (seam `:341,:344`).
- **Fully-additive fallback if CT rules an enum value non-additive** (CT-R3): keep `Source = Manual` and only fill
  `sourceDocument` as above — no wire change at all, only the 0175 seam.

#### 8.11.3 TRACE — none required
0177 walks genealogy in-process (§8.2). Optional description note on `getGenealogy` that the full recall trace is
RECALL-BUNDLE `runTrace` (the depth cap 2 stays). No change to `linkGenealogy`.

#### 8.11.4 SHIPMENT-BUNDLE — requests to the MVP-6 team (NOT needs; KR-E)
Separate section in the proposal (§9.4): optional `ShipmentLine.lotId`, `serialIds[]`, `skuLevel`; `GET /shipments?lotId=`;
a structured customer `partyId` next to `shipToReference`; return `reasonCode = RECALL` as a defined value + optional
`recallCaseReference` on `CreateReturnCommand`; accept InventoryService tenant tokens without the `legal_entity_id` claim
(or define the claim mapping). 0177 works without any of them.

#### 8.11.5 FEFO 1.1.0 (owner MOD-0176) — optional
Exclusion reason `RECALLED` in `getFefoOrder` (lot under active recall), CT-R12.

#### 8.11.6 ESIGN — none (consumer). PRODUCT-MASTER — none.
**New stock status / movement type — none** (explicit check above).

### 8.12 Audited events (AUD-001)

Numbering note as in 0176 §8.10: §21 is the UI input in 0174–0177 packs, so the audit list lives here.

| Event | When | Payload |
|---|---|---|
| `recall.case.opened` / `.cancelled` | open / cancel | number, root lot, class, level, reason |
| `recall.trace.run` | every trace (case, ad-hoc, drill) | root lot, direction, node / gap counts, duration, truncated |
| `recall.scope.marked` / `.submitted` / `.returned` / `.approved` / `.revised` | scope steps | revision, snapshot SHA-256, excluded items, signatureId |
| `recall.quarantine.requested` / `.failed` / `.retried` | KR-B | request id, bucket, hold ids, error code |
| `recall.exit.refused` | KR-C | lot, case, movement type, request key |
| `recall.segregation.confirmed` | | request id, location, quantity |
| `recall.customer.notified` | KR-F | customer ref, channel |
| `recall.consumption.declared` | | customer ref, sku, quantity |
| `recall.return.linked` | KR-H | transaction, quantity, source |
| `recall.effectiveness.completed` | | per SKU + unit result, signatureId |
| `recall.case.closed` | | summary, open quantity acceptance, signatureId |
| `recall.drill.started` / `.step` / `.completed` / `.cancelled` | KR-G | run, lot, selection mode, duration, passed, signatureId |
| `recall.drill-plan.saved` | | plan |
| `recall.settings.updated` | | fields changed, reason, version, signatureId? |
| `recall.evidence.exported` | | subject, format, file name, SHA-256 |

(InventoryService has no audit ledger the audit standard credits — 0176 O-F1-1 / FU-SYNC1-AUDIT; 0177 commands join it.)

## 9. Layout & Shell Contract

- Tenant shell only: `/account/login`, `_LayoutTenantShell`, company selector (`X-Legal-Entity-Id`). **No `/Platform/`
  route** (manifest route test, §17).
- Golden reference: lists = slim DataTable (server paging); İzleme = tree + table toggle (mockup `L:230-231`); Vaka Detayı =
  single record with tabs; decisions / closure = two-step dialog + MOD-0022 common signature window (mockup `vPanel`
  `L:372-387`: step 1 "Karar", step 2 "Elektronik imza"); Ayarlar = form + history.
- UAS-001: a user without the page key gets no page skeleton and no redirect.

## 10. Backend File Convention

```
Domain/Recall/{Entities,Enums,Rules}/…        RecallCase, RecallTraceSnapshot, RecallLotBlock, …, RecallExitRule, RecallCaseRules (transitions, SoD), EffectivenessRule, TraceCanonicalForm
Application/Features/Recall/{Commands,Queries,Handlers,Ports,Trace,Drills}/…
Application/Features/Recall/Ports/IRecallPostingGate.cs        (default AllowAll, in InventoryLedger DI)
Application/Features/Recall/Ports/IRecallQuarantineRequester.cs (implemented by MOD-0175 application layer)
Application/Features/Recall/Ports/IShipmentReader.cs
Persistence/Recall/…                           repositories, RecallQuarantineDispatcher(+HostedService), index builder
Infrastructure/Shipments/ShipmentBundleHttpReader.cs
Api/Controllers/Recall/RecallController.cs, RecallESignSubjectsController.cs; Api/Recall/RecallServiceCollectionExtensions.cs, RecallReadinessManifestProvider.cs
Infrastructure/Authorization/RecallPermissions.cs
```
Controllers pass HTTP status explicitly per handler (`CustomBaseController.cs:15-39`).

## 11. Frontend File Contract

### 11.1 Mockup screen → route (all Scope = Tenant, all `_LayoutTenantShell`)

| Mockup screen (`L:168` NAV) | PageCode | Route | Kind | Page permission | Nav |
|---|---|---|---|---|---|
| Hazırlık Paneli | `RECALLDASHBOARD` | `/Recall` | KPI cards + chart + open cases table | `recall.dashboard.read` | yes |
| İzleme | `RECALLTRACE` | `/Recall/Trace` | lot + direction form, tree / table, CSV | `recall.trace.run` | yes |
| Vakalar | `RECALLCASES` | `/Recall/Cases` | slim DataTable + status chips + filters (type, status, class, product, from) | `recall.cases.read` | yes |
| Vaka Detayı | `RECALLCASEDETAILS` | `/Recall/Cases/Details/{id}` | single record, 6 tabs, steps bar, dialogs | `recall.cases.read` | no (parent CASES) |
| Tatbikat Planlayıcı | `RECALLDRILLS` | `/Recall/Drills` | plans table, running drill panel (clock, steps), recent runs | `recall.drills.read` | yes |
| Kanıt Paketi | `RECALLEVIDENCE` | `/Recall/Evidence` | subject picker + 7-section preview + export log | `recall.evidence.export` | yes |
| Ayarlar | `RECALLSETTINGS` | `/Recall/Settings` | form + history | `recall.settings.read` | yes |
| Diğer Ekranlar — lot detayı rozeti | — | MOD-0174 `/Lots/Details` (`LotsController.cs:55`) | badge "Açık geri çağırma: RC-…" + "İzle" | (0174's) + `recall.lots.status.read` (API) | — |
| — karantina kaydı kaynağı | — | MOD-0175 `/Quarantine/Details/{holdId}` (`QuarantineController.cs:72`) | "Kaynak: Geri çağırma RC-…" (from `sourceDocument`) | (0175's) | — |
| — çıkış engeli bandı | — | MOD-0173 `/StockMovements/Create` (`StockMovementsController.cs:57`) | red band "Bu lot geri çağırmada — çıkış engelli (RC-…)" on lot select; server 422 is the control | (0173's) + `recall.lots.status.read` | — |

The mockup's "Diğer Ekranlar" nav item (`L:168` `links`) and the external nav group (`L:175`) are design aids, not pages.

### 11.2 Files
`Controllers/RecallController.cs`; `Views/Recall/{Index,Trace,Cases,CaseDetails,Drills,Evidence,Settings}.cshtml`,
partials `_TraceTree.cshtml`, `_SignDialog.cshtml` (two-step + MOD-0022 window), `_NotificationModal.cshtml`,
`_ConsumptionModal.cshtml`, `_LinkReturnModal.cshtml`; `wwwroot/assets/js/Recall/*.js`; band partials
`Views/Shared/Recall/_LotRecallBadge.cshtml`, `_ExitBlockBand.cshtml`, `_HoldRecallSource.cshtml`.

### 11.3 Screen → button → action → permission

Mockup roles (`L:9-15`, `can()` `L:85`): QA = Jane (case, scope, close), Depo = John (segregation, return), Ticari = Emma
(notification), Kalite sistem yöneticisi = Charlie (drills, settings, evidence), Yönetim = read-only panel.

| Screen | Button / control | Action code | Permission |
|---|---|---|---|
| Panel | KPI links, "Tatbikat planla" band link, open cases table | — | `recall.dashboard.read` |
| İzleme | "İzle" (run), Ağaç / Tablo, "Tümünü aç" | — | `recall.trace.run` |
| İzleme | "CSV indir" | `RECALL_TRACE_EXPORT` | `recall.trace.export` (not logged as evidence — 0176 CT-F14 analogue) |
| İzleme | "Vaka aç" | `RECALL_CASE_OPEN` | `recall.cases.create` |
| İzleme | "Tatbikat taslağı olarak kaydet" | — | **not built** (a drill starts from a plan; §21.3) |
| Vakalar | filters, chips, row open | — | `recall.cases.read` |
| Vaka Detayı / Kapsam | Dahil / Hariç toggle + gerekçe | `RECALL_SCOPE_MARK` | `recall.cases.update` |
| Vaka Detayı / Kapsam | "Kapsam onayına gönder" | `RECALL_SCOPE_SUBMIT` | `recall.cases.update` |
| Vaka Detayı / Kapsam | "Geri gönder" | `RECALL_SCOPE_RETURN` | `recall.cases.approve` |
| Vaka Detayı / Kapsam | "Kapsamı onayla" → dialog step 2 "İmzala ve kaydet" | `RECALL_SCOPE_APPROVE` | `recall.cases.approve` (+ `esign.signatures.sign`, MOD-0022) |
| Vaka Detayı / Kapsam | "Yeniden izle" (live diff) | — | `recall.cases.read` |
| Vaka Detayı / Kapsam | "Kapsam revizyonu" | `RECALL_SCOPE_REVISE` | `recall.cases.update` |
| Vaka Detayı / Eldeki stok | "Karantina isteği gönder" (lines without a request) / "Yeniden dene" | `RECALL_QUARANTINE_REQUEST` | `recall.quarantine-requests.create` |
| Vaka Detayı / Eldeki stok | "Fiziksel ayırmayı teyit et" | `RECALL_SEGREGATION_CONFIRM` | `recall.segregations.confirm` |
| Vaka Detayı / Eldeki stok | KRN link | — (0175 page) | (0175's `quarantine.holds.read`, link only) |
| Vaka Detayı / Müşteriler | "Bilgilendirildi olarak kaydet" | `RECALL_CUSTOMER_NOTIFY` | `recall.notifications.create` |
| Vaka Detayı / Müşteriler | "Tüketim beyanı ekle" | `RECALL_CONSUMPTION_DECLARE` | `recall.consumptions.create` |
| Vaka Detayı / Geri dönüşler | "İadeyi bağla" | `RECALL_RETURN_LINK` | `recall.returns.link` |
| Vaka Detayı / Geri dönüşler | "Müşteri iadesi gir" | — (link to `/StockMovements/Create?type=CUSTOMER_RETURN&lotId=…&toStatus=QUARANTINE`) | (0173's posting key) |
| Vaka Detayı / Etkinlik | "Etkinlik kontrolüne geç" | `RECALL_EFFECTIVENESS_START` | `recall.effectiveness.complete` |
| Vaka Detayı / Etkinlik | "Etkinlik kontrolünü tamamla" (+ e-imza) | `RECALL_EFFECTIVENESS_COMPLETE` | `recall.effectiveness.complete` (+ `esign.signatures.sign`) |
| Vaka Detayı | "Vakayı kapat" (+ e-imza, kabul gerekçesi when open > 0) | `RECALL_CASE_CLOSE` | `recall.cases.close` (+ `esign.signatures.sign`) |
| Vaka Detayı | "Vakayı iptal et" (Draft / Pending only) | `RECALL_CASE_CANCEL` | `recall.cases.cancel` |
| Vaka Detayı | demo status selector (`L:283`) | — | **not built** |
| Tatbikat | "Plan ekle / düzenle" | `RECALL_DRILL_PLAN_SAVE` | `recall.drills.manage` |
| Tatbikat | "Başlat", "Sistem seçsin / Ben seçeyim", step "Tamamlandı", "İptal" | `RECALL_DRILL_RUN` | `recall.drills.run` |
| Tatbikat | "Sonucu imzala" | `RECALL_DRILL_SIGN` | `recall.drills.run` (+ `esign.signatures.sign`) |
| Tatbikat | "+30 dk" (`L:328`) | — | **not built** (demo) |
| Kanıt Paketi | "PDF indir" / "CSV indir" | `RECALL_EVIDENCE_EXPORT` | `recall.evidence.export` |
| Ayarlar | "Kaydet" (reason, optional e-imza by policy), "Geri al" | `RECALL_SETTINGS_SAVE` | `recall.settings.manage` |
| Ayarlar | "Değişiklik geçmişi" | — | `recall.settings.read` |
| Lot detayı (0174) | badge + "İzle" | — | `recall.lots.status.read` (badge) / `recall.trace.run` (link) |
| Hareket formu (0173) | exit band (display only) | — | `recall.lots.status.read` |

### 11.5 Not built here
E-mail / template to customers (KR-F → FU-0177-NOTIF-01); "Gerekçe + e-imza ile çıkış" (KR-C); MOD-0023 workflow variant
(KR-A); mockup decisions panel A–H (design aid); "Öner" quarantine mode (KR-B); demo controls (status selector, +30 dk, role
switcher); PDF archive to DMS (FU-0177-EVID-01).

### 11.6 Resx
`Recall.*` keys (screens, columns, filters, buttons, statuses, classes, levels, node kinds, gap kinds and their texts,
request statuses / not-needed reasons, channels, drill steps, evidence section titles, errors incl. `LOT_UNDER_RECALL`
band text), `Nav.Module.RECALL-READINESS`, `Nav.Page.*` for the 6 nav pages — **7 languages** (tenant module).

## 12. Validation Rules

| Rule | Code | HTTP |
|---|---|---|
| reason / exclusion reason / closure summary < 10 | `REASON_REQUIRED` | 400 |
| class not 1–3, level / direction / channel unknown, quantity ≤ 0 | `INVALID_REQUEST` | 400 |
| settings out of range | `RECALL_SETTINGS_INVALID` | 400 |
| open with root lot already in an active case | `RECALL_CASE_ALREADY_OPEN` | 409 |
| transition not allowed | `RECALL_INVALID_TRANSITION` | 409 |
| stale If-Match | `RECALL_CONFLICT` | 409 |
| scope changed between submit and approve | `RECALL_SCOPE_CHANGED` | 409 |
| approver = submitter / closer = effectiveness completer (CT-R1) | `RECALL_SOD_VIOLATION` | 409 |
| signature missing when policy requires | `RECALL_SIGNATURE_REQUIRED` | 400 |
| signature mismatch (signer, action, subject hash, meaning, reused, before pending) | `RECALL_SIGNATURE_MISMATCH` | 409 |
| pending action expired / differs from command | `RECALL_PENDING_ACTION_MISMATCH` | 409 |
| close with open > 0 and no acceptance reason | `RECALL_OPEN_QUANTITY_ACCEPTANCE_REQUIRED` | 400 |
| close before effectiveness completed | `RECALL_INVALID_TRANSITION` | 409 |
| return link: not `CUSTOMER_RETURN` / lot not in scope / before approval | `RECALL_RETURN_NOT_ELIGIBLE` | 422 |
| return link: into `AVAILABLE` | `RECALL_RETURN_NOT_QUARANTINED` | 422 |
| return already linked | `RECALL_RETURN_ALREADY_LINKED` | 409 |
| quarantine retry on a `Held` / `NotNeeded` request | `RECALL_QUARANTINE_REQUEST_NOT_RETRYABLE` | 409 |
| drill already running / steps out of order | `RECALL_DRILL_ALREADY_RUNNING` / `RECALL_DRILL_STEP_ORDER` | 409 |
| evidence period > 366 days | `RECALL_EVIDENCE_RANGE_INVALID` | 400 |
| exit block | `LOT_UNDER_RECALL` (INVENTORY 1.4.0) | 422 |
| unknown lot / other company / other tenant | `UNKNOWN_LOT` / `CROSS_LE_FORBIDDEN` / 404 | 404 / 403 / 404 |

## 13. Failure Path to Verify

1. Trace on a cycle (A→B→A written by a migration) → terminates, edge `cycle: true`, no duplicate node.
2. Trace beyond `TraceMaxNodes` → `truncated: true` + gap `LIMIT_REACHED`; case submit with a truncated trace → allowed but
   the gap is in the snapshot and the evidence (never silent).
3. Lot-controlled SKU with an outbound movement without lot in the window → gap row; non-lot-controlled packaging input →
   "Takip dışı", not counted as a gap.
4. MVP-6 down / 401 → trace completes with ledger data, shipment nodes marked; no exception, no cache.
5. Scope changes between submit and approve (new shipment posted) → 409 `RECALL_SCOPE_CHANGED`; approve after resubmit →
   snapshot hash equals the signed subject's.
6. Approve with signature of another case / stale hash → 409; approver = submitter → 409 `RECALL_SOD_VIOLATION`.
7. Approval → requests: free bucket `Held` (0175 hold with origin MOD-0177), reserved bucket `BlockedByReservation`
   (reservation lines shown), already-quarantined bucket `NotNeeded`; crash after commit → sweeper sends `Pending`; replay
   with the same key → one hold.
8. `GOODS_ISSUE_SALES` of an in-scope lot (any `sourceModule`, incl. `MOD-0175` and `MOD-0183`) → 422 `LOT_UNDER_RECALL`, no
   movement, no FEFO snapshot / exception; `SCRAP`, `STATUS_TRANSFER`, `REVERSAL`, `CUSTOMER_RETURN` of the same lot → pass.
9. Pre-check passed, block committed before the posting transaction → in-transaction re-check refuses (race test).
10. Manual `CUSTOMER_RETURN` into QUARANTINE → candidate → link → effectiveness counts it; into AVAILABLE → 422; linked twice → 409.
11. Close with open quantity and no acceptance → 400; with → closed, blocks released, next issue of the lot passes.
12. Drill full run → no write to any non-drill collection (counted); timer from server; cancel keeps a `Cancelled` run.
13. Company B user reads company A case → 403 `CROSS_LE_FORBIDDEN`; tenant B → 404.
14. Evidence export twice with the same key → same file + same SHA-256; the hash equals SHA-256 of the downloaded bytes.

## 14. Authorization Convention

### 14.1 Namespace
New namespace **`recall.*`** (PKS-001 §1 `:30-60`, §4 `:91-104`: one owner per namespace) owned by MOD-0177 / manifest
`RECALL-READINESS`. Not `trace.*` (MOD-0174's), not `inventory.*` (MOD-0173's), not `quarantine.*` (MOD-0175's).
**FU-0174-CAT-01 lesson** (MOD-0174 pack `:1029-1041`; `ExpiryFefoManifestProvider.cs:9` comment): the manifest declares
**only** `recall.*` keys; external keys used by 0177 screens (`quarantine.holds.read`, `trace.lots.read`,
`esign.signatures.sign`, inventory posting keys) are never declared here — a namespace test asserts it
(`ManifestNamespaceOwnershipTests` extended, 0176 sabotage (e) pattern).

### 14.2 Keys
| Key | Tier | Meaning |
|---|---|---|
| `recall.dashboard.read` | 1 | panel |
| `recall.trace.run` | 3 | run a trace (heavy read; own key so management can see the panel without running traces) |
| `recall.trace.export` | 2 (`export`) | trace CSV |
| `recall.cases.read` | 1 | list, detail, timeline |
| `recall.cases.create` | 1 | open from trace |
| `recall.cases.update` | 1 | scope marks, submit, revise |
| `recall.cases.approve` | 2 (`approve`) | scope approval, return-to-draft |
| `recall.cases.cancel` | 3 | cancel before approval |
| `recall.cases.close` | 3 | closure |
| `recall.effectiveness.complete` | 3 | start / complete effectiveness check |
| `recall.quarantine-requests.create` | 1 | send / retry KR-B requests manually |
| `recall.segregations.confirm` | 3 | physical segregation confirmation |
| `recall.notifications.create` | 1 | KR-F record |
| `recall.consumptions.create` | 1 | consumption declaration |
| `recall.returns.link` | 3 (`link`) | KR-H link |
| `recall.drills.read` | 1 | plans, runs |
| `recall.drills.manage` | 3 | plan create / update |
| `recall.drills.run` | 3 | start / steps / cancel / sign a drill |
| `recall.evidence.export` | 2 (`export`) | evidence preview + export + log |
| `recall.settings.read` | 1 | settings + history |
| `recall.settings.manage` | 3 | settings save |
| `recall.lots.status.read` | 1 | API-only (`ApiPermissions`): "is this lot under an active recall" — 0174 badge, 0173 band, 0176 / 0178 consumers |

Tier-3 actions (`run`, `cancel`, `close`, `complete`, `confirm`, `link`, `manage`) are registered here (PKS-001 §5 `:108-135`).

### 14.3 Default role mapping (proposal — owner decision ROLE1: only new tenants get templates)
| Role (mockup `L:9-15`) | Keys |
|---|---|
| Kalite (QA) · geri çağırma sorumlusu | dashboard.read, trace.run, trace.export, cases.read / create / update / approve / cancel / close, effectiveness.complete, quarantine-requests.create, notifications.create, consumptions.create, drills.read, drills.run, evidence.export, settings.read |
| Depo / stok operatörü | dashboard.read, cases.read, segregations.confirm, returns.link, lots.status.read |
| Ticari / müşteri hizmetleri | dashboard.read, cases.read, notifications.create, consumptions.create |
| Kalite sistem yöneticisi | dashboard.read, trace.run, cases.read, drills.read / manage / run, evidence.export, settings.read / manage |
| Yönetim · salt-okunur | dashboard.read, cases.read |
Four-eyes (CT-R1) needs **two** users with `cases.approve` / `cases.close`; the template gives both to QA — tenants with one QA
user must grant a second approver.

### 14.4 Manifest
| Field | Value |
|---|---|
| ModuleCode | `RECALL-READINESS` |
| SortOrder | 177 |
| Pages | R1: **none** (API-only keys, 0176 F1 pattern — "sayfasız ilk dilim"; `ExpiryFefoManifestProvider.cs:15,32-40`); R6 / R7 add the 6 nav pages + `RECALLCASEDETAILS` |
| Actions | §11.3 action codes, one per real button (R6 / R7) |
| ApiPermissions | R1: every `recall.*` key used by R1 operations + `recall.lots.status.read` (Tenant) |

## 15. Gateway / API Routing Decision

`/api/recall` and `/api/recall/{everything}` → Diten.InventoryService **5067** (INT-RECALL, integration-agent, route
coverage test, GET / POST / PUT / OPTIONS — no DELETE / PATCH). **In-process with 0173 / 0174 / 0175 / 0176** because (a) the
exit block must run **inside** the ledger posting transaction (§8.5 — a separate service would need a distributed
transaction or a post-commit observer that cannot refuse, `IPostedMovementObserver.cs:7,10`), (b) the trace walks genealogy and
ledger repositories without HTTP (the HTTP genealogy read stops at depth 2), (c) KR-B calls 0175's command in-process. Platform
calls the e-sign provider through the gateway (`ESign:SubjectProviders:recall:BaseUrl`). MVP-6 is called through the gateway
(`/api/shipment-bundle`).

## 16. Acceptance Criteria

| Slice | Criteria | Sabotage (must turn a test red) |
|---|---|---|
| R1 | settings defaults (4 h / 180 d / 100-95-90) read without a row; save needs If-Match + reason ≥ 10, one history row per field; case state machine §8.7 only; SoD on approve / close; signature port fail-closed; company + tenant isolation; counters; manifest namespace test; contract test against `recall.openapi.yaml`; no page in manifest | (a) drop `LegalEntityId` filter in case repository → cross-company test red; (b) skip If-Match → concurrency red; (c) allow `Cancelled → Active` → state test red; (d) allow approver = submitter → SoD test red; (e) add a `quarantine.*` key to the manifest → namespace test red |
| INT | gateway routes + coverage test | remove route / add DELETE → red |
| R2 | trace = §8.2 on seeded graphs (multi-level, cycle, diamond, serial lot, lotless outbound, typed-lot, non-lot input, production without link, other company); deterministic hash; snapshot frozen; live diff lists out-of-scope findings; shipment reader degrade table §8.6 (stub MVP-6: 200 / 401 / 404 / timeout); p95 §8.2; open / mark / submit | (a) remove the visited set → cycle test hangs / red; (b) count "Takip dışı" as gap → red; (c) sum quantities across units → red; (d) cache shipment reads → "no cache" test red; (e) change node order → golden hash red |
| R3 | KR-B table §8.4 every row (stub-free: real 0175 handler on own Mongo); KR-C table §8.5 every movement type via the HTTP posting endpoint; recall before FEFO (with 0176 F2 merged); in-transaction re-check; typed-lot match; refusal timeline row; return link rules; INVENTORY 1.4.0 / QUARANTINE 1.3.0 contract fixtures | (a) remove pre-check → exit test red only at re-check, remove re-check → race test red; (b) exempt by `sourceModule = MOD-0175` → O-F2-2 test red; (c) send all buckets in one call → partial-success test red; (d) new key per retry → double-hold test red; (e) put recall after FEFO → "no FEFO exception on refused posting" red |
| R4 | 5 actions registered (prefix `recall.`), provider `rc-v1` / `dr-v1` / `rs-v1` with golden SHA-256 vectors; snapshot hash inside the signed subject; verification rules §8.8; only `signatureId` stored | (a) skip hash compare → stale-subject test red; (b) accept another case's signature → red; (c) drop `approvedSnapshotSha256` from `rc-v1` → golden red |
| R5 | plan CRUD; run timer from server; step order; result signature; frequency warning; read-only (architecture + write-count test) | (a) drill handler references `IRecallQuarantineRequester` → architecture test red; (b) take timer from client → red; (c) write a scope item in a drill → write-count red |
| R6 | panel / trace / cases 1:1 with mockup except §21.3; KPIs equal formulas on seeded data; tree / table / CSV; UAS-001 | (a) render skeleton without key → red; (b) drop company scope in KPI query → red |
| R7 | case detail tabs, dialogs, drills, evidence (7 sections, SHA-256 of produced bytes, log), settings + history, three bands; buttons hidden without key; 375 px measured | (a) show "Kapsamı onayla" without `cases.approve` → red; (b) hash parameters instead of file → red |
| L10n | 7 languages, parity test | delete one `ar` key → red |

## 17. Test Expectations

- Unit: `TraceEngine` on in-memory graphs (cycle, diamond, depth / node limits, ordering), `RecallExitRule` table for all 15
  movement types, `EffectivenessRule` (units, over-return), `RecallCaseRules` transitions + SoD, canonical form golden vectors.
- Integration (own ephemeral Mongo replica set via `INVENTORY_TEST_MONGO`, never 27017 — memory note): posting endpoint with
  the gate (+ 0176 F2 hook), transaction rollback, approval → requests → 0175 holds, dispatcher recovery, idempotent replays,
  provider hash, drill write-count.
- Contract: `recall.openapi.yaml` vs controllers; INVENTORY 1.4.0 / QUARANTINE 1.3.0 fixture tests by owners.
- Architecture: manifest namespace, no `/Platform/` route, tenant filter guard, no signature entity, drill read-only, no
  `sourceModule` in `RecallExitRule`.
- Web: Razor page tests per route, permission hiding, 7-language parity (`NavManifestL10nGuardTests`).
- Not run: `AccountKindAcceptanceHost.Tests`.

## 18. Ready-for-dev Checklist

| # | Item | State |
|---|---|---|
| 1 | Owner decisions KR-A…KR-H recorded | ✅ §19.1 |
| 2 | RECALL-BUNDLE v1 proposal | ✅ draft — CT acceptance pending |
| 3 | CT-R1 (four-eyes) decided | ✅ §19.7 |
| 4 | CT-R2 / CT-R3 (0175 in-process call, origin / `HoldSource.Recall`) decided + 0175 owner agrees | ⏳ |
| 5 | CT-R4…CT-R18 decided | ✅ §19.7 |
| 6 | INVENTORY 1.4.0 / QUARANTINE 1.3.0 additive proposals accepted by owners | ⏳ (R3) |
| 7 | 0176 F2 (+ FIX1) merged into MVP-1 branch (R3 base) | ✅ 78da50309 |
| 8 | MOD-0022 E2 live (signature API + provider registry) | ✅ live on main |
| 9 | Trace possible with existing reads (`path:line`) | ✅ §8.1, §8.2 |
| 10 | Exit block in-process inside the posting transaction | ✅ §8.5 |
| 11 | Failure path ≥ 4 scenarios | ✅ §13 (14) |
| 12 | Screen → route, button → action → key | ✅ §11.1, §11.3 |
| 13 | Namespace + manifest (API-only first slice) | ✅ §14 |
| 14 | Mockup differences listed | ✅ §21.3 |
| 15 | Audited events | ✅ §8.12 (numbering note) |
| 16 | DCP-002 verifier | waiver **W-0177-01** (no Python) |
| 17 | Port 5067 consistent in pack | ✅ (drift elsewhere → FU-0176-DOC-01) |
| 18 | SHIPMENT requests sent to MVP-6 (not a blocker) | ⏳ CT forwards §8.11.4 |

## 19. Implementation Notes

### 19.1 Owner decisions (2026-10-13) — DECISIONS

| ID | Decision |
|---|---|
| **KR-A** | Case opening / scope approval / closure: **in-module state machine + e-signature** (MOD-0022); MOD-0023 Workflow NOT used. Four-eyes design proposed; whether the opener may approve the scope alone is a CT decision (CT-R1, 0176 CT-F10 principle). |
| **KR-B** | On scope approval, **automatic quarantine request** for the in-scope on-hand stock (MOD-0175 path; the decision is 0175's). Stock already in quarantine / blocked: no request, its state is shown. |
| **KR-C** | Exit from a lot under an active recall is **BLOCKED** (no reason-based permission). Exemptions (reversal, scrap / destruction, 0175 movements) by movement **TYPE** — `sourceModule` is never an exemption reason (CT finding O-F2-2). |
| **KR-D** | Trace depth **full genealogy** (multi-level, forward + backward); cycle guard; trace gaps (outbound without lot, missing genealogy link) as separate rows; non-lot-tracked material "Takip dışı" (not a gap). |
| **KR-E** | Shipment information = **stock ledger + shipment read** (ledger GOODS_ISSUE → `sourceDocumentId` → SHIPMENT-BUNDLE `GET /shipments/{id}`); a lot field on SHIPMENT-BUNDLE only as an ADDITIVE request to the MVP-6 team (separate section). Shipment unreadable (MVP-6 down / auth) → trace continues with ledger data, missing fields marked. |
| **KR-F** | Customer notification **record only** (who, when, channel, note); NO e-mail (FU). |
| **KR-G** | Drill target duration = **company setting**, default 4 hours; drill frequency warning default 180 days; effectiveness target per class (1: 100 %, 2: 95 %, 3: 90 %) company setting. |
| **KR-H** | Recall return: from MOD-0186 (MVP-6) once connected; **in MVP-1 also manual `CUSTOMER_RETURN`** (enters QUARANTINE, 0175 intake opens the hold, linked to the case). |

### 19.2 KR trace table

| KR | Where in pack | Contract | Slice | Test |
|---|---|---|---|---|
| KR-A | §8.3, §8.7, §8.8 | RECALL submit / approve / close + `signatureId`; ESIGN consumer | R1, R4 | failure 5, 6, 11; SoD sabotage |
| KR-B | §8.4 | QUARANTINE 1.3.0 origin / `Recall`; RECALL `retryQuarantineRequest` | R3 | failure 7 |
| KR-C | §8.5 | INVENTORY 1.4.0 `LOT_UNDER_RECALL`; RECALL `getLotRecallStatus` | R3, R7 band | failure 8, 9; O-F2-2 sabotage |
| KR-D | §8.2 | RECALL `runTrace` | R2 | failure 1, 2, 3 |
| KR-E | §8.6 | SHIPMENT read (unchanged) + MVP-6 requests §8.11.4 | R2 | failure 4 |
| KR-F | §4.7, §8.6 | RECALL `recordNotification` | R1, R7 | no e-mail endpoint test |
| KR-G | §4.14, §8.10 | RECALL settings, drills | R1, R5 | defaults test, drill timer |
| KR-H | §8.6 | RECALL `linkReturn`, `getCaseReturns` (candidates) | R3, R7 | failure 10 |

### 19.3 Mockup rules → pack

| Mockup rule | Pack |
|---|---|
| Case statuses / colours (`L:7`), classes (`L:8`), steps bar (`L:257`) | §4.1, §8.7 |
| Exclusion needs ≥ 10 chars (`L:289`); only QA marks; marks frozen after approval ("yeni kapsam revizyonu gerekir" `L:262`) | §8.3, CT-R5 |
| Trace node kinds (`L:33`), gaps "Çıkış hareketinde lot alanı boş" (`L:27`), "Takip dışı" packaging (`L:18`), CSV columns (`L:235`) | §8.2 |
| Quarantine request → "MOD-0175'e gönderildi; karar orada verilir" (`L:293`), physical confirmation by warehouse (`L:268`) | §8.4 |
| Effectiveness = sevk − dönen − tüketildi = açık; % vs class target (`L:277-278`, `L:300`) | §8.6 |
| Closure: summary + acceptance reason when open > 0 (`L:301`) | §8.6, §12 |
| Notification modal: date, channel, note; "e-posta gönderilmedi" (`L:391-392`) | §4.7, KR-F |
| Drill: plan, auto / manual lot, 3 steps, clock, pass vs target, gaps, "Salt-okunur prova" sign note (`L:306-331`) | §8.10 |
| Evidence: 7 sections, PDF / CSV, export log with hash (`L:332-353`, template sections 1–7) | §4.13, §8.12, proposal §4 |
| Settings: target, frequency, class targets, history with reason (`L:355-363`, `chg` `L:66`) | §4.14 |
| Sign dialog two steps, password + meaning, min reason (`L:372-387`) | §8.8, §9 |

### 19.4 Measured facts (selection — every line read in worktree `C:\Users\Ali\erp-vnext-wt-0177` at `1240a1ce9`; F2 = branch `feature/sce/mod-0176-expiry-fefo` at `bc7f520e9`)

`GetGenealogyHandler.cs:12,22-38` · `ILotRepository.cs:33,35` · `LotRepository.cs:151-155,283-291` · `LinkGenealogyHandler.cs:23,26,79` ·
`LotGenealogyLink.cs:9` · `TraceCollections.cs:11-28` · `TraceIndexConfiguration.cs:78-90,92-109,111-121` · `ManualLotMigrationQueryHandlers.cs:62,64` ·
`InventoryLedgerRepository.cs:130,152,166,176,198,205,212-218,278-293` · `TransactionQuery.cs:8-15` · `InventoryIndexConfiguration.cs:28-55,37,39` ·
`MovementMasterDataValidator.cs:75,82,144,159,197-205` · `LotPostingRules.cs:42-43,78-93,89-91,95-111` · `Lot.cs:44` · `LotTrackingMode.cs:4-8` ·
`SerialNumber.cs:12-36` · `StockActionHandlers.cs:99,125-142,155-163,213-226,229,248,322,341,344` · `QuarantineContract.cs:11,21,69` ·
`QuarantineEnums.cs:7-16` · `QuarantineEntities.cs:41,268` · `GatewayESignClient.cs:14,29,44` · `IExpiryResolutionSignature.cs:24,38` ·
`ExpiryFefoManifestProvider.cs:9,15,32-40` · `InventoryPermissions.cs:12-23` · `Program.cs:42-51` · frontend `LotTraceController.cs:15,40,49,67`,
`LotsController.cs:55`, `QuarantineController.cs:72`, `StockMovementsController.cs:57` ·
F2: `IExpiryPostingPolicy.cs:10-23,53-60,63` · `MovementMasterDataValidator.cs:49,59,212,216-219` · `InventoryLedgerRepository.cs:62,74,169,180,185-189` ·
`FefoRules.cs:169-170,172-186` · `DependencyInjection.cs:37` · `ExpiryServiceCollectionExtensions.cs:29-30` · `InventoryErrorCodes.cs:69,72,75` ·
contracts: `inventory-bundle.openapi.yaml:9,150,170-181,330-336,337-364,401-424`, `trace.openapi.yaml:8,113-130,187-191,199-214,259-267,298,308,343-349,380`,
`quarantine.openapi.yaml:12,54-66,67-69,77-79,371,397,438-440,578-587,914-918`, `esign.openapi.yaml:11,109-111,174-176,552-554,702,1160-1161`,
`fefo.openapi.yaml:12,17,32`, `shipment-bundle.openapi.yaml:15,119-122,141,3036,3042-3052,3101-3112,3113-3126,3246-3254` ·
registry `:278` · DCP-009 `:16,42,52,57,63,98` · domain-config `:24,64` · report `:94,124,188-189,214,232,425,518,570` ·
integration plan `:35,36,42-49` · MOD-0174 pack `:170-173,388-391,798,1029-1041,1038` · MOD-0175 pack `:128,343,365` ·
MOD-0176 pack `§8.3, §8.6, §8.7, §19.7` · MOD-0022 pack `:221-224,401-429,449-456,500-514,533-564`.

### 19.5 Open CT decisions

| ID | Question | Recommendation |
|---|---|---|
| **CT-R1** | Four-eyes: may the user who submitted the scope approve it? may the effectiveness completer close? | **No / No** — 409 `RECALL_SOD_VIOLATION` (GxP, CT-F10 principle); no single-approver exception in MVP-1 |
| **CT-R2** | KR-B call shape: in-process port into 0175's `QuarantineStockHandler` with the approver as actor, without re-checking `quarantine.holds.create`; retry key suffix `:a{n}` | Yes — the approver's `recall.cases.approve` authorises the consequence; 0175 still runs every stock / lot / reservation check |
| **CT-R3** | QUARANTINE 1.3.0: add `HoldSource.Recall` + fill `sourceDocument` (MOD-0177 / RECALL_CASE), or `sourceDocument` only (no wire change) | Enum value + sourceDocument (filters and the 0175 detail read naturally); fallback sourceDocument only |
| **CT-R4** | Is `WAREHOUSE_TRANSFER` blocked under recall? | Yes (segregation = `LOCATION_TRANSFER`) |
| **CT-R5** | Scope revision after approval (adding out-of-scope findings): allowed as revision r+1 with its own signed approval of the delta? | Yes; blocks of the approved scope stay meanwhile |
| **CT-R6** | Excluding a derived lot node excludes its subtree (and the lot is not blocked)? | Yes, with reason ≥ 10 |
| **CT-R7** | Exit block applies to the lot in every company of the tenant (block is by lot id) although the case is company-scoped? | Yes — a recalled lot is recalled everywhere |
| **CT-R8** | Trace company rule: genealogy tenant-wide, stock / movements / shipments company-only, other-company activity as count | Yes |
| **CT-R9** | Should 0175 warn on releasing recalled stock to AVAILABLE (read `getLotRecallStatus`)? | Yes — a warning band in 0175 decision (0175 owner, additive), not a block |
| **CT-R10** | Held lot (Blocked / Expired) refuses `CUSTOMER_RETURN` (`LotPostingRules.cs:78-93`) → recall returns of such lots cannot be booked | MOD-0174 lot-rule change: allow `CUSTOMER_RETURN` into `QUARANTINE` / `QI` for held lots (owner step, before R3 acceptance) |
| **CT-R11** | Separate `IRecallPostingGate` vs a link in `IExpiryPostingPolicy` | Separate port, called before expiry |
| **CT-R12** | FEFO 1.1.0 exclusion reason `RECALLED` in `getFefoOrder` | Yes (0176 owner, additive) |
| **CT-R13** | INVENTORY 1.4.0 optional `getTransactions` filters `movementType`, `sourceDocumentId` now? | Not needed by 0177; add only if MVP-6 asks |
| **CT-R14** | Evidence PDF: InventoryService has no PDF library (no QuestPDF / PdfSharp / iText in `*.csproj`) — add one in R7, or server HTML + browser print with CSV as the hashed artifact | Add a PDF library in R7 (license check by CT); the hash is always of the delivered bytes |
| **CT-R15** | Existing 0174 `/LotTrace` (depth 2) vs 0177 `/Recall/Trace` | Keep both; `/LotTrace` gets a "Tam izleme (geri çağırma)" link when the user has `recall.trace.run` |
| **CT-R16** | `recall.settings.update` signable with `defaultRequired=false` (mockup signs settings) | Yes — reason mandatory, signature by tenant policy |
| **CT-R17** | Drill cancel keeps a `Cancelled` run (mockup: "kayıt oluşturulmadı") | Keep (audit of aborted drills) |
| **CT-R18** | Serial-level recall (subset of a lot's serials) | Not in MVP-1 (FU-0177-SER-01); scope and block are lot-level, serials shown |

### 19.6 Assumptions

- **ASSUMPTION-0177-01:** "company" = `LegalEntityId` (W-0173-02).
- **ASSUMPTION-0177-02:** "in-scope on-hand stock" for KR-B = AVAILABLE balance rows of included lots in the approved
  snapshot; QI is treated as already held (`NotNeeded` `AlreadyQuarantined`).
- **ASSUMPTION-0177-03:** finished good vs raw material node kind is derived from the lot source (ProductionReceipt /
  GoodsReceipt); no product-type master field is read.
- **ASSUMPTION-0177-04:** trace limits default depth 20, nodes 5000 (the genealogy writer's own ancestor walk is 5000,
  `LinkGenealogyHandler.cs:26`).
- **ASSUMPTION-0177-05:** the lotless-outbound window starts at the earliest receipt of any closure lot of that SKU in the
  company.
- **ASSUMPTION-0177-06:** typed-lot movements (migration window) are matched by lot number text of the same SKU for the exit
  block.
- **ASSUMPTION-0177-07:** customer = `shipToReference` (or source document) — no customer master in MVP-1.
- **ASSUMPTION-0177-08:** drill duration is whole minutes, rounded up; target compared in minutes.
- **ASSUMPTION-0177-09:** drill-candidate set for "Rastgele bitmiş ürün" = finished-good lots with outbound movements in the
  last 365 days in the company.
- **ASSUMPTION-0177-10:** case class and level are entered at opening and editable only in Draft.

### 19.7 CT acceptance — WP-0177-P0 (2026-10-13)

**CT-ACCEPTED.** Commit `6765606d8` on `feature/sce/mod-0177-recall-readiness` (base `1240a1ce9`), two allowed files only (+1648),
merged (docs only, `--no-ff`) into the MVP-1 branch after 0176 F2 + FIX1 (`78da50309`). The CT re-read the load-bearing facts:
`trace.openapi.yaml:115-122` (`getGenealogy` depth 1–2), `GetGenealogyHandler.cs:22-38`, `LotRepository.cs:151-155,283-291` (list-based,
unbounded link reads → an in-process level walk needs no TRACE change), `StockActionHandlers.cs:125-142` (0175's reservation check refuses
per SKU → per-bucket calls for partial success), `LotPostingRules.cs:78-112` (`EvaluateClassic`: a Blocked lot refuses every non-CT-D4
type, an expired lot too — CT-R10 is real). All exact.

| ID | CT decision |
|---|---|
| CT-R1 | **Yes, four-eyes both ways:** the scope submitter cannot approve; the effectiveness completer cannot close → 409 `RECALL_SOD_VIOLATION`. No single-approver exception in MVP-1. |
| CT-R2 | In-process port into 0175 `QuarantineStockHandler`, actor = the approver, retry key `:a{n}`. Accepted **with conditions**: the hold carries origin MOD-0177 + case number and the approver as actor; 0175 runs every stock / lot / reservation / e-sign-policy check unchanged; 0177 never cancels a reservation. |
| CT-R3 | QUARANTINE 1.3.0: `HoldSource.Recall` + `sourceDocument` (MOD-0177 / RECALL_CASE) — 0175 owner step inside R3, separate commit. |
| CT-R4 | `WAREHOUSE_TRANSFER` blocked under recall (segregation within a warehouse = `LOCATION_TRANSFER`, allowed). |
| CT-R5 | Scope revision r+1 after approval, its own signed approval of the delta; the approved scope's blocks stay meanwhile. |
| CT-R6 | Excluding a derived node excludes its subtree, reason ≥ 10. |
| CT-R7 | The exit block is by lot id across every company of the tenant. |
| CT-R8 | Genealogy tenant-wide; stock / movements / shipments company-only; other-company activity shown as a count. |
| CT-R9 | 0175 shows a warning band (not a block) when releasing recalled stock to AVAILABLE — 0175 owner step, additive, R3 or R7. |
| CT-R10 | **MOD-0174 lot-rule change accepted, narrowly:** `CUSTOMER_RETURN` of a Blocked / Expired lot is allowed **only into `QUARANTINE` / `QUALITY_INSPECTION` / `BLOCKED`**, never `AVAILABLE`; MOD-0174 owner step inside R3, separate commit, with a sabotage that proves an AVAILABLE return of a held lot stays refused. |
| CT-R11 | Separate `IRecallPostingGate`, evaluated **before** the 0176 expiry policy; a refused posting opens no FEFO exception. Exemption by movement TYPE only — never `sourceModule` (O-F2-2 lesson; architecture test). |
| CT-R12 | FEFO 1.1.0 additive exclusion reason `RECALLED` in `getFefoOrder` — 0176 owner step (R3). |
| CT-R13 | No `getTransactions` filters now. |
| CT-R14 | **PDF: PDFsharp / MigraDoc (MIT) — not QuestPDF** (its community licence has a company-revenue cap). One shared choice for 0176 F5 and 0177 R7; the CT checks the licence and version when the first slice adds it; the hash is always of the delivered bytes. |
| CT-R15 | Keep `/LotTrace`; it links to `/Recall/Trace` for users with `recall.trace.run`. |
| CT-R16 | `recall.settings.update` signable with `defaultRequired=false`; reason mandatory. |
| CT-R17 | A cancelled drill run is kept (`Cancelled`). |
| CT-R18 | Serial-level recall out of MVP-1 (FU-0177-SER-01). |

Order: **R1 → INT → R2 → R3 → R4 → R5 → R6 → R7 → L10n**. R3 starts on the MVP-1 branch that already holds 0176 F2 + FIX1. The SHIPMENT
requests (§8.11.4) go to the MVP-6 team with the CT's next MVP-6 note (not a blocker).

### 19.8 WP-0177-R1 — agent result, pending CT (2026-10-13)

Work branch `feature/sce/mod-0177-recall-readiness` (fast-forwarded to base `827cac4e9`). Evidence
`docs/records/audits/2026-10/mod-0177-r1-evidence-2026-10-13.md` (+ scripts). Delivered: company `RecallSettings` (KR-G defaults 4 h / 180 days
/ 100-95-90 without a row; If-Match `"0"` first save, reason ≥ 10, one history row per field; CT-R16 signature by policy), `RecallCase` state
machine §8.7 with insert-only events + `inventory_audit` rows, hand-entered scope items (Lot / Stock / Shipment, computed node keys, CT-R6
subtree exclusion), the scope seal (`rs-scope-v1` SHA-256 at submit, re-sealed at approval → 409 `RECALL_SCOPE_CHANGED`), CT-R1 four-eyes both
ways (409 `RECALL_SOD_VIOLATION` on prepare / approve / close), the signature port **fail-closed** until R4 (no approval, completion or
closure passes in production), pending action 15 min, lot blocks of the approved scope (released at closure), `getLotRecallStatus`
tenant-wide (CT-R7), notification / consumption / segregation records, effectiveness result (pack §8.6, returned = 0 until R3), counters
`RC-YYYY-NNNN` and `RC-YYYY-DNNN`, manifest `RECALL-READINESS` with the 13 R1 keys as `ApiPermissions` (no page), `recall.openapi.yaml` 1.0.0
FROZEN (reconciliations R-1…R-12 in its changelog; R-2 new `addScopeItems`, R-12 `item` instead of `product`). Shared files: additions only
(evidence §8). Inventory 1000 + 1 skipped → **1053 + 1 skipped**; architecture **69/77**, the same 8 reds (the audit list grows 35 → 50
commands, FU-SYNC1-AUDIT); gateway 134/135 (known port red). Sabotage a–i: each red, restored 85/85, sources SHA-identical. Probe on own port
5168 + own mongod 27517: `/health` 200, all 22 R1 routes 401 without a token, MOD-0177 index gate at start.
`git diff --stat 827cac4e9..HEAD`: 39 files, 8223 insertions, 0 deletions — the R1 paths, the eight PARALEL-KURAL files (additions only), `recall.openapi.yaml`, evidence + scripts, this note.

Open for CT (evidence §10): **O-R1-1** audit debt (+15 commands to FU-SYNC1-AUDIT); **O-R1-2** contract reconciliations R-1…R-12; **O-R1-3**
lot blocks written in R1 (read by the status, enforced from R3); **O-R1-4** the scope-change test writes a stored item under the seal (R2: the
live trace); **O-R1-5** a Prism start-up timeout flake in one full run (green alone and on re-run); **O-R1-6** `prepareCaseAction` checks the
decision's key in the controller (R-6).

### 19.9 WP-MVP1-FIX-F4R1 finding 2 — agent result, pending CT (2026-10-13)

The page-less `RECALL-READINESS` manifest left `frontend/Diten.Web.Tests` at 1438/1440 (`NavManifestL10nGuardTests`: no module key; "no pages
parsed"). Branch `fix/mvp1-f4r1` from `1e15e3d59`; no manifest or production change. `Nav.Module.RECALLREADINESS` (the key
`NavNameLocalizer.Normalize("RECALL-READINESS")` yields — the WP's dashed spelling is read by nobody) in all 7 `SharedResource` resx. The
guard accepts ONE named page-less module — `PagelessUntil["RECALL-READINESS"] = "RECALL-READINESS — API-only until WP-0177-R6 (CT FIX-F4R1)"`
— and the new `A_pageless_exception_expires_the_moment_its_manifest_declares_a_page` turns red the moment the recall manifest declares a page
(**R6 must delete the entry with its first page**). Sabotage s1 (ar key) / s2 (entry) / s3 (page added, entry kept) / s4 (another
manifest emptied) each red → green, `git diff` empty. Web 1438/1440 → 1441/1441. Evidence
`docs/records/audits/2026-10/mvp1-fix-f4r1-evidence-2026-10-13.md`.

### 19.10 R1 + FIX-F4R1 — CT acceptance (2026-10-13)

Integration: F4 (`dbefa2a3a`…`b2e8fb12b`) and R1 (`1d79e4ed8`) ran in parallel under the PARALEL KURAL; the CT merged both in its own worktree (`ct/mvp1-int-f4-r1`, `dd682caf9` + `1e15e3d59`, the one conflict = the contracts README row, both rows kept). Merged-tree measurement: Inventory **1060 + 2 skipped**, architecture 69/77 (same 8), gateway 134/135, **Web 1438/1440** — the two `NavManifestL10nGuardTests` reds came from R1's page-less `RECALL-READINESS` manifest; the CT notes that the same two reds existed from 0176 F1 until F4 because the CT did not run the Web suite at F1 acceptance (process gap; the CT now runs Web at every acceptance). WP-MVP1-FIX-F4R1 (`823fb8425`, `c20c4d86a`, `12545ffff`, tests + 7 resx lines only) fixed it: `Nav.Module.RECALLREADINESS` in 7 languages (normalised key) and a self-expiring page-less exception (`RECALL-READINESS — API-only until WP-0177-R6`; red the moment the manifest declares a page). After the fix the CT re-measured Web **1441/1441**; CT sabotage (own): the zh `Nav.Module.RECALLREADINESS` value emptied → the seven-language guard red; restored, `git diff` empty.
R1-specific: CT sabotage (own) — the scope-exclusion reason check bypassed → `CT_R6_excluding_a_derived_lot_…_needs_a_reason…` red; restored.
Open points: **O-R1-1** reconciliations R-1…R-12 accepted (R-2 `addScopeItems` for the manual R1 scope; R-12 `item` instead of `product`,
Model-B type rule); **O-R1-2** lot block rows written at scope approval and read by `getLotRecallStatus` — enforced as the exit block in
R3 (CT-R11); **O-R1-3** `RECALL_SCOPE_CHANGED` proven by a direct DB change until R2's live re-trace; **O-R1-4** one `prepareCaseAction`
route with the decision's own key checked in the controller — accepted; the 15 new write commands join FU-SYNC1-AUDIT (Inventory 50).
R6 must delete the `PagelessUntil` entry when it adds the first page (the guard forces it). Fast-forwarded into the MVP-1 branch.

### 19.11 WP-0177-INT (INT-RECALL) — agent result, pending CT (2026-10-13)

Branch `feature/int/mod-0177-recall-routes` (base `32267f884`). Evidence `docs/records/audits/2026-10/mod-0177-int-evidence-2026-10-13.md`.
`ocelot.json`: `/api/recall` (`:1071`) + `/api/recall/{everything}` (`:1089`) → localhost:5067, GET / POST / PUT / OPTIONS (the methods of
`recall.openapi.yaml` 1.0.0, measured: 22 GET, 26 POST, 2 PUT — no PATCH / DELETE), clones of the `/api/expiry` blocks (no
AuthenticationOptions, no header transform, no rate limit), placed right after `/api/expiry/{everything}` (D-INT-1 pattern; no block moved).
`MvpOneRecallRouteTests` (30): all 50 operations of the contract (R1 served + R2–R7 not yet implemented) → exactly one route
(`/api/recall/{everything}`) with their method, the R4 subject-provider path `/api/recall/esign-subjects/…` and `getLotRecallStatus`
(`/api/recall/lots/{lotId}/status`) shown separately, no DELETE / PATCH, `/api/recallX` look-alikes miss, neighbours (`/api/expiry`,
`/api/quarantine`, `/api/trace`, `/api/inventory` → 5067, `/api/v1/esign` → 5057, `/api/bom` → 5070) unchanged. Gateway tests 134/135 →
**164/165** (the known 5065 port red unchanged). Sabotage a/b/c → 15 / 7 / 5 new reds, restored green, `ocelot.json` SHA-identical. Probe:
own gateway copy on 5100 → `GET /api/recall/settings` (random `X-Tenant-Id`, no token) **401 from localhost:5067** (Ocelot log),
`/api/recallX` 404 (no route).

**INT-RECALL — CT acceptance (2026-10-13).** Commit `aee9d1923` on `feature/int/mod-0177-recall-routes` (base `32267f884`, tree clean,
4 files +348, no deletions). The ocelot diff is exactly the two `/api/recall` blocks (5067, GET/POST/PUT/OPTIONS — measured against
`recall.openapi.yaml` 1.0.0: 22 GET, 26 POST, 2 PUT), placed after `/api/expiry/{everything}` (D-INT-1 pattern). The CT re-measured gateway
**164/165** (the one red = main's known `EveryRoute_DownstreamPortIsInKnownServiceSet`). CT sabotage (own): `GET` removed from
`/api/recall/{everything}` → **9 of 30** recall route tests red; restored, `git diff` empty. No Web / service file touched. Fast-forwarded
into the MVP-1 branch; the owner's gateway reloads `ocelot.json` on change (`reloadOnChange: true`).

### 19.12 WP-0177-R2 — agent result, pending CT (2026-10-13)

Work branch `feature/sce/mod-0177-recall-readiness` (fast-forwarded to base `0bc76ccf7`). Evidence
`docs/records/audits/2026-10/mod-0177-r2-evidence-2026-10-13.md` (+ scripts). Delivered: the §8.2 trace engine over in-process reads (one
batched link query per level, visited set + `cycle` edges, Backward / Forward / Both, settings limits 20 / 5000 → `truncated` + `LIMIT_REACHED`,
"Takip dışı" inputs never gaps, the six gap rules, serials, per SKU + unit totals, reversals netted); CT-R8 (genealogy tenant-wide, ledger
reads in the caller's company, other companies as one `OTHER_COMPANY_ACTIVITY` count — no lot, quantity or document); rule-10 order +
canonical `rt-v1` + SHA-256 (shipment reads and timing outside the seal; golden `7318b1d8…9133a`); `IShipmentReader` through the Gateway with
the caller's token + `X-Legal-Entity-Id`, no cache, 2 s, ≤ 8 in flight, degrade 200 / 401-403 / 404 / timeout-5xx, the trace always
continues, shipment line quantities never read; scope freezing §8.3 (open from trace, submit → `RecallTraceSnapshot(Submitted)`, prepare and
approve re-trace → 409 `RECALL_SCOPE_CHANGED` with the node-key diff, approve → `Approved` snapshot shown by the reads, insert-only;
"Yeniden izle" `getCaseScopeDiff` → `OutOfScopeFinding` events once per node); CSV `exportTrace` (BOM, `;`, rule-10, no evidence log); keys
`recall.trace.run` / `recall.trace.export` in the manifest; `recall.openapi.yaml` **1.1.0** additive (reconciliations R2-1…R2-7).
`reviseScope` → **R3** (§1 R2 row is the diff; a revision changes the lot blocks R3 enforces). Inventory 1062 + 2 skipped → **1109 + 4
skipped**; perf (gated) 535 nodes p95 159 ms, 4973 nodes 886 ms; architecture **69/77** same 8 reds (audit list 50 → 53, FU-SYNC1-AUDIT);
gateway 164/165; Web 1445/1445. Sabotage a–i: each red, restored 132/132, sources SHA-identical. Probe 5168 + own mongod 27517: `/health`
200, all 25 R1 + R2 routes 401. Shared files: additions only (`InventoryIndexConfiguration.cs:41-43`,
`SingleSystemOfRecordArchitectureTests.cs:57-58,68`, contracts `README.md:17`).

Open for CT (evidence §11): **O-R2-1** audit debt +3; **O-R2-2** `RecallPermissions.R1` is an alias of `Implemented` (shared namespace test
pins `R1`); **O-R2-3** reconciliations R2-1…R2-7; **O-R2-4** the R1 `sourceModule` guard narrowed to node identification; **O-R2-5** test rows
the APIs refuse are seeded as clones of real rows; **O-R2-6** `TraceRun` events on open / submit only, no drift check for pre-R2 submissions;
**O-R2-7** Backward still lists the root's own stock / outbound; **O-R2-8** CSV `\r\n` fixed.

**R2 — CT acceptance (2026-10-13).** Commits `12a383b74` + `d299ae682` (base `0bc76ccf7`, tree clean, 37 files +3801 / −41, deletions only in
MOD-0177's own R1 files and 4 lines of `recall.openapi.yaml` → 1.1.0). The CT re-measured Inventory **1109 + 4 skipped** (full run). CT sabotage
(own): the `X-Legal-Entity-Id` header removed from `ShipmentBundleHttpReader` → 2 red (`Reads_go_through_the_gateway_path_with_the_callers_company`,
the multi-level API trace with the MVP-6 shipment); restored, `git diff` empty. Decisions: **`reviseScope` → R3** accepted (a revision changes the lot
blocks R3 enforces). **O-R2-2** accepted (`RecallPermissions.R1` alias of `Implemented` — rename when the shared test is next touched, FU-0177-PERM-ALIAS).
**O-R2-4** accepted: `sourceModule` is read only in the five trace files and only to recognise the shipment node kind; no exemption / authorisation
decision reads it (the R1 guard narrowed accordingly). **O-R2-5** accepted (rows the APIs refuse by design are seeded by cloning real rows).
**R2-1…R2-7** accepted; note R2-x: `WAREHOUSE_TRANSFER` is not an outbound leg (the stock stays in the company and appears as a Stock node), and the
customer is carried on the Shipment node (no separate Customer node) — R6 renders the customer row from the shipment node's fields. Fast-forwarded
into the MVP-1 branch. Live: R2 adds DI / handlers — the owner's Inventory window needs the CT restart (with approval) together with F5.

### 19.13 WP-0177-R3 — agent result, pending CT (2026-10-14)

Start condition measured: MVP-1 tip `acff1f7be` (WP-0176-F5 CT-ACCEPTED); work branch fast-forwarded to it. Evidence
`docs/records/audits/2026-10/mod-0177-r3-evidence-2026-10-14.md` (+ scripts). Commits — owner steps separate: `5ad58042e` (MOD-0173 / MOD-0174:
INVENTORY 1.4.0 `LOT_UNDER_RECALL`, `IRecallPostingGate` seam at the validator pre-check and the posting re-check, CT-R10), `47957b106` + `7ef123d2e`
(MOD-0175: QUARANTINE 1.3.0 `HoldSource.Recall`, in-process origin, `QuarantineRecallRequester`), `9fd9a740c` (MOD-0176: FEFO 1.3.0 `RECALLED`),
`2befeac9d` (R3 code, RECALL 1.2.0). Delivered: **KR-C** exit block by movement type only (GOODS_ISSUE_SALES incl. MVP-6, GOODS_ISSUE_PROD,
WAREHOUSE_TRANSFER), tenant-wide by lot id (CT-R7, another company sees the case number, not the id), typed lot by number + SKU, after the lot rules
and BEFORE the FEFO hook at both seams (CT-R11), re-checked in the posting transaction, `ExitRefused` + `recall.exit.refused`; **KR-B** requests in
the approval transaction, one 0175 call per bucket after the commit (approver as actor, key `recall:{case}:r{n}:{node}`, `:a{n}` on retry), NotNeeded
rows, BlockedByReservation with the lines (never cancelled), 60 s sweeper for Pending > 2 min; **CT-R12** recalled lots excluded from the FEFO
order; **KR-H** return candidates + `linkReturn` (after approval, held status, one case per return; returned in the effectiveness); **CT-R5**
`reviseScope` r+1 from findings, the delta's own signed approval adds blocks / requests and never removes one; **CT-R10** held-lot returns only into
QUARANTINE / QI / BLOCKED. Inventory 1115 + 4 → **1153 + 4** (one full run had 16 Prism start-up timeouts; Prism alone 16/16); architecture
**69/77** same 8 reds (audit list 54 → 58, FU-SYNC1-AUDIT); gateway 164/165; Web 1458/1458; build 0 / 0. Sabotage a1, a2, b–j: each red, restored
green, sources SHA-identical, `git diff` empty. Probe 5168 + own mongod 27517: `/health` 200, all R1–R3 recall routes 401.

Open for CT (evidence §12): **O-R3-1** audit debt +4; **O-R3-2** reconciliations R3-1…R3-4; **O-R3-3** the bucket quantity is decided once per key
(replay body), retry re-plans; **O-R3-4** the port takes a bucket list only for sabotage (c); **O-R3-5** `listHoldsBySource` still not served by
0175; **O-R3-6** records during a pending revision; **O-R3-7** emptied request lines in `getCaseStock`; **O-R3-8** CT-R9 band → R7; **O-R3-9**
the R2 trace test's finding is now the quarantined stock line.

**CT-ACCEPTED (2026-10-14).** Read the gate / exit rule / seam diffs: decision by movement type only, no `sourceModule`, tenant filter on every block read, case id hidden from another company, refusal recorded outside the posting transaction; CT-R10 narrow. CT trial merge on the MVP-1 tip `713d4ee8a` (FIX-LIVE1 in): Inventory **1153 + 4** all green (no Prism timeout), Web **1468/1468**, gateway 164/165, architecture 69/77 (same 8). **CT sabotage:** `RecallPostingGate.ByLotAsync` without the `TenantId` filter → the FULL Inventory suite stays **1153/1153 green** — the tenant boundary of the exit block is correct but UNTESTED (AGENTS.md: a guard without sabotage proof is not a guard) → **WP-0177-FIX-R3T** (tenant-boundary tests for `ByLotAsync`, `ByTypedLotAsync`, `UnderRecallAsync`, the sweeper scope). CT note: `RecallQuarantineSweeper.SweepAsync` has no per-request catch — one throwing request ends the tick for the rest → in FIX-R3T. O-R3-1…9 accepted as written (O-R3-5 → FU-0175-Q5-03, O-R3-8 → R7). Merged `--no-ff` into MVP-1 (`847d9536b`); owner Inventory window needs a restart (hosted sweeper + DI) — owner approval.

### 19.14 WP-0177-FIX-R3T — ajan sonucu, CT bekliyor (2026-10-14)

Dal `fix/mod-0177-r3-tenant` (taban `9362c0402`). Kanıt `docs/records/audits/2026-10/mod-0177-fix-r3t-evidence-2026-10-14.md`. Commit'ler:
`98b940b7f` kiracı sınırı testleri, `7c2885d94` süpürücüde istek başına yakalama + testler. `RecallPostingGate.cs` değişmedi.

- **Testler (gerçek gate, gerçek kayıt ucu, kendi Mongo):** T2'de aynı lot id / aynı SKU + lot numarası üzerinde aktif blok →
  (a) `ByLotAsync` ön kontrol ve (yarış dikişiyle) işlem içi yeniden kontrol T1'i reddetmez (201); (b) `ByTypedLotAsync` aynısı tipli lotla;
  (c) `UnderRecallAsync` lotu FEFO sırasından `RECALLED` diye çıkarmaz; (d) aynı blok T1'de üçünü de engeller; (e) T1 ve T2'de
  `ExitRefused` / `recall.exit.refused` satırı oluşmaz. Süpürücü: (f) tek tur iki kiracının isteğini kendi kiracı / şirket kapsamında gönderir
  (karantina kaydı isteğin `TenantId` / `LegalEntityId`'sinde); (g) 0175 hatasını dispatcher'ın kendi yakalaması Pending tutar; (g2) döngüde
  hata veren satır (`LegalEntityId` null) Pending kalır, `recall.quarantine.sweep_request_failed` (istek id'si + kiracı) loglanır, diğerleri gönderilir.
- **BLOCKED → CT kararı A:** dekoratörlü (g) yeni try/catch'i kanıtlayamadı (dispatcher 0175 hatasını zaten yakalıyor; ölçüldü) → (g2) eklendi.
- **Sabotaj:** s1 `ByLotAsync` filtresiz → (a) kırmızı; s2 `ByTypedLotAsync` → (b); s3 `UnderRecallAsync` → (c); s4 `SetScope` ilk isteğe sabit →
  (f) (+ g2); s5 try/catch'siz → (g2) kırmızı, (g) beklenen yeşil (kapsam dışı). Her geri almada SHA başlangıçla aynı.
- **Sayılar:** Inventory 1153 + 4 → **1159 + 4** (tam koşuda 16 Prism başlangıç zaman aşımı; Prism tek başına 16/16); mimari **69/77** aynı 8
  (denetim listesi 58 değişmedi); derleme **0 / 0**.
- Açık: O-R3T-1 kalıcı bozuk satır her turda yeniden denenir (FU-0177-SWEEP-01 önerisi); O-R3T-2 dispatcher'ın kendi uyarı logunda kiracı yok
  (FU-0177-SWEEP-02 önerisi); O-R3T-3 kiracı listesi aşırı yüklemesini yalnız testler kullanıyor.
**FIX-R3T CT-ACCEPTED (2026-10-14).** Read the sweeper change (per-request catch, shutdown cancellation not swallowed, tenant-list overload test-only) — `RecallPostingGate` unchanged. CT trial merge on the MVP-1 tip `1f904aa83`: Inventory **1159 + 4** (the full run had the known 16 Prism start-up timeouts; Prism alone 16/16). **CT sabotage:** only the IN-TRANSACTION re-check of `ByLotAsync` loses its `TenantId` filter (the pre-check keeps it) → `a_ByLotAsync_…_refuses_neither_the_pre_check_nor_the_re_check` red — the re-check stage is measured on its own, as claimed; restored, porcelain clean. The R3 CT finding (untested tenant boundary) is closed. O-R3T-1 → FU-0177-SWEEP-01 (retry limit / Failed + reason), O-R3T-2 → FU-0177-SWEEP-02 (tenant in the dispatcher warning, OBS-001). Merged `--no-ff` into MVP-1; the owner Inventory window needs a restart for the sweeper change (approval).


### 19.15 WP-0177-R4 — agent result, pending CT (2026-10-14)

Dal `feature/sce/mod-0177-r4` (taban `9729409e0`). Kanıt `docs/records/audits/2026-10/mod-0177-r4-evidence-2026-10-14.md` (+ betikler). Commit'ler:
`642489c0f` RECALL **1.3.0** (yalnız ekleme), `9813aebc4` Inventory, `9c0ef2668` Platform yapılandırma satırı + host testi.

- **BLOCKED → CT kararı B:** rs-v1 için `prepareSettingsChange` (yeni yazma komutu) uygulandı; denetim listesi 58 → 59 GEÇİCİ kabul (BL-501 /
  WP-MVP1-AUDIT1 v2); borç listesine satır eklenmedi. Kanıt §2 "R4 komutları" tablosu (komut, handler, koleksiyonlar).
- **Teslim:** 5 işlem (`recall.` öneki, 7 dilde ad, settings.update `false`); `rc-v1` (gönderilen + onaylanan iz mührü imzanın içinde) /
  `dr-v1` / `rs-v1` altın vektörleri; `GET /api/recall/esign-subjects/{type}/{id}` (kiracıya bağlı dizin, her ret 404
  `RECALL_SUBJECT_NOT_FOUND`, drill-run R5'e kadar 404); `ESignRecallSignature` F3-A (getSignature + yerel hash; imzacı, işlem, anlam, o anki
  hash, geçersiz değil, hazırlıktan sonra, tek kullanım, başka vaka yok; MOD-0022 okunamazsa ret); kapsam onayı + CT-R5 revizyonu + tamamlama
  + kapanış + ayar bu kapıdan; CT-R1 dört göz değişmedi; 0177 yalnız `signatureId` saklar.
- **Pack uzlaştırması:** §8.8'deki `verifySignature` cümlesi F3-A ile geçersiz (0176 F3 CT kararı); WP'nin `SUBJECT_NOT_FOUND`'u →
  `RECALL_SUBJECT_NOT_FOUND`; BaseUrl Gateway emsaliyle `http://localhost:5000/api/recall`; R4-1 / R4-2 (kanıt §3).
- **Sabotaj:** a–h 8/8 kırmızı → yeşil, özgün baytlar geri yazıldı, SHA eşit (kanıt §7).
- **Sayılar:** Inventory 1159 + 4 → **1182 + 4**; mimari **69/77** aynı 8; gateway 164/165; Web 2272/2272; Platform ESign 176 → **177/177**; derleme 0 / 0.
  Sonda 5168 + mongod 27517: R4 yolları ve imzalı kararlar token'sız 401; filo PID'leri değişmedi.
- Açık: O-R4-1 (geçici denetim borcu), O-R4-2 (drill-run → R5), O-R4-3 (sahip yeniden başlatması + canlı MOD-0022 kontrolü), O-R4-4, O-R4-5.
**R4 CT-ACCEPTED (2026-10-14).** Read the binding (`ESignRecallSignature`: getSignature + current local hash, F3-A; no `verifySignature`) and the scope-middleware exemption (GET-only `/api/recall/esign-subjects/…`, same rule as quarantine / expiry). BLOCKED → CT decision B (`prepareSettingsChange` now; audit list 58 → 59 accepted temporarily — AUDIT1 v2 / BL-501 wires every Inventory command). CT trial merge on the MVP-1 tip `a114d6879`: Inventory **1182 + 4** all green, Platform ~ESign **177/177**, architecture 69/77 (same 8). **CT sabotage (not in a–h):** the "signed at or after the preparation" check dropped → `Rule_signed_after_the_preparation` + the stale-subject test red; restored, porcelain clean. Reconciliations accepted (pack `verifySignature` → F3-A; `RECALL_SUBJECT_NOT_FOUND`; provider via the Gateway like quarantine / expiry). O-R4-2 → R5, O-R4-3: owner Inventory + Platform restart needed for live (approval). Merged `--no-ff` into MVP-1.


### 19.16 WP-0177-R5 — agent result, pending CT (2026-10-14)

Dal `feature/sce/mod-0177-r5` (taban `394c05550`). Kanıt `docs/records/audits/2026-10/mod-0177-r5-evidence-2026-10-14.md` (+ betikler). Commit'ler:
`52299453d` RECALL **1.4.0** (yalnız ekleme), `016c44c63` Inventory, (kanıt commit'i) kanıt + bu not + PUT bekçisine `updateDrillPlan` satırı.

- **Teslim (KR-G):** `Features/Recall/Drills/**` — plan CRUD (`DP-NN`, If-Match, şirket kapsamı, `recall.drills.read/manage/run`);
  `startDrill` (sunucu saati, başlangıçtaki hedef, Auto = enjekte rastgelelikle denetlenen seçim, Manual, şirket başına tek Running — kısmi tekil
  indeks, `RC-YYYY-DNNN`); adımlar sıkı sırada (İzleme → `Drill` amaçlı iz anlık görüntüsü; Kapsam → kapsam yazılmaz; Müşteri listesi →
  `EndedAtUtc`, `ceil` dakika, geçti = süre ≤ hedef × 60); sonuç hazırla → imzala (`recall.drill.result`, dr-v1 koşudan; sağlayıcı artık 200;
  F3-A R4 portuyla; dört göz yok; politika gerekmezse imzasız); iptal saklanır (CT-R17), kapalı koşu değişmez; okumalar + `frequencyWarning`.
- **Salt-okunur garanti:** ayrı tatbikat deposu; mimari test (IL dahil, gerçek tip adlarıyla uzlaştırılmış yasak liste, §3.2) + kiracının HER
  koleksiyonunda yazma sayımı testi (istisnalar açıkça listelenir).
- **AUD-001:** denetim listesi 59 → **66** GEÇİCİ (önceden verilen CT kararı); borç listesine satır yok; kanıt §2 "R5 komutları" tablosu.
- **Uzlaştırmalar:** R5-1…R5-5 (sözleşme changelog'u, kanıt §3.1); pack §8.10 adları ↔ gerçek tipler (kanıt §3.2).
- **Sabotaj:** a–h (b iki biçimde) 9/9 kırmızı → yeşil, özgün baytlar geri yazıldı, SHA eşit (kanıt §6). dr-v1 altın vektörü R4 ile aynı.
- **Sayılar:** Inventory 1182 + 4 → **1199 + 4**; mimari **69/77** aynı 8; gateway 164/165; Web 2280/2280; Platform ESign 177/177; derleme 0 / 0.
  Sonda 5168 + mongod 27517: tatbikat uçları ve sağlayıcı drill-run token'sız 401; filo PID'leri değişmedi.
- Açık: O-R5-1 (geçici denetim borcu), O-R5-2 (sağlayıcının `recall.cases.read` anahtarı — CT kararı), O-R5-3 (pano / Tatbikat satırı / ekranlar
  → R6 / R7), O-R5-4 (sahip yeniden başlatması), O-R5-5 (aday lot türü).
- **CT kararı sonrası (2026-10-14, agent result, pending CT):** MVP-1 ucu `a27519491` (BL-501) merge edildi (`2d79e2485`, çakışmasız).
  7 tatbikat komutu `ICentralAuditDeclaration` taşır, `RecallDrillRepository` işlemi merkezi kayda katılır (10 işlem noktası); yazma sayımı
  istisnasına yalnız `central_audit_outbox`. Inventory 66 komut, denetimsiz **0**. O-R5-2: drill-run konusu `recall.drills.read` ile de okunur
  (RECALL 1.4.0 içinde yalnız ekleme: `x-permission-by-subject-type`). Inventory **1217 + 4**; mimari 71/79 aynı 8; BB 28/28; Platform ESign 177;
  Web 2280; derleme 0/0. Sabotaj (5) + merkezi denetim + 2 ek: kırmızı → yeşil, SHA eşit. Kanıt §13.
**R5 CT-ACCEPTED (2026-10-14).** First result sound (1199 + 4; CT sabotage: cancelling a closed run → `A_cancelled_run_is_kept_and_a_closed_run_never_changes` red) but on the BL-501 tip 3 central-audit tests were red → follow-up: MVP-1 tip merged, 7 drill commands marked, drill repository transaction enlisted, write-count test exempts only `central_audit_outbox` by name; O-R5-2 CT decision: drill-run subject readable with `recall.drills.read` (additive `x-permission-by-subject-type`). CT run on the branch tip (contains `a27519491`): Inventory **1217 + 4**, architecture 71/79 same 8, Inventory new-unaudited **0**. **CT sabotage (reverse leak):** `recall.drills.read` accepted for a CASE subject too → `A_signer_with_only_recall_drills_read_reads_a_drill_run_subject_but_not_a_case_subject` red; restored. O-R5-3 (panel, Vakalar "Tatbikat" row, screens) → R6 / R7; O-R5-5 → FU. Merged `--no-ff` into MVP-1; live after the owner Inventory restart (approval).


### 19.17 WP-0177-R6 — agent result, pending CT (2026-10-15)

Dal `feature/sce/mod-0177-r6` (taban `9c41cc92c`). Kanıt `docs/records/audits/2026-10/mod-0177-r6-evidence-2026-10-15.md`. Commit'ler:
`2c8089fc8` RECALL **1.5.0** (yalnız ekleme), `f5a1b75aa` + `a160433b5` Inventory, `bebec8cc3` Web, `7cfc7e703` test, `87fed11d8` Web düzeltme,
(kanıt commit'i) kanıt + bu not + §21.3 D-25.

- **Teslim (UI 1):** `/Recall` Hazırlık Paneli (§8.9 KPI'ları servis hesaplar: açık vaka sınıf kırılımı, son tatbikat vs hedef, son 12
  tatbikat grafiği + hedef çizgisi, lot kapsamı / lotsuz çıkış 90 gün, geri dönen SKU + birim başına, sıklık bandı `>`), `/Recall/Trace` İzleme
  (ağaç / tablo aynı veri, CSV baytları değişmeden, Vaka aç = gerçek Taslak, CT-R8 başka şirket yalnız sayı), `/Recall/Cases` Vakalar.
- **CT kararı A:** `DrillCaseRow` + isteğe bağlı `CasePage.drillRows` / `drillStatusCounts`, `drillStatus` parametresi, ortak `sortAt` +
  `position`; `CaseRef` zorunluları değişmedi; `class` süzgeci tatbikatı dışarıda bırakır; ekranda tatbikat satırı sınıf / seviye "—",
  "Tatbikat" çipi, R7'ye kadar tıklanmaz. Mockup farkı **D-25** §21.3'e eklendi.
- **Web:** 7 kapı, her biri kendi anahtarı (anahtarsız 403 + gateway'e istek yok), catch-all yok; UAS-001 üç sayfa; 7 dil; UXH-001 metinleri
  resx'te, panel bağlaması FU-0177-HELP-PANEL; `PagelessUntil` girdisi silindi; ApexCharts (repo'daki kütüphane).
- **Sabotaj:** a–f + g (CT) 7/7 kırmızı → yeşil, özgün baytlar, SHA eşit. d ilk koşuda yeşil kaldı (motor bakiyeleri yer başına
  topluyor) → test miktar toplamıyla sıkılaştırıldı, yeniden kırmızı → yeşil (kanıt §6).
- **Sayılar:** Inventory 1217 + 4 → **1238 + 4**; Web 2280 → **2320**; mimari 71/79 aynı 8; AuditTrail listesi birebir aynı (yeni yazma
  komutu yok, denetimsiz 0); gateway 164/165 aynı; derleme 0 hata, uyarı aynı (Recall kaynaklı 0).
- **Önizleme:** kendi portlar 5168 / 5105 / 5104 (+ anahtarsız 5106); gerçek API'ye karşı üç sayfa, anahtarsız kullanıcı iskelet görmez
  ve gateway'e istek gitmez; ekran görüntüleri alındı; dar ekranda İzleme formu taşması bulundu ve düzeltildi.
- Açık: O-R6-1 (yardım paneli bağlama), O-R6-2 (R7 ekranları, Panel "Tatbikat planla", satır tıklama, 375 px), O-R6-3 (önizleme ortamı notu).
**R6 CT-ACCEPTED (2026-10-15).** BLOCKED → CT decision A (drill rows are not case rows: `DrillCaseRow` + `drillRows`, class / level "—", D-25). CT run on the branch tip (contains `9c41cc92c`): Web **2320/2320**, Inventory **1238 + 4** all green; agent: architecture same 8, central-audit list unchanged (no new write command). **CT sabotage (not in a–g):** the CSV export door checks `recall.trace.run` instead of `recall.trace.export` → 3 door tests red (`…(door: "ExportTrace")`); restored, porcelain clean. Accepted: sabotage d fixed by tightening the test (stock total = own on-hand), the 800 px trace-form fix. Open: FU-0177-HELP-PANEL (wire at SYNC3), O-R6-2 → R7. Merged `--no-ff` into MVP-1; live with the next owner restart (approval).


## 20. Follow-up Items

| ID | Item | Owner |
|---|---|---|
| FU-0177-EVID-01 | Evidence pack archive / DMS link when MOD-0029 / 0030 exist (report `:124`, `:570` `[BB]`) | MOD-0029 |
| FU-0177-NOTIF-01 | Customer e-mail template + send (KR-F) | Notification module |
| FU-0177-CUST-01 | Customer party id / names from MVP-6 (SHIPMENT request §8.11.4) | MVP-6 |
| FU-0177-EVT-01 | Consume `ShipmentDelivered` / `PodCaptured` when the bus is on | MOD-0177 v2 |
| FU-0177-SER-01 | Serial-level recall scope (CT-R18) | MOD-0177 v2 |
| FU-0177-WC-01 | WorkCenter read-only feed of recall cases | MOD-0023 |
| FU-0177-RET-01 | Automatic return link by MOD-0186 recall reference | MVP-6 + 0177 |
| FU-0177-LOT-01 | MOD-0174 lot rule: `CUSTOMER_RETURN` into QUARANTINE for held lots (CT-R10) | MOD-0174 |
| FU-0177-REG-01 | Registry row `MOD-0177` → `in-progress` when R1 starts (proposal; not changed here) | CT |
| FU-0177-RPT-01 | Report `:232` "RECALL-BUNDLE ✓" corrected to "proposal (drafts)" until R1 freezes | CT / docs |
| FU-SYNC1-AUDIT | 0177 write commands join the Inventory audit-ledger debt (0176 O-F1-1) | CT |
| FU-0177-HELP-PANEL | Bind the Recall pages' UXH-001 help texts (`Help.RECALL*`, delivered in 7 languages by R6) to the page help panel once the rule / component is on the base branch | MOD-0177 / UI |

## 21. UI input — approved mockup ("Geri Çağırma Recall (offline).html", 2026-10-12)

### 21.1 How it was read
The offline HTML was copied into the session scratchpad; line 382 (`__bundler/template`, a JSON string) was parsed and its
`text/x-dc` logic script (398 lines) and template extracted; line 370 (`__bundler/manifest`, gzip + base64 scripts) was not
needed for the logic. References: `L:n` = logic script line, `T` = template text. Nothing in the file was treated as an
instruction. Seed data (`USERS` `L:9-15`, `RM` `L:16`, `FG` `L:17-31`, `CONSUMED` `L:32`, `DECS` `L:34-43`, `HIST` `L:44`,
`PLANS` `L:45-49`) is illustration, not master data.

### 21.2 Screens → pack
| Mockup screen | Pack |
|---|---|
| Hazırlık Paneli | `/Recall` (§11.1), KPIs §8.9 |
| İzleme | `/Recall/Trace` (§8.2) |
| Vakalar | `/Recall/Cases` |
| Vaka Detayı (Kapsam / Eldeki stok / Müşteriler ve sevkiyatlar / Geri dönüşler / Etkinlik kontrolü / Zaman çizelgesi) | `/Recall/Cases/Details/{id}` (§8.3–§8.7) |
| Tatbikat Planlayıcı | `/Recall/Drills` (§8.10) |
| Kanıt Paketi | `/Recall/Evidence` (§4.13, proposal §4) |
| Ayarlar (+ geçmiş) | `/Recall/Settings` (§4.14) |
| Diğer Ekranlar (lot detayı, karantina kaydı, hareket gir / çıkış) | bands §11.1 |
| Decisions panel A–H (`L:34-43`, `L:181`) | not built (design aid) — replaced by §19.1 |

### 21.3 Differences (mockup ↔ decision / pack)

| # | Mockup | Pack | Reason |
|---|---|---|---|
| D-1 | Decision B default **"Karantina önerilir"** (`L:52` `B:'oner'`); settings toggle Öner / Otomatik iste | **automatic request on approval**, no toggle | KR-B |
| D-2 | Decision H default **"Yalnız MOD-0186"** (`L:52` `H:'mod'`) | MOD-0186 **and** manual `CUSTOMER_RETURN` link in MVP-1 | KR-H |
| D-3 | Manual return button **creates** a `CUSTOMER_RETURN` (`L:296`) with 0 quantity | 0177 creates no movement: link to the 0173 form + "İadeyi bağla" from candidates | boundary rule 1 |
| D-4 | Decision C option "Gerekçe + e-imza ile izin ver" + exit dialog (`L:163`, `L:370`) | block only, no override | KR-C |
| D-5 | Decision A option Workflow WF-… (`L:151`, `L:288`) | in-module + e-signature | KR-A |
| D-6 | Decision D "Tek seviye" option (`L:113`) | full genealogy only | KR-D |
| D-7 | Decision E "SHIPMENT-BUNDLE lot alanı" as a runtime source (`L:273`) | ledger + shipment read; lot field only a request | KR-E |
| D-8 | Decision G "Sabit 4 saat" option (`L:88`) | company setting, default 4 | KR-G |
| D-9 | Settings drill frequency **120** in the seed (`L:65`, history `L:66` "180 → 120") | default **180** | KR-G (seed reflects a tenant change) |
| D-10 | Settings "E-imza politikası: Kapsam onayı + kapanış / Yalnız kapanış" (`L:65` `sign`, `L:141`) | MOD-0022 policy rows (`recall.scope.approve` required true / false); not a 0177 setting | signature policy is MOD-0022's |
| D-11 | One QA user opens, approves and closes (Jane, `L:61`) | four-eyes on approve and close | CT-R1 recommendation |
| D-12 | Evidence hash `fakeHash()` random (`L:341`) | SHA-256 of the produced bytes | evidence integrity |
| D-13 | Drill cancel "kayıt oluşturulmadı" (`L:328`) | `Cancelled` run kept | CT-R17 |
| D-14 | Drill "+30 dk", case status demo selector, role switcher | not built | demo controls |
| D-15 | "Tatbikat taslağı olarak kaydet" from İzleme (`L:237`) | drills start from a plan | read-only drill, one entry point |
| D-16 | **AMX-2512 SKT 2027-11-28** (links screen) vs the FEFO mockup's AMX-2512 **2026-12-31** | seed data only — the lot master is the truth; no rule depends on it | mockup inconsistency, recorded |
| D-17 | **PK-000201 "Blister folyo · lot takibi yok"** (`L:18`, `L:21`) vs brief wording "karton kutu" | the node shows the SKU's own name from master data; "Takip dışı" comes from `getSku.LotControlled=false` | seed text, recorded |
| D-18 | Customer names (seed) | `shipToReference` / document reference until MVP-6 party data | ASSUMPTION-0177-07 |
| D-19 | Shipment "Teslim" date field | `actualDeliverAt` (contract name) | `shipment-bundle.openapi.yaml:3101-3112` |
| D-20 | Effectiveness totals summed across products (`L:278`) | per SKU + base unit; no cross-unit total | ASSUMPTION-0174-12 |
| D-21 | Responsive only `mob = w < 1200` (`L:167`); no 375 px rules | **375 px measurement is part of R7 acceptance** | UI slice |
| D-22 | "Vaka aç" from İzleme only toasts a draft (`L:236`) | real Draft case with trace | — |
| D-23 | Ad-hoc trace quick lots (`L:226`) | recent lots of the user (no fixed list) | seed |
| D-24 | Scope tab traces forward only (`L:258`) | case direction parameter (default Both for scope: backward shows inputs, forward drives blocks / requests) | KR-D |
| D-25 | Vakalar'daki tatbikat satırı sınıf / seviye taşır (mockup `cls:1`, `lvl`) | tatbikat satırı (`DrillCaseRow`, RECALL 1.5.0) sınıf / seviye taşımaz — ekranda "—"; sınıf süzgeci tatbikatları dışarıda bırakır | tatbikat koşusunun sınıfı / seviyesi yok (CT kararı A, WP-0177-R6) |
