using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

public sealed class GlobalProduct : EntityBase, IAuditIntentAggregate
{
    public string CanonicalCode { get; set; } = string.Empty;
    public string GlobalProductName { get; set; } = string.Empty;
    public string GlobalProductNameNormalized { get; set; } = string.Empty;
    public Guid CodeReservationId { get; set; }
    public ProductIdentityLifecycleStatus LifecycleStatus { get; set; } = ProductIdentityLifecycleStatus.Draft;
    public List<LocalAuditIntent> AuditIntents { get; set; } = [];
    public List<LocalAuditIntentReceipt> AuditIntentReceipts { get; set; } = [];
}
