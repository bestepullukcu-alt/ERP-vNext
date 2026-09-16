using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords;

public static class ExitReferenceRecordGuard
{
    private static readonly string[] RequiredDependencyKeys =
    [
        "tep.association-memberships",
        "tep.consent-visibility-policies",
        "tep.verified-participants",
        "tep.review-board",
        "tep.trust-levels",
        "tep.candidate-profiles"
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
        "referenceexchange",
        "reference_exchange",
        "marketplace",
        "recommendation",
        "rehire",
        "disputeworkflow",
        "dispute_workflow"
    ];

    public static Response<Guid> RequireTenant(ITenantContext tenantContext) =>
        tenantContext.TenantId is { } tenantId
            ? Response<Guid>.Success(tenantId)
            : Response<Guid>.Fail("Tenant context is required.", 401);

    public static IReadOnlyList<string> ValidateRequest(ExitReferenceRecordRequest request)
    {
        var errors = new List<string>();

        ValidateRequiredText(request.Code, "Code", errors);
        ValidateRequiredText(request.DisplayName, "DisplayName", errors);
        ValidateRequiredText(request.OffboardingCaseReference, "OffboardingCaseReference", errors);
        ValidateRequiredText(request.SourceContractVersion, "SourceContractVersion", errors);

        if (request.ReferenceRecordVersion <= 0)
        {
            errors.Add("ReferenceRecordVersion must be greater than zero.");
        }

        ValidateOptionalText(request.DeferredReason, "DeferredReason", errors);

        foreach (var dependency in request.DependencyStates)
        {
            ValidateRequiredText(dependency.DependencyKey, "DependencyKey", errors);
            ValidateOptionalText(dependency.Reason, "DependencyReason", errors);
        }

        return errors;
    }

    public static Response<NoContent> ValidateActivationRequest(ExitReferenceRecordRequest request)
    {
        if (IsActivationRequested(request.ReferenceRecordState)
            && !HasApprovedReferencePreconditions(
                request.CandidateProfileReference,
                request.VerifiedParticipantReference,
                request.AssociationMembershipReference,
                request.ConsentVisibilityPolicyReference,
                request.ReviewBoardCaseReference,
                request.TrustLevelPolicyReference,
                request.ReferenceSharingState,
                request.ConsentPreconditionState,
                request.VisibilityApprovalState,
                request.DataScopeState,
                request.EvidenceRetentionState,
                request.ReviewDisputeBoundaryState,
                DependencyStatesAllowActivation(request.DependencyStates)))
        {
            return Response<NoContent>.Fail(
                "Exit reference record activation requires same-tenant dependency preconditions, approved consent, approved visibility, available data scope, and local metadata retention boundaries.",
                404);
        }

        return Response<NoContent>.Success(204);
    }

    public static bool IsActivationRequested(TepExitReferenceRecordState referenceRecordState) =>
        referenceRecordState is TepExitReferenceRecordState.Active;

    public static bool HasApprovedReferencePreconditions(
        Guid? candidateProfileReference,
        Guid? verifiedParticipantReference,
        Guid? associationMembershipReference,
        Guid? consentVisibilityPolicyReference,
        Guid? reviewBoardCaseReference,
        Guid? trustLevelPolicyReference,
        TepReferenceSharingState referenceSharingState,
        TepConsentRequirementState consentPreconditionState,
        TepVisibilityApprovalState visibilityApprovalState,
        TepDataScopeState dataScopeState,
        TepEvidenceRetentionDecisionState evidenceRetentionState,
        TepReviewDisputeBoundaryState reviewDisputeBoundaryState,
        bool dependencyStatesAllowActivation) =>
        candidateProfileReference.HasValue
        && verifiedParticipantReference.HasValue
        && associationMembershipReference.HasValue
        && consentVisibilityPolicyReference.HasValue
        && reviewBoardCaseReference.HasValue
        && trustLevelPolicyReference.HasValue
        && referenceSharingState is TepReferenceSharingState.LocalMetadata
        && consentPreconditionState is TepConsentRequirementState.Approved
        && visibilityApprovalState is TepVisibilityApprovalState.Approved
        && dataScopeState is TepDataScopeState.Available
        && evidenceRetentionState is TepEvidenceRetentionDecisionState.LocalMetadata
        && reviewDisputeBoundaryState is TepReviewDisputeBoundaryState.PreconditionSatisfied
        && dependencyStatesAllowActivation;

