using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;

internal static class TenantModuleEntitlementCommandSupport
{
    public static string NormalizeModuleCode(string moduleCode) => moduleCode.Trim().ToUpperInvariant();

    public static async Task<(bool IsValid, string? Error, int StatusCode)> ValidateModuleAsync(
        IModuleCatalogRepository moduleRepository,
        string moduleCode,
        CancellationToken ct)
    {
        var module = await moduleRepository.GetByCodeAsync(NormalizeModuleCode(moduleCode), ct);
        if (module is null)
        {
            return (false, "Module was not found.", 404);
        }

        // FEAT-BASELINE-MODULES — defense in depth: a baseline module is entitlement-free (every tenant auto-has it),
        // so it must never receive a manual entitlement row. Reject even a direct API call. Keys off IsBaseline, not
        // a hardcoded code list, so any future baseline module is guarded automatically.
        if (module.IsBaseline)
        {
            return (false, "Baseline modules are entitlement-free and cannot be manually entitled.", 409);
        }

        return (true, null, 0);
    }

    // FIX-ENTITLEMENT-DUP (Fix 2) — duplicate is decided PER MODULE, Source-independent. If the tenant already has
    // an ACTIVE (IsEnabled & !IsDeleted) entitlement for this module under ANY source, reject (409). Soft-deleted
    // rows are already excluded by the repository's execution filter, and disabled rows are ignored here, so a
    // re-enable / re-add after disable or delete still works.
    public static async Task<(bool IsValid, string? Error, int StatusCode)> ValidateDuplicateAsync(
        ITenantModuleEntitlementRepository repository,
        Guid tenantId,
        string moduleCode,
        Guid? excludeId,
        CancellationToken ct)
    {
        var existing = await repository.GetByTenantAndModuleAsync(tenantId, NormalizeModuleCode(moduleCode), ct);
        var hasActiveConflict = existing.Any(x =>
            x.IsEnabled && (!excludeId.HasValue || x.Id != excludeId.Value));

        return hasActiveConflict
            ? (false, "This module is already entitled for the tenant.", 409)
            : (true, null, 0);
    }

    public static Response<NoContent> ConcurrencyFailure() =>
        Response<NoContent>.Fail("Entitlement was modified by another process.", 409);

    public static TenantModuleEntitlement CreateManualOverride(Guid tenantId, string moduleCode, bool isEnabled, string reason) => new()
    {
        TenantId = tenantId,
        ModuleCode = NormalizeModuleCode(moduleCode),
        Source = EntitlementSource.ManualOverride,
        IsEnabled = isEnabled,
        Reason = reason
    };
}
