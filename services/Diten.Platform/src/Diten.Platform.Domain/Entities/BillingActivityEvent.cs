using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.Billing;

namespace Diten.Platform.Domain.Entities;

public sealed class BillingActivityEvent : TenantScopedEntity
{
    public Guid? InvoiceId { get; init; }
    public string? InvoiceNumber { get; init; }
    public Guid? PaymentRecordId { get; init; }
    public Guid? RefundRecordId { get; init; }
    public required BillingActivityEventType EventType { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string ActorUserId { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
