using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;

public sealed class GetFinishedGoodByIdValidator : AbstractValidator<GetFinishedGoodByIdQuery>
{
    public GetFinishedGoodByIdValidator() => RuleFor(x => x.Id).NotEmpty();
}
