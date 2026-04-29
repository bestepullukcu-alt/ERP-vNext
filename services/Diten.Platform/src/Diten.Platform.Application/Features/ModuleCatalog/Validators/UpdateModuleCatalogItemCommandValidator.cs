using Diten.Platform.Application.Features.ModuleCatalog.Commands;

namespace Diten.Platform.Application.Features.ModuleCatalog.Validators;

public sealed class UpdateModuleCatalogItemCommandValidator : ModuleCatalogItemRequestValidator<UpdateModuleCatalogItemCommand>
{
    public UpdateModuleCatalogItemCommandValidator()
        : base(
            x => x.Request.ModuleCode,
            x => x.Request.ModuleName,
            x => x.Request.DisplayName,
            x => x.Request.Domain,
            x => x.Request.Service,
            x => x.Request.Status,
            x => x.Request.ModuleVersion,
            x => x.Request.SortOrder)
    {
    }
}
