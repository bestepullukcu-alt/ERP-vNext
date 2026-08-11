using Diten.PvgService.Domain.CaseProcessing;

namespace Diten.PvgService.Application.CaseProcessing;

public static class PvgCaseProcessingValidator
{
    public static PvgCaseProcessingValidationResult ValidateAcceptHandoff(AcceptMod0230HandoffCommand? command)
    {
        var failures = ValidateCommon(command?.TenantContext, command?.ActorContext, command?.CorrelationContext, command?.GuardContext);

        if (command?.HandoffReference is null)
        {
            failures.Add(new("HandoffReference", PvgCaseProcessingReasonCodes.Mod0230HandoffRequired));
        }
        else
        {
            ValidateHandoffReference(command.HandoffReference, failures);
        }

        return ToResult(failures);
    }

    public static PvgCaseProcessingValidationResult ValidateUpdateAssessment(UpdateSignalMinimumAssessmentCommand? command)
    {
        var failures = ValidateCommon(command?.TenantContext, command?.ActorContext, command?.CorrelationContext, command?.GuardContext);
        AddRequired(command?.CaseProcessingId, "CaseProcessingId", PvgCaseProcessingReasonCodes.CaseProcessingIdRequired, failures);

        if (command?.Assessment is null)
        {
            failures.Add(new("Assessment", PvgCaseProcessingReasonCodes.AssessmentRequired));
        }
        else
        {
            ValidateAssessment(command.Assessment, failures);
        }

        return ToResult(failures);
    }

    public static PvgCaseProcessingValidationResult ValidateMarkSignalMinimumReady(MarkSignalMinimumReadyCommand? command)
    {
        var failures = ValidateCommon(command?.TenantContext, command?.ActorContext, command?.CorrelationContext, command?.GuardContext);
        AddRequired(command?.CaseProcessingId, "CaseProcessingId", PvgCaseProcessingReasonCodes.CaseProcessingIdRequired, failures);
        return ToResult(failures);
    }

    public static PvgCaseProcessingResult ToResult(PvgCaseProcessingValidationResult validation) =>
        validation.IsValid
            ? PvgCaseProcessingResult.Accepted(new("Validate", "pvg.case-processing.validate", "system", true))
            : PvgCaseProcessingResult.Blocked(validation.Failures.Select(failure => failure.ReasonCode).ToArray());

    private static List<PvgCaseProcessingValidationFailure> ValidateCommon(
        PvgCaseProcessingServerTenantContext? tenantContext,
        PvgCaseProcessingActorContext? actorContext,
        PvgCaseProcessingCorrelationContext? correlationContext,
        PvgCaseProcessingGuardContext? guardContext)
    {
        var failures = new List<PvgCaseProcessingValidationFailure>();

        if (tenantContext is null || string.IsNullOrWhiteSpace(tenantContext.TenantId))
        {
            failures.Add(new(null, PvgCaseProcessingReasonCodes.TenantContextRequired));
        }

        if (actorContext is null || string.IsNullOrWhiteSpace(actorContext.ActorId) || string.IsNullOrWhiteSpace(actorContext.ActorKind))
        {
            failures.Add(new(null, PvgCaseProcessingReasonCodes.ActorContextRequired));
        }

        if (correlationContext is null || string.IsNullOrWhiteSpace(correlationContext.CorrelationId))
        {
            failures.Add(new(null, PvgCaseProcessingReasonCodes.CorrelationContextRequired));
        }
        else if (!correlationContext.IsValid)
        {
            failures.Add(new(null, PvgCaseProcessingReasonCodes.CorrelationContextInvalid));
        }

        ValidateGuardContext(guardContext, failures);
        return failures;
    }

    private static void ValidateGuardContext(
        PvgCaseProcessingGuardContext? guardContext,
        ICollection<PvgCaseProcessingValidationFailure> failures)
    {
        if (guardContext?.PermissionDecision is null)
        {
            failures.Add(new(null, PvgCaseProcessingReasonCodes.PermissionContextRequired));
        }
        else if (!guardContext.PermissionDecision.IsAllowed)
        {
            failures.Add(new(null, PvgCaseProcessingReasonCodes.PermissionDenied));
        }

        AddPortFailure(guardContext?.FieldPolicyDecision, PvgCaseProcessingReasonCodes.FieldPolicyRequired, PvgCaseProcessingReasonCodes.FieldPolicyDenied, failures);
        AddPortFailure(guardContext?.WorkflowGateDecision, PvgCaseProcessingReasonCodes.WorkflowGateRequired, PvgCaseProcessingReasonCodes.WorkflowGateDenied, failures);
        AddPortFailure(guardContext?.EvidenceCompletenessDecision, PvgCaseProcessingReasonCodes.EvidenceCompletenessRequired, PvgCaseProcessingReasonCodes.EvidenceCompletenessDenied, failures);
    }

