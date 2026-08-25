using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

public sealed class ResolveVerifiedMarketReferenceDataValidator : AbstractValidator<ResolveVerifiedMarketReferenceDataQuery>
{
    // The handler deliberately maps grammar failures to the locked non-enumerating 404 contract.
    // FluentValidation's generic 400 envelope must not replace that provider-facing behavior.
    public ResolveVerifiedMarketReferenceDataValidator() { }
}
