#!/usr/bin/env python3
"""Q210 — builds ACCEPTANCE-MATRIX.tsv, SCOPE-DELTA.tsv, BOUNDARY-REFS.tsv. READ-ONLY on the repo:
reads files and hashes them; writes only into the folder given as argv[1]. Every path:line is looked up
from the file at run time (cite()), so a citation cannot silently point at the wrong line."""
import sys, os, csv, json, hashlib, re
OUT = sys.argv[1]
SVC = "services/Diten.SupplyChainService/"
SRC = SVC + "src/Diten.SupplyChainService."
TST = SVC + "tests/Diten.SupplyChainService.Tests/Returns/"
PACK = "execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md"
ANNEX = "docs/analysis/contracts/returns-semantics-v3.0.0.md"
YAML = "docs/analysis/contracts/shipment-bundle.openapi.yaml"
REPO = SRC + "Persistence/Features/Returns/ReturnRepository.cs"
SCHEMA = SRC + "Persistence/Features/Returns/ReturnSchema.cs"
MW = SRC + "Api/Features/Returns/ReturnContextMiddleware.cs"
CTRL = SRC + "Api/Features/Returns/ReturnsController.cs"
READER = SRC + "Infrastructure/Features/Returns/ReturnReferenceReader.cs"
PERM = SRC + "Infrastructure/Features/Returns/ReturnPermissions.cs"
WIRE = SRC + "Application/Features/Returns/ReturnModels.cs"
LIFE = SRC + "Domain/Features/Returns/ReturnLifecycle.cs"
PROG = SRC + "Api/Program.cs"
Q208 = "docs/records/audits/2026-10/mvp6-q208-suite-baseline-01/"
Q214 = "docs/records/audits/2026-10/mvp6-q214-failpoint-isolation-01/"
ACC = "docs/records/audits/2026-09/mvp6-mod0186-wp-acceptance-01/"
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
def t(file, method): return cite(TST + file, method)
def sha(p): return hashlib.sha256(open(p, "rb").read()).hexdigest()
def pk(needle): return cite(PACK, needle)
GREEN = "passed in the Q208 baseline run (" + Q208 + "evidence/full.trx; Returns 78/78, " + Q208 + "NEW-BASELINE.tsv:6)"
M, N, I = "MET", "NOT MET", "INSUFFICIENT EVIDENCE"
rows = []
def row(id_, packref, crit, verdict, ev, test, note=""): rows.append([id_, packref, crit, verdict, ev, test, note])

# ---- §16 acceptance criteria
row("AC-16-1", pk("- [ ] Runtime remains absent while `draft`."), "Runtime remains absent while draft", M,
    pk("status: ready-for-dev") + "; " + pk("**Precedence over stale wording:**"), "n/a (gate)",
    "Vacuously met: the pack is not draft. Returns runtime source IS present (36 files). Prior CT row PACK-16-01 = GATE_SUPERSEDED_FOR_ISOLATED_SCOPE (" + cite(ACC + "ACCEPTANCE-MATRIX.tsv", "PACK-16-01") + "). Box left unticked.")
row("AC-16-2", pk("- [ ] Future code consumes MOD-0183/INVENTORY strictly"), "Code consumes MOD-0183 / INVENTORY strictly by frozen contract / mock", M,
    cite(READER, 'new HttpRequestMessage(HttpMethod.Get') + " (only outbound call: GET shipments/{id}); " + cite(ANNEX, "Inventory and Warehouse HTTP calls ZERO"),
    t("ReturnReferenceTests.cs", "Reference_MockedTransport_OnlyShipmentGetAndTrustedScope") + " — " + GREEN,
    "No Inventory/Warehouse namespace, client or collection in Features/Returns (BOUNDARY-REFS.tsv). One type-level coupling to a MOD-0183 domain type is reported separately (BOUNDARY-REFS.tsv row B-04).")
row("AC-16-3a", pk("- [ ] Return quantity invariants and lifecycle are atomic"), "Quantity invariant is atomic and deterministic (cap, race)", M,
    cite(REPO, 'if (next.CompareTo(cap) > 0)') + "; " + cite(REPO, "key.Add(\"Version\", stored.Version); stored.Version++; stored.UsedQuantity = next.ToString();") ,
    t("ReturnConcurrencyTests.cs", "Create_Races_RespectsExactCap") + "; " + t("ReturnAtomicityTests.cs", "MultilineFailure_SecondLineOverCap_LeavesNoFirstLineDebit") + " — " + GREEN, "Repository level, real Mongo. Not through HTTP.")
row("AC-16-3b", pk("- [ ] Return quantity invariants and lifecycle are atomic"), "Lifecycle is atomic and deterministic (7 arrows, no partial write)", M,
    cite(LIFE, "public static bool Allows") + "; " + cite(REPO, 'throw new ReturnFailureException(422, "INVALID_RETURN_TRANSITION")'),
    t("ReturnLifecycleTests.cs", "Lifecycle_ActualRepository_All64PairsRejectWithoutWrites") + " — " + GREEN, "")
row("AC-16-4", pk("- [ ] No stock collection, projection or movement SoR"), "No stock collection, projection or movement SoR in MOD-0186", M,
    cite(SCHEMA, '"returns", "return_entitlements", "returns_receipts", "returns_audit", "returns_outbox"') + " (the only five collections); " + cite(SRC + "Domain/Features/Returns/ReturnEntitlement.cs", "public string UsedQuantity"),
    "none — the explicit no-shadow-balance architecture test the pack expects (" + pk("outbox and explicit no-shadow-balance architecture tests") + ") does not exist",
    "Proven by reading: the entitlement record holds a return-request cap per shipment line, no on-hand/location/valuation field.")
