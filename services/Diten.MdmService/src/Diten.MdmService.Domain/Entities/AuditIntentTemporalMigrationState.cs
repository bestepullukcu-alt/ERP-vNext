namespace Diten.MdmService.Domain.Entities;

public sealed class AuditIntentTemporalMigrationState
{
    public const string ExactId = "mdm-audit-intent-temporal-v1";
    public const string ExactIndexSuffix = "tenant_audit_delivery_ticks_v1";

    public string Id { get; init; } = ExactId;
    public int TargetVersion { get; init; } = AuditIntentTemporalStorage.CurrentVersion;
    public string Phase { get; set; } = Phases.Ready;
    public string? LeaseOwner { get; set; }
    public long LeaseGeneration { get; set; }
    public long? LeaseExpiresAtUtcTicks { get; set; }
    public int CollectionOrdinal { get; set; }
    public Guid? LastAggregateId { get; set; }
    public Guid? LastIntentId { get; set; }
    public List<AuditIntentTemporalCollectionBoundary> CollectionBoundaries { get; set; } = [];
    public long ScannedCount { get; set; }
    public long MigratedCount { get; set; }
    public long AlreadyCurrentCount { get; set; }
    public long UpdatedAtUtcTicks { get; set; }
    public long? CompletedAtUtcTicks { get; set; }
    public string? FailureCode { get; set; }
    public int? ActivationVersion { get; set; }
    public string? IndexEvidenceFingerprint { get; set; }

    public static class Phases
    {
        public const string Ready = "Ready";
        public const string Migrating = "Migrating";
        public const string RecoveryRequired = "RecoveryRequired";
        public const string Completed = "Completed";
        public const string CompletionVerified = "CompletionVerified";
        public const string CutoverActive = "CutoverActive";
        public const string Failed = "Failed";

        public static bool IsKnown(string phase) => phase is
            Ready or Migrating or RecoveryRequired or Completed or CompletionVerified or CutoverActive or Failed;
    }

    public void Validate()
    {
        if (!string.Equals(Id, ExactId, StringComparison.Ordinal)
            || TargetVersion != AuditIntentTemporalStorage.CurrentVersion
            || !Phases.IsKnown(Phase)
            || LeaseGeneration < 0
            || CollectionOrdinal is < 0 or > 8
            || ScannedCount < 0
            || MigratedCount < 0
            || AlreadyCurrentCount < 0
            || ScannedCount != MigratedCount + AlreadyCurrentCount
            || UpdatedAtUtcTicks <= 0)
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_MIGRATION_STATE_INVALID");
        }

        if (CollectionBoundaries.Count != 8
            || CollectionBoundaries.Select(item => item.CollectionOrdinal).Distinct().Count() != 8
            || CollectionBoundaries.Any(item => item.CollectionOrdinal is < 0 or > 7))
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_MIGRATION_BOUNDARY_INVALID");
        }

        if (Phase is Phases.Ready or Phases.RecoveryRequired or Phases.Completed or Phases.CompletionVerified or Phases.CutoverActive
            && (LeaseOwner is not null || LeaseExpiresAtUtcTicks.HasValue))
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_MIGRATION_TERMINAL_LEASE_INVALID");
        }

        if (Phase == Phases.Migrating
            && (string.IsNullOrWhiteSpace(LeaseOwner) || !LeaseExpiresAtUtcTicks.HasValue))
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_MIGRATION_LEASE_INVALID");
        }

        if (Phase is Phases.Completed or Phases.CompletionVerified or Phases.CutoverActive
            && (CollectionOrdinal != 8 || !CompletedAtUtcTicks.HasValue))
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_MIGRATION_COMPLETION_INVALID");
        }

        if (Phase is Phases.CompletionVerified or Phases.CutoverActive
            && string.IsNullOrWhiteSpace(IndexEvidenceFingerprint))
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_INDEX_EVIDENCE_INVALID");
        }

        if (Phase == Phases.CutoverActive
            && (ActivationVersion != AuditIntentTemporalStorage.CurrentVersion
                || string.IsNullOrWhiteSpace(IndexEvidenceFingerprint)))
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_CUTOVER_STATE_INVALID");
        }
    }
}
public sealed class AuditIntentTemporalCollectionBoundary
{
    public int CollectionOrdinal { get; set; }
    public Guid UpperAggregateId { get; set; }
}
