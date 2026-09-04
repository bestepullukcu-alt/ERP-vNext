using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Application.Features.Initiatives;

internal static class InitiativeMapping
{
    internal static InitiativeV2Dto ToV2Dto(this Initiative x,
        IReadOnlyList<InitiativeActionAvailability> availableActions) => new(x.Id, x.Code, x.Name, x.Description,
        x.PortfolioId, x.InitiativeTypeCode, x.PriorityCode, x.PlannedStartDate, x.PlannedEndDate,
        x.LifecycleState, x.SupersedesInitiativeId, x.IsReferenceable, x.Version, availableActions);

    internal static InitiativeClosureDto ToDto(this InitiativeClosure x) => new(x.Id, x.InitiativeId,
        x.OutcomeCode, x.ClosureReasonCode, x.CompletedAt, x.CompletionSummary, x.EvidenceReferences,
        x.FollowUpTaskReferences, x.BenefitDisposition);
}
