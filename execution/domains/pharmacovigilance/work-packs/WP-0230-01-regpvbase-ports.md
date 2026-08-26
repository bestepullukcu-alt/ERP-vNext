---
id: WP-0230-01
title: REG-PV-BASE consumption ports
module: MOD-0230
service: Diten.PvgService
depends_on: []
gate: build/test only
status: ready
estimate: 1.5 d
---

# WP-0230-01 - REG-PV-BASE consumption ports

## Objective

Build the three PVG-owned consumption ports that stand in for MOD-0019, MOD-0023, and MOD-0031, each with a
deny-by-default adapter and one configuration-gated non-production adapter, plus the conformance suite that
proves the whole thing fails closed.

**This is the gate on everything downstream.** No MOD-0230 handler may be written until C-01 through C-17 pass.

Full contract spec: [`docs/specs/pvg-reg-pv-base-port-contracts-v1.md`](../../../../docs/specs/pvg-reg-pv-base-port-contracts-v1.md).

## Preconditions

- [ ] DCP-004 `approved`, MOD-0230 `ready-for-dev` - both true as of 2026-08-09.
- [ ] `services/Diten.PvgService/` stale `bin`/`obj` deleted (see work-pack README).
- [ ] WP-02 may run first if you prefer the projects to exist before the ports; either order works, but the ports must compile before WP-04.

## The one rule

**A port is an interface plus a deny-by-default adapter. Nothing else.**

It stores no policy data, hosts no workflow engine, persists no evidence, and makes no regulated decision on
its own authority. If a reviewer can point at code where a port *decides* rather than *asks and denies*, the
pack is rejected.

## File manifest

Create only these. Namespace root `Diten.Pvg.Application.RegPvBase`.

```text
services/Diten.PvgService/src/Diten.Pvg.Application/RegPvBase/
├── Ports/
│   ├── RegPvBaseModels.cs                    PvgPortOutcome, PvgDenyReason, PvgPortResult, PvgSurface, request records
│   ├── IPvgFieldSecurityPolicy.cs
│   ├── IPvgWorkflowTransitionGate.cs
│   ├── IPvgEvidenceLinkPort.cs
│   └── IPvgSafetyPartnerAdapter.cs
├── Adapters/Deny/
│   ├── DenyAllFieldSecurityPolicy.cs
│   ├── DenyAllWorkflowTransitionGate.cs
│   ├── DenyAllEvidenceLinkPort.cs
│   └── NotConfiguredSafetyPartnerAdapter.cs
├── Adapters/NonProduction/
│   ├── PvgStaticFieldPolicy.cs
│   ├── PvgStaticTransitionGate.cs
│   └── PvgPendingEvidenceStore.cs
└── RegPvBaseServiceCollectionExtensions.cs

services/Diten.PvgService/tests/Diten.Pvg.Application.Tests/RegPvBase/
├── PortDefaultsTests.cs                      C-01, C-02, C-06, C-13, C-14
├── RegistrationGuardTests.cs                 C-03, C-04, C-05
├── StaticFieldPolicyTests.cs                 C-07, C-08
├── StaticTransitionGateTests.cs              C-09, C-10
├── PendingEvidenceStoreTests.cs              C-11, C-12
└── PortArchitectureTests.cs                  C-16, C-17
```

C-15 (gate-before-commit) lives in WP-05 because it needs handlers to exist.

## Implementation spec

### 1. Result model - `RegPvBaseModels.cs`

```csharp
namespace Diten.Pvg.Application.RegPvBase.Ports;

public enum PvgPortOutcome { Denied = 0, Allowed = 1, Masked = 2, Omitted = 3, Pending = 4 }

public enum PvgDenyReason
{
    ContractUnavailable = 0, PolicyMissing, PolicyEvaluationFailed, NotPermitted,
    TenantMismatch, TransitionBlocked, EvidenceIncomplete, LegalHold, NonProductionAdapterDisabled
}

public sealed record PvgPortResult(
    PvgPortOutcome Outcome, PvgDenyReason? Reason, string? SafeReasonCode, string CorrelationId)
{
    public bool IsAllowed => Outcome == PvgPortOutcome.Allowed;
    public static PvgPortResult Deny(PvgDenyReason reason, string code, string correlationId)
        => new(PvgPortOutcome.Denied, reason, code, correlationId);
}

public enum PvgSurface { List, Detail, Create, Update, Export, Audit }
```

