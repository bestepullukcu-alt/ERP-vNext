namespace Diten.Platform.API.Configuration;

public sealed class AuditOutboxTemporalStorageMigrationOptions
{
    public const string SectionName = "AuditOutboxTemporalStorageMigration";
    public const string ExactMigrationId = "audit-outbox-temporal-storage-v1";
    public const int ExactTargetVersion = 1;

    public bool Enabled { get; init; }
    public string MigrationId { get; init; } = ExactMigrationId;
    public int TargetVersion { get; init; } = ExactTargetVersion;
    public int BatchSize { get; init; } = 100;
    public int LeaseDurationSeconds { get; init; } = 60;
    public string LeaseOwner { get; init; } = string.Empty;
    public bool ActivateScalarClaims { get; init; }
    public string SelectedIndexName { get; init; } = string.Empty;

    public void Validate(string environmentName)
    {
        if (!Enabled)
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_MIGRATION_DISABLED");
        }

        if (!string.Equals(environmentName, "Development", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_MIGRATION_ENVIRONMENT_NOT_ALLOWED");
        }

        if (!string.Equals(MigrationId, ExactMigrationId, StringComparison.Ordinal)
            || TargetVersion != ExactTargetVersion)
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_MIGRATION_FACTS_INVALID");
        }

        if (BatchSize is < 1 or > 1000)
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_MIGRATION_BATCH_INVALID");
        }

        if (LeaseDurationSeconds is < 10 or > 900)
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_MIGRATION_LEASE_INVALID");
        }

        if (string.IsNullOrWhiteSpace(LeaseOwner) || LeaseOwner.Length > 128)
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_MIGRATION_OWNER_INVALID");
        }

        if (!string.Equals(
                SelectedIndexName,
                "ix_audit_outbox_status_next_attempt_ticks_v1_created_ticks_v1_id",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_MIGRATION_INDEX_REQUIRED");
        }
    }
}
