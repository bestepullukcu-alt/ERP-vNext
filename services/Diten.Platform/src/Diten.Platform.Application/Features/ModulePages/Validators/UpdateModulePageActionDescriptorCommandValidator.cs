using Diten.Platform.Application.Features.ModulePages.Commands;

namespace Diten.Platform.Application.Features.ModulePages.Validators;

public sealed class UpdateModulePageActionDescriptorCommandValidator
    : ModulePageActionDescriptorRequestValidator<UpdateModulePageActionDescriptorCommand>
{
    public UpdateModulePageActionDescriptorCommandValidator()
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
