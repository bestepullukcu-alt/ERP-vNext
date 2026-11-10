using FluentValidation;
using Diten.SupplyChainService.Application.Features.SupplierPerformance.Queries;
namespace Diten.SupplyChainService.Application.Features.SupplierPerformance.Validators;
public sealed class GetSupplierScorecardListValidator : AbstractValidator<GetSupplierScorecardListQuery>
{
    public GetSupplierScorecardListValidator()
    {
        RuleFor(x => x.PeriodFrom).Must(x => x is null || SupplierPerformanceWire.Date(x));
        RuleFor(x => x.PeriodTo).Must(x => x is null || SupplierPerformanceWire.Date(x));
        RuleFor(x => x.RiskLevel).Must(x => x is null || SupplierPerformanceWire.RiskLevel(x));
    }
}
