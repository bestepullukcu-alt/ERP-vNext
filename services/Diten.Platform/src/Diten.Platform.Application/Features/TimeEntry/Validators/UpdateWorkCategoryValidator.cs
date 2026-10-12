using Diten.Platform.Application.Features.TimeEntry.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TimeEntry.Validators;

/// <summary>MOD-0280-FU01 §12 — the code is immutable; everything else follows the create rules.</summary>
public sealed class UpdateWorkCategoryValidator : AbstractValidator<UpdateWorkCategoryCommand>
{
    public UpdateWorkCategoryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request.ExpectedVersion).GreaterThan(0);
        WorkCategoryRules.AddCommonRules(this, x => x.Request.LabelText, x => x.Request.Description, x => x.Request.SortOrder);
    }
}
