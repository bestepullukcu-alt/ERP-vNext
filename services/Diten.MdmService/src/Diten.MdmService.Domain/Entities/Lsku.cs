using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Entities;

public sealed class Lsku : EntityBase, IAuditIntentAggregate
{
    public Guid GskuId { get; set; }
    public string CanonicalCode { get; set; } = string.Empty;
    public Guid CodeReservationId { get; set; }
    public string CreationCommandId { get; set; } = string.Empty;
    public string MarketCode { get; set; } = string.Empty;
    public ReferenceCatalogSelection MarketSelection { get; set; } = new();
    public ProductIdentityLifecycleStatus LifecycleStatus { get; set; } = ProductIdentityLifecycleStatus.Draft;
    public List<LocalAuditIntent> AuditIntents { get; set; } = [];
    public List<LocalAuditIntentReceipt> AuditIntentReceipts { get; set; } = [];
}
