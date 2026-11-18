namespace Diten.PlanningService.Application.Features.DemandPlanning;

// The delivery worker must verify persistent Mongo indexes before publishing to a broker.
public interface IDemandRevisionOutboxPreflight
{
    Task VerifyAsync(CancellationToken cancellationToken = default);
}
