using FluentValidation;
using Diten.SupplyChainService.Application.Features.SupplierPerformance.Queries;
namespace Diten.SupplyChainService.Application.Features.SupplierPerformance.Validators;
public sealed class GetSupplierEvaluationListValidator : AbstractValidator<GetSupplierEvaluationListQuery>
{
    public GetSupplierEvaluationListValidator()
    {
        RuleFor(x => x.Status).Must(x => x is null || SupplierPerformanceWire.Status(x));
        RuleFor(x => x.PeriodFrom).Must(x => x is null || SupplierPerformanceWire.Date(x));
        RuleFor(x => x.PeriodTo).Must(x => x is null || SupplierPerformanceWire.Date(x));
        RuleFor(x => x.Page).Must(x => x is null || SupplierPerformanceWire.PositiveInteger(x, int.MaxValue, out _));
        RuleFor(x => x.PageSize).Must(x => x is null || SupplierPerformanceWire.PositiveInteger(x, SupplierPerformanceWire.MaxPageSize, out _));
    }
}
