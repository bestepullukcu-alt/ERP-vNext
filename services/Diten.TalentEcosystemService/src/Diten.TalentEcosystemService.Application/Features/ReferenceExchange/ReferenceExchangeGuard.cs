using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.ReferenceExchange;

public static class ReferenceExchangeGuard
{
    private static readonly string[] RequiredDependencyKeys =
    [
        "tep.association-memberships",
        "tep.consent-visibility-policies",
        "tep.verified-participants",
        "tep.review-board",
        "tep.trust-levels",
        "tep.candidate-profiles",
        "tep.exit-reference-records"
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
        "marketplacetransaction",
        "marketplace_transaction",
        "referencepayload",
        "reference_payload",
        "recommendation",
        "rehire",
        "disputeworkflow",
        "dispute_workflow",
        "notificationdelivery",
        "documentrepository"
    ];

    public static Response<Guid> RequireTenant(ITenantContext tenantContext) =>
        tenantContext.TenantId is { } tenantId
            ? Response<Guid>.Success(tenantId)
            : Response<Guid>.Fail("Tenant context is required.", 401);

    public static IReadOnlyList<string> ValidateRequest(ReferenceExchangeReadinessRequest request)
    {
        var errors = new List<string>();

        ValidateRequiredText(request.Code, "Code", errors);
        ValidateRequiredText(request.DisplayName, "DisplayName", errors);
        ValidateRequiredText(request.SourceContractVersion, "SourceContractVersion", errors);

        if (request.ReferenceExchangeVersion <= 0)
        {
            errors.Add("ReferenceExchangeVersion must be greater than zero.");
        }

        ValidateOptionalText(request.DeferredReason, "DeferredReason", errors);

        foreach (var dependency in request.DependencyStates)
        {
            ValidateRequiredText(dependency.DependencyKey, "DependencyKey", errors);
            ValidateOptionalText(dependency.Reason, "DependencyReason", errors);
        }

        return errors;
    }

    public static Response<NoContent> ValidateReadinessRequest(ReferenceExchangeReadinessRequest request)
    {
        if (IsReadinessRequested(request.ExchangeReadinessState)
            && !HasApprovedReadinessPreconditions(
                request.AssociationMembershipReference,
                request.VerifiedParticipantReference,
                request.ConsentVisibilityPolicyReference,
                request.CandidateProfileReference,
                request.ExitReferenceRecordReference,
                request.ReviewBoardCaseReference,
                request.TrustLevelPolicyReference,
                request.ExchangeAvailabilityState,
                request.ParticipantEligibilityState,
                request.ConsentPreconditionState,
                request.VisibilityApprovalState,
                request.DataScopeState,
                request.MinimizationState,
                request.LegalPrivacyBasisState,
                request.EvidenceRetentionState,
                request.AuditReadinessState,
                request.AbuseControlState,
                request.ThrottlingPolicyState,
                request.ReviewDisputeBoundaryState,
                request.NotificationDependencyState,
                request.DocumentDependencyState,
                DependencyStatesAllowReadiness(request.DependencyStates)))
        {
            return Response<NoContent>.Fail(
                "Reference exchange readiness requires dependency preconditions, approved consent and visibility, minimized data scope, local metadata audit/retention boundaries, and deferred external dependencies.",
                404);
        }

        return Response<NoContent>.Success(204);
    }

    public static bool IsReadinessRequested(TepReferenceExchangeReadinessState readinessState) =>
        readinessState is TepReferenceExchangeReadinessState.Ready;

