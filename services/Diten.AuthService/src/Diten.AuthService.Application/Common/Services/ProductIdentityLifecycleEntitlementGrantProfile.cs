using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Services;

/// <summary>
/// Exact entitlement grant profile for Product Identity lifecycle permissions. Approval and rejection remain
/// Workflow-owned; this profile owns the bounded product lifecycle keys and consumes existing Workflow dependencies.
/// </summary>
public static class ProductIdentityLifecycleEntitlementGrantProfile
{
    public const string ModuleCode = "product-item-sku-master";

    public const string GlobalProductsRead = "mdm.global-products.read";
    public const string GlobalProductsCreate = "mdm.global-products.create";
    public const string GlobalProductsSubmit = "mdm.global-products.submit";
    public const string GlobalProductsRetire = "mdm.global-products.retire";
    public const string GlobalProductsUpdate = "mdm.global-products.update";
    public const string GlobalProductsWithdraw = "mdm.global-products.withdraw";
    public const string GlobalProductsRequestCorrection = "mdm.global-products.request-correction";
    public const string GlobalProductsRequestRetirement = "mdm.global-products.request-retirement";
    public const string GskusRead = "mdm.gskus.read";
    public const string GskusCreate = "mdm.gskus.create";
    public const string GskusSubmit = "mdm.gskus.submit";
    public const string GskusRetire = "mdm.gskus.retire";
    public const string GskusUpdate = "mdm.gskus.update";
    public const string GskusWithdraw = "mdm.gskus.withdraw";
    public const string GskusRequestCorrection = "mdm.gskus.request-correction";
    public const string GskusRequestRetirement = "mdm.gskus.request-retirement";
    public const string LskusRead = "mdm.lskus.read";
    public const string LskusCreate = "mdm.lskus.create";
    public const string LskusSubmit = "mdm.lskus.submit";
    public const string LskusRetire = "mdm.lskus.retire";
    public const string LskusWithdraw = "mdm.lskus.withdraw";
    public const string LskusRequestRetirement = "mdm.lskus.request-retirement";
    public const string FinishedGoodsRead = "mdm.finished-goods.read";
    public const string FinishedGoodsCreate = "mdm.finished-goods.create";
    public const string FinishedGoodsSubmit = "mdm.finished-goods.submit";
    public const string FinishedGoodsRetire = "mdm.finished-goods.retire";
    // WP-AUTH-BL573-01 (O-12) — stock behaviour has its own edit keys, apart from the identity edit keys.
    public const string GskusEditStockBehaviour = "mdm.gskus.edit-stock-behaviour";
    public const string LskusEditStockBehaviour = "mdm.lskus.edit-stock-behaviour";
    public const string FinishedGoodsEditStockBehaviour = "mdm.finished-goods.edit-stock-behaviour";
    // BL-592 (WP-MDM-PERMS-01): the item master keys MDM's ItemsController checks (measured on the CT line, FU04 S4 and
    // S5: the same six; reactivation is gated by .submit — there is no .reactivate key) and the stock team's read
    // contract (S4 ProductMasterContract.ReadPermission).
    public const string ItemsRead = "mdm.items.read";
    public const string ItemsCreate = "mdm.items.create";
    public const string ItemsUpdate = "mdm.items.update";
    public const string ItemsSubmit = "mdm.items.submit";
    public const string ItemsDeactivate = "mdm.items.deactivate";
    public const string ItemsRetire = "mdm.items.retire";
    public const string ProductMasterRead = "mdm.product-master.read";
    public const string ProductIdentityLifecycleOperationRecover =
        DefaultRolePermissionTemplate.ProductIdentityLifecycleRecoveryPermission;

    public const string WorkCenterInboxView = "platform.work-aggregation.inbox.view";
    public const string WorkflowInstancesStart = "platform.workflow.instances.start";
    public const string WorkflowTasksApprove = "platform.workflow.tasks.approve";
    public const string WorkflowTasksReject = "platform.workflow.tasks.reject";

    public const string StewardRole = "ProductDataSteward";
    public const string ApproverRole = "ProductIdentityApprover";
    public const string RetirementStewardRole = "ProductIdentityRetirementSteward";

