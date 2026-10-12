# MOD-0173 Inventory Ledger & Valuation — Slice S2 (Reservations) + FU-S1-01/02 audit (2026-10-01)

| | |
|---|---|
| Work package | WP-0173-S2 · prompt P-0173-S2 v2 · lane AL-SCE-MOD0173-LEDGER |
| Branch | `feature/sce/mod-0173-inventory-ledger-valuation` (local commits, not pushed) |
| Base | `7f8c7aa9` |
| Authority | module pack `ready-for-dev` §8 / §12 / §13 / §16 + §19 ASSUMPTION-0173-12..19 + §20 FU-S1-01/02 |
| Evidence | [`mod-0173-s2-evidence/`](mod-0173-s2-evidence/README.md) |
| Verdict of this audit | **agent PASS** — not CT ACCEPTED (K13). |

## 1. Environment (re-measured 2026-10-01)

| | Result |
|---|---|
| MongoDB | Windows service, 9.0.2, `hello` → `setName rs0`, `isWritablePrimary true`; transaction start/insert/abort OK. Service config untouched. |
| .NET | SDK 10.0.401, runtime 8.0.31, target `net8.0` |
| Node / Prism | Node v24.19.0; Prism 4.10.5 started as one `node` process (no cmd→npx chain) |
| Python | absent → no Python; DCP-002 verifier not run (waiver W-0173-01) |
| Test DB | `diten_inventory_s1_tests` (DB-010: fixed name, TenantId isolation) |

## 2. Phase 1.5 checklist (pack-only; no deviation)

| # | Check | Answer |
|---|---|---|
| 1 | Pack fields on entities | **Yes** — `InventoryReservation` (item, sku, level, quantity, remaining, status, type, source triple, priority, expiresAt, idempotency key + fingerprint, correlation, createdBy, Version = ETag); `InventorySkuPosition` (`AvailableOnHand`, `Reserved`, `Version`) exactly as ASSUMPTION-0173-13 names them |
| 2 | Naming | **Yes** — contract names; enum `ReservationStatus` → ACTIVE / RELEASED / EXPIRED |
| 3 | Isolation / soft delete | **Yes** — `InventoryReservationRepository.Scope()`, `ReservationPositionOperations` (explicit Tenant + LE in every filter, constructor refuses an empty scope); sweeper / rebuilder discover across tenants but write per Tenant + LE |
| 4 | Entity base | **Yes** — `EntityBase` |
| 5 | CQRS one type per file | **Yes** — `CreateReservationCommand` / `ReleaseReservationCommand`, handlers, validators, parser, fingerprint |
| 6–8 | DataTable / Compact / UI required parity | **N/A** (`shell: none`) |
| 9 | Lookups | **None** |

## 3. Design as implemented (pack ASSUMPTION-0173-12..19)

- **Scope:** reservations at Tenant + LE + item + sku + level against AVAILABLE stock; balance rows keep `reserved 0`.
- **One transaction** for every change: the posting path now ends with the SKU-position step — net AVAILABLE delta
  of the posting; on a net decrease: expire due → consume matching reservation → guard
  `AvailableOnHand − Reserved ≥ 0` (Mongo `$expr` in the update filter). Reservation create: expire due → guard
  `AvailableOnHand − Reserved ≥ q` → insert reservation + audit + outbox. Release: expire due → conditional update on
  `(Status ACTIVE, Version = If-Match)` → position `Reserved −remaining` → audit + outbox.
- **Rebuild:** `SkuPositionRebuilder` at startup (before serving), per-SKU snapshot transaction, idempotent, no deletion;
  documents with incomplete scope are skipped and counted, never guessed.
- **Sweeper:** `ReservationExpirySweeperHostedService` (default 60 s) → `ReservationExpirySweeper` (same code path as
  the in-transaction expiry; system actor; one correlation per sweep).
- **FU-S1-01:** `/availability` with no row → PRODUCT-MASTER `validateRefs` → 200 zeros / 404 / 503.
- **FU-S1-02:** `PermissionAuthorizationHandler` has no role shortcut.

## 4. Acceptance criteria

