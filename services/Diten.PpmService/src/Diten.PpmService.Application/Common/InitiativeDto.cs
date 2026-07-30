using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Application.Common;

public sealed record InitiativeDto(Guid Id, string Code, string Name, string? Description, Guid? PortfolioId, InitiativeLifecycleState LifecycleState, string? VisibilityPolicyKey, bool IsReferenceable, int Version);
