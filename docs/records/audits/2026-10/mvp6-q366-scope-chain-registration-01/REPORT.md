# Q366 — restore the legal-entity scope chain, and guard the composition root

- Lane: Q366, integration-agent. Recorded 2026-10-03.
- Preflight: `Sat Oct  3 18:08:16 UTC 2026` · `feature/mvp6-logistics` · HEAD `bf1ef28f9` · dirty 7 · staged 0. App ports
  were free.
- Step 0: `AGENTS.md`, `git-safety.md`, `code-style.md` and `integration-agent.md` are unchanged since this session's
  earlier reads (sha256/16 `ce8c12ad…`, `4181c697…`, `6032fbb0…`, `f760471e…`). The Q362 REPORT is this lane's own
  (`9f5996ca…`).
- **Nothing staged or committed. No secret or token is printed in this record.** The decoded claim set below is
  printed; the token itself is not.
- **Agent verdict ≠ CT ACCEPTED.**
- **State: A, B, C, D and the suites DONE.** The fix introduces **0** new test failures in any of the four suites
  (see "Suites").

## A — the 14 lines restored (additions only)

Each composition file is diffed against its pre-edit copy. **0 lines were removed in each.**

| file | code lines added | with comments | diff |
|---|---:|---:|---|
| `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs` | 4 | 12 | `evidence/diffs/services_Diten.Platform_…DependencyInjection.cs.diff` |
| `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/DependencyInjection.cs` | 6 | 11 | `…AuthService…DependencyInjection.cs.diff` |
| `services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs` | 4 (+1, see below) | 10 (+3) | `…MdmService.Api_Program.cs.diff` |

- The 14 code lines are byte-identical to the Q185 lane tree (`~/mvp6-env/q185/treeB`). Re-diffed against HEAD at the
  start of this lane: still +4, +6, +4, −0.
- Placement: Platform's two registrations sit one line below where Q185 put them, after `ICurrentUserContext`; the
  order has no effect on DI.
- Each block carries a Q271/Q272-style comment: what was unreachable, why it failed silently, what it makes reachable,
  and a citation of Q362 and Q363.
- Note on the dispatch: MDM's composition root is `Program.cs`, not a `DependencyInjection.cs`.
- **The one MDM line that is not a restoration.** `public partial class Program;` is appended to MDM `Program.cs`
  so that `WebApplicationFactory<Program>` can see MDM's Program (it was `internal`). It changes no behaviour, and
  Platform, Auth and SupplyChain already declare the same line.

## B — the service identity is configuration

Paired sections; both must carry the same `KeyId` and `Secret`:

| service | section → env-var form | read by |
|---|---|---|
| Platform | `MdmServiceIdentity__Enabled`, `__KeyId`, `__Secret` (≥32 bytes); `__Issuer`, `__Audience`, `__CallerId`, `__TokenLifetimeSeconds` have defaults | `MdmServiceIdentityTokenProvider` (`Create` returns null, so the call fails closed, when disabled, the key id is blank or the secret is shorter than 32 bytes) |
| MDM | `PlatformServiceIdentity__Enabled`, `__KeyId`, `__Secret`; issuer, audience, caller id, lifetime and skew have defaults | `PlatformServiceTokenValidator` |

**Who supplies them.** In every non-Development environment, the environment's secret store or deployment
configuration supplies them, injected as environment variables. **They never go in a tracked file.**

Files changed:

- Tracked `appsettings.Development.example.json` for Platform and for MDM: the section, with `Enabled: true`,
  `KeyId: "local-dev"`, `Secret: ""` (5 lines each, additions only).
- Untracked, gitignored `appsettings.Development.json` for Platform and for MDM: the same section, carrying **one
  generated throwaway value shared by both**, len 64, sha256/8 `53c43bf9`. The value is not printed; the redacted diffs
  are in `evidence/diffs/UNTRACKED_*`.

**Proven by D.** The live stack ran with **no** environment override for the service identity, and Platform → MDM
validation worked from these files alone.

## C — the guard (Q364)

The guard is `CompositionRootGuardTests` in four places:

| service | where | new project? |
|---|---|---|
| Platform | `tests/Diten.Platform.CompositionGuard.Tests/` (csproj + test), added to `Diten.Platform.sln` | **yes** |
| Auth | `tests/Diten.AuthService.Application.Tests/CompositionRootGuardTests.cs` | no |
| MDM | `tests/Diten.MdmService.Application.Tests/CompositionRootGuardTests.cs`; `Microsoft.AspNetCore.Mvc.Testing` 8.0.8 added to the csproj (same version as Platform and Auth) | no |
| SupplyChain | `tests/Diten.SupplyChainService.Tests/CompositionRootGuardTests.cs` | no |

