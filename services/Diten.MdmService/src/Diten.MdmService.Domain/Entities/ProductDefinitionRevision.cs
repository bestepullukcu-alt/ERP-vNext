using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

public sealed class ProductDefinitionRevision : EntityBase, IAuditIntentAggregate
{
    public Guid GlobalProductId { get; set; }
    public string RevisionIdentifier { get; set; } = string.Empty;
    public string CreationCommandId { get; set; } = string.Empty;
    public ProductIdentityLifecycleStatus LifecycleStatus { get; set; } = ProductIdentityLifecycleStatus.Draft;
    public List<LocalAuditIntent> AuditIntents { get; set; } = [];
    public List<LocalAuditIntentReceipt> AuditIntentReceipts { get; set; } = [];
}
