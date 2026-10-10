# Q212 — Is 308 lines the bounded core, or a shortfall?

Static reading only. Authority: the pack
(`execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md`) and the owned-path file
(`docs/roadmap/plans/mvp6-mod0190-0192-dispatch-preflight-01/MOD-0190-OWNED.tsv`).
Per-path table: `OWNED-PATH-COVERAGE.tsv` (38 rows).

## 1. Verdict

**308 lines is the bounded 38-path core, as it was delivered and as CT accepted it. It is not a missing-work
shortfall.** Every owned path exists, and every one is the same bytes as the accepted source.

**It is thin in two specific ways that the pack does not ask for,** and both are worth a CT decision:

1. **Layering is hollow in places.** The module's logic sits in one repository file. Three domain entity files are
   never used at run time; the handlers and validators are pass-throughs.
2. **The test layer is thinner than the pack's own §17.** There is no handler test, no API test, no contract-shape
   test and no mock smoke.

Neither changes what the module does. Both change how much the 19 green tests prove.

## 2. Why the line count misleads

| Measure | MOD-0190 S&OP | MOD-0192 Capacity | Ratio |
|---|---:|---:|---:|
| Source files | 32 | 35 | 91% |
| Source lines | 308 | 904 | **34%** |
| Source bytes | 40,557 | 65,428 | **62%** |
| Longest line | 551 characters | — | |
| Tests (Q208) | 19 | 48 | 40% |

- S&OP source is written as long lines. `SandopRepository.cs` is 94 lines and 14.0 kB; three of its lines exceed 430
  characters. By bytes the module is 62% of its peer, not 34%.
- The peer is larger for a scope reason the packs state. MOD-0192's 43 paths include an evaluation executor and a
  lease store — `CapacityLeaseStore.cs` (150 lines) and `CapacityEvaluationExecutor.cs` (44 lines) alone are 194
  lines, more than half of all of S&OP. MOD-0190 has no background work by design: "No Workflow HTTP or Event Bus
  delivery; lifecycle events remain atomic Pending outbox records" (pack `:42`), "no publisher/worker/delivery
  claim" (pack `:122`).
- MOD-0190 also has no outbound reference client: DEMAND is "FROZEN / TEST FIXTURE ONLY" (pack `:119`). Its whole
  dependency seam is an 8-line in-memory matcher.

So "308 vs 904" compares two differently shaped scopes, measured in a unit that penalises neither.

## 3. The 38 paths, walked

All 38 are **present**; all 38 are **byte-equal** to the accepted 379-entry source manifest
(`docs/records/audits/2026-09/mvp6-mod0190-test-oracle-rework-01/source-manifest.tsv`, sha256 `ae7ef59e…`, the hash
the pack binds at `:260`) and to BASE-STACK v2.

| Layer (paths) | Lines | Bytes | What is there | Assessment |
|---|---:|---:|---|---|
| Api (3) | 49 | 6,920 | Controller with six actions; the request gate; the error envelope with 13 codes | Substantive. Complete for six operations. |
| Application / Commands (3), Queries (3) | 18 | 1,696 | Six request records | Minimal and complete. |
| Application / Handlers (6) | 27 | 4,052 | Validate, then delegate to the repository | Thin delegates. No rule lives here. |
| Application / Validators (3) | 12 | 1,509 | Tenant, LE, actor and key not empty | Thin. Body rules are elsewhere. |
| Application (3) | 51 | 5,669 | `SandopWire.Validate` (all body rules), fingerprint, projection | Substantive, except three uncalled projection methods. |
| Domain (5) | 24 | 3,133 | Scope/result/action, two interfaces, three entity classes | Two files substantive. **Three entity files are shells.** |
| Persistence (3) | 112 | 16,054 | Repository (all business logic), six indexes, one DI line | The core. 40% of the module's bytes. |
| Infrastructure (3) | 15 | 1,524 | Four permission constants, a marker attribute, the fixture reader | Minimal. The attribute has no behaviour. |
| Tests (6) | — | — | 19 cases | See §4. |

Functional completeness against the pack's §3 owned objects (`:49-58`):

