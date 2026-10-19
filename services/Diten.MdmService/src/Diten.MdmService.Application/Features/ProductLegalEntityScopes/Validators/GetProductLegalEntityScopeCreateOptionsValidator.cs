using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Validators;

public sealed class GetProductLegalEntityScopeCreateOptionsValidator
    : AbstractValidator<GetProductLegalEntityScopeCreateOptionsQuery>
{
    public GetProductLegalEntityScopeCreateOptionsValidator() => RuleFor(x => x.GlobalProductId).NotEmpty();
}
