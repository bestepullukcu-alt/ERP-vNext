using Diten.PvgService.Domain.SignalManagement;

namespace Diten.PvgService.Application.SignalManagement;

public sealed record SignalManagementServerTenantContext(string TenantContextReference);

public sealed record SignalManagementActorContext(string ActorReference, string ActorTypeReference);

public sealed record SignalManagementCorrelationContext(string CorrelationReference);

public sealed record SignalManagementPermissionDecision(bool IsAllowed, SignalManagementReasonCode DeniedReason);

public sealed record SignalManagementGuardDecision(bool IsAllowed, SignalManagementReasonCode DeniedReason)
{
    public static SignalManagementGuardDecision Allow() => new(true, SignalManagementReasonCode.None);

    public static SignalManagementGuardDecision Deny(SignalManagementReasonCode reasonCode) => new(false, reasonCode);
}

public sealed record CreateSignalHypothesisContractCommand(
    SignalIntakeReference IntakeReference,
    SignalMinimumCaseReference CaseReference,
    SignalCodedOutputReference CodedOutputReference,
    SignalManagementServerTenantContext ServerTenantContext,
    SignalManagementActorContext ActorContext,
    SignalManagementPermissionDecision PermissionDecision,
    SignalManagementCorrelationContext CorrelationContext,
    SignalManagementGuardDecision IntakeGuard,
    SignalManagementGuardDecision CaseGuard,
    SignalManagementGuardDecision CodedOutputGuard,
    SignalManagementGuardDecision FieldPolicyGuard,
    SignalManagementGuardDecision EvidenceGuard,
    SignalManagementGuardDecision AuditIntentMetadataGuard,
    SignalManagementGuardDecision MetricContractGuard,
    SignalManagementGuardDecision DataProductContractGuard);

public sealed record AttachSignalMetricDataProductReferenceCommand(
    string SignalHypothesisReferenceToken,
    Mod0004MetricReferenceToken MetricReference,
    SignalThresholdDecisionPlaceholderReference ThresholdDecisionPlaceholderReference,
    Mod0063DataProductCohortReferenceToken DataProductCohortReference,
    SignalManagementServerTenantContext ServerTenantContext,
    SignalManagementActorContext ActorContext,
    SignalManagementPermissionDecision PermissionDecision,
    SignalManagementCorrelationContext CorrelationContext,
    SignalManagementGuardDecision IntakeGuard,
    SignalManagementGuardDecision CaseGuard,
    SignalManagementGuardDecision CodedOutputGuard,
    SignalManagementGuardDecision FieldPolicyGuard,
    SignalManagementGuardDecision EvidenceGuard,
    SignalManagementGuardDecision AuditIntentMetadataGuard,
    SignalManagementGuardDecision MetricContractGuard,
    SignalManagementGuardDecision ThresholdDecisionContractGuard,
    SignalManagementGuardDecision DataProductContractGuard);

public sealed record MarkSignalReviewDecisionContractCommand(
    string SignalHypothesisReferenceToken,
    SignalReviewDecisionStatus ReviewDecisionStatus,
    SignalManagementServerTenantContext ServerTenantContext,
    SignalManagementActorContext ActorContext,
    SignalManagementPermissionDecision PermissionDecision,
    SignalManagementCorrelationContext CorrelationContext,
    SignalManagementGuardDecision IntakeGuard,
    SignalManagementGuardDecision CaseGuard,
    SignalManagementGuardDecision CodedOutputGuard,
    SignalManagementGuardDecision FieldPolicyGuard,
    SignalManagementGuardDecision EvidenceGuard,
    SignalManagementGuardDecision AuditIntentMetadataGuard,
    SignalManagementGuardDecision MetricContractGuard,
    SignalManagementGuardDecision DataProductContractGuard);

public sealed record GetSignalContractMetadataByIdQuery(
    string SignalHypothesisReferenceToken,
    SignalManagementServerTenantContext ServerTenantContext,
    SignalManagementActorContext ActorContext,
    SignalManagementPermissionDecision PermissionDecision,
    SignalManagementCorrelationContext CorrelationContext,
    SignalManagementGuardDecision FieldPolicyGuard);

