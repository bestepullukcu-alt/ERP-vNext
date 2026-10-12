# Q453 — SOP §18.0 row 13 (observability) for Carriers, Loads, Returns and Claims

- **Lane:** Q453 (ledger Q446), integration-agent, single writer on the four modules' service logging. Recorded 2026-10-05.
- **Preflight:** `Sun Oct  4 22:39:08 UTC 2026` · `feature/mvp6-logistics` · HEAD `cea01354e` · staged 0.
- **Step 0 (sha256/16):**
  - `AGENTS.md` `ce8c12ad80f93bb5`
  - `git-safety.md` `4181c697b96dc9f8`
  - `code-style.md` `6032fbb07819dc47`
  - `integration-agent.md` `f760471e69efb7dc`
  - Q446 `GATE-18-0.tsv` row 13 and `evidence/lane-log-scan.txt`
- **Not touched:** Shipments; any pack; `Program.cs` or the Serilog setup; the enricher. **No logging dependency was added.**
  The change uses the existing `ILogger<T>` and `System.Diagnostics.Stopwatch` from the BCL.
- Nothing staged or committed. No secret printed.

## What each pack requires, and what was there (Q446)

| module | pack row | required | before (Q446 scan) |
|---|---|---|---|
| MOD-0184 Carriers | `:300-302` | scoped **operation**, outcome, correlation, replay/conflict/transaction-failure; **record list size and timings** | outcome line with scope, correlation, status, error code and replay; **no operation, list size or timings** |
| MOD-0185 Loads | `:432` | operation / result / correlation / replay | result line with correlation; **no replay** |
| MOD-0186 Returns | `:437` | operation / result / correlation / replay | result line with correlation; **no replay** |
| MOD-0187 Claims | `:426` | operation / result / correlation / replay | **no result line at all** |

## What changed (`evidence/code-change.diff`, 4 files, +46 / −23)

Each controller already funnels every operation through one wire method, which is where Carriers logged its outcome. The
other three now log there too, in Carriers' shape:

| module | line |
|---|---|
| Carriers | `Carrier outcome {Operation} {TenantId} {LegalEntityId} {CorrelationId} {Status} {ErrorCode} {IdempotentReplay} {ListSize} {ElapsedMs}` (+ Operation, ListSize, ElapsedMs) |
| Loads | `Load outcome {Operation} {Status} {ErrorCode} {IdempotentReplay}` |
| Returns | `Return outcome {Operation} {Status} {ErrorCode} {IdempotentReplay}` |
| Claims | `Claim outcome {Operation} {Status} {ErrorCode} {IdempotentReplay}` |

- **Operation** is the contract `operationId` (`queryCarriers`, `createLoadPlan`, `transitionClaim`, …), the name the
  packs use.
- **Correlation** reuses Shipments' mechanism: `CorrelationIdEnricher` plus the explicit output template at
  `Program.cs:40-42`, which renders `CorrelationId=` on every line. No second mechanism was added. For Returns and Claims
  the value on mutations is the Shipment root, the correlation the request carries.
- **Replay** is the response's own `IdempotentReplay`; it is `False` on every failure.
- **Error code** is the module's code (conflict codes such as `SHIPMENT_ALREADY_ASSIGNED`, and the 503 storage codes),
  which is Carriers' conflict and transaction-failure signal.
- **Timings and list size are Carriers-only**, because only MOD-0184's pack asks for them. `ElapsedMs` is measured from
  before the handler runs (arguments evaluate left to right) to the outcome line. `ListSize` is the list's item count on
  `queryCarriers` and `null` otherwise.

## Proof (live, own stack)

**Stack.** Six services from a repository copy. The lane database is a **copy** of R-4c's stopped lane database (new
folder, `cp`, no `rm`). Loads and Returns were entitled on that STARTER tenant; Loads ran with the Q414 env override.

**Driver.** Per module: one list, one create, then the **same** create (same key, body and correlation)
(`evidence/driver-results.txt`).

### 1. Fixed: every field present, and the replay flag moves (`evidence/outcome-1-fixed.txt`)

```
Carrier outcome queryCarriers  … 4049b989-… 200 null False 1 79.3      CorrelationId=4049b989-…
Carrier outcome createCarrier  … c672286e-… 201 null False null 73.7   CorrelationId=c672286e-…
Carrier outcome createCarrier  … c672286e-… 201 null True null 2.7     CorrelationId=c672286e-…
Load outcome queryLoads 200 null False                    CorrelationId=28669d3a-…
Load outcome createLoadPlan 201 null False                CorrelationId=ffdf3bd3-…
Load outcome createLoadPlan 201 null True                 CorrelationId=ffdf3bd3-…
Return outcome queryReturns 200 null False                CorrelationId=9089beb2-…
Return outcome createReturn 201 null False                CorrelationId=a0e19dd4-…   (the Shipment root)
Return outcome createReturn 201 null True                 CorrelationId=a0e19dd4-…
Claim outcome queryClaims 200 null False                  CorrelationId=1083c002-…
Claim outcome createClaim 201 null False                  CorrelationId=e3896107-…   (the Shipment root)
Claim outcome createClaim 201 null True                   CorrelationId=e3896107-…
```