**What it does.** It builds the **real** host: `WebApplicationFactory<Program>`, Development,
`ValidateOnBuild`/`ValidateScopes` on, hosted services removed, no request sent. Then it fails on any of:

1. **Construction.** `builder.Build()` fails. This is what catches a missing dependency of a registered handler
   (sabotage S-sc).
2. **Controllers.** A required constructor parameter of any controller is not a service. Controllers are not
   registered services, so `ValidateOnBuild` never checks them.
3. **Optional dependencies.** An **optional (`= null`) `Diten.*`** constructor parameter is not a service, on any of:
   - a registered implementation type;
   - a controller;
   - **any concrete `Diten.*` class that implements a registered `Diten.*` interface.** This covers typed `HttpClient`
     and factory registrations, which carry no `ImplementationType` (first version missed this; see S-p2).

Each service reads its own disposable mongod from one variable: `PLATFORM_`, `AUTH_`, `MDM_` or
`SUPPLYCHAIN_COMPOSITION_GUARD_MONGO_URI`. When the variable is unset the guard **fails** rather than skips.

**Why Platform needs a new project.** `Diten.Platform.Application.Tests` registers the BSON serializers from a
`[ModuleInitializer]` (`PlatformTestSerializers.cs`), and Platform's `AddInfrastructure` registers them again with
`RegisterSerializer`, which throws. So the real Platform host can never be built inside that assembly. That is likely
why Platform's DI tests build the container by hand (`DependencyInjectionSmokeTests.cs`): the defect shape Q364 names.

**One stray file, for the owner to delete.** The first draft of the guard was written into
`Diten.Platform.Application.Tests/CompositionRootGuardTests.cs`. Under the no-rm/no-mv rule it was overwritten with a
3-line comment stub rather than deleted.

**The SupplyChain guard found a real gap and pins it.** `CapacityPlansController` cannot be constructed:
`CapacityRequestContext` is unregistered because Capacity is deliberately not composed (Q273). `MapControllers()` still
maps it, so its routes exist and cannot be served. It is recorded as a named `KnownUncomposed` entry. A second
assertion fails if the entry stops being a gap, so the entry has to be removed when Q273 composes Capacity. Not fixed
here.

### Sabotage (§24.2)

Each variant ran in its own `cp` of the services, never in the repository. Results are in `evidence/sabotage/`.

| run | removed | result | guard message (abridged) |
|---|---|---|---|
| fixed tree ×4 | — | Platform 2/2, Auth 2/2, MDM 2/2, SupplyChain 2/2 **green** | — |
| S-p1 | Platform `AddScoped<IInternalScopeResolutionContext,…>` | **2 red** | controller `InternalTenantLegalEntityScopeController(IInternalScopeResolutionContext)`; optional `MdmLegalEntityReferenceValidator(IInternalScopeResolutionContext? = null)` |
| S-p2 | Platform `AddSingleton<IMdmServiceIdentityTokenProvider,…>` | **1 red** (first guard version: **green** — fixed, see C.3) | optional `MdmLegalEntityReferenceValidator(IMdmServiceIdentityTokenProvider? = null)` |
| S-p3 | Platform DI file = HEAD's pre-fix file | **2 red** | both of the above |
| S-a | Auth `AddHttpClient<ITenantLegalEntityScopeClient,…>` | **1 red** | optional on `LoginCommandHandler`, `RefreshTokenCommandHandler`, `ForcedChangeTenantPasswordCommandHandler`, `VerifyMfaCommandHandler` |
| S-m | MDM `AddSingleton<IPlatformServiceTokenValidator,…>` | **1 red** | controller `InternalLegalEntityReferenceController(IPlatformServiceTokenValidator)` |
| S-sc | SupplyChain `AddHttpClient<IClaimReferenceReader,…>` (the Q271/Q272 shape) | **2 red** | host refused at build: "Unable to resolve service for type 'IClaimReferenceReader' while attempting to activate …CreateClaimHandler" |

## D — the golden flow on the real tree (R-09 measured)

**Stack.** A copy of the repository as edited (8,025 files, 0 mismatches), all six services built in that copy:

| service | port |
|---|---|
| Auth | 5056 |
| Platform | 5057 |
| MDM | 5059 |
| SupplyChain | 5061 |
| Gateway | 5000 |
| Web | 5001 |