public sealed record GetSignalContractMetadataListQuery(
    SignalManagementServerTenantContext ServerTenantContext,
    SignalManagementActorContext ActorContext,
    SignalManagementPermissionDecision PermissionDecision,
    SignalManagementCorrelationContext CorrelationContext,
    SignalManagementGuardDecision FieldPolicyGuard);

public sealed record SignalManagementSafeResult(
    bool IsAllowed,
    SignalManagementReasonCode ReasonCode,
    IReadOnlyDictionary<string, string> Metadata)
{
    public static SignalManagementSafeResult Allowed(SignalManagementOperation operation) =>
        new(
            true,
            SignalManagementReasonCode.None,
            new Dictionary<string, string>
            {
                ["module"] = "MOD-0234",
                ["operation"] = operation.ToString(),
                ["result"] = "Allowed"
            });

    public static SignalManagementSafeResult Blocked(
        SignalManagementOperation operation,
        SignalManagementReasonCode reasonCode) =>
        new(
            false,
            reasonCode,
            new Dictionary<string, string>
            {
                ["module"] = "MOD-0234",
                ["operation"] = operation.ToString(),
                ["result"] = "Blocked"
            });
}

public enum SignalManagementOperation
{
    CreateSignalHypothesisContract = 0,
    AttachSignalMetricDataProductReference = 1,
    MarkSignalReviewDecisionContract = 2,
    GetById = 3,
    List = 4
}

public enum SignalManagementReasonCode
{
    None = 0,
    MissingServerTenantContext = 1,
    MissingActorContext = 2,
    PermissionDenied = 3,
    MissingCorrelationContext = 4,
    InvalidCorrelationContext = 5,
    MissingIntakeReference = 6,
    IntakeReferenceDenied = 7,
    MissingCaseReference = 8,
    CaseReferenceDenied = 9,
    MissingCodedOutputReference = 10,
    CodedOutputReferenceDenied = 11,
    FieldPolicyDenied = 12,
    EvidenceDenied = 13,
    AuditIntentDenied = 14,
    MetricContractMissing = 15,
    MetricContractDenied = 16,
    DataProductContractMissing = 17,
    DataProductContractDenied = 18,
    InvalidRequest = 19,
    NotFoundOrUnavailable = 20,
    ThresholdDecisionContractMissing = 21,
    ThresholdDecisionContractDenied = 22
}

public static class SignalManagementContractGuard
{
    private const int MaxCorrelationReferenceLength = 128;

    public static SignalManagementSafeResult Evaluate(CreateSignalHypothesisContractCommand command)
    {
        var guardResult = EvaluateMutationGuards(
            SignalManagementOperation.CreateSignalHypothesisContract,
            command.ServerTenantContext,
            command.ActorContext,
            command.PermissionDecision,
            command.CorrelationContext,
            command.IntakeGuard,
            command.CaseGuard,
            command.CodedOutputGuard,
            command.FieldPolicyGuard,
            command.EvidenceGuard,
            command.AuditIntentMetadataGuard,
            command.MetricContractGuard,
            command.DataProductContractGuard);

        if (!guardResult.IsAllowed)
        {
            return guardResult;
        }

        return IsValid(command.IntakeReference) &&
               IsValid(command.CaseReference) &&
               IsValid(command.CodedOutputReference)
            ? SignalManagementSafeResult.Allowed(SignalManagementOperation.CreateSignalHypothesisContract)
            : SignalManagementSafeResult.Blocked(
                SignalManagementOperation.CreateSignalHypothesisContract,
                SignalManagementReasonCode.InvalidRequest);
    }

    public static SignalManagementSafeResult Evaluate(AttachSignalMetricDataProductReferenceCommand command)
    {
        var guardResult = EvaluateMutationGuards(
            SignalManagementOperation.AttachSignalMetricDataProductReference,
            command.ServerTenantContext,
            command.ActorContext,
            command.PermissionDecision,
            command.CorrelationContext,
            command.IntakeGuard,
            command.CaseGuard,
            command.CodedOutputGuard,
            command.FieldPolicyGuard,
            command.EvidenceGuard,
            command.AuditIntentMetadataGuard,
            command.MetricContractGuard,
            command.DataProductContractGuard);

        if (!guardResult.IsAllowed)
        {
            return guardResult;
        }

        if (!command.ThresholdDecisionContractGuard.IsAllowed)
        {
            return SignalManagementSafeResult.Blocked(
                SignalManagementOperation.AttachSignalMetricDataProductReference,
                command.ThresholdDecisionContractGuard.DeniedReason);
        }

        return HasValue(command.SignalHypothesisReferenceToken) &&
               IsValid(command.MetricReference) &&
               IsValid(command.ThresholdDecisionPlaceholderReference) &&
               IsValid(command.DataProductCohortReference)
            ? SignalManagementSafeResult.Allowed(SignalManagementOperation.AttachSignalMetricDataProductReference)
            : SignalManagementSafeResult.Blocked(
                SignalManagementOperation.AttachSignalMetricDataProductReference,
                SignalManagementReasonCode.InvalidRequest);
    }

