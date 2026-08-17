using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.Audit;

public sealed class AuditEventRetentionPolicy : GlobalEntity
{
    public const string DefaultPlanTierCode = "Default";

    public AuditCategory Category { get; set; }
    public string PlanTierCode { get; set; } = DefaultPlanTierCode;
    public int MinimumRetentionDays { get; set; }
    public int DefaultRetentionDays { get; set; }
    public int MaximumRetentionDays { get; set; }
    public int HotStorageDays { get; set; }
    public bool ColdStoragePrepared { get; set; } = true;
    public bool AllowTenantOverride { get; set; }
    public bool IsActive { get; set; } = true;

    public void Validate()
    {
        if (Category == AuditCategory.Unknown)
        {
            throw new InvalidOperationException("Audit retention policy category is required.");
        }

        if (string.IsNullOrWhiteSpace(PlanTierCode))
        {
            throw new InvalidOperationException("Audit retention policy plan tier code is required.");
        }

        if (MinimumRetentionDays <= 0)
        {
            throw new InvalidOperationException("Audit retention policy minimum retention days must be greater than zero.");
        }

        if (MaximumRetentionDays < MinimumRetentionDays)
        {
            throw new InvalidOperationException("Audit retention policy maximum retention days must be greater than or equal to the minimum.");
        }

        if (DefaultRetentionDays < MinimumRetentionDays || DefaultRetentionDays > MaximumRetentionDays)
        {
            throw new InvalidOperationException("Audit retention policy default retention days must be within the floor and ceiling.");
        }

        if (HotStorageDays <= 0)
        {
            throw new InvalidOperationException("Audit retention policy hot storage days must be greater than zero.");
        }

        if (HotStorageDays > DefaultRetentionDays)
        {
            throw new InvalidOperationException("Audit retention policy hot storage days cannot exceed default retention days.");
        }
    }
}
