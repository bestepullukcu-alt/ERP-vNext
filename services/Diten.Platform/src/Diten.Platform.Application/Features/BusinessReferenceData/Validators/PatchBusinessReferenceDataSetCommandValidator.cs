using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

public sealed class PatchBusinessReferenceDataSetCommandValidator : AbstractValidator<PatchBusinessReferenceDataSetCommand>
{
    public PatchBusinessReferenceDataSetCommandValidator()
    {
        RuleFor(x => x.SetId).NotEmpty();
        RuleFor(x => x.RowVersion).GreaterThan(0);
        RuleFor(x => x.Name).MaximumLength(256);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.SetCode).MaximumLength(64);
        RuleFor(x => x.ScopeType).MaximumLength(64);
    }
}
