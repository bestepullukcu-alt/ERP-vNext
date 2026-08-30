using Diten.AuthService.Application.Common.Interfaces;

namespace Diten.AuthService.Application.Common.Authorization;

public sealed class TenantEffectivePermissionResolver : ITenantEffectivePermissionResolver
{
    private readonly ITenantEntitlementClient _entitlementClient;
    private readonly IPpmEntitlementPermissionPolicy _ppmPolicy;

    public TenantEffectivePermissionResolver(
        ITenantEntitlementClient entitlementClient,
        IPpmEntitlementPermissionPolicy ppmPolicy)
    {
        _entitlementClient = entitlementClient;
        _ppmPolicy = ppmPolicy;
    }

    public async Task<IReadOnlyList<string>> ResolveAsync(
        Guid tenantId,
        IEnumerable<string>? rolePermissions,
        CancellationToken cancellationToken)
    {
        var permissions = rolePermissions?
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];

        var ppmPermissions = permissions
            .Where(_ppmPolicy.AppliesToPermission)
            .ToArray();

        if (ppmPermissions.Length == 0)
        {
            return permissions;
        }

        var nonPpmPermissions = permissions
            .Where(permission => !_ppmPolicy.AppliesToPermission(permission))
            .ToList();

        var entitlement = await _entitlementClient
            .ReadEntitledModulesWithPermissionKeysAsync(tenantId, cancellationToken);

        if (!entitlement.IsAuthoritative)
        {
            return nonPpmPermissions;
        }

        var ppmModules = entitlement.Modules
            .Where(module => _ppmPolicy.Applies(module.ModuleCode))
            .Take(2)
            .ToArray();

        if (ppmModules.Length != 1 || ppmModules[0].PermissionKeys is null)
        {
            return nonPpmPermissions;
        }

        var entitledKeys = ppmModules[0].PermissionKeys
            .Where(_ppmPolicy.IsCanonicalPermission)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        nonPpmPermissions.AddRange(ppmPermissions.Where(entitledKeys.Contains));
        return nonPpmPermissions;
    }
}