All four replays show `True` on the second call and `False` on the first. Carriers' list size moves 1 → 2 → 3 → 4 across
runs as carriers are added. Its elapsed time separates a real write (≈ 62–79 ms) from a replay (≈ 2.5 ms).

### 2. Sabotage, one field per module, then restore (`evidence/sabotage.diff`, `outcome-2-sabotage.txt`)

Source edited in the scratch copy only, then SupplyChain rebuilt and restarted:

| module | removed | the line afterwards |
|---|---|---|
| Carriers | `ListSize` | `… 200 null False 66.8` — the list size slot is gone; elapsed time remains |
| Loads | `IdempotentReplay` | `Load outcome createLoadPlan 409 SHIPMENT_ALREADY_ASSIGNED` — no flag |
| Returns | `Operation` | `Return outcome 201 null True` — no operation name |
| Claims | `Status` | `Claim outcome createClaim null True` — no result status |

The Loads sabotage create answered 409, because the fixed run had already assigned the only Draft shipment. That also put
a conflict code on a live line.

**Restored** from the repository (copy equal, 0 sabotage markers), rebuilt and restarted. All fields are back, and all
four replays again read `False` then `True` (`outcome-3-restored.txt`). The driver now creates its own Draft shipment for
Loads, so Loads replays again (`201 null True`).

### 3. Redaction

The three SupplyChain runs and the gateway log were scanned for JWTs, bearer tokens, test-user emails and lane secrets:
**0 / 0 / 0 / 0**. The new lines log only identifiers, codes, flags, counts and milliseconds. No body, amount, reason or
evidence text is logged.

## Suites

The SupplyChain module filters ran on their own mongod (57412, `enableTestCommands=1`). The copy was equal to the
repository's SupplyChain service before the run.

| module | result |
|---|---|
| Shipments | 90 / 0 / 90 |
| Carriers | 36 / 0 / 36 |
| Loads | 33 / 0 / 33 |
| Returns | 80 / 0 / 80 |
| Claims | 132 / 1 / 133 (the known `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`) |
| S&OP | 19 / 0 / 19 |
| Capacity | 48 / 0 / 48 |
| **total** | **438 / 1 / 439**; 472 listed |

**The dispatch's 439/1/440 is one test too many, and the error is in Q447's total row, not in a test.** Q447's own
per-module rows (`mvp6-q447-return-403-test-01/REPORT.md:62-69`) are exactly the ones above, and they sum to 438/1/439. Its
total adds the 7 new cases but not the moved row: "78 − 1 moved row + 3 new cases" in Returns is +2, not +3. So the
correct current figure is **438/1/439**. No unexplained failure occurred, and no failure moved.

## Gate row 13, re-read against Q446's definition

| module | operation | result | correlation | replay | pack-specific | redaction | row 13 |
|---|---|---|---|---|---|---|---|
| Carriers | ✓ | ✓ | ✓ | ✓ | list size ✓, timings ✓, conflict/transaction-failure via error code | ✓ | **MET** (this lane's measurement) |
| Loads | ✓ | ✓ | ✓ | ✓ | — | ✓ | **MET** |
| Returns | ✓ | ✓ | ✓ | ✓ | — | ✓ | **MET** |
| Claims | ✓ | ✓ | ✓ | ✓ | — | ✓ | **MET** |

The verdict is this lane's. Q446's TSV is a sealed record and was not edited. CT re-reads the row.

## Findings

- **F-Q453-1 — the suite figure.** 438/1/439, not 439/1/440 (above).
- **F-Q453-2 — `TenantId=` and `LegalEntityId=` render empty on Loads, Returns and Claims lines.** The template's scope
  slots are filled from the shared request context, which only Shipments and Carriers populate (Carriers sets
  `loggingContext.Scope`, `CarrierContextMiddleware.cs:79-80`). The three packs do not require scope on the log line, so
  row 13 is unaffected. Copying Carriers' two lines into the other three middlewares would close it if CT wants scope
  everywhere. Not done here, because no pack asks for it.
- **F-Q453-3 — no automated test pins these lines.** The proof is live and by sabotage, as the dispatch asked. A
  log-capturing test per module would keep the fields from regressing silently; it is a follow-up, not added here.
- **F-Q453-4 — the lane database was R-4c's, copied.** The tenant, users and Claims data are R-4c's. Loads and Returns
  entitlements were added in the copy only.

## How it was run

- **Scratch:** `~/mvp6-env/q453-20261005-0140/`: repository copy built there; R-4c's `db`, JWT secret and actor password
  files copied (mode 600).
- **Services:** Auth, Platform, MDM, SupplyChain and Gateway on lane mongod 57409 (copied data), all stopped by pid at the
  end. The suite ran on 57412, shut down after.
- **Driver:** calls the gateway as the tenant Admin with a password read from a file, never printed.

Nothing committed, nothing pushed, nothing staged.
