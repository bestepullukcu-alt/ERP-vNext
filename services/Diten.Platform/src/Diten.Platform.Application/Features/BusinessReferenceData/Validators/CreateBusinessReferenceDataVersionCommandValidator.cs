using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

public sealed class CreateBusinessReferenceDataVersionCommandValidator : AbstractValidator<CreateBusinessReferenceDataVersionCommand>
{
    public CreateBusinessReferenceDataVersionCommandValidator()
    {
        RuleFor(x => x.SetId).NotEmpty();
    }
}
