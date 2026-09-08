using System.Text.RegularExpressions;
using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards;

public static class ScorecardsDashboardsGuard
{
    public const string OwnerKey = "dki.scorecards-dashboards";
    public const string ReadPermission = "dki.scorecards-dashboards.read";
    public const string ManagePermission = "dki.scorecards-dashboards.manage";
    public const string EvaluatePermission = "dki.scorecards-dashboards.evaluate";
    public const string AuditReadPermission = "dki.scorecards-dashboards.audit.read";

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
    // "scorecards-dashboards" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(ScorecardsDashboardsReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.ScorecardsDashboardsReadinessVersion < 1)
        {
            errors.Add("ScorecardsDashboardsReadinessVersion must be greater than zero.");
        }

        ValidateState(request.ScorecardsDashboardsReadinessState, nameof(request.ScorecardsDashboardsReadinessState), errors);
        ValidateState(request.ScorecardCatalogBoundaryState, nameof(request.ScorecardCatalogBoundaryState), errors);
        ValidateState(request.WidgetBindingIntakeBoundaryState, nameof(request.WidgetBindingIntakeBoundaryState), errors);
        ValidateState(request.LayoutScopeBoundaryState, nameof(request.LayoutScopeBoundaryState), errors);
        ValidateState(request.PublicationControlBoundaryState, nameof(request.PublicationControlBoundaryState), errors);
        ValidateState(request.DashboardReviewBoundaryState, nameof(request.DashboardReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.MetricSemanticRegistrySourceDependencyState, nameof(request.MetricSemanticRegistrySourceDependencyState), errors);
        ValidateState(request.DataWarehouseSourceDependencyState, nameof(request.DataWarehouseSourceDependencyState), errors);
        ValidateState(request.DataContractRegistryDependencyState, nameof(request.DataContractRegistryDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.StewardshipPreconditionState, nameof(request.StewardshipPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.PublicationPolicyState, nameof(request.PublicationPolicyState), errors);

        if (request.ScorecardsDashboardsReadinessState == ScorecardsDashboardsReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.ScorecardCatalogBoundaryState == ScorecardsDashboardsReadinessState.Ready)
        {
            errors.Add("Scorecard catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.WidgetBindingIntakeBoundaryState == ScorecardsDashboardsReadinessState.Ready)
        {
            errors.Add("Widget binding intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.LayoutScopeBoundaryState == ScorecardsDashboardsReadinessState.Ready)
        {
            errors.Add("Layout scope cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.PublicationControlBoundaryState == ScorecardsDashboardsReadinessState.Ready)
        {
            errors.Add("Publication control cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.DashboardReviewBoundaryState == ScorecardsDashboardsReadinessState.Ready)
        {
            errors.Add("Dashboard review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == ScorecardsDashboardsReadinessState.Ready)
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
            errors.Add("Scorecards and dashboards readiness metadata cannot contain widget/tile data or query results, chart series or measure values, rendered visual payloads, image/export blobs, embedded dataset rows, viewer/owner PII or personal contact details, free-text notes, narrative, attachments, document payloads, ratings, scores, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static ScorecardsDashboardsReadinessState ResolveFailClosedReadinessState(ScorecardsDashboardsReadinessCreateRequest request)
    {
        if (request.ScorecardsDashboardsReadinessState != ScorecardsDashboardsReadinessState.Ready)
        {
            return request.ScorecardsDashboardsReadinessState;
        }

        return ArePreconditionsReady(
            request.ScorecardCatalogBoundaryState,
            request.WidgetBindingIntakeBoundaryState,
            request.LayoutScopeBoundaryState,
            request.PublicationControlBoundaryState,
            request.DashboardReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.MetricSemanticRegistrySourceDependencyState,
            request.DataWarehouseSourceDependencyState,
            request.DataContractRegistryDependencyState,
            request.NotificationDependencyState,
            request.StewardshipPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.PublicationPolicyState,
            request.DependencyStates)
            ? ScorecardsDashboardsReadinessState.Ready
            : ScorecardsDashboardsReadinessState.Deferred;
    }

    public static void ApplyEvaluation(ScorecardsDashboardsReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.ScorecardsDashboardsReadinessState = ArePreconditionsReady(
            entity.ScorecardCatalogBoundaryState,
            entity.WidgetBindingIntakeBoundaryState,
            entity.LayoutScopeBoundaryState,
            entity.PublicationControlBoundaryState,
            entity.DashboardReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.MetricSemanticRegistrySourceDependencyState,
            entity.DataWarehouseSourceDependencyState,
            entity.DataContractRegistryDependencyState,
            entity.NotificationDependencyState,
            entity.StewardshipPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates)
            ? ScorecardsDashboardsReadinessState.Ready
            : ScorecardsDashboardsReadinessState.Deferred;

        entity.DeferredReason = entity.ScorecardsDashboardsReadinessState == ScorecardsDashboardsReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Scorecards and dashboards readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        ScorecardsDashboardsReadinessState scorecardCatalog,
        ScorecardsDashboardsReadinessState widgetBindingIntake,
        ScorecardsDashboardsReadinessState layoutScope,
        ScorecardsDashboardsReadinessState publicationControl,
        ScorecardsDashboardsReadinessState dashboardReview,
        ScorecardsDashboardsReadinessState automatedDecision,
        ScorecardsDashboardsReadinessState metricSemanticRegistrySourceDependency,
        ScorecardsDashboardsReadinessState dataWarehouseSourceDependency,
        ScorecardsDashboardsReadinessState dataContractRegistryDependency,
        ScorecardsDashboardsReadinessState notificationDependency,
        ScorecardsDashboardsReadinessState consent,
        ScorecardsDashboardsReadinessState dataMinimization,
        ScorecardsDashboardsReadinessState retention,
        ScorecardsDashboardsReadinessState evidence,
        IReadOnlyDictionary<string, ScorecardsDashboardsReadinessState> dependencyStates)
    {
        var required = new[]
        {
            scorecardCatalog,
            widgetBindingIntake,
            layoutScope,
            publicationControl,
            dashboardReview,
            automatedDecision,
            metricSemanticRegistrySourceDependency,
            dataWarehouseSourceDependency,
            dataContractRegistryDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(ScorecardsDashboardsReadinessState state) =>
        state is ScorecardsDashboardsReadinessState.Ready or ScorecardsDashboardsReadinessState.NotRequired;

    private static void ValidateState(ScorecardsDashboardsReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(ScorecardsDashboardsReadinessCreateRequest request)
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