    public static SignalManagementSafeResult Evaluate(MarkSignalReviewDecisionContractCommand command)
    {
        var guardResult = EvaluateMutationGuards(
            SignalManagementOperation.MarkSignalReviewDecisionContract,
            command.ServerTenantContext,
            command.ActorContext,
            command.PermissionDecision,
            command.CorrelationContext,
            command.IntakeGuard,
            command.CaseGuard,
            command.CodedOutputGuard,
            command.FieldPolicyGuard,
            command.EvidenceGuard,
            command.AuditIntentMetadataGuard,
            command.MetricContractGuard,
            command.DataProductContractGuard);

        if (!guardResult.IsAllowed)
        {
            return guardResult;
        }

        return HasValue(command.SignalHypothesisReferenceToken) &&
               command.ReviewDecisionStatus == SignalReviewDecisionStatus.DecisionRecorded
            ? SignalManagementSafeResult.Allowed(SignalManagementOperation.MarkSignalReviewDecisionContract)
            : SignalManagementSafeResult.Blocked(
                SignalManagementOperation.MarkSignalReviewDecisionContract,
                SignalManagementReasonCode.InvalidRequest);
    }

    public static SignalManagementSafeResult Evaluate(GetSignalContractMetadataByIdQuery query)
    {
        var guardResult = EvaluateReadGuards(
            SignalManagementOperation.GetById,
            query.ServerTenantContext,
            query.ActorContext,
            query.PermissionDecision,
            query.CorrelationContext,
            query.FieldPolicyGuard);

        if (!guardResult.IsAllowed)
        {
            return guardResult;
        }

        return HasValue(query.SignalHypothesisReferenceToken)
            ? SignalManagementSafeResult.Allowed(SignalManagementOperation.GetById)
            : SignalManagementSafeResult.Blocked(SignalManagementOperation.GetById, SignalManagementReasonCode.InvalidRequest);
    }

    public static SignalManagementSafeResult Evaluate(GetSignalContractMetadataListQuery query) =>
        EvaluateReadGuards(
            SignalManagementOperation.List,
            query.ServerTenantContext,
            query.ActorContext,
            query.PermissionDecision,
            query.CorrelationContext,
            query.FieldPolicyGuard);

    private static SignalManagementSafeResult EvaluateMutationGuards(
        SignalManagementOperation operation,
        SignalManagementServerTenantContext serverTenantContext,
        SignalManagementActorContext actorContext,
        SignalManagementPermissionDecision permissionDecision,
        SignalManagementCorrelationContext correlationContext,
        SignalManagementGuardDecision intakeGuard,
        SignalManagementGuardDecision caseGuard,
        SignalManagementGuardDecision codedOutputGuard,
        SignalManagementGuardDecision fieldPolicyGuard,
        SignalManagementGuardDecision evidenceGuard,
        SignalManagementGuardDecision auditIntentMetadataGuard,
        SignalManagementGuardDecision metricContractGuard,
        SignalManagementGuardDecision dataProductContractGuard)
    {
        var readResult = EvaluateReadGuards(
            operation,
            serverTenantContext,
            actorContext,
            permissionDecision,
            correlationContext,
            fieldPolicyGuard);

        if (!readResult.IsAllowed)
        {
            return readResult;
        }

        if (!intakeGuard.IsAllowed)
        {
            return SignalManagementSafeResult.Blocked(operation, intakeGuard.DeniedReason);
        }

        if (!caseGuard.IsAllowed)
        {
            return SignalManagementSafeResult.Blocked(operation, caseGuard.DeniedReason);
        }

        if (!codedOutputGuard.IsAllowed)
        {
            return SignalManagementSafeResult.Blocked(operation, codedOutputGuard.DeniedReason);
        }

        if (!evidenceGuard.IsAllowed)
        {
            return SignalManagementSafeResult.Blocked(operation, evidenceGuard.DeniedReason);
        }

        if (!auditIntentMetadataGuard.IsAllowed)
        {
            return SignalManagementSafeResult.Blocked(operation, auditIntentMetadataGuard.DeniedReason);
        }

        if (!metricContractGuard.IsAllowed)
        {
            return SignalManagementSafeResult.Blocked(operation, metricContractGuard.DeniedReason);
        }

        if (!dataProductContractGuard.IsAllowed)
        {
            return SignalManagementSafeResult.Blocked(operation, dataProductContractGuard.DeniedReason);
        }

        return SignalManagementSafeResult.Allowed(operation);
    }

