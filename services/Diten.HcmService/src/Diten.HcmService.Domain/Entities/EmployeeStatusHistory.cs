namespace Diten.HcmService.Domain.Entities;

public sealed class EmployeeStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
    public string? ReasonCategory { get; set; }
    public string? ReasonNote { get; set; }
    public string TriggeredByModule { get; set; } = "MOD-0251";
    public Guid? WorkflowReferenceId { get; set; }
    public Guid? ApprovalReferenceId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
