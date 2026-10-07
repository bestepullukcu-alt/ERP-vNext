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

    // WP-MDM-ITEM-MASTER (MOD-0290-FU04) S3 — stock behaviour (pack §4.2). Every field is null on a document written
    // before S3 ("not set yet", never false / 0) and is written only by its own stock-behaviour command, which moves the
    // version. A filled value is locked on a non-Draft record (O-8, 409 STOCK_BEHAVIOUR_FIELD_LOCKED).
    // The base unit is the parent GSKU's (O-14) and is not stored; a null ShelfLifeDays reads the GSKU's at read time
    // (D-2, source Gsku) and an own value never exceeds it (O-15). Gtin = the sales-box GTIN-14, unique among the tenant's
    // non-retired Finished Goods.
    public List<ItemUomConversion>? UomConversions { get; set; }
    public int? ShelfLifeDays { get; set; }
    public List<string>? StorageConditionCodes { get; set; }
    public bool? LotControlled { get; set; }
    public bool? SerialControlled { get; set; }
    public bool? Stockable { get; set; }
    public string? Gtin { get; set; }
    // The MOD-0048 selection proof of every unit and storage code the stock fields hold (pack §12), replaced as a whole
    // when one of them changes; derived, so never locked itself.
    public List<ReferenceCatalogSelection>? StockReferenceSelections { get; set; }
    public StockBehaviourCommandMark? StockBehaviourCommand { get; set; }
    public StockBehaviourCommandMark? GtinCommand { get; set; }
}
