using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Application.Common;

public sealed record ProgramDto(Guid Id, string Code, string Name, string? Description, Guid? PortfolioId, ProgramLifecycleState LifecycleState, string? VisibilityPolicyKey, bool IsReferenceable, int Version);