row("AC-16-5", pk("- [ ] Correlation ID remains unchanged through reverse lifecycle events"), "Correlation ID unchanged through reverse lifecycle events", I,
    cite(REPO, "order.CorrelationRoot = observation!.Root;") + "; " + cite(REPO, 'if (order.CorrelationRoot != root) throw') + "; " + cite(REPO, "correlationId = order.CorrelationRoot, causationId = cause"),
    "partial — " + t("ReturnReplayTests.cs", "RestartReceipt") + " asserts the stored root only; no in-tree test asserts the outbox envelope correlationId",
    "The code is consistent with the criterion. No test in the tree reads the event envelope. Prior bounded evidence: " + cite(ACC + "ACCEPTANCE-MATRIX.tsv", "PACK-16-05") + " (isolated environment, not this tree).")

# ---- §18 ready-for-dev checklist
row("RFD-18-1", pk("- [x] Canonical ID/name passed DCP-002."), "Canonical ID/name passed DCP-002", M, ".antigravity/scripts/verify_module_id.py (run read-only by Q210: 'OK  MOD-0186: proven against Blueprint/registry.', exit 0)", "identity gate", "")
row("RFD-18-2", pk("- [x] MOD-0183 and INVENTORY consumption boundaries are explicit."), "MOD-0183 and INVENTORY consumption boundaries are explicit", M, pk("**Consumes:** MOD-0183 shipment/POD") + "; " + pk("**Does not own:**"), "n/a (document)", "")
row("RFD-18-3", pk("- [x] Frozen contract defines reverse lifecycle and mock examples"), "Frozen contract defines reverse lifecycle and mock examples", M, cite(YAML, "  /returns:") + "; " + cite(YAML, "    ReturnStatus:") + "; " + cite(ANNEX, "Frozen arrows ONLY"), "n/a (document)",
    "The YAML on disk is info.version 3.1.0, sha256 " + sha(YAML)[:12] + "…, not the 3.0.0 pin 5dfe7c1b… the pack binds (" + pk("| Published contracts at acceptance and today |") + "). Returns annex and root annex hashes match the pack.")
row("RFD-18-4", pk("- [ ] MOD-0183 dependency has executable verified evidence."), "MOD-0183 dependency has executable verified evidence", N,
    Q208 + "NEW-BASELINE.tsv:2 (Shipments 74/74); docs/records/audits/2026-10/mvp6-q205-testenv-01/MOD0183-EVIDENCE-STATEMENT.md:43-49", "MOD-0183 suite (not re-run by Q210)",
    "Executable: yes. Verified: no — one lane's run each time, no independent VER, no CT acceptance as dependency evidence. Satisfiable; see SOP-22.md §4. Box NOT ticked.")
row("RFD-18-5", pk("- [ ] Owner approval changed status from `draft`."), "Owner approval changed status from draft", M, pk("status: ready-for-dev") + "; " + pk("Approved: `docs/records/decisions/2026-09/mvp6-returns-pack-signoff-owner-decision-01.md`"), "n/a (record)",
    "Met by record; the checkbox is still unticked in the pack (inconsistency, F-Q210-9). Box NOT ticked by Q210.")

# ---- §27 DEV prerequisites
row("PRE-27-1", pk("- [x] Existing canonical module ID/name verified DCP-002"), "Canonical ID verified; no new/FU ID minted", M, "see RFD-18-1", "identity gate", "")
row("PRE-27-2", pk("- [x] Frozen operation/schema parity measured"), "Frozen operation/schema parity measured", M, pk("## 22. Exact frozen operation and field parity"), "n/a (document)", "")
row("PRE-27-3", pk("- [x] Exact proposed ownership and protected-file/shared-composition separation recorded."), "Ownership and shared-composition separation recorded", M, pk("## 25. Exact prospective DEV owned files"), "n/a (document)", "")
row("PRE-27-4", pk("- [ ] §23/24 owner decisions signed with exact test outcomes"), "§23/24 owner decisions signed; amendment published and consumed", M, ANNEX + " sha256 " + sha(ANNEX)[:12] + "… = the pack's pin (" + pk("| Published contracts at acceptance and today |") + ")", "n/a (record)", "Box unticked in the pack; superseded wording per §31.")
row("PRE-27-5", pk("- [ ] CT confirms bounded dependency acceptance under v4.0 sequence"), "CT confirms bounded dependency acceptance", M, pk("Settled sources: MOD0183 bounded CT acceptance (2026-09-16)") + "; docs/records/audits/2026-09/mvp6-mod0183-ct-review-01-2026-09-16.md (exists)", "n/a (record)", "Historical bounded acceptance of MOD-0183. It is not the same thing as RFD-18-4 on today's tree.")
row("PRE-27-6a", pk("- [ ] Phase1.5 full approval"), "Phase 1.5 full approval", M, pk("| Phase 1.5 closure |") + "; docs/records/audits/2026-09/mvp6-mod0186-http-01/authority/PHASE15.md sha256 c921c423… (re-hashed by Q210, equal)", "n/a (record)", "")
row("PRE-27-6b", pk("- [ ] Phase1.5 full approval"), "Program.cs single writer released", N, "docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv:305 (Q209 HELD); " + cite(PROG, "builder.Services.AddLoadPersistence();"), "n/a", "Program.cs still registers Carrier and Load only. See REACHABILITY.md.")
row("PRE-27-7", pk("- [ ] User/CT explicitly promotes this draft"), "Draft promoted; versioned DEV prompt issued", M, pk("## 30. PHASE15-CLOSE-01"), "n/a (record)", "For the isolated core only.")
row("PRE-27-8", pk("- [ ] Independent VER assigned after completed DEV"), "Independent VER after DEV; CT acceptance follows VER", I, pk("| Controlling CT acceptance |") + " (isolated bounded WP, ACCEPTED)", "n/a",
    "Done for the isolated work package. For the code as it stands in the common checkout, this WP (Q210) is the first VER and it is static only.")

