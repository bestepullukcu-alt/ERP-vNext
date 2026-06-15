using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

public sealed class CreateBusinessReferenceDataSetCommandValidator : AbstractValidator<CreateBusinessReferenceDataSetCommand>
{
    public CreateBusinessReferenceDataSetCommandValidator()
    {
        RuleFor(x => x.SetCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.ScopeType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}
