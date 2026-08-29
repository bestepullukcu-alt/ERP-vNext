using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Services;

/// <summary>
/// Exact entitlement grant profile for Product Identity lifecycle permissions. Approval and rejection remain
/// Workflow-owned; this profile owns only submit/retire and consumes the existing Workflow catalog dependencies.
/// </summary>
public static class ProductIdentityLifecycleEntitlementGrantProfile
{
    public const string ModuleCode = "product-item-sku-master";

    public const string GlobalProductsRead = "mdm.global-products.read";
    public const string GlobalProductsCreate = "mdm.global-products.create";
    public const string GlobalProductsSubmit = "mdm.global-products.submit";
    public const string GlobalProductsRetire = "mdm.global-products.retire";
    public const string GskusRead = "mdm.gskus.read";
    public const string GskusCreate = "mdm.gskus.create";
    public const string GskusSubmit = "mdm.gskus.submit";
    public const string GskusRetire = "mdm.gskus.retire";
    public const string LskusRead = "mdm.lskus.read";
    public const string LskusCreate = "mdm.lskus.create";
    public const string LskusSubmit = "mdm.lskus.submit";
    public const string LskusRetire = "mdm.lskus.retire";
    public const string FinishedGoodsRead = "mdm.finished-goods.read";
    public const string FinishedGoodsCreate = "mdm.finished-goods.create";
    public const string FinishedGoodsSubmit = "mdm.finished-goods.submit";
    public const string FinishedGoodsRetire = "mdm.finished-goods.retire";

    public const string WorkCenterInboxView = "platform.work-aggregation.inbox.view";
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
            GskusSubmit,
            GskusRetire,
            LskusSubmit,
            LskusRetire,
            FinishedGoodsSubmit,
            FinishedGoodsRetire
        };

    public static readonly IReadOnlySet<string> SharedDependencyKeys =
        new HashSet<string>(StringComparer.Ordinal)
        {
            WorkCenterInboxView,
            WorkflowTasksApprove,
            WorkflowTasksReject
        };

    public static readonly IReadOnlyList<ProductIdentityLifecycleRoleGrantTemplate> DedicatedRoles =
    [
        new(
            StewardRole,
            "Product Data Steward",
            "Creates and submits Product Identity records without checker or retirement authority.",
            BasePermissionKeys.Concat(
                    [GlobalProductsSubmit, GskusSubmit, LskusSubmit, FinishedGoodsSubmit])
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
            "Retires Product Identity records subject to MDM child-admission and version fences.",
            new HashSet<string>(StringComparer.Ordinal)
            {
                GlobalProductsRead,
                GlobalProductsRetire,
                GskusRead,
                GskusRetire,
                LskusRead,
                LskusRetire,
                FinishedGoodsRead,
                FinishedGoodsRetire
            })
    ];

    public static bool AppliesTo(string normalizedModuleCode, IEnumerable<string> permissionKeys)
        => string.Equals(normalizedModuleCode, ModuleCode, StringComparison.OrdinalIgnoreCase)
           && permissionKeys.Any(IsLifecycleCandidateKey);

    public static bool IsOwnedPermissionKey(string? permissionKey)
        => !string.IsNullOrWhiteSpace(permissionKey) && PermissionKeys.Contains(permissionKey);

    public static bool IsBasePermissionKey(string? permissionKey)
        => !string.IsNullOrWhiteSpace(permissionKey) && BasePermissionKeys.Contains(permissionKey);

    public static void ValidateExactDeclaredPermissionSet(IEnumerable<string> permissionKeys)
    {
        var supplied = permissionKeys.ToList();
        var lifecycle = supplied.Where(IsLifecycleCandidateKey).ToHashSet(StringComparer.Ordinal);
        var basePermissions = supplied.Where(IsBasePermissionKey).ToHashSet(StringComparer.Ordinal);

        if (!lifecycle.SetEquals(PermissionKeys)
            || !basePermissions.SetEquals(BasePermissionKeys)
            || supplied.Any(key => key.StartsWith("mdm.product-definition-revisions.", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Product Identity lifecycle reconciliation requires the exact eight base and eight submit/retire keys, with no Revision or product approve/reject key.");
        }
    }

    public static IReadOnlyDictionary<string, Permission> ValidateAndResolveDefinitions(
        IEnumerable<Permission> declaredModulePermissions,
        IEnumerable<Permission> globalCatalog)
    {
        var declared = declaredModulePermissions.ToList();
        ValidateExactDeclaredPermissionSet(declared.Select(permission => permission.Key));

        foreach (var permission in declared.Where(permission =>
                     IsBasePermissionKey(permission.Key) || IsOwnedPermissionKey(permission.Key)))
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
