using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Common.Services;

/// <summary>
/// Applies the locked entitlement → role-permission revoke semantics
/// (services/Diten.AuthService/docs/entitlement-permission-bridge.md, S2) over the S1 grant-source
/// fields. Pure-ish (repository-backed, no transport) so it is unit-testable and reused by the
/// eventing consumer.
/// </summary>
public sealed class EntitlementPermissionSyncService : IEntitlementPermissionSyncService
{
    // Role targeting (S2 left this to S3): the default-provisioned tenant roles receive module grants.
    // Admin gets the module's full permission set; Viewer gets the module's read permissions only —
    // mirroring the S1 baseline role semantics (administrative vs read-only), scoped to the module.
    private static readonly string[] TargetRoleNames =
        [DefaultRolePermissionTemplate.AdminRole, DefaultRolePermissionTemplate.ViewerRole];

    private static readonly string[] ReconciliationRoleNames =
    [
        DefaultRolePermissionTemplate.AdminRole,
        DefaultRolePermissionTemplate.ViewerRole,
        ProductAbbreviationEntitlementGrantProfile.RequesterRole,
        ProductAbbreviationEntitlementGrantProfile.StewardRole,
        ProductAbbreviationEntitlementGrantProfile.ApproverRole,
        ProductAbbreviationEntitlementGrantProfile.AuditorRole,
        ProductLegalEntityScopeEntitlementGrantProfile.StewardRole,
        ProductLegalEntityScopeEntitlementGrantProfile.AuditorRole,
        ProductLegalEntityScopeEntitlementGrantProfile.RolloutOperatorRole,
        ProductIdentityLifecycleEntitlementGrantProfile.StewardRole,
        ProductIdentityLifecycleEntitlementGrantProfile.ApproverRole,
        ProductIdentityLifecycleEntitlementGrantProfile.RetirementStewardRole,
        ProductIdentityRecoveryOperatorEntitlementGrantProfile.RoleName
    ];

    private readonly IPermissionRepository _permissions;
    private readonly IRoleRepository _roles;
    private readonly IRolePermissionRepository _rolePermissions;
    private readonly ILogger<EntitlementPermissionSyncService> _logger;

    public EntitlementPermissionSyncService(
        IPermissionRepository permissions,
        IRoleRepository roles,
        IRolePermissionRepository rolePermissions,
        ILogger<EntitlementPermissionSyncService> logger)
    {
        _permissions = permissions;
        _roles = roles;
        _rolePermissions = rolePermissions;
        _logger = logger;
    }

    public async Task GrantModuleAsync(Guid tenantId, string moduleCode, string actor, CancellationToken ct = default)
    {
        var code = ModulePermissionResolver.NormalizeModuleCode(moduleCode);
        if (code.Length == 0)
        {
            return; // fail-safe: blank module code is a no-op
        }

        var catalog = await _permissions.GetAllAsync(ct);
        var modulePermissions = ModulePermissionResolver.ResolvePermissions(moduleCode, catalog);
        // unmatched / platform module → no-op (resolver already excludes platform)
        await GrantPermissionsToRolesAsync(tenantId, code, modulePermissions, catalog.ToList(), actor, ct);
    }

