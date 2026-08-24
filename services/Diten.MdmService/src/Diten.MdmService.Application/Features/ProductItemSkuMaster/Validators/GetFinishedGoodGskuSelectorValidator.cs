using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;

public sealed class GetFinishedGoodGskuSelectorValidator : AbstractValidator<GetFinishedGoodGskuSelectorQuery>
{
    public GetFinishedGoodGskuSelectorValidator()
    {
        RuleFor(x => x.PageNumber).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(200);
    }
}
