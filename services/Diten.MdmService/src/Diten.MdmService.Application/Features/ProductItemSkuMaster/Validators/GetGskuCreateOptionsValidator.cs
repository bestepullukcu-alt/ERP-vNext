using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;

public sealed class GetGskuMutationOptionsValidator : AbstractValidator<GetGskuMutationOptionsQuery>
{
    public GetGskuMutationOptionsValidator()
    {
        RuleFor(x => x.GskuId).NotEmpty();
        RuleFor(x => x.Operation).IsInEnum();
    }
}

public sealed class GetGskuCreateOptionsValidator : AbstractValidator<GetGskuCreateOptionsQuery>
{
    public GetGskuCreateOptionsValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}
