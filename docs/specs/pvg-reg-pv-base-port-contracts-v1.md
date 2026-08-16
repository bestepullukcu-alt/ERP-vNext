# REG-PV-BASE Consumption Port Contracts v1 (W-3A0-Lite)

> **Scope:** the three PVG-owned consumption ports that let MOD-0230 be built and tested before MOD-0019,
> MOD-0023, and MOD-0031 exist as real modules.
>
> **Authority:** [OD-2 decision record](../../execution/portfolio/delivery-capability-packs/DCP-004-open-decision-records-2026-08-09.md).
> **Owner:** NY / PVG. **Location:** `services/Diten.PvgService/`.
> **Authorizes:** build and test only. It does **not** authorize operational runtime.

---

## 0. The one rule that governs this whole package

**A port is an interface plus a deny-by-default adapter. Nothing else.**

A port must never store policy data, host a workflow engine, persist evidence, or make a regulated decision on
its own authority. The moment a port starts *deciding* rather than *asking and denying*, it has become an
unauthorized reimplementation of MOD-0019, MOD-0023, or MOD-0031, and it must be rejected in review.

The five existing REG-PV-BASE legs - MOD-0018 authorization, MOD-0021 audit, MOD-0041 observability,
correlation, tenancy - get **no port**. They are consumed directly from `Diten.Platform.Common` and
`Diten.Platform`, because they already exist as merged code.

---

## 1. Project layout

```text
services/Diten.PvgService/
├── src/
│   ├── Diten.Pvg.Application/
│   │   └── RegPvBase/                        <-- this package
│   │       ├── Ports/
│   │       │   ├── IPvgFieldSecurityPolicy.cs
│   │       │   ├── IPvgWorkflowTransitionGate.cs
│   │       │   ├── IPvgEvidenceLinkPort.cs
│   │       │   ├── IPvgSafetyPartnerAdapter.cs    (declared, not implemented)
│   │       │   └── RegPvBaseModels.cs
│   │       ├── Adapters/Deny/                 <-- DI default, always registered first
│   │       │   ├── DenyAllFieldSecurityPolicy.cs
│   │       │   ├── DenyAllWorkflowTransitionGate.cs
│   │       │   └── DenyAllEvidenceLinkPort.cs
│   │       ├── Adapters/NonProduction/         <-- config-gated, refuses Production
│   │       │   ├── PvgStaticFieldPolicy.cs
│   │       │   ├── PvgStaticTransitionGate.cs
│   │       │   └── PvgPendingEvidenceStore.cs
│   │       └── RegPvBaseServiceCollectionExtensions.cs
│   ├── Diten.Pvg.Domain/
│   ├── Diten.Pvg.Persistence/
│   ├── Diten.Pvg.Infrastructure/
│   └── Diten.Pvg.API/
└── tests/
    └── Diten.Pvg.Application.Tests/
        └── RegPvBase/                         <-- conformance suite
```

---

## 2. Shared result model

Every port returns an explicit outcome. **No port returns `bool`, and no port returns `null` to mean "allowed".**
An ambiguous return is the single most likely way a fail-closed design silently becomes fail-open.

```csharp
namespace Diten.Pvg.Application.RegPvBase.Ports;

public enum PvgPortOutcome
{
    Denied = 0,          // default(PvgPortOutcome) MUST be Denied
    Allowed = 1,
    Masked = 2,
    Omitted = 3,
    Pending = 4
}

public enum PvgDenyReason
{
    ContractUnavailable = 0,   // default: no owner contract wired
    PolicyMissing,
    PolicyEvaluationFailed,
    NotPermitted,
    TenantMismatch,
    TransitionBlocked,
    EvidenceIncomplete,
    LegalHold,
    NonProductionAdapterDisabled
}

public sealed record PvgPortResult(
    PvgPortOutcome Outcome,
    PvgDenyReason? Reason,
    string? SafeReasonCode,      // taxonomy code only - never free text, never PHI
    string CorrelationId)
{
    public bool IsAllowed => Outcome == PvgPortOutcome.Allowed;

    public static PvgPortResult Deny(PvgDenyReason reason, string code, string correlationId)
        => new(PvgPortOutcome.Denied, reason, code, correlationId);
}
```