Own mongod on 57367. The service identity came from the Development files only.

**Users and org.** Built the Q362 way: Auth's invite endpoint, the forced password change through the gateway, and an
org fixture through the gateway (legal entity created and activated in MDM, OU, positions, assignments).
`evidence/fixture.py`, `evidence/live/fixture.json`: result **PASS**.

**Decoded claim set** of the tenant Admin (`evidence/live/decoded-claims-admin.txt`; the token was not printed):

```
"actor_type": "tenant_user",
"tenant_id": "00000000-0000-0000-0000-000000000001",
"legal_entity_id": "477b0f55-1d38-4459-a280-96e645332355",
"…/claims/role": "Admin",
permission claims: 81 | supplychain.shipments.*: cancel, create, dispatch, pod.capture, read
```

**Browser, as that user, with a reload after every step:**

| step | request → status | after reload |
|---|---|---|
| list | GET 200 in 561 ms (Q362 as-is: **403** from the Web adapter); denied card hidden | empty table |
| create | POST 201 `Draft` → redirect to Details | Draft |
| Planned | POST …/transition 200 | Planned |
| Dispatched | POST …/transition 200 | Dispatched, Capture POD offered |
| POD | POST …/pod 201 | Delivered, POD card filled |
| Closed | POST …/transition 200 | Closed, 0 actions |

**The database agrees** (`evidence/live/R09-db-check.txt`): `Status: 'Closed'`, `Version: 5`,
`LegalEntityId: 477b0f55…`, POD stored, `sce_shipment_audit` 5, `sce_shipment_outbox` 6.

**Gateway.** It logged **16** `shipment-bundle` requests (Q362 as-is: **0**). All are 200/201
(`evidence/live/gateway-shipment-bundle-lines.txt`).

**Shutdown.** Signed out; every process stopped by recorded pid; ports 5000, 5001, 5056, 5057, 5059, 5061, 5199 and
57367 free.

## Suites — DONE: the fix introduces 0 new failures

**How the suites were run.** Every suite ran twice: on the **fixed** tree (the repository as edited) and on a **pre-fix**
tree. The pre-fix tree is the same copy with the three composition files restored from their pre-edit backups. MDM keeps
only `public partial class Program;`, so that its guard compiles.

Both trees carry `AGENTS.md`, `.antigravity`, `execution`, `scripts`, `docs` and `frontend`, because Platform's tests
locate the repository root by `AGENTS.md`. Each module had its own mongod:

| port | used by |
|---|---|
| 57371 | Platform tests (`DITEN_PLATFORM_TEST_MONGO_URI`) |
| 57366 | Platform guard |
| 57369 | Auth guard |
| 57368 | MDM tests and MDM guard |
| 57370 | SupplyChain (Q335 recipe and guard) |

Platform and Auth spawn their own mongod for their isolated fixtures (`DITEN_TEST_MONGOD`, `DITEN_ITEST_MONGOD_BIN_DIR`).
Scripts: `evidence/run-suites.sh`, `run-suites-noSC.sh`, `run-sc-modules.sh`. Results: `evidence/suites/`.

| suite | fixed tree | pre-fix tree | failing only in fixed | failing only in pre-fix |
|---|---|---|---:|---|
| Platform `CompositionGuard.Tests` | **2 / 0 / 2** | 0 / 2 / 2 | 0 | both guard tests |
| Platform `Application.Tests` | 4344 / 64 / 4408 | 4344 / 64 / 4408 | 0 | — (the same 64) |
| Auth `Application.Tests` (incl. guard) | 789 / 3 / 792 | 788 / 4 / 792 | 0 | guard `EveryOptionalDitenDependency…` |
| MDM `Application.Tests` (incl. guard) | 492 / 6 / 498 | 491 / 7 / 498 | 0 | guard `EveryControllerConstructorParameter…` |
| SupplyChain (Q335 recipe) | **432 / 1 / 433** | not run (composition unchanged by this lane) | — | — |
| SupplyChain guard | 2 / 0 / 2 | — | — | — |

Counts are passed / failed / total, by name-by-name comparison of the failing-test lists. SupplyChain per module:
Shipments 90/0, Carriers 36/0, Loads 33/0, Returns 78/0, Claims 128/1 (`ClaimReplayTests.Receipt_TwoIndependentTest
Processes_DurableRecovery`, restart-mode by design, the same as Q335), SandopPlans 19/0, CapacityPlans 48/0. That is
**identical to the Q335 baseline**. 435 tests are listed because of the 2 new guard tests. The flaky
`ShipmentTelemetryTests.cs:71` passed on this run.

