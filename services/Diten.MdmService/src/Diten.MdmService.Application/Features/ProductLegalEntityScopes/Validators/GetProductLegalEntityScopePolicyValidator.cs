using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Validators;

public sealed class GetProductLegalEntityScopePolicyValidator
    : AbstractValidator<GetProductLegalEntityScopePolicyQuery>
{
    public GetProductLegalEntityScopePolicyValidator() => RuleFor(x => x.GlobalProductId).NotEmpty();
}
