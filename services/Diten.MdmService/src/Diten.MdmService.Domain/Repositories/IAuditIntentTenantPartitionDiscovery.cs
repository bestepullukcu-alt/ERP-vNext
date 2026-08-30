namespace Diten.MdmService.Domain.Repositories;

public interface IAuditIntentTenantPartitionDiscovery
{
    Task<AuditIntentTenantPartitionPage> DiscoverAsync(
        Guid? afterTenantId,
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed record AuditIntentTenantPartitionPage(
    IReadOnlyList<Guid> TenantIds,
    Guid? NextTenantId);
