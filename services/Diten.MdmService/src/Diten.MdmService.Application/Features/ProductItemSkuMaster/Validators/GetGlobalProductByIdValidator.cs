using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;

public sealed class GetGlobalProductByIdValidator : AbstractValidator<GetGlobalProductByIdQuery>
{
    public GetGlobalProductByIdValidator()
        => RuleFor(x => x.Id).NotEmpty();
}
