using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.CandidateProfiles;

public static class CandidateProfileGuard
{
    private static readonly string[] ForbiddenMarkers =
    [
        "rawprofilebody",
        "raw_profile_body",
        "rawpayload",
        "raw_payload",
        "providerpayload",
        "provider_payload",
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
        "referenceexchange",
        "reference_exchange",
        "marketplace",
        "recommendation",
        "reputation",
        "riskanalytics",
        "risk_analytics"
    ];

    public static Response<Guid> RequireTenant(ITenantContext tenantContext) =>
        tenantContext.TenantId is { } tenantId
            ? Response<Guid>.Success(tenantId)
            : Response<Guid>.Fail("Tenant context is required.", 401);

    public static IReadOnlyList<string> ValidateRequest(CandidateProfileRequest request)
    {
        var errors = new List<string>();

        ValidateRequiredText(request.Code, "Code", errors);
        ValidateRequiredText(request.DisplayName, "DisplayName", errors);
        ValidateRequiredText(request.CandidateReference, "CandidateReference", errors);
        ValidateRequiredText(request.TalentProfileReference, "TalentProfileReference", errors);
        ValidateRequiredText(request.HcmFoundationReference, "HcmFoundationReference", errors);
        ValidateRequiredText(request.SkillSummaryMetadata, "SkillSummaryMetadata", errors);
        ValidateRequiredText(request.CredentialSummaryMetadata, "CredentialSummaryMetadata", errors);
        ValidateRequiredText(request.ExperienceSummaryMetadata, "ExperienceSummaryMetadata", errors);
        ValidateRequiredText(request.SourceContractVersion, "SourceContractVersion", errors);

        if (request.CandidateVersion <= 0)
        {
            errors.Add("CandidateVersion must be greater than zero.");
        }

        ValidateOptionalText(request.DeferredReason, "DeferredReason", errors);

        foreach (var dependency in request.DependencyStates)
        {
            ValidateRequiredText(dependency.DependencyKey, "DependencyKey", errors);
            ValidateOptionalText(dependency.Reason, "DependencyReason", errors);
        }

        return errors;
    }

    public static Response<NoContent> ValidateActivationRequest(CandidateProfileRequest request)
    {
        if (IsActivationRequested(request.CandidateIdentityState, request.TalentProfileState)
            && !HasApprovedCandidateProfilePreconditions(
                request.AssociationMembershipId,
                request.ConsentVisibilityPolicyId,
                request.VerifiedParticipantId,
                request.ReviewBoardCaseId,
                request.TrustLevelPolicyId,
                request.ConsentBasisState,
                request.PolicyEvaluationState,
                request.ProfilePolicyEvaluationState,
                request.VisibilityApprovalState,
                request.DataScopeState,
                request.DataMinimizationState,
                request.ProfileCompletenessState,
                request.VisibilityClassification,
                request.AssociationValidationState,
                request.VerifiedAccessValidationState,
                request.ReviewBoardValidationState,
                request.TrustLevelValidationState,
                request.HcmValidationState))
        {
            return Response<NoContent>.Fail(
                "Candidate profile activation requires same-tenant Association, Consent/Visibility, Verified Access, Review Board, Trust-Level, HCM context, approved consent basis, available data scope, and approved data minimization.",
                404);
        }

        return Response<NoContent>.Success(204);
    }

    public static bool IsActivationRequested(TepCandidateIdentityState candidateState, TepTalentProfileState profileState) =>
        candidateState is TepCandidateIdentityState.Active || profileState is TepTalentProfileState.Published;

    public static bool HasApprovedCandidateProfilePreconditions(
        Guid? associationMembershipId,
        Guid? consentVisibilityPolicyId,
        Guid? verifiedParticipantId,
        Guid? reviewBoardCaseId,
        Guid? trustLevelPolicyId,
        TepCandidateConsentBasisState consentBasis,
        TepPolicyEvaluationState policyEvaluation,
        TepPolicyEvaluationState profilePolicyEvaluation,
        TepVisibilityApprovalState visibility,
        TepDataScopeState dataScope,
        TepDataMinimizationState dataMinimization,
        TepProfileCompletenessState profileCompleteness,
        TepCandidateVisibilityClassification visibilityClassification,
        TepShellDependencyStatus associationValidation,
        TepShellDependencyStatus verifiedAccessValidation,
        TepShellDependencyStatus reviewBoardValidation,
        TepShellDependencyStatus trustLevelValidation,
        TepShellDependencyStatus hcmValidation) =>
        associationMembershipId.HasValue
        && consentVisibilityPolicyId.HasValue
        && verifiedParticipantId.HasValue
        && reviewBoardCaseId.HasValue
        && trustLevelPolicyId.HasValue
        && consentBasis is TepCandidateConsentBasisState.Approved
        && policyEvaluation is TepPolicyEvaluationState.Approved
        && profilePolicyEvaluation is TepPolicyEvaluationState.Approved
        && visibility is TepVisibilityApprovalState.Approved
        && dataScope is TepDataScopeState.Available
        && dataMinimization is TepDataMinimizationState.Approved
        && profileCompleteness is TepProfileCompletenessState.Complete
        && visibilityClassification is TepCandidateVisibilityClassification.AssociationVisible
        && associationValidation is TepShellDependencyStatus.Available
        && verifiedAccessValidation is TepShellDependencyStatus.Available
        && reviewBoardValidation is TepShellDependencyStatus.Available
        && trustLevelValidation is TepShellDependencyStatus.Available
        && hcmValidation is TepShellDependencyStatus.Available;

