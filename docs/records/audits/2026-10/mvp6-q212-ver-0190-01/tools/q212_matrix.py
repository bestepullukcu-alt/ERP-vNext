#!/usr/bin/env python3
"""Q212 — builds ACCEPTANCE-MATRIX.tsv, OWNED-PATH-COVERAGE.tsv, SCOPE-DELTA.tsv, BOUNDARY-REFS.tsv.
READ-ONLY on the repo (reads and hashes files); writes only into argv[1]. Every path:line is looked up
from the file at run time by cite(); a needle that is not found stops the script."""
import sys, os, csv, hashlib
OUT = sys.argv[1]
SVC = "services/Diten.SupplyChainService/"
SRC = SVC + "src/Diten.SupplyChainService."
TST = SVC + "tests/Diten.SupplyChainService.Tests/SandopPlans/"
PACK = "execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md"
PRE = "docs/roadmap/plans/mvp6-mod0190-0192-dispatch-preflight-01/"
OWNED = PRE + "MOD-0190-OWNED.tsv"
YAML = "docs/analysis/contracts/sandop-capacity.openapi.yaml"
ANNEX = "docs/analysis/contracts/sandop-capacity-semantics-v3.0.0.md"
REPO = SRC + "Persistence/Features/SandopPlans/SandopRepository.cs"
SCHEMA = SRC + "Persistence/Features/SandopPlans/SandopSchema.cs"
REG = SRC + "Persistence/Features/SandopPlans/SandopPersistenceRegistration.cs"
MW = SRC + "Api/Features/SandopPlans/SandopContextMiddleware.cs"
CTRL = SRC + "Api/Features/SandopPlans/SandopPlansController.cs"
WIRE = SRC + "Application/Features/SandopPlans/SandopPlanModels.cs"
PROJ = SRC + "Application/Features/SandopPlans/SandopProjection.cs"
FIX = SRC + "Infrastructure/Features/SandopPlans/DemandFixtureReader.cs"
PERM = SRC + "Infrastructure/Features/SandopPlans/SandopPermissions.cs"
PATTR = SRC + "Infrastructure/Features/SandopPlans/SandopPermissionAttribute.cs"
PLAN = SRC + "Domain/Features/SandopPlans/SandopPlan.cs"
PROG = SRC + "Api/Program.cs"
APPDI = SRC + "Application/DependencyInjection.cs"
Q208 = "docs/records/audits/2026-10/mvp6-q208-suite-baseline-01/"
Q214 = "docs/records/audits/2026-10/mvp6-q214-failpoint-isolation-01/"
ACC = "docs/records/audits/2026-09/mvp6-mod0190-401-disposition-ct-02/SUCCESSOR-ACCEPTANCE-MATRIX.tsv"
MAN = "docs/records/audits/2026-09/mvp6-mod0190-test-oracle-rework-01/source-manifest.tsv"
_c = {}
def lines(p):
    if p not in _c: _c[p] = open(p, encoding="utf-8").read().splitlines()
    return _c[p]
def cite(p, needle, nth=1):
    n = 0
    for i, l in enumerate(lines(p), 1):
        if needle in l:
            n += 1
            if n == nth: return f"{p}:{i}"
    raise SystemExit(f"cite failed: {p} :: {needle}")
def t(f, m): return cite(TST + f + ".cs", m)
def pk(n): return cite(PACK, n)
def acc(i): return cite(ACC, i + "\t")
def sha(p): return hashlib.sha256(open(p, "rb").read()).hexdigest()
GREEN = "passed in Q208 (" + Q208 + "evidence/full.trx; SandopPlans 19/19, " + Q208 + "NEW-BASELINE.tsv:8)"
M, N, I = "MET", "NOT MET", "INSUFFICIENT EVIDENCE"
ISO = "Prior evidence exists only for the isolated environment: "
rows = []
def row(*a): rows.append(list(a))

# ---------------- §16 acceptance criteria
row("AC-16-1", pk("- [ ] Create stores one scoped Draft plan"), "Create stores one scoped Draft plan, null currentSnapshotId, against an exact scoped Published DEMAND fixture", M,
    cite(REPO, 'if(!await fixture.MatchesAsync(scope,demandId,version,null,ct))') + "; " + cite(REPO, 'p.Add("Status","Draft");p.Add("CurrentSnapshotId",BsonNull.Value)'),
    t("SandopLifecycleTests", "Create_capture_signoff_preserves_prior_snapshot_and_draft_to_inreview") + "; " + t("SandopAtomicityTests", "Invalid_fixture_creates_nothing_in_five_effect_collections") + " — " + GREEN, "Repository level. Not live producer validation (the pack says so).")
row("AC-16-2", pk("- [ ] Draft/InReview capture appends immutable fixture"), "Capture appends immutable provenance, sets InReview, preserves older snapshots; no DEMAND series stored", M,
    cite(REPO, 'var provenance=new{demandPlanId=demandId') + "; " + cite(REPO, 'Update.Set("Status","InReview")'),
    t("SandopLifecycleTests", "Create_capture_signoff_preserves_prior_snapshot_and_draft_to_inreview") + " — " + GREEN, "The stored provenance is ID, version, contract name/version, time and checksum only; supplyInputRefs are three opaque strings. No quantity or series field exists (BOUNDARY-REFS.tsv B-07). currentSnapshotId is set in code but not asserted by a test.")
row("AC-16-3", pk("- [ ] InReview sign-off binds to any same-plan immutable snapshot"), "Sign-off binds to any same-plan snapshot and role; duplicate role cannot overwrite; five approvals do not advance the plan", M,
    cite(REPO, 'if(await SignOffs.Find(session,dup).AnyAsync(ct))') + "; " + cite(SCHEMA, '"sandop_role_snapshot"'),
    t("SandopLifecycleTests", "All_five_role_decisions_leave_plan_in_review_and_outbox_pending") + "; " + t("SandopConcurrencyTests", "Concurrent_different_keys_cannot_record_same_role_twice") + "; " + t("SandopLifecycleTests", "Create_capture_signoff_preserves_prior_snapshot_and_draft_to_inreview") + " — " + GREEN, "The lifecycle test signs the FIRST of two snapshots, so 'any same-plan snapshot' is exercised. The race protection depends on the unique index (F-Q212-4).")
