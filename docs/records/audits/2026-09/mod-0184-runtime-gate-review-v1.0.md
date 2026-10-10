# MOD-0184 runtime approval gate — v1.0 / 2026-09-16

## 1. Verdict / authority

**Agent Verdict: review complete. Runtime gate: BLOCKED. DEV dispatch: NO-GO.**
Preparation is complete; this is a governance decision, not runtime implementation or acceptance.
The user's current instruction delegates review and approval within existing boundaries. It does not
permit changing frozen contracts or acting for an unrecorded contract/security-owner decision.
Five bounded design decisions below are APPROVED; two externally observable contract decisions remain
BLOCKED. APPROVED design rows do not independently release a writer, Phase 2, or Program.cs edits.

Authority: AGENTS.md §§1/4/7/10; domain-config ownership; pack §§21–27;
Control Tower SOP §4.1–4.2 (lines 324–353), §18 contract blocker, §22 report and K12;
/add-module Phase 0/1.5. Applied orchestrator skill and canonical orchestrator workflow.
The conditional approval instruction in the current request is sufficient to decide within scope;
no additional routine confirmation is requested. The condition for full release is not met.

## 2. Seven exact decisions

Contract references below are to `docs/analysis/contracts/shipment-bundle.openapi.yaml`.

| GAP | Exact reviewed proposal / disposition | Frozen compatibility and reason | Decision |
|---|---|---|---|
| GAP-184-02 lifecycle | Create Active. Active→Suspended/Retired and Suspended→Active/Retired succeed. Each same-state request with a new key and every transition from Retired returns 422 `INVALID_CARRIER_TRANSITION`. An authorized exact replay is resolved before current-state validation. | Lines 235,244,267: Active creation example, Active↔Suspended, terminal Retired, declared 422 business error. This completes module-local business policy without adding states, fields or endpoints. Error.code is an open string; no enum amendment. Replay transport remains subject to 05. | **APPROVED**, bounded lifecycle design |
| GAP-184-03 uniqueness | Preserve carrierCode/displayName exactly. Unique code by tenant+LE with ordinal case-sensitive comparison, including retired/deleted records; never reuse a reserved code. Distinct case remains distinct. Duplicate create returns 409 `CARRIER_CODE_CONFLICT`. | Lines 237,932–940: create already declares Conflict for idempotency OR uniqueness; new open-string error code fits the existing Error schema. This selects the prepared pack's local uniqueness rule; no trim/case normalization or tighter input schema. | **APPROVED**, local rule and create error |
| GAP-184-04 errors/headers | Adopt the explicit proposed matrix and precedence in §3 only after contract/security-owner disposition. No automatic adoption of Shipment middleware's nil-UUID rejection or key trimming. | GET lists only 200 (210–216); create 201/409/422 (229–238); status 200/404/422 (255–267). Repo auth/scope rules require failure handling, but do not settle the complete Carrier error envelope, correlation fallback, precedence and undocumented response mapping. Shared response definitions are not automatically operation responses. | **BLOCKED**, contract + security owner |
| GAP-184-05 durable replay | Scope key by tenant+LE+operation+target (fixed create target); fingerprint validated semantic payload preserving text, mode order and duplicates, treating absent/null externalReference equally. Exclude correlation. Store original result durably; replay returns original result plus replay=true with 201/200, without audit duplication/state overwrite. Retain indefinitely initially. Changed payload proposes 409 `IDEMPOTENCY_KEY_REUSED`; original audit correlation retained, current request correlation proposed for response header. | Create409 and idempotentReplay fit the schema. Status409 is not declared at 255–267; cross-correlation response-header semantics are unspecified. Do not reinterpret a payload conflict as an approved422 merely to fit the listed statuses. Absence alone is not proof of a breaking change; owner must classify clarification vs amendment. | **BLOCKED**, contract owner; internal design endorsed but whole replay gate open |
| GAP-184-06 scope/composition | Carrier-only context from validated JWT tenant_id/legal_entity_id/sub and matching scope headers; no body/query-selected scope. Operation permissions read/create/status.change. Exact pack §23 file allowlist and minimal composition design in §4; one future DEV writer. | Frozen bundle's server-resolved tenant/LE rule plus security-jwt header/claim match and fail-closed isolation are preserved. Wire adapter uses frozen responses, not a new outer envelope. Invalid context's external response remains 04. | **APPROVED**, design boundary only; no current source write grant |
| GAP-184-07 persistence | L3 replica-set Mongo transaction writes carrier+replay+audit atomically. Unique code and scoped replay indexes per pack §24; internal CAS/version and reread/revalidation on retry. Specific Carrier repository exception approved for atomic multi-document mutation, scoped reads/writes and soft-delete enforcement. No Carrier event/outbox invented. | No wire field/client-version change. REPO-001 permits justified specific repository; EntityBase and existing MongoClient reused. Rollback before commit leaves all three unchanged; lost response after commit recovers through replay. Failure HTTP mapping remains 04; replay transport remains05. | **APPROVED**, storage architecture; dependent release still blocked |
| GAP-184-08 integration | Future direct-service bounded E4 with real JWT validation and permission claims. Gateway/shared permission catalog and provisioning require a separate integration-owner WP; no edits here. | Exactly the existing Carrier surface, no live Shipment ingress, stock movement or Supplier ownership. Does not claim production access, integration-owner completion, E5/G5 or full-module acceptance. | **APPROVED**, bounded evidence target only |

