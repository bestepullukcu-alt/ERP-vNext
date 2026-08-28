---
id: WP-0230-04
title: CQRS handlers and API surface
module: MOD-0230
service: Diten.PvgService
depends_on: [WP-0230-01, WP-0230-03]
gate: build/test only
status: ready
estimate: 2 d
---

# WP-0230-04 - CQRS handlers and API surface

## Objective

Implement the slice-1 command and query surface for MOD-0230 - create, update, triage, route, list, detail -
wired through the REG-PV-BASE ports, tenant context, audit, and correlation, with every dependency failing
closed.

## Preconditions

- [ ] WP-01 conformance suite green.
- [ ] WP-03 entity and repository complete.

## File manifest

```text
services/Diten.PvgService/src/Diten.Pvg.Application/Features/CaseIntakeTriage/
├── CaseIntakeTriageModels.cs          DTOs: list item, detail, filter
├── Commands/
│   ├── CreateCaseIntakeTriageCommand.cs
│   ├── UpdateCaseIntakeTriageCommand.cs
│   ├── TriageCaseIntakeTriageCommand.cs
│   └── RouteCaseIntakeTriageCommand.cs
├── Queries/
│   ├── GetCaseIntakeTriageListQuery.cs
│   └── GetCaseIntakeTriageByIdQuery.cs
├── Handlers/CommandHandlers/          Create*, Update*, Triage*, Route*  (suffix: Handler)
├── Handlers/QueryHandlers/            GetCaseIntakeTriageList*, GetCaseIntakeTriageById*
└── Validators/
    ├── CreateCaseIntakeTriageValidator.cs
    ├── UpdateCaseIntakeTriageValidator.cs
    ├── TriageCaseIntakeTriageValidator.cs
    └── RouteCaseIntakeTriageValidator.cs

services/Diten.PvgService/src/Diten.Pvg.Application/Common/
├── PvgPermissions.cs                  permission key constants
└── PvgReasonCodes.cs                  closed safe-reason-code taxonomy

services/Diten.PvgService/src/Diten.Pvg.API/Controllers/
└── CaseIntakeTriageController.cs
```

## Implementation spec

### Naming - binding, from the MOD-0230 pack

- Handlers end in `Handler`. **Never** `CommandHandler`, `QueryHandler`, or `RequestHandler`.
- Validators end in `Validator`. **Never** `CommandValidator`.
- Commands and queries are `sealed record`s returning `Response<T>` from namespace `Diten.Shared.Core`.

### Permission keys - PKS-001 compliant

```csharp
public static class PvgPermissions
{
    public const string Read   = "pvg.case-intake-triage.read";
    public const string Create = "pvg.case-intake-triage.create";
    public const string Update = "pvg.case-intake-triage.update";
    public const string Triage = "pvg.case-intake-triage.triage";
    public const string Route  = "pvg.case-intake-triage.route";
    // NOT in slice 1: .archive, .export
    // NEVER: .delete, .bulk-delete
}
```

### The handler pipeline - the order is the contract

Every **mutating** handler executes in exactly this order. Any deviation is a defect, not a style choice.

```text
1. Resolve correlation id      → ICorrelationContext. Null/empty ⇒ deny, no mutation.
2. Resolve tenant              → ITenantContext. Never from the payload.
3. Authorize                   → IEntitlementChecker. Deny ⇒ 403, no body detail.
4. Field security              → IPvgFieldSecurityPolicy.EvaluateProjectionAsync over every field touched.
                                  AnyDenied ⇒ deny the operation. No permissive fallback.
5. Validate                    → FluentValidation, per the MOD-0230 Validation Rules table.
6. Workflow gate               → IPvgWorkflowTransitionGate.EvaluateTransitionAsync,
                                  BEFORE commit, for any lifecycle change. Not Allowed ⇒ no mutation.
7. Evidence                    → IPvgEvidenceLinkPort where the operation requires evidence.
                                  Not Allowed ⇒ block. Pending is not Allowed.
8. Persist                     → repository.
9. Audit                       → AuditEvent with a redacted payload allow-list.
                                  Audit unavailable ⇒ the mutation FAILS. No unaudited mutation. Ever.
```

Steps 1-7 all precede persistence. Auditing after persistence is only acceptable via the platform's approved
durable outbox; if the outbox is unavailable, the mutation fails.

### Audit payload allow-list

Only these may be written to an audit payload:

`Id`, `CaseNumber`, `TenantId`, `LifecycleState`, `TriageOutcome`, `IntakeChannel`, `SourceType`,
`Seriousness`, `IntakePriority`, `ReceivedAtUtc`, `CorrelationId`, actor id, UTC timestamp, reason **code**.

Everything else is excluded. `AdverseEventNarrative`, `TriageReason`, `PatientSubjectCode`, `EventOnsetDate`,
`ReporterContactSummary`, `SourceReference`, and `SuspectProductText` must never appear - not as values, not
as before/after diffs, not in a serialized command object.

### Command semantics

| Command | Behaviour |
|---|---|
| `Create` | `LifecycleState = Received`. Server sets `CaseNumber`, `TenantId`, `CorrelationId`. Duplicate `(SourceType, SourceReference)` in-tenant ⇒ **409** with a duplicate-candidate reason code, never a silent overwrite. |
| `Update` | Pre-triage only for the Intake Agent role. Cannot change `LifecycleState`, `CaseNumber`, `TenantId`, or `CorrelationId`. |
| `Triage` | Requires `TriageOutcome`. `TriageReason` mandatory when outcome is `Rejected` or `Duplicate`, and must carry a taxonomy reason code. Transition gate runs before commit. |
| `Route` | **Will not succeed in slice 1.** `ResolveRouteTargetAsync` denies unconditionally because the queue registry is MOD-0023's. The command, endpoint, permission, audit event, and fail-closed test are all built anyway - they go live with a one-line DI swap when MOD-0023 ships. Any implementation that makes Route succeed by inventing a queue list must be rejected. |

