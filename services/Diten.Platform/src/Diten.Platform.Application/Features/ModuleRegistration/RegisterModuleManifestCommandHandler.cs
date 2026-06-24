using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Application.Features.ModulePages;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.Platform.Application.Features.ModuleRegistration;

/// <summary>
/// Idempotent, BEST-EFFORT reconcile of a pushed module manifest. Ownership model:
///   • Catalog item: HARD = ModuleCode (case-insensitive match), ModuleName, ModuleVersion. SOFT (seed-once,
///     operator-owned, NEVER overwritten on re-push) = Domain, Service, DisplayName, SortOrder, IsTenantAssignable, Status.
///   • Pages + Actions: HARD (code-owned) — upserted by natural key. Pages NOT in the manifest are PRESERVED (v1 additive,
///     no delete) so operator-added pages survive.
///   • Resilience: a manifest page whose route is already held by a DIFFERENT page in the module is SKIPPED (logged +
///     reported), never deleting the holder. Any page/action that still hits a unique-key violation (E11000) is skipped
///     too. A partial failure returns 200 + a summary listing the skips — the whole register never 500s.
///   • Every reconciled page RequiredPermission + action PermissionKey is best-effort synced to AuthService.
/// </summary>
public sealed class RegisterModuleManifestCommandHandler
    : IRequestHandler<RegisterModuleManifestCommand, Response<ModuleManifestReconcileResult>>
{
    private readonly IModuleCatalogRepository _catalogRepository;
    private readonly IModulePageDescriptorRepository _pageRepository;
    private readonly IModulePageActionDescriptorRepository _actionRepository;
    private readonly ICatalogPermissionSyncService _permissionSyncService;
    private readonly ILogger<RegisterModuleManifestCommandHandler> _logger;

    public RegisterModuleManifestCommandHandler(
        IModuleCatalogRepository catalogRepository,
        IModulePageDescriptorRepository pageRepository,
        IModulePageActionDescriptorRepository actionRepository,
        ICatalogPermissionSyncService permissionSyncService,
        ILogger<RegisterModuleManifestCommandHandler> logger)
    {
        _catalogRepository = catalogRepository;
        _pageRepository = pageRepository;
        _actionRepository = actionRepository;
        _permissionSyncService = permissionSyncService;
        _logger = logger;
    }

    public async Task<Response<ModuleManifestReconcileResult>> Handle(RegisterModuleManifestCommand request, CancellationToken ct)
    {
        var manifest = request.Manifest;
        var moduleCode = ModuleCatalogCodeNormalizer.Normalize(manifest.ModuleCode);
        if (string.IsNullOrWhiteSpace(moduleCode))
        {
            return Response<ModuleManifestReconcileResult>.Fail("ModuleCode is required.", 400);
        }

        var catalogAction = await ReconcileCatalogItemAsync(manifest, moduleCode, ct);

        // Mutable so collisions between two manifest pages in the SAME push are detected (newly created pages occupy their route).
        var existingPages = (await _pageRepository.GetByModuleAsync(moduleCode, ct)).ToList();
        var pagesUpserted = 0;
        var actionsUpserted = 0;
        var permissionsSynced = 0;
        var pagesSkipped = new List<string>();

        foreach (var manifestPage in manifest.Pages)
        {
            var pageCode = ModulePageDescriptorNormalizer.NormalizePageCode(manifestPage.PageCode);
            var routePath = ModulePageDescriptorNormalizer.NormalizeRoutePath(manifestPage.RoutePath);

            // Route collision: a DIFFERENT page in this module already holds this route (unique index on module+route).
            var routeHolder = existingPages.FirstOrDefault(p =>
                string.Equals(p.RoutePath, routePath, StringComparison.Ordinal)
                && !string.Equals(p.PageCode, pageCode, StringComparison.Ordinal));
            if (routeHolder is not null)
            {
                var reason = $"{pageCode}: route {routePath} held by {routeHolder.PageCode}";
                _logger.LogWarning("Skipping manifest page (route collision). ModuleCode={ModuleCode} Detail={Detail}", moduleCode, reason);
                pagesSkipped.Add(reason);
                continue;
            }

            ModulePageDescriptor page;
            bool created;
            try
            {
                (page, created) = await UpsertPageAsync(manifestPage, moduleCode, pageCode, routePath, existingPages, ct);
            }
            catch (MongoWriteException ex) when (IsDuplicateKey(ex))
            {
                var reason = $"{pageCode}: duplicate key (E11000)";
                _logger.LogWarning(ex, "Skipping manifest page (duplicate key). ModuleCode={ModuleCode} PageCode={PageCode}", moduleCode, pageCode);
                pagesSkipped.Add(reason);
                continue;
            }

            if (created)
            {
                existingPages.Add(page); // occupy this route for subsequent collision checks in the same push
            }
            pagesUpserted++;

            if (!string.IsNullOrWhiteSpace(page.RequiredPermission)
                && await TrySyncPermissionAsync(page.RequiredPermission, page.DisplayName, ct))
            {
                permissionsSynced++;
            }

            var existingActions = await _actionRepository.GetByPageAsync(page.Id, ct);
            foreach (var manifestAction in manifestPage.Actions)
            {
                ModulePageActionDescriptor? action;
                try
                {
                    action = await UpsertActionAsync(manifestAction, moduleCode, page, existingActions, ct);
                }
                catch (MongoWriteException ex) when (IsDuplicateKey(ex))
                {
                    _logger.LogWarning(ex, "Skipping manifest action (duplicate key). ModuleCode={ModuleCode} PageCode={PageCode} ActionCode={ActionCode}",
                        moduleCode, pageCode, manifestAction.ActionCode);
                    continue;
                }

                actionsUpserted++;
                if (!string.IsNullOrWhiteSpace(action.PermissionKey)
                    && await TrySyncPermissionAsync(action.PermissionKey, action.DisplayName, ct))
                {
                    permissionsSynced++;
                }
            }
        }

        return Response<ModuleManifestReconcileResult>.Success(
            new ModuleManifestReconcileResult(moduleCode, catalogAction, pagesUpserted, actionsUpserted, permissionsSynced, pagesSkipped));
    }

    private async Task<string> ReconcileCatalogItemAsync(ModuleManifestDocument manifest, string moduleCode, CancellationToken ct)
    {
        var existing = await _catalogRepository.GetByCodeAsync(moduleCode, ct);
        if (existing is null)
        {
            // First registration: HARD identity + SOFT metadata seeded once from the manifest.
            await _catalogRepository.CreateAsync(new ModuleCatalogItem
            {
                ModuleCode = moduleCode,
                ModuleName = manifest.ModuleName.Trim(),
                DisplayName = manifest.DisplayName.Trim(),
                Domain = manifest.Domain.Trim(),
                Service = manifest.Service.Trim(),
                Status = ModuleCatalogStatus.Active,
                ModuleVersion = string.IsNullOrWhiteSpace(manifest.ModuleVersion) ? "1.0.0" : manifest.ModuleVersion.Trim(),
                IsTenantAssignable = manifest.IsTenantAssignable,
                SortOrder = manifest.SortOrder
            }, ct);
            return "created";
        }

        // Re-push: refresh only HARD fields. SOFT metadata (Domain/Service/DisplayName/SortOrder/IsTenantAssignable/Status)
        // belongs to the operator and is NEVER overwritten here.
        existing.ModuleName = manifest.ModuleName.Trim();
        existing.ModuleVersion = string.IsNullOrWhiteSpace(manifest.ModuleVersion) ? existing.ModuleVersion : manifest.ModuleVersion.Trim();
        await _catalogRepository.UpdateAsync(existing, ct);
        return "updated";
    }

    private async Task<(ModulePageDescriptor Page, bool Created)> UpsertPageAsync(
        ModuleManifestPage manifestPage,
        string moduleCode,
        string pageCode,
        string routePath,
        IReadOnlyList<ModulePageDescriptor> existingPages,
        CancellationToken ct)
    {
        var requiredPermission = ModulePageDescriptorNormalizer.NormalizeOptionalPermission(manifestPage.RequiredPermission);
        var parentPageCode = ModulePageDescriptorNormalizer.NormalizeOptionalPageCode(manifestPage.ParentPageCode);
        var displayName = manifestPage.DisplayName.Trim();
        var pageType = ParseEnum(manifestPage.PageType, ModulePageType.List);

        var existing = existingPages.FirstOrDefault(p => string.Equals(p.PageCode, pageCode, StringComparison.Ordinal));
        if (existing is null)
        {
            var page = new ModulePageDescriptor
            {
                TenantId = Guid.Empty,
                ModuleCode = moduleCode,
                PageCode = pageCode,
                DisplayName = displayName,
                RoutePath = routePath,
                RequiredPermission = requiredPermission,
                ParentPageCode = parentPageCode,
                IsNavigationVisible = manifestPage.IsNavigationVisible,
                PageType = pageType,
                Status = ModulePageStatus.Active,
                SortOrder = manifestPage.SortOrder
            };
            await _pageRepository.CreateAsync(page, ct);
            return (page, true);
        }

        // HARD code-owned fields refreshed; Status is operator-owned (left as-is).
        existing.DisplayName = displayName;
        existing.RoutePath = routePath;
        existing.RequiredPermission = requiredPermission;
        existing.ParentPageCode = parentPageCode;
        existing.IsNavigationVisible = manifestPage.IsNavigationVisible;
        existing.PageType = pageType;
        existing.SortOrder = manifestPage.SortOrder;
        await _pageRepository.UpdateAsync(existing, ct);
        return (existing, false);
    }

    private async Task<ModulePageActionDescriptor> UpsertActionAsync(
        ModuleManifestAction manifestAction,
        string moduleCode,
        ModulePageDescriptor page,
        IReadOnlyList<ModulePageActionDescriptor> existingActions,
        CancellationToken ct)
    {
        var actionCode = ModulePageDescriptorNormalizer.NormalizePageCode(manifestAction.ActionCode);
        var permissionKey = ModulePageDescriptorNormalizer.NormalizePermission(manifestAction.PermissionKey);
        var displayName = manifestAction.DisplayName.Trim();
        var actionType = ParseEnum(manifestAction.ActionType, ModulePageActionType.Toolbar);

        var existing = existingActions.FirstOrDefault(a => string.Equals(a.ActionCode, actionCode, StringComparison.Ordinal));
        if (existing is null)
        {
            var action = new ModulePageActionDescriptor
            {
                TenantId = Guid.Empty,
                PageDescriptorId = page.Id,
                ModuleCode = moduleCode,
                PageCode = page.PageCode,
                ActionCode = actionCode,
                DisplayName = displayName,
                PermissionKey = permissionKey,
                ActionType = actionType,
                SortOrder = manifestAction.SortOrder,
                IsDangerous = manifestAction.IsDangerous,
                IsToolbarAction = manifestAction.IsToolbarAction,
                IsRowAction = manifestAction.IsRowAction,
                Status = ModulePageActionStatus.Active
            };
            await _actionRepository.CreateAsync(action, ct);
            return action;
        }

        existing.DisplayName = displayName;
        existing.PermissionKey = permissionKey;
        existing.ActionType = actionType;
        existing.SortOrder = manifestAction.SortOrder;
        existing.IsDangerous = manifestAction.IsDangerous;
        existing.IsToolbarAction = manifestAction.IsToolbarAction;
        existing.IsRowAction = manifestAction.IsRowAction;
        await _actionRepository.UpdateAsync(existing, ct);
        return existing;
    }

    private async Task<bool> TrySyncPermissionAsync(string? permissionKey, string displayName, CancellationToken ct)
    {
        var status = await _permissionSyncService.SyncPermissionAsync(permissionKey, displayName, ct);
        return status == CatalogPermissionSyncStatus.Synced;
    }

    private static bool IsDuplicateKey(MongoWriteException ex) =>
        ex.WriteError?.Category == ServerErrorCategory.DuplicateKey;

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed) ? parsed : fallback;
}
