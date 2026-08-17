using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;

public sealed class PayrollIntegrationRun : TenantScopedEntity
{
    public required string RunCode { get; set; }
    public Guid PayrollSourceProfileId { get; set; }
    public Guid? TimeAttendanceProviderProfileId { get; set; }
    public Guid? HrisSourceProfileId { get; set; }
    public required string ContractVersion { get; set; }
    public PayrollIntegrationRunType RunType { get; set; }
    public PayrollIntegrationRunStatus Status { get; set; } = PayrollIntegrationRunStatus.Draft;
    public Guid? RequestedByActorId { get; set; }
    public required string CorrelationId { get; set; }
    public required string IdempotencyKey { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Summary { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
