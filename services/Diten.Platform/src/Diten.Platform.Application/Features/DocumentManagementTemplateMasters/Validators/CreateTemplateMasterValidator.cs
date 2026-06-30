using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Validators;

public sealed class CreateTemplateMasterValidator : AbstractValidator<CreateTemplateMasterCommand>
{
    public CreateTemplateMasterValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.MasterCode).NotEmpty().MaximumLength(80).When(x => x.Input is not null);
        RuleFor(x => x.Input.TemplateName).NotEmpty().MaximumLength(256).When(x => x.Input is not null);
        RuleFor(x => x.Input.Description).MaximumLength(2000).When(x => x.Input?.Description is not null);
        RuleFor(x => x.Input.Classification).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.CanonicalId).MaximumLength(200).When(x => x.Input?.CanonicalId is not null);
        RuleFor(x => x.Input.VariantPolicy).NotEmpty().When(x => x.Input is not null);
    }
}
