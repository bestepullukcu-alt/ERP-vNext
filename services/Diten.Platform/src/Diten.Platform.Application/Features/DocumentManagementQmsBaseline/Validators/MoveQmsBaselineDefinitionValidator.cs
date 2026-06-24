using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Validators;

public sealed class MoveQmsBaselineDefinitionValidator : AbstractValidator<MoveQmsBaselineDefinitionCommand>
{
    public MoveQmsBaselineDefinitionValidator()
    {
        RuleFor(x => x.BaselineReleaseId).NotEmpty();
        RuleFor(x => x.CanonicalId).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Request.ParentCanonicalId).MaximumLength(160);
        RuleFor(x => x.Request.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.VersionToken).GreaterThanOrEqualTo(0);
    }
}
