using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.AssociationMemberships;

public static class AssociationMembershipGuard
{
    private static readonly string[] ForbiddenMarkers =
    [
        "candidateidentity",
        "candidate_identity",
        "talentidentity",
        "talent_identity",
        "talentprofile",
        "talent_profile",
        "consentengine",
        "consent_engine",
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

    public static IReadOnlyList<string> ValidateRequest(AssociationMembershipRegistryRequest request)
    {
        var errors = new List<string>();

        ValidateRequiredText(request.Code, "Code", errors);
        ValidateRequiredText(request.DisplayName, "DisplayName", errors);
        ValidateRequiredText(request.MemberCompanyReference, "MemberCompanyReference", errors);
        ValidateRequiredText(request.HcmFoundationReference, "HcmFoundationReference", errors);
        ValidateRequiredText(request.SourceContractVersion, "SourceContractVersion", errors);

        if (request.RegistryVersion <= 0)
        {
            errors.Add("RegistryVersion must be greater than zero.");
        }

        foreach (var dependency in request.DependencyStates)
        {
            ValidateRequiredText(dependency.DependencyKey, "DependencyKey", errors);

            if (!string.IsNullOrWhiteSpace(dependency.Reason) && ContainsForbiddenMarker(dependency.Reason))
            {
                errors.Add("Dependency reason contains a forbidden TEP runtime marker.");
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateMemberCompanyRequest(AssociationMemberCompanyRequest request)
    {
        var errors = new List<string>();
        ValidateRequiredText(request.MemberCompanyReference, "MemberCompanyReference", errors);
        ValidateRequiredText(request.SourceContractVersion, "SourceContractVersion", errors);
        return errors;
    }

    public static Response<NoContent> ValidateActivationRequest(AssociationMembershipRegistryRequest request)
    {
        if (IsActivationRequested(request.AssociationMembershipState, request.AssociationActivationState)
            && !HasApprovedAssociationPreconditions(
                request.ConsentVisibilityPolicyId,
                request.PolicyEvaluationState,
                request.VisibilityApprovalState,
                request.MemberCompanyState))
        {
            return Response<NoContent>.Fail(
                "Association activation requires a same-tenant consent/visibility policy, approved policy evaluation, approved visibility, and verified member company metadata.",
                404);
        }

        return Response<NoContent>.Success(204);
    }

    public static bool IsActivationRequested(TepAssociationMembershipState membershipState, TepAssociationActivationState activationState) =>
        membershipState is TepAssociationMembershipState.Active || activationState is TepAssociationActivationState.Active;

    public static bool HasApprovedAssociationPreconditions(
        Guid? policyId,
        TepPolicyEvaluationState policyEvaluation,
        TepVisibilityApprovalState visibility,
        TepMemberCompanyState memberCompany) =>
        policyId.HasValue
        && policyEvaluation is TepPolicyEvaluationState.Approved
        && visibility is TepVisibilityApprovalState.Approved
        && memberCompany is TepMemberCompanyState.Verified;

    public static AssociationMembershipEvaluationDto Evaluate(
        TepAssociationMembershipRegistry registry,
        TepConsentVisibilityPolicy? policy,
        bool activationRequested)
    {
        var evaluatedAt = DateTimeOffset.UtcNow;
        var policyAllowsActivation = policy is not null
            && policy.PolicyState is TepPolicyState.Active
            && policy.ConsentRequirementState is TepConsentRequirementState.Approved
            && policy.VisibilityScope is TepVisibilityScope.AssociationVisible
            && policy.DataScopeState is TepDataScopeState.Available
            && policy.AccessPolicyState is TepAccessPolicyState.Approved
            && policy.PolicyUnavailableBehavior is TepPolicyUnavailableBehavior.FailClosed;

        var activationAllowed = activationRequested
            && policyAllowsActivation
            && HasApprovedAssociationPreconditions(
                registry.ConsentVisibilityPolicyId,
                registry.PolicyEvaluationState,
                registry.VisibilityApprovalState,
                registry.MemberCompanyState);

        var evaluationDeferred = !activationRequested && !policyAllowsActivation;
        var decision = activationAllowed
            ? "ActivationAllowed"
            : evaluationDeferred
                ? "EvaluationDeferred"
                : "FailClosed";

        return new AssociationMembershipEvaluationDto(registry.Id, activationAllowed, evaluationDeferred, decision, evaluatedAt);
    }

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

    private static bool ContainsForbiddenMarker(string value)
    {
        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return ForbiddenMarkers.Any(marker => normalized.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