    public static readonly IReadOnlySet<string> BasePermissionKeys =
        new HashSet<string>(StringComparer.Ordinal)
        {
            GlobalProductsRead,
            GlobalProductsCreate,
            GskusRead,
            GskusCreate,
            LskusRead,
            LskusCreate,
            FinishedGoodsRead,
            FinishedGoodsCreate
        };

    public static readonly IReadOnlySet<string> PermissionKeys =
        new HashSet<string>(StringComparer.Ordinal)
        {
            GlobalProductsSubmit,
            GlobalProductsRetire,
            GlobalProductsUpdate,
            GlobalProductsWithdraw,
            GlobalProductsRequestCorrection,
            GlobalProductsRequestRetirement,
            GskusSubmit,
            GskusRetire,
            GskusUpdate,
            GskusWithdraw,
            GskusRequestCorrection,
            GskusRequestRetirement,
            LskusSubmit,
            LskusRetire,
            LskusWithdraw,
            LskusRequestRetirement,
            FinishedGoodsSubmit,
            FinishedGoodsRetire
        };

    /// <summary>
    /// WP-AUTH-BL573-01 (CT decision A) — the stock-behaviour keys are an ALL-OR-NOTHING optional set: the module may
    /// declare none of them (today's MDM manifest) or exactly these three (once S5 ships the manifest); one or two of
    /// them is refused like any partial set. Auth can therefore merge before the manifest, and the manifest can follow
    /// without another Auth change. Making the three mandatory is the second half of BL-573 (after S5 is live everywhere).
    /// Granted to <see cref="StewardRole"/> only when declared; never to Viewer (not a read action) and — like the owned
    /// lifecycle write keys — not in the Admin role's generic set; no backfill to roles holding the provisional keys.
    /// </summary>
    public static readonly IReadOnlySet<string> StockBehaviourPermissionKeys =
        new HashSet<string>(StringComparer.Ordinal)
        {
            GskusEditStockBehaviour,
            LskusEditStockBehaviour,
            FinishedGoodsEditStockBehaviour
        };

    /// <summary>
    /// BL-592 (CT decision, the BL-573 pattern) — the item master keys are an ALL-OR-NOTHING optional set: today's
    /// manifest declares none, FU04 S5's declares all six; one to five of them, or any other <c>mdm.items.*</c> key, is
    /// refused. Generic keys (not dedicated-only): with the module's entitlement the tenant Admin receives all six and the
    /// Viewer the read action only (<see cref="DefaultRolePermissionTemplate.EntitlementOnlyViewerPermissions"/>).
    /// </summary>
    public static readonly IReadOnlySet<string> ItemPermissionKeys =
        new HashSet<string>(StringComparer.Ordinal)
        {
            ItemsRead,
            ItemsCreate,
            ItemsUpdate,
            ItemsSubmit,
            ItemsDeactivate,
            ItemsRetire
        };

    /// <summary>
    /// BL-592 — the product-master read contract is an optional single key: declared or not; any other
    /// <c>mdm.product-master.*</c> key is refused. Generic: Admin and (read action) Viewer receive it with the entitlement.
    /// </summary>
    public static readonly IReadOnlySet<string> ProductMasterPermissionKeys =
        new HashSet<string>(StringComparer.Ordinal)
        {
            ProductMasterRead
        };

    public static readonly IReadOnlySet<string> SharedDependencyKeys =
        new HashSet<string>(StringComparer.Ordinal)
        {
            WorkCenterInboxView,
            WorkflowInstancesStart,
            WorkflowTasksApprove,
            WorkflowTasksReject
        };

    public static readonly IReadOnlySet<string> NonHumanPermissionKeys =
        new HashSet<string>(StringComparer.Ordinal)
        {
            ProductIdentityLifecycleOperationRecover
        };

    // WP-MDM-BRAND-ACCEPT-01 FIX2 (item 1, CT model A) — Brand is its own module (brand-product-master) and declares
    // mdm.brands.* itself; the product module no longer carries a Brands page, so its declared set has NO cross-module
    // key. The set stays (empty) so the exact-set rule still refuses a stray foreign key in this module's descriptor.
    public static readonly IReadOnlySet<string> DeclaredCrossModuleDependencyKeys =
        new HashSet<string>(StringComparer.Ordinal);

