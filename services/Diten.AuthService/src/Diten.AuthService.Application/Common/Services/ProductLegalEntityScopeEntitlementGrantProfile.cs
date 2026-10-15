using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Services;

/// <summary>
/// Exact entitlement grant profile for Product Legal Entity Scope management and rollout permissions.
/// These permissions share the Product / Item / SKU Master entitlement but do not inherit its generic
/// Admin-full / Viewer-read role policy.
/// </summary>
public static class ProductLegalEntityScopeEntitlementGrantProfile
{
    public const string ModuleCode = "product-item-sku-master";
    public const string ManagementPrefix = "mdm.product-legal-entity-scopes.";
    public const string RolloutPrefix = "mdm.product-legal-entity-scope-rollout.";

    public const string Read = ManagementPrefix + "read";
    public const string Configure = ManagementPrefix + "configure";
    public const string Replace = ManagementPrefix + "replace";
    public const string End = ManagementPrefix + "end";
    public const string Activate = RolloutPrefix + "activate";
    public const string Rollback = RolloutPrefix + "rollback";

    public const string StewardRole = "ProductLegalEntityScopeSteward";
    public const string AuditorRole = "ProductLegalEntityScopeAuditor";
    public const string RolloutOperatorRole = "ProductLegalEntityScopeRolloutOperator";

    public static readonly IReadOnlySet<string> PermissionKeys =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Read,
            Configure,
            Replace,
            End,
            Activate,
            Rollback
        };

    public static readonly IReadOnlyList<ProductLegalEntityScopeRoleGrantTemplate> DedicatedRoles =
    [
        new(
            StewardRole,
            "Product Legal Entity Scope Steward",
            "Configures, replaces and ends Product Legal Entity scope assignments.",
            new HashSet<string>(StringComparer.Ordinal) { Read, Configure, Replace, End }),
        new(
            AuditorRole,
            "Product Legal Entity Scope Auditor",
            "Reads Product Legal Entity scope assignments and completeness projections without mutation authority.",
            new HashSet<string>(StringComparer.Ordinal) { Read }),
        new(
            RolloutOperatorRole,
            "Product Legal Entity Scope Rollout Operator",
            "Activates or rolls back controlled Product Legal Entity scope rollout.",
            new HashSet<string>(StringComparer.Ordinal) { Read, Activate, Rollback })
    ];

    public static bool AppliesTo(string normalizedModuleCode, IEnumerable<string> permissionKeys)
        => string.Equals(normalizedModuleCode, ModuleCode, StringComparison.OrdinalIgnoreCase)
           && permissionKeys.Any(IsProductLegalEntityScopeKey);

    public static void ValidateExactPermissionSet(IEnumerable<string> permissionKeys)
    {
        var supplied = permissionKeys
            .Where(IsProductLegalEntityScopeKey)
            .ToHashSet(StringComparer.Ordinal);

        if (!supplied.SetEquals(PermissionKeys))
        {
            throw new InvalidOperationException(
                "Product Legal Entity Scope entitlement reconciliation requires the exact six-key permission set.");
        }
    }

    public static void ValidateExactPermissionDefinitions(IEnumerable<Permission> permissions)
    {
        var supplied = permissions
            .Where(permission => IsProductLegalEntityScopeKey(permission.Key))
            .ToList();
        ValidateExactPermissionSet(supplied.Select(permission => permission.Key));

        foreach (var permission in supplied)
        {
            var expectedResource = permission.Key.StartsWith(ManagementPrefix, StringComparison.Ordinal)
                ? "product-legal-entity-scopes"
                : "product-legal-entity-scope-rollout";
            var expectedAction = permission.Key[(permission.Key.LastIndexOf('.') + 1)..];
            if (permission.IsDeleted
                || permission.Scope != PermissionScope.Tenant
                || !string.Equals(permission.Module, ModuleCode, StringComparison.Ordinal)
                || !string.Equals(permission.Resource, expectedResource, StringComparison.Ordinal)
                || !string.Equals(permission.Action, expectedAction, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Product Legal Entity Scope permission catalog definitions do not match the exact approved contract.");
            }
        }
    }

    public static bool IsProductLegalEntityScopeKey(string? permissionKey)
        => !string.IsNullOrWhiteSpace(permissionKey)
           && (permissionKey.StartsWith(ManagementPrefix, StringComparison.OrdinalIgnoreCase)
               || permissionKey.StartsWith(RolloutPrefix, StringComparison.OrdinalIgnoreCase));
}

public sealed record ProductLegalEntityScopeRoleGrantTemplate(
    string RoleName,
    string DisplayName,
    string Description,
    IReadOnlySet<string> PermissionKeys);