    public static ExitReferenceEvaluationDto Evaluate(
        TepExitReferenceRecordMetadata referenceRecord,
        TepAssociationMembershipRegistry? association,
        TepConsentVisibilityPolicy? policy,
        TepVerifiedParticipantAccess? verifiedAccess,
        TepReviewBoardCaseMetadata? reviewCase,
        TepTrustLevelPolicyMetadata? trustPolicy,
        TepCandidateProfileMetadata? candidateProfile,
        bool activationRequested)
    {
        var evaluatedAt = DateTimeOffset.UtcNow;
        var dependencyAllowed = AssociationAllowsReference(referenceRecord, association)
            && PolicyAllowsReference(policy)
            && VerifiedAccessAllowsReference(referenceRecord, verifiedAccess)
            && ReviewBoardAllowsReference(referenceRecord, reviewCase)
            && TrustLevelAllowsReference(referenceRecord, trustPolicy)
            && CandidateProfileAllowsReference(referenceRecord, candidateProfile)
            && HasApprovedReferencePreconditions(
                referenceRecord.CandidateProfileReference,
                referenceRecord.VerifiedParticipantReference,
                referenceRecord.AssociationMembershipReference,
                referenceRecord.ConsentVisibilityPolicyReference,
                referenceRecord.ReviewBoardCaseReference,
                referenceRecord.TrustLevelPolicyReference,
                referenceRecord.ReferenceSharingState,
                referenceRecord.ConsentPreconditionState,
                referenceRecord.VisibilityApprovalState,
                referenceRecord.DataScopeState,
                referenceRecord.EvidenceRetentionState,
                referenceRecord.ReviewDisputeBoundaryState,
                DependencyStatesAllowActivation(referenceRecord.DependencyStates));

        var activationAllowed = activationRequested && dependencyAllowed;
        var evaluationDeferred = !activationRequested && !dependencyAllowed;
        var decision = activationAllowed
            ? "ActivationAllowed"
            : evaluationDeferred
                ? "EvaluationDeferred"
                : "FailClosed";

        return new ExitReferenceEvaluationDto(referenceRecord.Id, activationAllowed, evaluationDeferred, decision, evaluatedAt);
    }

    public static bool AssociationAllowsReference(TepExitReferenceRecordMetadata referenceRecord, TepAssociationMembershipRegistry? association) =>
        association is not null
        && referenceRecord.AssociationMembershipReference == association.Id
        && association.AssociationMembershipState is TepAssociationMembershipState.Active
        && association.MemberCompanyState is TepMemberCompanyState.Verified
        && association.PolicyEvaluationState is TepPolicyEvaluationState.Approved
        && association.VisibilityApprovalState is TepVisibilityApprovalState.Approved
        && association.AssociationActivationState is TepAssociationActivationState.Active;

    public static bool PolicyAllowsReference(TepConsentVisibilityPolicy? policy) =>
        policy is not null
        && policy.PolicyState is TepPolicyState.Active
        && policy.ConsentRequirementState is TepConsentRequirementState.Approved
        && policy.VisibilityScope is TepVisibilityScope.AssociationVisible
        && policy.DataScopeState is TepDataScopeState.Available
        && policy.AccessPolicyState is TepAccessPolicyState.Approved
        && policy.AssociationConsumptionState is TepAssociationConsumptionState.ActivationApproved
        && policy.PolicyUnavailableBehavior is TepPolicyUnavailableBehavior.FailClosed;

