using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Application.Features.ModulePages.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.ModulePages.Handlers.CommandHandlers;

public sealed class DeleteModulePageActionDescriptorCommandHandler
    : IRequestHandler<DeleteModulePageActionDescriptorCommand, Response<NoContent>>
{
    private readonly IModulePageActionDescriptorRepository _repository;
    private readonly IModuleCatalogRepository _catalogRepository;
    private readonly IModulePageDescriptorRepository _pageRepository;
    private readonly ICatalogPermissionSyncService _permissionSync;
    private readonly ILogger<DeleteModulePageActionDescriptorCommandHandler> _logger;

    public DeleteModulePageActionDescriptorCommandHandler(
        IModulePageActionDescriptorRepository repository,
        IModuleCatalogRepository catalogRepository,
        IModulePageDescriptorRepository pageRepository,
        ICatalogPermissionSyncService permissionSync,
        ILogger<DeleteModulePageActionDescriptorCommandHandler> logger)
    {
        _repository = repository;
        _catalogRepository = catalogRepository;
        _pageRepository = pageRepository;
        _permissionSync = permissionSync;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(DeleteModulePageActionDescriptorCommand request, CancellationToken ct)
    {
        var descriptor = await _repository.GetByIdAsync(request.Id, ct);
        if (descriptor is null)
        {
            return Response<NoContent>.Fail("Module page action descriptor not found.", 404);
        }

        // MC-7 — code-owned module: actions are reconciled from the manifest, not deleted by hand.
        if (await SelfRegisteredModuleGuard.IsManagedByCodeAsync(_catalogRepository, descriptor.ModuleCode, ct))
        {
            return Response<NoContent>.Fail(ModuleCatalogErrorCodes.ModuleManagedByCode, 409);
        }

        await _repository.DeleteAsync(request.Id, ct);

        // FEAT-CATALOG-PERM-DELETE-SYNC — best-effort orphan cleanup: if the deleted action held the LAST catalog
        // reference to its PermissionKey, ask AuthService to remove the (catalog-sourced) permission. Any failure here
        // (count read or S2S call) must NEVER fail the already-committed delete.
        await TryRemoveOrphanPermissionAsync(descriptor.PermissionKey, ct);

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
            var actionRefs = await _repository.CountByPermissionKeyAsync(permissionKey, ct);
            var pageRefs = await _pageRepository.CountByRequiredPermissionAsync(permissionKey, ct);
            if (actionRefs + pageRefs > 0)
            {
                _logger.LogInformation(
                    "Catalog permission still referenced; not removing. PermissionKey={PermissionKey} ActionRefs={ActionRefs} PageRefs={PageRefs}",
                    permissionKey, actionRefs, pageRefs);
                return; // CatalogPermissionSyncStatus.SkippedStillReferenced
            }

            await _permissionSync.RemovePermissionAsync(permissionKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Orphan permission cleanup failed after action delete; catalog delete is unaffected. PermissionKey={PermissionKey}",
                permissionKey);
        }
    }
}