# ---- §26 A01–A12
row("A01a", pk("| A01 wire |"), "Exactly three routes; no by-ID/update/delete", M, cite(CTRL, "[HttpGet, ReturnPermission(ReturnPermissions.Read)]") + "; " + cite(CTRL, "[HttpPost, ReturnPermission(ReturnPermissions.Create)]") + "; " + cite(CTRL, '[HttpPost("{returnId}/transition")'), "none needed (structural)", "")
row("A01b", pk("| A01 wire |"), "Exact frozen fields/nullability/responses; all inline examples validated", I, cite(WIRE, "public static bool CreateValid") + "; " + cite(WIRE, "public static bool TransitionValid") + "; " + cite(YAML, "    CreateReturnCommand:"), "none — no test validates the OpenAPI examples", "Fields and required lists match the YAML on disk by reading. The YAML on disk is 3.1.0, not the bound 3.0.0 file; parity with the bound bytes could not be checked.")
row("A02", pk("| A02 schema |"), "Every required field missing/null/type error; optional null vs omission; additional properties", I, cite(WIRE, "public static bool Object("), t("ReturnContractTests.cs", "Wire_BoundedShape_RejectsNullEvidenceAndExtrasAllowsOpaqueEmpty") + " — " + GREEN, "The test covers null evidence, one extra property, empty strings and a bad instant. It does not enumerate every required field.")
row("A03", pk("| A03 identity/security |"), "Real JWT tests: anonymous, missing grant, invalid/duplicate claims, scope injection, 2 tenants × 2 LEs", I, cite(MW, "if (authorization.Count != 1 || http.User.Identity?.IsAuthenticated != true)") + "; " + cite(MW, "if (grant is not null && !http.User.HasClaim"),
    t("ReturnIsolationTests.cs", "Middleware_PostAuthenticationUnit_OrderedContextMatrix") + " — the file itself says: " + cite(TST + "ReturnIsolationTests.cs", "NOT JWT validation"), "No test in the tree starts a host, sends a real token, or exercises the per-target grant branch. Prior HTTP evidence exists only for the isolated environment (" + cite(ACC + "ACCEPTANCE-MATRIX.tsv", "R08\t") + ").")
row("A04", pk("| A04 reference |"), "Published mock shape only; unknown/malformed/timeout/unavailable references fail closed; outbound method/path/headers observed", M, cite(READER, "public static ReturnReferenceSnapshot ParseShipment") + "; " + cite(READER, "catch (HttpRequestException)"),
    t("ReturnReferenceTests.cs", "Reference_MockedTransport_OnlyShipmentGetAndTrustedScope") + "; " + t("ReturnReferenceTests.cs", "OtherProducerFailures_RemainDependencyUnavailable") + "; " + t("ReturnReferenceTests.cs", "TimedOutProducer_RemainsDependencyUnavailable") + " — " + GREEN, "")
row("A05a", pk("| A05 state |"), "Every source×target pair against frozen arrows; initial state; no terminal reopen; same-state invalid", M, cite(LIFE, "public static bool Allows"), t("ReturnLifecycleTests.cs", "Lifecycle_All64Pairs_MatchesSevenCanonicalArrows") + "; " + t("ReturnLifecycleTests.cs", "Lifecycle_ActualRepository_All64PairsRejectWithoutWrites") + " — " + GREEN, "")
row("A05b", pk("| A05 state |"), "Real concurrent transitions yield an allowed serial history only", I, cite(REPO, 'cas.Add("Version", order.Version - 1);'), "none — no test races two transitions on one return", "Version CAS exists in code.")
row("A06a", pk("| A06 replay |"), "Same/split root, changed payload, scoped key isolation, concurrent duplicate, old result after new state, lost response", M, cite(REPO, "private static ReturnMutationResult Replay"),
    t("ReturnReplayTests.cs", "Receipt_RootBeforePayload_AndCurrentActorDoesNotRewriteAudit") + "; " + t("ReturnReplayTests.cs", "Receipt_AfterLaterState_ReturnsOriginalResultWithoutReread") + "; " + t("ReturnConcurrencyTests.cs", "SameKey_TwentyConcurrentCalls_OneAggregateReceiptEvent") + "; " + t("ReturnIsolationTests.cs", "Receipt_SameKeyDifferentLegalEntity_IsIndependent") + "; " + t("ReturnAtomicityTests.cs", "PostCommitResponseLoss_RecoversOriginalReceipt_NotZeroWrites") + " — " + GREEN, "")
row("A06b", pk("| A06 replay |"), "Replay after restart (fresh process)", I, t("ReturnReplayTests.cs", "RestartReceipt"), "in an ordinary run the test seeds and reads in ONE process", "The two-process driver " + SVC + "tests/returns/restart_probe.py calls runtime_probe.py, which reads a record that is not in the checkout (" + cite(SVC + "tests/returns/runtime_probe.py", "owned-paths.txt") + "). See SCOPE-DELTA.tsv.")
row("A07a", pk("| A07 atomic |"), "Fault after each write and before commit rolls back all; after commit/response loss returns original receipt", M, cite(REPO, 'await probe.AtAsync("aggregate", ct);') + "; " + cite(REPO, "if (commitAttempted)"),
    t("ReturnAtomicityTests.cs", "PrecommitFault_EachActualWriteStage_RollsBackAllFive") + " (5 stages); " + t("ReturnAtomicityTests.cs", "PostCommitResponseLoss_RecoversOriginalReceipt_NotZeroWrites") + " — " + GREEN, "Probe-injected faults: right reason, not exposed to the Q214 mechanism.")
row("A07b", pk("| A07 atomic |"), "Mongo unavailable / index failure / unsupported transaction fails closed", I, cite(SCHEMA, 'throw new InvalidOperationException("Returns require replica-set transactions.")') + "; " + cite(REPO, 'return ReturnMutationResult.Error(503, "PERSISTENCE_UNAVAILABLE");'), "none", "Code paths exist; no Returns test drives them.")
row("A07c", pk("| A07 atomic |"), "Unknown commit: repeated unknown results / retry exhaustion end in 503 without partial state", I, cite(REPO, 'catch (MongoException ex) when (ex.HasErrorLabel("UnknownTransactionCommitResult") && commit < 2) { }'),
    t("ReturnAtomicityTests.cs", "UnknownCommit_Unresolved503_AllOrNoneThenSameKeyRecovery") + " — GREEN FOR THE WRONG REASON (F-Q214-3)", "The fail point is armed times:5 and fires once (" + Q214 + "BLAST-RADIUS.tsv:4). Retry exhaustion is not tested. The green is treated as absent evidence.")
