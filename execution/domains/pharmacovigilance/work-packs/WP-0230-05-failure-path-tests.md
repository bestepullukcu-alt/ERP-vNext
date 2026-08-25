---
id: WP-0230-05
title: Failure-path and PHI leak-scan suite
module: MOD-0230
service: Diten.PvgService
depends_on: [WP-0230-04]
gate: build/test only
status: ready
estimate: 1.5 d
---

# WP-0230-05 - Failure-path and PHI leak-scan suite

## Objective

Prove that every failure mode the MOD-0230 pack enumerates actually fails closed, and that no PHI or PII can
reach a log, trace, metric, audit payload, validation error, or error response.

**This pack is the evidence.** Without it, "fail closed" is an assertion. With it, it is a test result. It is
also the artefact you hand the MOD-0019, MOD-0023, and MOD-0031 owners when their modules ship and they need
to know what MOD-0230 expects.

## Preconditions

- [ ] WP-04 complete and its unit tests green.

## File manifest

```text
services/Diten.PvgService/tests/Diten.Pvg.Application.Tests/FailurePaths/
├── ValidationFailureTests.cs          F-01, F-02
├── AuthorizationFailureTests.cs       F-03, F-04
├── FieldSecurityFailureTests.cs       F-05, F-06, F-07
├── DependencyOutageTests.cs           F-09, F-10, F-11
├── TraceFailureTests.cs               F-12
├── DeleteAbsenceTests.cs              F-13
├── GateBeforeCommitTests.cs           C-15
└── PhiLeakScanTests.cs                F-08 - the important one
```

## The 12 failure paths

Straight from the MOD-0230 pack's Failure Path section. Each gets at least one test per affected surface.

| # | Path | Expected |
|---|---|---|
| F-01 | Missing required intake field | 400; **no record created** |
| F-02 | Duplicate / conflicting intake identifier | 409 with duplicate-candidate reason code; **no silent overwrite** |
| F-03 | Unauthorized actor | 401 / 403 per policy; no data leakage in the body |
| F-04 | Cross-tenant access | **404 or empty**; never 403-with-detail, never another tenant's row |
| F-05 | Unmasked sensitive field | request or response blocked / masked per `IPvgFieldSecurityPolicy` |
| F-06 | Missing field policy for a sensitive field | field omitted / masked, or operation denied. **No permissive fallback** |
| F-07 | Unauthorized field-level read | restricted field masked / omitted, or request denied |
| F-08 | Sensitive input reaches audit / log / trace | **test fails.** No raw PHI/PII/free text anywhere |
| F-09 | Evidence-link unavailable | fail closed. **No fake evidence pack** |
| F-10 | Workflow / Inbox unavailable | triage and routing transitions blocked. No untraceable routing |
| F-11 | Audit sink unavailable | regulated mutation **blocked**. No unaudited mutation |
| F-12 | Correlation / trace context missing | no untraceable regulated state change is created |
| F-13 | Delete or archive attempted before retention decision | operation **absent** - not merely denied |

Plus the conformance test deferred from WP-01:

| # | Test | Asserts |
|---|---|---|
| C-15 | Gate-before-commit | No state-changing handler reaches the repository without an `Allowed` transition result in the same correlation scope |

## F-08 - the PHI leak scan

This is the test most likely to be written weakly. Specify it precisely.

**Method.** Seed one intake record whose every PHI/PII field carries a unique, high-entropy canary string:

```csharp
const string CanaryNarrative = "CANARY-NARRATIVE-7f3a9c21-do-not-leak";
const string CanaryPatient   = "CANARY-PATIENT-4b8e1d55";
const string CanaryReporter  = "CANARY-REPORTER-9a2c7e04";
const string CanaryReason    = "CANARY-REASON-1d6f8b33";
const string CanaryOnset     = "CANARY-ONSET-2e5a4c17";
```

Then exercise **every** surface - create, update, triage, route, list, detail, and each failure path above -
while capturing:

1. the in-memory Serilog sink (all levels, including `Debug` and `Verbose`),
2. the OpenTelemetry activity/span export, including tags and events,
3. every metric name and label emitted,
4. every persisted `AuditEvent` document,
5. every HTTP response body, including 4xx and 5xx,
6. every `ValidationFailure` message,
7. any file the service writes.

Assert that **no canary string appears in any of the seven**. One assertion helper, applied to all of them:

