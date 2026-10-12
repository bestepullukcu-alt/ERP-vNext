using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

public sealed class GetConsumableBusinessReferenceDataPublishedValuesQueryValidator
    : AbstractValidator<GetConsumableBusinessReferenceDataPublishedValuesQuery>
{
    public GetConsumableBusinessReferenceDataPublishedValuesQueryValidator()
    {
        RuleFor(x => x.SetCode).NotEmpty().MaximumLength(64);
    }
}
