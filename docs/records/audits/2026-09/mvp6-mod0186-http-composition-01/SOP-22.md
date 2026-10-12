# MVP6-MOD0186-HTTP-COMPOSITION-01 — SOP §22

Verdict: REVIEWABLE exact integration candidate; disposable apply/build PASS; real application HELD pending Returns-specific owner decision.

## Baseline and order
Common checkout Program remains7fdb5ef0…04d8; it is NOT the patch baseline. Selected actual baseline comes from registered Claims worktree /Users/natig/.codex/worktrees/claims-dev-start-01/ERP-vNext-recovery and hashes a28cb1ab5b7dc235cb68bb0bf7ad72cdc4889a49846ab6fdd4f01601285ab34a, matching applied authorized Claims target. Order: approved Claims composition → verify exact46Returns core delivery → this Returns-only Program diff. Never overwrite with old Returns/Loads baseline. Existing independent VER snapshots not accessed for mutation.
Returns core source: registered returns-phase15-close-01 worktree, writer-complete CORE-DEV-02;46source entries and Phase1.5allowlist match exactly. Published Returns annex and canonical hashes recorded in inputs.json. Full selected source inventory accompanies handoff; no HEAD-only assumption about dirty/untracked dependencies.

## Exact delta
Four Returns namespaces; AddReturnPersistence(); typed IReturnReferenceReader/ReturnReferenceReader client timeout5s; Returns InvalidModelStateResponseFactory branch; ReturnContextMiddleware after authentication/before authorization; Returns exclusion in generic Shipment branch. Existing Claims/Carrier/Loads registrations preserved. Auth/JWT/serializer/controller routes unchanged; no permission/worker added.
Handoff's extra AddScoped<ReturnRequestContext>() intentionally omitted because actual ReturnPersistenceRegistration already registers it. AddReturnPersistence also registers existing ReturnSchema startup index checks: schema initializer, not a delivery worker. No separate startup duplicate registration.
Returns:ReferenceBaseUrl is consumed by existing reader; value absent/invalid returns503. No environment config or live dependency endpoint invented. Deployment/test configuration remains separately owned.

Baseline SHA: a28cb1ab5b7dc235cb68bb0bf7ad72cdc4889a49846ab6fdd4f01601285ab34a
Program-only patch SHA: 70b80f7920f0d216ccd22795328df750a761c949e438a55ede7876c57cbf6444
Target SHA: a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c

## Verification
Fresh disposable copy of actual Claims checkout plus exact46Returns source/test files. patch --batch --fuzz=0 applied successfully and target bytes matched. Fresh API local-cache restore and build exit0,0warnings,0errors. Initial sandbox restore stalled and was terminated; permitted retry logs retained. No product/test source repair.
12source-composition checks PASS: Claims DI/client/model-state/middleware preservation; single Returns persistence/context wiring;5s client; middleware order; Shipment exclusions; unchanged auth/serializer/hosted workers. All selected pre-build source hashes rechecked after build. Binary hash in manifest.json. Static composition/build evidence only; no API process/HTTP/JWT/database/index startup/producer uptake/restart/independent runtime VER claim.

## Authority
Actual user record session09/18…8677.jsonl:6779 approves exact Claims Program diff, explicitly excludes Returns Program changes. authority-source.md preserves text. Current task permits preparation and disposable validation; no exact Returns application grant found. Claims grant was not transferred. Real checkout/worktrees unmodified. Single concrete owner decision in OWNER-DECISION.md.

## Delivery and limits
Patch applies only to selected Program hash; Returns core46 files are dependencies, not patch contents. Real target must first contain their approved handoff bytes; this task does not transfer them or change Claims business sources. Source inventory is snapshot identity, not broad edit authority. After approval, integration owner rechecks source pins before apply/build then hands source/binary hashes to Returns HTTP verifier. Core68/68 and Claims historical tests are not relabelled as fresh integration evidence.
Only new audit directory and disposable working directory written. No canonical/guard/gateway/pack/runtime-business/git changes, no commit/push/stash, no independent VER snapshot mutation. Remaining gate: exact Returns Program application consent plus matching integration source prerequisites; runtime acceptance separate.
