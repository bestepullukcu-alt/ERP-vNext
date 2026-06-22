using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModulePages.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Handlers.CommandHandlers;

public sealed class UpdateModulePageDescriptorCommandHandler : IRequestHandler<UpdateModulePageDescriptorCommand, Response<NoContent>>
{
    private readonly IModulePageDescriptorRepository _repository;
    private readonly ICatalogPermissionSyncService _catalogPermissionSyncService;

    public UpdateModulePageDescriptorCommandHandler(
        IModulePageDescriptorRepository repository,
        ICatalogPermissionSyncService catalogPermissionSyncService)
    {
        _repository = repository;
        _catalogPermissionSyncService = catalogPermissionSyncService;
    }

    public async Task<Response<NoContent>> Handle(UpdateModulePageDescriptorCommand request, CancellationToken ct)
    {
        var descriptor = await _repository.GetByIdAsync(request.Id, ct);
        if (descriptor is null)
        {
            return Response<NoContent>.Fail("Module page descriptor not found.", 404);
        }

        var pageCode = ModulePageDescriptorNormalizer.NormalizePageCode(request.Request.PageCode);
        var routePath = ModulePageDescriptorNormalizer.NormalizeRoutePath(request.Request.RoutePath);

        if (await _repository.ExistsByPageCodeAsync(descriptor.ModuleCode, pageCode, descriptor.Id, ct))
        {
            return Response<NoContent>.Fail("Sayfa kodu bu modülde zaten kayıtlı.", 409);
        }

        if (await _repository.ExistsByRoutePathAsync(descriptor.ModuleCode, routePath, descriptor.Id, ct))
        {
            return Response<NoContent>.Fail("Rota yolu bu modülde zaten kayıtlı.", 409);
        }

        descriptor.PageCode = pageCode;
        descriptor.DisplayName = request.Request.DisplayName.Trim();
        descriptor.RoutePath = routePath;
        descriptor.RequiredPermission = ModulePageDescriptorNormalizer.NormalizeOptionalPermission(request.Request.RequiredPermission);
        descriptor.ParentPageCode = ModulePageDescriptorNormalizer.NormalizeOptionalPageCode(request.Request.ParentPageCode);
        descriptor.IsNavigationVisible = request.Request.IsNavigationVisible ?? true;
        descriptor.PageType = Enum.Parse<ModulePageType>(request.Request.PageType, ignoreCase: false);
        descriptor.Status = Enum.Parse<ModulePageStatus>(request.Request.Status, ignoreCase: false);
        descriptor.SortOrder = request.Request.SortOrder ?? 0;
        descriptor.Description = ModulePageDescriptorNormalizer.NormalizeOptional(request.Request.Description);

        await _repository.UpdateAsync(descriptor, ct);

        // Declarative permission sync (best-effort; never blocks the save). See create handler.
        await _catalogPermissionSyncService.SyncPermissionAsync(descriptor.RequiredPermission, descriptor.DisplayName, ct);

        return Response<NoContent>.Success(204);
    }
}
