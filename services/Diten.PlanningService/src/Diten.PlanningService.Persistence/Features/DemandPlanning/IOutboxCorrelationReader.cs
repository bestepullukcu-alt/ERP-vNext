namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

public interface IOutboxCorrelationReader
{
    Guid? CurrentId { get; }
}
