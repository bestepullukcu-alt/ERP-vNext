using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.ReviewBoard;

public static class ReviewBoardGuard
{
    private static readonly string[] ForbiddenMarkers =
    [
        "candidateidentity",
        "candidate_identity",
        "talentidentity",
        "talent_identity",
        "talentprofile",
        "talent_profile",
        "referenceexchange",
        "reference_exchange",
        "reputation",
        "risk",
        "analytics",
        "disputeworkflow",
        "dispute_workflow",
        "externalreviewboardintegration",
        "external_review_board_integration",
        "rawpayload",
        "raw_payload",
        "providerpayload",
        "provider_payload",
        "credential",
        "token",
        "secret",
        "password",
        "payslip",
        "payroll",
        "bank",
        "tax",
        "biometric",
        "geolocation",
        "nationalid",
        "national_id",
        "dateofbirth",
        "dob",
        "homeaddress",
        "home_address",
        "piiheavy",
        "pii_heavy"
    ];

    public static Response<Guid> RequireTenant(ITenantContext tenantContext) =>
        tenantContext.TenantId is { } tenantId
            ? Response<Guid>.Success(tenantId)
            : Response<Guid>.Fail("Tenant context is required.", 401);

    public static IReadOnlyList<string> ValidateRequest(ReviewBoardCaseRequest request)
    {
        var errors = new List<string>();

        ValidateRequiredText(request.Code, "Code", errors);
        ValidateRequiredText(request.DisplayName, "DisplayName", errors);
        ValidateRequiredText(request.SourceContractVersion, "SourceContractVersion", errors);
        ValidateOptionalText(request.DeferredReason, "DeferredReason", errors);

        if (request.ReviewBoardVersion <= 0)
        {
            errors.Add("ReviewBoardVersion must be greater than zero.");
        }

        foreach (var dependency in request.DependencyStates)
        {
            ValidateRequiredText(dependency.DependencyKey, "DependencyKey", errors);
            ValidateOptionalText(dependency.Reason, "DependencyReason", errors);
        }

        return errors;
    }

    public static Response<NoContent> ValidateReviewDecisionRequest(ReviewBoardCaseRequest request)
    {
        if (IsDecisionRequested(request.ReviewBoardCaseState, request.ReviewDecisionState)
            && !HasReviewDecisionPreconditions(
                request.AssociationMembershipRegistryId,
                request.ConsentVisibilityPolicyId,
                request.VerifiedParticipantAccessId,
                request.ReviewerEligibilityState,
                request.SegregationOfDutiesState,
                request.LegalSecurityDecisionState,
                request.ExternalReviewBoardState))
        {
            return Response<NoContent>.Fail(
                "Review-board decisions require same-tenant Association, Consent/Visibility, Verified Access, reviewer eligibility, segregation-of-duties, and legal/security metadata preconditions.",
                404);
        }

        return Response<NoContent>.Success(204);
    }

    public static bool IsDecisionRequested(TepReviewBoardCaseState caseState, TepReviewDecisionState decisionState) =>
        caseState is TepReviewBoardCaseState.DecisionRecorded
        || decisionState is TepReviewDecisionState.Approved or TepReviewDecisionState.Rejected or TepReviewDecisionState.Escalated;

    public static bool HasReviewDecisionPreconditions(
        Guid? associationMembershipRegistryId,
        Guid? consentVisibilityPolicyId,
        Guid? verifiedParticipantAccessId,
        TepReviewerEligibilityState reviewerEligibilityState,
        TepSegregationOfDutiesState segregationOfDutiesState,
        TepLegalSecurityDecisionState legalSecurityDecisionState,
        TepExternalReviewBoardState externalReviewBoardState) =>
        associationMembershipRegistryId.HasValue
        && consentVisibilityPolicyId.HasValue
        && verifiedParticipantAccessId.HasValue
        && reviewerEligibilityState is TepReviewerEligibilityState.Eligible
        && segregationOfDutiesState is TepSegregationOfDutiesState.Passed
        && legalSecurityDecisionState is TepLegalSecurityDecisionState.Approved
        && externalReviewBoardState is TepExternalReviewBoardState.NotRequired;

