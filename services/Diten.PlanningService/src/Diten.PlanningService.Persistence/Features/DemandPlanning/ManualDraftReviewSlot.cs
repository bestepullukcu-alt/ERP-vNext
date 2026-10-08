using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

// The unique cycle slot is held only while a revision is InReview or Approved.
public sealed class ManualDraftReviewSlot : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid PlanningCycleId { get; set; }
    public Guid RevisionId { get; set; }
}
