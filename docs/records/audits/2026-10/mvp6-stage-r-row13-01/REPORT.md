# Stage R row 13 — two of the four observability cells close on re-measurement, not on new code

**Lane:** gate 18.0 row 13 (Observability) for MOD-0184 and MOD-0185 · **Date:** 2026-10-12
**Branch:** `feature/sce/mod-0190-0192-bounded-slice` · **Lane mongod:** slot 4, port 34994, rs `rsq202s4`

The development plan called row 13 a "re-measure … now that the code carries them". That is exactly what it
was: **no source file was changed in this lane.** The Q446 notes pointed at the context middlewares, but the
replay signal is logged by the **controllers**, which already carry it.

| note in `GATE-18-0.tsv` | where the signal actually is |
|---|---|
| 0185: "the replay required by pack :432 is not logged (`LoadContextMiddleware.cs:50,86`)" | `LoadsController.cs:21` — `"Load outcome {Operation} {Status} {ErrorCode} {IdempotentReplay}"` |
| 0184: "'record list size and timings' (pack :302) and a conflict/transaction-failure signal are not logged" | `CarriersController.cs:27` — `{IdempotentReplay} {ListSize} {ElapsedMs}` |

## MOD-0185 Loads — row 13 MET

`LoadReplayTests` + `LoadContractTests`, 8/8 passed, 43 `Load outcome` lines.

| pack :432 signal | measured |
|---|---|
| operation | `createLoadPlan`, `queryLoads`, `transitionLoad` |
| result | 201 / 200 / 422 `DUPLICATE_SHIPMENT` |
| correlation | **43 of 43** lines carry a `CorrelationId=`, and **zero** carry the nil guid |
| replay | `True` on 4 lines (2 create, 2 transition), `False` on the rest — the field discriminates rather than being constant |

Sample, verbatim:

```
[21:19:35 INF] Load outcome createLoadPlan 201 null False CorrelationId=07ff83d1-…
[21:19:35 INF] Load outcome queryLoads 200 null False CorrelationId=07ff83d1-…
[21:19:35 INF] Load outcome createLoadPlan 422 DUPLICATE_SHIPMENT False CorrelationId=f06d3265-…
```

A constant `replay` field would have proved nothing, which is why the True/False split is reported rather
than the presence of the word.

## MOD-0184 Carriers — row 13 MET

`CarrierContractTests`, 33/33 passed, 131 `Carrier outcome` lines over three operations
(`createCarrier` 74, `changeCarrierStatus` 49, `queryCarriers` 8).

Both things the note called absent are present:

- **list size and timings (pack :302).** `queryCarriers` logs `ListSize` `1` and `ElapsedMs` `4.6` / `1.8`;
  writes log `ListSize` `null` with `ElapsedMs` `109.6` / `29.6`. So the field is populated where a list
  exists and null where none does.
- **a conflict / transaction-failure signal.** Nine distinct outcomes were logged, among them
  `409 CARRIER_CODE_CONFLICT` ×8 and `409 IDEMPOTENCY_KEY_REUSED` ×10 (conflict), and
  `503 PERSISTENCE_UNAVAILABLE` ×1 with `500 INTERNAL_ERROR` ×9 (transaction failure).

Replay is `True` on 20 lines. Tenant, legal entity and correlation are on every line and none is nil.

Measuring MOD-0184 is permitted: `:376` withholds runtime **write**, not observation.

## The negative half of pack :432, which is the half a tick usually skips

> "Logs carry operation/result/correlation/replay, **never token/connection string/raw evidence or
> confidential payload**."

Counted over both logs — values deliberately not reproduced here, only counts:

| pattern | 0184 log | 0185 log |
|---|---|---|
| `Bearer <10+ chars>` | 0 | 0 |
| `eyJ` (JWT header) | 0 | 0 |
| `mongodb://` | 0 | 0 |
| `replicaSet=` | 0 | 0 |

## One finding this lane could not explain, and did not guess at

**Every one of the 43 `Load outcome` lines carries an empty `TenantId=` and `LegalEntityId=`, while every
`Carrier outcome` line carries both filled.** The two middlewares write the same two lines —
`LoadContextMiddleware.cs:91-92` and `CarrierContextMiddleware.cs:89-90`, both
`loggingContext.Scope = new(tenant, le, actor)` followed by `loggingContext.CorrelationId = …` — and the
correlation half demonstrably took effect on the Loads lines, since only the middleware sets it. So the
scope half not appearing is not explained by the middleware source, the request path
(`IsLoadPath` matches `/api/shipment-bundle/loads`, which the tests use) or the test factory
(`LoadTestFactory` replaces only `ILoadCommitProbe`).

This does **not** block the tick: pack :432 names operation, result, correlation and replay, and tenant is
not among them — its absence is on the safe side of the same sentence's prohibition. But a Loads log line
cannot today be attributed to a tenant while a Carriers line can, and sibling modules differing like this is
the kind of thing K6 says will drift. Raised as **Q486** rather than tidied away or asserted with a cause
this lane did not measure.

## Still open on row 13

MOD-0186 and MOD-0187 cannot be measured from the test suite: their replay tests
(`ReturnReplayTests`, `ClaimReplayTests`) drive the **repository** directly, and the project has no HTTP test
host for Returns or Claims — `WebApplicationFactory` appears only for Loads, Carriers, Shipments and the
Sandop/Capacity host. Their row 13 needs the live stack, which is the rest of Stage R.

`ClaimContextMiddleware` logging, whose absence the 0187 note records, was added on this branch in
`7d75431fa`, so that note is already stale; the cell still needs a runtime session to tick.
