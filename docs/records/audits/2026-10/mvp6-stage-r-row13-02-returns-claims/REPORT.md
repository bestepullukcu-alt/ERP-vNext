# Stage R row 13 — Returns and Claims get an HTTP seam, and the log template turns out to promise two fields nothing fills

**Lane:** gate 18.0 row 13 (Observability) for MOD-0186 and MOD-0187 · **Date:** 2026-10-12
**Branch:** `feature/sce/mod-0190-0192-bounded-slice` · **Lane mongod:** slot 4, port 34994, rs `rsq202s4`

## Why these two cells needed new test infrastructure

Row 13 asks that service logs carry operation, result, correlation and replay. Those four live on the
controllers' outcome lines — `ReturnsController.cs:26`, `ClaimsController.cs:28` — which only a request
reaches. Both modules' suites drive repositories: `ReturnReplayTests` and `ClaimReplayTests` never send one,
and before this lane the project had no HTTP seam for either (`WebApplicationFactory` appeared only for
Loads, Carriers, Shipments and the Sandop/Capacity host). So the cells were measurable only in a live stack
session, by hand, once.

`Common/ReturnsClaimsHttpHost.cs` makes them measurable repeatably. It needs none of the composition tricks
`SandopCapacityHttpHost` needs: `Program.cs:76-79` already registers `AddReturnPersistence` and
`AddClaimPersistence` with both controllers, so **this host changes nothing about what production composes.**
Two seams are replaced, both because they reach outside the process — the two reference readers, registered
in production as typed `HttpClient`s, and MDM legal-entity validation through the existing
`StubLegalEntityValidation()`.

Three things had to be measured rather than assumed while building it, each found by a red run:

1. **The stubs cannot return a constant root.** A create's correlation id *is* the correlation root the
   repository checks the snapshot against (`CreateReturnHandler` passes `context.CorrelationId`), so a fixed
   Guid answers `CORRELATION_ROOT_MISMATCH` on every create. Each stub reads the request's own context and
   echoes its correlation id back, which is what a real reader observing that shipment would return.
2. **Returns gates every transition on a permission of its own.** A token holding
   `supplychain.returns.transition` answered **403** on a transition to `Authorized`;
   `ReturnPermissions.ForTarget` maps it to `supplychain.returns.authorize`. The host now enumerates the
   per-target keys from `ForTarget` instead of assuming the generic one.
3. **All requests on one return share a correlation root.** A transition under a fresh correlation id
   answered **409 `CORRELATION_ROOT_MISMATCH`**; under the create's id it answers 200. That is a contract
   fact, and the test says so where it would otherwise look like tidiness.

## What was measured — the four signals, both modules

`ReturnsClaimsObservabilityTests`, 2/2 passed, in `evidence/returns-claims-http.log`.

| | MOD-0186 Returns | MOD-0187 Claims |
|---|---|---|
| operation | `createReturn`, `transitionReturn`, `queryReturns` | `createClaim`, `queryClaims` |
| result | 201, 200 | 201, 200 |
| correlation | **4 of 4** lines, none nil | **3 of 3** lines, none nil |
| replay | `True` on the second create, `False` on the rest | `True` on the second create, `False` on the rest |

The replay is a real replay, not a flag: the second create returns the **same** `returnId` / `claimId` as the
first under the same idempotency key, which the test asserts. And the shared root is visible in the log — the
create, its replay and the transition all carry `CorrelationId=5bf4263a-…` while the query carries its own.

The 0187 note in `GATE-18-0.tsv` reads "`ClaimContextMiddleware` logs nothing, so result and replay are
absent from service logs". That was already stale: the logging was added on this branch in `7d75431fa`, and
the outcome line was always the controller's.

## The finding, and it corrects CT's own Q486

Q486 was raised earlier today after noticing that all 43 MOD-0185 outcome lines carried an empty `TenantId=`
while MOD-0184's carried it filled, and it said the cause was "not explained by the middleware source". With
two more modules measured, the real cause is in hand and **Q486's framing was wrong — it blamed the wrong
layer.**

Returns and Claims render `TenantId=` and `LegalEntityId=` empty too. So three of four modules do, and
Carriers is the exception rather than Loads being the oddity. The cause is not in any middleware:

- `Program.cs:42-44` registers exactly **one** enricher, `CorrelationIdEnricher`, and it sets exactly one
  property: `CorrelationId`. `Enrich.FromLogContext()` is present, but `LogContext.Push` appears **nowhere**
  in `services/Diten.SupplyChainService/src` — measured.
- The console output template at `Program.cs:44` nonetheless renders
  `CorrelationId={CorrelationId} TenantId={TenantId} LegalEntityId={LegalEntityId}`.
- So `TenantId` and `LegalEntityId` are **unbound placeholders on every line the service writes**. They
  render filled only when a message's own template happens to declare properties of those names — which is
  `CarriersController.cs:27` and nothing else.

The four `loggingContext.Scope = new(tenant, le, actor)` assignments in the middlewares are therefore not the
problem and are not dead either: they populate `RequestContext` for the code that reads it. What is missing
is anything that turns that scope into log properties. The comment above the template reads *"Render only the
three trace properties"*, so three were intended and two were never wired.

**This lane did not fix it.** The fix lands in `Program.cs` — the integration-owner-reserved, hash-pinned
file over which Q484 is already waiting for the owner — so it is recorded and left. Row 13 is unaffected
either way: pack :432 names operation, result, correlation and replay, and tenant is not among them.

## Cell movement

`GATE-18-0.tsv` is inside its own lane's `ARTIFACTS.sha256` and was **not edited**; the transitions are in
`MOVED.tsv` here, which supersedes those cells.

| module | row | from | to |
|---|---|---|---|
| MOD-0186 Returns | 13 Observability | NOT MET | MET |
| MOD-0187 Claims | 13 Observability | NOT MET | MET |

With the two cells closed earlier today for MOD-0184 and MOD-0185, **row 13 is now MET for all four
modules**, and MOD-0184's `:376` runtime-write reservation never blocked it — observation was always
permitted.

## Regression, and an environment failure measured rather than blamed

`Tests.Returns` + `Tests.Claims` + `Tests.Common`: **252 of 253 passed.** The single failure is
`ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`, which fails in 2 ms with its own
message — *"Explicit write/read restart mode required; exclude from ordinary suite."* It declares itself out
of scope for an ordinary run and the filter used here swept it in; it is not a regression.

The first attempt at this run reported **59 failures**, and they were not a regression either. Every one of
them took exactly 15 s, the lane's `serverSelectionTimeoutMS`, and the set included suites this lane never
touched. The cause is in the lane mongod's own log, which is why it is stated rather than guessed:

```
22:30:43  "s":"F"  c":"FTDC"  id:9399800
  Exception thrown in full-time diagnostic data capture subsystem.
  Terminating the process because diagnostics cannot be captured.
```

The volume had filled — `ENOSPC` reached this session's own tool output at the same moment — and mongod
terminated itself because FTDC could not write. So the 59 reds were one dead server, not fifty-nine defects.
The lane was restarted on a fresh timestamped data directory (the old one kept, not deleted) and the run
came back 252/253.

Free space on this volume moved between 911 MiB and 6.4 GiB across the day and then hit zero, which is worth
an owner's attention independently of this lane: the Returns suite needs roughly 500 MB for `createIndexes`
alone, and a lane mongod that dies this way takes a whole suite red with it and says nothing a test can see.
