namespace Diten.InventoryService.Application.Features.Recall.Ports;

/// <summary>
/// WP-MVP1-FIX-LIVE2 (CT-accepted name path) — READ-ONLY: the display name the central audit queue (BL-501 <c>central_audit_outbox</c>,
/// <c>ActorDisplayName</c>) recorded for the user who ran a command, found by the command's correlation id AND that user's id (a row of the
/// same correlation by ANOTHER user never gives its name). The caller's tenant only. A pair with no recorded name is absent — the evidence
/// then prints the id with "(ad okunamadı)"; nothing is invented. 0177's own reader (the 0176 one is not shared: no Expiry ↔ Recall link).
/// </summary>
public interface IRecallAuditNames
{
    Task<IReadOnlyDictionary<(Guid CorrelationId, Guid ActorId), string>> NamesAsync(
        IReadOnlyCollection<(Guid CorrelationId, Guid ActorId)> keys, CancellationToken cancellationToken = default);
}

/// <summary>
/// WP-MVP1-FIX-LIVE2 — the display name of the user running the current command (the token's name claims, the same value the central
/// audit queue records), written on each NEW <c>RecallCaseEvent</c> as <c>ActorName</c> (additive). Null outside a request / without the
/// claims — never invented.
/// </summary>
public interface IRecallActorName
{
    string? Current { get; }
}
