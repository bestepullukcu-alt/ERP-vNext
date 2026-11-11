using MediatR;

namespace Diten.SafetyStockService.Application;

public sealed class PolicyMutationHandler : IRequestHandler<PolicyMutationCommand, PolicyMutationResult>
{
    private readonly ILegalEntityReferenceValidator _reference;
    private readonly IActorSeriesScopeReader _scope;
    private readonly IPolicyMutationStore _store;

    public PolicyMutationHandler(ILegalEntityReferenceValidator reference, IActorSeriesScopeReader scope,
        IPolicyMutationStore store)
    {
        _reference = reference;
        _scope = scope;
        _store = store;
    }

    public async Task<PolicyMutationResult> Handle(PolicyMutationCommand command, CancellationToken cancellationToken)
    {
        if (command.TenantId == Guid.Empty || command.LegalEntityId == Guid.Empty || command.ActorId == Guid.Empty
            || string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 128)
            return new(PolicyMutationStatus.Invalid);

        ReferenceCheck reference;
        ActorSeriesScope? scope;
        try
        {
            reference = await _reference.ValidateAsync(command.LegalEntityId, cancellationToken);
            if (reference != ReferenceCheck.Valid)
                return new(reference == ReferenceCheck.NotFound ? PolicyMutationStatus.NotFound : PolicyMutationStatus.Unavailable);
            scope = await _scope.ResolveAsync(command.TenantId, command.ActorId, command.LegalEntityId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new(PolicyMutationStatus.Unavailable);
        }

        if (scope is null) return new(PolicyMutationStatus.Unavailable);
        if (command.Operation == PolicyOperation.Create && !scope.AllSeriesInLegalEntity
            && !scope.Contains(new(command.SkuId, command.WarehouseId)))
            return new(PolicyMutationStatus.NotFound);

        try
        {
            return await _store.ExecuteAsync(command, scope, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new(PolicyMutationStatus.Unavailable);
        }
    }
}
