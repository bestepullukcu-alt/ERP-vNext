using Diten.SafetyStockService.Application;
using Diten.SafetyStockService.Domain;

namespace Diten.SafetyStockService.Infrastructure;

public sealed class MdmOnlyLegalEntityScopeReader : IActorSeriesScopeReader
{
    // Temporary Ali decision: MDM validation precedes this call; no actor/company or series entitlement is inferred.
    public Task<ActorSeriesScope?> ResolveAsync(Guid tenantId, Guid actorId, Guid legalEntityId, CancellationToken cancellationToken)
        => Task.FromResult<ActorSeriesScope?>(new ActorSeriesScope(new HashSet<SeriesKey>(), AllSeriesInLegalEntity: true));
}
