using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Validators;

public sealed class GetProductLegalEntityScopeCompletenessValidator
    : AbstractValidator<GetProductLegalEntityScopeCompletenessQuery>;
