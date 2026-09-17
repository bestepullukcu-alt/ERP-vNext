using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.TrustLevels;

public static class TrustLevelGuard
{
    private static readonly string[] ForbiddenMarkers =
    [
        "candidateidentity",
        "candidate_identity",
        "talentidentity",
        "talent_identity",
        "referenceexchange",
        "reference_exchange",
        "reputation",
        "risk",
        "analytics",
        "signingworkflow",
        "signing_workflow",
        "signatureexecution",
        "signature_execution",
        "cryptographic",
        "keymanagement",
        "key_management",
        "externalsignatureprovider",
        "external_signature_provider",
        "rawpayload",
        "raw_payload",
        "providerpayload",
        "provider_payload",
        "credential",
        "token",
        "secret",
        "password",
        "piiheavy",
        "pii_heavy"
    ];

    public static Response<Guid> RequireTenant(ITenantContext tenantContext) =>
        tenantContext.TenantId is { } tenantId
            ? Response<Guid>.Success(tenantId)
            : Response<Guid>.Fail("Tenant context is required.", 401);

    public static IReadOnlyList<string> ValidateRequest(TrustLevelPolicyRequest request)
    {
        var errors = new List<string>();

        ValidateRequiredText(request.Code, "Code", errors);
        ValidateRequiredText(request.DisplayName, "DisplayName", errors);
        ValidateRequiredText(request.SourceContractVersion, "SourceContractVersion", errors);
        ValidateOptionalText(request.SignatureSubstrateReference, "SignatureSubstrateReference", errors);
        ValidateOptionalText(request.DeferredReason, "DeferredReason", errors);

        if (request.TrustPolicyVersion <= 0)
        {
            errors.Add("TrustPolicyVersion must be greater than zero.");
        }

        foreach (var dependency in request.DependencyStates)
        {
            ValidateRequiredText(dependency.DependencyKey, "DependencyKey", errors);
            ValidateOptionalText(dependency.Reason, "DependencyReason", errors);
        }

        if (IsActivating(request)
            && !HasTrustElevationPreconditions(
                request.AssociationMembershipRegistryId,
                request.ConsentVisibilityPolicyId,
                request.VerifiedParticipantAccessId,
                request.ReviewBoardCaseId,
                request.AssociationValidationState,
                request.ConsentVisibilityValidationState,
                request.VerifiedAccessValidationState,
                request.ReviewBoardValidationState,
                request.SignatureSubstrateState,
                request.LegalSecurityTrustModelState,
                request.MultiSignatureRequirementState,
                request.MultiSignaturePolicyUnavailableBehavior))
        {
            errors.Add("Trust activation requires approved same-tenant foundation, signature substrate, legal/security, and multi-signature metadata preconditions.");
        }

        return errors;
    }

    public static bool IsActivating(TrustLevelPolicyRequest request) =>
        request.TrustLevelPolicyState is TepTrustLevelPolicyState.Active
        || request.TrustValidationState is TepTrustValidationState.Approved;

    public static bool HasTrustElevationPreconditions(
        Guid? associationMembershipRegistryId,
        Guid? consentVisibilityPolicyId,
        Guid? verifiedParticipantAccessId,
        Guid? reviewBoardCaseId,
        TepShellDependencyStatus associationValidationState,
        TepShellDependencyStatus consentVisibilityValidationState,
        TepShellDependencyStatus verifiedAccessValidationState,
        TepShellDependencyStatus reviewBoardValidationState,
        TepSignatureSubstrateState signatureSubstrateState,
        TepTrustLegalSecurityState legalSecurityTrustModelState,
        TepMultiSignatureRequirementState multiSignatureRequirementState,
        TepMultiSignaturePolicyUnavailableBehavior unavailableBehavior) =>
        associationMembershipRegistryId.HasValue
        && consentVisibilityPolicyId.HasValue
        && verifiedParticipantAccessId.HasValue
        && reviewBoardCaseId.HasValue
        && associationValidationState is TepShellDependencyStatus.Available
        && consentVisibilityValidationState is TepShellDependencyStatus.Available
        && verifiedAccessValidationState is TepShellDependencyStatus.Available
        && reviewBoardValidationState is TepShellDependencyStatus.Available
        && signatureSubstrateState is TepSignatureSubstrateState.Available or TepSignatureSubstrateState.NotRequired
        && legalSecurityTrustModelState is TepTrustLegalSecurityState.Approved or TepTrustLegalSecurityState.LocalMetadata
        && multiSignatureRequirementState is TepMultiSignatureRequirementState.NotRequired or TepMultiSignatureRequirementState.Approved
        && unavailableBehavior is TepMultiSignaturePolicyUnavailableBehavior.FailClosed;

