using Diten.PlanningService.Domain.Features.DemandPlanning;
using FluentValidation;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;

public sealed class TransitionManualDraftCommandValidator
    : AbstractValidator<TransitionManualDraftCommand>
{
    public TransitionManualDraftCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.SelectedLegalEntityHint).NotEmpty();
        RuleFor(x => x.RevisionId).NotEmpty();
        RuleFor(x => x.Action).Must(x => x is DraftReviewAction.Submitted or
            DraftReviewAction.Approved or DraftReviewAction.Rejected or
            DraftReviewAction.Reopened);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000)
            .When(x => x.Action is DraftReviewAction.Approved or
                DraftReviewAction.Rejected or DraftReviewAction.Reopened);
        RuleFor(x => x.ExpectedContentVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExpectedStateVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
