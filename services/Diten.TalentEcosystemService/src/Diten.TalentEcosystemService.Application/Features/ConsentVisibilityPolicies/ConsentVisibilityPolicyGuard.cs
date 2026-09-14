using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies;

public static class ConsentVisibilityPolicyGuard
{
    private static readonly string[] ForbiddenMarkers =
    [
        "candidateidentity",
        "candidate_identity",
        "talentidentity",
        "talent_identity",
        "talentprofile",
        "talent_profile",
        "associationregistry",
        "association_registry",
        "membercompanyruntime",
        "membershipruntime",
        "consentcapture",
        "preferencecenter",
        "legaldocument",
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

    public static IReadOnlyList<string> ValidateRequest(ConsentVisibilityPolicyRequest request)
    {
        var errors = new List<string>();

        ValidateRequiredText(request.Code, "Code", errors);
        ValidateRequiredText(request.DisplayName, "DisplayName", errors);
        ValidateRequiredText(request.SourceContractVersion, "SourceContractVersion", errors);

        if (request.PolicyVersion <= 0)
        {
            errors.Add("PolicyVersion must be greater than zero.");
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

    public static Response<NoContent> ValidateActivationRules(ConsentVisibilityPolicyRequest request)
    {
        if (IsActivationRequested(
                request.PolicyState,
                request.AssociationConsumptionState)
            && !HasApprovedPolicyForActivation(
                request.ConsentRequirementState,
                request.VisibilityScope,
                request.DataScopeState,
                request.AccessPolicyState,
                request.PolicyUnavailableBehavior))
        {
            return Response<NoContent>.Fail(
                "Association activation requires consent approval, association visibility, available data scope, approved access policy, and fail-closed unavailable behavior.",
                404);
        }

        return Response<NoContent>.Success(204);
    }

    public static ConsentVisibilityPolicyEvaluationDto Evaluate(TepConsentVisibilityPolicy policy, bool activationRequested)
    {
        var evaluatedAt = DateTimeOffset.UtcNow;
        var activationAllowed = activationRequested
            && IsActivationRequested(policy.PolicyState, policy.AssociationConsumptionState)
            && HasApprovedPolicyForActivation(
                policy.ConsentRequirementState,
                policy.VisibilityScope,
                policy.DataScopeState,
                policy.AccessPolicyState,
                policy.PolicyUnavailableBehavior);

        var evaluationDeferred = !activationRequested
            && policy.PolicyUnavailableBehavior is TepPolicyUnavailableBehavior.DeferredEvaluation;

        var decision = activationAllowed
            ? "ActivationAllowed"
            : evaluationDeferred
                ? "EvaluationDeferred"
                : "FailClosed";

        return new ConsentVisibilityPolicyEvaluationDto(policy.Id, activationAllowed, evaluationDeferred, decision, evaluatedAt);
    }

    private static bool IsActivationRequested(TepPolicyState policyState, TepAssociationConsumptionState associationState) =>
        policyState is TepPolicyState.Active || associationState is TepAssociationConsumptionState.ActivationApproved;

    private static bool HasApprovedPolicyForActivation(
        TepConsentRequirementState consent,
        TepVisibilityScope visibility,
        TepDataScopeState dataScope,
        TepAccessPolicyState accessPolicy,
        TepPolicyUnavailableBehavior unavailableBehavior) =>
        consent is TepConsentRequirementState.Approved
        && visibility is TepVisibilityScope.AssociationVisible
        && dataScope is TepDataScopeState.Available
        && accessPolicy is TepAccessPolicyState.Approved
        && unavailableBehavior is TepPolicyUnavailableBehavior.FailClosed;

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
