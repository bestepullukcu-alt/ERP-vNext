using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Entities;

public sealed class FinishedGood : EntityBase, IAuditIntentAggregate
{
    public Guid GskuId { get; set; }
    public string CanonicalCode { get; set; } = string.Empty;
    public Guid CodeReservationId { get; set; }
    public string CreationCommandId { get; set; } = string.Empty;
    public ProductIdentityLifecycleStatus LifecycleStatus { get; set; } = ProductIdentityLifecycleStatus.Draft;
    // FG FIX1 item 1: the audited Draft cancellation (null: none). The FG stays Draft in storage; its disposition is
    // derived from this decision and the central receipt of its intent (FinishedGoodDraftCancellationProof).
    public FinishedGoodDraftCancellation? DraftCancellation { get; set; }
    public List<LocalAuditIntent> AuditIntents { get; set; } = [];
    public List<LocalAuditIntentReceipt> AuditIntentReceipts { get; set; } = [];
}