ASSUMPTION-184-C: The user's delegated approval covers the proposed local lifecycle, uniqueness and
storage choices, not amendment of the shared frozen wire contract. No record appoints this lane as its owner.
ASSUMPTION-184-D: An omitted OpenAPI response is an unresolved documentation/behavior decision here,
not automatically a breaking change. No compatibility waiver or inferred default response is granted.
Existing ASSUMPTION-184-A/B remain: externalReference is opaque, Supplier is MOD-0140; Warehouse
trigger is not a Carrier runtime prerequisite. No new Supplier relation or consumer lookup is introduced.

## 3. Concrete owner disposition required for 04/05

The following is an **exact recommendation, not approved runtime semantics**:

| Condition | Recommended status/code | Operations |
|---|---|---|
| Missing/invalid JWT | 401 / INVALID_REQUEST, Bearer challenge | all three |
| Missing operation permission or unusable/duplicate signed scope/actor claims | 403 / INVALID_REQUEST | all three |
| Missing/malformed required correlation/scope header; invalid body/query/path schema | 400 / INVALID_REQUEST | applicable operations |
| Scope-header mismatch against valid signed context | 404 / CARRIER_NOT_FOUND, no resource lookup/disclosure | all three |
| Unknown/cross-tenant/cross-LE/soft-deleted target | 404 / CARRIER_NOT_FOUND | status |
| Reserved same-scope code | 409 / CARRIER_CODE_CONFLICT | create; local rule approved in03 |
| Same scoped key, different validated payload | 409 / IDEMPOTENCY_KEY_REUSED | create and status; status declaration unresolved |
| Invalid lifecycle/new-key same-state | 422 / INVALID_CARRIER_TRANSITION | status; local rule approved in02 |
| Transient persistence unavailability after bounded safe retry | 503 / PERSISTENCE_UNAVAILABLE | all three |
| Unexpected internal failure | 500 / INTERNAL_ERROR | all three |

Proposed processing precedence: authenticate → validate signed context and operation permission →
validate required headers/matching scope → validate operation schema → scoped replay lookup →
current-state/uniqueness check → atomic persistence. No replay access before current authorization.
Use frozen Error shape (including contractVersion v1); sanitize error details, never expose stack,
connection strings or cross-scope identifiers. On startup/index/transaction-capability failure, do not
expose Carrier routes as healthy; do not falsely claim an HTTP response if startup never completed.

