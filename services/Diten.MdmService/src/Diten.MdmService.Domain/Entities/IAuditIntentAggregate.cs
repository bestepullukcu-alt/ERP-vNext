namespace Diten.MdmService.Domain.Entities;

public interface IAuditIntentAggregate
{
    List<LocalAuditIntent> AuditIntents { get; set; }
    List<LocalAuditIntentReceipt> AuditIntentReceipts { get; set; }
}
