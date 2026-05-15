using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Contracts.Audit;

public sealed record AuditRetentionResolution(
    AuditCategory Category,
    string RequestedPlanTierCode,
    string EffectivePlanTierCode,
    int MinimumRetentionDays,
    int DefaultRetentionDays,
    int MaximumRetentionDays,
    int EffectiveRetentionDays,
    int HotStorageDays,
    bool ColdStoragePrepared,
    bool AllowTenantOverride,
    bool TenantPreferenceApplied,
    bool UsedDefaultPolicyFallback);
