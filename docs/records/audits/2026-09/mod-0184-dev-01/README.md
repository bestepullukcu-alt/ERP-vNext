# MOD-0184 DEV-02 — bounded Carrier implementation handoff

Date: 2026-09-17. WP `MVP6-MOD0184-DEV-02`, prompt `MVP6-MOD0184-P02 v2.0`, lane `AL-MVP6-MOD0184-DEV02`.

**Agent verdict: bounded implementation and developer verification PASS; independent VER required.**
Repository architecture gate remains **BLOCKED: 14 PASS / 4 FAIL**, not the dispatch's assumed 15/3. The fourth failure is pre-existing preparation/consumer evidence under protected paths, proven below. No CT acceptance, E5/G5, gateway or complete-module delivery claim.

## Authority and baseline

Branch `feature/mvp6-logistics`; HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c` unchanged. Authority: AGENTS.md, SupplyChain domain, MOD-0184 ready-for-dev pack §29 superseding earlier draft text, published SHIPMENT-BUNDLE1.1.0 and normative Carrier annex, DEV02 v2.0 and owner release record. Canonical/annex hashes remain dispatch hashes.

Pre-existing dirty inputs were accepted at dispatch and preserved. `baseline-hashes.sha256` records 14,327 non-generated files; **14,326 protected files remain byte-identical**. Only existing source exception is `Program.cs`; exact diff in `program.diff`. All new source belongs to the pack's Carrier feature/test directories; new evidence is in this directory. No commit, push, staging or stash operation. No external consumer/publisher decisions changed.

## Changed files and contract flow

`changed-files.json` contains the complete new/modified file list with SHA256, with a null self-reference only. `preservation.json` records the protected comparison. Source is five layers:

- Domain: EntityBase-derived Carrier, exact lifecycle enum/table, scoped repository contract.
- Application: separate create/status CQRS and list query, handlers, validators, Carrier context and collision-safe canonical fingerprint text. Whitespace/case/mode order/duplicates preserved; missing/null externalReference equivalent.
- API: exactly GET/POST carriers and POST carrierId/status, inherited CustomBaseController with unwrapped contract wire adaptation. Carrier-only ordered authentication, signed claims, permissions, UUID/header, scope, path/query/content-type gates.
- Persistence: tenant+LE filtered repository, unique ordinal code/replay indexes and atomic Carrier + success receipt + audit transaction. Retired/deleted codes remain reserved. Historical replay precedes current lifecycle/deletion lookup. No TTL.
- Infrastructure: exact three permission constants/attribute. No external client, Supplier master, Inventory write or Carrier event.

Program.cs registers Carrier context/store/schema, isolates middleware by Carrier family, and uses a Carrier-only model validation error. All existing Shipment source is otherwise untouched. Carrier middleware populates the existing request-scoped diagnostic context **only after Carrier authorization**, so the unchanged shared logging pipeline uses correct tenant/LE/current correlation; Carrier business scope/errors still use CarrierRequestContext. No cross-request or Shipment context reuse.

## Tests and measured results

| Check | Fresh result | Evidence |
|---|---|---|
| Build | PASS, 0 warnings/errors | build.log |
| Full service tests | **99/99 PASS**, 66 pre-existing + 33 Carrier cases | service-tests.log, carrier-service.trx |
| Capture regressions | 2/2 PASS | capture-tests.log |
| Carrier real HTTP + schema | 33/33 PASS | carrier-runtime/runtime.json |
| Carrier two-process restart | all 3 scoped collections identical; historical replay retained | carrier-runtime/runtime.json |
| Immutable request capture | 33 request snapshots exactly match retained sent-body bytes | carrier-runtime.log |
| Live persistence outage | Q/C/S 503; unavailable startup exits without listener; restored persistence same-key fresh201 then replay201 | carrier-outage/outage.json |
| Actual Mongo unknown-commit labels | 2 injected commitTransaction errors recovered; 3 attempts exhausted to503, same key recovered; one entity/receipt/audit | carrier-outage/commit-uncertainty.json |
| Shipment unchanged HTTP regression | 33/33, zero capture mismatches; restart and boundary semantics retained | shipment-regression/sent-body-comparison.json, shipment-regression.log |
| Standalone Mongo | rejected before listener | standalone/startup-rejection.log, standalone.log |
| DCP002 | PASS MOD-0184 | dcp002.log |
| Architecture | **14 PASS / 4 FAIL** | architecture.log, carrier-architecture.trx, architecture-baseline-proof.md |
| Preservation/diff | 14,326 protected hashes unchanged; diff check PASS | preservation.json, baseline-hashes.sha256 |

### Acceptance mapping C01–C12

- **C01:** Exactly three controller operations; frozen schema validation for success/errors in real HTTP; no extra routes or envelope. Published create422 example checks **N/A**, not runtime PASS.
- **C02:** Required/missing/null/unknown bodies; whitespace code/name; duplicate modes; empty reason; externalReference null/missing; exact enum and string checks. 1/128/129 scalar key corpus, including 128/129 emoji in parser-level HTTP tests. Transport OWS/encoding limitations remain explicitly separate.
- **C03:** 2 tenants × 2 LEs, overlapping codes, isolated lists/status/replay/audit, scoped soft-deleted fixtures and historical receipt behavior. Scope-header mismatch before body evaluation.
- **C04:** Real JwtBearer signature validation and independent read/create/status permission tests. Duplicate signed JSON claim members fail403; duplicate sub encoded as an array is rejected by JwtBearer parsing401 (annex step1), not misrepresented as validated-context403. No body/header error exposes token/tenant/payload.
- **C05:** Concurrent distinct-key same-code creates yield one success; exact/case-sensitive uniqueness; reserved retired/deleted code behavior.
- **C06:** All nine lifecycle pairs through HTTP and Mongo; valid transitions update version/audit once; forbidden transitions leave three collections unchanged.
- **C07:** Same-key create/status concurrent replay, semantic fingerprint equivalence/differences, changed correlation/actor, operation/target/scope isolation, old result after later state and deleted-target receipts. No duplicate business audit.
- **C08:** Explicit start barrier for distinct valid Suspended/Retired requests; persisted audit history equals one legal serial order and ends Retired with exact version/receipt count. Same-target contention also tested.
- **C09:** Fault after entity, receipt, audit and before commit rolls back all writes for create and status. Post-acknowledged-commit injected failure proves lost-response recovery only. **Separately**, real Mongo failCommand labels on commitTransaction prove unknown-result retry/exhaustion branches and same-key recovery. No false rollback claim for unknown outcome.
- **C10:** Process restart durability, live proxy outage503 for Q/C/S, unavailable startup, standalone rejection and index creation failure. No in-memory fallback. Test DB names fixed; each test owns fresh tenant fixtures.
- **C11:** Nil UUID accepted, valid correlation canonicalized, error body/header match; first audit root/actor retained under replay. Carrier outcome logs include scoped tenant/LE/current correlation/status/replay. No Carrier lifecycle outbox/events.
- **C12:** Full non-generated baseline preservation with only approved Program.cs exception, frozen authority unchanged. The fourth architecture failure existed in 17 protected preparation/consumer files at dispatch; no new Carrier/evidence file is named by the final guard.

## Persistence, failures, observability and rollback

Collections: `carriers`, `carrier_idempotency`, `carrier_audit`. All application access is scoped; ordinal unique code includes retired/deleted entities. Replay tuple includes tenant, LE, operation, target and unchanged key. Receipt retains original status/body fields/correlation/actor; current HTTP response has current trace only. No raw payload logging, JWT or connection string retained in saved evidence. A positive-controlled counting repository test proves request validation stages1–5 call the repository zero times.

Transactions use primary/snapshot/majority, at most12 transient/duplicate retries and at most3 unknown-commit attempts, with max commit time5s. Unexpected failures are sanitized500; persistence unavailability/unknown commit maps503. Read-only queries map persistence outages503. No returned503 is evidence of rollback. Server failpoints run only on the owned test replica set and were disabled in finally. Postcommit test-hook exceptions are explicitly differentiated from actual Mongo-label tests.

Startup creates indexes and verifies transaction support; failure prevents readiness. Rollback plan: stop Carrier exposure and revert only the approved composition change under owner authorization; preserve Carrier data/receipts/audit. No destructive migration or data cleanup performed.

## Reproduction

Use an isolated replica set with `enableTestCommands=1` for the deterministic failpoint probe. Development fixture is `127.0.0.1:27684`, replica set `mod0184`; it is unrelated to operational Mongo27017. API processes use5061 and reject an existing listener. Mongo fixture remains available for independent VER; service/proxy/standalone test processes are stopped.

```sh
export MOD0184_TEST_MONGO='mongodb://127.0.0.1:27684/?replicaSet=mod0184&serverSelectionTimeoutMS=3000'
export MOD0183_TEST_MONGO="$MOD0184_TEST_MONGO"
export PATH="/Users/natig/.dotnet:$PATH"
dotnet build services/Diten.SupplyChainService/Diten.SupplyChainService.sln --no-restore -m:1 /nr:false
dotnet test services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj --no-restore -m:1 /nr:false
python3 services/Diten.SupplyChainService/tests/carriers/runtime_probe.py /private/tmp/carrier-ver-runtime docs/analysis/contracts/shipment-bundle.openapi.yaml
python3 services/Diten.SupplyChainService/tests/carriers/verify_evidence.py /private/tmp/carrier-ver-runtime
python3 services/Diten.SupplyChainService/tests/carriers/restart_probe.py /private/tmp/carrier-ver-outage
python3 services/Diten.SupplyChainService/tests/sent_body_probe.py /private/tmp/carrier-ver-shipment docs/analysis/contracts/shipment-bundle.openapi.yaml
python3 -m unittest discover -s services/Diten.SupplyChainService/tests -p test_runtime_capture.py
python3 services/Diten.SupplyChainService/tests/startup_probe.py /private/tmp/carrier-ver-standalone
dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --no-restore -m:1 /nr:false
python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0184 --name 'Carrier Management'
git diff --check
```

Run process probes sequentially. Fixed fixture databases are `diten_mod0184_tests`, `diten_mod0184_runtime_tests`, `diten_mod0184_outage_tests`, `diten_mod0184_index_failure_tests`; baseline Shipment uses its unchanged fixed database names. Index-failure fixtures deliberately retain duplicate test data in their separate fixed database.

## Decisions, blockers and gaps

ASSUMPTION: owner-approved module pack exceptions supersede generic response/repository/versioning conventions; no new authority inferred. Minimal source-writer mandate supersedes generic shared backlog/registry updates; CT handles governance after independent verification.

**Blocker:** dispatch architecture assumption15/3 was stale. Final14/4 includes the three known Platform/HCM/Talent failures and a DocsPathGuard failure in17 pre-existing protected files, all hashed at baseline. This developer neither waived the additional failure nor edited those files. See architecture-baseline-proof.md for exact paths/hashes. Central CT must dispose this baseline discrepancy before claiming the requested repository gate.

Gateway/catalog provisioning, UI, E5/G5, Shipment ingress/GAP01–04, MOD0185+ and other services remain out of scope. No runtime consumer integration claimed from fixture conformance. Independent VER and CT acceptance remain separate. No commit/push authorization exercised.
