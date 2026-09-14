using System.Text.RegularExpressions;
using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.MentorshipRecommendationNetwork;

public static class MentorshipRecommendationNetworkGuard
{
    public const string OwnerKey = "tep.mentorship-recommendation-network";
    public const string ReadPermission = "tep.mentorship-recommendation-network.read";
    public const string ManagePermission = "tep.mentorship-recommendation-network.manage";
    public const string EvaluatePermission = "tep.mentorship-recommendation-network.evaluate";
    public const string AuditReadPermission = "tep.mentorship-recommendation-network.audit.read";

    private static readonly string[] ForbiddenMarkers =
    [
        "workflowbody",
        "workflow_body",
        "reviewbody",
        "review_body",
        "appraisalbody",
        "appraisal_body",
        "appraisalnarrative",
        "appraisal_narrative",
        "approvalbody",
        "approval_body",
        "approvaldecision",
        "approval_decision",
        "reviewnote",
        "review_note",
        "reviewnotes",
        "review_notes",
        "score",
        "skillScoring",
        "rating",
        "rank",
        "ranking",
        "calibration",
        "modeloutput",
        "model_output",
        "automateddecision",
        "automated_decision",
        "positionmutation",
        "position_mutation",
        "positionassignmentpayload",
        "position_assignment_payload",
        "actionpayload",
        "action_payload",
        "managernote",
        "manager_note",
        "hrnote",
        "hr_note",
        "employeestatement",
        "employee_statement",
        "freetext",
        "free_text",
        "narrative",
        "attachment",
        "documentpayload",
        "document_payload",
        "rawpayload",
        "raw_payload",
        "providerpayload",
        "provider_payload",
        "credential",
        "access_token",
        "refresh_token",
        "secret",
        "password",
        "compensationamount",
        "compensation_amount",
        "salaryamount",
        "salary_amount",
        "salary",
        "wage",
        "payroll",
        "bank",
        "tax",
        "payslip",
        "benefitselection",
        "benefits_election",
        "nationalid",
        "national_id",
        "dateofbirth",
        "dob",
        "homeaddress",
        "home_address",
        "biometric",
        "geolocation"
    ];

