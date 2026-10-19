namespace Diten.InventoryService.Application.Features.Expiry.Ports;

/// <summary>
/// WP-MVP1-FIX-LIVE2 (CT decision "0176 kişi adları A") — READ-ONLY: the display name the central audit queue (BL-501
/// <c>central_audit_outbox</c>, <c>ActorDisplayName</c>) recorded for the user who ran a command, found by the command's correlation id
/// AND that user's id (a row of the same correlation by ANOTHER user never gives its name). The caller's tenant only. A pair with no
/// recorded name is simply absent — the evidence then prints the id with "(ad okunamadı)"; nothing is invented. No Expiry entity gains a
/// field: the name is the one recorded at operation time, read back.
/// </summary>
public interface IExpiryAuditNames
{
    Task<IReadOnlyDictionary<(Guid CorrelationId, Guid ActorId), string>> NamesAsync(
        IReadOnlyCollection<(Guid CorrelationId, Guid ActorId)> keys, CancellationToken cancellationToken = default);
}
