namespace Diten.PvgService.Domain.CaseProcessing;

public sealed record Mod0230HandoffReference(
    string IntakeDraftId,
    string IntakeNumber,
    DateTimeOffset ReceivedAtUtc,
    string TriageOutcomeCode,
    string RouteTargetQueueCode,
    IReadOnlyCollection<string> EvidenceLinkReferenceIds);