    public static readonly IReadOnlyList<ProductIdentityLifecycleRoleGrantTemplate> DedicatedRoles =
    [
        new(
            StewardRole,
            "Product Data Steward",
            "Creates and submits Product Identity records without checker or retirement authority.",
            BasePermissionKeys.Concat(
                    [
                        GlobalProductsSubmit,
                        GlobalProductsUpdate,
                        GlobalProductsWithdraw,
                        GlobalProductsRequestCorrection,
                        GskusSubmit,
                        GskusUpdate,
                        GskusWithdraw,
                        GskusRequestCorrection,
                        LskusSubmit,
                        LskusWithdraw,
                        FinishedGoodsSubmit,
                        WorkflowInstancesStart
                    ])
                .ToHashSet(StringComparer.Ordinal)),
        new(
            ApproverRole,
            "Product Identity Approver",
            "Reads Product Identity records and performs assigned decisions through native Workflow.",
            new HashSet<string>(StringComparer.Ordinal)
            {
                GlobalProductsRead,
                GskusRead,
                LskusRead,
                FinishedGoodsRead,
                WorkCenterInboxView,
                WorkflowTasksApprove,
                WorkflowTasksReject
            }),
        new(
            RetirementStewardRole,
            "Product Identity Retirement Steward",
            "Requests Global Product retirement and retires remaining Product Identity records subject to MDM fences.",
            new HashSet<string>(StringComparer.Ordinal)
            {
                GlobalProductsRead,
                GlobalProductsWithdraw,
                GlobalProductsRequestRetirement,
                WorkflowInstancesStart,
                GskusRead,
                // WP-MDM-GSKU-ACCEPT-01 FIX1 (B5): no longer granted — direct GSKU retirement is a system action
                // (DefaultRolePermissionTemplate.NonHumanPermissionKeys). A human retires through a request, which
                // is approved. The module sync removes this role's existing grant (it is no longer desired).
                GskusRequestRetirement,
                LskusRead,
                LskusRequestRetirement,
                FinishedGoodsRead
                // FG FIX1 item 9 (CT decision B): FinishedGoodsRetire no longer granted — a system action
                // (DefaultRolePermissionTemplate.NonHumanPermissionKeys); the sync removes the historical grant.
            })
    ];

    public static bool AppliesTo(string normalizedModuleCode, IEnumerable<string> permissionKeys)
        => string.Equals(normalizedModuleCode, ModuleCode, StringComparison.OrdinalIgnoreCase)
           && permissionKeys.Any(IsLifecycleCandidateKey);

    public static bool IsOwnedPermissionKey(string? permissionKey)
        => !string.IsNullOrWhiteSpace(permissionKey) && PermissionKeys.Contains(permissionKey);

    public static bool IsStockBehaviourPermissionKey(string? permissionKey)
        => !string.IsNullOrWhiteSpace(permissionKey) && StockBehaviourPermissionKeys.Contains(permissionKey);

    /// <summary>A key only the dedicated lifecycle roles receive: the owned lifecycle keys and the stock-behaviour keys.</summary>
    public static bool IsDedicatedOnlyPermissionKey(string? permissionKey)
        => IsOwnedPermissionKey(permissionKey) || IsStockBehaviourPermissionKey(permissionKey);

    /// <summary>
    /// The Steward's stock-behaviour grants for a declared key set: all three when the module declares them, none
    /// otherwise (the exact-set check has already refused a partial set).
    /// </summary>
    public static IReadOnlyList<string> DeclaredStockBehaviourKeys(IEnumerable<string> declaredKeys)
    {
        var declared = declaredKeys.ToHashSet(StringComparer.Ordinal);
        return StockBehaviourPermissionKeys.All(declared.Contains) ? StockBehaviourPermissionKeys.ToList() : [];
    }

    public static bool IsItemPermissionKey(string? permissionKey)
        => !string.IsNullOrWhiteSpace(permissionKey) && ItemPermissionKeys.Contains(permissionKey);

    public static bool IsProductMasterPermissionKey(string? permissionKey)
        => !string.IsNullOrWhiteSpace(permissionKey) && ProductMasterPermissionKeys.Contains(permissionKey);

