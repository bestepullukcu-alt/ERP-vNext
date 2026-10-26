<!-- verify from: this folder -->
# Q446 row 13 re-checked: stale for three of four modules, and Claims' real half is now closed

    lane      : mvp6-q446-row13-recheck-01
    measured  : 2026-10-12
    basis     : docs/records/audits/2026-10/mvp6-q446-gate-18-0-four-modules-01 (GATE-18-0.tsv, evidence/lane-log-scan.txt)
    verdict   : Q446's row 13 is CORRECT AS MEASURED and STALE AS A VERDICT for MOD-0184/0185/0186.
                MOD-0187's missing half was real and is closed by this lane.

## What Q446 recorded, and why it was right at the time

Row 13 (Observability) is NOT MET for all four modules. Its note for three of them says the replay
signal is absent:

| module | Q446 note |
|---|---|
| MOD-0185 Loads | "the replay required by pack :432 is not logged (LoadContextMiddleware.cs:50,86)" |
| MOD-0186 Returns | "replay not logged (ReturnContextMiddleware.cs:57,110)" |
| MOD-0187 Claims | "ClaimContextMiddleware logs nothing, so result and replay are absent" |

The runtime half of that evidence is unambiguous — `evidence/lane-log-scan.txt` counted **outcome lines
with replay field = 0** for Returns (R-2), Loads (R-4b) and Claims (R-4c), against 26 for Carriers (R-4a).

## Why the verdict is stale: the code is newer than the lanes

Every one of the four modules logs replay on its controller's outcome line in today's source:

```
Carriers  CarriersController.cs:27   {Operation} {TenantId} {LegalEntityId} {CorrelationId} {Status} {ErrorCode} {IdempotentReplay}
Loads     LoadsController.cs:21      {Operation} {Status} {ErrorCode} {IdempotentReplay}
Returns   ReturnsController.cs:26    {Operation} {Status} {ErrorCode} {IdempotentReplay}
Claims    ClaimsController.cs:28     {Operation} {Status} {ErrorCode} {IdempotentReplay}
```

Q446 found Carriers' line and read only the middleware for the other three. The dates explain the zero
counts, and they are not a defect:

```
2026-10-03  a3c335256   Carriers' outcome log added
2026-10-04  R-2 runs                                  <- Returns measured here
2026-10-05  eb1306590   Loads, Returns and Claims outcome logs added
2026-10-05  R-4a, R-4b, R-4c run
```

R-2 ran a day before the line existed, so Returns' zero is the lane reading code that had not been
written yet. Carriers' 26 is consistent for the same reason in reverse. Loads and Claims ran the same
day as `eb1306590`; whether their runs preceded or followed the commit is not established here, and the
counts are consistent with preceding it.

**Correlation is not a gap either.** `CorrelationIdEnricher` is wired at `Program.cs:42` through Serilog,
so every log entry carries the correlation as an enriched property, not a message placeholder. Returns'
own comment at `ReturnsController.cs:23` says exactly that.

## What was genuinely missing, and is now closed

Only MOD-0187. `ClaimContextMiddleware` had **zero** log calls where Carriers, Loads and Returns each
have two, so Claims produced no service-side result line at all — `result lines = 0` in the scan, against
26, 24 and 36 for the others. That is a real gap in a file inside MOD-0187's 47 owned paths
(`docs/roadmap/plans/mod-0187-runtime-dispatch-01/owned-paths.txt`).

Two lines added, matching the Returns shape exactly:

```
rejected   "Claim request rejected {Status} {Code} {CorrelationId}"     first statement of Error(...)
completed  "Claim request completed {Status} {CorrelationId}"           after await next(http)
```

`ILogger<ClaimContextMiddleware>` is injected as Returns does, and the two tests that construct the
middleware directly pass `NullLogger<ClaimContextMiddleware>.Instance`, as `ReturnIsolationTests.cs:42`
already did. Measured after: Claims 134/135 — the same single `ClaimReplayTests` failure the suite
carried before, by design — and Common 34/34.

## What row 13 still needs, per module

| module | remaining | who |
|---|---|---|
| MOD-0184 | pack `:302` list size and timings, plus a conflict/transaction-failure signal. **Not actionable by CT:** `:376` accepts §23 owned paths "as design; no runtime write" | owner release |
| MOD-0185 | nothing in code. `LoadsController.cs:21` carries replay; the zero was the lane | re-measure |
| MOD-0186 | nothing in code. `ReturnsController.cs:26` carries replay; the zero was the lane | re-measure |
| MOD-0187 | nothing in code after this lane | re-measure |

Three of the four need **a lane run, not a change**. This lane cannot supply it: the counts that close
row 13 come from a live stack with the UI, which is the Terminal runtime lane's work, and a source
reading cannot substitute for a log count. No row 13 cell is ticked here.

## Compliance

Read-only outside MOD-0187's owned paths and this record. No pack line ticked, no `Program.cs` touched,
nothing pushed. Q446's record is unmodified; this lane corrects it by addition, as records are not
rewritten.
