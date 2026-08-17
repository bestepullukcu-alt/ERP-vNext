using Diten.Platform.Application.Features.ModulePages.Commands;

namespace Diten.Platform.Application.Features.ModulePages.Validators;

public sealed class CreateModulePageActionDescriptorCommandValidator
    : ModulePageActionDescriptorRequestValidator<CreateModulePageActionDescriptorCommand>
{
    public CreateModulePageActionDescriptorCommandValidator()
        : base(
            x => x.Request.ActionCode,
            x => x.Request.DisplayName,
            x => x.Request.PermissionKey,
            x => x.Request.ActionType,
            x => x.Request.Status,
            x => x.Request.SortOrder)
    {
    }
}
