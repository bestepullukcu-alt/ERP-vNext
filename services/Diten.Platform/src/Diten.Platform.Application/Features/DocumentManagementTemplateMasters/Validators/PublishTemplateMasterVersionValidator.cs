using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Validators;

public sealed class PublishTemplateMasterVersionValidator : AbstractValidator<PublishTemplateMasterVersionCommand>
{
    public PublishTemplateMasterVersionValidator()
    {
        RuleFor(x => x.TemplateMasterId).NotEmpty();
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.File.FileName).NotEmpty().When(x => x.File is not null);
        RuleFor(x => x.File.ContentBase64).NotEmpty().When(x => x.File is not null);
        RuleFor(x => x.ChangeSummary).MaximumLength(1000).When(x => x.ChangeSummary is not null);
    }
}