| Pack requires | In the code |
|---|---|
| Six operations | Six actions — `SandopPlansController.cs:9-19` |
| Three commands, three queries | Present, one file each |
| Three lifecycle events | `sandop.plan.created.v1`, `sandop.snapshot.captured.v1`, `sandop.sign-off.recorded.v1` — `SandopRepository.cs:53`, `:66`, `:75` |
| Four permission keys | `SandopPermissions.cs:3` |
| Plan, Snapshot, SignOff persistence + receipt, audit, outbox | Six `sandop_*` collections — `SandopRepository.cs:9-14` |
| Three unique invariants | Three unique indexes — `SandopSchema.cs:7-9` |
| Every §13 failure code | Every code §13 names is produced in `SandopRepository.cs` or the gate; see `ACCEPTANCE-MATRIX.tsv` rows FP-13-* |

Nothing the bounded scope lists is missing from the code.

## 4. Where it is thin beyond the bounded scope

These are measured facts. Each cites the pack line it sits against.

| # | Fact | Evidence | Pack line it meets |
|---|---|---|---|
| T1 | The three entity classes have no run-time caller. They are referenced only by three `SandopProjection` methods that nothing calls. The repository reads and writes `BsonDocument`. | `SandopPlan.cs:2`, `SandopSnapshot.cs:4`, `SandopSignOff.cs:2`; `SandopProjection.cs:6-8`; `SandopRepository.cs:9-14` | `:39` owns `S&OPPlan`, `S&OPSnapshot`, `SignOff` — owned as data, yes; as types, unused |
| T2 | The entities do not derive from `EntityBase`. | `SandopPlan.cs:2` | frontmatter `:8` `entity_base: EntityBase`. The pack already records this as F190-ENT (`:468`) |
| T3 | The controller derives from `ControllerBase`, not the service's base controller. | `SandopPlansController.cs:5` | `:143-144` "Runtime work must use the service's four pipeline behaviors, base controller …" |
| T4 | The FluentValidation validators check context fields only. Body validation is a static function called twice (handler, then repository). | `CreateSandopPlanValidator.cs:4`; `SandopPlanModels.cs:8`; `SandopRepository.cs:32` | `:141-143` validators as their own files — satisfied in form |
| T5 | `SandopPermissionAttribute` is a marker, not a filter. Permission is enforced by the in-action gate. | `SandopPermissionAttribute.cs:3`; `SandopContextMiddleware.cs:14` | `:177` "default-deny JWT and permission checks" — done, by another mechanism |
| T6 | `AddSandopPersistence()` registers only the repository. Index creation and the fixture reader are not registered, and `SandopSchema.EnsureAsync` has no caller except a test. | `SandopPersistenceRegistration.cs:5`; `SandopContractTests.cs:6` | `:99` shared composition is integration-owned — so this is a seam, but it is a seam the module leaves entirely open |
| T7 | Tests: 19 repository-level cases. No handler test, no API test, no response/event shape test, no mock smoke. | `ACCEPTANCE-MATRIX.tsv` TE-17-3, TE-17-4, TE-17-6 | `:209`, `:210`, `:212` |

T1–T5 are byte-equal to what CT accepted in September. They are not regressions and not new. T6 matters for
integration (see `REACHABILITY.md`). T7 is the reason 20 acceptance rows are INSUFFICIENT EVIDENCE.

## 5. What the test's own workaround says

`SandopAtomicityTests.cs:60-62`, inside the unknown-commit test:

```text
// A failed commit can leave an inactive server transaction. Resolve the isolated test session before retrying.
await db.Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("killAllSessions",new BsonArray()));
var recoveredDb=FreshDatabase();
```

`FreshDatabase()` (`:14-16`) builds a new client with `DirectConnection=true`, no replica-set name and a new
application name.

What this implies, read together with Q214:

- The author met the state Q214 measured: after the injected error 91 the test's normal client cannot use the
  server, and the failed transaction stays open.
- The response was to go around it, not to change the injected fault: kill every session on the server, then talk to
  the server through a connection that skips topology discovery.
- So the "exact key recovers" half of the test proves that **a different client** can create the plan afterwards. It
  does not prove that the repository instance that received the unknown result recovers.
- `killAllSessions` with an empty list ends all sessions on that mongod, not only this test's. That is safe only
  because the assembly runs serially.
- The module's confidence in its own fail-point pattern is therefore low by its own evidence: the one test that
  needs the pattern to work carries a two-step bypass, asserts only "fired at least once" (`:59`), and its sibling
  accepts either outcome (`:77`).

## 6. Bottom line for CT

- Scope shortfall: **none**. 38 of 38, bytes as accepted.
- Design thinness (T1–T5): present, previously accepted, and now recorded in one place.
- Evidence thinness (T7, §5): real. It is the main reason this VER cannot say READY.
