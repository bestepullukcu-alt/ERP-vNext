using FluentValidation;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;

public sealed class CreateManualDraftCommandValidator
    : AbstractValidator<CreateManualDraftCommand>
{
    public CreateManualDraftCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.SelectedLegalEntityHint).NotEmpty();
        RuleFor(x => x.PlanningCycleId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Series).NotNull().NotEmpty();
    }
}
