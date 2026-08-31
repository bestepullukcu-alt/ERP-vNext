using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Application.Features.ModulePages;
using Diten.Platform.Domain.Catalog;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Diten.Platform.Application.Features.GlobalApplicability;
using MongoDB.Driver;

namespace Diten.Platform.Application.Features.ModuleRegistration;

/// <summary>
/// Idempotent, BEST-EFFORT reconcile of a pushed module manifest. Ownership model:
///   • Catalog item: HARD = ModuleCode (case-insensitive match), ModuleName, ModuleVersion. SOFT (seed-once,
///     operator-owned, NEVER overwritten on re-push) = Domain, Service, DisplayName, SortOrder, IsTenantAssignable, Status.
///   • Pages + Actions: HARD (code-owned) — upserted by natural key. AUTHORITATIVE (MC-6): module pages/actions the
///     manifest no longer declares are SOFT-DELETED (module-scoped, idempotent) so the catalog mirrors code with zero
///     drift. Permissions in AuthService are NOT deleted (additive; permission removal is separate and riskier).
///   • Resilience: a manifest page whose route is already held by a DIFFERENT page in the module is SKIPPED (logged +
///     reported), never deleting the holder. Any page/action that still hits a unique-key violation (E11000) is skipped
///     too. A partial failure returns 200 + a summary listing the skips — the whole register never 500s.
///   • Every reconciled page RequiredPermission + action PermissionKey is best-effort synced to AuthService.
/// </summary>
public sealed class RegisterModuleManifestCommandHandler
    : IRequestHandler<RegisterModuleManifestCommand, Response<ModuleManifestReconcileResult>>
{
    private const string ProtectedModuleCode = "PRODUCT-ITEM-SKU-MASTER";
    private const string ProtectedModuleName = "ProductItemSkuMaster";
    private const string ProtectedDomain = "MASTERDATAMANAGEMENT";
    private const string ProtectedService = "DITENMDMSERVICE";
    private const string ProtectedOwner = "DITENMDMSERVICE";

    private readonly ITransactionalModuleCatalogRepository _catalogRepository;
    private readonly IModulePageDescriptorRepository _pageRepository;
    private readonly IModulePageActionDescriptorRepository _actionRepository;
    private readonly ICatalogPermissionSyncService _permissionSyncService;
    private readonly Features.ModuleCatalog.Services.IModuleTaxonomyResolver _taxonomyResolver;
    private readonly IModuleDomainRepository _domainRepository;
    private readonly ILogger<RegisterModuleManifestCommandHandler> _logger;
    private readonly IGlobalApplicabilityTransactionCoordinator _transaction;
    private readonly IGlobalApplicabilityStateRepository _applicabilityState;

    public RegisterModuleManifestCommandHandler(
        ITransactionalModuleCatalogRepository catalogRepository,
        IModulePageDescriptorRepository pageRepository,
        IModulePageActionDescriptorRepository actionRepository,
        ICatalogPermissionSyncService permissionSyncService,
        Features.ModuleCatalog.Services.IModuleTaxonomyResolver taxonomyResolver,
        IModuleDomainRepository domainRepository,
        ILogger<RegisterModuleManifestCommandHandler> logger,
        IGlobalApplicabilityTransactionCoordinator transaction,
        IGlobalApplicabilityStateRepository applicabilityState)
    {
        _catalogRepository = catalogRepository;
        _pageRepository = pageRepository;
        _actionRepository = actionRepository;
        _permissionSyncService = permissionSyncService;
        _taxonomyResolver = taxonomyResolver;
        _domainRepository = domainRepository;
        _logger = logger;
        _transaction = transaction;
        _applicabilityState = applicabilityState;
    }

    public async Task<Response<ModuleManifestReconcileResult>> Handle(RegisterModuleManifestCommand request, CancellationToken ct)
    {
        var manifest = request.Manifest;
        var moduleCode = ModuleCatalogCodeNormalizer.Normalize(manifest.ModuleCode);
        if (string.IsNullOrWhiteSpace(moduleCode))
        {
            return Response<ModuleManifestReconcileResult>.Fail("ModuleCode is required.", 400);
        }

        if (string.Equals(moduleCode, ProtectedModuleCode, StringComparison.Ordinal))
        {
            var ownerGuardFailure = await ValidateProtectedOwnerIdentityAsync(request, moduleCode, ct);
            if (ownerGuardFailure is not null)
            {
                return Response<ModuleManifestReconcileResult>.Fail(ownerGuardFailure, 409);
            }
        }

        var catalogAction = await ReconcileCatalogItemAsync(
            manifest,
            moduleCode,
            string.Equals(moduleCode, ProtectedModuleCode, StringComparison.Ordinal) ? ProtectedOwner : null,
            ct);

        var pagesUpserted = 0;
        var actionsUpserted = 0;
        var permissionsSynced = 0;
        var pagesPruned = 0;
        var actionsPruned = 0;
        var pagesSkipped = new List<string>();

        // MC-6 — authoritative prune set: pages the manifest still declares (everything else in the module is orphan).
        var manifestPageCodes = manifest.Pages
            .Select(p => ModulePageDescriptorNormalizer.NormalizePageCode(p.PageCode))
            .ToHashSet(StringComparer.Ordinal);

        // Mutable so collisions between two manifest pages in the SAME push are detected (newly created pages occupy their route).
        var existingPages = (await _pageRepository.GetByModuleAsync(moduleCode, ct)).ToList();

        // MC-6 — authoritative prune FIRST: soft-delete this module's live pages (and their actions) that the manifest
        // no longer declares, BEFORE upserting. This frees the routes/codes of moved/renamed descriptors so the new
        // page upserts cleanly in the same push (a non-manifest orphan never blocks a manifest page). Module-scoped
        // (GetByModuleAsync is filtered by moduleCode), soft-delete only, idempotent (live query excludes the pruned).
        foreach (var orphanPage in existingPages.Where(p => !manifestPageCodes.Contains(p.PageCode)).ToList())
        {
            foreach (var orphanAction in await _actionRepository.GetByPageAsync(orphanPage.Id, ct))
            {
                await _actionRepository.DeleteAsync(orphanAction.Id, ct);
                actionsPruned++;
            }

            await _pageRepository.DeleteAsync(orphanPage.Id, ct);
            pagesPruned++;
            _logger.LogInformation(
                "Pruned orphan module page (not in manifest). ModuleCode={ModuleCode} PageCode={PageCode}",
                moduleCode,
                orphanPage.PageCode);
        }
        existingPages.RemoveAll(p => !manifestPageCodes.Contains(p.PageCode));

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
                && await TrySyncPermissionAsync(page.RequiredPermission, page.DisplayName, moduleCode, page.RoutePath, ct))
            {
                permissionsSynced++;
            }

            var existingActions = await _actionRepository.GetByPageAsync(page.Id, ct);

            // MC-6 — prune actions on THIS page that the manifest no longer declares (e.g. an action moved to
            // another page). existingActions was read before the upserts, so newly-created actions are excluded.
            var manifestActionCodes = manifestPage.Actions
                .Select(a => ModulePageDescriptorNormalizer.NormalizePageCode(a.ActionCode))
                .ToHashSet(StringComparer.Ordinal);
            foreach (var orphanAction in existingActions.Where(a => !manifestActionCodes.Contains(a.ActionCode)))
            {
                await _actionRepository.DeleteAsync(orphanAction.Id, ct);
                actionsPruned++;
            }

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
                // The action's scope follows its owning PAGE's route (actions have no route of their own).
                if (!string.IsNullOrWhiteSpace(action.PermissionKey)
                    && await TrySyncPermissionAsync(action.PermissionKey, action.DisplayName, moduleCode, page.RoutePath, ct))
                {
                    permissionsSynced++;
                }
            }
        }

        if (pagesPruned > 0 || actionsPruned > 0)
        {
            _logger.LogInformation(
                "Manifest reconcile pruned orphans. ModuleCode={ModuleCode} PagesPruned={PagesPruned} ActionsPruned={ActionsPruned}",
                moduleCode,
                pagesPruned,
                actionsPruned);
        }

        return Response<ModuleManifestReconcileResult>.Success(
            new ModuleManifestReconcileResult(
                moduleCode, catalogAction, pagesUpserted, actionsUpserted, permissionsSynced, pagesSkipped, pagesPruned, actionsPruned));
    }

    private async Task<string?> ValidateProtectedOwnerIdentityAsync(
        RegisterModuleManifestCommand request,
        string moduleCode,
        CancellationToken ct)
    {
        var manifest = request.Manifest;
        if (!string.Equals(
                ModuleTaxonomyCanonicalizer.NormalizeKey(request.TrustedProducerOwnerCode),
                ProtectedOwner,
                StringComparison.Ordinal))
        {
            return "Trusted producer owner does not match the protected module owner.";
        }

        if (!string.Equals(manifest.ModuleName?.Trim(), ProtectedModuleName, StringComparison.Ordinal)
            || !string.Equals(ModuleTaxonomyCanonicalizer.NormalizeKey(manifest.Domain), ProtectedDomain, StringComparison.Ordinal)
            || !string.Equals(ModuleTaxonomyCanonicalizer.NormalizeKey(manifest.Service), ProtectedService, StringComparison.Ordinal))
        {
            return "Manifest identity does not match the protected module identity.";
        }

        var existing = await _catalogRepository.GetByCodeIncludingDeletedAsync(moduleCode, ct);
        if (existing is null)
        {
            return null;
        }

        if (!string.Equals(ModuleCatalogCodeNormalizer.Normalize(existing.ModuleCode), ProtectedModuleCode, StringComparison.Ordinal)
            || !string.Equals(existing.ModuleName?.Trim(), ProtectedModuleName, StringComparison.Ordinal)
            || !string.Equals(existing.ProducerOwnerCode, ProtectedOwner, StringComparison.Ordinal)
            || existing.Origin != ModuleCatalogOrigin.SelfRegistered)
        {
            return "Catalog owner identity conflicts with the protected module identity.";
        }

        if (existing.IsDeleted)
        {
            await _catalogRepository.RestoreAsync(existing, ct);
        }

        return null;
    }

    private async Task<string> ReconcileCatalogItemAsync(
        ModuleManifestDocument manifest,
        string moduleCode,
        string? producerOwnerCode,
        CancellationToken ct)
    {
        // FIX-SELFREG-DOMAIN-REGISTER — ensure the manifest's Domain exists in the operator lookup (auto-register an
        // unknown domain), on BOTH first-register and re-push, so Domain Management surfaces self-registered domains
        // (e.g. Access Governance, Settings) that were added to the enum AFTER the one-time ModuleDomainSeed ran.
        var seededDomain = await ResolveOrRegisterDomainCodeAsync(manifest.Domain, ct);

        var seededService = await _taxonomyResolver.ResolveServiceCodeAsync(manifest.Service, ct);
        return await _transaction.ExecuteAsync(
            new(nameof(RegisterModuleManifestCommand), AuditOperation.Update, "ModuleCatalogItem", DeterministicEntityId(moduleCode)),
            async (session, transactionCt) =>
            {
        var existing = await _catalogRepository.GetByCodeAsync(session, moduleCode, transactionCt);
        if (existing is null)
        {
            // First registration: HARD identity + SOFT metadata seeded once from the manifest. FIX-DOMAIN-SERVICE-
            // CANONICAL — the manifest carries enum-names (e.g. "PlatformSharedServices"); resolve them to the
            // canonical lookup Code at seed time so the catalog never stores an enum-name/DisplayName variant.
            var created = new ModuleCatalogItem
            {
                ModuleCode = moduleCode,
                ModuleName = manifest.ModuleName.Trim(),
                DisplayName = manifest.DisplayName.Trim(),
                Domain = seededDomain,
                Service = seededService,
                ProducerOwnerCode = producerOwnerCode,
                Status = ModuleCatalogStatus.Active,
                ModuleVersion = string.IsNullOrWhiteSpace(manifest.ModuleVersion) ? "1.0.0" : manifest.ModuleVersion.Trim(),
                IsTenantAssignable = manifest.IsTenantAssignable,
                SortOrder = manifest.SortOrder,
                // FIX-MODULE-ICON — SOFT: seed the module's default sidebar icon once. Re-push (below) never
                // overwrites it, so an operator's catalog edit is permanent.
                Icon = string.IsNullOrWhiteSpace(manifest.Icon) ? null : manifest.Icon.Trim(),
                // FEAT-BASELINE-MODULES — HARD (code-owned): baseline is a code decision, refreshed on every push.
                IsBaseline = manifest.IsBaseline,
                Origin = ModuleCatalogOrigin.SelfRegistered // MC-4 — code-owned
            };
            await _catalogRepository.CreateAsync(session, created, transactionCt);
            return new GlobalApplicabilityMutation<string>("created", true,
                (s, version, token) => _applicabilityState.UpsertModuleCatalogAsync(s, created, version, token));
        }

        // Re-push: refresh only HARD fields. SOFT metadata (Domain/Service/DisplayName/SortOrder/IsTenantAssignable/Status)
        // belongs to the operator and is NEVER overwritten here.
        var moduleName = manifest.ModuleName.Trim();
        var moduleVersion = string.IsNullOrWhiteSpace(manifest.ModuleVersion) ? existing.ModuleVersion : manifest.ModuleVersion.Trim();
        var changed = existing.ModuleName != moduleName || existing.ModuleVersion != moduleVersion
            || existing.IsBaseline != manifest.IsBaseline || existing.Origin != ModuleCatalogOrigin.SelfRegistered;
        if (!changed) return new GlobalApplicabilityMutation<string>("updated", false);
        existing.ModuleName = moduleName;
        existing.ModuleVersion = moduleVersion;
        // FEAT-BASELINE-MODULES — HARD (code-owned): refreshed on every re-push (unlike SOFT Icon/Domain/Service).
        existing.IsBaseline = manifest.IsBaseline;
        // MC-4 — a manual placeholder that later self-registers flips to code-owned (Manual → SelfRegistered).
        existing.Origin = ModuleCatalogOrigin.SelfRegistered;
        await _catalogRepository.UpdateAsync(session, existing, transactionCt);
        return new GlobalApplicabilityMutation<string>("updated", true,
            (s, version, token) => _applicabilityState.UpsertModuleCatalogAsync(s, existing, version, token));
            }, ct);
    }

    private static Guid DeterministicEntityId(string moduleCode)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(moduleCode));
        return new Guid(hash.AsSpan(0, 16));
    }

    /// <summary>
    /// FIX-SELFREG-DOMAIN-REGISTER — resolves the manifest's raw Domain to a canonical lookup Code, AUTO-REGISTERING
    /// it in <c>platform_module_domains</c> when it is genuinely unknown. Matching is format-tolerant (normalized
    /// Code OR DisplayName) and considers ALL live domains — active AND inactive — so a domain the operator
    /// DEACTIVATED is reused, never recreated. A soft-deleted domain (excluded by the repo filter) is guarded by a
    /// duplicate-key catch on create, so "delete is permanent" holds. Idempotent: a re-push finds the existing row.
    /// </summary>
    private async Task<string> ResolveOrRegisterDomainCodeAsync(string? rawDomain, CancellationToken ct)
    {
        var trimmed = rawDomain?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var key = ModuleTaxonomyCanonicalizer.NormalizeKey(trimmed);
        if (key.Length == 0)
        {
            return trimmed;
        }

        // Match against every live domain (active + inactive) by normalized Code OR DisplayName. Domains are few,
        // so a single page (repo clamps to 200) covers them. An existing match is REUSED — never recreated.
        var (all, _) = await _domainRepository.QueryAsync(new ModuleDomainQuery(null, null, 1, 200, "code"), ct);
        var match = all.FirstOrDefault(d =>
            string.Equals(ModuleTaxonomyCanonicalizer.NormalizeKey(d.Code), key, StringComparison.Ordinal)
            || string.Equals(ModuleTaxonomyCanonicalizer.NormalizeKey(d.DisplayName), key, StringComparison.Ordinal));
        if (match is not null)
        {
            return match.Code;
        }

        // Genuinely unknown → register it (SOFT: operator may rename/deactivate/delete later; a re-push finds it
        // by the match above and never duplicates). FIX-DOMAIN-DEDUP — the Code is the SAME canonical normalized
        // key used for lookup (and by the create/seed paths), so self-registration can never mint a second row in
        // a different format (e.g. "ACCESS-GOVERNANCE") that collides with an existing "ACCESSGOVERNANCE".
        var code = key;
        try
        {
            var created = await _domainRepository.CreateAsync(new ModuleDomain
            {
                Code = code,
                DisplayName = trimmed,
                IsActive = true,
                SortOrder = 1000 // appended after the seeded defaults; operator can reorder
            }, ct);
            _logger.LogInformation(
                "Auto-registered module domain from manifest. Code={Code} DisplayName={DisplayName}", created.Code, trimmed);
            return created.Code;
        }
        catch (MongoWriteException ex) when (IsDuplicateKey(ex))
        {
            // A soft-deleted domain (or a race) already holds this Code under a full unique index → do NOT revive it.
            _logger.LogWarning(ex, "Module domain '{Code}' already exists (possibly soft-deleted); not recreating.", code);
            return code;
        }
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

    private async Task<bool> TrySyncPermissionAsync(
        string? permissionKey, string displayName, string moduleCode, string? routePath, CancellationToken ct)
    {
        var status = await _permissionSyncService.SyncPermissionAsync(
            permissionKey, displayName, moduleCode, ModulePageDescriptorNormalizer.ScopeFromRoute(routePath), ct);
        return status == CatalogPermissionSyncStatus.Synced;
    }

    private static bool IsDuplicateKey(MongoWriteException ex) =>
        ex.WriteError?.Category == ServerErrorCategory.DuplicateKey;

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed) ? parsed : fallback;
}
