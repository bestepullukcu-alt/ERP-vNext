using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Validators;

public sealed class DeprecateTemplateMasterValidator : AbstractValidator<DeprecateTemplateMasterCommand>
{
    public DeprecateTemplateMasterValidator()
    {
        RuleFor(x => x.TemplateMasterId).NotEmpty();
        RuleFor(x => x.Input.DeprecationReason).MaximumLength(1000).When(x => x.Input?.DeprecationReason is not null);
    }
}
