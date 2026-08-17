using Diten.Platform.Application.Features.ModulePages.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.ModulePages.Validators;

public sealed class UpdateModulePageDescriptorCommandValidator : ModulePageDescriptorRequestValidator<UpdateModulePageDescriptorCommand>
{
    public UpdateModulePageDescriptorCommandValidator()
        : base(
            x => x.Request.PageCode,
            x => x.Request.DisplayName,
            x => x.Request.RoutePath,
            x => x.Request.RequiredPermission,
            x => x.Request.ParentPageCode,
            x => x.Request.PageType,
            x => x.Request.Status,
            x => x.Request.SortOrder)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required.");
    }
}
