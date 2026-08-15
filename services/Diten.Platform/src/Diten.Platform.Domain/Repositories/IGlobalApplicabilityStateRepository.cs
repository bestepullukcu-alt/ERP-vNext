using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public interface IGlobalApplicabilityStateRepository
{
    Task UpsertSubscriptionPlanAsync(
        IPlatformTransactionSession session,
        SubscriptionPlan plan,
        ulong globalVersion,
        CancellationToken cancellationToken = default);

    Task UpsertModuleCatalogAsync(
        IPlatformTransactionSession session,
        ModuleCatalogItem module,
        ulong globalVersion,
        CancellationToken cancellationToken = default);
}
