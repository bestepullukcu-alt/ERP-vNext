using Diten.SafetyStockService.Application;
using Diten.SafetyStockService.Domain;

namespace Diten.SafetyStockService.Persistence;

public sealed class UnconfiguredPolicyReadStore : IPolicyReadStore
{
    private static InvalidOperationException Unavailable() => new("The authoritative policy read store is not configured.");

    public Task<IReadOnlyList<PolicyReadModel>> ListAsync(Guid tenantId, Guid legalEntityId, ActorSeriesScope scope, CancellationToken cancellationToken)
        => throw Unavailable();

    public Task<PolicyReadModel?> GetPolicyAsync(Guid tenantId, Guid legalEntityId, Guid policyId, ActorSeriesScope scope, CancellationToken cancellationToken)
        => throw Unavailable();

    public Task<PolicyRevisionReadModel?> GetRevisionAsync(Guid tenantId, Guid legalEntityId, Guid policyId, Guid revisionId, ActorSeriesScope scope, CancellationToken cancellationToken)
        => throw Unavailable();

    public Task<PolicyEvaluationReadModel?> GetEvaluationAsync(Guid tenantId, Guid legalEntityId, Guid policyId, Guid evaluationId, ActorSeriesScope scope, CancellationToken cancellationToken)
        => throw Unavailable();
}
