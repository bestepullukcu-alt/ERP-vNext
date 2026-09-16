using System.Text.RegularExpressions;
using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.HrKpiAnalytics;

public static class HrKpiAnalyticsGuard
{
    public const string OwnerKey = "hcm.hr-kpi-analytics";
    public const string ReadPermission = "hcm.hr-kpi-analytics.read";
    public const string ManagePermission = "hcm.hr-kpi-analytics.manage";
    public const string EvaluatePermission = "hcm.hr-kpi-analytics.evaluate";
    public const string AuditReadPermission = "hcm.hr-kpi-analytics.audit.read";

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
    // "hr-kpi-analytics" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(HrKpiAnalyticsReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.HrKpiAnalyticsReadinessVersion < 1)
        {
            errors.Add("HrKpiAnalyticsReadinessVersion must be greater than zero.");
        }

        ValidateState(request.HrKpiAnalyticsReadinessState, nameof(request.HrKpiAnalyticsReadinessState), errors);
        ValidateState(request.KpiCatalogBoundaryState, nameof(request.KpiCatalogBoundaryState), errors);
        ValidateState(request.MetricDefinitionBoundaryState, nameof(request.MetricDefinitionBoundaryState), errors);
        ValidateState(request.DashboardBoundaryState, nameof(request.DashboardBoundaryState), errors);
        ValidateState(request.AnalyticsQueryBoundaryState, nameof(request.AnalyticsQueryBoundaryState), errors);
        ValidateState(request.DataExportBoundaryState, nameof(request.DataExportBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.AnalyticsPlatformDependencyState, nameof(request.AnalyticsPlatformDependencyState), errors);
        ValidateState(request.DataSourceDependencyState, nameof(request.DataSourceDependencyState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.HrKpiAnalyticsReadinessState == HrKpiAnalyticsReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.KpiCatalogBoundaryState == HrKpiAnalyticsReadinessState.Ready)
        {
            errors.Add("KPI catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.MetricDefinitionBoundaryState == HrKpiAnalyticsReadinessState.Ready)
        {
            errors.Add("Metric definition cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.DashboardBoundaryState == HrKpiAnalyticsReadinessState.Ready)
        {
            errors.Add("Dashboard cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AnalyticsQueryBoundaryState == HrKpiAnalyticsReadinessState.Ready)
        {
            errors.Add("Analytics query cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.DataExportBoundaryState == HrKpiAnalyticsReadinessState.Ready)
        {
            errors.Add("Data export cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == HrKpiAnalyticsReadinessState.Ready)
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
            errors.Add("HR KPI and analytics readiness metadata cannot contain computed KPI values, metric results, dashboard datasets, exported analytics data, scores, ratings, calibration outcomes, rankings, model output, automated decision outputs, free-text analytics notes, narrative, attachments, document payloads, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static HrKpiAnalyticsReadinessState ResolveFailClosedReadinessState(HrKpiAnalyticsReadinessCreateRequest request)
    {
        if (request.HrKpiAnalyticsReadinessState != HrKpiAnalyticsReadinessState.Ready)
        {
            return request.HrKpiAnalyticsReadinessState;
        }

        return ArePreconditionsReady(
            request.KpiCatalogBoundaryState,
            request.MetricDefinitionBoundaryState,
            request.DashboardBoundaryState,
            request.AnalyticsQueryBoundaryState,
            request.DataExportBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.AnalyticsPlatformDependencyState,
            request.DataSourceDependencyState,
            request.DocumentDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.DependencyStates)
            ? HrKpiAnalyticsReadinessState.Ready
            : HrKpiAnalyticsReadinessState.Deferred;
    }

    public static void ApplyEvaluation(HrKpiAnalyticsReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.HrKpiAnalyticsReadinessState = ArePreconditionsReady(
            entity.KpiCatalogBoundaryState,
            entity.MetricDefinitionBoundaryState,
            entity.DashboardBoundaryState,
            entity.AnalyticsQueryBoundaryState,
            entity.DataExportBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.AnalyticsPlatformDependencyState,
            entity.DataSourceDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates)
            ? HrKpiAnalyticsReadinessState.Ready
            : HrKpiAnalyticsReadinessState.Deferred;

        entity.DeferredReason = entity.HrKpiAnalyticsReadinessState == HrKpiAnalyticsReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "HR KPI and analytics readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        HrKpiAnalyticsReadinessState kpiCatalog,
        HrKpiAnalyticsReadinessState metricDefinition,
        HrKpiAnalyticsReadinessState dashboard,
        HrKpiAnalyticsReadinessState analyticsQuery,
        HrKpiAnalyticsReadinessState dataExport,
        HrKpiAnalyticsReadinessState automatedDecision,
        HrKpiAnalyticsReadinessState analyticsPlatformDependency,
        HrKpiAnalyticsReadinessState dataSourceDependency,
        HrKpiAnalyticsReadinessState documentDependency,
        HrKpiAnalyticsReadinessState notificationDependency,
        HrKpiAnalyticsReadinessState consent,
        HrKpiAnalyticsReadinessState dataMinimization,
        HrKpiAnalyticsReadinessState retention,
        HrKpiAnalyticsReadinessState evidence,
        IReadOnlyDictionary<string, HrKpiAnalyticsReadinessState> dependencyStates)
    {
        var required = new[]
        {
            kpiCatalog,
            metricDefinition,
            dashboard,
            analyticsQuery,
            dataExport,
            automatedDecision,
            analyticsPlatformDependency,
            dataSourceDependency,
            documentDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(HrKpiAnalyticsReadinessState state) =>
        state is HrKpiAnalyticsReadinessState.Ready or HrKpiAnalyticsReadinessState.NotRequired;

    private static void ValidateState(HrKpiAnalyticsReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(HrKpiAnalyticsReadinessCreateRequest request)
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