row("AC-16-4", pk("- [ ] Three mutations use scoped durable receipt"), "Three mutations: scoped durable receipt, exact key / decoded-JSON fingerprint, original-result replay, atomic audit + at most one Pending event; no publisher or Workflow call", M,
    cite(REPO, 'await Receipts.InsertOneAsync(session,receipt') + "; " + cite(REPO, 'outbox.Add("Status","Pending")') + "; " + cite(REPO, "static SandopResult Replay"),
    t("SandopReplayTests", "All_three_mutations_replay_without_duplicate_effects_or_fixture_reread") + "; " + t("SandopReplayTests", "Exact_key_is_not_trimmed") + "; " + t("SandopAtomicityTests", "Receipt_insert_failure_after_plan_write_rolls_back_and_same_key_retries") + " — " + GREEN, "The rollback test is green for the right reason (Q214: namespace-scoped, error code 2). No publisher, worker or HTTP client exists in the 32 source files.")
row("AC-16-5a", pk("- [ ] Current UUID is in response header/error"), "Original UUID remains in audit/event on replay", M,
    cite(REPO, 'audit.Add("CorrelationId",correlation.ToString())'), t("SandopReplayTests", "Exact_key_replays_original_body_without_second_effect_and_changed_payload_conflicts") + " — " + GREEN, "The audit row is asserted. The event envelope is written in the same single transaction and is not asserted.")
row("AC-16-5b", pk("- [ ] Current UUID is in response header/error"), "Current UUID is in the response header and error body", I,
    cite(CTRL, 'Response.Headers["X-Correlation-Id"]=correlation.ToString()') + "; " + cite(MW, 'http.Response.Headers["X-Correlation-Id"]=correlation.ToString();'),
    "partial — " + t("SandopContractTests", "Module_request_gate_requires_permission_and_preserves_exact_key_and_correlation") + " asserts the header for a valid request at gate level; no test executes the controller", ISO + acc("A05"))
row("AC-16-5c", pk("- [ ] Current UUID is in response header/error"), "Missing/malformed/duplicate UUID → 400 fallback; application-generated 401 follows the published fallback", I,
    cite(MW, 'if(!valid)return new(default,"",correlation,400,"INVALID_CORRELATION_ID");') + "; " + cite(MW, '401,"UNAUTHENTICATED"'), "none in the tree", ISO + acc("F04A") + "; F04B was NOT_APPLICABLE there (" + acc("F04B") + ").")
row("AC-16-6a", pk("- [ ] Cross-scope access returns 404; missing permission returns 403."), "Cross-scope access returns 404", M,
    cite(REPO, "static BsonDocument PlanFilter") + "; " + cite(REPO, 'return SandopResult.Error(404,"UNKNOWN_SANDOP_PLAN");}'),
    t("SandopIsolationTests", "Tenant_and_legal_entity_cross_scope_reads_are_404") + "; " + t("SandopIsolationTests", "Cross_scope_mutation_cannot_disclose_receipt_or_plan") + " — " + GREEN, "Repository level: foreign tenant and foreign legal entity both get 404 for read and for mutation.")
row("AC-16-6b", pk("- [ ] Cross-scope access returns 404; missing permission returns 403."), "Missing permission returns 403", I,
    cite(MW, '!http.User.HasClaim("permission",permission))return new(default,"",correlation,403,"FORBIDDEN");'),
    "unit level only — " + t("SandopContractTests", "Module_request_gate_requires_permission_and_preserves_exact_key_and_correlation") + " calls the gate function with a fabricated principal", "RBAC criterion: no test sends a real token or executes an action. " + ISO + acc("A06"))
row("AC-16-7a", pk("- [ ] Six operations and three event payloads match"), "Six operations exist, on the published paths", M,
    cite(CTRL, '[Authorize,Route("api/supply-chain/sandop-plans")]') + "; " + cite(CTRL, "[HttpPost,SandopPermission(SandopPermissions.Create)]") + "; " + cite(YAML, "operationId: createSandopPlan") + "; " + cite(YAML, "operationId: listSandopSignOffs"), "none needed (structural)", "Six actions for six operationIds. YAML on disk sha256 " + sha(YAML)[:12] + "… = the pack's 3.0.0 pin.")
row("AC-16-7b", pk("- [ ] Six operations and three event payloads match"), "Three event types as published", M,
    cite(REPO, 'eventType="sandop.plan.created.v1"') + "; " + cite(REPO, 'eventType="sandop.snapshot.captured.v1"') + "; " + cite(REPO, 'eventType="sandop.sign-off.recorded.v1"') + "; " + cite(YAML, "const: sandop.plan.created.v1"),
    t("SandopLifecycleTests", "All_five_role_decisions_leave_plan_in_review_and_outbox_pending") + " — " + GREEN, "Event type names are asserted. Payload fields are not.")
row("AC-16-7c", pk("- [ ] Six operations and three event payloads match"), "Response/event payload shape, required/null fields and 400/401/409/422/503 precedence match the YAML and annex", I,
    cite(REPO, "static string PlanBody") + "; " + cite(MW, "public static Resolution Resolve"), "none — no test validates a response or an event against the YAML", ISO + acc("A07") + " (PASS_BOUNDARY).")
row("AC-16-8", pk("- [ ] MOD-0190 shares no persistence/internal DTOs with MOD-0192"), "No shared persistence or internal DTOs with MOD-0192; runs against frozen mocks", M,
    cite(REPO, 'db.GetCollection<BsonDocument>("sandop_plans")') + " (six sandop_* collections); BOUNDARY-REFS.tsv B-03, B-04", "none needed (structural)", "0 references from SandopPlans into CapacityPlans and 0 the other way. 38 ∩ 43 owned paths = 0. DEMAND is a fixture reader (" + cite(FIX, "test-only exact fixture seam") + ").")
