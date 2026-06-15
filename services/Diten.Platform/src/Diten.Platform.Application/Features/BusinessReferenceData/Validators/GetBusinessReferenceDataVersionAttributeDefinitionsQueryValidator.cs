using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

public sealed class GetBusinessReferenceDataVersionAttributeDefinitionsQueryValidator : AbstractValidator<GetBusinessReferenceDataVersionAttributeDefinitionsQuery>
{
    public GetBusinessReferenceDataVersionAttributeDefinitionsQueryValidator()
    {
        RuleFor(x => x.VersionId).NotEmpty();
    }
}