    public async Task GrantModuleWithKeysAsync(
        Guid tenantId,
        string moduleCode,
        IReadOnlyCollection<string> permissionKeys,
        string actor,
        CancellationToken ct = default)
    {
        var code = ModulePermissionResolver.NormalizeModuleCode(moduleCode);
        if (code.Length == 0)
        {
            return; // fail-safe: blank module code is a no-op
        }

        var suppliedKeys = permissionKeys ?? Array.Empty<string>();
        var normalizedKeys = suppliedKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .ToList();
        var keySet = normalizedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var isProductAbbreviationProfile =
            ProductAbbreviationEntitlementGrantProfile.AppliesTo(code, keySet);
        var isProductLegalEntityScopeProfile =
            ProductLegalEntityScopeEntitlementGrantProfile.AppliesTo(code, keySet);
        var isProductIdentityLifecycleProfile =
            ProductIdentityLifecycleEntitlementGrantProfile.AppliesTo(code, keySet);
        var isProductIdentityRecoveryProfile =
            ProductIdentityRecoveryOperatorEntitlementGrantProfile.AppliesTo(code, keySet);
        if ((isProductAbbreviationProfile || isProductLegalEntityScopeProfile || isProductIdentityLifecycleProfile
             || isProductIdentityRecoveryProfile)
            && (normalizedKeys.Count != suppliedKeys.Count || keySet.Count != normalizedKeys.Count))
        {
            throw new InvalidOperationException(
                "Special entitlement reconciliation rejects blank or duplicate permission descriptors.");
        }
        if (isProductAbbreviationProfile)
        {
            ProductAbbreviationEntitlementGrantProfile.ValidateExactPermissionSet(keySet);
            ValidateExactProductAbbreviationDescriptorKeys(keySet);
        }
        if (isProductLegalEntityScopeProfile)
        {
            ProductLegalEntityScopeEntitlementGrantProfile.ValidateExactPermissionSet(keySet);
        }
        if (isProductIdentityLifecycleProfile)
        {
            ProductIdentityLifecycleEntitlementGrantProfile.ValidateExactDeclaredPermissionSet(keySet);
        }
        if (isProductIdentityRecoveryProfile)
        {
            ProductIdentityRecoveryOperatorEntitlementGrantProfile.ValidateExactPermissionSet(keySet);
        }

        var catalog = await _permissions.GetAllAsync(ct);
        var activeModuleCatalog = catalog
            .Where(permission => !permission.IsDeleted
                                 && string.Equals(
                                     ModulePermissionResolver.NormalizeModuleCode(permission.Module),
                                     code,
                                     StringComparison.OrdinalIgnoreCase))
            .ToList();
        var catalogContainsSpecialProfile = activeModuleCatalog.Any(permission =>
            ProductAbbreviationEntitlementGrantProfile.IsProductAbbreviationKey(permission.Key)
            || ProductLegalEntityScopeEntitlementGrantProfile.IsProductLegalEntityScopeKey(permission.Key)
            || ProductIdentityLifecycleEntitlementGrantProfile.IsOwnedPermissionKey(permission.Key)
            || ProductIdentityRecoveryOperatorEntitlementGrantProfile.IsRecoveryPermissionKey(permission.Key));
        var lifecycleDeactivationSnapshot = !isProductIdentityLifecycleProfile
            && activeModuleCatalog.Any(permission =>
                ProductIdentityLifecycleEntitlementGrantProfile.IsOwnedPermissionKey(permission.Key))
            && ProductIdentityLifecycleEntitlementGrantProfile.BasePermissionKeys.All(keySet.Contains)
            && !keySet.Any(ProductIdentityLifecycleEntitlementGrantProfile.IsOwnedPermissionKey);

        // No declared keys normally fall back to the convention resolver. A module whose active catalog contains a
        // special least-privilege profile must never take that path: an authoritative empty descriptor snapshot is
        // malformed, not permission to reconstruct a broader grant set from the Auth catalog.
        if (keySet.Count == 0)
        {
            if (catalogContainsSpecialProfile)
            {
                throw new InvalidOperationException(
                    "Special entitlement reconciliation rejects an empty active module descriptor set.");
            }

            await GrantModuleAsync(tenantId, moduleCode, actor, ct);
            return;
        }

        // Catalog is authoritative: grant exactly the permissions the module DECLARES, by Key — namespace-agnostic,
        // so organization's platform.organization-units.* / platform.positions.* keys resolve where the convention
        // (Module==ModuleCode) could not. Per-role selection (Admin=full, Viewer=read) is preserved.
        if (isProductAbbreviationProfile || isProductLegalEntityScopeProfile
            || isProductIdentityLifecycleProfile || isProductIdentityRecoveryProfile
            || catalogContainsSpecialProfile)
        {
            var authoritativeKeys = activeModuleCatalog
                .Select(permission => permission.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (lifecycleDeactivationSnapshot)
            {
                authoritativeKeys.RemoveWhere(key =>
                    ProductIdentityLifecycleEntitlementGrantProfile.IsOwnedPermissionKey(key));
            }
            if (!keySet.SetEquals(authoritativeKeys))
            {
                throw new InvalidOperationException(
                    "Special entitlement reconciliation requires the complete active module descriptor set.");
            }
        }

        var modulePermissions = catalog
            .Where(p => !p.IsDeleted && keySet.Contains(p.Key))
            .ToList();

        if (isProductAbbreviationProfile)
        {
            ProductAbbreviationEntitlementGrantProfile.ValidateExactPermissionSet(
                modulePermissions.Select(permission => permission.Key));
        }
        if (isProductLegalEntityScopeProfile)
        {
            ProductLegalEntityScopeEntitlementGrantProfile.ValidateExactPermissionDefinitions(modulePermissions);
        }
        if (isProductIdentityLifecycleProfile)
        {
            ProductIdentityLifecycleEntitlementGrantProfile.ValidateAndResolveDefinitions(modulePermissions, catalog);
        }
        if (isProductIdentityRecoveryProfile)
        {
            ProductIdentityRecoveryOperatorEntitlementGrantProfile.ValidateAndResolveDefinition(modulePermissions);
        }

        await GrantPermissionsToRolesAsync(tenantId, code, modulePermissions, catalog.ToList(), actor, ct);
    }

    // Shared role-grant body: assigns the resolved module permissions to the target roles as Module-grants
    // (Admin = full set, Viewer = read-only), idempotently. Empty set → no-op.
    private async Task GrantPermissionsToRolesAsync(
        Guid tenantId,
        string code,
        IReadOnlyList<Permission> modulePermissions,
        IReadOnlyList<Permission> globalCatalog,
        string actor,
        CancellationToken ct)
    {
        if (modulePermissions.Count == 0)
        {
            return;
        }

        var permissionKeys = modulePermissions.Select(permission => permission.Key).ToArray();
        var hasProductAbbreviationProfile =
            ProductAbbreviationEntitlementGrantProfile.AppliesTo(code, permissionKeys);
        var hasProductLegalEntityScopeProfile =
            ProductLegalEntityScopeEntitlementGrantProfile.AppliesTo(code, permissionKeys);
        var hasProductIdentityLifecycleProfile =
            ProductIdentityLifecycleEntitlementGrantProfile.AppliesTo(code, permissionKeys);
        var hasProductIdentityRecoveryProfile =
            ProductIdentityRecoveryOperatorEntitlementGrantProfile.AppliesTo(code, permissionKeys);
        if (hasProductAbbreviationProfile || hasProductLegalEntityScopeProfile
            || hasProductIdentityLifecycleProfile || hasProductIdentityRecoveryProfile)
        {
            if (hasProductAbbreviationProfile)
            {
                ProductAbbreviationEntitlementGrantProfile.ValidateExactPermissionSet(permissionKeys);
            }
            if (hasProductLegalEntityScopeProfile)
            {
                ProductLegalEntityScopeEntitlementGrantProfile.ValidateExactPermissionSet(permissionKeys);
            }
            if (hasProductIdentityLifecycleProfile)
            {
                ProductIdentityLifecycleEntitlementGrantProfile.ValidateExactDeclaredPermissionSet(permissionKeys);
            }
            if (hasProductIdentityRecoveryProfile)
            {
                ProductIdentityRecoveryOperatorEntitlementGrantProfile.ValidateExactPermissionSet(permissionKeys);
            }

            await ReconcileSpecialProfilesAsync(
                tenantId,
                code,
                modulePermissions,
                globalCatalog,
                hasProductAbbreviationProfile,
                hasProductLegalEntityScopeProfile,
                hasProductIdentityLifecycleProfile,
                hasProductIdentityRecoveryProfile,
                actor,
                ct);
            return;
        }

        foreach (var roleName in TargetRoleNames)
        {
            var role = await _roles.GetByNameAndTenantAsync(roleName, tenantId, ct);
            if (role is null)
            {
                continue; // role not provisioned yet → skip (idempotent / fail-safe)
            }

            var rolePermissions = SelectForRole(roleName, modulePermissions);
            if (rolePermissions.Count == 0)
            {
                continue;
            }

            var existing = await _rolePermissions.GetByRoleAsync(role.Id, tenantId, ct);

            foreach (var permission in rolePermissions)
            {
                // Align with the unique index (RoleId, PermissionId, TenantId): if this permission is ALREADY
                // granted to the role by ANY source (System baseline / Manual operator / another Module), do NOT
                // insert — that would hit E11000 and abort the whole sync. Safe-skip keeps baseline/manual intact
                // (we never downgrade or duplicate); the permission is already effective for the role.
                var alreadyGranted = existing.Any(rp => rp.PermissionId == permission.Id);
                if (alreadyGranted)
                {
                    continue; // idempotent
                }

                await _rolePermissions.AssignAsync(
                    RolePermission.ModuleGrant(role.Id, permission.Id, tenantId, actor, code),
                    ct);
            }
        }
    }

    public async Task RevokeModuleAsync(Guid tenantId, string moduleCode, string actor, CancellationToken ct = default)
    {
        var code = ModulePermissionResolver.NormalizeModuleCode(moduleCode);
        if (code.Length == 0)
        {
            return;
        }

        IReadOnlyList<Role> dedicatedRoles = Array.Empty<Role>();
        IReadOnlyList<RecoveryContamination> recoveryContamination = Array.Empty<RecoveryContamination>();
        if (string.Equals(
                code,
                ProductAbbreviationEntitlementGrantProfile.ModuleCode,
                StringComparison.OrdinalIgnoreCase))
        {
            recoveryContamination = await RevokeRecoveryAndScanContaminationAsync(tenantId, code, ct);
            dedicatedRoles = await ResolveDedicatedRolesAsync(
                tenantId,
                includeProductAbbreviation: true,
                includeProductLegalEntityScope: true,
                includeProductIdentityLifecycle: true,
                includeProductIdentityRecovery: false,
                createMissing: false,
                ct);
        }

        var roles = new List<Role>();
        foreach (var roleName in TargetRoleNames)
        {
            var role = await _roles.GetByNameAndTenantAsync(roleName, tenantId, ct);
            if (role is not null)
            {
                roles.Add(role);
            }
        }
        roles.AddRange(dedicatedRoles);

        foreach (var role in roles.DistinctBy(item => item.Id))
        {
            // The recovery role is provisioning-owned. Its exact recovery rows were narrowed tenant-wide above;
            // every other row on this role is contamination evidence and must survive for explicit remediation.
            if (string.Equals(
                    role.Name,
                    ProductIdentityRecoveryOperatorEntitlementGrantProfile.RoleName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var existing = await _rolePermissions.GetByRoleAsync(role.Id, tenantId, ct);

            // Drop ONLY this module's grants. System (baseline) and Manual (operator) grants — and
            // Module grants from other source modules (shared permissions) — are left untouched, so a
            // shared permission survives until its last contributing entitlement is removed.
            var toRemove = existing
                .Where(rp => rp.GrantSource == GrantSource.Module
                             && string.Equals(rp.SourceModuleCode, code, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var rp in toRemove)
            {
                await _rolePermissions.RemoveByIdAsync(rp.Id, tenantId, ct);
            }
        }

        if (recoveryContamination.Count > 0)
        {
            foreach (var item in recoveryContamination)
            {
                _logger.LogError(
                    "entitlement.sync.product_identity_recovery_contamination TenantId={TenantId} RoleId={RoleId} RoleName={RoleName} GrantSource={GrantSource} SourceModuleCode={SourceModuleCode}",
                    tenantId,
                    item.RoleId,
                    item.RoleName,
                    item.GrantSource,
                    item.SourceModuleCode);
            }

            throw RecoveryContaminationException();
        }
    }

    public async Task SyncTenantModulesAsync(
        Guid tenantId,
        IReadOnlyCollection<string> entitledModuleCodes,
        string actor,
        CancellationToken ct = default)
    {
        // Normalize + dedupe the authoritative entitled set (blank codes dropped).
        var entitled = (entitledModuleCodes ?? Array.Empty<string>())
            .Select(ModulePermissionResolver.NormalizeModuleCode)
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 1) Grant every currently-entitled module (idempotent; no-op for already-granted). Best-effort PER
        //    module: one module's failure (e.g. a transient repo error) must not abort the rest — workflow must
        //    still be granted even if goldenslim hiccuped.
        foreach (var code in entitled)
        {
            try
            {
                await GrantModuleAsync(tenantId, code, actor, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "entitlement.sync.grant_failed TenantId={TenantId} ModuleCode={ModuleCode}", tenantId, code);
                if (IsCompositeSpecialModule(code))
                {
                    throw;
                }
            }
        }

        // 2) Revoke Module-grants whose source module is no longer entitled (plan downgrade / removal). System
        //    (baseline) and Manual (operator) grants are never considered. Other modules' grants are preserved.
        await RevokeStaleModulesAsync(tenantId, entitled, actor, ct);
    }

    public async Task SyncTenantModulesWithKeysAsync(
        Guid tenantId,
        IReadOnlyCollection<EntitledModulePermissionKeys> modules,
        string actor,
        CancellationToken ct = default)
    {
        var list = (modules ?? Array.Empty<EntitledModulePermissionKeys>())
            .Where(m => m is not null && !string.IsNullOrWhiteSpace(m.ModuleCode))
            .ToList();

        // The authoritative entitled set (normalized + deduped) for the revoke pass.
        var entitled = list
            .Select(m => ModulePermissionResolver.NormalizeModuleCode(m.ModuleCode))
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 1) Grant every entitled module from its DECLARED catalog key set. Continue the pass so independent modules
        //    can converge, but retain every failure: the caller must not mark the integration event complete.
        var failures = new List<Exception>();
        foreach (var module in list)
        {
            try
            {
                await GrantModuleWithKeysAsync(tenantId, module.ModuleCode, module.PermissionKeys, actor, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "entitlement.sync.grant_failed TenantId={TenantId} ModuleCode={ModuleCode}", tenantId, module.ModuleCode);
                failures.Add(new InvalidOperationException(
                    $"Entitlement grant reconciliation failed for module '{module.ModuleCode}'.",
                    ex));
            }
        }

        // 2) Revoke Module-grants whose source module is no longer entitled. Identical semantics to the
        //    convention-based sync — System/Manual grants are never touched.
        await RevokeStaleModulesAsync(tenantId, entitled, actor, ct, failures);

        if (failures.Count > 0)
        {
            // Preserve the established single-module failure contract. A multi-module pass uses an
            // aggregate so independent modules can converge without hiding any reconciliation error.
            if (list.Count == 1 && failures.Count == 1)
            {
                throw failures[0];
            }

            throw new AggregateException("One or more entitlement module reconciliations failed.", failures);
        }
    }

    // Drops Module-grants whose source module is no longer in the entitled set. System (baseline) and Manual
    // (operator) grants are never considered; other modules' grants are preserved (shared-permission safe).
    private async Task RevokeStaleModulesAsync(
        Guid tenantId,
        IReadOnlySet<string> entitled,
        string actor,
        CancellationToken ct,
        ICollection<Exception>? failures = null)
    {
        foreach (var roleName in ReconciliationRoleNames)
        {
            var role = await _roles.GetByNameAndTenantAsync(roleName, tenantId, ct);
            if (role is null)
            {
                continue;
            }

            var existing = await _rolePermissions.GetByRoleAsync(role.Id, tenantId, ct);
            var staleSourceCodes = existing
                .Where(rp => rp.GrantSource == GrantSource.Module && !string.IsNullOrWhiteSpace(rp.SourceModuleCode))
                .Select(rp => ModulePermissionResolver.NormalizeModuleCode(rp.SourceModuleCode))
                .Where(c => c.Length > 0 && !entitled.Contains(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var stale in staleSourceCodes)
            {
                try
                {
                    await RevokeModuleAsync(tenantId, stale, actor, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "entitlement.sync.revoke_failed TenantId={TenantId} ModuleCode={ModuleCode}", tenantId, stale);
                    failures?.Add(new InvalidOperationException(
                        $"Entitlement revoke reconciliation failed for module '{stale}'.",
                        ex));
                }
            }
        }
    }

    private static IReadOnlyList<Permission> SelectForRole(string roleName, IReadOnlyList<Permission> modulePermissions)
        => roleName switch
        {
            DefaultRolePermissionTemplate.AdminRole => modulePermissions,
            DefaultRolePermissionTemplate.ViewerRole => modulePermissions
                .Where(p => string.Equals(p.Action, DefaultRolePermissionTemplate.ReadAction, StringComparison.OrdinalIgnoreCase))
                .ToList(),
            _ => Array.Empty<Permission>()
        };

    private async Task ReconcileSpecialProfilesAsync(
        Guid tenantId,
        string code,
        IReadOnlyList<Permission> modulePermissions,
        IReadOnlyList<Permission> globalCatalog,
        bool includeProductAbbreviation,
        bool includeProductLegalEntityScope,
        bool includeProductIdentityLifecycle,
        bool includeProductIdentityRecovery,
        string actor,
        CancellationToken ct)
    {
        var abbreviationPermissions = modulePermissions
            .Where(permission => ProductAbbreviationEntitlementGrantProfile.IsProductAbbreviationKey(permission.Key))
            .ToDictionary(permission => permission.Key, StringComparer.OrdinalIgnoreCase);
        var legalEntityScopePermissions = modulePermissions
            .Where(permission => ProductLegalEntityScopeEntitlementGrantProfile.IsProductLegalEntityScopeKey(permission.Key))
            .ToDictionary(permission => permission.Key, StringComparer.OrdinalIgnoreCase);
        var productIdentityPermissions = modulePermissions
            .Where(permission => ProductIdentityLifecycleEntitlementGrantProfile.IsBasePermissionKey(permission.Key)
                                 || ProductIdentityLifecycleEntitlementGrantProfile.IsOwnedPermissionKey(permission.Key))
            .ToDictionary(permission => permission.Key, StringComparer.Ordinal);
        var recoveryPermissions = modulePermissions
            .Where(permission => ProductIdentityRecoveryOperatorEntitlementGrantProfile.IsRecoveryPermissionKey(permission.Key))
            .ToDictionary(permission => permission.Key, StringComparer.Ordinal);
        var genericPermissions = modulePermissions
            .Where(permission => !ProductAbbreviationEntitlementGrantProfile.IsProductAbbreviationKey(permission.Key)
                                 && !ProductLegalEntityScopeEntitlementGrantProfile.IsProductLegalEntityScopeKey(permission.Key)
                                 && !ProductIdentityLifecycleEntitlementGrantProfile.IsOwnedPermissionKey(permission.Key)
                                 && !ProductIdentityRecoveryOperatorEntitlementGrantProfile.IsRecoveryPermissionKey(permission.Key))
            .ToList();

        if (includeProductAbbreviation)
        {
            ValidateProductAbbreviationPermissionDefinitions(abbreviationPermissions.Values);
        }
        if (includeProductLegalEntityScope)
        {
            ProductLegalEntityScopeEntitlementGrantProfile.ValidateExactPermissionDefinitions(
                legalEntityScopePermissions.Values);
        }
        IReadOnlyDictionary<string, Permission> workflowDependencies =
            new Dictionary<string, Permission>(StringComparer.Ordinal);
        if (includeProductIdentityLifecycle)
        {
            workflowDependencies = ProductIdentityLifecycleEntitlementGrantProfile.ValidateAndResolveDefinitions(
                modulePermissions,
                globalCatalog);
        }
        Permission? recoveryPermission = null;
        if (includeProductIdentityRecovery)
        {
            recoveryPermission = ProductIdentityRecoveryOperatorEntitlementGrantProfile.ValidateAndResolveDefinition(
                recoveryPermissions.Values);
            await PreflightRecoveryTenantAsync(tenantId, recoveryPermission.Id, ct);
        }

        // All applicable dedicated role names are preflighted before the first role or grant mutation. A collision in
        // either special profile therefore cannot leave the other profile partially provisioned.
        var dedicatedRoles = await ResolveDedicatedRolesAsync(
            tenantId,
            includeProductAbbreviation,
            includeProductLegalEntityScope,
            includeProductIdentityLifecycle,
            includeProductIdentityRecovery,
            createMissing: true,
            ct);
        if (includeProductIdentityRecovery && recoveryPermission is not null)
        {
            // Re-read immediately before grant-plan mutation. The generic assignment boundary blocks supported
            // concurrent writes; this second scan also catches another reconciliation that raced the first preflight.
            await PreflightRecoveryTenantAsync(tenantId, recoveryPermission.Id, ct);
        }
        var plans = new List<(Role Role, IReadOnlyList<Permission> Permissions)>();

        foreach (var roleName in TargetRoleNames)
        {
            var role = await _roles.GetByNameAndTenantAsync(roleName, tenantId, ct);
            if (role is null)
            {
                continue;
            }

            IReadOnlyList<Permission> permissions = roleName switch
            {
                DefaultRolePermissionTemplate.AdminRole => genericPermissions
                    .Concat(includeProductAbbreviation
                        ? [abbreviationPermissions[ProductAbbreviationEntitlementGrantProfile.Read]]
                        : Array.Empty<Permission>())
                    .Concat(includeProductLegalEntityScope
                        ? [legalEntityScopePermissions[ProductLegalEntityScopeEntitlementGrantProfile.Read]]
                        : Array.Empty<Permission>())
                    .ToList(),
                DefaultRolePermissionTemplate.ViewerRole => genericPermissions
                    .Where(permission => string.Equals(
                        permission.Action,
                        DefaultRolePermissionTemplate.ReadAction,
                        StringComparison.OrdinalIgnoreCase))
                    .Concat(includeProductAbbreviation
                        ? [abbreviationPermissions[ProductAbbreviationEntitlementGrantProfile.Read]]
                        : Array.Empty<Permission>())
                    .ToList(),
                _ => Array.Empty<Permission>()
            };
            plans.Add((role, permissions));
        }

        foreach (var role in dedicatedRoles)
        {
            var abbreviationTemplate = ProductAbbreviationEntitlementGrantProfile.DedicatedRoles
                .SingleOrDefault(item => string.Equals(item.RoleName, role.Name, StringComparison.Ordinal));
            if (abbreviationTemplate is not null)
            {
                plans.Add((
                    role,
                    abbreviationTemplate.PermissionKeys.Select(key => abbreviationPermissions[key]).ToList()));
                continue;
            }

            var scopeTemplate = ProductLegalEntityScopeEntitlementGrantProfile.DedicatedRoles
                .SingleOrDefault(item => string.Equals(item.RoleName, role.Name, StringComparison.Ordinal));
            if (scopeTemplate is not null)
            {
                plans.Add((
                    role,
                    includeProductLegalEntityScope
                        ? scopeTemplate.PermissionKeys.Select(key => legalEntityScopePermissions[key]).ToList()
                        : Array.Empty<Permission>()));
                continue;
            }

            var lifecycleTemplate = ProductIdentityLifecycleEntitlementGrantProfile.DedicatedRoles
                .SingleOrDefault(item => string.Equals(item.RoleName, role.Name, StringComparison.Ordinal));
            if (lifecycleTemplate is null)
            {
                if (!string.Equals(
                        role.Name,
                        ProductIdentityRecoveryOperatorEntitlementGrantProfile.RoleName,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Unknown entitlement profile role '{role.Name}'.");
                }

                plans.Add((
                    role,
                    includeProductIdentityRecovery && recoveryPermission is not null
                        ? [recoveryPermission]
                        : Array.Empty<Permission>()));
                continue;
            }
            plans.Add((
                role,
                includeProductIdentityLifecycle
                    ? lifecycleTemplate.PermissionKeys.Select(key =>
                            workflowDependencies.TryGetValue(key, out var dependency)
                                ? dependency
                                : productIdentityPermissions[key])
                        .ToList()
                    : Array.Empty<Permission>()));
        }

        foreach (var (role, desiredPermissions) in plans)
        {
            var existing = await _rolePermissions.GetByRoleAsync(role.Id, tenantId, ct);
            var desiredIds = desiredPermissions.Select(permission => permission.Id).ToHashSet();
            var existingSourceOwnedCount = existing.Count(grant =>
                grant.GrantSource == GrantSource.Module
                && string.Equals(grant.SourceModuleCode, code, StringComparison.OrdinalIgnoreCase));
            var inserted = 0;
            var removed = 0;

            var staleModuleGrants = existing
                .Where(grant => grant.GrantSource == GrantSource.Module
                                && string.Equals(
                                    grant.SourceModuleCode,
                                    code,
                                    StringComparison.OrdinalIgnoreCase)
                                && !desiredIds.Contains(grant.PermissionId))
                .ToList();

            foreach (var stale in staleModuleGrants)
            {
                await _rolePermissions.RemoveByIdAsync(stale.Id, tenantId, ct);
                removed++;
            }

            foreach (var permission in desiredPermissions)
            {
                if (existing.Any(grant => grant.PermissionId == permission.Id))
                {
                    continue;
                }

                await _rolePermissions.AssignAsync(
                    RolePermission.ModuleGrant(role.Id, permission.Id, tenantId, actor, code),
                    ct);
                inserted++;
            }

            var isLifecycleRole = includeProductIdentityLifecycle
                && ProductIdentityLifecycleEntitlementGrantProfile.DedicatedRoles.Any(template =>
                    string.Equals(template.RoleName, role.Name, StringComparison.Ordinal));
            var isRecoveryRole = includeProductIdentityRecovery
                && string.Equals(
                    role.Name,
                    ProductIdentityRecoveryOperatorEntitlementGrantProfile.RoleName,
                    StringComparison.Ordinal);
            if (!isLifecycleRole && !isRecoveryRole)
            {
                continue;
            }

            var effective = await _rolePermissions.GetByRoleAsync(role.Id, tenantId, ct);
            var effectiveIds = effective.Select(grant => grant.PermissionId).ToHashSet();
            var sourceOwnedIds = effective
                .Where(grant => grant.GrantSource == GrantSource.Module
                                && string.Equals(
                                    grant.SourceModuleCode,
                                    code,
                                    StringComparison.OrdinalIgnoreCase))
                .Select(grant => grant.PermissionId)
                .ToHashSet();

            _logger.LogInformation(
                "Product identity lifecycle role reconciled. RoleName={RoleName} Desired={Desired} Existing={Existing} Inserted={Inserted} Removed={Removed}",
                role.Name,
                desiredIds.Count,
                existingSourceOwnedCount,
                inserted,
                removed);

            if (!desiredIds.IsSubsetOf(effectiveIds) || !sourceOwnedIds.IsSubsetOf(desiredIds))
            {
                throw new InvalidOperationException(
                    $"Product identity grant reconciliation did not converge for role '{role.Name}'.");
            }
        }
    }

    private async Task<IReadOnlyList<Role>> ResolveDedicatedRolesAsync(
        Guid tenantId,
        bool includeProductAbbreviation,
        bool includeProductLegalEntityScope,
        bool includeProductIdentityLifecycle,
        bool includeProductIdentityRecovery,
        bool createMissing,
        CancellationToken ct)
    {
        var templates = ProductAbbreviationEntitlementGrantProfile.DedicatedRoles.Select(template =>
                new DedicatedRoleTemplate(
                    template.RoleName,
                    template.DisplayName,
                    template.Description,
                    includeProductAbbreviation))
            .Concat(ProductLegalEntityScopeEntitlementGrantProfile.DedicatedRoles.Select(template =>
                new DedicatedRoleTemplate(
                    template.RoleName,
                    template.DisplayName,
                    template.Description,
                    includeProductLegalEntityScope)))
            .Concat(ProductIdentityLifecycleEntitlementGrantProfile.DedicatedRoles.Select(template =>
                new DedicatedRoleTemplate(
                    template.RoleName,
                    template.DisplayName,
                    template.Description,
                    includeProductIdentityLifecycle)))
            .ToList();
        if (includeProductIdentityRecovery || createMissing)
        {
            templates.Add(new DedicatedRoleTemplate(
                ProductIdentityRecoveryOperatorEntitlementGrantProfile.RoleName,
                ProductIdentityRecoveryOperatorEntitlementGrantProfile.DisplayName,
                ProductIdentityRecoveryOperatorEntitlementGrantProfile.Description,
                includeProductIdentityRecovery));
        }

        var existing = new Dictionary<string, Role?>(StringComparer.Ordinal);
        foreach (var template in templates)
        {
            var role = await _roles.GetByNameAndTenantAsync(template.RoleName, tenantId, ct);
            if (role is not null && !role.IsSystem)
            {
                throw new InvalidOperationException(
                    $"Entitlement profile system role name collision: '{template.RoleName}'.");
            }

            existing[template.RoleName] = role;
        }

        if (!createMissing)
        {
            return existing.Values.Where(role => role is not null).Cast<Role>().ToList();
        }

        var resolved = new List<Role>();
        foreach (var template in templates)
        {
            var role = existing[template.RoleName];
            if (role is null && !template.IsEnabled)
            {
                continue;
            }

            role ??= await _roles.UpsertSystemRoleAsync(
                           template.RoleName,
                           template.DisplayName,
                           template.Description,
                           tenantId,
                           ct);
            if (!role.IsSystem)
            {
                throw new InvalidOperationException(
                    $"Entitlement profile system role name collision: '{template.RoleName}'.");
            }

            resolved.Add(role);
        }

        return resolved;
    }

    private static void ValidateProductAbbreviationPermissionDefinitions(IEnumerable<Permission> permissions)
    {
        var supplied = permissions.ToList();
        ProductAbbreviationEntitlementGrantProfile.ValidateExactPermissionSet(
            supplied.Select(permission => permission.Key));
        ValidateExactProductAbbreviationDescriptorKeys(supplied.Select(permission => permission.Key));
        foreach (var permission in supplied)
        {
            var expectedAction = permission.Key[(permission.Key.LastIndexOf('.') + 1)..];
            if (permission.IsDeleted
                || permission.Scope != PermissionScope.Tenant
                || !string.Equals(
                    permission.Module,
                    ProductAbbreviationEntitlementGrantProfile.ModuleCode,
                    StringComparison.Ordinal)
                || !string.Equals(permission.Resource, "product-abbreviations", StringComparison.Ordinal)
                || !string.Equals(permission.Action, expectedAction, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Product Abbreviation permission catalog definitions do not match the exact approved contract.");
            }
        }
    }

    private static void ValidateExactProductAbbreviationDescriptorKeys(IEnumerable<string> permissionKeys)
    {
        var supplied = permissionKeys
            .Where(ProductAbbreviationEntitlementGrantProfile.IsProductAbbreviationKey)
            .ToHashSet(StringComparer.Ordinal);
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            ProductAbbreviationEntitlementGrantProfile.Read,
            ProductAbbreviationEntitlementGrantProfile.Request,
            ProductAbbreviationEntitlementGrantProfile.Cancel,
            ProductAbbreviationEntitlementGrantProfile.Approve,
            ProductAbbreviationEntitlementGrantProfile.Reject,
            ProductAbbreviationEntitlementGrantProfile.Correct,
            ProductAbbreviationEntitlementGrantProfile.Retire,
            ProductAbbreviationEntitlementGrantProfile.Audit
        };
        if (!supplied.SetEquals(expected))
        {
            throw new InvalidOperationException(
                "Product Abbreviation entitlement reconciliation requires exact canonical permission keys.");
        }
    }

    private async Task PreflightRecoveryTenantAsync(Guid tenantId, Guid recoveryPermissionId, CancellationToken ct)
    {
        var contamination = await ScanRecoveryContaminationAsync(tenantId, recoveryPermissionId, ct);
        if (contamination.Count > 0)
        {
            throw RecoveryContaminationException();
        }
    }

    private async Task<IReadOnlyList<RecoveryContamination>> RevokeRecoveryAndScanContaminationAsync(
        Guid tenantId,
        string code,
        CancellationToken ct)
    {
        // Narrowing revoke is identity-based, not descriptor-validity-based. A deleted or divergent catalog row must
        // not strand an already-effective grant. Active grant/restore retains the strict tuple validation above.
        var recoveryPermission = await _permissions.GetByKeyIncludingDeletedAsync(
            ProductIdentityRecoveryOperatorEntitlementGrantProfile.PermissionKey,
            ct);
        if (recoveryPermission is null)
        {
            return Array.Empty<RecoveryContamination>();
        }

        if (!string.Equals(
                recoveryPermission.Key,
                ProductIdentityRecoveryOperatorEntitlementGrantProfile.PermissionKey,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("PRODUCT_IDENTITY_RECOVERY_CATALOG_IDENTITY_MISMATCH");
        }

        var roles = (await _roles.GetAllByTenantAsync(tenantId, ct)).ToList();

        foreach (var role in roles)
        {
            var grants = await _rolePermissions.GetByRoleAsync(role.Id, tenantId, ct);
            foreach (var grant in grants.Where(grant =>
                         grant.PermissionId == recoveryPermission.Id
                         && grant.GrantSource == GrantSource.Module
                         && string.Equals(grant.SourceModuleCode, code, StringComparison.Ordinal)))
            {
                await _rolePermissions.RemoveByIdAsync(grant.Id, tenantId, ct);
            }
        }

        return await ScanRecoveryContaminationAsync(tenantId, recoveryPermission.Id, ct);
    }

    private async Task<IReadOnlyList<RecoveryContamination>> ScanRecoveryContaminationAsync(
        Guid tenantId,
        Guid recoveryPermissionId,
        CancellationToken ct)
    {
        var contamination = new List<RecoveryContamination>();
        var canonicalRecoveryCount = 0;
        var roles = await _roles.GetAllByTenantAsync(tenantId, ct);
        foreach (var role in roles)
        {
            var isReservedRole = string.Equals(
                role.Name,
                ProductIdentityRecoveryOperatorEntitlementGrantProfile.RoleName,
                StringComparison.Ordinal);
            var grants = await _rolePermissions.GetByRoleAsync(role.Id, tenantId, ct);
            foreach (var grant in grants)
            {
                var isRecoveryGrant = grant.PermissionId == recoveryPermissionId;
                var isCanonicalRecoveryGrant = isReservedRole
                    && role.IsSystem
                    && isRecoveryGrant
                    && grant.GrantSource == GrantSource.Module
                    && string.Equals(
                        grant.SourceModuleCode,
                        ProductIdentityRecoveryOperatorEntitlementGrantProfile.ModuleCode,
                        StringComparison.Ordinal);
                if (isCanonicalRecoveryGrant)
                {
                    canonicalRecoveryCount++;
                    if (canonicalRecoveryCount == 1)
                    {
                        continue;
                    }
                }
                else if (!isReservedRole && !isRecoveryGrant)
                {
                    continue;
                }

                contamination.Add(new RecoveryContamination(
                    role.Id,
                    role.Name,
                    grant.GrantSource,
                    string.IsNullOrWhiteSpace(grant.SourceModuleCode)
                        ? string.Empty
                        : ModulePermissionResolver.NormalizeModuleCode(grant.SourceModuleCode)));
            }

            if (isReservedRole && !role.IsSystem && grants.Count == 0)
            {
                contamination.Add(new RecoveryContamination(
                    role.Id,
                    role.Name,
                    GrantSource.Manual,
                    string.Empty));
            }
        }

        return contamination;
    }

    private static InvalidOperationException RecoveryContaminationException()
        => new("PRODUCT_IDENTITY_RECOVERY_GRANT_CONTAMINATION");

    private sealed record DedicatedRoleTemplate(
        string RoleName,
        string DisplayName,
        string Description,
        bool IsEnabled);

    private sealed record RecoveryContamination(
        Guid RoleId,
        string RoleName,
        GrantSource GrantSource,
        string SourceModuleCode);

    private static bool IsCompositeSpecialModule(string? moduleCode)
        => string.Equals(
            ModulePermissionResolver.NormalizeModuleCode(moduleCode),
            ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
            StringComparison.OrdinalIgnoreCase);
}