### Queries

`GetCaseIntakeTriageListQuery(CaseIntakeTriageFilter)` - tenant-scoped, paged, sorted by `ReceivedAtUtc` desc.
List projection applies field security per surface: PHI fields **omitted**, confidential/regulated-safety
**masked**. `GetCaseIntakeTriageByIdQuery(Guid)` - PHI **masked**, not omitted, per the matrix. A row belonging
to another tenant returns **404**, never 403.

### Controller

Mirror `GoldenReferenceCompactController`: `[Authorize]`, `[ApiController]`,
`[Route("api/v1/pv-case-intake-triage")]` (downstream template - the Gateway maps `/api/pv-case-intake-triage`
onto it in WP-06), inherits `CustomBaseController`, one `[HasPermission(...)]` per action.

```csharp
[HttpGet]                        [HasPermission(PvgPermissions.Read)]
[HttpGet("{id:guid}")]           [HasPermission(PvgPermissions.Read)]
[HttpPost]                       [HasPermission(PvgPermissions.Create)]
[HttpPut("{id:guid}")]           [HasPermission(PvgPermissions.Update)]
[HttpPost("{id:guid}/triage")]   [HasPermission(PvgPermissions.Triage)]
[HttpPost("{id:guid}/route")]    [HasPermission(PvgPermissions.Route)]
```

**No `[HttpDelete]` of any kind. No `/export`. No `/archive`.**

### Error model

Every failure returns the `Response<T>` envelope with a `PvgReasonCodes` taxonomy value. Never an exception
message, never a stack trace, never a field value. `PvgReasonCodes` is a closed constant class - a reason code
not in it cannot be emitted.

## Forbidden

- `DeleteCaseIntakeTriageCommand`, `BulkDeleteCaseIntakeTriageCommand`, `ArchiveCaseIntakeTriageCommand`, `VoidCaseIntakeTriageCommand`, any export command.
- `[HttpDelete]`, `/bulk-delete`, `/export`, `/archive` routes.
- `CommandHandler` / `QueryHandler` / `RequestHandler` / `CommandValidator` suffixes.
- Accepting `TenantId` in any DTO, command, or form payload.
- Any permissive fallback when a port denies or is unavailable.
- Logging or auditing a field outside the allow-list.
- Calling a repository before the workflow gate returns `Allowed` for a lifecycle change.
- Hardcoding a route-target queue list to make `Route` succeed.

## Acceptance criteria

- [ ] Exactly four commands and two queries exist; no delete, archive, void, or export.
- [ ] All handlers end in `Handler`, all validators in `Validator`.
- [ ] Every mutating handler follows the 9-step order.
- [ ] Every port denial results in no mutation and a safe reason code.
- [ ] Audit failure blocks the mutation.
- [ ] `Route` denies in slice 1 and is tested to do so.
- [ ] Cross-tenant detail returns 404; cross-tenant update returns 404 or 403 with no body detail.
- [ ] Controller exposes no `[HttpDelete]`, no `/export`, no `/archive`.
- [ ] All permission keys match `^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*){2,}$`.
- [ ] Project builds; existing tests still pass.

## Tests

Unit tests here; the full failure-path matrix is WP-05.

- Each handler denies when each port denies - one test per handler per port.
- `Create` sets `TenantId` from context even when the payload tries to supply one.
- `Create` returns 409 on in-tenant duplicate source.
- `Update` cannot mutate `LifecycleState`, `CaseNumber`, or `TenantId`.
- `Triage` rejects a missing reason code for `Rejected` / `Duplicate`.
- `Route` denies with `ContractUnavailable` in slice 1.
- Reflection: no type in the feature namespace matches `/Delete|Archive|Void|Export/`.
- Reflection: no handler type name ends in `CommandHandler` or `QueryHandler`.

## Verify

```bash
dotnet build services/Diten.PvgService/src/Diten.Pvg.API/Diten.Pvg.API.csproj -v q
dotnet test  services/Diten.PvgService/tests/Diten.Pvg.Application.Tests/Diten.Pvg.Application.Tests.csproj
grep -rn "HttpDelete\|BulkDelete\|/export\|/archive" services/Diten.PvgService/src   # expect no matches
```

## Agent prompt

> Implement WP-0230-04 in `/Users/natig/Projects/ERP-vNext-recovery`.
>
> Read first: `execution/domains/pharmacovigilance/work-packs/WP-0230-04-cqrs-api.md`, the full
> `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`,
> `docs/specs/pvg-reg-pv-base-port-contracts-v1.md`,
> `.antigravity/rules/{handler-design,api-conventions,response-envelope,permission-key-standard,pipeline-behaviors,multi-tenancy}.md`.
>
> Copy the CQRS shape from
> `services/Diten.DevEnablementService/src/.../Features/GoldenReferenceCompact/**` - but **do not copy** its
> `Delete`, `BulkDelete`, or `Export` members. Their absence is the enforcement.
>
> Implement exactly four commands (Create, Update, Triage, Route) and two queries (List, ById). Every mutating
> handler follows the 9-step order in the pack: correlation → tenant → authorize → field security → validate →
> workflow gate → evidence → persist → audit. Audit unavailable means the mutation fails.
>
> `Route` must deny in slice 1. Do not invent a queue list to make it pass. Write the test that proves it denies.
>
> Report build and test output, plus the output of the forbidden-surface grep.