    public static bool HasApprovedReadinessPreconditions(
        Guid? associationMembershipReference,
        Guid? verifiedParticipantReference,
        Guid? consentVisibilityPolicyReference,
        Guid? candidateProfileReference,
        Guid? exitReferenceRecordReference,
        Guid? reviewBoardCaseReference,
        Guid? trustLevelPolicyReference,
        TepReferenceExchangeAvailabilityState exchangeAvailabilityState,
        TepParticipantEligibilityState participantEligibilityState,
        TepConsentRequirementState consentPreconditionState,
        TepVisibilityApprovalState visibilityApprovalState,
        TepDataScopeState dataScopeState,
        TepDataMinimizationState minimizationState,
        TepLegalPrivacyBasisState legalPrivacyBasisState,
        TepEvidenceRetentionDecisionState evidenceRetentionState,
        TepAuditReadinessState auditReadinessState,
        TepAbuseControlState abuseControlState,
        TepThrottlingPolicyState throttlingPolicyState,
        TepReviewDisputeBoundaryState reviewDisputeBoundaryState,
        TepExternalDependencyBoundaryState notificationDependencyState,
        TepExternalDependencyBoundaryState documentDependencyState,
        bool dependencyStatesAllowReadiness) =>
        associationMembershipReference.HasValue
        && verifiedParticipantReference.HasValue
        && consentVisibilityPolicyReference.HasValue
        && candidateProfileReference.HasValue
        && exitReferenceRecordReference.HasValue
        && reviewBoardCaseReference.HasValue
        && trustLevelPolicyReference.HasValue
        && exchangeAvailabilityState is TepReferenceExchangeAvailabilityState.LocalMetadata
        && participantEligibilityState is TepParticipantEligibilityState.Eligible
        && consentPreconditionState is TepConsentRequirementState.Approved
        && visibilityApprovalState is TepVisibilityApprovalState.Approved
        && dataScopeState is TepDataScopeState.Available
        && minimizationState is TepDataMinimizationState.Approved
        && legalPrivacyBasisState is TepLegalPrivacyBasisState.LocalMetadata or TepLegalPrivacyBasisState.Approved
        && evidenceRetentionState is TepEvidenceRetentionDecisionState.LocalMetadata
        && auditReadinessState is TepAuditReadinessState.LocalMetadata
        && abuseControlState is TepAbuseControlState.LocalMetadata
        && throttlingPolicyState is TepThrottlingPolicyState.LocalMetadata
        && reviewDisputeBoundaryState is TepReviewDisputeBoundaryState.PreconditionSatisfied
        && notificationDependencyState is TepExternalDependencyBoundaryState.Deferred or TepExternalDependencyBoundaryState.OutOfScope
        && documentDependencyState is TepExternalDependencyBoundaryState.Deferred or TepExternalDependencyBoundaryState.OutOfScope
        && dependencyStatesAllowReadiness;

    public static ReferenceExchangeEvaluationDto Evaluate(
        TepReferenceExchangeMarketplaceReadinessMetadata metadata,
        bool readinessRequested)
    {
        var evaluatedAt = DateTimeOffset.UtcNow;
        var dependencyAllowed = HasApprovedReadinessPreconditions(
            metadata.AssociationMembershipReference,
            metadata.VerifiedParticipantReference,
            metadata.ConsentVisibilityPolicyReference,
            metadata.CandidateProfileReference,
            metadata.ExitReferenceRecordReference,
            metadata.ReviewBoardCaseReference,
            metadata.TrustLevelPolicyReference,
            metadata.ExchangeAvailabilityState,
            metadata.ParticipantEligibilityState,
            metadata.ConsentPreconditionState,
            metadata.VisibilityApprovalState,
            metadata.DataScopeState,
            metadata.MinimizationState,
            metadata.LegalPrivacyBasisState,
            metadata.EvidenceRetentionState,
            metadata.AuditReadinessState,
            metadata.AbuseControlState,
            metadata.ThrottlingPolicyState,
            metadata.ReviewDisputeBoundaryState,
            metadata.NotificationDependencyState,
            metadata.DocumentDependencyState,
            DependencyStatesAllowReadiness(metadata.DependencyStates));

        var readinessAllowed = readinessRequested && dependencyAllowed;
        var evaluationDeferred = !readinessRequested && !dependencyAllowed;
        var decision = readinessAllowed
            ? "ReadinessAllowed"
            : evaluationDeferred
                ? "EvaluationDeferred"
                : "FailClosed";

        return new ReferenceExchangeEvaluationDto(metadata.Id, readinessAllowed, evaluationDeferred, decision, evaluatedAt);
    }

    public static bool DependencyStatesAllowReadiness(IEnumerable<ReferenceExchangeDependencyStateDto> dependencyStates) =>
        RequiredDependencyKeys.All(requiredKey => dependencyStates.Any(state =>
            string.Equals(state.DependencyKey, requiredKey, StringComparison.Ordinal)
            && state.State is TepShellDependencyStatus.Available));

    public static bool DependencyStatesAllowReadiness(IEnumerable<TepReferenceExchangeDependencyState> dependencyStates) =>
        RequiredDependencyKeys.All(requiredKey => dependencyStates.Any(state =>
            string.Equals(state.DependencyKey, requiredKey, StringComparison.Ordinal)
            && state.State is TepShellDependencyStatus.Available));

    public static IReadOnlyList<TepReferenceExchangeDependencyState> BuildRequiredDependencyStates(TepShellDependencyStatus state, string? reason) =>
        RequiredDependencyKeys
            .Select(key => new TepReferenceExchangeDependencyState
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
            errors.Add($"{field} contains a forbidden reference exchange marker.");
        }
    }

    private static void ValidateOptionalText(string? value, string field, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(value) && ContainsForbiddenMarker(value))
        {
            errors.Add($"{field} contains a forbidden reference exchange marker.");
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
