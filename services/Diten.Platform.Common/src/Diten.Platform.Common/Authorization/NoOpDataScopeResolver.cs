namespace Diten.Platform.Common.Authorization;

public sealed class NoOpDataScopeResolver : IDataScopeResolver
{
    public Task<IReadOnlyList<EntitlementDataScope>> ResolveAsync(
        Guid tenantId,
        Guid userId,
        string moduleCode,
        string? featureCode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<EntitlementDataScope>>(Array.Empty<EntitlementDataScope>());
    }
}
