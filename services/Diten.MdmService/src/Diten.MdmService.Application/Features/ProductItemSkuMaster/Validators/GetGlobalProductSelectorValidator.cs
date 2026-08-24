using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;

public sealed class GetGlobalProductSelectorValidator : AbstractValidator<GetGlobalProductSelectorQuery>
{
    public GetGlobalProductSelectorValidator()
    {
        RuleFor(x => x.PageNumber).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(200);
    }
}
