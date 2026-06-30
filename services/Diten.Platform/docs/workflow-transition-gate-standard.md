# Workflow Transition Gate — Module Integration Standard (A3 / MOD-0023)

**Rule (mandatory):** Any state-transitioning business module MUST call the workflow transition gate
*before* committing a state change. If the gate returns **Blocked**, the module MUST NOT commit. This is
defence-in-depth — skipping the gate silently bypasses approvals.

## Why
The workflow engine (MOD-0023) tracks approval/evidence state per object. Permissions alone (`[HasPermission]`)
do not know whether an object's pending approval has completed. Without a gate call, a user with the action
permission could commit a transition that the workflow has not approved.

## How (in-process, same service)
Inject `IWorkflowTransitionGate` and check it before the commit:

```csharp
var result = await _gate.EvaluateAsync(new WorkflowGateRequest(
    ObjectType: "LegalEntity",
    ObjectId: id.ToString(),
    ObjectRef: $"LegalEntity:{id}",
    RequestedTransition: "Activate",
    RequestedTargetState: "Active",
    ActorId: _currentUser.UserId.ToString(),
    ReasonCode: null), ct);

if (result.IsBlocked)
{
    return Response<...>.Fail(result.BlockingMessage ?? "Blocked by workflow.", 409);
}
// ...only now commit the transition...
```

Or fail-fast: `await _gate.EnsureAllowedOrThrowAsync(request, ct);` (throws `WorkflowTransitionBlockedException`).

## How (cross-service)
A module in another service calls `POST /api/v1/workflow/transitions/evaluate`
(permission `platform.workflow.transitions.evaluate`) and applies the same rule to the response `Decision`.

## Semantics
- `Allowed` / `NotApplicable` (no workflow attached to the object) → **commit permitted**.
- `Blocked` → **do not commit**; surface `BlockingReasonCode` / `BlockingMessage`.
- **Fail-closed:** if the gate cannot be evaluated, `IWorkflowTransitionGate` returns Blocked. It is *not*
  best-effort — a gate outage must not let an unapproved transition through.

## Reference
- Contract: `Diten.Platform.Application/Contracts/IWorkflowTransitionGate.cs`
- Implementation: `Diten.Platform.Application/Services/WorkflowTransitionGate.cs`
- Underlying query: `EvaluateWorkflowTransitionGateQuery` / `EvaluateWorkflowTransitionGateHandler`
- **Wired reference example:** `ActivateModuleCatalogItemCommandHandler` (Draft/Inactive→Active) — an in-process
  Platform handler that calls `EnsureAllowedOrThrowAsync` before committing the status change. Copy this shape.
- Cross-service note: `LegalEntity` (MDM) activate would consume the gate via the HTTP
  `transitions/evaluate` endpoint, not this in-process contract — deferred.
