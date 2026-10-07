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
    public ProductIdentityWorkflowBinding? IdentityWorkflowBinding { get; set; }
    public LskuActiveLifecycleOperationBinding? ActiveLifecycleOperation { get; set; }
    public List<LocalAuditIntent> AuditIntents { get; set; } = [];
    public List<LocalAuditIntentReceipt> AuditIntentReceipts { get; set; } = [];

    // WP-MDM-ITEM-MASTER (MOD-0290-FU04) S3 — stock behaviour (pack §4.2). Every field is null on a document written
    // before S3 ("not set yet", never false / 0) and is written only by its own stock-behaviour command, which moves the
    // version. A filled value is locked on a non-Draft record (O-8, 409 STOCK_BEHAVIOUR_FIELD_LOCKED).
    // Only serial control is the LSKU's own (it follows the market — İTS); everything else is its GSKU's at read time (O-3).
    public bool? SerialControlled { get; set; }
    public StockBehaviourCommandMark? StockBehaviourCommand { get; set; }
}
