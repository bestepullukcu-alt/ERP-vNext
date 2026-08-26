namespace Diten.MdmService.Domain.Entities;

public sealed class LocalAuditIntentReceipt
{
    public string SourceService { get; set; } = string.Empty;
    public Guid IntentId { get; set; }
    public Guid TenantId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CentralAcknowledgement { get; set; } = string.Empty;
    public string CentralIdempotencyKey { get; set; } = string.Empty;
    public string ContractVersion { get; set; } = string.Empty;
    public DateTimeOffset AcknowledgedAt { get; set; }
    public DateTimeOffset DeliveredAt { get; set; }
    public DateTimeOffset CompactedAt { get; set; }
    public string CompactReceiptReference { get; set; } = string.Empty;
    public string EvidenceHash { get; set; } = string.Empty;
}
