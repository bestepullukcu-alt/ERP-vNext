using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Entities;

public sealed class GlobalProduct : EntityBase, IAuditIntentAggregate
{
    public string CanonicalCode { get; set; } = string.Empty;
    public string GlobalProductName { get; set; } = string.Empty;
    public string GlobalProductNameNormalized { get; set; } = string.Empty;
    public Guid CodeReservationId { get; set; }
    public ProductIdentityLifecycleStatus LifecycleStatus { get; set; } = ProductIdentityLifecycleStatus.Draft;
    public ProductIdentityWorkflowBinding? WorkflowBinding { get; set; }
    public GlobalProductActiveLifecycleOperationBinding? ActiveLifecycleOperation { get; set; }
    public List<ProductChildCreationAdmission> ChildCreationAdmissions { get; set; } = [];
    public List<LocalAuditIntent> AuditIntents { get; set; } = [];
    public List<LocalAuditIntentReceipt> AuditIntentReceipts { get; set; } = [];
}