**Binding constraints**

- `default(PvgPortOutcome)` is `Denied`. An uninitialised struct, a deserialization miss, or a forgotten branch all land on deny.
- `SafeReasonCode` is drawn from the MOD-0041 reason-code taxonomy. Raw exception text, field values, narratives, patient codes, and reporter details must never appear.
- `CorrelationId` is taken from `ICorrelationContext` (`Diten.Platform.Common/Observability`). A port call without a correlation ID denies with `ContractUnavailable`.

---

## 3. Port 1 - `IPvgFieldSecurityPolicy` (stands in for MOD-0019, `SEC-DATA-BUNDLE`)

```csharp
public interface IPvgFieldSecurityPolicy
{
    /// Evaluate one field on one surface for the current actor and tenant.
    Task<PvgPortResult> EvaluateFieldAsync(
        PvgFieldSecurityRequest request, CancellationToken ct);

    /// Evaluate a whole projection. Any field that cannot be evaluated denies the whole projection.
    Task<PvgFieldSecurityDecision> EvaluateProjectionAsync(
        PvgProjectionSecurityRequest request, CancellationToken ct);
}

public sealed record PvgFieldSecurityRequest(
    string FieldName,
    string SensitivityClass,      // public-metadata | confidential | regulated-safety | PII | PHI
    PvgSurface Surface,           // List | Detail | Create | Update | Export | Audit
    Guid TenantId,
    string ActorId,
    string CorrelationId);

public enum PvgSurface { List, Detail, Create, Update, Export, Audit }

public sealed record PvgFieldSecurityDecision(
    IReadOnlyDictionary<string, PvgPortResult> PerField,
    bool AnyDenied);
```

**Required behaviour of `DenyAllFieldSecurityPolicy` (the DI default)**

| Input | Result |
|---|---|
| Any field, any surface | `Denied` / `ContractUnavailable` |
| Projection with any field | `AnyDenied = true` |

**`PvgStaticFieldPolicy` (non-production only)** applies the 16-field sensitivity matrix already written in the
MOD-0230 pack. It is a lookup over PVG's *own* declared matrix - PVG's input to MOD-0019, not a masking engine.

| Sensitivity class | List | Detail | Create/Update | Export | Audit payload |
|---|---|---|---|---|---|
| `public-metadata` | Allowed | Allowed | Allowed | Allowed | Allowed |
| `confidential` | Masked | Allowed | Allowed | **Denied** | Omitted |
| `regulated-safety` | Masked | Allowed | Allowed | **Denied** | Omitted |
| `PII` | Omitted | Masked | Allowed | **Denied** | Omitted |
| `PHI` | Omitted | Masked | Allowed | **Denied** | Omitted |

An unknown field name or unknown sensitivity class **denies**. There is no permissive default row.
Export denies for every non-public class because masked export requires a real MOD-0019 approval - and export
is out of slice-1 scope anyway.

---

## 4. Port 2 - `IPvgWorkflowTransitionGate` (stands in for MOD-0023, Workflow/Inbox v1)

```csharp
public interface IPvgWorkflowTransitionGate
{
    /// MUST be called before the mutation is committed, never after.
    Task<PvgPortResult> EvaluateTransitionAsync(
        PvgTransitionRequest request, CancellationToken ct);

    Task<PvgPortResult> ResolveRouteTargetAsync(
        PvgRouteRequest request, CancellationToken ct);
}

public sealed record PvgTransitionRequest(
    string ObjectType,            // "SafetyCaseIntake"
    Guid ObjectId,
    string FromState,
    string ToState,
    string ReasonCode,            // taxonomy code - never free text
    Guid TenantId,
    string ActorId,
    string CorrelationId);

public sealed record PvgRouteRequest(
    Guid ObjectId, string RequestedQueue, Guid TenantId, string ActorId, string CorrelationId);
```

**Required behaviour of `DenyAllWorkflowTransitionGate` (the DI default)**

Every transition and every route resolution returns `Denied` / `ContractUnavailable`.

**`PvgStaticTransitionGate` (non-production only)** enforces the triage state set declared in the MOD-0230 pack
and nothing more. It owns **no** queue registry, **no** assignment policy, **no** SLA, and **no** escalation -
those are MOD-0023's, and requesting them denies.