Proposed correlation handling: accept all schema-valid UUIDs, including nil (no nonzero restriction in
lines 517–522). For a valid supplied UUID preserve its value in error body and response header. For a
missing/invalid header, generate a UUID only for rejection trace, clearly not an accepted mutation root;
use it in that error body/header. Auth errors follow the same safe trace rule. A successful replay leaves
original persisted audit correlation unchanged and echoes the currently supplied UUID in response header.
No Carrier lifecycle event exists, so no event-root binding may be inferred from Shipment.

Proposed key handling: enforce only schema length1..128; no application trim or whitespace-only ban.
Transport parser normalization must be captured separately in future evidence; do not claim raw bytes
survive HTTP parsing. Preserve accepted code/name/reason text and mode ordering/duplicates;
empty reasonCode is allowed. No silent use of Shipment's stricter validator.

**Required owner artifact:** SHIPMENT-BUNDLE contract owner, with security owner for auth/scope and
correlation rejection, must record the above matrix/precedence/header policy against FROZEN1.0.0:
(a) explicitly classify and authorize it as a compatible normative clarification, or (b) provide an approved
versioned contract amendment and consumer compatibility decision in a separate authorized owner WP.
In particular resolve status409 and all missing failure responses. This lane changes neither contract nor
README. If owner chooses a different mapping, update acceptance and new prompts before DEV; do not
force a generic422 workaround or accept an incomplete happy-path implementation.

## 4. Phase 1.5, owned paths and Program.cs

Pack §23 exact feature-file allowlist is **approved as a design boundary**, without broader parent-folder
ownership. Domain/Application/Api/Persistence/Infrastructure Carrier files, six Carrier test files,
three Carrier probe files, and distinct DEV/VER evidence directories are the intended scope.
The listed filenames/roots remain authoritative; additions require versioned scope review.
No file in that runtime allowlist is writable under this review task.

The only prospective existing-source exception is
`services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs`:

1. Register CarrierRequestContext and the Carrier persistence registration extension using the existing
   MongoClient/database; no new connection/client/fallback.
2. Select Carrier middleware only for exact `/api/shipment-bundle/carriers` route segments and its
   declared status route; never a string prefix matching `carriersXYZ`. Existing Shipment middleware
   continues for the other bundle paths; its source bytes remain protected.
3. If needed, map invalid-model errors only for Carrier actions after04 closes. No global error rewrite.

No health/auth/JWT/CORS/outbox/source-intake startup rewrite; no shared DI, csproj, ShipmentSchema,
Shipment context, base controller or permission-attribute edits. Existing assembly scans are reused.
One future DEV writer only. This approves the proposed limit, **not an executable exception today**.

Phase1.5 disposition: checks1/2/3/4/5/9 design APPROVED; 6/7 N/A (backend-only);
check8 required/error parity remains BLOCKED by04. Replay boundary05 additionally blocks end-to-end
consistency. **Overall Phase1.5 HELD**, not completed approval. Pack remains **draft**; no ready-for-dev
promotion, no DEV READY prompt. HELD v1.0 DEV and VER prompts remain byte-identical; no replacement
version is issued while these decisions remain open. Later VER release must bind a completed DEV
handoff, exact diff/hash manifest, binary/runtime provenance and independent verifier.

## 5. Fresh baseline and preservation

Measured UTC: 2026-09-16T17:44:59.480264+00:00.
Repository: `/Users/natig/Projects/ERP-vNext-recovery`.
Branch: `feature/mvp6-logistics`; HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
Staged diff empty. This is a review baseline, not a future dispatch baseline; remeasure before DEV.

Expected pre-existing dirty files (all preserved except additive review update to the pack):

- Modified: `execution/domains/supply-chain-execution/module-packs/MOD-0184-carrier-management.md`.
- Untracked: `docs/records/audits/2026-09/mod-0184-prep-01-approval-v1.0.md`.
- Untracked: `docs/records/audits/2026-09/mvp6-continuation-review-2026-09-16/` containing provenance.json,
  ver-04-report.md, ver-05-report.md and ver-06-report.md.
