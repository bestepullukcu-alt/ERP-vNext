using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

public sealed class CodeReservation : EntityBase, IAuditIntentAggregate
{
    public CodeBearingEntityType EntityType { get; set; }
    public string ReservedCode { get; set; } = string.Empty;
    public CodeReservationState ReservationState { get; set; } = CodeReservationState.Reserved;
    public CodeReservationBindingState BindingState { get; set; }
    public string ReservationCommandId { get; set; } = string.Empty;
    public string? ConsumeCommandId { get; set; }
    public string? LastCommandId { get; set; }
    public DateTimeOffset ReservedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string ReservedByActorId { get; set; } = string.Empty;
    public Guid? ConsumedEntityId { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? BindingConfirmedAt { get; set; }
    public DateTimeOffset? BurnedAt { get; set; }
    public string? BurnReason { get; set; }
    public string? RecoveryDisposition { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public List<LocalAuditIntent> AuditIntents { get; set; } = [];
    public List<LocalAuditIntentReceipt> AuditIntentReceipts { get; set; } = [];
}
