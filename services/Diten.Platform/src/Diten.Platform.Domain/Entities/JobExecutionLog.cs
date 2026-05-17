using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities;

public sealed class JobExecutionLog : BaseEntity
{
    public Guid? TenantId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string? JobId { get; set; }
    public string? RecurringJobId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid? CausationId { get; set; }
    public JobExecutionStatus Status { get; set; } = JobExecutionStatus.Started;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    public long? DurationMs { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public string? TriggeredBy { get; set; }
    public string? EventName { get; set; }
    public Guid? EventId { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