**Pre-existing failures, present on both trees and unrelated to this lane** (recorded, not investigated):

- **Platform, 64.** 49 are `FormatException: ObjectSerializer does not support BSON type 'Timestamp'`, in
  BusinessReferenceData Mongo tests; the rest are assertion failures in DocumentManagement lifecycle and training
  matrix tests.
- **Auth, 3.**
  - `PermissionScopePreservationTests.Baseline_covers_every_seeded_permission_and_nothing_else`: the seed has
    `crm.knowledge.*` keys that `permission-scope-baseline.csv` lacks.
  - Two `UserLookupValidationContractTests` contract assertions.
- **MDM, 6.** Three `ProductItemSkuMasterManifestProviderTests`, and three `LskuRegisterMongoTests` with the same BSON
  `Timestamp` error.

**What the pre-fix guard failures add.** They were measured on the real pre-fix HEAD files, not on sabotage copies, and
they name exactly the gaps Q362 found.

**First attempt.** An earlier attempt, on a services-only copy without `DITEN_PLATFORM_TEST_MONGO_URI`, gave Platform
4158/250/4408. That number was environmental and is superseded. The lane stopped that attempt when free disk fell to
~1 GiB, and, with the owner's approval in this session, deleted only its own six sabotage copies (3.0 GB, results
already recorded). Every lane mongod is stopped; ports 57366 and 57368–57371 are free.

## SOP §18.0 — what this moves

| row | after Q366 |
|---|---|
| 1 Golden flow | the UI half is **measured on the real tree** (R-09), with the five elements as written in Q362 and re-observed here. The rest of the row stays CT's ruling |
| 2 No-shell | **the Q362 blocker is removed in code**: a tenant Admin reaches every Shipment screen through the gateway. It still depends on environment configuration (Q358 part A keys plus the service identity above) and on the uncommitted AdminModules widening (Q357) |
| 8 RBAC / Tenant | unchanged (Q362 R-03 live) |
| — suites | 0 new failures from this lane; SupplyChain 432/1/433 = Q335 |
| 12 UX states | **not moved.** The Q362 defects (502 shown as validation, Details loading) belong to Q365 |
| 13 Observability | **not moved.** Web still logs no correlation |
| L10n | **not moved** |

## Findings

- **F-Q366-1.** The composition-root guard now exists in four services and is proven by sabotage. On pre-fix HEAD it
  names exactly the three registrations Q362 found missing.
- **F-Q366-2.** SupplyChain's `CapacityPlansController` is mapped but cannot be constructed (Q273). Pinned, not fixed.
- **F-Q366-3.** Platform's test assembly cannot host the real Platform: its `[ModuleInitializer]` and production's
  `RegisterSerializer` conflict. Any future Platform host test must live in a separate assembly.
- **F-Q366-4.** The first guard design missed typed-`HttpClient` registrations (S-p2 green). It is fixed in all four
  copies, and the sabotage proves it.
- **F-Q366-5.** During this lane, free disk fell to ~1 GiB. `~/mvp6-env` holds 74 GB across all lanes. The lane deleted its own sabotage copies with the owner's approval; the rest is the owner's call.
- **F-Q366-6.** A stray comment-only stub sits at `Diten.Platform.Application.Tests/CompositionRootGuardTests.cs`, left
  for the owner under the no-rm rule.

## Hygiene — a leak this lane caught in its own record

The first versions of the two `evidence/diffs/UNTRACKED_*` files redacted the new `Secret` line only. Their unified
**context** lines still carried other secret-bearing values from those Development files. The pre-seal scan found
them. Both files were overwritten with every Secret/Key/Password/Connection/Token value replaced by
`<REDACTED len N>`. They were never committed, pushed or shared.

The final scan covered the JWT secret, the three test passwords, the service-identity secret, Auth's internal key and
every secret-bearing value of both Development files. Its only remaining match is `KeyId: "local-dev"`, which is not
a secret and appears in the tracked example on purpose.

**Recommendation: rotate nothing.** The values never left the local disk. The owner may still prefer to rotate the
Development internal key; that is the owner's call.

## Files changed (sha256)

See `CHANGED-FILES.txt` in this folder: sha256, tracked or untracked, and path for all 15 files.

Nothing committed, nothing pushed, nothing staged.