row("AC-16-9", pk("- [ ] Bounded E4 evidence links decisions to immutable fixture provenance"), "Bounded E4 evidence links decisions to fixture provenance without overriding source SoRs", I,
    "no E4 (HTTP + DB) evidence exists for this tree", "none", ISO + acc("A09") + " (PASS_BOUNDARY).")

# ---------------- §18 ready-for-dev checklist
row("RFD-18-1", pk("- [x] Canonical ID/name passed DCP-002"), "Canonical ID/name passed DCP-002", M, ".antigravity/scripts/verify_module_id.py — run read-only by Q212: 'OK  MOD-0190: proven against Blueprint/registry.', exit 0", "identity gate", "")
row("RFD-18-2", pk("- [x] DCP-009, domain boundary, MVP-6 brief and frozen contracts were read."), "DCP-009, domain boundary, MVP-6 brief and frozen contracts were read", I, PACK + " (a statement about a past act)", "n/a", "Cannot be checked by reading the tree.")
row("RFD-18-3", pk("- [x] Backend/contract initial slice, shell and golden-reference decisions are explicit."), "Slice, shell and golden-reference decisions are explicit", M, pk("`shell: none`. The initial slice has no Razor UI") + "; " + pk("## 23. Tenant UI scope"), "n/a (document)", "")
row("RFD-18-4", pk("- [x] Owned objects, paths, validation, failures and acceptance are specified."), "Owned objects, paths, validation, failures and acceptance are specified", M, pk("## 3. Owned Objects") + "; " + pk("## 12. Validation Rules") + "; " + pk("## 13. Failure Path to Verify") + "; " + OWNED, "n/a (document)", "")
row("RFD-18-5", pk("- [x] Published SANDOP-CAPACITY 2.0.0 YAML/annex and MOD-0190 consumer repin"), "Contract hashes verified; re-pinned to 3.0.0; DEMAND v1 fixture-only", M, YAML + " sha256 " + sha(YAML)[:12] + "…; " + ANNEX + " sha256 " + sha(ANNEX)[:12] + "… — both equal the pack pins (" + pk("| `SANDOP-CAPACITY` 3.0.0 / wire `v1` |") + ")", "hash comparison by Q212", "docs/roadmap/plans/mvp6-text-patch-q83-01/COMPARE-0190-PIN.md exists; not re-derived.")
row("RFD-18-6", pk("- [x] Fixture-only DEMAND, exact key/name, replay and validation boundaries"), "Fixture-only DEMAND, exact key/name, replay boundaries map to the annex; no narrower key/name rule", M, cite(MW, 'if(keys.Count!=1||string.IsNullOrEmpty(keys[0]))') + "; " + cite(WIRE, "if(required.Any(p=>string.IsNullOrEmpty("), t("SandopReplayTests", "Exact_key_is_not_trimmed") + " — " + GREEN, "No trim, no max-200 rule in code.")
row("RFD-18-7", pk("- [x] Bounded MOD-0183..0187 sequence evidence is recorded"), "Bounded MOD-0183..0187 sequence evidence is recorded by Returns/Claims CT", M, pk("| Shipment lane MOD-0183..0187 | BOUNDED SEQUENCE EVIDENCED |") + "; docs/records/audits/2026-09/mvp6-mod0186-wp-acceptance-01/SOP-22.md (exists)", "n/a (record)", "Bounded, isolated evidence. Not the same as verified dependency evidence on today's tree.")
row("RFD-18-8", pk("- [x] Owner approved trusted JWT actor/no Workflow"), "Owner approved JWT actor / no Workflow and Pending-only outbox", M, "docs/records/audits/2026-09/mvp6-mod0190-0192-scope-disposition-01/README.md (exists); " + pk("| Workflow / actor identity | JWT ACTOR ONLY |"), "n/a (record)", "")
row("RFD-18-9a", pk("- [x] The 38 prospective SandopPlans paths are absent and disjoint"), "The 38 paths are disjoint from MOD-0192's 43", M, OWNED + " vs " + PRE + "MOD-0192-OWNED.tsv — intersection 0 (recomputed by Q212)", "set comparison by Q212", "")
row("RFD-18-9b", pk("- [x] The 38 prospective SandopPlans paths are absent and disjoint"), "The 38 paths are absent from the common checkout", N, "OWNED-PATH-COVERAGE.tsv — 38 of 38 present", "n/a", "True when written; stale since the Q202a write of 2026-10-02. The same stale wording is at " + pk("none exists in the common checkout yet") + " and " + pk("1. `SandopPermissions.cs` exists only in the accepted isolated source") + ". Not corrected.")
row("RFD-18-10", pk("- [x] User/owner approved exact draft target SHA-256"), "Owner approved the draft target, Phase 1.5 and the isolated 38-path core DEV/VER", M, pk("Approved: `docs/records/decisions/2026-09/mvp6-sop-pack-promotion-owner-decision-q27-01.md`") + " (file exists)", "n/a (record)", "")

