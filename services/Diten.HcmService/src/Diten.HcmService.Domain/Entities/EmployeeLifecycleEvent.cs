namespace Diten.HcmService.Domain.Entities;

public sealed class EmployeeLifecycleEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string SourceModule { get; set; } = "MOD-0251";
    public Guid? DraftSessionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public string? ReplayKey { get; set; }
    public int? DecisionVersion { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
