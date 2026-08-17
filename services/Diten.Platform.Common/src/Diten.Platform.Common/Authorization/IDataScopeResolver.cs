namespace Diten.Platform.Common.Authorization;

public interface IDataScopeResolver
{
    Task<IReadOnlyList<EntitlementDataScope>> ResolveAsync(
        Guid tenantId,
        Guid userId,
        string moduleCode,
        string? featureCode,
        CancellationToken cancellationToken);
}