- Untracked: `docs/records/audits/2026-09/mvp6-mod0183-ct-review-01-2026-09-16.md`.
- Untracked: `docs/roadmap/plans/mod-0184-dev-01-prompt-v1.0.md` and mod-0184-ver-01-prompt-v1.0.md.
- Untracked: `docs/roadmap/plans/mvp6-logistics-continuation-2026-09-16.md`.

Protected: all other baseline files, frozen contracts, .antigravity, MOD-0183 source/evidence,
Platform/HCM/Talent and all other modules, gateway, registry, DCP and central historical records.
14,260 tracked/untracked existing files were hashed before edits (temporary measurement
`/private/tmp/mod0184-runtime-gate-baseline.json`; not runtime evidence).

| Preserved artifact | SHA-256 |
|---|---|
| SHIPMENT-BUNDLE | f6415bbfda42a61a9845e7e1fc843be087bf249cac6bcdc9cb678284766450a1 |
| DEV HELD v1.0 | c07d81f9732dddb8221852de8254f4a886903d347191a60811cfcf606bee520c |
| VER HELD v1.0 | 56f31fb7ccba2489b21a2203a6b52f432701e649ab764079e3a8bb4fc0cb6ae8 |

## 6. SOP §22 handoff / evidence limits

Changed files in this task only:

1. This new decision report: `docs/records/audits/2026-09/mod-0184-runtime-gate-review-v1.0.md`.
2. Pack: status_note and additive §28 decision pointer; preserve prep content as historical proposal.

Contract flow reviewed: scoped caller → auth/schema → replay/current-state → atomic carrier/replay/audit
→ frozen response. Failure paths reviewed: invalid scope/auth/schema, payload conflict, concurrent lifecycle,
rollback and postcommit response loss. No implementation created or runtime reproduced.
Tests: documentation/identity/integrity validation only; no service build/test/HTTP/Mongo execution.

Validation results:

- `python3 -B .antigravity/scripts/verify_module_id.py . --check-id MOD-0184 --name 'Carrier Management'`: exit0, Blueprint/registry PASS.
- `git diff --check`: exit0. Staged diff empty; HEAD unchanged.
- Fresh-baseline SHA-256 comparison: 14,259/14,260 existing files unchanged; only the owned pack changed.
  Exactly one new file (this report). Frozen contract and both HELD prompts match §5 hashes.
- Historical `python3 -B services/Diten.SupplyChainService/tests/verify_evidence.py . docs/records/audits/2026-09/mod-0183-r1-evidence/baseline-preservation.json.txt docs/records/audits/2026-09/mod-0183-r1-evidence/changed-files.json`:
  **exit1**, historical protected hash differs for MOD-0184 pack. This divergence already existed at this
  review's baseline (PREP changes); it is not concealed or repaired by changing historical evidence.
- Separate SHA-256 measurement: historical protected set matches HEAD **172/172**; current worktree
  matches **171/172**, with only that pack different. Current MOD-0183 delivery manifest has151 unique
  entries and all150 non-self hashes match. No claim that the historical all-protected verifier passed
  against the current prepared worktree.

Persistence/security evidence for Carrier: design only, no E4 achieved. No independent VER verdict claimed.
MOD-0183 prerequisite: central report records bounded E4 ACCEPTED; architecture baseline remains
**15 PASS / 3 deferred external FAIL**, not waived or rerun here. VER-06 is packaging, not runtime rerun.
Observability plan: scoped outcome/correlation/replay/conflict/transaction signals, no credentials/raw payloads.
Rollback plan: disable only future Carrier composition, preserve Carrier data/audit; no destructive migration.
Out-of-scope changes: none. No runtime code, staging, commit, push, stash or branch switch.

Next gate: receive the concrete04/05 owner disposition; reconcile Phase1.5 check8 and acceptance;
then explicitly promote pack and issue versioned DEV READY/VER handoff-dependent prompts with fresh
baseline and writer assignment. This review does not authorize partial runtime work around the blockers.