    public static bool IsBasePermissionKey(string? permissionKey)
        => !string.IsNullOrWhiteSpace(permissionKey) && BasePermissionKeys.Contains(permissionKey);

    public static bool IsNonHumanPermissionKey(string? permissionKey)
        => !string.IsNullOrWhiteSpace(permissionKey) && NonHumanPermissionKeys.Contains(permissionKey);

    /// <summary>FIX2 (item 1) — a key brand-product-master owns (mdm.brands.*), never part of this module's set.</summary>
    public static bool IsBrandModuleKey(string? permissionKey)
        => !string.IsNullOrWhiteSpace(permissionKey)
           && permissionKey.StartsWith("mdm.brands.", StringComparison.OrdinalIgnoreCase);

    public static bool IsDeclaredCrossModuleDependencyKey(string? permissionKey)
        => !string.IsNullOrWhiteSpace(permissionKey)
           && DeclaredCrossModuleDependencyKeys.Contains(permissionKey);

    public static void ValidateExactDeclaredPermissionSet(IEnumerable<string> permissionKeys)
    {
        var supplied = permissionKeys.ToList();
        var lifecycle = supplied.Where(key => IsLifecycleCandidateKey(key) && !IsStockBehaviourPermissionKey(key))
            .ToHashSet(StringComparer.Ordinal);
        var stockBehaviour = supplied.Where(IsStockBehaviourPermissionKey).ToHashSet(StringComparer.Ordinal);
        var basePermissions = supplied.Where(IsBasePermissionKey).ToHashSet(StringComparer.Ordinal);
        var nonHumanPermissions = supplied.Where(IsNonHumanPermissionKey).ToHashSet(StringComparer.Ordinal);
        var crossModuleDependencies = supplied
            .Where(IsDeclaredCrossModuleDependencyKey)
            .ToHashSet(StringComparer.Ordinal);
        // BL-592: every key of the two resources, so an unknown action (mdm.items.reactivate, mdm.product-master.write) is
        // a mismatch, not a key the rule never looked at.
        var items = supplied.Where(key => key.StartsWith("mdm.items.", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.Ordinal);
        var productMaster = supplied.Where(key => key.StartsWith("mdm.product-master.", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.Ordinal);
        var relevantCount = supplied.Count(key =>
            IsLifecycleCandidateKey(key)
            || IsBasePermissionKey(key)
            || IsNonHumanPermissionKey(key)
            || IsDeclaredCrossModuleDependencyKey(key));

        if (!lifecycle.SetEquals(PermissionKeys)
            // BL-573 (CT decision A): none of the three, or all three — never one or two.
            || (stockBehaviour.Count != 0 && !stockBehaviour.SetEquals(StockBehaviourPermissionKeys))
            // BL-592: none of the six item keys, or all six — and the product-master read key alone, or nothing.
            || (items.Count != 0 && !items.SetEquals(ItemPermissionKeys))
            || (productMaster.Count != 0 && !productMaster.SetEquals(ProductMasterPermissionKeys))
            || !basePermissions.SetEquals(BasePermissionKeys)
            || !nonHumanPermissions.SetEquals(NonHumanPermissionKeys)
            || !crossModuleDependencies.SetEquals(DeclaredCrossModuleDependencyKeys)
            || relevantCount != PermissionKeys.Count
                + stockBehaviour.Count
                + BasePermissionKeys.Count
                + NonHumanPermissionKeys.Count
                + DeclaredCrossModuleDependencyKeys.Count
            || supplied.Any(key => key.StartsWith("mdm.product-definition-revisions.", StringComparison.OrdinalIgnoreCase))
            // FIX2 (item 1) — a Brand key in this module's descriptor is a stale manifest (Brand is brand-product-master).
            || supplied.Any(IsBrandModuleKey))
        {
            throw new InvalidOperationException(
                "Product Identity lifecycle reconciliation requires the exact eight base, eighteen lifecycle and one non-human recovery key — either without the three stock-behaviour edit keys or with exactly all three (mdm.gskus / mdm.lskus / mdm.finished-goods .edit-stock-behaviour), either without the item keys or with exactly all six (mdm.items .read / .create / .update / .submit / .deactivate / .retire), and mdm.product-master.read alone or nothing of that resource — with no Brand, Revision or product approve/reject key.");
        }
    }

    public static IReadOnlyDictionary<string, Permission> ValidateAndResolveDefinitions(
        IEnumerable<Permission> declaredModulePermissions,
        IEnumerable<Permission> globalCatalog)
    {
        var declared = declaredModulePermissions.ToList();
        ValidateExactDeclaredPermissionSet(declared.Select(permission => permission.Key));

        foreach (var permission in declared.Where(permission =>
                     IsBasePermissionKey(permission.Key)
                     || IsOwnedPermissionKey(permission.Key)
                     || IsStockBehaviourPermissionKey(permission.Key)
                     || IsItemPermissionKey(permission.Key)
                     || IsProductMasterPermissionKey(permission.Key)
                     || IsNonHumanPermissionKey(permission.Key)))
        {
            var separator = permission.Key.LastIndexOf('.');
            var expectedResource = permission.Key["mdm.".Length..separator];
            var expectedAction = permission.Key[(separator + 1)..];
            if (permission.IsDeleted
                || permission.Scope != PermissionScope.Tenant
                || !string.Equals(permission.Module, ModuleCode, StringComparison.Ordinal)
                || !string.Equals(permission.Resource, expectedResource, StringComparison.Ordinal)
                || !string.Equals(permission.Action, expectedAction, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Product Identity permission catalog definitions do not match the exact approved contract.");
            }
        }

        var dependencies = new Dictionary<string, Permission>(StringComparer.Ordinal);
        foreach (var key in SharedDependencyKeys)
        {
            var matches = globalCatalog.Where(permission =>
                    !permission.IsDeleted && string.Equals(permission.Key, key, StringComparison.Ordinal))
                .ToList();
            if (matches.Count != 1 || !IsValidSharedDependency(matches[0]))
            {
                throw new InvalidOperationException(
                    $"Product Identity lifecycle dependency '{key}' is missing or divergent.");
            }

            dependencies.Add(key, matches[0]);
        }

        return dependencies;
    }

    private static bool IsLifecycleCandidateKey(string? permissionKey)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            return false;
        }

        if (permissionKey.StartsWith("mdm.product-definition-revisions.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsProductIdentityResourceKey(permissionKey)
               && !IsBasePermissionKey(permissionKey);
    }

    private static bool IsProductIdentityResourceKey(string permissionKey)
        => permissionKey.StartsWith("mdm.global-products.", StringComparison.OrdinalIgnoreCase)
           || permissionKey.StartsWith("mdm.gskus.", StringComparison.OrdinalIgnoreCase)
           || permissionKey.StartsWith("mdm.lskus.", StringComparison.OrdinalIgnoreCase)
           || permissionKey.StartsWith("mdm.finished-goods.", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidSharedDependency(Permission permission)
    {
        if (permission.Scope != PermissionScope.Tenant)
        {
            return false;
        }

        return permission.Key switch
        {
            WorkCenterInboxView => string.Equals(permission.Module, "work-aggregation", StringComparison.Ordinal)
                                   && string.Equals(permission.Resource, "work-aggregation.inbox", StringComparison.Ordinal)
                                   && string.Equals(permission.Action, "view", StringComparison.Ordinal),
            WorkflowInstancesStart => string.Equals(permission.Module, "workflow", StringComparison.Ordinal)
                                      && string.Equals(permission.Resource, "workflow.instances", StringComparison.Ordinal)
                                      && string.Equals(permission.Action, "start", StringComparison.Ordinal),
            WorkflowTasksApprove => string.Equals(permission.Module, "workflow", StringComparison.Ordinal)
                                    && string.Equals(permission.Resource, "workflow.tasks", StringComparison.Ordinal)
                                    && string.Equals(permission.Action, "approve", StringComparison.Ordinal),
            WorkflowTasksReject => string.Equals(permission.Module, "workflow", StringComparison.Ordinal)
                                   && string.Equals(permission.Resource, "workflow.tasks", StringComparison.Ordinal)
                                   && string.Equals(permission.Action, "reject", StringComparison.Ordinal),
            _ => false
        };
    }
}

public sealed record ProductIdentityLifecycleRoleGrantTemplate(
    string RoleName,
    string DisplayName,
    string Description,
    IReadOnlySet<string> PermissionKeys);