    private static SignalManagementSafeResult EvaluateReadGuards(
        SignalManagementOperation operation,
        SignalManagementServerTenantContext serverTenantContext,
        SignalManagementActorContext actorContext,
        SignalManagementPermissionDecision permissionDecision,
        SignalManagementCorrelationContext correlationContext,
        SignalManagementGuardDecision fieldPolicyGuard)
    {
        if (!HasValue(serverTenantContext.TenantContextReference))
        {
            return SignalManagementSafeResult.Blocked(operation, SignalManagementReasonCode.MissingServerTenantContext);
        }

        if (!HasValue(actorContext.ActorReference) || !HasValue(actorContext.ActorTypeReference))
        {
            return SignalManagementSafeResult.Blocked(operation, SignalManagementReasonCode.MissingActorContext);
        }

        if (!permissionDecision.IsAllowed)
        {
            return SignalManagementSafeResult.Blocked(operation, permissionDecision.DeniedReason);
        }

        if (!HasValue(correlationContext.CorrelationReference))
        {
            return SignalManagementSafeResult.Blocked(operation, SignalManagementReasonCode.MissingCorrelationContext);
        }

        if (correlationContext.CorrelationReference.Length > MaxCorrelationReferenceLength)
        {
            return SignalManagementSafeResult.Blocked(operation, SignalManagementReasonCode.InvalidCorrelationContext);
        }

        if (!fieldPolicyGuard.IsAllowed)
        {
            return SignalManagementSafeResult.Blocked(operation, fieldPolicyGuard.DeniedReason);
        }

        return SignalManagementSafeResult.Allowed(operation);
    }

    private static bool IsValid(SignalIntakeReference intakeReference) =>
        HasValue(intakeReference.IntakeReferenceToken) &&
        intakeReference.IsApprovedForSignalUse;

    private static bool IsValid(SignalMinimumCaseReference caseReference) =>
        HasValue(caseReference.CaseReferenceToken) &&
        HasValue(caseReference.LifecycleReferenceToken) &&
        caseReference.IsApprovedForSignalUse;

    private static bool IsValid(SignalCodedOutputReference codedOutputReference) =>
        HasValue(codedOutputReference.CodedOutputReferenceToken) &&
        HasValue(codedOutputReference.DictionaryVersionReferenceToken) &&
        codedOutputReference.IsApprovedForSignalUse;

    private static bool IsValid(Mod0004MetricReferenceToken metricReference) =>
        HasValue(metricReference.MetricReferenceToken) &&
        HasValue(metricReference.ThresholdReferenceToken) &&
        metricReference.IsApprovedForSignalUse;

    private static bool IsValid(SignalThresholdDecisionPlaceholderReference thresholdDecisionPlaceholderReference) =>
        HasValue(thresholdDecisionPlaceholderReference.ThresholdDecisionReferenceToken) &&
        HasValue(thresholdDecisionPlaceholderReference.ThresholdComparisonReferenceToken) &&
        HasValue(thresholdDecisionPlaceholderReference.InsufficientDataRuleReferenceToken) &&
        thresholdDecisionPlaceholderReference.IsApprovedForSignalUse;

    private static bool IsValid(Mod0063DataProductCohortReferenceToken dataProductCohortReference) =>
        HasValue(dataProductCohortReference.DataProductReferenceToken) &&
        HasValue(dataProductCohortReference.CohortReferenceToken) &&
        HasValue(dataProductCohortReference.LineageReferenceToken) &&
        dataProductCohortReference.IsApprovedForSignalUse;

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);
}
