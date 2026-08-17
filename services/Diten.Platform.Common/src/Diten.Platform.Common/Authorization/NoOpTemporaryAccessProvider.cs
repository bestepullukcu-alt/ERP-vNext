namespace Diten.Platform.Common.Authorization;

public sealed class NoOpTemporaryAccessProvider : ITemporaryAccessProvider
{
    public Task<IReadOnlyList<TemporaryAccessGrant>> GetActiveGrantsAsync(
        Guid tenantId,
        Guid userId,
        string moduleCode,
        string? featureCode,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<TemporaryAccessGrant>>(Array.Empty<TemporaryAccessGrant>());
    }
}
