namespace Diten.PvgService.Domain.CaseProcessing;

public sealed record SignalMinimumAssessment(
    string CaseProcessingPriority,
    string CaseValidityStatus,
    string? CaseValidityReason,
    string ProcessingOwnerQueue,
    DateTimeOffset? ProcessingDueAtUtc,
    string ProductExposureAssessment,
    string SeriousnessConfirmed,
    string EventAssessmentSummary,
    string? PreliminaryExpectedness,
    string EvidenceCompletenessStatus,
    string? EvidenceGapReason,
    string SignalRelevanceFlag,
    string? SignalRelevanceReason,
    string SignalHandoffReadiness,
    string? SignalHandoffSummary,
    string? ProcessingNotesInternal);
