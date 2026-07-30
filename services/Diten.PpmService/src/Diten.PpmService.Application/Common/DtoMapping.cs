using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Application.Common;

internal static class DtoMapping
{
    internal static PortfolioDto ToDto(this Portfolio x) => new(x.Id, x.Code, x.Name, x.Description, x.LifecycleState, x.VisibilityPolicyKey, x.IsReferenceable, x.Version);
    internal static InitiativeDto ToDto(this Initiative x) => new(x.Id, x.Code, x.Name, x.Description, x.PortfolioId, x.LifecycleState, x.VisibilityPolicyKey, x.IsReferenceable, x.Version);
    internal static ProgramDto ToDto(this Program x) => new(x.Id, x.Code, x.Name, x.Description, x.PortfolioId, x.LifecycleState, x.VisibilityPolicyKey, x.IsReferenceable, x.Version);
    internal static ProjectDto ToDto(this Project x) => new(x.Id, x.Code, x.Name, x.Description, x.ParentType, x.ParentId, x.LifecycleState, x.VisibilityPolicyKey, x.IsReferenceable, x.Version);
}
