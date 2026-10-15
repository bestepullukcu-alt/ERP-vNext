using FluentValidation;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;

public sealed class EditManualDraftWeekCommandValidator
    : AbstractValidator<EditManualDraftWeekCommand>
{
    public EditManualDraftWeekCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.SelectedLegalEntityHint).NotEmpty();
        RuleFor(x => x.RevisionId).NotEmpty();
        RuleFor(x => x.SkuId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.WeekNumber).InclusiveBetween(1, 52);
        RuleFor(x => x.ExpectedContentVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
    }
}
