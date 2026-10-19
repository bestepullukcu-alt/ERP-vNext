using Diten.PlanningService.Application.Features.DemandPlanning;

namespace Diten.PlanningService.Infrastructure.Features.DemandPlanning;

// Live Platform/MDM assignments are not wired. Production remains closed.
public sealed class UnconfiguredPublishAuthority : IInternalPublishAuthority
{
    public Task<PublishAuthorityEvidence> VerifyAsync(Guid tenantId, Guid legalEntityId,
        Guid actorId, IReadOnlyList<(Guid SkuId, string WarehouseId)> selectedScope,
        CancellationToken cancellationToken) =>
        Task.FromResult(new PublishAuthorityEvidence(false, false, false, false));
}