```csharp
private static void AssertNoCanary(string haystack, string surface)
    => Assert.False(Canaries.Any(haystack.Contains, StringComparison.Ordinal),
        $"PHI canary leaked into {surface}");
```

**Also assert the negative-control:** the canary *must* appear in the persisted Mongo document. If it appears
nowhere at all, the test is passing for the wrong reason - the record was never written and the scan proves
nothing.

**Include the exception path.** Force an unhandled exception mid-handler with a populated entity and assert the
500 response body and the logged exception contain no canary. This is the single most common real-world PHI
leak: a stack trace with a serialized entity in it.

## Outage simulation

Do not mock at the port interface alone - that only proves the deny adapter works. For F-09, F-10, and F-11,
substitute a **throwing** adapter as well as a denying one:

| Path | Denying adapter | Throwing adapter |
|---|---|---|
| F-09 evidence | `DenyAllEvidenceLinkPort` | throws `TimeoutException` |
| F-10 workflow | `DenyAllWorkflowTransitionGate` | throws `HttpRequestException` |
| F-11 audit | audit writer returns failure | throws `MongoException` |

Both variants must produce the same outcome: **no mutation, safe reason code, no PHI in the error.** A handler
that catches an outage and proceeds is the exact defect this pack exists to catch.

## F-13 - absence, not denial

`Delete`, `BulkDelete`, `Archive`, `Void`, and `Export` must be *structurally absent*, not present-and-denied.

```csharp
[Fact] public void No_forbidden_surface_exists()
{
    var types = typeof(CreateCaseIntakeTriageCommand).Assembly.GetTypes();
    Assert.Empty(types.Where(t => Regex.IsMatch(t.Name, "Delete|Archive|Void|Export")));

    var actions = typeof(CaseIntakeTriageController).GetMethods()
        .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());
    Assert.Empty(actions.Where(m => m.GetCustomAttribute<HttpDeleteAttribute>() is not null));
}
```

Plus a repository reflection test asserting no member matches `/[Dd]elete/` other than `IsDeleted` / `DeletedAt`.

## Forbidden

- Asserting only on `Information` and above - `Debug` and `Verbose` are where entities get dumped.
- Mocking `ILogger` in a way that discards the formatted message. Capture the rendered string.
- Substituting a canary that could plausibly occur naturally. Use a GUID-bearing literal.
- Skipping the negative-control assertion.
- Adding production code in this pack. If a test reveals a defect, fix it in the owning pack and note it.

## Acceptance criteria

- [ ] F-01 through F-13 each have at least one passing test, F-05/F-06/F-07 one per affected surface.
- [ ] C-15 gate-before-commit passes.
- [ ] The F-08 scan covers all seven capture surfaces and includes the negative control.
- [ ] The exception-path leak test passes.
- [ ] Both denying and throwing variants tested for F-09, F-10, F-11.
- [ ] Forbidden surfaces proven **absent** by reflection, not denied.
- [ ] Full suite green: `dotnet test` on the service.

## Verify

```bash
dotnet test services/Diten.PvgService/tests/Diten.Pvg.Application.Tests/Diten.Pvg.Application.Tests.csproj
dotnet test services/Diten.PvgService/tests/Diten.Pvg.Application.Tests/Diten.Pvg.Application.Tests.csproj --filter "FullyQualifiedName~PhiLeakScan" -v n
```

## Agent prompt

> Implement WP-0230-05 in `/Users/natig/Projects/ERP-vNext-recovery`.
>
> Read first: `execution/domains/pharmacovigilance/work-packs/WP-0230-05-failure-path-tests.md`, the
> **Failure Path to Verify** and **Test Expectations** sections of
> `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`,
> `docs/specs/pvg-reg-pv-base-port-contracts-v1.md`, `.antigravity/rules/logging-observability.md`.
>
> Write tests only. If a test reveals a production defect, report it and fix it in the owning pack rather than
> weakening the test.
>
> The PHI leak scan is the centrepiece: unique GUID-bearing canaries in every PHI/PII field, captured across
> logs (including Debug/Verbose), traces and span tags, metric labels, audit documents, HTTP bodies including
> 4xx/5xx, validation messages, and written files. Include the negative control asserting the canary *is* in
> the Mongo document, and the forced-exception case.
>
> For evidence, workflow, and audit outages, test both a denying adapter and a throwing adapter. Both must
> produce no mutation.
>
> Prove Delete/BulkDelete/Archive/Void/Export are structurally absent by reflection, not merely denied.
>
> Report the full test output.
