using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;

// MOD-0290-FU05 (T-1): the selector's limits (GetGlobalProductSelectorValidator).
public sealed class GetGlobalProductBrandOptionsValidator : AbstractValidator<GetGlobalProductBrandOptionsQuery>
{
    public GetGlobalProductBrandOptionsValidator()
    {
        RuleFor(x => x.PageNumber).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(200);
    }
}
