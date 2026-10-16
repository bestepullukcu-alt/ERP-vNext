using FluentValidation;

namespace Diten.PlanningService.Application.Features.DemandPlanning.Cycles;

public sealed class CreatePlanningCycleCommandValidator : AbstractValidator<CreatePlanningCycleCommand>
{
    public CreatePlanningCycleCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.SelectedLegalEntityHint).NotEmpty();
        RuleFor(x => x.AsOfDate).NotEqual(default(DateOnly));
        RuleFor(x => x.FirstWeekStart).NotEqual(default(DateOnly));
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
