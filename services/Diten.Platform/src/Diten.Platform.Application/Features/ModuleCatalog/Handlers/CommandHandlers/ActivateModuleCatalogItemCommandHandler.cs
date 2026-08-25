using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class ActivateModuleCatalogItemCommandHandler : IRequestHandler<ActivateModuleCatalogItemCommand, Response<NoContent>>
{
    // A3-ref — system actor for the gate (this handler has no per-user context; the gate decision is driven by
    // the object's workflow state, not the actor). The actor id only needs to be a non-empty identifier.
    private const string GateActor = "system:module-catalog-activate";

    private readonly IModuleCatalogRepository _repository;
    private readonly IWorkflowTransitionGate _transitionGate;

    public ActivateModuleCatalogItemCommandHandler(
        IModuleCatalogRepository repository,
        IWorkflowTransitionGate transitionGate)
    {
        _repository = repository;
        _transitionGate = transitionGate;
    }

    public async Task<Response<NoContent>> Handle(ActivateModuleCatalogItemCommand request, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(request.Id, ct);
        if (item is null)
        {
            return Response<NoContent>.Fail("Module catalog item not found.", 404);
        }

        if (item.Status is not (ModuleCatalogStatus.Draft or ModuleCatalogStatus.Inactive))
        {
            return Response<NoContent>.Fail($"Invalid status transition from {item.Status} to Active.", 400);
        }

        // A3-ref — reference consumer of the workflow transition gate. Call the gate BEFORE committing the
        // state change. No workflow bound to this object → NotApplicable → Allowed (behaviour unchanged).
        // Blocked (or a failed evaluation — fail-closed) → WorkflowTransitionBlockedException → activation is
        // aborted and the item stays in its current state (no commit). This is the template other state-
        // transitioning modules follow (see docs/workflow-transition-gate-standard.md).
        try
        {
            await _transitionGate.EnsureAllowedOrThrowAsync(
                BuildGateRequest(item),
                ct);
        }
        catch (WorkflowTransitionBlockedException ex)
        {
            return Response<NoContent>.Fail(
                ex.Result.BlockingMessage ?? "Workflow gate blocked module activation.",
                ex.Result.StatusCode,
                ex.Result.BlockingReasonCode,
                ex.Result.CorrelationId);
        }

        item.Status = ModuleCatalogStatus.Active;
        await _repository.UpdateAsync(item, ct);
        return Response<NoContent>.Success(204);
    }

    private static WorkflowGateRequest BuildGateRequest(Diten.Platform.Domain.Entities.ModuleCatalogItem item)
    {
        var binding = item.WorkflowBinding;
        return new WorkflowGateRequest(
            ObjectType: "ModuleCatalogItem",
            ObjectId: item.Id.ToString(),
            ObjectRef: $"ModuleCatalogItem:{item.ModuleCode}",
            RequestedTransition: "Activate",
            RequestedTargetState: ModuleCatalogStatus.Active.ToString(),
            ActorId: GateActor,
            ReasonCode: null,
            CorrelationId: binding?.CorrelationId,
            TargetScope: binding is null ? null : WorkflowTransitionGateTargetScope.Tenant,
            TargetTenantId: binding?.TargetTenantId,
            RequiresWorkflowGate: binding?.RequiresWorkflowGate,
            TargetTenantSource: binding?.TargetTenantSource);
    }
}
