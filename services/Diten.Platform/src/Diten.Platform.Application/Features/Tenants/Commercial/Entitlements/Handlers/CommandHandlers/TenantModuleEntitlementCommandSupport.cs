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
        return module is null
            ? (false, "Module was not found.", 404)
            : (true, null, 0);
    }

    public static async Task<(bool IsValid, string? Error, int StatusCode)> ValidateDuplicateAsync(
        ITenantModuleEntitlementRepository repository,
        Guid tenantId,
        string moduleCode,
        EntitlementSource source,
        Guid? excludeId,
        CancellationToken ct)
    {
        var duplicate = await repository.GetActiveBySourceAsync(tenantId, moduleCode, source, excludeId, ct);
        return duplicate is null
            ? (true, null, 0)
            : (false, "An active entitlement already exists for this tenant, module and source.", 409);
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
