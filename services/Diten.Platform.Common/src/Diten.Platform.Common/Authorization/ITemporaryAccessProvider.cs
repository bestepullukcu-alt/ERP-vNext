namespace Diten.Platform.Common.Authorization;

public interface ITemporaryAccessProvider
{
    Task<IReadOnlyList<TemporaryAccessGrant>> GetActiveGrantsAsync(
        Guid tenantId,
        Guid userId,
        string moduleCode,
        string? featureCode,
        CancellationToken cancellationToken);
}
