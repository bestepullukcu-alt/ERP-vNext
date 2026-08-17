using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.TimeAttendanceProviders;

public sealed class TimeAttendanceSyncCheckpoint : TenantScopedEntity
{
    public required Guid ProviderProfileId { get; set; }
    public required string SyncRunId { get; set; }
    public TimeAttendanceSyncMode SyncMode { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? CheckpointReference { get; set; }
    public int RecordsSeen { get; set; }
    public int RecordsAccepted { get; set; }
    public int RecordsRejected { get; set; }
    public TimeAttendanceSyncStatus Status { get; set; }
    public string? ErrorSummary { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
