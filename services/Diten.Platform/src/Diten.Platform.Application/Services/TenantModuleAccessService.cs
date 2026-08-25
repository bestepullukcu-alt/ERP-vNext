using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Services;

public sealed class TenantModuleAccessService : ITenantModuleAccessService
{
    private readonly ITenantModuleEntitlementRepository _entitlementRepository;
    private readonly IModuleCatalogRepository _moduleRepository;
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ISubscriptionPlanRepository _planRepository;

    public TenantModuleAccessService(
        ITenantModuleEntitlementRepository entitlementRepository,
        IModuleCatalogRepository moduleRepository,
        ITenantSubscriptionRepository subscriptionRepository,
        ISubscriptionPlanRepository planRepository)
    {
        _entitlementRepository = entitlementRepository;
        _moduleRepository = moduleRepository;
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
    }

    public async Task<bool> HasAccessAsync(Guid tenantId, string moduleCode, CancellationToken ct = default)
    {
        var detail = await GetEffectiveAccessDetailAsync(tenantId, moduleCode, ct);
        return detail.HasAccess;
    }

    public async Task<Domain.Enums.TenantModuleEffectiveAccess> GetEffectiveAccessAsync(Guid tenantId, string moduleCode, CancellationToken ct = default)
    {
        var detail = await GetEffectiveAccessDetailAsync(tenantId, moduleCode, ct);
        return detail.EffectiveAccess;
    }

    public async Task EnsureAccessOrThrowAsync(Guid tenantId, string moduleCode, CancellationToken ct = default)
    {
        if (!await HasAccessAsync(tenantId, moduleCode, ct))
        {
            throw new UnauthorizedAccessException($"Tenant does not have access to module '{moduleCode}'.");
        }
    }

    public async Task<TenantModuleEffectiveAccessDto> GetEffectiveAccessDetailAsync(Guid tenantId, string moduleCode, CancellationToken ct = default)
    {
        var normalizedCode = NormalizeModuleCode(moduleCode);
        var module = await _moduleRepository.GetByCodeAsync(normalizedCode, ct);

        // FEAT-BASELINE-MODULES — a baseline module is entitlement-free: every tenant automatically has access, so
        // the tenant-level entitlement check is bypassed here. The per-user permission gate (each page's
        // RequiredPermission, e.g. auth.*) still applies downstream — this only removes the tenant entitlement wall.
        if (module?.IsBaseline == true)
        {
            var displayName = module.DisplayName ?? module.ModuleName ?? normalizedCode;
            return new TenantModuleEffectiveAccessDto(
                tenantId, normalizedCode, displayName, "Baseline",
                Domain.Enums.TenantModuleEffectiveAccess.Active, true, null, null);
        }

        if (SystemTenantRules.IsSystemTenantId(tenantId))
        {
            if (module is null || module.IsDeleted || module.Status != ModuleCatalogStatus.Active || !module.IsTenantAssignable)
            {
                return new TenantModuleEffectiveAccessDto(
                    tenantId, normalizedCode, module?.DisplayName ?? module?.ModuleName ?? normalizedCode, "None",
                    TenantModuleEffectiveAccess.NoAccess, false, null, null);
            }

            var displayName = module.DisplayName ?? module.ModuleName ?? normalizedCode;
            return new TenantModuleEffectiveAccessDto(
                tenantId, normalizedCode, displayName, "PlatformSystemTenant",
                TenantModuleEffectiveAccess.Active, true, "PlatformSystemTenant", null);
        }

        var physicalRows = await _entitlementRepository.GetByTenantAndModuleAsync(tenantId, normalizedCode, ct);
        var planCodes = await GetPlanModuleCodesAsync(tenantId, ct);
        return TenantModuleEntitlementAccessEvaluator.Evaluate(
            tenantId,
            normalizedCode,
            module?.DisplayName ?? module?.ModuleName ?? normalizedCode,
            planCodes.Contains(normalizedCode, StringComparer.OrdinalIgnoreCase),
            module?.IsCoreModule == true,
            physicalRows,
            DateTimeOffset.UtcNow);
    }

    private async Task<IReadOnlyList<string>> GetPlanModuleCodesAsync(Guid tenantId, CancellationToken ct)
    {
        var subscription = await _subscriptionRepository.GetCurrentByTenantIdAsync(tenantId, ct);
        if (subscription is null)
        {
            return [];
        }

        var plan = await _planRepository.GetByIdAsync(subscription.PlanId, ct);
        return plan?.IncludedModuleKeys ?? [];
    }

    private static string NormalizeModuleCode(string moduleCode) => moduleCode.Trim().ToUpperInvariant();
}
