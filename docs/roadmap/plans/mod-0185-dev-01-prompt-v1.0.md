# MVP6-MOD0185-P01 v1.0 — DEV READY

WP MVP6-MOD0185-DEV-01; lane AL-MVP6-MOD0185-DEV01; Type DEV; target @orchestrator /add-module, backend-architect + testing-agent responsibilities in one sole writer. Profile B; HIGH; target E2/E3/E4. Date2026-09-18.

Repository /Users/natig/Projects/ERP-vNext-recovery; branch feature/mvp6-logistics; expected HEAD4a8d4d4b339528a88e6220fb8402e5a2c771136c. No branch/index/commit/push/stash mutation. Pre-dispatch baseline /private/tmp/mvp6-mod0185-dispatch-ng7utit_/baseline.json;14,443 files before pack/prompt preparation. DEV must take its own fresh baseline after dispatch files. Existing dirty changes are protected inputs.

## Authority / prerequisites

Read AGENTS.md, .antigravity/agents/orchestrator.md, backend-architect.md, testing-agent.md, workflows/add-module.md and relevant backend/security/persistence/configuration/testing rules; CT SOP§17/22. Domain execution/domains/supply-chain-execution/domain-config.md; pack execution/domains/supply-chain-execution/module-packs/MOD-0185-routing-load-planning.md ready-for-dev effective§28, exact§25 and A01–A12. DCP009 ownership read-only; DCP002 identity freshly PASS. shell:none, golden_reference:none,form_field_count0. User explicitly approved Phase1.5, pack promotion, single writer and mock-first DEV. No UI/gateway phase applies.

Read published docs/analysis/contracts/shipment-bundle.openapi.yaml SHA93c696e2fba13dbc8fbfcf2cd1ae0ae0bd93cd9d0935b3ee743229e810163571 and loads-semantics-v2.0.0.md SHAa2187c934cf9176cae4cb8b9c70dfd1298154e5124cdc197d3029103636c2be1 completely. Annex preserved candidate-status text is superseded by docs/records/audits/2026-09/mvp6-loads-r2-publication-2026-09-18/README.md and current approval; normative semantics frozen. Read D185 and R185 decisions. No need to reopen settled approvals.

## NE / objective

Implement bounded Loads three-operation HTTP core in existing Diten.SupplyChainService5061 with five layers, exact frozen request/response/error schemas and Pending-only transactional outbox. Build, meaningful service and actual isolated HTTP/Mongo tests; completed SOP22 handoff.

## NEDEN

Published schemas and test-consumer models are not runtime enforcement. Produce source/binary-bound evidence for A01–A12 without operational rollout.

## NASIL / ownership and consistency

One DEV writer. Exact§25 filenames in Features/Loads across Domain/Application/Api/Persistence/Infrastructure, named Loads C# tests and three loads Python probes. EXCLUDE LoadOutboxWorker.cs entirely. Shared exception only existing Api/Program.cs: register Loads context/reference client/persistence and exact-family middleware/model-validation mapping; preserve Carrier/Shipment branches, health/auth/global settings and Shipment outbox worker. No other existing source/project/config/test change. If new path required, report before expanding; do not silently invent allowlist.

Persist only loads/load_assignments/loads_receipts/loads_audit/loads_outbox; tenant+LE, soft delete, immutable root, atomic current-state/Version, exact receipt scope/fingerprint, unique reservations. Audit snapshots and one frozen event per fresh successful state change in same Mongo replica-set transaction. Pending status only; no publisher/worker/shared outbox replacement. Completed retains assignments; only Cancelled conditionally releases old owner's assignments. No inventory reservation/Shipment mutation.

Scoped GET-only HTTP reference readers use frozen mocks: Carrier Active list validation (full required summaries), per-ID Shipment detail profile. No DB/internal-type sharing. Preserve request correlation/scope in outbound evidence; explicit configured fixture endpoint, fail closed if unavailable. Fresh retry rereads dependencies, successful replay/Completed/Cancelled skip them. HTTP observations do not lock remote state. Exact published error/auth/header/query/key lexical order and root-before-fingerprint semantics mandatory.

Use current service implementation read-only as engineering reference; don't copy Carrier policy where Loads differs. CQRS and separate validators/handlers; internal Response<T> adapted to plain contract. Reuse EntityBase and four pipeline behaviors read-only. Test schema nullability/string/array/decimal/UUID/date-time fingerprint exactly. No new business rule.

## YAPMA

No frozen-contract/annex/guard/seal edit; no other modules/Platform/HCM/Talent, gateway, registry, DCP, board, UI, shared permission seeding, live reference consumer rollout or stock writes. No operational Mongo27017, operational credentials/data/migration. No package promotion beyond already ready-for-dev; no E5/G5 or CT acceptance claim. No commit/push/stash. No LoadOutboxWorker file or worker registration.

## DOĞRULA / validation plan

Build service; run entire service suite plus Loads meaningful tests. Frozen examples/ref validation can read contract; no copied successful mock responses presented as runtime. Isolated Mongo replica set (DB010 fixed test DB names, unique tenant fixtures), owned local mock HTTP server(s); no operational data. Log exact commands/processes/binary/source hashes and clean owned processes/failpoints only.

A01–A12 explicit matrix: schema/three routes; real JWT independently permissioned scopes2×2; exact errors/precedence, duplicate claims/headers and six query keys; full lifecycle source×target; reference malformed/missing/null/foreign/inactive/mode/profile/unavailable; HTTP GET only and actual outbound request captures; create business and stop checks; same/different root/payload, past-state replay/no reread, actor change and restart; simultaneous assignment and cancellation races, retired/deleted/reserved states; retry rereads; number collision control; fault after each aggregate/constraint/receipt/audit/event write and before commit; postcommit lost response; actual unknown-commit retry/exhaustion; index failure/standalone/outage startup/recovery. Persisted DB snapshots/counts for every atomicity claim. Outbox Pending after restart/replay, correct root/causation/event time. Actual sent body bytes including zero-byte GET; no secrets retained.

Regression: existing Shipment/Carrier service tests and bounded runtime probes against isolated data, sources untouched. Architecture fresh measure; known50/3 external Platform/HumanCapital/Talent failures remain separate, never assumed PASS. Frozen hashes/protected baseline preservation plus exact Program diff. Tests can't silently skip Mongo; report unsupported environment as blocker.

## Output / failure protocol

Only new DEV evidence under docs/records/audits/2026-09/mod-0185-dev-01/ plus /private/tmp unique working outputs. Avoid unsealed executable/JSON references into legacy docs paths: keep authority pointers in Markdown; don't weaken guard. Report SOP22, exact changed manifest (self-hash excluded only for itself), build/test/runtime evidence, A01–A12, remaining gaps, protected hash proof and independent VER handoff. API usage notes in owned report directory; shared README/UI manual N/A. Immutable prior evidence untouched.

Stop affected portion for a core contract/authority ambiguity, uncontrolled writer drift or required path expansion; report exact blocker. A failing implementation/test is reworked within approved scope, not waived. Independent read-only VER starts only after sole DEV writer finishes, then separate DEV rework if findings. Developer PASS is not independent acceptance.
