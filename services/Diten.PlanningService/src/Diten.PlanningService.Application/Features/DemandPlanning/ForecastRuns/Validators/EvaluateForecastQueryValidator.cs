using FluentValidation;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ForecastRuns;

public sealed class EvaluateForecastQueryValidator : AbstractValidator<EvaluateForecastQuery>
{
    public EvaluateForecastQueryValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.SelectedLegalEntityHint).NotEmpty();
        RuleFor(x => x.PlanningCycleId).NotEmpty();
        RuleFor(x => x.SkuId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
    }
}