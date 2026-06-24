using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Validators;

public sealed class CreateManualQmsBaselineValidator : AbstractValidator<CreateManualQmsBaselineCommand>
{
    public CreateManualQmsBaselineValidator()
    {
        RuleFor(x => x.Request.BaselineVersion).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Request.Name).MaximumLength(200);
        RuleFor(x => x.Request.ChangeSummary).MaximumLength(2000);
    }
}