row("A08", pk("| A08 module business |"), "Each §24 decision row has boundary and concurrent negative/positive tests", I,
    cite(REPO, '"SHIPMENT_NOT_RETURNABLE"') + "; " + cite(REPO, '"SHIPMENT_LINE_NOT_FOUND"') + "; " + cite(REPO, '"RETURN_UOM_MISMATCH"') + "; " + cite(REPO, '"DISPOSITION_REQUIRED"') + "; " + cite(MW, "if (grant is not null && !http.User.HasClaim"),
    "none for these five branches", "D186-02, D186-03, D186-06 are tested. D186-01 (eligibility, missing line, command UoM mismatch), D186-04 (empty dispositionCode) and D186-05 (per-target grant) have code and no test.")
row("A09", pk("| A09 persistence |"), "Restart in a fresh process: list reload, scoped counts, exact decimal text, version, audit/outbox", I, t("ReturnReplayTests.cs", "RestartReceipt"), "single process in an ordinary run", "Exact decimal text and root are asserted, in one process. See A06b.")
row("A10", pk("| A10 no SoR duplication |"), "No Warehouse/Inventory mutation or stock/finance/Supplier collection; HTTP reads only for approved dependencies", M, cite(SCHEMA, '"returns", "return_entitlements"') + "; " + cite(READER, "new HttpRequestMessage(HttpMethod.Get"), t("ReturnReferenceTests.cs", "Reference_MockedTransport_OnlyShipmentGetAndTrustedScope") + " — " + GREEN, "Structural; proven by reading plus the single-GET test.")
row("A11", pk("| A11 regression |"), "Shipment and Carrier suites unchanged after the separately approved composition", I, Q208 + "NEW-BASELINE.tsv:2-3 (Shipments 74/74, Carriers 36/36)", "Q208 run", "The composition has not been applied (Program.cs held, Q209). The green numbers are pre-composition.")
row("A12", pk("| A12 evidence |"), "Actual request bytes/status/body/headers, source/binary/input hashes, process IDs", I, "no HTTP evidence exists for this tree", "none", "Source identity is established (SCOPE-DELTA.tsv: 46/46 byte-equal to the accepted source). Nothing else of A12 can exist while the module is unreachable.")

# ---- §29 R01–R11
row("R01a", pk("| R01 |"), "Source required omission → 502; optional shipmentId/status absent → 503", M, cite(READER, "if (e.ValueKind != JsonValueKind.Object || required.Any(n => !e.TryGetProperty(n, out _))) Invalid();") + "; " + cite(READER, '"REFERENCE_STATE_UNAVAILABLE"'), t("ReturnReferenceTests.cs", "Reference_Omission_RequiredVersusOptionalDecisionField") + " — " + GREEN, "")
row("R01b", pk("| R01 |"), "Duplicate source lines → 502", M, cite(READER, "if (lines.Select(x => x.LineNumber).Distinct"), t("ReturnReferenceTests.cs", "Reference_DuplicateOrNonpositiveLines_RejectsMalformed") + " — " + GREEN, "")
row("R01c", pk("| R01 |"), "Wrong source identity → 502", I, cite(READER, "if (Guid.Parse(shipmentId.GetString()!) != expectedId) Invalid();"), "none", "")
row("R01d", pk("| R01 |"), "Source 404 preserved", I, cite(READER, '"SHIPMENT_NOT_FOUND"'), "none", "")
row("R02a", pk("| R02 |"), "Source 10: 6+6 race → one 201, one 422; 4+6 → total 10", M, cite(REPO, 'if (next.CompareTo(cap) > 0)'), t("ReturnConcurrencyTests.cs", "Create_Races_RespectsExactCap") + " — " + GREEN, "")
row("R02b", pk("| R02 |"), "Multi-line failure rolls all lines back", M, cite(REPO, "private async Task Debit"), t("ReturnAtomicityTests.cs", "MultilineFailure_SecondLineOverCap_LeavesNoFirstLineDebit") + " — " + GREEN, "")
row("R02c", pk("| R02 |"), "UoM mismatch cannot create a second cap", I, cite(REPO, '"RETURN_UOM_MISMATCH"') + "; " + cite(SCHEMA, '"return_entitlement", true, ct, "ShipmentId", "LineNumber"'), "partial — " + t("ReturnConcurrencyTests.cs", "FirstSnapshot_IdentityOrOrdinalUomDrift_Returns409") + " covers source-side UoM drift (409) only", "The command-line UoM mismatch branch (422) has no test. The unique key excludes UoM by construction.")
row("R03", pk("| R03 |"), "Eight-state contribution table; Rejected/Cancelled decrement once; Closed/deleted still count; replay no debit/release", M, cite(LIFE, "public static bool Releases") + "; " + cite(REPO, "if (ReturnLifecycle.Releases(target.Value)) await Release"),
    t("ReturnLifecycleTests.cs", "Entitlement_OnlyRejectedCancelled_Release") + "; " + t("ReturnConcurrencyTests.cs", "Release_ReplayAndDifferentKey_DebitsExactlyOnce") + "; " + t("ReturnConcurrencyTests.cs", "ClosedAndManualReceived_RetainEntitlementOpaqueReferenceAndPendingEvents") + "; " + t("ReturnIsolationTests.cs", "SoftDelete_HidesAggregate_PreservesReceiptAndEntitlement") + " — " + GREEN, "")
