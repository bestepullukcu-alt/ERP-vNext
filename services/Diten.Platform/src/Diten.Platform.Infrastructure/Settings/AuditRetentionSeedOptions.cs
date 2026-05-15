namespace Diten.Platform.Infrastructure.Settings;

public sealed class AuditRetentionSeedOptions
{
    public const string SectionName = "AuditRetentionSeed";

    public int MinimumRetentionDays { get; set; }
    public int DefaultRetentionDays { get; set; }
    public int MaximumRetentionDays { get; set; }
    public int HotStorageDays { get; set; }
    public bool AllowTenantOverride { get; set; }
    public bool ColdStoragePrepared { get; set; }

    public void Validate()
    {
        if (MinimumRetentionDays <= 0)
        {
            throw new InvalidOperationException("AuditRetentionSeed:MinimumRetentionDays must be greater than zero.");
        }

        if (DefaultRetentionDays < MinimumRetentionDays)
        {
            throw new InvalidOperationException("AuditRetentionSeed:DefaultRetentionDays must be greater than or equal to MinimumRetentionDays.");
        }

        if (MaximumRetentionDays < DefaultRetentionDays)
        {
            throw new InvalidOperationException("AuditRetentionSeed:MaximumRetentionDays must be greater than or equal to DefaultRetentionDays.");
        }

        if (HotStorageDays <= 0 || HotStorageDays > DefaultRetentionDays)
        {
            throw new InvalidOperationException("AuditRetentionSeed:HotStorageDays must be greater than zero and cannot exceed DefaultRetentionDays.");
        }
    }
}
