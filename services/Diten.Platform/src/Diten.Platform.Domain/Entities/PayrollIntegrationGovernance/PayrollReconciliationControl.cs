using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;

public sealed class PayrollReconciliationControl : TenantScopedEntity
{
    public Guid RunId { get; set; }
    public PayrollIntegrationReconciliationType ReconciliationType { get; set; }
    public int? ExpectedCount { get; set; }
    public int? ObservedCount { get; set; }
    public PayrollIntegrationControlState ControlState { get; set; } = PayrollIntegrationControlState.Pending;
    public Guid? EvidenceReferenceId { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
