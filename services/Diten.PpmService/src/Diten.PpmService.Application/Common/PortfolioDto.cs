using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Application.Common;

public sealed record PortfolioDto(Guid Id, string Code, string Name, string? Description, PortfolioLifecycleState LifecycleState, string? VisibilityPolicyKey, bool IsReferenceable, int Version);
