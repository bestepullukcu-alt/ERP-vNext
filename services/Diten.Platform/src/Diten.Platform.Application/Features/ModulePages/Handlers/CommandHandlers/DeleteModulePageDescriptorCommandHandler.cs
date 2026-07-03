using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Application.Features.ModulePages.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.ModulePages.Handlers.CommandHandlers;

public sealed class DeleteModulePageDescriptorCommandHandler : IRequestHandler<DeleteModulePageDescriptorCommand, Response<NoContent>>
{
    private readonly IModulePageDescriptorRepository _repository;
    private readonly IModuleCatalogRepository _catalogRepository;
    private readonly IModulePageActionDescriptorRepository _actionRepository;
    private readonly ICatalogPermissionSyncService _permissionSync;
    private readonly ILogger<DeleteModulePageDescriptorCommandHandler> _logger;

    public DeleteModulePageDescriptorCommandHandler(
        IModulePageDescriptorRepository repository,
        IModuleCatalogRepository catalogRepository,
        IModulePageActionDescriptorRepository actionRepository,
        ICatalogPermissionSyncService permissionSync,
        ILogger<DeleteModulePageDescriptorCommandHandler> logger)
    {
        _repository = repository;
        _catalogRepository = catalogRepository;
        _actionRepository = actionRepository;
        _permissionSync = permissionSync;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(DeleteModulePageDescriptorCommand request, CancellationToken ct)
    {
        var descriptor = await _repository.GetByIdAsync(request.Id, ct);
        if (descriptor is null)
        {
            return Response<NoContent>.Fail("Module page descriptor not found.", 404);
        }

        // MC-7 — code-owned module: pages are reconciled from the manifest, not deleted by hand.
        if (await SelfRegisteredModuleGuard.IsManagedByCodeAsync(_catalogRepository, descriptor.ModuleCode, ct))
        {
            return Response<NoContent>.Fail(ModuleCatalogErrorCodes.ModuleManagedByCode, 409);
        }

        await _repository.DeleteAsync(descriptor.Id, ct);

        // FEAT-CATALOG-PERM-DELETE-SYNC — best-effort orphan cleanup: if the deleted page held the LAST catalog
        // reference to its RequiredPermission, ask AuthService to remove the (catalog-sourced) permission. Any failure
        // here (count read or S2S call) must NEVER fail the already-committed delete.
        await TryRemoveOrphanPermissionAsync(descriptor.RequiredPermission, ct);

        return Response<NoContent>.Success(204);
    }

    private async Task TryRemoveOrphanPermissionAsync(string? permissionKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            return;
        }

        try
        {
            // Count taken AFTER the soft-delete, so the just-deleted descriptor is already excluded.
            var pageRefs = await _repository.CountByRequiredPermissionAsync(permissionKey, ct);
            var actionRefs = await _actionRepository.CountByPermissionKeyAsync(permissionKey, ct);
            if (pageRefs + actionRefs > 0)
            {
                _logger.LogInformation(
                    "Catalog permission still referenced; not removing. PermissionKey={PermissionKey} PageRefs={PageRefs} ActionRefs={ActionRefs}",
                    permissionKey, pageRefs, actionRefs);
                return; // CatalogPermissionSyncStatus.SkippedStillReferenced
            }

            await _permissionSync.RemovePermissionAsync(permissionKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Orphan permission cleanup failed after page delete; catalog delete is unaffected. PermissionKey={PermissionKey}",
                permissionKey);
        }
    }
}
