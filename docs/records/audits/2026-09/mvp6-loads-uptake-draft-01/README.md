# Q77a — MOD-0185 Loads producer uptake + F-1 fix — DRAFT source overlay (MVP6-WP-185-UPTAKE-DRAFT-01)

🤖 Applying knowledge of @backend-architect + @testing-agent.
Lane AL-MVP6-185-UPTAKE-DRAFT-01 (DEV, draft overlay — NOT writer-complete), chat lane on the linked Mac folder. Repo `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Start 2026-09-26T16:17:42+03:00 (Europe/Istanbul). No product-path writes, no git writes, no ledger edits, uncommitted (Q03a).

> **DRAFT — not built.** No .NET build, test or runtime ran here. Build, tests and runtime are Q77b on the local Mac (NOT-VERIFIED.md).

Authority: owner decision `docs/records/decisions/2026-09/mvp6-loads-uptake-ph15-owner-decision-01.md` (Decision 1: Phase 1.5 table — `queryLoads` emits the persisted root; missing/null/malformed → null; the list never fails; no new endpoint, no backfill. Decision 2: F-1 fixed together — a transition on a Load without a stored root is rejected safely, no contract or endpoint change); the draft-overlay rule (CT conversation ~16:17); Q03a no commit. Inputs: `docs/roadmap/plans/mvp6-loads-uptake-prep-01/` (8/8), contracts YAML `6dc1dd48…` and annex `9d8a3706…`.

## What the overlay does

| Part | Change |
|---|---|
| Read model | New `LoadReadResult` + `LoadRootState {Missing, ExplicitNull, InvalidStoredValue, PresentStoredUuid}` (Domain). New `LoadRootMaterializer` (Persistence): raw `BsonDocument` → state + UUID-or-null; a detached copy without the field is deserialized; the raw document is never mutated or written. Pattern: `ShipmentDetailMaterializer.cs` (`832bc229…`) |
| queryLoads | `LoadRepository.QueryAsync` reads raw documents with the same filter, read concern and 503 mapping; `GetLoadListHandler` emits `LoadSummary.lifecycleCorrelationId` = stored value for `PresentStoredUuid` (including nil), otherwise `null`. One bad row never fails the list |
| F-1 | Transition read in `LoadRepository.MutateAsync` (preimage lines 43-44 only) goes through the same materializer. Unless the stored root is a present UUID equal to the inbound correlation, the transition is rejected with the existing `409 CORRELATION_ROOT_MISMATCH` before lifecycle checks, dependency reads or any write. A missing root can no longer match a nil inbound correlation |
| Unchanged | Create path (byte-identical), every write, routes, permissions, contract, error body, DI, Program.cs, `.csproj`, Gateway; no migration, no backfill |
| Tests | New `LoadRootStorageTests` (unit), `LoadRootQueryTests` (API + replica set), `LoadRootTransitionTests` (F-1); one assertion block added to `LoadContractTests` |
| Evidence tools | `verify_evidence.py` repinned to 3.1.0 (YAML `6dc1dd48…`, annex `9d8a3706…`) plus 2 checks on the listed root; `runtime_probe.py` list-field assertion as a patch |

Files: 12 overlay entries (11 full files + 1 patch) — FILE-PLAN.tsv, SOURCE-MANIFEST.tsv, archive `overlay.tar.gz`. Acceptance mapping: COVERAGE.tsv (LU-01…LU-15 + F1-01…F1-04; every row mapped to a test or to the Mac plan). Static checks: STATIC-CHECKS.txt (plan ↔ manifest ↔ overlay 12/12/12; tree-sitter syntax parse 10/10; checker self-test 6/6; route/permission/config delta 0).

## FINDINGS

- **FN-01 — F-1 error code.** The 3.1.0 contract has no code named for "Load has no stored root". The overlay uses the existing transitionLoad `409 CORRELATION_ROOT_MISMATCH` (YAML line 1216/1231; annex line 215: "a different authoritative root returns 409"). A missing, null or invalid stored value is not an authoritative root, so no inbound correlation can equal it. Not chosen: `503 PERSISTENCE_UNAVAILABLE`, whose message tells the client to retry with the same key, which would never succeed. See A-01.
- **FN-02 — Behaviour change only for rows without an authoritative root.** Before, on the transition path:
  - A missing root plus a **nil** inbound correlation **succeeded** (the F-1 defect).
  - A missing root plus any other correlation already returned 409.
  - A stored null, malformed or wrong-type root failed with **500 INTERNAL_ERROR**, because typed deserialization threw an exception caught at `LoadRepository.cs:93`.

  After, all of these return 409 with no writes. A valid stored root behaves exactly as before.
- **FN-03 — LU-10 wording.** ACCEPTANCE.tsv LU-10 says "Transition unchanged". That holds for valid rows. For rows without a stored root, owner Decision 2 (F-1) changes it on purpose; F1-01…F1-04 cover it.
- **FN-04 — Lexical rule.** A stored root counts as present only in the exact 8-4-4-4-12 form (`LoadWire.Uuid` + `TryParseExact("D")`), the rule the Loads middleware uses for `X-Correlation-Id`. `ShipmentDetailMaterializer` uses the looser `Guid.TryParse`. Braced or 32-hex stored values therefore read as invalid (null in the list, 409 on transition). Create always stores the D form through `GuidSerializer(BsonType.String)`, so only tampered data is affected. See A-02.
- **FN-05 — `restart_probe.py`** is not an owned path and does not assert the new field. LU-12 is covered by the integration test `ListedRootsSurviveApiRestart` plus a manual list check in Q77b.
- **FN-06 — `runtime_probe.py`** is delivered as a patch, not a full copy, because the file carries a pre-existing lane-local test signing constant; that constant is not copied into this folder.
- **FN-07 — Write-back on transition.** A successful transition still replaces the whole Load document, as it does today. A stored upper-case root is therefore rewritten in lower case on a successful transition, exactly as the current typed read/replace does. The read path never writes.

## ASSUMPTIONS

- **A-01** F-1 uses `409 CORRELATION_ROOT_MISMATCH` (FN-01); CT or the owner can confirm or name another existing code.
- **A-02** Strict lexical rule for a present stored root (FN-04).
- **A-03** Wire form: `Guid?` via System.Text.Json → lower-case canonical string or explicit `null`; the key is always present (Program.cs sets no ignore-null condition; prep A4).
- **A-04** Unit-test seeds use BSON types the default serializers read without the app's registration, as `ShipmentRootStorageTests` does (NOT-VERIFIED item 1).
- **A-05** LU-07 "malformed / wrong type" is covered with malformed, braced, empty-string, Int32 and BSON-binary UUID values.
- **A-06** The overlay applies on top of the ISOLATED-ENV composition (HEAD archive + BC-SOURCE `ebd5d80c…`). The preimages here equal the working tree, which equals BC-SOURCE per prep A5. Q77b re-checks every preimage and stops on mismatch.
- **A-07** F-1 estimate delta (agent estimate by analogy, not in any ledger): backend 0.5/1/2 h + tests 1/2/3 h = **+1.5/3/5 h** O/M/P on top of the Q68 total 11.5/19/35 h.
- **A-08** FILE-PLAN widening for F-1 is the `LoadRepository` transition read plus one new test file. `TransitionLoadHandler`, `TransitionLoadValidator`, `TransitionLoadCommand`, `LoadsController` and `LoadContractError` were reviewed and need no change: the rejection is raised in the repository and returned through the existing error path.
- **A-09** The draft-overlay rule (CT, ~16:17) is applied as stated in the prompt; no repository record for it was found by this lane.

## Q77b plan (local Mac, Terminal)

1. Compose the isolated env per `ISOLATED-ENV.md` (verify every input hash).
2. Verify SHA256SUMS of this folder, then check each `preimage_sha256` in SOURCE-MANIFEST.tsv against the env.
3. Extract `overlay.tar.gz` (or copy `overlay/`); apply `runtime_probe.py.patch` with `git apply`. Check every `result_sha256`.
4. `dotnet build`, then run the Loads suites (new + existing) and the full test project with TRX against an isolated replica set on a lane port. Then run `runtime_probe.py` + `verify_evidence.py`, and the restart check.
5. Write the evidence to `docs/records/audits/2026-09/mvp6-loads-uptake-dev-01/`. An independent VER follows (LU-15).

## Files

`README.md`, `FILE-PLAN.tsv`, `SOURCE-MANIFEST.tsv`, `COVERAGE.tsv`, `STATIC-CHECKS.txt`, `NOT-VERIFIED.md`, `overlay/` (12 entries), `overlay.tar.gz`, `SHA256SUMS`.