    public static TrustLevelEvaluationDto Evaluate(
        TepTrustLevelPolicyMetadata metadata,
        TepAssociationMembershipRegistry? association,
        TepConsentVisibilityPolicy? policy,
        TepVerifiedParticipantAccess? verifiedAccess,
        TepReviewBoardCaseMetadata? reviewBoard,
        bool trustElevationRequested)
    {
        var evaluatedAt = DateTimeOffset.UtcNow;
        var preconditionsAllowActivation = AssociationAllowsTrust(metadata, association)
            && PolicyAllowsTrust(policy)
            && VerifiedAccessAllowsTrust(metadata, verifiedAccess)
            && ReviewBoardAllowsTrust(metadata, reviewBoard)
            && HasTrustElevationPreconditions(
                metadata.AssociationMembershipRegistryId,
                metadata.ConsentVisibilityPolicyId,
                metadata.VerifiedParticipantAccessId,
                metadata.ReviewBoardCaseId,
                metadata.AssociationValidationState,
                metadata.ConsentVisibilityValidationState,
                metadata.VerifiedAccessValidationState,
                metadata.ReviewBoardValidationState,
                metadata.SignatureSubstrateState,
                metadata.LegalSecurityTrustModelState,
                metadata.MultiSignatureRequirementState,
                metadata.MultiSignaturePolicyUnavailableBehavior);

        var allowed = trustElevationRequested && preconditionsAllowActivation;
        var deferred = !trustElevationRequested && !preconditionsAllowActivation;
        var decision = allowed
            ? "TrustElevationAllowed"
            : deferred
                ? "EvaluationDeferred"
                : "FailClosed";

        return new TrustLevelEvaluationDto(metadata.Id, allowed, deferred, decision, evaluatedAt);
    }

    public static bool AssociationAllowsTrust(TepTrustLevelPolicyMetadata metadata, TepAssociationMembershipRegistry? association) =>
        association is not null
        && metadata.AssociationMembershipRegistryId == association.Id
        && association.AssociationMembershipState is TepAssociationMembershipState.Active
        && association.MemberCompanyState is TepMemberCompanyState.Verified
        && association.PolicyEvaluationState is TepPolicyEvaluationState.Approved
        && association.VisibilityApprovalState is TepVisibilityApprovalState.Approved
        && association.AssociationActivationState is TepAssociationActivationState.Active;

    public static bool PolicyAllowsTrust(TepConsentVisibilityPolicy? policy) =>
        policy is not null
        && policy.PolicyState is TepPolicyState.Active
        && policy.ConsentRequirementState is TepConsentRequirementState.Approved
        && policy.VisibilityScope is TepVisibilityScope.AssociationVisible
        && policy.DataScopeState is TepDataScopeState.Available
        && policy.AccessPolicyState is TepAccessPolicyState.Approved
        && policy.AssociationConsumptionState is TepAssociationConsumptionState.ActivationApproved
        && policy.PolicyUnavailableBehavior is TepPolicyUnavailableBehavior.FailClosed;

    public static bool VerifiedAccessAllowsTrust(TepTrustLevelPolicyMetadata metadata, TepVerifiedParticipantAccess? verifiedAccess) =>
        verifiedAccess is not null
        && metadata.VerifiedParticipantAccessId == verifiedAccess.Id
        && verifiedAccess.VerificationState is TepVerificationState.Verified
        && verifiedAccess.AccessState is TepAccessState.Active
        && verifiedAccess.PolicyEvaluationState is TepPolicyEvaluationState.Approved
        && verifiedAccess.VisibilityApprovalState is TepVisibilityApprovalState.Approved
        && verifiedAccess.AssociationValidationState is TepShellDependencyStatus.Available
        && verifiedAccess.VerifiedCompanyAccessState is TepVerifiedCompanyAccessState.Ready;

    public static bool ReviewBoardAllowsTrust(TepTrustLevelPolicyMetadata metadata, TepReviewBoardCaseMetadata? reviewBoard) =>
        reviewBoard is not null
        && metadata.ReviewBoardCaseId == reviewBoard.Id
        && reviewBoard.ReviewBoardCaseState is TepReviewBoardCaseState.DecisionRecorded
        && reviewBoard.ReviewDecisionState is TepReviewDecisionState.Approved
        && reviewBoard.ReviewerEligibilityState is TepReviewerEligibilityState.Eligible
        && reviewBoard.SegregationOfDutiesState is TepSegregationOfDutiesState.Passed
        && reviewBoard.LegalSecurityDecisionState is TepLegalSecurityDecisionState.Approved
        && reviewBoard.ExternalReviewBoardState is TepExternalReviewBoardState.NotRequired;

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
