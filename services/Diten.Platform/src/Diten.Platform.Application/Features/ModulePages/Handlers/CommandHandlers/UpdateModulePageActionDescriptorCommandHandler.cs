using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Application.Features.ModulePages.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Handlers.CommandHandlers;

public sealed class UpdateModulePageActionDescriptorCommandHandler
    : IRequestHandler<UpdateModulePageActionDescriptorCommand, Response<NoContent>>
{
    private readonly IModulePageActionDescriptorRepository _repository;
    private readonly IModuleCatalogRepository _catalogRepository;
    private readonly ICatalogPermissionSyncService _catalogPermissionSyncService;

    public UpdateModulePageActionDescriptorCommandHandler(
        IModulePageActionDescriptorRepository repository,
        IModuleCatalogRepository catalogRepository,
        ICatalogPermissionSyncService catalogPermissionSyncService)
    {
        _repository = repository;
        _catalogRepository = catalogRepository;
        _catalogPermissionSyncService = catalogPermissionSyncService;
    }

    public async Task<Response<NoContent>> Handle(UpdateModulePageActionDescriptorCommand request, CancellationToken ct)
    {
        var descriptor = await _repository.GetByIdAsync(request.Id, ct);
        if (descriptor is null)
        {
            return Response<NoContent>.Fail("Module page action descriptor not found.", 404);
        }

        // MC-7 — code-owned module: actions are reconciled from the manifest, not edited by hand.
        if (await SelfRegisteredModuleGuard.IsManagedByCodeAsync(_catalogRepository, descriptor.ModuleCode, ct))
        {
            return Response<NoContent>.Fail(ModuleCatalogErrorCodes.ModuleManagedByCode, 409);
        }

        var actionCode = ModulePageDescriptorNormalizer.NormalizePageCode(request.Request.ActionCode);
        if (await _repository.ExistsByActionCodeAsync(descriptor.PageDescriptorId, actionCode, descriptor.Id, ct))
        {
            return Response<NoContent>.Fail("ActionCode already exists for this page.", 409);
        }

        descriptor.ActionCode = actionCode;
        descriptor.DisplayName = request.Request.DisplayName.Trim();
        descriptor.PermissionKey = ModulePageDescriptorNormalizer.NormalizePermission(request.Request.PermissionKey);
        descriptor.ActionType = Enum.Parse<ModulePageActionType>(request.Request.ActionType, ignoreCase: false);
        descriptor.SortOrder = request.Request.SortOrder ?? 0;
        descriptor.IsDangerous = request.Request.IsDangerous;
        descriptor.IsToolbarAction = request.Request.IsToolbarAction;
        descriptor.IsRowAction = request.Request.IsRowAction;
        descriptor.Status = Enum.Parse<ModulePageActionStatus>(request.Request.Status, ignoreCase: false);
        descriptor.Description = ModulePageDescriptorNormalizer.NormalizeOptional(request.Request.Description);

        await _repository.UpdateAsync(descriptor, ct);

        // Declarative permission sync (best-effort; never blocks the save). See create handler.
        // Refresh Module; leave Scope null (no owning-page route here — an action metadata edit must not change the
        // route-derived scope, so AuthService keeps the existing value).
        await _catalogPermissionSyncService.SyncPermissionAsync(
            descriptor.PermissionKey, descriptor.DisplayName, descriptor.ModuleCode, null, ct);

        return Response<NoContent>.Success(204);
    }
}
