using MongoDB.Bson.Serialization.Attributes;

namespace Diten.Platform.Infrastructure.Persistence.Models;

public sealed class AuditOutboxTemporalMigrationState
{
    public const string ExactId = "audit-outbox-temporal-storage-v1";
    public const int ExactTargetVersion = 1;
    public const string ExactScalarClaimIndexName = "ix_audit_outbox_status_next_attempt_ticks_v1_created_ticks_v1_id";

    [BsonId]
    public string Id { get; init; } = ExactId;
    public int TargetVersion { get; init; } = ExactTargetVersion;
    public string Phase { get; set; } = Phases.Preflight;
    public string? LeaseOwner { get; set; }
    public long LeaseGeneration { get; set; }
    public long LeaseFenceSequence { get; set; }
    public long? LeaseExpiresAtUtcTicks { get; set; }
    public Guid? LastProcessedId { get; set; }
    public long ScannedCount { get; set; }
    public long MigratedCount { get; set; }
    public long AlreadyCurrentCount { get; set; }
    public string SourceFingerprint { get; set; } = string.Empty;
    public long StartedAtUtcTicks { get; set; }
    public long UpdatedAtUtcTicks { get; set; }
    public long? CompletedAtUtcTicks { get; set; }
    public string? FailureCode { get; set; }
    public int? ActivationVersion { get; set; }
    public string? SelectedIndexName { get; set; }

    public static class Phases
    {
        public const string Preflight = "Preflight";
        public const string Ready = "Ready";
        public const string Migrating = "Migrating";
        public const string RecoveryRequired = "RecoveryRequired";
        public const string Completed = "Completed";
        public const string CompletionVerified = "CompletionVerified";
        public const string CutoverActive = "CutoverActive";
        public const string Failed = "Failed";

        public static bool IsKnown(string value) => value is
            Preflight or Ready or Migrating or RecoveryRequired or Completed or CompletionVerified or CutoverActive or Failed;
    }

    public void Validate()
    {
        if (!string.Equals(Id, ExactId, StringComparison.Ordinal)
            || TargetVersion != ExactTargetVersion
            || !Phases.IsKnown(Phase)
            || LeaseGeneration < 0
            || LeaseFenceSequence < 0
            || ScannedCount < 0
            || MigratedCount < 0
            || AlreadyCurrentCount < 0
            || (Phase is not (Phases.Preflight or Phases.Failed) && string.IsNullOrWhiteSpace(SourceFingerprint))
            || StartedAtUtcTicks <= 0
            || UpdatedAtUtcTicks <= 0)
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_MIGRATION_STATE_INVALID");
        }

        if (Phase == Phases.CutoverActive
            && (ActivationVersion != ExactTargetVersion
                || !string.Equals(SelectedIndexName, ExactScalarClaimIndexName, StringComparison.Ordinal)
                || LeaseOwner is not null
                || LeaseExpiresAtUtcTicks.HasValue))
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_ACTIVE_STATE_INVALID");
        }

        if (Phase == Phases.CompletionVerified
            && (LeaseOwner is not null || LeaseExpiresAtUtcTicks.HasValue))
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_COMPLETION_STATE_INVALID");
        }
    }
}
