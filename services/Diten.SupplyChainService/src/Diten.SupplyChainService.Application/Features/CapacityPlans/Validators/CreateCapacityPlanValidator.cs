using FluentValidation;
using Diten.SupplyChainService.Application.Features.CapacityPlans.Commands;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans.Validators;
public sealed class CreateCapacityPlanValidator : AbstractValidator<CreateCapacityPlanCommand>
{
    public CreateCapacityPlanValidator()
    {
        RuleFor(x=>x.Body.Name).NotNull().Must(x=>x is { Length: >0 });
        RuleFor(x=>x.Body.DemandPlanId).NotNull().Must(x=>x is { Length: >0 });
        RuleFor(x=>x.Body.DemandPlanVersion).NotNull().Must(x=>x is { Length: >0 });
        RuleFor(x=>x.Body.SourceChecksum).NotNull().Must(x=>x is { Length: >0 });
        RuleFor(x=>x.Body).Must(x=>x.HorizonStart!=default && x.HorizonEnd>=x.HorizonStart);
        RuleFor(x=>x.Body.SourceCapturedAt).NotEqual(default(DateTimeOffset));
    }
}