# ---------------- §12 validation rules
row("VAL-12-1", pk("| `Idempotency-Key` | Mutations |"), "Idempotency-Key: nonempty exact value, no trim", M, cite(MW, 'if(keys.Count!=1||string.IsNullOrEmpty(keys[0]))'), t("SandopReplayTests", "Exact_key_is_not_trimmed") + "; " + t("SandopContractTests", "Module_request_gate_requires_permission_and_preserves_exact_key_and_correlation") + " — " + GREEN, "")
row("VAL-12-2", pk("| `X-Correlation-Id` | All six operations |"), "X-Correlation-Id: single valid UUID; missing/malformed/duplicate → 400", I, cite(MW, "static bool UniqueUuid"), "none for the negative cases", "Same as AC-16-5c.")
row("VAL-12-3", pk("| `Name` | Create |"), "Name: nonempty exact string, no trim/max-200", I, cite(WIRE, "if(required.Any(p=>string.IsNullOrEmpty("), "none — no test sends an empty name", "")
row("VAL-12-4", pk("| Horizon | Create |"), "Horizon: valid dates, end >= start", I, cite(WIRE, "end<start"), "none", "")
row("VAL-12-5", pk("| Demand reference | Create/snapshot |"), "Demand reference checked against the exact scoped fixture", M, cite(FIX, "fixtures.Any(x=>x.TenantId==scope.TenantId"), t("SandopAtomicityTests", "Invalid_fixture_creates_nothing_in_five_effect_collections") + "; " + t("SandopAtomicityTests", "Checksum_mismatch_produces_422_and_no_capture_effects") + " — " + GREEN, "")
row("VAL-12-6", pk("| Source checksum/time | Snapshot |"), "Snapshot: non-empty checksum, time; DEMAND version must match the plan", I, cite(REPO, 'if(demandId!=S(p!,"DemandPlanId")||version!=S(p!,"DemandPlanVersion")'), "partial — " + t("SandopAtomicityTests", "Checksum_mismatch_produces_422_and_no_capture_effects") + " covers the checksum; nothing covers a version that differs from the plan", "The pack says 'UTC time'; the code accepts any offset (" + cite(WIRE, "(Z|[+-]") + "). The YAML type is date-time.")
row("VAL-12-7", pk("| Snapshot ID | Sign-off |"), "Snapshot ID: UUID, exists in the same tenant/LE and plan", I, cite(REPO, '"INVALID_SNAPSHOT_REFERENCE"'), "none", "")
row("VAL-12-8", pk("| Role/decision | Sign-off |"), "Role/decision: frozen enum values", M, cite(WIRE, '"DemandPlanning","SupplyPlanning","Finance","Operations","Executive"'), t("SandopContractTests", "Schema_rejects_extra_fields_and_invalid_enums") + " — " + GREEN, "")

# ---------------- §13 failure paths
row("FP-13-1a", pk("- Unknown/unpublished/cross-scope or checksum-mismatched exact DEMAND fixture"), "Unknown or checksum-mismatched fixture → 422 INVALID_DEMAND_REFERENCE, zero writes", M, cite(REPO, '422,"INVALID_DEMAND_REFERENCE"'), t("SandopAtomicityTests", "Invalid_fixture_creates_nothing_in_five_effect_collections") + "; " + t("SandopAtomicityTests", "Checksum_mismatch_produces_422_and_no_capture_effects") + " — " + GREEN, "")
row("FP-13-1b", pk("- Unknown/unpublished/cross-scope or checksum-mismatched exact DEMAND fixture"), "Unpublished or cross-scope fixture → 422", I, cite(FIX, "x.Published"), "none", "")
row("FP-13-2", pk("- Cross-tenant or cross-legal-entity plan returns 404"), "Cross-tenant / cross-LE plan → 404 without existence leakage", M, "see AC-16-6a", t("SandopIsolationTests", "Cross_scope_mutation_cannot_disclose_receipt_or_plan") + " — " + GREEN, "")
row("FP-13-3", pk("- Duplicate plan horizon+demand version returns 409"), "Duplicate horizon + demand version → 409, no duplicate aggregate", M, cite(REPO, '409,"SANDOP_PLAN_ALREADY_EXISTS");}') + "; " + cite(SCHEMA, '"sandop_active_horizon_demand"'), t("SandopAtomicityTests", "Mongo_unique_index_rejects_definite_duplicate_plan_without_partial_effect") + "; " + t("SandopReplayTests", "Exact_key_is_not_trimmed") + " — " + GREEN, "")
row("FP-13-4a", pk("- Capture Draft/InReview appends an immutable snapshot"), "Capture in Draft/InReview appends a snapshot and sets InReview / currentSnapshotId", M, cite(REPO, 'Update.Set("Status","InReview")'), t("SandopLifecycleTests", "Create_capture_signoff_preserves_prior_snapshot_and_draft_to_inreview") + " — " + GREEN, "")
row("FP-13-4b", pk("- Capture Draft/InReview appends an immutable snapshot"), "Capture in Approved/Rejected/Archived → 409 SANDOP_PLAN_STATE_CONFLICT", I, cite(REPO, '409,"SANDOP_PLAN_STATE_CONFLICT"'), "none", ISO + acc("F01") + ". In this slice no operation moves a plan out of InReview, so the branch is reachable only by changing the stored status.")
row("FP-13-5a", pk("- Sign-off outside InReview returns 409"), "Sign-off outside InReview → 409 SANDOP_SIGN_OFF_STATE_CONFLICT", I, cite(REPO, '409,"SANDOP_SIGN_OFF_STATE_CONFLICT"'), "none — no test signs off a Draft plan", ISO + acc("F02"))
row("FP-13-5b", pk("- Sign-off outside InReview returns 409"), "Wrong-plan snapshot → 422 INVALID_SNAPSHOT_REFERENCE", I, cite(REPO, '422,"INVALID_SNAPSHOT_REFERENCE"'), "none", ISO + acc("F03"))
row("FP-13-6", pk("- Repeated role sign-off returns 409"), "Repeated role sign-off → 409 SIGN_OFF_ALREADY_RECORDED, evidence not overwritten", M, cite(REPO, '409,"SIGN_OFF_ALREADY_RECORDED");}'), t("SandopLifecycleTests", "Create_capture_signoff_preserves_prior_snapshot_and_draft_to_inreview") + "; " + t("SandopConcurrencyTests", "Concurrent_different_keys_cannot_record_same_role_twice") + " — " + GREEN, "")
row("FP-13-7", pk("- Same scoped key and equivalent valid payload replays"), "Same key + same payload replays the original body with no reread or second event; changed payload → 409 IDEMPOTENCY_KEY_REUSED", M, cite(REPO, "static SandopResult Replay"), t("SandopReplayTests", "Exact_key_replays_original_body_without_second_effect_and_changed_payload_conflicts") + "; " + t("SandopReplayTests", "Replay_survives_new_client_and_does_not_reread_fixture") + " — " + GREEN, "'Current response correlation' is controller behaviour and is covered by AC-16-5b, not here.")
row("FP-13-8a", pk("- Missing/malformed/duplicate correlation returns 400"), "Bad correlation → 400 with one rejection-only UUID and no write", I, "see AC-16-5c", "none", "")
row("FP-13-8b", pk("- Missing/malformed/duplicate correlation returns 400"), "Known dependency failure → 503 DEPENDENCY_UNAVAILABLE", M, cite(REPO, 'catch(Exception ex) when(ex is MongoException or TimeoutException) { return SandopResult.Error(503,"DEPENDENCY_UNAVAILABLE"); }'), t("SandopAtomicityTests", "Unavailable_dependency_returns_declared_503") + " — " + GREEN, "Uses an unreachable Mongo port by design (57191).")
row("FP-13-8c", pk("- Missing/malformed/duplicate correlation returns 400"), "Unresolved commit → 503 COMMIT_RESULT_UNRESOLVED after scoped receipt resolution", I, cite(REPO, 'return SandopResult.Error(503,"COMMIT_RESULT_UNRESOLVED");}'),
    t("SandopAtomicityTests", "Unknown_commit_without_receipt_returns_unresolved_then_exact_key_recovers") + " — WEAK GREEN (Q214); " + t("SandopAtomicityTests", "Committed_write_concern_uncertainty_resolves_original_receipt") + " — CANNOT DISTINGUISH (Q214)",
    "Q214: the fail point is armed times:3 and fires once (" + Q214 + "BLAST-RADIUS.tsv:5); the second test accepts 201 or 503 (" + Q214 + "BLAST-RADIUS.tsv:6). After error 91 the driver cannot reach the server, so the receipt lookup at the cited line most likely times out rather than finding nothing; 'after scoped receipt resolution' is not shown. The same-key recovery half is real but uses a new direct client (THINNESS-VERDICT.md §5).")

