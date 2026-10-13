using FluentValidation;
using Diten.SupplyChainService.Application.Features.CapacityPlans.Commands;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans.Validators;
public sealed class EvaluateCapacityScenarioValidator : AbstractValidator<EvaluateCapacityScenarioCommand>
{
    public EvaluateCapacityScenarioValidator()
    {
        RuleFor(x=>x.CapacityPlanId).NotEmpty();
        RuleFor(x=>x.ScenarioId).NotEmpty();
        RuleFor(x=>x.Body.EvaluationMode).Must(x=>x is "Finite" or "Infinite");
        RuleFor(x=>x.Body.ResourceRefs).NotNull().Must(x=>x is { Count: >0 } && x.All(y=>y.Length>0));
    }
}