| From | To | Allowed |
|---|---|---|
| `Received` | `Triaged` | Yes, if `TriageOutcome` and `RouteTargetQueue` are present |
| `Received` | `Rejected`, `Duplicate` | Yes, with reason code |
| `Triaged` | `Rejected`, `Duplicate` | Yes, with reason code |
| anything else | anything else | **Denied** / `TransitionBlocked` |
| `ResolveRouteTargetAsync` | any queue | **Denied** / `ContractUnavailable` - queue registry is MOD-0023's |

**Gate-before-commit is mandatory.** A conformance test asserts that no handler commits a state change without
an `Allowed` result from this port in the same correlation scope.

---

## 5. Port 3 - `IPvgEvidenceLinkPort` (stands in for MOD-0031, `EVIDENCE-LINK`)

```csharp
public interface IPvgEvidenceLinkPort
{
    Task<PvgPortResult> RecordRequirementAsync(
        PvgEvidenceRequirement requirement, CancellationToken ct);

    /// Returns Allowed only when the owning evidence service confirms completeness.
    Task<PvgPortResult> EvaluateCompletenessAsync(
        PvgEvidenceCompletenessRequest request, CancellationToken ct);

    Task<IReadOnlyList<PvgEvidenceReference>> QueryReferencesAsync(
        Guid objectId, Guid tenantId, string correlationId, CancellationToken ct);
}

public sealed record PvgEvidenceRequirement(
    Guid ObjectId, string ObjectType, string RequirementCode, Guid TenantId, string CorrelationId);

public sealed record PvgEvidenceReference(
    Guid ObjectId, string RequirementCode, PvgPortOutcome Status);   // never carries document content
```

**Required behaviour of `DenyAllEvidenceLinkPort` (the DI default)**

Completeness always returns `Denied` / `ContractUnavailable`. `QueryReferencesAsync` returns an empty list -
never a fabricated reference.

**`PvgPendingEvidenceStore` (non-production only)** records requirements as `Pending` and **never** returns
`Allowed` from `EvaluateCompletenessAsync`. This is the hardest constraint in the package:

- It may never report evidence as satisfied.
- It may never assemble or return an evidence pack.
- It may never store, copy, or reference document content - only requirement codes and a status.
- Downstream handoff to MOD-0231 stays blocked, exactly as the MOD-0230 pack requires.

The result is that evidence-gated paths remain provably blocked in the build gate, which is the correct
outcome - MOD-0031 does not exist.

---

## 6. Port 4 - `IPvgSafetyPartnerAdapter` (declared, not implemented)

Per OD-7, the partner boundary is declared in slice 1 so a bought PV safety system can be wrapped later without
changing the tenant surface. **No implementation is authorized in slice 1**, not even a deny adapter with
behaviour - only the interface and a throwing `NotConfiguredSafetyPartnerAdapter`.

```csharp
public interface IPvgSafetyPartnerAdapter
{
    Task<PvgPortResult> PushIntakeAsync(Guid intakeId, Guid tenantId, string correlationId, CancellationToken ct);
    Task<PvgPortResult> PullCaseStatusAsync(Guid intakeId, Guid tenantId, string correlationId, CancellationToken ct);
}
```

---

## 7. DI registration and the production guard

