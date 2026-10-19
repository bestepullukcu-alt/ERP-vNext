using FluentValidation;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ForecastRuns;

public sealed class CreateForecastRunCommandValidator : AbstractValidator<CreateForecastRunCommand>
{
    public CreateForecastRunCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.SelectedLegalEntityHint).NotEmpty();
        RuleFor(x => x.PlanningCycleId).NotEmpty();
        RuleFor(x => x.SkuId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Method).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2_000);
        RuleFor(x => x.ExpectedCycleVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}