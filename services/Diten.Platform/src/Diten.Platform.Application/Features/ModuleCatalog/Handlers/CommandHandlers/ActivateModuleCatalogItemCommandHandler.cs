using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Diten.Platform.Application.Features.GlobalApplicability;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class ActivateModuleCatalogItemCommandHandler : IRequestHandler<ActivateModuleCatalogItemCommand, Response<NoContent>>
{
    // A3-ref — system actor for the gate (this handler has no per-user context; the gate decision is driven by
    // the object's workflow state, not the actor). The actor id only needs to be a non-empty identifier.
    private const string GateActor = "system:module-catalog-activate";

    private readonly ITransactionalModuleCatalogRepository _repository;
    private readonly IWorkflowTransitionGate _transitionGate;
    private readonly IGlobalApplicabilityTransactionCoordinator _transaction;
    private readonly IGlobalApplicabilityStateRepository _state;

    public ActivateModuleCatalogItemCommandHandler(
        ITransactionalModuleCatalogRepository repository,
        IWorkflowTransitionGate transitionGate, IGlobalApplicabilityTransactionCoordinator transaction,
        IGlobalApplicabilityStateRepository state)
    {
        _repository = repository;
        _transitionGate = transitionGate;
        _transaction = transaction;
        _state = state;
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
        await _transitionGate.EnsureAllowedOrThrowAsync(
            new WorkflowGateRequest(
                ObjectType: "ModuleCatalogItem",
                ObjectId: item.Id.ToString(),
                ObjectRef: $"ModuleCatalogItem:{item.ModuleCode}",
                RequestedTransition: "Activate",
                RequestedTargetState: ModuleCatalogStatus.Active.ToString(),
                ActorId: GateActor,
                ReasonCode: null),
            ct);

        return await _transaction.ExecuteAsync<Response<NoContent>>(
            new(nameof(ActivateModuleCatalogItemCommand), AuditOperation.Activate, "ModuleCatalogItem", request.Id),
            async (session, transactionCt) =>
            {
                var current = await _repository.GetByIdAsync(session, request.Id, transactionCt);
                if (current is null) return new(Response<NoContent>.Fail("Module catalog item not found.", 404), false);
                if (current.Status is not (ModuleCatalogStatus.Draft or ModuleCatalogStatus.Inactive))
                    return new(Response<NoContent>.Fail($"Invalid status transition from {current.Status} to Active.", 400), false);
                current.Status = ModuleCatalogStatus.Active;
                await _repository.UpdateAsync(session, current, transactionCt);
                return new(Response<NoContent>.Success(204), true,
                    (s, version, token) => _state.UpsertModuleCatalogAsync(s, current, version, token));
            }, ct);
    }
}
