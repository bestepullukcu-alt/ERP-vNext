using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.RehireRecommendations;

public static class RehireRecommendationGuard
{
    private static readonly string[] RequiredDependencyKeys =
    [
        "tep.association-memberships",
        "tep.consent-visibility-policies",
        "tep.verified-participants",
        "tep.review-board",
        "tep.trust-levels",
        "tep.candidate-profiles",
        "tep.exit-reference-records",
        "tep.reference-exchange"
    ];

    private static readonly string[] ForbiddenMarkers =
    [
        "rawpayload",
        "raw_payload",
        "providerpayload",
        "provider_payload",
        "profilebody",
        "profile_body",
        "credentialsecret",
        "credential_secret",
        "secret",
        "password",
        "token",
        "dateofbirth",
        "dob",
        "nationalid",
        "national_id",
        "homeaddress",
        "home_address",
        "payroll",
        "payslip",
        "bank",
        "tax",
        "biometric",
        "geolocation",
        "piiheavy",
        "pii_heavy",
        "score",
        "rank",
        "modeloutput",
        "model_output",
        "eligibilitylabel",
        "eligibility_label",
        "automateddecision",
        "automated_decision",
        "candidatepayload",
        "candidate_payload",
        "disputeworkflow",
        "dispute_workflow",
        "notificationdelivery",
        "documentrepository"
    ];

    public static Response<Guid> RequireTenant(ITenantContext tenantContext) =>
        tenantContext.TenantId is { } tenantId
            ? Response<Guid>.Success(tenantId)
            : Response<Guid>.Fail("Tenant context is required.", 401);

    public static IReadOnlyList<string> ValidateRequest(RehireRecommendationReadinessRequest request)
    {
        var errors = new List<string>();

        ValidateRequiredText(request.Code, "Code", errors);
        ValidateRequiredText(request.DisplayName, "DisplayName", errors);
        ValidateRequiredText(request.SourceContractVersion, "SourceContractVersion", errors);

        if (request.RecommendationNetworkVersion <= 0)
        {
            errors.Add("RecommendationNetworkVersion must be greater than zero.");
        }

        ValidateOptionalText(request.DeferredReason, "DeferredReason", errors);

        foreach (var dependency in request.DependencyStates)
        {
            ValidateRequiredText(dependency.DependencyKey, "DependencyKey", errors);
            ValidateOptionalText(dependency.Reason, "DependencyReason", errors);
        }

        return errors;
    }

    public static Response<NoContent> ValidateReadinessRequest(RehireRecommendationReadinessRequest request)
    {
        if (IsReadinessRequested(request.RecommendationReadinessState)
            && !HasApprovedReadinessPreconditions(
                request.ReferenceExchangeReference,
                request.ExitReferenceRecordReference,
                request.CandidateProfileReference,
                request.VerifiedParticipantReference,
                request.AssociationMembershipReference,
                request.ConsentVisibilityPolicyReference,
                request.ReviewBoardCaseReference,
                request.TrustLevelPolicyReference,
                request.RecommendationPolicyState,
                request.RecommendationEvaluationState,
                request.EligibilityPreconditionState,
                request.ConsentPreconditionState,
                request.VisibilityApprovalState,
                request.DataScopeState,
                request.MinimizationState,
                request.ExplainabilityState,
                request.HumanReviewState,
                request.ContestabilityState,
                request.CandidateResponseBoundaryState,
                request.AbuseControlState,
                request.MisuseDetectionState,
                request.ThrottlingState,
                request.EscalationState,
                request.EvidenceRetentionState,
                request.AuditReadinessState,
                request.LegalHoldState,
                request.DeletionPolicyState,
                DependencyStatesAllowReadiness(request.DependencyStates)))
        {
            return Response<NoContent>.Fail(
                "Rehire recommendation readiness requires dependency preconditions, approved consent and visibility, minimized data scope, local metadata explainability/review/contestability boundaries, and local metadata audit/retention controls.",
                404);
        }

        return Response<NoContent>.Success(204);
    }

    public static bool IsReadinessRequested(TepRehireRecommendationReadinessState readinessState) =>
        readinessState is TepRehireRecommendationReadinessState.Ready;

