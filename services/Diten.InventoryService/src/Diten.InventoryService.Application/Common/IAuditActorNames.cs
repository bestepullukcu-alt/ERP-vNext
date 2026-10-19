namespace Diten.InventoryService.Application.Common;

/// <summary>
/// WP-MVP1-FU-CLEAN1 (FU-EVIDENCE-AUDIT-NAMES) — READ-ONLY and shared by the evidence packs of every feature (FEFO / expiry, recall): the
/// display name the central audit queue (BL-501 <c>central_audit_outbox</c>, <c>ActorDisplayName</c>) recorded for the user who ran a command,
/// found by the command's correlation id AND that user's id — a row of the same correlation by ANOTHER user never gives its name. The
/// caller's tenant only. A pair with no recorded name is absent (the pack prints the id with "(ad okunamadı)"); nothing is invented.
/// It lives here, in the neutral Common layer, so no feature depends on another for it (an architecture test pins that).
/// </summary>
public interface IAuditActorNames
{
    Task<IReadOnlyDictionary<(Guid CorrelationId, Guid ActorId), string>> NamesAsync(
        IReadOnlyCollection<(Guid CorrelationId, Guid ActorId)> keys, CancellationToken cancellationToken = default);
}