    private static void AddPortFailure(
        PvgCaseProcessingPortDecision? decision,
        string missingReasonCode,
        string deniedReasonCode,
        ICollection<PvgCaseProcessingValidationFailure> failures)
    {
        if (decision is null)
        {
            failures.Add(new(null, missingReasonCode));
        }
        else if (!decision.IsAllowed)
        {
            failures.Add(new(null, deniedReasonCode));
        }
    }

    private static void ValidateHandoffReference(
        Mod0230HandoffReference handoffReference,
        ICollection<PvgCaseProcessingValidationFailure> failures)
    {
        AddRequired(handoffReference.IntakeDraftId, "HandoffReference.IntakeDraftId", PvgCaseProcessingReasonCodes.HandoffReferenceInvalid, failures);
        AddRequired(handoffReference.IntakeNumber, "HandoffReference.IntakeNumber", PvgCaseProcessingReasonCodes.HandoffReferenceInvalid, failures);
        AddRequired(handoffReference.TriageOutcomeCode, "HandoffReference.TriageOutcomeCode", PvgCaseProcessingReasonCodes.HandoffReferenceInvalid, failures);
        AddRequired(handoffReference.RouteTargetQueueCode, "HandoffReference.RouteTargetQueueCode", PvgCaseProcessingReasonCodes.HandoffReferenceInvalid, failures);

        if (handoffReference.ReceivedAtUtc == default)
        {
            failures.Add(new("HandoffReference.ReceivedAtUtc", PvgCaseProcessingReasonCodes.HandoffReferenceInvalid));
        }

        if (handoffReference.EvidenceLinkReferenceIds.Count == 0)
        {
            failures.Add(new("HandoffReference.EvidenceLinkReferenceIds", PvgCaseProcessingReasonCodes.EvidenceCompletenessRequired));
        }
    }

    private static void ValidateAssessment(
        SignalMinimumAssessment assessment,
        ICollection<PvgCaseProcessingValidationFailure> failures)
    {
        AddRequired(assessment.CaseProcessingPriority, nameof(assessment.CaseProcessingPriority), PvgCaseProcessingReasonCodes.RequiredFieldMissing, failures);
        AddRequired(assessment.CaseValidityStatus, nameof(assessment.CaseValidityStatus), PvgCaseProcessingReasonCodes.RequiredFieldMissing, failures);
        AddRequired(assessment.ProcessingOwnerQueue, nameof(assessment.ProcessingOwnerQueue), PvgCaseProcessingReasonCodes.RequiredFieldMissing, failures);
        AddRequired(assessment.ProductExposureAssessment, nameof(assessment.ProductExposureAssessment), PvgCaseProcessingReasonCodes.RequiredFieldMissing, failures);
        AddRequired(assessment.SeriousnessConfirmed, nameof(assessment.SeriousnessConfirmed), PvgCaseProcessingReasonCodes.RequiredFieldMissing, failures);
        AddRequired(assessment.EventAssessmentSummary, nameof(assessment.EventAssessmentSummary), PvgCaseProcessingReasonCodes.RequiredFieldMissing, failures);
        AddRequired(assessment.EvidenceCompletenessStatus, nameof(assessment.EvidenceCompletenessStatus), PvgCaseProcessingReasonCodes.RequiredFieldMissing, failures);
        AddRequired(assessment.SignalRelevanceFlag, nameof(assessment.SignalRelevanceFlag), PvgCaseProcessingReasonCodes.RequiredFieldMissing, failures);
        AddRequired(assessment.SignalHandoffReadiness, nameof(assessment.SignalHandoffReadiness), PvgCaseProcessingReasonCodes.RequiredFieldMissing, failures);
    }

    private static void AddRequired(
        string? value,
        string field,
        string reasonCode,
        ICollection<PvgCaseProcessingValidationFailure> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add(new(field, reasonCode));
        }
    }

    private static PvgCaseProcessingValidationResult ToResult(List<PvgCaseProcessingValidationFailure> failures) =>
        failures.Count == 0 ? PvgCaseProcessingValidationResult.Valid : new(failures);
}
