using Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Validators;

public sealed class CreateTemplateVariantValidator : AbstractValidator<CreateTemplateVariantCommand>
{
    public CreateTemplateVariantValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.TemplateMasterId).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.TemplateMasterVersionId).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.VariantCode).NotEmpty().MaximumLength(80).When(x => x.Input is not null);
        RuleFor(x => x.Input.VariantName).NotEmpty().MaximumLength(256).When(x => x.Input is not null);
        RuleFor(x => x.Input.Description).MaximumLength(2000).When(x => x.Input?.Description is not null);
        RuleFor(x => x.Input.ScopeType).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.ScopeId).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.TargetCollectionInstanceId).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.ContentSource).NotEmpty().When(x => x.Input is not null);
    }
}
