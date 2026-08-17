using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;

public sealed class PayrollIntegrationEvidenceExportReference : TenantScopedEntity
{
    public Guid RunId { get; set; }
    public required string ExportPurposeCode { get; set; }
    public Guid? AuditEventId { get; set; }
    public Guid? EvidenceReferenceId { get; set; }
    public Guid? RecordsRetentionReferenceId { get; set; }
    public PayrollIntegrationEvidenceExportState ExportState { get; set; } = PayrollIntegrationEvidenceExportState.Requested;
    public Guid? RequestedByActorId { get; set; }
    public required string CorrelationId { get; set; }
    public string? RedactedNotes { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
