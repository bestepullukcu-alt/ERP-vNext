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
    // FIX3 item 6: touched by every identity submit's reservation, in the reservation's own transaction, so that a
    // draft edit and a submit cannot both commit (the edit reads the latest submit in its transaction).
    public int IdentitySubmitReservations { get; set; }
    public ProductIdentityWorkflowBinding? WorkflowBinding { get; set; }
    public GlobalProductActiveLifecycleOperationBinding? ActiveLifecycleOperation { get; set; }
    public List<ProductChildCreationAdmission> ChildCreationAdmissions { get; set; } = [];
    public List<LocalAuditIntent> AuditIntents { get; set; } = [];
    public List<LocalAuditIntentReceipt> AuditIntentReceipts { get; set; } = [];
}
