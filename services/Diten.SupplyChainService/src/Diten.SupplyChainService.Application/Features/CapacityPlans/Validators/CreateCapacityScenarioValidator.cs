using FluentValidation;
using Diten.SupplyChainService.Application.Features.CapacityPlans.Commands;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans.Validators;
public sealed class CreateCapacityScenarioValidator : AbstractValidator<CreateCapacityScenarioCommand>
{
    public CreateCapacityScenarioValidator()
    {
        RuleFor(x=>x.CapacityPlanId).NotEmpty();
        RuleFor(x=>x.Body.Name).NotNull().Must(x=>x is { Length: >0 });
        RuleFor(x=>x.Body.ConstraintRefs).NotNull();
        RuleFor(x=>x.Body.Adjustments).NotNull();
    }
}
