using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Audit;

public sealed class AuditRetentionPolicyResolver : IAuditRetentionPolicyResolver
{
    private readonly IAuditRetentionPolicyRepository _retentionPolicies;
    private readonly ITenantAuditPreferenceRepository _tenantPreferences;

    public AuditRetentionPolicyResolver(
        IAuditRetentionPolicyRepository retentionPolicies,
        ITenantAuditPreferenceRepository tenantPreferences)
    {
        _retentionPolicies = retentionPolicies;
        _tenantPreferences = tenantPreferences;
    }

    public async Task<AuditRetentionResolution> ResolveAsync(AuditRetentionResolutionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Category == AuditCategory.Unknown)
        {
            throw new ArgumentException("Audit retention category is required.", nameof(request));
        }

        if (request.TenantId == Guid.Empty)
        {
            throw new ArgumentException("Audit retention tenant id cannot be empty.", nameof(request));
        }

        var requestedPlanTierCode = NormalizePlanTierCode(request.PlanTierCode);
        var policy = await ResolvePolicyAsync(request.Category, requestedPlanTierCode, ct);
        policy.Validate();

        var tenantPreference = request.TenantId.HasValue && policy.AllowTenantOverride
            ? await _tenantPreferences.GetByTenantAndCategoryAsync(request.TenantId.Value, request.Category, ct)
            : null;

        var tenantPreferenceApplied = tenantPreference is not null && policy.AllowTenantOverride;
        var effectiveRetentionDays = tenantPreferenceApplied
            ? Math.Clamp(tenantPreference!.RetentionDays, policy.MinimumRetentionDays, policy.MaximumRetentionDays)
            : policy.DefaultRetentionDays;

        return new AuditRetentionResolution(
            request.Category,
            requestedPlanTierCode,
            policy.PlanTierCode,
            policy.MinimumRetentionDays,
            policy.DefaultRetentionDays,
            policy.MaximumRetentionDays,
            effectiveRetentionDays,
            policy.HotStorageDays,
            policy.ColdStoragePrepared,
            policy.AllowTenantOverride,
            tenantPreferenceApplied,
            !string.Equals(policy.PlanTierCode, requestedPlanTierCode, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<AuditEventRetentionPolicy> ResolvePolicyAsync(AuditCategory category, string requestedPlanTierCode, CancellationToken ct)
    {
        AuditEventRetentionPolicy? policy = null;

        if (!string.Equals(requestedPlanTierCode, AuditEventRetentionPolicy.DefaultPlanTierCode, StringComparison.OrdinalIgnoreCase))
        {
            policy = await _retentionPolicies.GetActivePolicyAsync(category, requestedPlanTierCode, ct);
        }

        policy ??= await _retentionPolicies.GetDefaultPolicyAsync(category, ct);

        return policy ?? throw new InvalidOperationException($"No active audit retention policy exists for category '{category}'.");
    }

    private static string NormalizePlanTierCode(string? planTierCode)
    {
        return string.IsNullOrWhiteSpace(planTierCode)
            ? AuditEventRetentionPolicy.DefaultPlanTierCode
            : planTierCode.Trim();
    }
}
