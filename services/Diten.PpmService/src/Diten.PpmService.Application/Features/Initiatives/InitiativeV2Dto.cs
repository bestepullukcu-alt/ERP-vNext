using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record InitiativeV2Dto(Guid Id, string Code, string Name, string? Description, Guid? PortfolioId,
    string? InitiativeTypeCode, string? PriorityCode, DateOnly? PlannedStartDate, DateOnly? PlannedEndDate,
    InitiativeLifecycleState LifecycleState, Guid? SupersedesInitiativeId, bool IsReferenceable, int Version,
    IReadOnlyList<InitiativeActionAvailability> AvailableActions);
