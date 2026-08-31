namespace Diten.Platform.Domain.Repositories;

public interface IEntitlementStateVersionRepository
{
    Task<ulong> IncrementPhysicalEntitlementVersionAsync(
        IPlatformTransactionSession session,
        Guid tenantId,
        string moduleCode,
        CancellationToken cancellationToken = default);

    Task<ulong> IncrementSubscriptionSelectionVersionAsync(
        IPlatformTransactionSession session,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<ulong> IncrementGlobalApplicabilityVersionAsync(
        IPlatformTransactionSession session,
        CancellationToken cancellationToken = default);
}
