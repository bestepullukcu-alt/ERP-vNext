namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;

public sealed class PerformanceImprovementCoordinator(PerformanceImprovementContract.IMutationStore store)
{
    public Task<PerformanceImprovementContract.MutationResult> ExecuteAsync(
        PerformanceImprovementContract.MutationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.TenantId == Guid.Empty || request.ActorId == Guid.Empty || request.AggregateId == Guid.Empty ||
            request.OccurredAtUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(request.IdempotencyKey) ||
            request.CanonicalPayloadHash.Length != 64 || request.CanonicalPayloadHash.AsSpan().ContainsAnyExcept("0123456789abcdef"))
            return Task.FromResult(PerformanceImprovementContract.MutationResult.Invalid(request.AggregateId));

        var required = PerformanceImprovementPermissions.RequiredFor(request.CommandFamily);
        if (!string.Equals(required, request.Permission, StringComparison.Ordinal))
            return Task.FromResult(new PerformanceImprovementContract.MutationResult(false, 403, "performance_improvement_permission_denied", request.AggregateId));

        return store.ExecuteAsync(request, cancellationToken);
    }
}
