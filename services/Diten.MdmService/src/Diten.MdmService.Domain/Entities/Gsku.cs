using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Entities;

public sealed class Gsku : EntityBase, IAuditIntentAggregate
{
    public Guid ProductDefinitionRevisionId { get; set; }
    public string CanonicalCode { get; set; } = string.Empty;
    public Guid CodeReservationId { get; set; }
    public string CreationCommandId { get; set; } = string.Empty;
    public string PackApplicabilityCode { get; set; } = string.Empty;
    public decimal PackQuantity { get; set; }
    public string PackUomCode { get; set; } = string.Empty;
    public ReferenceCatalogSelection PackApplicabilitySelection { get; set; } = new();
    public ReferenceCatalogSelection PackUomSelection { get; set; } = new();
    public ProductIdentityLifecycleStatus LifecycleStatus { get; set; } = ProductIdentityLifecycleStatus.Draft;
    public FirstGskuIdentityWorkflowBinding? IdentityWorkflowBinding { get; set; }
    public GskuActiveLifecycleOperationBinding? ActiveLifecycleOperation { get; set; }
    public List<GskuChildCreationAdmission> ChildCreationAdmissions { get; set; } = [];
    public Guid? RetirementOperationId { get; set; }
    public string? RetirementOperationFingerprint { get; set; }
    // FIX2 item 10: every identity-submit reservation touches the GSKU in the same transaction as it is written, and a
    // draft edit reads the reservations and writes the GSKU in one transaction — so an edit and a submit that race
    // cannot both commit (the store refuses one of them with a write conflict). The count itself is not read.
    public int IdentitySubmitReservations { get; set; }
    public List<LocalAuditIntent> AuditIntents { get; set; } = [];
    public List<LocalAuditIntentReceipt> AuditIntentReceipts { get; set; } = [];

    // WP-MDM-ITEM-MASTER (MOD-0290-FU04) S3 — stock behaviour (pack §4.2). Every field is null on a document written
    // before S3 ("not set yet", never false / 0) and is written only by its own stock-behaviour command, which moves the
    // version. A filled value is locked on a non-Draft record (O-8, 409 STOCK_BEHAVIOUR_FIELD_LOCKED).
    // The box ↔ pack conversion is derived from PackQuantity + PackUomCode (O-14) and never stored here.
    public string? BaseUomCode { get; set; }
    public List<ItemUomConversion>? UomConversions { get; set; }
    public int? ShelfLifeDays { get; set; }
    public List<string>? StorageConditionCodes { get; set; }
    public bool? LotControlled { get; set; }
    public bool? Stockable { get; set; }
    // The MOD-0048 selection proof of every unit and storage code the stock fields hold (pack §12), replaced as a whole
    // when one of them changes; derived, so never locked itself.
    public List<ReferenceCatalogSelection>? StockReferenceSelections { get; set; }
    public StockBehaviourCommandMark? StockBehaviourCommand { get; set; }
}
