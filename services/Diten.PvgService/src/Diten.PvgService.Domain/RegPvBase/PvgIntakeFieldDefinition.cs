namespace Diten.PvgService.Domain.RegPvBase;

public sealed record PvgIntakeFieldDefinition(
    PvgIntakeField Field,
    bool IsRequiredAtCreate,
    PvgFieldSensitivity Sensitivity,
    bool IsFreeText)
{
    public static IReadOnlyList<PvgIntakeFieldDefinition> ApprovedFields { get; } =
    [
        new(PvgIntakeField.IntakeChannel, true, PvgFieldSensitivity.PublicMetadata, false),
        new(PvgIntakeField.SourceType, true, PvgFieldSensitivity.PublicMetadata, false),
        new(PvgIntakeField.SourceReference, false, PvgFieldSensitivity.Confidential, false),
        new(PvgIntakeField.ReceivedAtUtc, true, PvgFieldSensitivity.RegulatedSafety, false),
        new(PvgIntakeField.ReporterType, true, PvgFieldSensitivity.PublicMetadata, false),
        new(PvgIntakeField.ReporterContactSummary, false, PvgFieldSensitivity.Pii, false),
        new(PvgIntakeField.PatientSubjectCode, false, PvgFieldSensitivity.Phi, false),
        new(PvgIntakeField.EventOnsetDate, false, PvgFieldSensitivity.Phi, false),
        new(PvgIntakeField.AdverseEventNarrative, true, PvgFieldSensitivity.Phi, true),
        new(PvgIntakeField.SuspectProductText, false, PvgFieldSensitivity.RegulatedSafety, false),
        new(PvgIntakeField.Seriousness, true, PvgFieldSensitivity.RegulatedSafety, false),
        new(PvgIntakeField.IntakePriority, true, PvgFieldSensitivity.RegulatedSafety, false),
        new(PvgIntakeField.TriageOutcome, false, PvgFieldSensitivity.RegulatedSafety, false),
        new(PvgIntakeField.TriageReason, false, PvgFieldSensitivity.Phi, true),
        new(PvgIntakeField.RouteTargetQueue, false, PvgFieldSensitivity.Confidential, false),
        new(PvgIntakeField.EvidenceLinkReferences, false, PvgFieldSensitivity.Confidential, false)
    ];
}
