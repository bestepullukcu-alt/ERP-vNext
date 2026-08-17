using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

public sealed class GetBusinessReferenceDataSetByIdQueryValidator : AbstractValidator<GetBusinessReferenceDataSetByIdQuery>
{
    public GetBusinessReferenceDataSetByIdQueryValidator()
    {
        RuleFor(x => x.SetId).NotEmpty();
    }
}
