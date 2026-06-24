using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Validators;

public sealed class ValidateQmsBaselineDraftValidator : AbstractValidator<ValidateQmsBaselineDraftCommand>
{
    public ValidateQmsBaselineDraftValidator()
    {
        RuleFor(x => x.BaselineReleaseId).NotEmpty();
    }
}