    // Word/token-boundary matcher: markers only match as standalone tokens, so legitimate
    // words such as "taxonomy" (contains "tax"), "scorecard" (contains "score") or
    // "mentorship-recommendation-network" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
    // "password", "national_id", ...) still match. Tested against the RAW value, not a
    // punctuation-stripped form. Underscore is a regex word character, so snake_case
    // markers ("workflow_body") match as whole tokens.
    private static readonly Regex ForbiddenMarkerRegex = new(
        @"\b(" + string.Join("|", ForbiddenMarkers.Select(Regex.Escape)) + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static Response<Guid> RequireTenant(ITenantContext tenantContext) =>
        tenantContext.TenantId is { } tenantId && tenantId != Guid.Empty
            ? Response<Guid>.Success(tenantId)
            : Response<Guid>.Fail("Tenant context is required.", 401);

    public static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    public static IReadOnlyList<string> Validate(MentorshipRecommendationNetworkReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.MentorshipRecommendationNetworkReadinessVersion < 1)
        {
            errors.Add("MentorshipRecommendationNetworkReadinessVersion must be greater than zero.");
        }

        ValidateState(request.MentorshipRecommendationNetworkReadinessState, nameof(request.MentorshipRecommendationNetworkReadinessState), errors);
        ValidateState(request.NetworkCatalogBoundaryState, nameof(request.NetworkCatalogBoundaryState), errors);
        ValidateState(request.PairingIntakeBoundaryState, nameof(request.PairingIntakeBoundaryState), errors);
        ValidateState(request.RecommendationScopeBoundaryState, nameof(request.RecommendationScopeBoundaryState), errors);
        ValidateState(request.VisibilityControlBoundaryState, nameof(request.VisibilityControlBoundaryState), errors);
        ValidateState(request.NetworkReviewBoundaryState, nameof(request.NetworkReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.TalentDataSourceDependencyState, nameof(request.TalentDataSourceDependencyState), errors);
        ValidateState(request.ConsentPolicyDependencyState, nameof(request.ConsentPolicyDependencyState), errors);
        ValidateState(request.ReputationSourceDependencyState, nameof(request.ReputationSourceDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.PublicationPolicyState, nameof(request.PublicationPolicyState), errors);

        if (request.MentorshipRecommendationNetworkReadinessState == MentorshipRecommendationNetworkReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.NetworkCatalogBoundaryState == MentorshipRecommendationNetworkReadinessState.Ready)
        {
            errors.Add("Network catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.PairingIntakeBoundaryState == MentorshipRecommendationNetworkReadinessState.Ready)
        {
            errors.Add("Pairing intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.RecommendationScopeBoundaryState == MentorshipRecommendationNetworkReadinessState.Ready)
        {
            errors.Add("Recommendation scope cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.VisibilityControlBoundaryState == MentorshipRecommendationNetworkReadinessState.Ready)
        {
            errors.Add("Visibility control cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.NetworkReviewBoundaryState == MentorshipRecommendationNetworkReadinessState.Ready)
        {
            errors.Add("Network review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == MentorshipRecommendationNetworkReadinessState.Ready)
        {
            errors.Add("Automated decision behavior cannot be marked Ready in the first metadata-only slice.");
        }

        foreach (var pair in request.DependencyStates)
        {
            RequireText(pair.Key, nameof(request.DependencyStates), 80, errors);
            ValidateState(pair.Value, $"{nameof(request.DependencyStates)}.{pair.Key}", errors);
        }

        if (ForbiddenValues(request).Any(ContainsForbiddenMarker))
        {
            errors.Add("Mentorship and recommendation network readiness metadata cannot contain mentor/mentee pairings or member rosters, participant/individual PII or contact details, recommendation or message/conversation content, free-text recommendation or endorsement narrative, resume/CV or profile content, proficiency or ranking scores, free-text notes, narrative, attachments, document payloads, ratings, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static MentorshipRecommendationNetworkReadinessState ResolveFailClosedReadinessState(MentorshipRecommendationNetworkReadinessCreateRequest request)
    {
        if (request.MentorshipRecommendationNetworkReadinessState != MentorshipRecommendationNetworkReadinessState.Ready)
        {
            return request.MentorshipRecommendationNetworkReadinessState;
        }

        return ArePreconditionsReady(
            request.NetworkCatalogBoundaryState,
            request.PairingIntakeBoundaryState,
            request.RecommendationScopeBoundaryState,
            request.VisibilityControlBoundaryState,
            request.NetworkReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.TalentDataSourceDependencyState,
            request.ConsentPolicyDependencyState,
            request.ReputationSourceDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.PublicationPolicyState,
            request.DependencyStates)
            ? MentorshipRecommendationNetworkReadinessState.Ready
            : MentorshipRecommendationNetworkReadinessState.Deferred;
    }

    public static void ApplyEvaluation(MentorshipRecommendationNetworkReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.MentorshipRecommendationNetworkReadinessState = ArePreconditionsReady(
            entity.NetworkCatalogBoundaryState,
            entity.PairingIntakeBoundaryState,
            entity.RecommendationScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.NetworkReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.ReputationSourceDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates)
            ? MentorshipRecommendationNetworkReadinessState.Ready
            : MentorshipRecommendationNetworkReadinessState.Deferred;

        entity.DeferredReason = entity.MentorshipRecommendationNetworkReadinessState == MentorshipRecommendationNetworkReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Mentorship and recommendation network readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        MentorshipRecommendationNetworkReadinessState networkCatalog,
        MentorshipRecommendationNetworkReadinessState pairingIntake,
        MentorshipRecommendationNetworkReadinessState recommendationScope,
        MentorshipRecommendationNetworkReadinessState visibilityControl,
        MentorshipRecommendationNetworkReadinessState networkReview,
        MentorshipRecommendationNetworkReadinessState automatedDecision,
        MentorshipRecommendationNetworkReadinessState talentDataSourceDependency,
        MentorshipRecommendationNetworkReadinessState consentPolicyDependency,
        MentorshipRecommendationNetworkReadinessState reputationSourceDependency,
        MentorshipRecommendationNetworkReadinessState notificationDependency,
        MentorshipRecommendationNetworkReadinessState consent,
        MentorshipRecommendationNetworkReadinessState dataMinimization,
        MentorshipRecommendationNetworkReadinessState retention,
        MentorshipRecommendationNetworkReadinessState evidence,
        IReadOnlyDictionary<string, MentorshipRecommendationNetworkReadinessState> dependencyStates)
    {
        var required = new[]
        {
            networkCatalog,
            pairingIntake,
            recommendationScope,
            visibilityControl,
            networkReview,
            automatedDecision,
            talentDataSourceDependency,
            consentPolicyDependency,
            reputationSourceDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(MentorshipRecommendationNetworkReadinessState state) =>
        state is MentorshipRecommendationNetworkReadinessState.Ready or MentorshipRecommendationNetworkReadinessState.NotRequired;

    private static void ValidateState(MentorshipRecommendationNetworkReadinessState state, string fieldName, List<string> errors)
    {
        if (!Enum.IsDefined(state))
        {
            errors.Add($"{fieldName} is not supported.");
        }
    }

    private static void RequireText(string value, string fieldName, int maxLength, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
            return;
        }

        if (value.Trim().Length > maxLength)
        {
            errors.Add($"{fieldName} must be {maxLength} characters or fewer.");
        }
    }

    private static IEnumerable<string> ForbiddenValues(MentorshipRecommendationNetworkReadinessCreateRequest request)
    {
        yield return request.Code;
        yield return request.DisplayName;
        yield return request.SourceContractVersion;
        yield return request.DeferredReason ?? string.Empty;

        foreach (var key in request.DependencyStates.Keys)
        {
            yield return key;
        }
    }

    private static bool ContainsForbiddenMarker(string value) =>
        !string.IsNullOrEmpty(value) && ForbiddenMarkerRegex.IsMatch(value);
}
