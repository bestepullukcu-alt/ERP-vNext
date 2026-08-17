using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.HrisSources;

public sealed class HrisSyncCheckpoint : TenantScopedEntity
{
    public required Guid SourceProfileId { get; set; }
    public required string SyncRunId { get; set; }
    public HrisSyncMode SyncMode { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public HrisSyncStatus Status { get; set; }
    public string? CursorReference { get; set; }
    public int RecordsSeen { get; set; }
    public int RecordsAccepted { get; set; }
    public int RecordsRejected { get; set; }
    public string? ErrorSummary { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
