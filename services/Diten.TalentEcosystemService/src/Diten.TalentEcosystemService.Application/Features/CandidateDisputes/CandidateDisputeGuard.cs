using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.CandidateDisputes;

public static class CandidateDisputeGuard
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
        "tep.reference-exchange",
        "tep.rehire-recommendations"
    ];

    private static readonly string[] ForbiddenMarkers =
    [
        "rawpayload",
        "raw_payload",
        "providerpayload",
        "provider_payload",
        "responsebody",
        "response_body",
        "disputebody",
        "dispute_body",
        "disputenarrative",
        "dispute_narrative",
        "complaintbody",
        "complaint_body",
        "freetextcomplaint",
        "free_text_complaint",
        "documentpayload",
        "document_payload",
        "documentbody",
        "document_body",
        "attachmentpayload",
        "attachment_payload",
        "evidencebody",
        "evidence_body",
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
        "automateddecision",
        "automated_decision",
        "override",
        "recalculation",
        "marketplacetransaction",
        "marketplace_transaction",
        "notificationdelivery",
        "documentrepository"
    ];

    public static Response<Guid> RequireTenant(ITenantContext tenantContext) =>
        tenantContext.TenantId is { } tenantId
            ? Response<Guid>.Success(tenantId)
            : Response<Guid>.Fail("Tenant context is required.", 401);

    public static IReadOnlyList<string> ValidateRequest(CandidateDisputeReadinessRequest request)
    {
        var errors = new List<string>();

        ValidateRequiredText(request.Code, "Code", errors);
        ValidateRequiredText(request.DisplayName, "DisplayName", errors);
        ValidateRequiredText(request.SourceContractVersion, "SourceContractVersion", errors);

        if (request.DisputeReadinessVersion <= 0)
        {
            errors.Add("DisputeReadinessVersion must be greater than zero.");
        }

        ValidateOptionalText(request.DeferredReason, "DeferredReason", errors);

        foreach (var dependency in request.DependencyStates)
        {
            ValidateRequiredText(dependency.DependencyKey, "DependencyKey", errors);
            ValidateOptionalText(dependency.Reason, "DependencyReason", errors);
        }

        return errors;
    }

    public static Response<NoContent> ValidateReadinessRequest(CandidateDisputeReadinessRequest request)
    {
        if (IsReadinessRequested(request.DisputeReadinessState)
            && !HasApprovedReadinessPreconditions(
                request.CandidateProfileReference,
                request.ExitReferenceRecordReference,
                request.ReferenceExchangeReference,
                request.RehireRecommendationReference,
                request.ResponseBoundaryState,
                request.DisputeIntakeState,
                request.DisputeReviewState,
                request.ResolutionLifecycleState,
                request.ContestabilityState,
                request.HumanReviewState,
                request.ConsentPreconditionState,
                request.VisibilityApprovalState,
                request.DataScopeState,
                request.EvidenceRetentionState,
                request.AuditReadinessState,
                request.LegalHoldState,
                request.DeletionPolicyState,
                request.SelfServiceBoundaryState,
                request.NotificationDependencyState,
                request.DocumentDependencyState,
                request.AutomatedDecisionBoundaryState,
                request.MarketplaceBoundaryState,
                DependencyStatesAllowReadiness(request.DependencyStates)))
        {
            return Response<NoContent>.Fail(
                "Candidate dispute readiness requires dependency preconditions, approved consent and visibility, bounded data scope, local metadata response/dispute policy, and local metadata audit/retention controls.",
                404);
        }

        return Response<NoContent>.Success(204);
    }

    public static bool IsReadinessRequested(TepCandidateDisputeReadinessState readinessState) =>
        readinessState is TepCandidateDisputeReadinessState.Ready;

    public static bool HasApprovedReadinessPreconditions(
        Guid? candidateProfileReference,
        Guid? exitReferenceRecordReference,
        Guid? referenceExchangeReference,
        Guid? rehireRecommendationReference,
        TepCandidateResponseBoundaryState responseBoundaryState,
        TepDisputeIntakeState disputeIntakeState,
        TepDisputeReviewState disputeReviewState,
        TepResolutionLifecycleState resolutionLifecycleState,
        TepContestabilityState contestabilityState,
        TepHumanReviewState humanReviewState,
        TepConsentRequirementState consentPreconditionState,
        TepVisibilityApprovalState visibilityApprovalState,
        TepDataScopeState dataScopeState,
        TepEvidenceRetentionDecisionState evidenceRetentionState,
        TepAuditReadinessState auditReadinessState,
        TepLocalDeferredPolicyState legalHoldState,
        TepLocalDeferredPolicyState deletionPolicyState,
        TepSelfServiceBoundaryState selfServiceBoundaryState,
        TepExternalDependencyState notificationDependencyState,
        TepExternalDependencyState documentDependencyState,
        TepCandidateDisputeBoundaryState automatedDecisionBoundaryState,
        TepCandidateDisputeBoundaryState marketplaceBoundaryState,
        bool dependencyStatesAllowReadiness) =>
        candidateProfileReference.HasValue
        && exitReferenceRecordReference.HasValue
        && referenceExchangeReference.HasValue
        && rehireRecommendationReference.HasValue
        && responseBoundaryState is TepCandidateResponseBoundaryState.LocalMetadata or TepCandidateResponseBoundaryState.Ready
        && disputeIntakeState is TepDisputeIntakeState.LocalMetadata or TepDisputeIntakeState.Ready
        && disputeReviewState is TepDisputeReviewState.LocalMetadata or TepDisputeReviewState.Ready
        && resolutionLifecycleState is TepResolutionLifecycleState.LocalMetadata or TepResolutionLifecycleState.Ready
        && contestabilityState is TepContestabilityState.LocalMetadata
        && humanReviewState is TepHumanReviewState.LocalMetadata
        && consentPreconditionState is TepConsentRequirementState.Approved
        && visibilityApprovalState is TepVisibilityApprovalState.Approved
        && dataScopeState is TepDataScopeState.Available
        && evidenceRetentionState is TepEvidenceRetentionDecisionState.LocalMetadata
        && auditReadinessState is TepAuditReadinessState.LocalMetadata
        && legalHoldState is TepLocalDeferredPolicyState.LocalMetadata
        && deletionPolicyState is TepLocalDeferredPolicyState.LocalMetadata
        && selfServiceBoundaryState is TepSelfServiceBoundaryState.OutOfScope
        && notificationDependencyState is TepExternalDependencyState.OutOfScope or TepExternalDependencyState.Waived
        && documentDependencyState is TepExternalDependencyState.OutOfScope or TepExternalDependencyState.Waived
        && automatedDecisionBoundaryState is TepCandidateDisputeBoundaryState.OutOfScope
        && marketplaceBoundaryState is TepCandidateDisputeBoundaryState.OutOfScope
        && dependencyStatesAllowReadiness;

    public static CandidateDisputeEvaluationDto Evaluate(
        TepCandidateDisputeReadinessMetadata metadata,
        bool readinessRequested)
    {
        var evaluatedAt = DateTimeOffset.UtcNow;
        var dependencyAllowed = HasApprovedReadinessPreconditions(
            metadata.CandidateProfileReference,
            metadata.ExitReferenceRecordReference,
            metadata.ReferenceExchangeReference,
            metadata.RehireRecommendationReference,
            metadata.ResponseBoundaryState,
            metadata.DisputeIntakeState,
            metadata.DisputeReviewState,
            metadata.ResolutionLifecycleState,
            metadata.ContestabilityState,
            metadata.HumanReviewState,
            metadata.ConsentPreconditionState,
            metadata.VisibilityApprovalState,
            metadata.DataScopeState,
            metadata.EvidenceRetentionState,
            metadata.AuditReadinessState,
            metadata.LegalHoldState,
            metadata.DeletionPolicyState,
            metadata.SelfServiceBoundaryState,
            metadata.NotificationDependencyState,
            metadata.DocumentDependencyState,
            metadata.AutomatedDecisionBoundaryState,
            metadata.MarketplaceBoundaryState,
            DependencyStatesAllowReadiness(metadata.DependencyStates));

        var readinessAllowed = readinessRequested && dependencyAllowed;
        var evaluationDeferred = !readinessRequested && !dependencyAllowed;
        var decision = readinessAllowed
            ? "ReadinessAllowed"
            : evaluationDeferred
                ? "EvaluationDeferred"
                : "FailClosed";

        return new CandidateDisputeEvaluationDto(metadata.Id, readinessAllowed, evaluationDeferred, decision, evaluatedAt);
    }

    public static bool DependencyStatesAllowReadiness(IEnumerable<CandidateDisputeDependencyStateDto> dependencyStates) =>
        RequiredDependencyKeys.All(requiredKey => dependencyStates.Any(state =>
            string.Equals(state.DependencyKey, requiredKey, StringComparison.Ordinal)
            && state.State is TepShellDependencyStatus.Available));

    public static bool DependencyStatesAllowReadiness(IEnumerable<TepCandidateDisputeDependencyState> dependencyStates) =>
        RequiredDependencyKeys.All(requiredKey => dependencyStates.Any(state =>
            string.Equals(state.DependencyKey, requiredKey, StringComparison.Ordinal)
            && state.State is TepShellDependencyStatus.Available));

    public static IReadOnlyList<TepCandidateDisputeDependencyState> BuildRequiredDependencyStates(TepShellDependencyStatus state, string? reason) =>
        RequiredDependencyKeys
            .Select(key => new TepCandidateDisputeDependencyState
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
            errors.Add($"{field} contains a forbidden candidate dispute marker.");
        }
    }

    private static void ValidateOptionalText(string? value, string field, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(value) && ContainsForbiddenMarker(value))
        {
            errors.Add($"{field} contains a forbidden candidate dispute marker.");
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
