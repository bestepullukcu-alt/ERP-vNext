using Diten.Platform.Application.Features.ModulePages.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.ModulePages.Validators;

public sealed class CreateModulePageDescriptorCommandValidator : ModulePageDescriptorRequestValidator<CreateModulePageDescriptorCommand>
{
    public CreateModulePageDescriptorCommandValidator()
        : base(
            x => x.Request.PageCode,
            x => x.Request.DisplayName,
            x => x.Request.RoutePath,
            x => x.Request.RequiredPermission,
            x => x.Request.PageType,
            x => x.Request.Status,
            x => x.Request.SortOrder)
    {
        RuleFor(x => x.Request.ModuleCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("ModuleCode is required.")
            .Must(value => ModulePageDescriptorNormalizer.NormalizeModuleCode(value).Length is >= 3 and <= 100)
            .WithMessage("ModuleCode must be between 3 and 100 characters after canonical normalization.");
    }
}
