namespace Diten.InventoryService.Application.Features.Recall.Ports;

// WP-MVP1-FU-CLEAN1 (FU-EVIDENCE-AUDIT-NAMES): the audit-name read is the shared Application.Common.IAuditActorNames (one reader for every pack).

/// <summary>
/// WP-MVP1-FIX-LIVE2 — the display name of the user running the current command (the token's name claims, the same value the central
/// audit queue records), written on each NEW <c>RecallCaseEvent</c> as <c>ActorName</c> (additive). Null outside a request / without the
/// claims — never invented.
/// </summary>
public interface IRecallActorName
{
    string? Current { get; }
}
