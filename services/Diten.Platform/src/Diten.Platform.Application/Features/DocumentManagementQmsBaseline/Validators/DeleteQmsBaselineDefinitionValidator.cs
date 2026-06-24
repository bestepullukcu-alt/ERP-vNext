using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Validators;

public sealed class DeleteQmsBaselineDefinitionValidator : AbstractValidator<DeleteQmsBaselineDefinitionCommand>
{
    public DeleteQmsBaselineDefinitionValidator()
    {
        RuleFor(x => x.BaselineReleaseId).NotEmpty();
        RuleFor(x => x.CanonicalId).NotEmpty().MaximumLength(160);
        RuleFor(x => x.VersionToken).GreaterThanOrEqualTo(0);
    }
}
