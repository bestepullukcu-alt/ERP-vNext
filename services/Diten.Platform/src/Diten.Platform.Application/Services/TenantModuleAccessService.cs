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

        // BL-059 / main — the platform system tenant passes the tenant ENTITLEMENT gate for every catalog module
        // that is active and tenant-assignable, so each newly self-registered module shows up for platform admins
        // without an operator opening an entitlement row by hand. Deliberately narrow:
        //   · only the EXACT SystemTenantRules.PlatformSystemTenantId — customer tenants are untouched,
        //   · the module state is re-verified HERE (exists · not soft-deleted · Active · IsTenantAssignable) rather
        //     than trusting a caller-side filter such as the navigation handler's GetAssignableModulesAsync: this
        //     service is reached from several call paths, and a filter that loosens later must not silently widen
        //     the bypass,
        //   · IsBaseline semantics are unchanged — this is a separate access reason, not a baseline promotion
        //     (baseline is evaluated above and keeps reporting "Baseline"),
        //   · like baseline, this only removes the tenant entitlement wall. The per-user permission gate (each
        //     page's RequiredPermission) still applies downstream, as do domain SoD / maker-checker rules.
        //
        // ⚠ MERGE 2026-08-26 — BOTH BRANCHES BUILT THIS INDEPENDENTLY, and main's CONTROL FLOW is the one kept.
        // The two agreed on the outcome (an invalid module grants nothing) and disagreed on the path: this branch
        // fell through to the entitlement lookup for an invalid module, main returns NoAccess here and reads
        // nothing. Main's is both stricter — a stale entitlement row could not resurrect an inactive or
        // soft-deleted module — and the only version under which its structural guard
        // (`VerifyNoEntitlementOrPlanReads`) can be true. This branch's explanation, which used to travel as the
        // runtime Reason string, is the comment you are reading; Reason stays the short stable token main's tests
        // assert on, because a reason is an identifier the caller matches, not prose for a human.
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