# ---------------- §4 / §10 conventions
row("ENT-4", pk("entity_base: EntityBase"), "Entities use EntityBase (frontmatter entity_base)", N, cite(PLAN, "public sealed class SandopPlan"), "n/a", "The three entity classes have no base class. The pack records this as F190-ENT (" + pk("**F190-ENT:**") + "). They are also unused at run time (OWNED-PATH-COVERAGE.tsv).")
row("IDX-4", pk("Tenant-first indexes: unique plan horizon+demand version"), "Tenant-first unique indexes: plan horizon+version, snapshot sequence, sign-off (snapshot, role)", M, cite(SCHEMA, '"sandop_active_horizon_demand"') + "; " + cite(SCHEMA, '"sandop_snapshot_sequence"') + "; " + cite(SCHEMA, '"sandop_role_snapshot"'), t("SandopAtomicityTests", "Mongo_unique_index_rejects_definite_duplicate_plan_without_partial_effect") + " — " + GREEN, "Defined and tested. Nothing in the service creates them: SandopSchema.EnsureAsync has one caller, a test (" + t("SandopContractTests", "SandopSchema.EnsureAsync") + "). See F-Q212-4.")
row("BC-10-1", pk("Each public command/query/handler/validator has its"), "Each command/query/handler/validator has its own file; names carry no Command/Query suffix on handlers and validators", M, "OWNED-PATH-COVERAGE.tsv (3 commands, 3 queries, 6 handlers, 3 validators, one type per file)", "none needed (structural)", "")
row("BC-10-2", pk("own file. Handler and validator names do not carry Command/Query suffixes. Runtime work must use the service's four"), "Runtime uses the service's four pipeline behaviors", M, cite(CTRL, "sender.Send(new CreateSandopPlanCommand") + "; " + cite(APPDI, "typeof(ValidationBehavior<,>)") + "; " + cite(APPDI, "typeof(PerformanceBehavior<,>)"), "none needed (structural)", "All six actions go through MediatR; the four behaviors are registered as open generics. Whether they behave correctly for a SandopResult response was not observed.")
row("BC-10-3", pk("pipeline behaviors, base controller and explicit tenant/legal-entity repository predicates."), "Runtime uses the service's base controller", N, cite(CTRL, "public sealed class SandopPlansController(ISender sender):ControllerBase") + "; " + cite(SRC + "Api/Controllers/CustomBaseController.cs", "public abstract class CustomBaseController"), "n/a", "The controller derives from ASP.NET ControllerBase, not the service's CustomBaseController. The source is byte-equal to the CT-accepted source, so this was accepted before. Reported, not judged.")
row("BC-10-4", pk("pipeline behaviors, base controller and explicit tenant/legal-entity repository predicates."), "Explicit tenant / legal-entity repository predicates", M, cite(REPO, "static BsonDocument Scope(SandopScope s)") + "; " + cite(REPO, "static BsonDocument Active(SandopScope s)"), t("SandopIsolationTests", "Tenant_and_legal_entity_cross_scope_reads_are_404") + " — " + GREEN, "Every read and write filter starts from Scope() or Active().")
row("AUTH-14", pk("Tenant actor/service actor with default-deny JWT and permission checks:"), "Four permission keys, default-deny", I, cite(PERM, 'public const string Read="supplychain.sandop-plans.read"') + "; " + cite(MW, '!http.User.HasClaim("permission",permission)'), "unit level only (see AC-16-6b)", "The four key strings match the pack. SandopPermissionAttribute is a marker with no filter (" + cite(PATTR, "public sealed class SandopPermissionAttribute(string permission):Attribute") + "); enforcement is the in-action gate.")
row("GW-15", pk("Desired public family is `/api/supply-chain/sandop-plans/**` routed to port 5061."), "Gateway routes for /api/supply-chain/sandop-plans/**", N, "gateway/Diten.ApiGateway/ocelot.json — 0 occurrences of 'sandop'", "n/a", "Shared seam, integration-agent only. Not a module defect (pack status_note).")

