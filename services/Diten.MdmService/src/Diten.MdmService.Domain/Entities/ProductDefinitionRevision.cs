using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Entities;

public sealed class ProductDefinitionRevision : EntityBase, IAuditIntentAggregate
{
    public Guid GlobalProductId { get; set; }
    public string RevisionIdentifier { get; set; } = string.Empty;
    public string CreationCommandId { get; set; } = string.Empty;
    public ProductIdentityLifecycleStatus LifecycleStatus { get; set; } = ProductIdentityLifecycleStatus.Draft;
    public FirstGskuIdentityWorkflowBinding? IdentityWorkflowBinding { get; set; }
    public Guid? RetirementOperationId { get; set; }
    public string? RetirementOperationFingerprint { get; set; }
    public List<LocalAuditIntent> AuditIntents { get; set; } = [];
    public List<LocalAuditIntentReceipt> AuditIntentReceipts { get; set; } = [];
}