    public static bool VerifiedAccessAllowsReference(TepExitReferenceRecordMetadata referenceRecord, TepVerifiedParticipantAccess? verifiedAccess) =>
        verifiedAccess is not null
        && referenceRecord.VerifiedParticipantReference == verifiedAccess.Id
        && verifiedAccess.VerificationState is TepVerificationState.Verified
        && verifiedAccess.AccessState is TepAccessState.Active
        && verifiedAccess.PolicyEvaluationState is TepPolicyEvaluationState.Approved
        && verifiedAccess.VisibilityApprovalState is TepVisibilityApprovalState.Approved
        && verifiedAccess.VerifiedCompanyAccessState is TepVerifiedCompanyAccessState.Ready;

    public static bool ReviewBoardAllowsReference(TepExitReferenceRecordMetadata referenceRecord, TepReviewBoardCaseMetadata? reviewCase) =>
        reviewCase is not null
        && referenceRecord.ReviewBoardCaseReference == reviewCase.Id
        && reviewCase.ReviewBoardCaseState is TepReviewBoardCaseState.DecisionRecorded
        && reviewCase.ReviewDecisionState is TepReviewDecisionState.Approved
        && reviewCase.ReviewerEligibilityState is TepReviewerEligibilityState.Eligible
        && reviewCase.SegregationOfDutiesState is TepSegregationOfDutiesState.Passed
        && reviewCase.LegalSecurityDecisionState is TepLegalSecurityDecisionState.Approved;

    public static bool TrustLevelAllowsReference(TepExitReferenceRecordMetadata referenceRecord, TepTrustLevelPolicyMetadata? trustPolicy) =>
        trustPolicy is not null
        && referenceRecord.TrustLevelPolicyReference == trustPolicy.Id
        && trustPolicy.TrustLevelPolicyState is TepTrustLevelPolicyState.Active
        && trustPolicy.TrustValidationState is TepTrustValidationState.Approved
        && trustPolicy.MultiSignaturePolicyUnavailableBehavior is TepMultiSignaturePolicyUnavailableBehavior.FailClosed;

    public static bool CandidateProfileAllowsReference(TepExitReferenceRecordMetadata referenceRecord, TepCandidateProfileMetadata? candidateProfile) =>
        candidateProfile is not null
        && referenceRecord.CandidateProfileReference == candidateProfile.Id
        && candidateProfile.CandidateIdentityState is TepCandidateIdentityState.Active
        && candidateProfile.TalentProfileState is TepTalentProfileState.Published
        && candidateProfile.ProfileCompletenessState is TepProfileCompletenessState.Complete
        && candidateProfile.VisibilityClassification is TepCandidateVisibilityClassification.AssociationVisible
        && candidateProfile.ConsentBasisState is TepCandidateConsentBasisState.Approved
        && candidateProfile.PolicyEvaluationState is TepPolicyEvaluationState.Approved
        && candidateProfile.ProfilePolicyEvaluationState is TepPolicyEvaluationState.Approved
        && candidateProfile.VisibilityApprovalState is TepVisibilityApprovalState.Approved
        && candidateProfile.DataScopeState is TepDataScopeState.Available
        && candidateProfile.DataMinimizationState is TepDataMinimizationState.Approved;

    public static bool DependencyStatesAllowActivation(IEnumerable<ExitReferenceDependencyStateDto> dependencyStates) =>
        RequiredDependencyKeys.All(requiredKey => dependencyStates.Any(state =>
            string.Equals(state.DependencyKey, requiredKey, StringComparison.Ordinal)
            && state.State is TepShellDependencyStatus.Available));

    public static bool DependencyStatesAllowActivation(IEnumerable<TepExitReferenceDependencyState> dependencyStates) =>
        RequiredDependencyKeys.All(requiredKey => dependencyStates.Any(state =>
            string.Equals(state.DependencyKey, requiredKey, StringComparison.Ordinal)
            && state.State is TepShellDependencyStatus.Available));

    public static IReadOnlyList<TepExitReferenceDependencyState> BuildRequiredDependencyStates(TepShellDependencyStatus state, string? reason) =>
        RequiredDependencyKeys
            .Select(key => new TepExitReferenceDependencyState
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
            errors.Add($"{field} contains a forbidden exit reference marker.");
        }
    }

    private static void ValidateOptionalText(string? value, string field, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(value) && ContainsForbiddenMarker(value))
        {
            errors.Add($"{field} contains a forbidden exit reference marker.");
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