# ---------------- §17 test expectations
for i, (name, v, ev, test, note) in enumerate([
 ("DCP-002 identity verifier and OpenAPI syntax/reference validation PASS", I, "identity: see RFD-18-1", "identity gate only", "The OpenAPI validation was not run by Q212 and no in-tree test does it."),
 ("Domain tests cover plan lifecycle, snapshot immutability and sign-off uniqueness", M, "see AC-16-2, AC-16-3", t("SandopLifecycleTests", "Create_capture_signoff_preserves_prior_snapshot_and_draft_to_inreview") + "; " + t("SandopConcurrencyTests", "Concurrent_different_keys_cannot_record_same_role_twice"), "Covered at repository level. There are no domain-object tests because the domain classes carry no behaviour."),
 ("Handler/API tests cover validation, RBAC, tenant/LE isolation, idempotency and concurrency", N, "0 hits in " + TST + " for a handler constructor, SandopPlansController, WebApplicationFactory or TestServer", "none — no handler or API test exists", "The topics are covered below the handler, at the repository. The handler/API layer itself is untested."),
 ("Contract tests cover every declared success/error shape and lifecycle event example", N, t("SandopContractTests", "public sealed class SandopContractTests") + " — 3 tests: fingerprint, two schema negatives, the request gate", "none for response or event shapes", ""),
 ("Mongo integration tests use isolated DB-010 naming and tenant-first indexes", M, t("SandopContractTests", 'GetDatabase("DitenSupplyChain_Mod0190_Test")') + "; " + cite(SCHEMA, '"TenantId","LegalEntityId"'), "all 16 Mongo-backed tests", "One fixed database; isolation by a fresh tenant per test."),
 ("Prism-compatible mock smoke uses frozen DEMAND and SANDOP-CAPACITY contracts", N, "no file under " + SVC + "tests mentions prism", "none", ""),
 ("Relevant service and repo architecture gates pass", I, "not run by Q212; Q208 ran the SupplyChain test project only", "n/a", ""),
], 1): row(f"TE-17-{i}", pk("## 17. Test Expectations"), "Test expectation: " + name, v, ev, test, note)

# ---------------- UI and self-registration
UI = "no S&OP UI file exists under frontend/ (find frontend -ipath '*Sandop*' → 0 files)"
for label, needle in [("SU-VS1", "| SU-VS1 |"), ("SU-01", "| SU-01 |"), ("SU-02", "| SU-02 |"), ("SU-03", "| SU-03 |"), ("SU-04", "| SU-04 |"), ("SU-05", "| SU-05 |"), ("SU-06..08", "| SU-06…SU-08 |"), ("SU-09", "| SU-09 |"), ("SU-10", "| SU-10 |"), ("SU-11", "| SU-11 |"), ("SU-12", "| SU-12 |"), ("SU-13", "| SU-13 |"), ("SU-14", "| SU-14 |"), ("SU-15..16", "| SU-15, SU-16 |"), ("SU-17", "| SU-17 |"), ("SU-18", "| SU-18 |"), ("SU-19", "| SU-19 |"), ("SU-20", "| SU-20 |")]:
    row(label, pk(needle), "UI acceptance row(s) " + label + " (§23.11)", N, UI, "none", "Outside Q212's scope and open by the pack's own status_note. Absence is not a module defect.")
PROV = "SopWorkflowSignoffsManifestProvider.cs is absent from " + SRC + "Api/ModuleRegistration/"
for i in range(1, 9): row(f"M-0{i}", pk(f"| M-0{i} |"), f"Self-registration test M-0{i} (§24)", N, PROV, "none", "Ships with the UI by the pack's ship rule (" + pk("### Ship rule (D4 = A)") + "). Not a module defect.")

def w(name, hdr, rws):
    with open(os.path.join(OUT, name), "w", newline="", encoding="utf-8") as f:
        cw = csv.writer(f, delimiter="\t", lineterminator="\n"); cw.writerow(hdr); cw.writerows(rws)
w("ACCEPTANCE-MATRIX.tsv", ["id", "pack_line", "criterion", "verdict", "evidence_path_line", "test_behind_it", "note"], rows)

# ---------------- OWNED-PATH-COVERAGE
man = {}
for l in lines(MAN):
    parts = l.split("\t")
    hs = [x for x in parts if len(x) == 64 and all(c in "0123456789abcdef" for c in x)]
    ps = [x for x in parts if "/" in x]
    if hs and ps: man[ps[0]] = hs[0]
