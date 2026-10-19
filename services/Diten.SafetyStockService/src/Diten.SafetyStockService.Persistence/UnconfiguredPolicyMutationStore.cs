using Diten.SafetyStockService.Application;

namespace Diten.SafetyStockService.Persistence;

public sealed class UnconfiguredPolicyMutationStore : IPolicyMutationStore
{
    public Task<PolicyMutationResult> ExecuteAsync(PolicyMutationCommand command, ActorSeriesScope scope,
        CancellationToken cancellationToken)
        => Task.FromResult(new PolicyMutationResult(PolicyMutationStatus.Unavailable));
}
