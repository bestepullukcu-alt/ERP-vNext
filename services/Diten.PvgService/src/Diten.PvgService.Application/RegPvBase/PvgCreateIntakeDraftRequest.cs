namespace Diten.PvgService.Application.RegPvBase;

public sealed record PvgCreateIntakeDraftRequest(
    string? IntakeChannel,
    string? SourceType,
    string? SourceReference,
    DateTimeOffset? ReceivedAtUtc,
    string? ReporterType,
    string? ReporterContactSummary,
    string? PatientSubjectCode,
    DateOnly? EventOnsetDate,
    string? AdverseEventNarrative,
    string? SuspectProductText,
    string? Seriousness,
    string? IntakePriority,
    IReadOnlyList<string>? EvidenceLinkReferences);