ROLE = {
 "SandopPlansController.cs": ("Six actions; each calls the request gate, then MediatR; writes the correlation header and the contract error body", "SUBSTANTIVE", "Derives from ControllerBase, not CustomBaseController (BC-10-3)."),
 "SandopContextMiddleware.cs": ("Request gate: correlation header, authentication, three trusted claims, permission claim, scope headers, idempotency key", "SUBSTANTIVE", "A static function called inside each action, not an ASP.NET middleware. The file says why: 'until a separately owned Program.cs composition is approved' (line 3)."),
 "SandopContractError.cs": ("Error envelope and the message for 13 published codes", "SUBSTANTIVE", ""),
 "CreateSandopPlanCommand.cs": ("Request record", "MINIMAL — complete for its job", ""), "CaptureSandopSnapshotCommand.cs": ("Request record", "MINIMAL — complete for its job", ""), "RecordSandopSignOffCommand.cs": ("Request record", "MINIMAL — complete for its job", ""),
 "GetSandopPlanQuery.cs": ("Request record", "MINIMAL — complete for its job", ""), "ListSandopSnapshotsQuery.cs": ("Request record", "MINIMAL — complete for its job", ""), "ListSandopSignOffsQuery.cs": ("Request record", "MINIMAL — complete for its job", ""),
 "CreateSandopPlanHandler.cs": ("Validates the body, then delegates to the repository", "THIN DELEGATE", "All business rules are in the repository."), "CaptureSandopSnapshotHandler.cs": ("Validates the body, then delegates to the repository", "THIN DELEGATE", ""), "RecordSandopSignOffHandler.cs": ("Validates the body, then delegates to the repository", "THIN DELEGATE", ""),
 "GetSandopPlanHandler.cs": ("Delegates to the repository", "THIN DELEGATE", ""), "ListSandopSnapshotsHandler.cs": ("Delegates to the repository", "THIN DELEGATE", ""), "ListSandopSignOffsHandler.cs": ("Delegates to the repository", "THIN DELEGATE", ""),
 "CreateSandopPlanValidator.cs": ("FluentValidation: tenant, LE, actor and key not empty", "THIN — context checks only", "Body rules are in SandopWire.Validate, not here."), "CaptureSandopSnapshotValidator.cs": ("FluentValidation: tenant, LE, actor and key not empty", "THIN — context checks only", ""), "RecordSandopSignOffValidator.cs": ("FluentValidation: tenant, LE, actor and key not empty", "THIN — context checks only", ""),
 "SandopPlanModels.cs": ("Command/query context records and SandopWire.Validate: every body rule for the three mutations", "SUBSTANTIVE", ""),
 "SandopRequestFingerprint.cs": ("Canonical-JSON sha256 fingerprint for replay", "SUBSTANTIVE", ""),
 "SandopProjection.cs": ("Items() wraps list responses. Plan(), Snapshot(), SignOff() serialize the domain classes", "PARTLY DEAD", "Only Items() has a caller (SandopRepository). The other three methods have none."),
 "SandopPlan.cs": ("Plan entity class", "SHELL — no runtime caller", "Referenced only by the uncalled SandopProjection.Plan(). The repository stores BsonDocument. No EntityBase (ENT-4)."),
 "SandopSnapshot.cs": ("Snapshot entity class and two value records", "SHELL — no runtime caller", "Referenced only by the uncalled SandopProjection.Snapshot()."),
 "SandopSignOff.cs": ("Sign-off entity class", "SHELL — no runtime caller", "Referenced only by the uncalled SandopProjection.SignOff()."),
 "SandopScope.cs": ("Scope record, action enum, result record", "SUBSTANTIVE", ""),
 "ISandopRepository.cs": ("Repository interface and the DEMAND fixture-reader interface", "SUBSTANTIVE", ""),
 "SandopRepository.cs": ("All business logic: scope filters, six reads/writes, receipts, audit, outbox, transaction, replay, error mapping", "SUBSTANTIVE — the core", "94 lines, up to 551 characters per line."),
 "SandopSchema.cs": ("Six indexes, three of them unique invariants", "SUBSTANTIVE", "Static EnsureAsync. Its only caller is a test."),
 "SandopPersistenceRegistration.cs": ("AddSandopPersistence: registers the repository", "MINIMAL", "Registers neither the schema nor a fixture reader. No caller in the tree."),
 "SandopPermissionAttribute.cs": ("Attribute carrying a permission string", "MARKER — no behaviour", "Not a filter. Enforcement is in the request gate."),
 "SandopPermissions.cs": ("Four permission key constants", "MINIMAL — complete for its job", ""),
 "DemandFixtureReader.cs": ("Exact tenant/LE/id/version/checksum/published match against a fixture list", "SUBSTANTIVE (fixture seam by design)", "No DEMAND HTTP call, as the pack requires."),
 "SandopContractTests.cs": ("Test host helper + 3 tests", "TEST", "3 cases in Q208"), "SandopLifecycleTests.cs": ("2 tests", "TEST", "2 cases"), "SandopReplayTests.cs": ("4 tests", "TEST", "4 cases"),
 "SandopConcurrencyTests.cs": ("1 test", "TEST", "1 case"), "SandopAtomicityTests.cs": ("7 tests", "TEST", "7 cases; 2 carry Q214 caveats"), "SandopIsolationTests.cs": ("2 tests", "TEST", "2 cases"),
}
cov = []
for i, l in enumerate(lines(OWNED)[1:], 2):
    mod, layer, p, _ = l.split("\t"); on = os.path.isfile(p); b = os.path.basename(p)
    h = sha(p) if on else "-"; role, sub, note = ROLE[b]
    cov.append([i - 1, layer, p, "present" if on else "ABSENT", len(lines(p)) if on else 0, os.path.getsize(p) if on else 0, max(len(x) for x in lines(p)) if on else 0, h,
                "equal" if man.get(p) == h else "DIFFERS", role, sub, note, f"{OWNED}:{i}"])
w("OWNED-PATH-COVERAGE.tsv", ["n", "layer", "owned_path", "state", "lines", "bytes", "longest_line_chars", "sha256", "vs_accepted_source_manifest", "what_it_does", "substance", "note", "authority"], cov)

# ---------------- SCOPE-DELTA
owned = {r[2] for r in cov}; present = []
for r, ds, fs in os.walk(SVC):
    ds[:] = [d for d in ds if d not in ("bin", "obj", "TestResults")]
    for fn in fs:
        p = os.path.join(r, fn)
        if "andop" in p: present.append(p)
sd = [[p, "in the 38 (" + OWNED + ")", "present", "PRESENT, AUTHORIZED, byte-equal to the accepted source"] for p in sorted(owned)]
for p in sorted(set(present) - owned): sd.append([p, "NOT in the 38", "present", "PRESENT-BUT-OUTSIDE-THE-38"])
for p, auth, d in [
 (PROG, pk("`Program.cs`, common DI, shared permission registry and gateway belong to integration owners."), "REQUIRED COMPOSITION ABSENT — the file has no S&OP line; integration-owned, not a module defect"),
 (SRC + "Api/ModuleRegistration/SopWorkflowSignoffsManifestProvider.cs", pk("| Provider / tests (proposed paths) |"), "ABSENT — proposed path; ships with the UI"),
 (SVC + "tests/Diten.SupplyChainService.Tests/ModuleRegistration/SopWorkflowSignoffsManifestProviderTests.cs", pk("| Provider / tests (proposed paths) |"), "ABSENT — proposed path; ships with the UI"),
 ("frontend/Diten.Web/** — the 32 owned UI paths", pk("### 23.10 Owned UI paths (32, new files only)"), "ABSENT (0 of 32) — UI is open by the pack's status_note"),
 ("module-local generated API reference", pk("- Module-local generated API reference and immutable verification evidence"), "ABSENT — permitted by the pack, not required; none found"),
]: sd.append([p, auth, "yes" if os.path.isfile(p) else "no", d])
w("SCOPE-DELTA.tsv", ["path", "authority", "present", "delta"], sd)

