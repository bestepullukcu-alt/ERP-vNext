using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Services;

/// <summary>
/// Exact, provisioning-only entitlement profile for orphaned Product Identity lifecycle recovery.
/// This permission is deliberately isolated from default and lifecycle responsibility roles.
/// </summary>
public static class ProductIdentityRecoveryOperatorEntitlementGrantProfile
{
    public const string ModuleCode = "product-item-sku-master";
    public const string PermissionKey = "mdm.product-identity.lifecycle-operations.recover";
    public const string Resource = "product-identity.lifecycle-operations";
    public const string Action = "recover";
    public const string RoleName = "ProductIdentityRecoveryOperator";
    public const string DisplayName = "Product Identity Recovery Operator";
    public const string Description =
        "Recovers orphaned Product Identity lifecycle operations before Workflow start.";

    public static bool AppliesTo(string normalizedModuleCode, IEnumerable<string> permissionKeys)
        => string.Equals(normalizedModuleCode, ModuleCode, StringComparison.OrdinalIgnoreCase)
           && permissionKeys.Any(IsRecoveryPermissionKey);

    public static bool IsRecoveryPermissionKey(string? permissionKey)
        => string.Equals(permissionKey, PermissionKey, StringComparison.Ordinal);

    public static void ValidateExactPermissionSet(IEnumerable<string> permissionKeys)
    {
        var supplied = permissionKeys.Where(IsRecoveryPermissionKey).ToList();
        if (supplied.Count != 1 || !string.Equals(supplied[0], PermissionKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Product Identity recovery reconciliation requires the exact singleton permission key.");
        }
    }

    public static Permission ValidateAndResolveDefinition(IEnumerable<Permission> permissions)
    {
        var supplied = permissions.Where(permission => IsRecoveryPermissionKey(permission.Key)).ToList();
        ValidateExactPermissionSet(supplied.Select(permission => permission.Key));
        var permission = supplied[0];
        if (permission.IsDeleted
            || permission.Scope != PermissionScope.Tenant
            || !string.Equals(permission.Module, ModuleCode, StringComparison.Ordinal)
            || !string.Equals(permission.Resource, Resource, StringComparison.Ordinal)
            || !string.Equals(permission.Action, Action, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Product Identity recovery permission definition does not match the exact approved contract.");
        }

        return permission;
    }
}
