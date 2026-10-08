using FluentValidation;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;

public sealed class GetManualDraftQueryValidator : AbstractValidator<GetManualDraftQuery>
{
    public GetManualDraftQueryValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.SelectedLegalEntityHint).NotEmpty();
        RuleFor(x => x.RevisionId).NotEmpty();
    }
}
