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
    public List<LocalAuditIntent> AuditIntents { get; set; } = [];
    public List<LocalAuditIntentReceipt> AuditIntentReceipts { get; set; } = [];
}