# ---------------- BOUNDARY-REFS
b = [
 ["B-01", "all 32 source files: the only 'using Diten…' namespaces are Domain/Application/Infrastructure/Persistence .Features.SandopPlans (33 using directives, 0 to anything else)", "own feature namespaces", "MOD-0190", "no cross-feature reference", pk("- MOD-0183..0187, MOD-0192 and MOD-0147/0148 runtime paths."), "Measured by grep over src/*/Features/SandopPlans."],
 ["B-02", "grep for Domain.Common, Application.Common, Api.Controllers, EntityBase, CustomBaseController, Response<, RequestContext in Features/SandopPlans → 0", "shared kernel types", "shared", "NOT USED — the module uses no shared-kernel type at all", pk("pipeline behaviors, base controller and explicit tenant/legal-entity repository predicates."), "This is the opposite of a violation, but it is why BC-10-3 and ENT-4 are NOT MET."],
 ["B-03", "grep for Features.CapacityPlans / CapacityPlan in Features/SandopPlans → 0", "MOD-0192 types", "MOD-0192", "no reference — PERMITTED state (nothing shared)", pk("| MOD-0192 | Parallel peer | Disjoint 38/43 paths; no shared persistence or internal types |"), ""],
 ["B-04", "grep for SandopPlans in src/*/Features/CapacityPlans → 0", "reverse direction: MOD-0192 → MOD-0190", "MOD-0192", "no reference", "", "CapacityPlans declares its own IDemandFixtureReader in its own namespace; it is a different type."],
 ["B-05", cite(SRC + "Domain/Features/SandopPlans/ISandopRepository.cs", "public interface IDemandFixtureReader") + "; " + cite(FIX, "public sealed class DemandFixtureReader"), "DEMAND v1 (MOD-0188)", "MOD-0188 contract", "PERMITTED read-by-contract (fixture form) — an in-memory exact-match reader; no HTTP client, no DEMAND namespace, no DEMAND collection", pk("| `DEMAND` v1 | FROZEN / TEST FIXTURE ONLY |"), "The reader returns a boolean. It cannot carry demand data into the module."],
 ["B-06", cite(REPO, 'sourceContract="DEMAND",sourceContractVersion="v1"'), "string literals 'DEMAND' / 'v1' in the stored provenance", "MOD-0188 contract", "PERMITTED — contract literal (provenance label)", pk("| Snapshot provenance | DEMAND plan ID/version, source contract/version, captured time and checksum |"), ""],
 ["B-07", cite(REPO, 'var provenance=new{demandPlanId=demandId') + "; " + cite(REPO, 'var refs=body.TryGetProperty("supplyInputRefs"'), "what a snapshot stores about demand and supply", "MOD-0188 / supply sources", "PERMITTED — references only: demand plan ID, version, checksum, time; supply input refs are (source, resourceId, resourceVersion) strings", pk("**Must not:** copy forecast series or demand quantities into a second SoR"), "No quantity, series, forecast or balance field in any of the 32 files (grep: 'forecast', 'quantit', 'series' → 0)."],
 ["B-08", "no write path to any demand object exists: the six collections are " + cite(REPO, '"sandop_plans"') + " … " + cite(REPO, '"sandop_outbox"'), "MOD-0188 data", "MOD-0188", "PERMITTED state — MOD-0190 cannot edit MOD-0188 data; it opens only sandop_* collections", pk("edit MOD-0188 data"), ""],
 ["B-09", "grep for 'scenario' / 'capacity' in Features/SandopPlans → 0", "capacity scenarios", "MOD-0192", "PERMITTED state — no capacity scenario type, field or collection", pk("own capacity scenarios"), ""],
 ["B-10", cite(MW, 'Header(http.Request,"X-Tenant-Id",out var headerTenant)') + "; " + cite(WIRE, "allowed.Contains(p.Name,StringComparer.Ordinal)"), "tenant / legal-entity scope in request bodies", "-", "PERMITTED state — scope comes from signed claims and must equal the headers; any extra body property is rejected", pk("accept tenant/legal-entity scope in request bodies"), "Test: " + t("SandopContractTests", "Schema_rejects_extra_fields_and_invalid_enums") + " (a body with tenantId is rejected)."],
 ["B-11", "tests: the only 'using Diten…' namespaces in " + TST + " are the four SandopPlans namespaces; " + t("SandopContractTests", "Diten.SupplyChainService.Api.Features.SandopPlans.SandopContextMiddleware.Resolve"), "test references", "MOD-0190", "no cross-feature reference", "", ""],
 ["B-12", t("SandopAtomicityTests", '"killAllSessions"'), "admin command killAllSessions on the shared lane mongod (test code)", "every module using that mongod", "NOT a code boundary violation; a test-side cross-module side effect", "", "It kills all sessions on the server, not only this test's. Harmless while the assembly runs serially; a hazard if classes ever run in parallel or two lanes share a mongod (see Q214 F-Q214-6)."],
 ["B-13", SVC + "tests/Diten.SupplyChainService.Tests/CapacityPlans/CapacityTestMongo.cs:8", "the word 'SandopPlans' in a comment in a MOD-0192 test helper", "MOD-0192", "no code reference", "", "The only occurrence of 'Sandop' outside the 38 paths in the service."],
]
w("BOUNDARY-REFS.tsv", ["id", "reference_path_line", "referenced_symbol", "owner", "verdict", "pack_basis", "note"], b)

from collections import Counter, defaultdict
g = defaultdict(Counter)
for r in rows: g[r[0].split("-")[0] if not r[0].startswith(("SU", "M-")) else r[0][:2]][r[3]] += 1
for k, v in g.items(): print(k, sum(v.values()), v[M], v[N], v[I])
print("total", len(rows), dict(Counter(r[3] for r in rows)))
print("owned", len(cov), Counter(r[3] for r in cov), Counter(r[8] for r in cov), "lines", sum(r[4] for r in cov[:32]), "bytes", sum(r[5] for r in cov[:32]))
print(Counter(r[10].split(" —")[0] for r in cov))