    public static ReviewBoardEvaluationDto Evaluate(
        TepReviewBoardCaseMetadata metadata,
        TepAssociationMembershipRegistry? association,
        TepConsentVisibilityPolicy? policy,
        TepVerifiedParticipantAccess? verifiedAccess,
        bool reviewRequested)
    {
        var evaluatedAt = DateTimeOffset.UtcNow;
        var preconditionsAllowReview = AssociationAllowsReview(metadata, association)
            && PolicyAllowsReview(policy)
            && VerifiedAccessAllowsReview(metadata, verifiedAccess)
            && HasReviewDecisionPreconditions(
                metadata.AssociationMembershipRegistryId,
                metadata.ConsentVisibilityPolicyId,
                metadata.VerifiedParticipantAccessId,
                metadata.ReviewerEligibilityState,
                metadata.SegregationOfDutiesState,
                metadata.LegalSecurityDecisionState,
                metadata.ExternalReviewBoardState);

        var reviewAllowed = reviewRequested && preconditionsAllowReview;
        var evaluationDeferred = !reviewRequested && !preconditionsAllowReview;
        var decision = reviewAllowed
            ? "ReviewAllowed"
            : evaluationDeferred
                ? "EvaluationDeferred"
                : "FailClosed";

        return new ReviewBoardEvaluationDto(metadata.Id, reviewAllowed, evaluationDeferred, decision, evaluatedAt);
    }

    public static bool AssociationAllowsReview(TepReviewBoardCaseMetadata metadata, TepAssociationMembershipRegistry? association) =>
        association is not null
        && metadata.AssociationMembershipRegistryId == association.Id
        && association.AssociationMembershipState is TepAssociationMembershipState.Active
        && association.MemberCompanyState is TepMemberCompanyState.Verified
        && association.PolicyEvaluationState is TepPolicyEvaluationState.Approved
        && association.VisibilityApprovalState is TepVisibilityApprovalState.Approved
        && association.AssociationActivationState is TepAssociationActivationState.Active;

    public static bool PolicyAllowsReview(TepConsentVisibilityPolicy? policy) =>
        policy is not null
        && policy.PolicyState is TepPolicyState.Active
        && policy.ConsentRequirementState is TepConsentRequirementState.Approved
        && policy.VisibilityScope is TepVisibilityScope.AssociationVisible
        && policy.DataScopeState is TepDataScopeState.Available
        && policy.AccessPolicyState is TepAccessPolicyState.Approved
        && policy.AssociationConsumptionState is TepAssociationConsumptionState.ActivationApproved
        && policy.PolicyUnavailableBehavior is TepPolicyUnavailableBehavior.FailClosed;

    public static bool VerifiedAccessAllowsReview(TepReviewBoardCaseMetadata metadata, TepVerifiedParticipantAccess? verifiedAccess) =>
        verifiedAccess is not null
        && metadata.VerifiedParticipantAccessId == verifiedAccess.Id
        && verifiedAccess.VerificationState is TepVerificationState.Verified
        && verifiedAccess.AccessState is TepAccessState.Active
        && verifiedAccess.PolicyEvaluationState is TepPolicyEvaluationState.Approved
        && verifiedAccess.VisibilityApprovalState is TepVisibilityApprovalState.Approved
        && verifiedAccess.AssociationValidationState is TepShellDependencyStatus.Available
        && verifiedAccess.VerifiedCompanyAccessState is TepVerifiedCompanyAccessState.Ready;

    private static void ValidateRequiredText(string value, string field, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{field} is required.");
        }
        else if (ContainsForbiddenMarker(value))
        {
            errors.Add($"{field} contains a forbidden TEP runtime marker.");
        }
    }

    private static void ValidateOptionalText(string? value, string field, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(value) && ContainsForbiddenMarker(value))
        {
            errors.Add($"{field} contains a forbidden TEP runtime marker.");
        }
    }

    private static bool ContainsForbiddenMarker(string value)
    {
        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return ForbiddenMarkers.Any(marker => normalized.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