```csharp
public static IServiceCollection AddRegPvBasePorts(
    this IServiceCollection services, IConfiguration config, IHostEnvironment env)
{
    // 1. Deny-by-default ALWAYS registers first. If everything below fails, deny wins.
    services.AddScoped<IPvgFieldSecurityPolicy, DenyAllFieldSecurityPolicy>();
    services.AddScoped<IPvgWorkflowTransitionGate, DenyAllWorkflowTransitionGate>();
    services.AddScoped<IPvgEvidenceLinkPort, DenyAllEvidenceLinkPort>();
    services.AddScoped<IPvgSafetyPartnerAdapter, NotConfiguredSafetyPartnerAdapter>();

    var useNonProd = config.GetValue<bool>("Pvg:RegPvBase:UseNonProductionAdapters");
    if (!useNonProd) return services;

    // 2. Hard refusal. Not a warning, not a log line - the host does not start.
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

**Rules**

- The deny defaults register unconditionally and first. A configuration error can only ever leave the system *more* closed.
- `Pvg:RegPvBase:UseNonProductionAdapters` defaults to `false` and must be absent from any production appsettings file.
- The Production check throws. It does not warn, downgrade, or fall back.

---

## 8. Conformance test suite - the gate on this package

`tests/Diten.Pvg.Application.Tests/RegPvBase/`. All of these must be green before any MOD-0230 handler is
written. This suite is the evidence that the fail-closed design is real rather than asserted.

| # | Test | Asserts |
|---|---|---|
| C-01 | `Default(PvgPortOutcome)` is `Denied` | Uninitialised outcome cannot mean allowed |
| C-02 | Every deny adapter denies every method, every input | No accidental allow branch |
| C-03 | Deny adapters register when configuration is absent, empty, or malformed | Config failure closes, never opens |
| C-04 | Host **throws** when `UseNonProductionAdapters=true` and environment is Production | Non-prod adapters cannot reach production |
| C-05 | No production appsettings file contains `UseNonProductionAdapters` | Static config scan |
| C-06 | Port call without a correlation ID denies with `ContractUnavailable` | Trace requirement holds at the port |
| C-07 | `PvgStaticFieldPolicy` denies unknown field names and unknown sensitivity classes | No permissive default row |
| C-08 | `PvgStaticFieldPolicy` denies Export for every non-`public-metadata` class | Masked export needs real MOD-0019 |
| C-09 | `PvgStaticTransitionGate` denies every transition outside the declared table | State set is closed |
| C-10 | `PvgStaticTransitionGate.ResolveRouteTargetAsync` always denies | Queue registry belongs to MOD-0023 |
| C-11 | `PvgPendingEvidenceStore.EvaluateCompletenessAsync` **never** returns `Allowed` | Evidence cannot be fabricated |
| C-12 | `PvgEvidenceReference` type carries no document-content field | Structural, via reflection |
| C-13 | Cross-tenant port call denies with `TenantMismatch` | Tenant isolation at the port |
| C-14 | No `PvgPortResult.SafeReasonCode` in the taxonomy contains free text, a field value, or PHI | Reason-code taxonomy is closed |
| C-15 | Gate-before-commit: no state-changing handler commits without an `Allowed` transition result in the same correlation scope | Architecture test over handlers |
| C-16 | Port assemblies reference no policy store, workflow engine, or evidence repository | Architecture test - ports have not become implementations |
| C-17 | `IPvgSafetyPartnerAdapter` has exactly one registration and it throws | Partner boundary declared, not built |

---

## 9. Swap plan - retiring each port

When a real module ships, exactly one line changes per port. If more than one line changes, the port was
designed wrong.

| Port | Replaced by | Change | Evidence row it closes |
|---|---|---|---|
| `IPvgFieldSecurityPolicy` | `Mod0019FieldSecurityClient` | one DI line | `PVG-MOD0230-FieldSecurity-Contract v1` |
| `IPvgWorkflowTransitionGate` | `Mod0023WorkflowGateClient` | one DI line | `PVG-MOD0230-WorkflowTransitionGate-v1` |
| `IPvgEvidenceLinkPort` | `Mod0031EvidenceLinkClient` | one DI line | `PVG-MOD0230-EvidenceLink-v1` |

**Retirement criteria for the non-production adapters:** once the corresponding real client is registered in a
given environment, the non-production adapter is deleted from that environment's configuration. When all three
real clients exist, `Pvg:RegPvBase:UseNonProductionAdapters` and all three non-production adapter classes are
deleted from the repository entirely. Test C-05 is then extended to assert their absence.

---

## 10. What this package explicitly does not do

- It does not authorize operational runtime, production deployment, supplier qualification, or validation.
- It does not implement MOD-0019, MOD-0023, or MOD-0031, and it does not reduce their scope.
- It does not close their owner-evidence rows in the MOD-0230 pack. Those stay `[ ]`.
- It does not enable archive, void, export, or delete on MOD-0230.
- It does not enable any AI behaviour.
- It does not touch `.antigravity/**`, other domains' packs, or other services.
