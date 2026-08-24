namespace Diten.PvgService.Domain.SignalManagement;

public sealed record SignalIntakeReference(
    string IntakeReferenceToken,
    bool IsApprovedForSignalUse);

public sealed record SignalMinimumCaseReference(
    string CaseReferenceToken,
    string LifecycleReferenceToken,
    bool IsApprovedForSignalUse);

public sealed record SignalCodedOutputReference(
    string CodedOutputReferenceToken,
    string DictionaryVersionReferenceToken,
    bool IsApprovedForSignalUse);

public sealed record SignalHypothesisReference(
    string SignalHypothesisReferenceToken,
    SignalIntakeReference IntakeReference,
    SignalMinimumCaseReference CaseReference,
    SignalCodedOutputReference CodedOutputReference,
    SignalReviewDecisionStatus ReviewDecisionStatus);

public sealed record SignalEvaluationReference(
    string EvaluationReferenceToken,
    SignalHypothesisReference SignalHypothesisReference,
    Mod0004MetricReferenceToken? MetricReference,
    Mod0063DataProductCohortReferenceToken? DataProductCohortReference);

public sealed record Mod0004MetricReferenceToken(
    string MetricReferenceToken,
    string ThresholdReferenceToken,
    bool IsApprovedForSignalUse);

public sealed record SignalThresholdDecisionPlaceholderReference(
    string ThresholdDecisionReferenceToken,
    string ThresholdComparisonReferenceToken,
    string InsufficientDataRuleReferenceToken,
    bool IsApprovedForSignalUse);

public sealed record Mod0063DataProductCohortReferenceToken(
    string DataProductReferenceToken,
    string CohortReferenceToken,
    string LineageReferenceToken,
    bool IsApprovedForSignalUse);

public enum SignalReviewDecisionStatus
{
    Draft = 0,
    UnderReview = 1,
    DecisionRecorded = 2,
    Blocked = 3
}