row("R04", pk("| R04 |"), "Long scale/digits exact, raw strings retained; equal representations no false drift; quantity 0/-1 → 422", M, cite(SRC + "Domain/Features/Returns/ReturnQuantity.cs", "public static ReturnQuantity Parse") + "; " + cite(REPO, '"INVALID_RETURN_QUANTITY"'),
    t("ReturnContractTests.cs", "Quantity_EquivalentNumericValues_Equal") + "; " + t("ReturnContractTests.cs", "Quantity_Arithmetic_HasNoPrecisionLoss") + "; " + t("ReturnReplayTests.cs", "RawCreateAudit_PreservesUuidSpellingAndOmission_WhileFingerprintNormalizes") + "; " + t("ReturnConcurrencyTests.cs", "FirstSnapshot_NumericEquivalentAllowed_DriftConflictsAtomically") + "; " + t("ReturnReferenceTests.cs", "LocalInvalidQuantity_RejectsBeforeReferenceLookup") + " — " + GREEN, "")
row("R05a", pk("| R05 |"), "InTransit→Received 200 as manual assertion, audited as such", M, cite(REPO, '"manual-assertion"'), t("ReturnConcurrencyTests.cs", "ClosedAndManualReceived_RetainEntitlementOpaqueReferenceAndPendingEvents") + " — " + GREEN, "")
row("R05b", pk("| R05 |"), "Wrong grant → 403", I, cite(MW, "if (grant is not null && !http.User.HasClaim"), "none — " + t("ReturnIsolationTests.cs", "Permissions_ReturnsSpecificTargets_HaveNoClaimsInheritance") + " checks the key mapping only", "")
row("R06a", pk("| R06 |"), "Inventory reference null/empty accepted unverified; no Inventory/Warehouse HTTP; no stock writes", M, cite(WIRE, "public static bool NullableText") + "; " + cite(REPO, "order.InventoryTransactionReferenceId = inventoryReference;"), t("ReturnConcurrencyTests.cs", "ClosedAndManualReceived_RetainEntitlementOpaqueReferenceAndPendingEvents") + "; " + t("ReturnReferenceTests.cs", "Reference_MockedTransport_OnlyShipmentGetAndTrustedScope") + " — " + GREEN, "Observation F-Q210-11: every transition overwrites the aggregate's reference, including with null.")
row("R06b", pk("| R06 |"), "dispositionCode null/empty → 422 only at target Dispositioned", I, cite(REPO, '"DISPOSITION_REQUIRED"'), "none — the lifecycle test always supplies a non-empty code", "")
row("R07a", pk("| R07 |"), "64 lifecycle pairs against 7 arrows; same-state new-key 422", M, cite(LIFE, "public static bool Allows"), t("ReturnLifecycleTests.cs", "Lifecycle_ActualRepository_All64PairsRejectWithoutWrites") + " — " + GREEN, "")
row("R07b", pk("| R07 |"), "Transition race serializes or fails safe (503) without partial commit", I, cite(REPO, "throw new RetryReturnTransactionException();"), "none", "Same gap as A05b.")
row("R08a", pk("| R08 |"), "Context precedence: 401 unauthenticated, 403 unusable claims, 400 headers/scope query, 404 scope mismatch", M, cite(MW, "await Error(401); return;") + "; " + cite(MW, 'await Error(404, "RETURN_NOT_FOUND"); return;'), t("ReturnIsolationTests.cs", "Middleware_PostAuthenticationUnit_OrderedContextMatrix") + " (9 variants) — " + GREEN, "Middleware unit test with a fabricated principal on GET only. Not JWT validation.")
row("R08b", pk("| R08 |"), "Root 409 before key 409; outputs carry the current trace", M, cite(REPO, '"CORRELATION_ROOT_MISMATCH"') + "; " + cite(REPO, '"IDEMPOTENCY_KEY_REUSED"'), t("ReturnReplayTests.cs", "Receipt_RootBeforePayload_AndCurrentActorDoesNotRewriteAudit") + " — " + GREEN, "")
row("R08c", pk("| R08 |"), "Real JwtBearer: parser 401 vs post-auth 403; base grant then per-target grant", I, cite(MW, "UniqueSignedContextFields(authorization[0]!)") + "; " + cite(CTRL, "[Authorize, Route("), "none in the tree", "Same gap as A03.")
row("R09a", pk("| R09 |"), "Original 201/200 replay after status change; same key isolated across tenant/LE", M, cite(REPO, "if (prior is not null) return Replay(prior, fingerprint, root);"), t("ReturnReplayTests.cs", "Receipt_AfterLaterState_ReturnsOriginalResultWithoutReread") + "; " + t("ReturnIsolationTests.cs", "Receipt_SameKeyDifferentLegalEntity_IsIndependent") + " — " + GREEN, "")
row("R09b", pk("| R09 |"), "Unknown commit: receipt recovery", M, cite(REPO, "var recovered = await Receipts.Find(identity"), t("ReturnAtomicityTests.cs", "PostCommitResponseLoss_RecoversOriginalReceipt_NotZeroWrites") + " — " + GREEN, "Proven by the probe-injected after-commit fault, NOT by the fail-point test.")
row("R09c", pk("| R09 |"), "Unknown commit without receipt → 503, all-or-none; retry exhaustion", I, cite(REPO, 'catch (MongoException ex) when (ex.HasErrorLabel("UnknownTransactionCommitResult") && commit < 2) { }'), t("ReturnAtomicityTests.cs", "UnknownCommit_Unresolved503_AllOrNoneThenSameKeyRecovery") + " — GREEN FOR THE WRONG REASON (F-Q214-3)", "Fires 1 of 5. The same-key recovery half of that test is genuine; the exhaustion half is not evidence.")
row("R09d", pk("| R09 |"), "Replay after restart", I, t("ReturnReplayTests.cs", "RestartReceipt"), "single process", "See A06b.")
row("R10a", pk("| R10 |"), "Five transaction groups fault injection; drift 409 with no cap reset", M, cite(REPO, 'await probe.AtAsync("entitlement", ct);') + "; " + cite(REPO, '"RETURN_SOURCE_CHANGED"'), t("ReturnAtomicityTests.cs", "PrecommitFault_EachActualWriteStage_RollsBackAllFive") + "; " + t("ReturnConcurrencyTests.cs", "FirstSnapshot_NumericEquivalentAllowed_DriftConflictsAtomically") + " — " + GREEN, "")
row("R10b", pk("| R10 |"), "Pending events, no publisher", M, cite(REPO, 'pending.Add("Status", "Pending");') + "; " + cite(SRC + "Persistence/Features/Returns/ReturnOutboxStore.cs", "No publisher or worker registration"), t("ReturnConcurrencyTests.cs", "ClosedAndManualReceived_RetainEntitlementOpaqueReferenceAndPendingEvents") + " — " + GREEN, "ReturnOutboxWorker.cs is absent, as §30 requires.")
row("R10c", pk("| R10 |"), "Pending events survive restart", I, "no evidence on this tree", "none", "")
row("R11a", pk("| R11 |"), "Source hashes bound to the accepted source", M, "SCOPE-DELTA.tsv (46/46 byte-equal: 44 = returns46.json, 2 = source-final-341.tsv)", "hash comparison by Q210", "")
row("R11b", pk("| R11 |"), "Actual request bytes/status/header evidence; binary/process hashes; no secret in logs; non-Returns regressions", I, "no HTTP run exists for this tree", "none", "")

