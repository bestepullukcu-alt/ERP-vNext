using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

public sealed class GetBusinessReferenceDataValuesQueryValidator : AbstractValidator<GetBusinessReferenceDataValuesQuery>
{
    public GetBusinessReferenceDataValuesQueryValidator()
    {
        RuleFor(x => x.SetCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ScopeKey).MaximumLength(128);
        RuleFor(x => x.VersionNumber).GreaterThan(0).When(x => x.VersionNumber.HasValue);
    }
}
