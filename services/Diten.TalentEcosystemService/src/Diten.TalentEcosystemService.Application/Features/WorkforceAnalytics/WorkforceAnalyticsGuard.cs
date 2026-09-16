using System.Text.RegularExpressions;
using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.WorkforceAnalytics;

public static class WorkforceAnalyticsGuard
{
    public const string OwnerKey = "tep.workforce-analytics";
    public const string ReadPermission = "tep.workforce-analytics.read";
    public const string ManagePermission = "tep.workforce-analytics.manage";
    public const string EvaluatePermission = "tep.workforce-analytics.evaluate";
    public const string AuditReadPermission = "tep.workforce-analytics.audit.read";

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
    // "workforce-analytics" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(WorkforceAnalyticsReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.WorkforceAnalyticsReadinessVersion < 1)
        {
            errors.Add("WorkforceAnalyticsReadinessVersion must be greater than zero.");
        }

        ValidateState(request.WorkforceAnalyticsReadinessState, nameof(request.WorkforceAnalyticsReadinessState), errors);
        ValidateState(request.AnalyticsCatalogBoundaryState, nameof(request.AnalyticsCatalogBoundaryState), errors);
        ValidateState(request.MetricBindingIntakeBoundaryState, nameof(request.MetricBindingIntakeBoundaryState), errors);
        ValidateState(request.AggregationScopeBoundaryState, nameof(request.AggregationScopeBoundaryState), errors);
        ValidateState(request.VisibilityControlBoundaryState, nameof(request.VisibilityControlBoundaryState), errors);
        ValidateState(request.AnalyticsReviewBoundaryState, nameof(request.AnalyticsReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.TalentDataSourceDependencyState, nameof(request.TalentDataSourceDependencyState), errors);
        ValidateState(request.ConsentPolicyDependencyState, nameof(request.ConsentPolicyDependencyState), errors);
        ValidateState(request.DataGovernancePolicyDependencyState, nameof(request.DataGovernancePolicyDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.PublicationPolicyState, nameof(request.PublicationPolicyState), errors);

        if (request.WorkforceAnalyticsReadinessState == WorkforceAnalyticsReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.AnalyticsCatalogBoundaryState == WorkforceAnalyticsReadinessState.Ready)
        {
            errors.Add("Analytics catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.MetricBindingIntakeBoundaryState == WorkforceAnalyticsReadinessState.Ready)
        {
            errors.Add("Metric binding intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AggregationScopeBoundaryState == WorkforceAnalyticsReadinessState.Ready)
        {
            errors.Add("Aggregation scope cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.VisibilityControlBoundaryState == WorkforceAnalyticsReadinessState.Ready)
        {
            errors.Add("Visibility control cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AnalyticsReviewBoundaryState == WorkforceAnalyticsReadinessState.Ready)
        {
            errors.Add("Analytics review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == WorkforceAnalyticsReadinessState.Ready)
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
            errors.Add("Workforce analytics readiness metadata cannot contain real metric/measurement values or results, per-employee or per-individual analytics data, aggregated figures or distributions, query results, individual/participant PII or contact details, employee rosters, free-text notes, narrative, attachments, document payloads, ratings, scores, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static WorkforceAnalyticsReadinessState ResolveFailClosedReadinessState(WorkforceAnalyticsReadinessCreateRequest request)
    {
        if (request.WorkforceAnalyticsReadinessState != WorkforceAnalyticsReadinessState.Ready)
        {
            return request.WorkforceAnalyticsReadinessState;
        }

        return ArePreconditionsReady(
            request.AnalyticsCatalogBoundaryState,
            request.MetricBindingIntakeBoundaryState,
            request.AggregationScopeBoundaryState,
            request.VisibilityControlBoundaryState,
            request.AnalyticsReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.TalentDataSourceDependencyState,
            request.ConsentPolicyDependencyState,
            request.DataGovernancePolicyDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.PublicationPolicyState,
            request.DependencyStates)
            ? WorkforceAnalyticsReadinessState.Ready
            : WorkforceAnalyticsReadinessState.Deferred;
    }

    public static void ApplyEvaluation(WorkforceAnalyticsReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.WorkforceAnalyticsReadinessState = ArePreconditionsReady(
            entity.AnalyticsCatalogBoundaryState,
            entity.MetricBindingIntakeBoundaryState,
            entity.AggregationScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.AnalyticsReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.DataGovernancePolicyDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates)
            ? WorkforceAnalyticsReadinessState.Ready
            : WorkforceAnalyticsReadinessState.Deferred;

        entity.DeferredReason = entity.WorkforceAnalyticsReadinessState == WorkforceAnalyticsReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Workforce analytics readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        WorkforceAnalyticsReadinessState analyticsCatalog,
        WorkforceAnalyticsReadinessState metricBindingIntake,
        WorkforceAnalyticsReadinessState aggregationScope,
        WorkforceAnalyticsReadinessState visibilityControl,
        WorkforceAnalyticsReadinessState analyticsReview,
        WorkforceAnalyticsReadinessState automatedDecision,
        WorkforceAnalyticsReadinessState talentDataSourceDependency,
        WorkforceAnalyticsReadinessState consentPolicyDependency,
        WorkforceAnalyticsReadinessState dataGovernancePolicyDependency,
        WorkforceAnalyticsReadinessState notificationDependency,
        WorkforceAnalyticsReadinessState consent,
        WorkforceAnalyticsReadinessState dataMinimization,
        WorkforceAnalyticsReadinessState retention,
        WorkforceAnalyticsReadinessState evidence,
        IReadOnlyDictionary<string, WorkforceAnalyticsReadinessState> dependencyStates)
    {
        var required = new[]
        {
            analyticsCatalog,
            metricBindingIntake,
            aggregationScope,
            visibilityControl,
            analyticsReview,
            automatedDecision,
            talentDataSourceDependency,
            consentPolicyDependency,
            dataGovernancePolicyDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(WorkforceAnalyticsReadinessState state) =>
        state is WorkforceAnalyticsReadinessState.Ready or WorkforceAnalyticsReadinessState.NotRequired;

    private static void ValidateState(WorkforceAnalyticsReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(WorkforceAnalyticsReadinessCreateRequest request)
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