# ---- §13 failure paths and §14/§15
row("FP-13-1", pk("Unknown shipment/line, excess quantity"), "Unknown shipment → deterministic error, unchanged state", I, cite(READER, '"SHIPMENT_NOT_FOUND"'), "none", "")
row("FP-13-2", pk("Unknown shipment/line, excess quantity"), "Unknown shipment line → deterministic error, unchanged state", I, cite(REPO, '"SHIPMENT_LINE_NOT_FOUND"'), "none", "")
row("FP-13-3", pk("Unknown shipment/line, excess quantity"), "Excess quantity → deterministic error, unchanged state", M, cite(REPO, '"RETURN_QUANTITY_EXCEEDED"'), t("ReturnConcurrencyTests.cs", "Create_Races_RespectsExactCap") + " — " + GREEN, "")
row("FP-13-4", pk("Unknown shipment/line, excess quantity"), "Invalid transition → deterministic error, unchanged state", M, cite(REPO, '"INVALID_RETURN_TRANSITION"'), t("ReturnLifecycleTests.cs", "Lifecycle_ActualRepository_All64PairsRejectWithoutWrites") + " — " + GREEN, "")
row("FP-13-5", pk("return deterministic errors with unchanged state; cross-tenant/cross-LE resources return 404"), "Cross-tenant / cross-LE resources return 404", M, cite(REPO, 'throw new ReturnFailureException(404, "RETURN_NOT_FOUND")') + "; " + cite(REPO, "private static BsonDocument Visible"), t("ReturnIsolationTests.cs", "Scope_AnotherTenantOrLE_CannotReadMutateOrReplay") + " — " + GREEN, "Repository level: foreign scope lists nothing and transitions get 404.")
row("AUTH-14", pk("Prospective permissions:"), "Permission keys as bound by the annex (read, create, transition + six target keys)", M, cite(PERM, 'public const string Read') + "; " + cite(PERM, "public static string? ForTarget") + "; " + cite(ANNEX, "Read=create mapping"), t("ReturnIsolationTests.cs", "Permissions_ReturnsSpecificTargets_HaveNoClaimsInheritance") + " — " + GREEN, "Key names only. Enforcement is A03 / R05b / R08c.")
row("GW-15", pk("route publication is a separate integration-agent task"), "Gateway routes for /returns** published", N, "gateway/Diten.ApiGateway/ocelot.json — 269 routes, 0 contain 'shipment-bundle'; the 35 routes on port 5061 are all /api/crm", "n/a", "Shared seam; see REACHABILITY.md.")

# ---- §17 test expectations
for i, (name, verdict, ev, test, note) in enumerate([
 ("DCP-002", M, "see RFD-18-1", "identity gate", ""),
 ("OpenAPI/mock", I, "see A01b", "none", ""),
 ("shipment dependency", M, "see A04", t("ReturnReferenceTests.cs", "Reference_MockedTransport_OnlyShipmentGetAndTrustedScope"), ""),
 ("quantity invariant", M, "see R02a, R04", t("ReturnConcurrencyTests.cs", "Create_Races_RespectsExactCap"), ""),
 ("lifecycle", M, "see R07a", t("ReturnLifecycleTests.cs", "Lifecycle_ActualRepository_All64PairsRejectWithoutWrites"), ""),
 ("idempotency", M, "see A06a", t("ReturnConcurrencyTests.cs", "SameKey_TwentyConcurrentCalls_OneAggregateReceiptEvent"), ""),
 ("tenant isolation", M, "see FP-13-5", t("ReturnIsolationTests.cs", "Scope_AnotherTenantOrLE_CannotReadMutateOrReplay"), "Data scope only."),
 ("RBAC", I, "see A03", "none with a real token or a per-target grant", ""),
 ("outbox", M, "see R10b", t("ReturnConcurrencyTests.cs", "ClosedAndManualReceived_RetainEntitlementOpaqueReferenceAndPendingEvents"), ""),
 ("explicit no-shadow-balance architecture test", N, "no such test in " + TST + " or tests/architecture (searched: 'shadow', 'stock', 'balance' in the Returns tests → 0)", "none", "The property itself holds (AC-16-4); the test the pack names does not exist."),
], 1): row(f"TE-17-{i}", pk("## 17. Test Expectations"), "Test expectation: " + name, verdict, ev, test, note)