    public static CandidateProfileEvaluationDto Evaluate(
        TepCandidateProfileMetadata profile,
        TepAssociationMembershipRegistry? association,
        TepConsentVisibilityPolicy? policy,
        TepVerifiedParticipantAccess? verifiedAccess,
        TepReviewBoardCaseMetadata? reviewCase,
        TepTrustLevelPolicyMetadata? trustPolicy,
        bool activationRequested)
    {
        var evaluatedAt = DateTimeOffset.UtcNow;
        var dependencyAllowed = AssociationAllowsCandidate(profile, association)
            && PolicyAllowsCandidate(policy)
            && VerifiedAccessAllowsCandidate(profile, verifiedAccess)
            && ReviewBoardAllowsCandidate(profile, reviewCase)
            && TrustLevelAllowsCandidate(profile, trustPolicy)
            && HasApprovedCandidateProfilePreconditions(
                profile.AssociationMembershipId,
                profile.ConsentVisibilityPolicyId,
                profile.VerifiedParticipantId,
                profile.ReviewBoardCaseId,
                profile.TrustLevelPolicyId,
                profile.ConsentBasisState,
                profile.PolicyEvaluationState,
                profile.ProfilePolicyEvaluationState,
                profile.VisibilityApprovalState,
                profile.DataScopeState,
                profile.DataMinimizationState,
                profile.ProfileCompletenessState,
                profile.VisibilityClassification,
                profile.AssociationValidationState,
                profile.VerifiedAccessValidationState,
                profile.ReviewBoardValidationState,
                profile.TrustLevelValidationState,
                profile.HcmValidationState);

        var activationAllowed = activationRequested && dependencyAllowed;
        var evaluationDeferred = !activationRequested && !dependencyAllowed;
        var decision = activationAllowed
            ? "ActivationAllowed"
            : evaluationDeferred
                ? "EvaluationDeferred"
                : "FailClosed";

        return new CandidateProfileEvaluationDto(profile.Id, activationAllowed, evaluationDeferred, decision, evaluatedAt);
    }

    public static bool AssociationAllowsCandidate(TepCandidateProfileMetadata profile, TepAssociationMembershipRegistry? association) =>
        association is not null
        && profile.AssociationMembershipId == association.Id
        && association.AssociationMembershipState is TepAssociationMembershipState.Active
        && association.MemberCompanyState is TepMemberCompanyState.Verified
        && association.PolicyEvaluationState is TepPolicyEvaluationState.Approved
        && association.VisibilityApprovalState is TepVisibilityApprovalState.Approved
        && association.AssociationActivationState is TepAssociationActivationState.Active;

    public static bool PolicyAllowsCandidate(TepConsentVisibilityPolicy? policy) =>
        policy is not null
        && policy.PolicyState is TepPolicyState.Active
        && policy.ConsentRequirementState is TepConsentRequirementState.Approved
        && policy.VisibilityScope is TepVisibilityScope.AssociationVisible
        && policy.DataScopeState is TepDataScopeState.Available
        && policy.AccessPolicyState is TepAccessPolicyState.Approved
        && policy.AssociationConsumptionState is TepAssociationConsumptionState.ActivationApproved
        && policy.PolicyUnavailableBehavior is TepPolicyUnavailableBehavior.FailClosed;

    public static bool VerifiedAccessAllowsCandidate(TepCandidateProfileMetadata profile, TepVerifiedParticipantAccess? verifiedAccess) =>
        verifiedAccess is not null
        && profile.VerifiedParticipantId == verifiedAccess.Id
        && verifiedAccess.VerificationState is TepVerificationState.Verified
        && verifiedAccess.AccessState is TepAccessState.Active
        && verifiedAccess.PolicyEvaluationState is TepPolicyEvaluationState.Approved
        && verifiedAccess.VisibilityApprovalState is TepVisibilityApprovalState.Approved
        && verifiedAccess.VerifiedCompanyAccessState is TepVerifiedCompanyAccessState.Ready;

    public static bool ReviewBoardAllowsCandidate(TepCandidateProfileMetadata profile, TepReviewBoardCaseMetadata? reviewCase) =>
        reviewCase is not null
        && profile.ReviewBoardCaseId == reviewCase.Id
        && reviewCase.ReviewBoardCaseState is TepReviewBoardCaseState.DecisionRecorded
        && reviewCase.ReviewDecisionState is TepReviewDecisionState.Approved
        && reviewCase.ReviewerEligibilityState is TepReviewerEligibilityState.Eligible
        && reviewCase.SegregationOfDutiesState is TepSegregationOfDutiesState.Passed
        && reviewCase.LegalSecurityDecisionState is TepLegalSecurityDecisionState.Approved;

    public static bool TrustLevelAllowsCandidate(TepCandidateProfileMetadata profile, TepTrustLevelPolicyMetadata? trustPolicy) =>
        trustPolicy is not null
        && profile.TrustLevelPolicyId == trustPolicy.Id
        && trustPolicy.TrustLevelPolicyState is TepTrustLevelPolicyState.Active
        && trustPolicy.TrustValidationState is TepTrustValidationState.Approved;

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