| AC | Result | Evidence (tests in `d2-inventory-tests.trx`; runtime in `runtime-probe/`) |
|---|---|---|
| AC-R1 reserve 30 of 100 → 201, availableAfter 70; availability 30 / 70 | **PASS** | `ReservationTests.AC_R1…` · `r-s2-reservations.json` |
| AC-R2 over-reserve → 422 INSUFFICIENT_AVAILABLE + details, no write | **PASS** | `AC_R2…` · runtime (available 70.000 / requested 80.000; counts unchanged) |
| AC-R3 parallel 2×60 on 100 → 201 + 422, Reserved 60 | **PASS** | `AC_R3…` (5 rounds) |
| AC-R4 reserved stock not issuable by others | **PASS** | `AC_R4…` (+ STATUS_TRANSFER out of AVAILABLE refused; transfer between AVAILABLE bins allowed) · runtime issue 75 → 422 |
| AC-R5 consumption 30 → issue 20 (ACTIVE, 10) → issue 10 (RELEASED) | **PASS** | `AC_R5…`, `A_holder_can_issue_its_own_reservation…` · runtime partial consumption |
| AC-R6 release: right ETag 200, stale 409, missing 400, other tenant/LE 404 | **PASS** | `AC_R6…`, `A_consumed_reservation_changes_version…`, `ReservationAccessTests` · runtime 409 then 200 |
| AC-R7 expiry — in-transaction and sweeper separately | **PASS** | `ReservationExpiryTests` (read path, decrease, create, release → 409, sweeper with system actor) |
| AC-R8 idempotency replay 200 / reuse 400 | **PASS** | `AC_R8…`, `Concurrent_duplicates_of_one_reservation_key…` · runtime replay |
| AC-R9 invariant + rebuild equality | **PASS** | `ReservationInvariantTests` (3 seeds × 40 steps of 1–3 concurrent mixed operations; independent oracle; rebuild equality; corrupted position restored; S1-era ledger gets its position) |
| AC-R10 isolation + RBAC, reservations.manage required | **PASS** | `ReservationAccessTests` (401, 403 without manage, manage-only works, other tenant / LE 404, CROSS_LE 403, tenantId in query 400) |
| AC-R11 FU-S1-01 200 zeros / 404 / 503 | **PASS** | `ReservationAccessTests` (+ "rows present → no upstream call") · runtime 200 zeros via Prism |
| AC-R12 FU-S1-02 SuperAdmin without permission → 403 | **PASS** | `SuperAdmin_role_without_the_permission_is_403…` (movements.post, reservations.manage, read; control with permission → 201) |
| AC-R13 S1 regression | **PASS** | 218 S1 tests still run; 5 updated with reasons (below), none deleted or skipped; schema validation extended to the reservation endpoints |
| AC-R14 audit + Pending outbox, same correlation | **PASS** | `AC_R14…`, `Consumption_is_audited…` · runtime |