# ---- §32.11 UI rows and §33 self-registration
UI = "no Returns UI file exists under frontend/ (find frontend -ipath '*Returns*' → 0 files)"
for label, needle in [("RU-VS1", "| RU-VS1 |"), ("RU-01..03", "| RU-01, RU-02, RU-03 |"), ("RU-04..05", "| RU-04, RU-05 |"), ("RU-06..07", "| RU-06, RU-07 |"), ("RU-08..10", "| RU-08…RU-10 |"), ("RU-11..14", "| RU-11…RU-14 |"), ("RU-15..16", "| RU-15, RU-16 |"), ("RU-17..25", "| RU-17…RU-25 |"), ("RU-26", "| RU-26 |"), ("RU-27", "| RU-27 |"), ("RU-28", "| RU-28 |"), ("RU-29", "| RU-29 |")]:
    row(label, pk(needle), "UI acceptance row(s) " + label + " (§32.11)", N, UI, "none", "Outside Q210's code scope (frontend). UI code is not authorized yet (" + pk("Not authorized by this section: UI code until an integrated target exists") + "). RU-26 names the backend 78/78, which is green pre-composition.")
PROV = "ReverseLogisticsManifestProvider.cs is absent from " + SRC + "Api/ModuleRegistration/ (present: CarrierManagementManifestProvider.cs, ShipmentTrackingPodManifestProvider.cs)"
for i in range(1, 9):
    row(f"M-0{i}", pk(f"| M-0{i} |"), f"Self-registration test M-0{i} (§33)", N, PROV, "none — the test file named in the pack does not exist", "By design the provider ships with the UI (" + pk("### Ship rule (D4 = A)") + ").")

