using Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Validators;

public sealed class RebaseTemplateVariantValidator : AbstractValidator<RebaseTemplateVariantCommand>
{
    public RebaseTemplateVariantValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Input).NotNull();
    }
}
