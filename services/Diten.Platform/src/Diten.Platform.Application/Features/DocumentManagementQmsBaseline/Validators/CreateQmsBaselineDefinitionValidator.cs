using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Validators;

public sealed class CreateQmsBaselineDefinitionValidator : AbstractValidator<CreateQmsBaselineDefinitionCommand>
{
    public CreateQmsBaselineDefinitionValidator()
    {
        RuleFor(x => x.BaselineReleaseId).NotEmpty();
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.ParentCanonicalId).MaximumLength(160);
        RuleFor(x => x.Request.PurposeScope).MaximumLength(2000);
        RuleFor(x => x.Request.RequiredByScope).MaximumLength(256);
        RuleFor(x => x.Request.AllowedDocClass).MaximumLength(256);
        RuleFor(x => x.Request.DefaultClassificationLevel).MaximumLength(128);
        RuleFor(x => x.Request.DefaultRetentionHint).MaximumLength(512);
        RuleFor(x => x.Request.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}
