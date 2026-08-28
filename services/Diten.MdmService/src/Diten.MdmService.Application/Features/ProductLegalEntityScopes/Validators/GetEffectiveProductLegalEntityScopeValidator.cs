using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Validators;

public sealed class GetEffectiveProductLegalEntityScopeValidator
    : AbstractValidator<GetEffectiveProductLegalEntityScopeQuery>
{
    public GetEffectiveProductLegalEntityScopeValidator() => RuleFor(x => x.GlobalProductId).NotEmpty();
}
