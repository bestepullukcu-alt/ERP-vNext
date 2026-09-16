using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedParticipants;

public static class VerifiedParticipantGuard
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
        "dispute",
        "reviewboard",
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

    public static IReadOnlyList<string> ValidateRequest(VerifiedParticipantAccessRequest request)
    {
        var errors = new List<string>();

        ValidateRequiredText(request.Code, "Code", errors);
        ValidateRequiredText(request.DisplayName, "DisplayName", errors);
        ValidateRequiredText(request.MemberCompanyReference, "MemberCompanyReference", errors);
        ValidateRequiredText(request.HrParticipantReference, "HrParticipantReference", errors);
        ValidateRequiredText(request.HcmFoundationReference, "HcmFoundationReference", errors);
        ValidateRequiredText(request.SourceContractVersion, "SourceContractVersion", errors);

        if (request.VerificationVersion <= 0)
        {
            errors.Add("VerificationVersion must be greater than zero.");
        }

        ValidateOptionalText(request.DeferredReason, "DeferredReason", errors);

        foreach (var dependency in request.DependencyStates)
        {
            ValidateRequiredText(dependency.DependencyKey, "DependencyKey", errors);
            ValidateOptionalText(dependency.Reason, "DependencyReason", errors);
        }

        return errors;
    }

    public static Response<NoContent> ValidateVerificationRequest(VerifiedParticipantAccessRequest request)
    {
        if (IsVerificationRequested(request.VerificationState, request.AccessState)
            && !HasApprovedVerifiedAccessPreconditions(
                request.AssociationMembershipId,
                request.ConsentVisibilityPolicyId,
                request.PolicyEvaluationState,
                request.VisibilityApprovalState,
                request.HcmValidationState,
                request.AssociationValidationState,
                request.VerifiedCompanyAccessState))
        {
            return Response<NoContent>.Fail(
                "Verified participant access requires same-tenant Association, approved Consent/Visibility policy, available HCM context, available Association validation, and ready local verified-company access metadata.",
                404);
        }

        return Response<NoContent>.Success(204);
    }

    public static bool IsVerificationRequested(TepVerificationState verificationState, TepAccessState accessState) =>
        verificationState is TepVerificationState.Verified || accessState is TepAccessState.Active;

    public static bool HasApprovedVerifiedAccessPreconditions(
        Guid? associationMembershipId,
        Guid? consentVisibilityPolicyId,
        TepPolicyEvaluationState policyEvaluation,
        TepVisibilityApprovalState visibility,
        TepShellDependencyStatus hcmValidation,
        TepShellDependencyStatus associationValidation,
        TepVerifiedCompanyAccessState verifiedCompanyAccess) =>
        associationMembershipId.HasValue
        && consentVisibilityPolicyId.HasValue
        && policyEvaluation is TepPolicyEvaluationState.Approved
        && visibility is TepVisibilityApprovalState.Approved
        && hcmValidation is TepShellDependencyStatus.Available
        && associationValidation is TepShellDependencyStatus.Available
        && verifiedCompanyAccess is TepVerifiedCompanyAccessState.Ready;

    public static VerifiedParticipantEvaluationDto Evaluate(
        TepVerifiedParticipantAccess access,
        TepAssociationMembershipRegistry? association,
        TepConsentVisibilityPolicy? policy,
        bool verificationRequested)
    {
        var evaluatedAt = DateTimeOffset.UtcNow;
        var associationAllowsAccess = AssociationAllowsAccess(access, association);
        var policyAllowsAccess = PolicyAllowsAccess(policy);
        var localPreconditionsAllowAccess = HasApprovedVerifiedAccessPreconditions(
            access.AssociationMembershipId,
            access.ConsentVisibilityPolicyId,
            access.PolicyEvaluationState,
            access.VisibilityApprovalState,
            access.HcmValidationState,
            access.AssociationValidationState,
            access.VerifiedCompanyAccessState);

        var verificationAllowed = verificationRequested
            && associationAllowsAccess
            && policyAllowsAccess
            && localPreconditionsAllowAccess;
        var evaluationDeferred = !verificationRequested && (!associationAllowsAccess || !policyAllowsAccess || !localPreconditionsAllowAccess);
        var decision = verificationAllowed
            ? "VerificationAllowed"
            : evaluationDeferred
                ? "EvaluationDeferred"
                : "FailClosed";

        return new VerifiedParticipantEvaluationDto(access.Id, verificationAllowed, evaluationDeferred, decision, evaluatedAt);
    }

    public static bool AssociationAllowsAccess(TepVerifiedParticipantAccess access, TepAssociationMembershipRegistry? association) =>
        association is not null
        && access.AssociationMembershipId == association.Id
        && association.AssociationMembershipState is TepAssociationMembershipState.Active
        && association.MemberCompanyState is TepMemberCompanyState.Verified
        && association.PolicyEvaluationState is TepPolicyEvaluationState.Approved
        && association.VisibilityApprovalState is TepVisibilityApprovalState.Approved
        && association.AssociationActivationState is TepAssociationActivationState.Active;

    public static bool PolicyAllowsAccess(TepConsentVisibilityPolicy? policy) =>
        policy is not null
        && policy.PolicyState is TepPolicyState.Active
        && policy.ConsentRequirementState is TepConsentRequirementState.Approved
        && policy.VisibilityScope is TepVisibilityScope.AssociationVisible
        && policy.DataScopeState is TepDataScopeState.Available
        && policy.AccessPolicyState is TepAccessPolicyState.Approved
        && policy.AssociationConsumptionState is TepAssociationConsumptionState.ActivationApproved
        && policy.PolicyUnavailableBehavior is TepPolicyUnavailableBehavior.FailClosed;

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
