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
        new(PvgIntakeField.Seriousness, true, PvgFieldSensitivity.RegulatedSafety, false),
        new(PvgIntakeField.IntakePriority, true, PvgFieldSensitivity.RegulatedSafety, false),
        new(PvgIntakeField.TriageOutcome, false, PvgFieldSensitivity.RegulatedSafety, false),
        new(PvgIntakeField.TriageReason, false, PvgFieldSensitivity.Phi, true),
        new(PvgIntakeField.RouteTargetQueue, false, PvgFieldSensitivity.Confidential, false),
        new(PvgIntakeField.EvidenceLinkReferences, false, PvgFieldSensitivity.Confidential, false),
        new(PvgIntakeField.ProductLines, false, PvgFieldSensitivity.RegulatedSafety, false),
        new(PvgIntakeField.ProductRole, false, PvgFieldSensitivity.RegulatedSafety, false),
        new(PvgIntakeField.GlobalProductId, false, PvgFieldSensitivity.RegulatedSafety, false),
        new(PvgIntakeField.ProductNameAsReported, false, PvgFieldSensitivity.RegulatedSafety, true),
        new(PvgIntakeField.PresentationGskuId, false, PvgFieldSensitivity.RegulatedSafety, false),
        new(PvgIntakeField.LotNumber, false, PvgFieldSensitivity.RegulatedSafety, true),
        new(PvgIntakeField.AwarenessAtUtc, true, PvgFieldSensitivity.RegulatedSafety, false),
        new(PvgIntakeField.CompanyRecipientPersonId, false, PvgFieldSensitivity.Confidential, false),
        new(PvgIntakeField.EventCountry, true, PvgFieldSensitivity.RegulatedSafety, false),
        new(PvgIntakeField.ReportType, true, PvgFieldSensitivity.RegulatedSafety, false),
        new(PvgIntakeField.ReporterSelectionDirectory, false, PvgFieldSensitivity.PublicMetadata, false),
        new(PvgIntakeField.ReporterSelectionId, false, PvgFieldSensitivity.Pii, false),
        new(PvgIntakeField.RemoveReporterLink, false, PvgFieldSensitivity.PublicMetadata, false),
        new(PvgIntakeField.ReporterSource, false, PvgFieldSensitivity.PublicMetadata, false),
        new(PvgIntakeField.ReporterSourceId, false, PvgFieldSensitivity.Pii, false),
        new(PvgIntakeField.ReporterCrmSearchState, false, PvgFieldSensitivity.PublicMetadata, false),
        new(PvgIntakeField.ReporterNameOrInitials, false, PvgFieldSensitivity.Pii, false),
        new(PvgIntakeField.ReporterProfession, false, PvgFieldSensitivity.Pii, false),
        new(PvgIntakeField.ReporterCountry, false, PvgFieldSensitivity.Pii, false)
    ];
}