    public static bool HasApprovedReadinessPreconditions(
        Guid? referenceExchangeReference,
        Guid? exitReferenceRecordReference,
        Guid? candidateProfileReference,
        Guid? verifiedParticipantReference,
        Guid? associationMembershipReference,
        Guid? consentVisibilityPolicyReference,
        Guid? reviewBoardCaseReference,
        Guid? trustLevelPolicyReference,
        TepRehireRecommendationPolicyState recommendationPolicyState,
        TepRehireRecommendationEvaluationState recommendationEvaluationState,
        TepRecommendationEligibilityPreconditionState eligibilityPreconditionState,
        TepConsentRequirementState consentPreconditionState,
        TepVisibilityApprovalState visibilityApprovalState,
        TepDataScopeState dataScopeState,
        TepDataMinimizationState minimizationState,
        TepRecommendationExplainabilityState explainabilityState,
        TepHumanReviewState humanReviewState,
        TepContestabilityState contestabilityState,
        TepReviewDisputeBoundaryState candidateResponseBoundaryState,
        TepAbuseControlState abuseControlState,
        TepMisuseDetectionState misuseDetectionState,
        TepThrottlingPolicyState throttlingState,
        TepEscalationState escalationState,
        TepEvidenceRetentionDecisionState evidenceRetentionState,
        TepAuditReadinessState auditReadinessState,
        TepLocalDeferredPolicyState legalHoldState,
        TepLocalDeferredPolicyState deletionPolicyState,
        bool dependencyStatesAllowReadiness) =>
        referenceExchangeReference.HasValue
        && exitReferenceRecordReference.HasValue
        && candidateProfileReference.HasValue
        && verifiedParticipantReference.HasValue
        && associationMembershipReference.HasValue
        && consentVisibilityPolicyReference.HasValue
        && reviewBoardCaseReference.HasValue
        && trustLevelPolicyReference.HasValue
        && recommendationPolicyState is TepRehireRecommendationPolicyState.LocalMetadata or TepRehireRecommendationPolicyState.Approved
        && recommendationEvaluationState is TepRehireRecommendationEvaluationState.Ready
        && eligibilityPreconditionState is TepRecommendationEligibilityPreconditionState.LocalMetadata
        && consentPreconditionState is TepConsentRequirementState.Approved
        && visibilityApprovalState is TepVisibilityApprovalState.Approved
        && dataScopeState is TepDataScopeState.Available
        && minimizationState is TepDataMinimizationState.Approved
        && explainabilityState is TepRecommendationExplainabilityState.LocalMetadata
        && humanReviewState is TepHumanReviewState.LocalMetadata
        && contestabilityState is TepContestabilityState.LocalMetadata
        && candidateResponseBoundaryState is TepReviewDisputeBoundaryState.PreconditionSatisfied
        && abuseControlState is TepAbuseControlState.LocalMetadata
        && misuseDetectionState is TepMisuseDetectionState.LocalMetadata
        && throttlingState is TepThrottlingPolicyState.LocalMetadata
        && escalationState is TepEscalationState.LocalMetadata
        && evidenceRetentionState is TepEvidenceRetentionDecisionState.LocalMetadata
        && auditReadinessState is TepAuditReadinessState.LocalMetadata
        && legalHoldState is TepLocalDeferredPolicyState.LocalMetadata
        && deletionPolicyState is TepLocalDeferredPolicyState.LocalMetadata
        && dependencyStatesAllowReadiness;

    public static RehireRecommendationEvaluationDto Evaluate(
        TepRehireRecommendationReadinessMetadata metadata,
        bool readinessRequested)
    {
        var evaluatedAt = DateTimeOffset.UtcNow;
        var dependencyAllowed = HasApprovedReadinessPreconditions(
            metadata.ReferenceExchangeReference,
            metadata.ExitReferenceRecordReference,
            metadata.CandidateProfileReference,
            metadata.VerifiedParticipantReference,
            metadata.AssociationMembershipReference,
            metadata.ConsentVisibilityPolicyReference,
            metadata.ReviewBoardCaseReference,
            metadata.TrustLevelPolicyReference,
            metadata.RecommendationPolicyState,
            metadata.RecommendationEvaluationState,
            metadata.EligibilityPreconditionState,
            metadata.ConsentPreconditionState,
            metadata.VisibilityApprovalState,
            metadata.DataScopeState,
            metadata.MinimizationState,
            metadata.ExplainabilityState,
            metadata.HumanReviewState,
            metadata.ContestabilityState,
            metadata.CandidateResponseBoundaryState,
            metadata.AbuseControlState,
            metadata.MisuseDetectionState,
            metadata.ThrottlingState,
            metadata.EscalationState,
            metadata.EvidenceRetentionState,
            metadata.AuditReadinessState,
            metadata.LegalHoldState,
            metadata.DeletionPolicyState,
            DependencyStatesAllowReadiness(metadata.DependencyStates));

        var readinessAllowed = readinessRequested && dependencyAllowed;
        var evaluationDeferred = !readinessRequested && !dependencyAllowed;
        var decision = readinessAllowed
            ? "ReadinessAllowed"
            : evaluationDeferred
                ? "EvaluationDeferred"
                : "FailClosed";

        return new RehireRecommendationEvaluationDto(metadata.Id, readinessAllowed, evaluationDeferred, decision, evaluatedAt);
    }

    public static bool DependencyStatesAllowReadiness(IEnumerable<RehireRecommendationDependencyStateDto> dependencyStates) =>
        RequiredDependencyKeys.All(requiredKey => dependencyStates.Any(state =>
            string.Equals(state.DependencyKey, requiredKey, StringComparison.Ordinal)
            && state.State is TepShellDependencyStatus.Available));

    public static bool DependencyStatesAllowReadiness(IEnumerable<TepRehireRecommendationDependencyState> dependencyStates) =>
        RequiredDependencyKeys.All(requiredKey => dependencyStates.Any(state =>
            string.Equals(state.DependencyKey, requiredKey, StringComparison.Ordinal)
            && state.State is TepShellDependencyStatus.Available));

    public static IReadOnlyList<TepRehireRecommendationDependencyState> BuildRequiredDependencyStates(TepShellDependencyStatus state, string? reason) =>
        RequiredDependencyKeys
            .Select(key => new TepRehireRecommendationDependencyState
            {
                DependencyKey = key,
                State = state,
                Reason = reason
            })
            .ToList();

    private static void ValidateRequiredText(string value, string field, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{field} is required.");
        }
        else if (ContainsForbiddenMarker(value))
        {
            errors.Add($"{field} contains a forbidden rehire recommendation marker.");
        }
    }

    private static void ValidateOptionalText(string? value, string field, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(value) && ContainsForbiddenMarker(value))
        {
            errors.Add($"{field} contains a forbidden rehire recommendation marker.");
        }
    }

    private static bool ContainsForbiddenMarker(string value)
    {
        var normalized = value
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return ForbiddenMarkers.Any(marker => normalized.Contains(
            marker.Replace("_", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal));
    }
}