Non-negotiable: `default(PvgPortOutcome)` **is** `Denied`, and `default(PvgDenyReason)` **is**
`ContractUnavailable`. No port returns `bool`. No port returns `null` to mean allowed.

`SafeReasonCode` values come from a closed taxonomy constant class. Raw exception text, field values,
narratives, patient codes, and reporter details must never reach it.

`CorrelationId` comes from `ICorrelationContext` (`Diten.Platform.Common.Observability`). A call arriving with
a null or empty correlation ID **denies** with `ContractUnavailable`.

### 2. Port interfaces

Signatures are in the contract spec §3-§6. Copy them verbatim; do not add methods.

`IPvgFieldSecurityPolicy` - `EvaluateFieldAsync`, `EvaluateProjectionAsync`
`IPvgWorkflowTransitionGate` - `EvaluateTransitionAsync`, `ResolveRouteTargetAsync`
`IPvgEvidenceLinkPort` - `RecordRequirementAsync`, `EvaluateCompletenessAsync`, `QueryReferencesAsync`
`IPvgSafetyPartnerAdapter` - `PushIntakeAsync`, `PullCaseStatusAsync`

### 3. Deny adapters

Every method returns `PvgPortResult.Deny(PvgDenyReason.ContractUnavailable, "PVG-PORT-UNAVAILABLE", correlationId)`.
`EvaluateProjectionAsync` returns `AnyDenied = true` with every field denied.
`QueryReferencesAsync` returns an **empty list** - never a fabricated reference.
`NotConfiguredSafetyPartnerAdapter` **throws** `NotSupportedException` on every call.

### 4. Non-production adapters

`PvgStaticFieldPolicy` - applies the sensitivity matrix in the contract spec §3.4, driven by the 16-field table
in the MOD-0230 pack. Unknown field name or unknown sensitivity class → **deny**. Export → **deny** for every
class except `public-metadata`.

`PvgStaticTransitionGate` - allows only the transitions in contract spec §4:
`Received→Triaged` (requires `TriageOutcome` + `RouteTargetQueue` present), `Received→Rejected|Duplicate`,
`Triaged→Rejected|Duplicate`. Everything else denies with `TransitionBlocked`.
`ResolveRouteTargetAsync` **always denies** - the queue registry is MOD-0023's and does not exist.

`PvgPendingEvidenceStore` - `RecordRequirementAsync` returns `Pending`. `EvaluateCompletenessAsync`
**never** returns `Allowed` under any input. `QueryReferencesAsync` returns requirement codes and status only.
The `PvgEvidenceReference` record must have **no** field capable of carrying document content.

### 5. Registration + production guard

```csharp
public static IServiceCollection AddRegPvBasePorts(
    this IServiceCollection services, IConfiguration config, IHostEnvironment env)
{
    // Deny defaults register FIRST, unconditionally.
    services.AddScoped<IPvgFieldSecurityPolicy, DenyAllFieldSecurityPolicy>();
    services.AddScoped<IPvgWorkflowTransitionGate, DenyAllWorkflowTransitionGate>();
    services.AddScoped<IPvgEvidenceLinkPort, DenyAllEvidenceLinkPort>();
    services.AddScoped<IPvgSafetyPartnerAdapter, NotConfiguredSafetyPartnerAdapter>();

    if (!config.GetValue<bool>("Pvg:RegPvBase:UseNonProductionAdapters")) return services;

    if (env.IsProduction())
        throw new InvalidOperationException(
            "PVG-REGPVBASE-001: non-production REG-PV-BASE adapters are forbidden in Production. " +
            "MOD-0019, MOD-0023, and MOD-0031 clients are required for operational runtime.");

    services.AddScoped<IPvgFieldSecurityPolicy, PvgStaticFieldPolicy>();
    services.AddScoped<IPvgWorkflowTransitionGate, PvgStaticTransitionGate>();
    services.AddScoped<IPvgEvidenceLinkPort, PvgPendingEvidenceStore>();
    return services;
}
```