**S1 tests changed (behaviour changed by this WP, reason in the test):** `IsolationTests` ×2 (FU-S1-01: 200 zeros instead
of 404 — isolation still asserted: zeros, never the other tenant's 100), `The_routes_are_exactly_the_six_contract_operations`
(S2 adds two contract operations), `The_service_owns_exactly_the_declared_collections` (+2 collections),
`Only_the_ledger_and_its_projection_carry_quantities` (+ `InventorySkuPosition`, a projection).

## 5. Verification

| # | Result | File |
|---|---|---|
| D1 | `dotnet build … --no-incremental` → 8 projects, **0 Hata / 0 Uyarı** | `d1-build.log` |
| D2 | **262 / 262** passed, **0 skipped** (218 S1 + 44 S2); also stable 3× in a row before evidence | `d2-inventory-tests.trx`, `d2-test-summary.log` |
| D3 | `tests/architecture/TenantArchitecture.ArchitectureTests` **18 / 18** | `d3-tenant-architecture-tests.log` |
| D4 | sabotage ×3: red → restored green, diff empty, full suite 262/262 | `d4-*.log` |
| §R | flows **48/48**, after-restart **6/6**, real restart PID 1120 → 23720, same binary (SHA-256 recorded), no token in logs, no orphan process | `runtime-probe/` |
| D5 | see final report | — |

**D4 detail:**

| Guard | Sabotage | Red |
|---|---|---|
| (a) create `AvailableOnHand − Reserved ≥ q` | condition removed from `TryReserveAsync` | 2 of 44: over-reserve 201 (`availableAfter -10.000`); parallel 60+60 both 201 |
| (b) SKU-level guard on decreases | condition removed from `ApplyAvailableDeltaAsync` | 6 of 44: reserved stock issued (R4), status transfer out of reserved stock, holder test, in-transaction expiry test, invariant `reserved exceeds available on hand` (2 seeds) |
| (c) ETag (version) check | version removed from the release filter | 2 of 44: stale ETag released (R6), creation ETag after consumption released |

## 6. Assumptions (ASSUMPTION-S2-xx)

| ID | Assumption |
|---|---|
| S2-01 | Reservation `quantity` is in the SKU base unit (request has no UoM); `baseQuantity`, when sent, must equal it, else 400. |
| S2-02 | `itemId` / `skuId` must be UUIDs (contract: plain string) — same as movements. |
| S2-03 | No upstream call on reserve: an unknown SKU simply has no available stock → 422 `INSUFFICIENT_AVAILABLE` (available 0); no undeclared 503 on this operation. |
| S2-04 | Consumption only when the reservation has a `sourceDocumentId` and the whole triple (`sourceLineId` null = null) and the SKU match; oldest first; only on a posting's net AVAILABLE decrease. |
| S2-05 | The SKU guard uses the posting's NET AVAILABLE delta: AVAILABLE→AVAILABLE transfers are free; leaving AVAILABLE (issue, status transfer) is guarded. |
| S2-06 | `/availability` with a warehouse / location scope floors `available` at 0 (SKU-level reservations are not located). |
| S2-07 | Unknown / malformed / other-scope reservation id → 404 with an `Error` body without code (no declared code). A malformed `If-Match` can never match → 409, after the 404 check. Missing `If-Match` → 400 before anything. |
| S2-08 | Release 200 body = the contract example shape (`reservationId, status, availableAfter, contractVersion`), validated against `ReservationResponse`. |
| S2-09 | `expiresAt` must be a future UTC timestamp at create, else 400. Reads ignore due reservations immediately; positions count them until expired (in-transaction or sweeper). Releasing a due reservation expires it first → 409. |
| S2-10 | Reservation audit / outbox documents live in `inventory_audit` / `inventory_outbox` as distinct document types (`ReservationId`, event `InventoryReservationChanged`). |
| S2-11 | Undeclared statuses, all pack-mandated: create 200 / 400 / 401 / 403; release 400 / 401 / 403 / 404; availability 503 (ASSUMPTION-0173-18). |
| S2-12 | The sweeper's discovery index `(Status, ExpiresAt)` is deliberately not tenant-first (system scan); every write is tenant-scoped. |
| S2-13 | Startup rebuild skips documents without a complete scope (counted, not deleted) — see NOTE-3. |
| S2-14 | Reservation `Version` starts at 1 (`W/"1"`); every change (consumption, release, expiry) increments it. |
| S2-15 | Permission names compare case-insensitively (template behaviour kept); only the role shortcut was removed. |

## 7. N/A phases, blockers, notes

| Item | State |
|---|---|
| Phase 3.5 gateway | **BLOCKER/NOT — INT-1:** no `/api/inventory/**` route; reservation routes need `If-Match` + `Idempotency-Key` propagation; permission seed incl. `supplychain.inventory.reservations.manage`. |
| DCP-002 | **Waiver W-0173-01** — verifier not run (no Python); must run exit 0 before the MVP-1 PR. |
| Phase 4 / 4a / l10n | N/A (`shell: none`); UI is MOD-0173-FU01. |
| NOTE-1 | No GET reservation in the frozen contract → nothing added (UI pack BG-08 remains). |
| NOTE-2 | `ContractValidatingHandler` allow-list documents every undeclared status with its pack source (S2-11). |
| NOTE-3 | Test database residue (measured, not deleted): 5 `inventory_balances` documents without `TenantId` — left by the S1 D4-3 sabotage run (tenant predicate removed) — and 230 zero-valued `inventory_sku_positions` of already-cleaned test tenants (concurrent test hosts rebuild at startup). Test-only; production code never deletes and the rebuild ignores unscoped documents. |
| NOTE-4 | The rebuild visits every SKU at startup; cost grows with the number of SKUs — acceptable for MVP-1, revisit with volume (follow-up). |

## 8. Proposed update of the MOD-0173 row in `module-implementation-status.md` (not written — CT records it)

```
| MOD-0173 Inventory Ledger & Valuation | Backend (S1+S2) | 50 | S1 ledger core (CT-ACCEPTED f4ec3892) + **S2 reservations**: POST /reservations, /reservations/{id}/release (Idempotency-Key, If-Match/ETag, 409/422 per contract); SKU-level holds; `inventory_sku_positions` projection in the same Mongo transaction as every AVAILABLE leg and reservation change; reserve guard + reserved-stock guard on decreases; consumption by source triple; expiry in-transaction + sweeper; startup rebuild; audit + Pending InventoryReservationChanged outbox. FU-S1-01 (availability 200 zeros / 404 / 503) and FU-S1-02 (no SuperAdmin bypass) closed. 262 tests (0 skipped), sabotage proof ×3, runtime probe 48+6 incl. restart. Build 0/0. | **INT-1** gateway route + permission seed (incl. reservations.manage); **S3** valuation (additive contract v1.1); UI MOD-0173-FU01 (draft); DCP-002 verifier re-run (W-0173-01); GET reservation not in contract (UI BG-08); real MOD-0290 / Location master. |
```
