namespace Diten.Platform.Common.Authorization;

public interface IEntitlementChecker
{
    Task<EntitlementCheckResult> IsModuleEntitledAsync(
        Guid tenantId,
        string moduleCode,
        CancellationToken ct);

    Task<EntitlementCheckResult> IsFeatureEnabledAsync(
        Guid tenantId,
        string featureCode,
        CancellationToken ct);

    Task<IReadOnlyList<EntitlementCheckResult>> CheckBatchAsync(
        Guid tenantId,
        IEnumerable<(string code, EntitlementKind kind)> targets,
        CancellationToken ct);
}