It **throws**. It does not warn, log-and-continue, or downgrade.
`Pvg:RegPvBase:UseNonProductionAdapters` goes in `appsettings.Development.json` only - never in
`appsettings.json` or any production config file.

## Forbidden

- Adding a method to any port interface beyond the four listed.
- Any adapter that reads or writes a database, cache, file, or HTTP endpoint.
- A hardcoded queue list, a hardcoded evidence pack, or a masking rule that is not in the MOD-0230 field table.
- Returning `Allowed` from `EvaluateCompletenessAsync` under any condition.
- `bool` return types on port methods.
- Registering a non-production adapter before the deny default.

## Acceptance criteria

- [ ] All 12 source files and 6 test files created, no others touched.
- [ ] `default(PvgPortOutcome) == PvgPortOutcome.Denied` holds.
- [ ] Deny defaults register first and unconditionally.
- [ ] Host throws when `UseNonProductionAdapters=true` and `env.IsProduction()`.
- [ ] `Pvg:RegPvBase:UseNonProductionAdapters` appears in **no** production config file.
- [ ] C-01 through C-14, C-16, C-17 pass.
- [ ] No port assembly references a repository, DbContext, `IMongoDatabase`, or `HttpClient`.

## Tests

| # | Test | Asserts |
|---|---|---|
| C-01 | `default(PvgPortOutcome)` is `Denied` | Uninitialised outcome cannot mean allowed |
| C-02 | Every deny adapter denies every method, every input | No accidental allow branch |
| C-03 | Deny adapters register when config is absent, empty, or malformed | Config failure closes |
| C-04 | Host throws when non-prod adapters + Production | Cannot reach production |
| C-05 | No production appsettings contains the switch | Static config scan |
| C-06 | Null/empty correlation ID denies with `ContractUnavailable` | Trace requirement at the port |
| C-07 | Static field policy denies unknown field / unknown class | No permissive default row |
| C-08 | Static field policy denies Export for every non-public class | Masked export needs real MOD-0019 |
| C-09 | Static gate denies every transition outside the table | State set is closed |
| C-10 | `ResolveRouteTargetAsync` always denies | Queue registry is MOD-0023's |
| C-11 | `EvaluateCompletenessAsync` never returns `Allowed` | Evidence cannot be fabricated |
| C-12 | `PvgEvidenceReference` carries no document-content field | Reflection test |
| C-13 | Cross-tenant port call denies with `TenantMismatch` | Tenant isolation at the port |
| C-14 | No taxonomy `SafeReasonCode` contains free text / field value / PHI | Closed taxonomy |
| C-16 | Port assemblies reference no store, engine, or repository | Architecture test |
| C-17 | `IPvgSafetyPartnerAdapter` has one registration and it throws | Boundary declared, not built |

## Verify

```bash
dotnet build services/Diten.PvgService/src/Diten.Pvg.Application/Diten.Pvg.Application.csproj -v q
dotnet test  services/Diten.PvgService/tests/Diten.Pvg.Application.Tests/Diten.Pvg.Application.Tests.csproj --filter "FullyQualifiedName~RegPvBase"
git diff --check
```

## Agent prompt

> Implement WP-0230-01 in `/Users/natig/Projects/ERP-vNext-recovery`.
>
> Read first, in order: `execution/domains/pharmacovigilance/work-packs/WP-0230-01-regpvbase-ports.md`,
> `docs/specs/pvg-reg-pv-base-port-contracts-v1.md`,
> `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` (Entity Fields and
> Validation Rules sections), `execution/domains/pharmacovigilance/domain-config.md`, `AGENTS.md`.
>
> Create only the files in the WP-01 manifest. Do not create handlers, entities, controllers, or views - those
> are WP-03 and WP-04. Do not touch `.antigravity/**`, other domains, or other services.
>
> The binding rule: a port is an interface plus a deny-by-default adapter and nothing else. No policy storage,
> no workflow engine, no evidence persistence. Deny defaults register first and unconditionally. The
> non-production registration path must **throw** in a Production environment.
>
> Implement conformance tests C-01 to C-14, C-16, C-17 exactly as specified. Do not implement C-15 (it needs
> handlers). Report the build and test command output; do not mark the pack done unless every test passes.
