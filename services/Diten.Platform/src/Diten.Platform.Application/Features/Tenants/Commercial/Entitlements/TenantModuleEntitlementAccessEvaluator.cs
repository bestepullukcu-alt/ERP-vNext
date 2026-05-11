using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;

internal static class TenantModuleEntitlementAccessEvaluator
{
    private static readonly EntitlementSource[] PhysicalPrecedence =
    [
        EntitlementSource.System,
        EntitlementSource.ManualOverride,
        EntitlementSource.Addon,
        EntitlementSource.Trial
    ];

    public static TenantModuleEffectiveAccessDto Evaluate(
        Guid tenantId,
        string moduleCode,
        string moduleName,
        bool hasPlanAccess,
        bool isCoreModule,
        IReadOnlyList<TenantModuleEntitlement> physicalRows,
        DateTimeOffset now)
    {
        var activeRows = physicalRows
            .Where(x => !x.IsDeleted)
            .OrderBy(x => Array.IndexOf(PhysicalPrecedence, x.Source))
            .ToList();

        var system = activeRows.FirstOrDefault(x => x.Source == EntitlementSource.System);
        if (isCoreModule && system is { IsEnabled: true } && !IsExpired(system, now))
        {
            return Build(tenantId, moduleCode, moduleName, "System", TenantModuleEffectiveAccess.SystemLocked, true, system);
        }

        foreach (var row in activeRows.Where(x => x.Source != EntitlementSource.System))
        {
            if (IsExpired(row, now))
            {
                continue;
            }

            if (row.Source == EntitlementSource.ManualOverride)
            {
                return row.IsEnabled
                    ? Build(tenantId, moduleCode, moduleName, "ManualOverride", TenantModuleEffectiveAccess.EnabledByOverride, true, row)
                    : Build(tenantId, moduleCode, moduleName, "ManualOverride", TenantModuleEffectiveAccess.BlockedByOverride, false, row);
            }

            if (row.IsEnabled)
            {
                return Build(tenantId, moduleCode, moduleName, row.Source.ToString(), TenantModuleEffectiveAccess.Active, true, row);
            }
        }

        var expired = activeRows.FirstOrDefault(x => IsExpired(x, now));
        if (expired is not null && !hasPlanAccess)
        {
            return Build(tenantId, moduleCode, moduleName, expired.Source.ToString(), TenantModuleEffectiveAccess.Expired, false, expired);
        }

        return hasPlanAccess
            ? new TenantModuleEffectiveAccessDto(tenantId, moduleCode, moduleName, "Plan", TenantModuleEffectiveAccess.Active, true, null, null)
            : new TenantModuleEffectiveAccessDto(tenantId, moduleCode, moduleName, "None", TenantModuleEffectiveAccess.NoAccess, false, null, null);
    }

    public static bool IsExpired(TenantModuleEntitlement row, DateTimeOffset now) =>
        row.ExpiryDateUtc.HasValue && row.ExpiryDateUtc.Value <= now;

    private static TenantModuleEffectiveAccessDto Build(
        Guid tenantId,
        string moduleCode,
        string moduleName,
        string source,
        TenantModuleEffectiveAccess access,
        bool hasAccess,
        TenantModuleEntitlement row) =>
        new(tenantId, moduleCode, moduleName, source, access, hasAccess, row.Reason, row.ExpiryDateUtc);
}