with open(os.path.join(OUT, "ACCEPTANCE-MATRIX.tsv"), "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f, delimiter="\t", lineterminator="\n"); w.writerow(["id", "pack_line", "criterion", "verdict", "evidence_path_line", "test_behind_it", "note"]); w.writerows(rows)

# ---- SCOPE-DELTA
a46 = {x["path"]: x["sha256"] for x in json.load(open("docs/records/audits/2026-09/mvp6-mod0186-r01-independent-ver-01/authority/returns46.json"))}
f341 = {}
for l in open("docs/records/audits/2026-09/mvp6-mod0186-r01-independent-ver-01/manifests/source-final-341.tsv", encoding="utf-8").read().splitlines()[1:]:
    p, h = l.split("\t")[:2]; f341[p] = h
present = []
for r, ds, fs in os.walk(SVC):
    ds[:] = [d for d in ds if d not in ("bin", "obj", "TestResults")]
    for fn in fs:
        p = os.path.join(r, fn)
        if "/Features/Returns/" in p or "/Diten.SupplyChainService.Tests/Returns/" in p or "/tests/returns/" in p: present.append(p)
pl = lines(PACK)
def packline(fn):
    base = os.path.basename(fn)
    for i in range(312, 376):
        if base in pl[i]: return f"{PACK}:{i+1}"
    return ""
sd = []
for p in sorted(set(present) | set(a46)):
    on = os.path.exists(p); h = sha(p) if on else "-"
    auth = packline(p) or (pk("Additional proposed Domain/Features/Returns/ReturnQuantity.cs") if p.endswith("ReturnQuantity.cs") else "")
    if h == a46.get(p): acc, d = "returns46.json " + a46[p][:12] + "…", "PRESENT, AUTHORIZED, byte-equal to the accepted source"
    elif h == f341.get(p): acc, d = "source-final-341.tsv " + f341[p][:12] + "… (returns46.json has the pre-R01-patch hash " + a46.get(p, "")[:12] + "…)", "PRESENT, AUTHORIZED, byte-equal to the accepted final source (R01 product patch)"
    elif not on: acc, d = a46.get(p, "")[:12], "REQUIRED, ABSENT"
    else: acc, d = "-", "PRESENT, NOT in the accepted list" if p not in a46 else "PRESENT, DIFFERS from the accepted source"
    sd.append([p, auth or "NOT LISTED IN §25/§29", "yes" if on else "no", h, acc, d])
extra = [
 (SRC + "Infrastructure/Features/Returns/ReturnOutboxWorker.cs", pk("- `ReturnOutboxWorker.cs`") + " (listed) / " + pk("Effective46pathallowlist") + " (excluded)", "ABSENT — correct: §30 excludes it; §31 says it is not an owned path"),
 (PROG, pk("Existing shared file requiring **separate explicit authorization and single writer**"), "REQUIRED COMPOSITION ABSENT — the file exists but has no Returns line (shared seam; not an owned path; Q209)"),
 (SRC + "Api/ModuleRegistration/ReverseLogisticsManifestProvider.cs", pk("| Provider / tests (proposed paths) |"), "ABSENT — proposed path; ships with the UI (§33 ship rule)"),
 (SVC + "tests/Diten.SupplyChainService.Tests/ModuleRegistration/ReverseLogisticsManifestProviderTests.cs", pk("| Provider / tests (proposed paths) |"), "ABSENT — proposed path; ships with the UI"),
 ("frontend/Diten.Web/** — the 21 owned UI paths", pk("### 32.10 Owned UI paths (21, new files only)"), "ABSENT (0 of 21) — UI not authorized yet (§32.14)"),
 ("docs/records/audits/2026-09/mvp6-mod0186-phase15-close-01/owned-paths.txt", cite(SVC + "tests/returns/runtime_probe.py", "owned-paths.txt") + " (read by the probe); " + pk("The original `mvp6-mod0186-phase15-close-01/` package named in §30 is not in the common checkout"), "ABSENT — an owned probe depends on a record that is not in the checkout; runtime_probe.py and restart_probe.py cannot run as written"),
]
for p, auth, d in extra: sd.append([p, auth, "yes" if os.path.exists(p) else "no", sha(p) if os.path.isfile(p) else "-", "-", d])
with open(os.path.join(OUT, "SCOPE-DELTA.tsv"), "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f, delimiter="\t", lineterminator="\n"); w.writerow(["path", "pack_authority", "present", "sha256_on_disk", "accepted_source_hash", "delta"]); w.writerows(sd)

# ---- BOUNDARY-REFS
RC = SRC + "Application/Common/RequestContext.cs"
P, V = "PERMITTED", "BOUNDARY VIOLATION"
b = [
 ["B-01", cite(SRC + "Domain/Features/Returns/ReturnOrder.cs", "using Diten.SupplyChainService.Domain.Common;") + "; " + cite(SRC + "Domain/Features/Returns/ReturnOrder.cs", "public sealed class ReturnOrder : EntityBase"), "Domain.Common.EntityBase", "shared kernel (not a feature)", P + " — shared base, read-only reuse", pk("Existing base controller/entity"), ""],
 ["B-02", cite(CTRL, "using Diten.SupplyChainService.Api.Controllers;") + "; " + cite(CTRL, ": CustomBaseController"), "Api.Controllers.CustomBaseController", "shared kernel", P + " — shared base, read-only reuse", pk("Existing base controller/entity"), ""],
 ["B-03", "8 files, e.g. " + cite(SRC + "Application/Features/Returns/Commands/CreateReturnCommand.cs", "using Diten.SupplyChainService.Application.Common;") + "; " + cite(CTRL, "using Diten.SupplyChainService.Application.Common;"), "Application.Common.Response<T>", "shared kernel", P + " — internal envelope, adapted at the API", pk("No outer data envelope; internal Response<T> is adapted at API."), ""],
 ["B-04", cite(MW, "loggingContext.Scope = new(tenant, le, actor);") + " (type declared at " + cite(RC, "public ShipmentScope Scope") + ", " + cite(RC, "using Diten.SupplyChainService.Domain.Features.Shipments;") + ")", "Domain.Features.Shipments.ShipmentScope, constructed through the shared Application.Common.RequestContext", "MOD-0183 domain type", V + " (type-level, low) — not a read-by-contract: Returns code constructs a MOD-0183 domain type. No Shipment data is read or changed, and no intake artifact is touched.", pk("read-only reuse; family wire adapter cannot invoke Shipment-specific context/error policy accidentally"), "The coupling originates in the shared RequestContext, which is typed with ShipmentScope. The file is byte-equal to the CT-accepted source, so this was accepted before. CT to rule; no fix made."],
 ["B-05", cite(READER, "new HttpRequestMessage(HttpMethod.Get"), "HTTP GET /api/shipment-bundle/shipments/{shipmentId}", "MOD-0183 published operation getShipment", P + " — read-by-contract", pk("**Consumes:** MOD-0183 shipment/POD") + "; " + cite(ANNEX, "published getShipment"), "Only outbound call in Features/Returns. Test: " + t("ReturnReferenceTests.cs", "Reference_MockedTransport_OnlyShipmentGetAndTrustedScope")],
 ["B-06", cite(READER, '"Draft", "Planned", "Dispatched"') + "; " + cite(READER, 'string[] required = ["lineNumber", "itemId", "skuId", "quantity", "uomId"];') + "; " + cite(READER, '"SHIPMENT_ROOT_INVALID"'), "Shipment status enum, line/POD field names and one error code, as string literals", "MOD-0183 wire contract", P + " — contract literals, no code reference", cite(YAML, "  /returns:") + " (same published bundle)", ""],
 ["B-07", cite(REPO, 'if (observation.Status is not ("Delivered" or "Closed"))'), "Shipment status literals 'Delivered' / 'Closed'", "MOD-0183 wire contract", P + " — contract literal (eligibility rule)", cite(ANNEX, "Delivered/Closed only"), ""],
 ["B-08", cite(SRC + "Domain/Features/Returns/ReturnOrder.cs", "InventoryTransactionReferenceId") + "; " + cite(REPO, "order.InventoryTransactionReferenceId = inventoryReference;"), "inventoryTransactionReferenceId (opaque string)", "INVENTORY", P + " — opaque text; no Inventory namespace, client, call or collection", cite(ANNEX, "Inventory and Warehouse HTTP calls ZERO"), ""],
 ["B-09", cite(SCHEMA, '"returns", "return_entitlements", "returns_receipts", "returns_audit", "returns_outbox"'), "Mongo collections", "none outside the module", P + " — five module-owned collections only; no Shipment/Carrier/Load/intake collection is opened", pk("Collections owned only"), ""],
 ["B-10", t("ReturnAtomicityTests.cs", "Diten.SupplyChainService.Persistence.DependencyInjection.AddPersistence"), "Persistence.DependencyInjection.AddPersistence (test fixture)", "shared composition", P + " — test fixture reuses the shared Mongo registration", pk("Persistence extension reuses existing MongoClient"), ""],
 ["B-11", t("ReturnIsolationTests.cs", "new Diten.SupplyChainService.Application.Common.RequestContext()"), "Application.Common.RequestContext (test)", "shared kernel typed with a MOD-0183 type", "same as B-04", "", "Test-side consequence of B-04."],
 ["B-12", "grep 'Features.Returns' in " + SVC + " outside Returns folders → 0 files", "reverse direction: other features referencing Returns", "-", "none found", "", "No other feature depends on Returns."],
 ["B-13", "grep in Features/Returns for Features.(Shipments|Carriers|Loads|Claims|SandopPlans|CapacityPlans), IEventOutboxStore, Warehouse → 0 explicit references", "explicit using / type references into another feature", "-", "none found (B-04 is implicit, through target-typed new)", "", ""],
]
with open(os.path.join(OUT, "BOUNDARY-REFS.tsv"), "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f, delimiter="\t", lineterminator="\n"); w.writerow(["id", "reference_path_line", "referenced_symbol", "owner", "verdict", "pack_or_contract_basis", "note"]); w.writerows(b)

from collections import Counter
print("matrix rows", len(rows), dict(Counter(r[3] for r in rows)))
print("backend only", dict(Counter(r[3] for r in rows if not r[0].startswith(("RU", "M-")))))
print("scope", dict(Counter(r[5].split(",")[0].split(" —")[0] for r in sd)))
