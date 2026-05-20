using Diten.Platform.Application.Features.SubscriptionFeatures;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Services;

public sealed class EntitlementChecker : IEntitlementChecker
{
    private const string InvalidModuleCode = "__INVALID_MODULE_CODE__";
    private const string InvalidFeatureCode = "__INVALID_FEATURE_CODE__";

    private readonly ITenantModuleAccessService moduleAccessService;
    private readonly ITenantSubscriptionRepository tenantSubscriptionRepository;
    private readonly ISubscriptionPlanRepository subscriptionPlanRepository;
    private readonly EntitlementCacheService cacheService;

    public EntitlementChecker(
        ITenantModuleAccessService moduleAccessService,
        ITenantSubscriptionRepository tenantSubscriptionRepository,
        ISubscriptionPlanRepository subscriptionPlanRepository,
        EntitlementCacheService cacheService)
    {
        this.moduleAccessService = moduleAccessService ?? throw new ArgumentNullException(nameof(moduleAccessService));
        this.tenantSubscriptionRepository = tenantSubscriptionRepository ?? throw new ArgumentNullException(nameof(tenantSubscriptionRepository));
        this.subscriptionPlanRepository = subscriptionPlanRepository ?? throw new ArgumentNullException(nameof(subscriptionPlanRepository));
        this.cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    }

    public Task<EntitlementCheckResult> IsModuleEntitledAsync(
        Guid tenantId,
        string moduleCode,
        CancellationToken ct)
    {
        var normalizedCode = NormalizeCode(moduleCode);
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(normalizedCode))
        {
            return Task.FromResult(EntitlementCheckResult.Denied(
                EntitlementKind.Module,
                string.IsNullOrWhiteSpace(normalizedCode) ? InvalidModuleCode : normalizedCode,
                EntitlementDenyReason.ModuleNotEntitled));
        }

        return cacheService.GetOrCreateModuleAsync(
            tenantId,
            normalizedCode,
            () => ResolveModuleEntitlementAsync(tenantId, normalizedCode, ct));
    }

    public Task<EntitlementCheckResult> IsFeatureEnabledAsync(
        Guid tenantId,
        string featureCode,
        CancellationToken ct)
    {
        var normalizedCode = NormalizeFeatureCode(featureCode);
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(normalizedCode))
        {
            return Task.FromResult(EntitlementCheckResult.Denied(
                EntitlementKind.Feature,
                string.IsNullOrWhiteSpace(normalizedCode) ? InvalidFeatureCode : normalizedCode,
                EntitlementDenyReason.FeatureNotEnabled));
        }

        return cacheService.GetOrCreateFeatureAsync(
            tenantId,
            normalizedCode,
            () => ResolveFeatureEntitlementAsync(tenantId, normalizedCode, ct));
    }

    public async Task<IReadOnlyList<EntitlementCheckResult>> CheckBatchAsync(
        Guid tenantId,
        IEnumerable<(string code, EntitlementKind kind)> targets,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(targets);

        var results = new List<EntitlementCheckResult>();

        // TODO MOD-0018: replace this safe minimal loop with a grouped batch read once
        // module and feature entitlement repositories expose batch contracts.
        foreach (var (code, kind) in targets)
        {
            var result = kind == EntitlementKind.Module
                ? await IsModuleEntitledAsync(tenantId, code, ct)
                : await IsFeatureEnabledAsync(tenantId, code, ct);
            results.Add(result);
        }

        return results;
    }

    private async Task<EntitlementCheckResult> ResolveModuleEntitlementAsync(
        Guid tenantId,
        string moduleCode,
        CancellationToken ct)
    {
        try
        {
            var detail = await moduleAccessService.GetEffectiveAccessDetailAsync(tenantId, moduleCode, ct);
            if (detail.HasAccess)
            {
                return EntitlementCheckResult.Allowed(
                    EntitlementKind.Module,
                    detail.ModuleCode,
                    detail.ExpiryDateUtc);
            }

            return EntitlementCheckResult.Denied(
                EntitlementKind.Module,
                detail.ModuleCode,
                MapModuleDenyReason(detail.EffectiveAccess),
                detail.ExpiryDateUtc);
        }
        catch
        {
            return EntitlementCheckResult.Denied(
                EntitlementKind.Module,
                moduleCode,
                EntitlementDenyReason.ModuleNotEntitled,
                isCacheable: false);
        }
    }

    private async Task<EntitlementCheckResult> ResolveFeatureEntitlementAsync(
        Guid tenantId,
        string featureCode,
        CancellationToken ct)
    {
        try
        {
            var subscription = await tenantSubscriptionRepository.GetCurrentByTenantIdAsync(tenantId, ct);
            if (subscription is null)
            {
                return EntitlementCheckResult.Denied(
                    EntitlementKind.Feature,
                    featureCode,
                    EntitlementDenyReason.FeatureNotEnabled);
            }

            var plan = await subscriptionPlanRepository.GetByIdAsync(subscription.PlanId, ct);
            var includedFeatures = plan?.IncludedFeatures ?? [];
            var isIncluded = includedFeatures.Any(x =>
                string.Equals(NormalizeFeatureCode(x), featureCode, StringComparison.OrdinalIgnoreCase));

            return isIncluded
                ? EntitlementCheckResult.Allowed(EntitlementKind.Feature, featureCode)
                : EntitlementCheckResult.Denied(
                    EntitlementKind.Feature,
                    featureCode,
                    EntitlementDenyReason.FeatureNotEnabled);
        }
        catch
        {
            return EntitlementCheckResult.Denied(
                EntitlementKind.Feature,
                featureCode,
                EntitlementDenyReason.FeatureNotEnabled,
                isCacheable: false);
        }
    }

    private static EntitlementDenyReason MapModuleDenyReason(TenantModuleEffectiveAccess effectiveAccess) =>
        effectiveAccess switch
        {
            TenantModuleEffectiveAccess.Expired => EntitlementDenyReason.EntitlementExpired,
            TenantModuleEffectiveAccess.BlockedByOverride => EntitlementDenyReason.EntitlementDisabled,
            _ => EntitlementDenyReason.ModuleNotEntitled
        };

    private static string NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();

    private static string NormalizeFeatureCode(string? code) =>
        SubscriptionFeatureCodeNormalizer.Normalize(code);
}
