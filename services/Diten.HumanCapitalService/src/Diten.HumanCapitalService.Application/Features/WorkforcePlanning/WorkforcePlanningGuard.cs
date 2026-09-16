using System.Text.RegularExpressions;
using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.WorkforcePlanning;

public static class WorkforcePlanningGuard
{
    public const string OwnerKey = "hcm.workforce-planning";
    public const string ReadPermission = "hcm.workforce-planning.read";
    public const string ManagePermission = "hcm.workforce-planning.manage";
    public const string EvaluatePermission = "hcm.workforce-planning.evaluate";
    public const string AuditReadPermission = "hcm.workforce-planning.audit.read";

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
    // "workforce-planning" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(WorkforcePlanningReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.WorkforcePlanningReadinessVersion < 1)
        {
            errors.Add("WorkforcePlanningReadinessVersion must be greater than zero.");
        }

        ValidateState(request.WorkforcePlanningReadinessState, nameof(request.WorkforcePlanningReadinessState), errors);
        ValidateState(request.HeadcountPlanBoundaryState, nameof(request.HeadcountPlanBoundaryState), errors);
        ValidateState(request.DemandForecastBoundaryState, nameof(request.DemandForecastBoundaryState), errors);
        ValidateState(request.SupplyForecastBoundaryState, nameof(request.SupplyForecastBoundaryState), errors);
        ValidateState(request.GapAnalysisBoundaryState, nameof(request.GapAnalysisBoundaryState), errors);
        ValidateState(request.ScenarioModelingBoundaryState, nameof(request.ScenarioModelingBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.OrganizationStructureDependencyState, nameof(request.OrganizationStructureDependencyState), errors);
        ValidateState(request.PositionFrameworkDependencyState, nameof(request.PositionFrameworkDependencyState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.WorkforcePlanningReadinessState == WorkforcePlanningReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.HeadcountPlanBoundaryState == WorkforcePlanningReadinessState.Ready)
        {
            errors.Add("Headcount plan cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.DemandForecastBoundaryState == WorkforcePlanningReadinessState.Ready)
        {
            errors.Add("Demand forecast cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.SupplyForecastBoundaryState == WorkforcePlanningReadinessState.Ready)
        {
            errors.Add("Supply forecast cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.GapAnalysisBoundaryState == WorkforcePlanningReadinessState.Ready)
        {
            errors.Add("Gap analysis cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ScenarioModelingBoundaryState == WorkforcePlanningReadinessState.Ready)
        {
            errors.Add("Scenario modeling cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == WorkforcePlanningReadinessState.Ready)
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
            errors.Add("Workforce planning readiness metadata cannot contain forecast workflow bodies, scores, ratings, calibration outcomes, rankings, automated decision outputs, free-text planning notes, narrative, attachments, document payloads, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static WorkforcePlanningReadinessState ResolveFailClosedReadinessState(WorkforcePlanningReadinessCreateRequest request)
    {
        if (request.WorkforcePlanningReadinessState != WorkforcePlanningReadinessState.Ready)
        {
            return request.WorkforcePlanningReadinessState;
        }

        return ArePreconditionsReady(
            request.HeadcountPlanBoundaryState,
            request.DemandForecastBoundaryState,
            request.SupplyForecastBoundaryState,
            request.GapAnalysisBoundaryState,
            request.ScenarioModelingBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.OrganizationStructureDependencyState,
            request.PositionFrameworkDependencyState,
            request.DocumentDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.DependencyStates)
            ? WorkforcePlanningReadinessState.Ready
            : WorkforcePlanningReadinessState.Deferred;
    }

    public static void ApplyEvaluation(WorkforcePlanningReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.WorkforcePlanningReadinessState = ArePreconditionsReady(
            entity.HeadcountPlanBoundaryState,
            entity.DemandForecastBoundaryState,
            entity.SupplyForecastBoundaryState,
            entity.GapAnalysisBoundaryState,
            entity.ScenarioModelingBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.OrganizationStructureDependencyState,
            entity.PositionFrameworkDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates)
            ? WorkforcePlanningReadinessState.Ready
            : WorkforcePlanningReadinessState.Deferred;

        entity.DeferredReason = entity.WorkforcePlanningReadinessState == WorkforcePlanningReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Workforce planning readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        WorkforcePlanningReadinessState headcountPlan,
        WorkforcePlanningReadinessState demandForecast,
        WorkforcePlanningReadinessState supplyForecast,
        WorkforcePlanningReadinessState gapAnalysis,
        WorkforcePlanningReadinessState scenarioModeling,
        WorkforcePlanningReadinessState automatedDecision,
        WorkforcePlanningReadinessState organizationStructureDependency,
        WorkforcePlanningReadinessState positionFrameworkDependency,
        WorkforcePlanningReadinessState documentDependency,
        WorkforcePlanningReadinessState notificationDependency,
        WorkforcePlanningReadinessState consent,
        WorkforcePlanningReadinessState dataMinimization,
        WorkforcePlanningReadinessState retention,
        WorkforcePlanningReadinessState evidence,
        IReadOnlyDictionary<string, WorkforcePlanningReadinessState> dependencyStates)
    {
        var required = new[]
        {
            headcountPlan,
            demandForecast,
            supplyForecast,
            gapAnalysis,
            scenarioModeling,
            automatedDecision,
            organizationStructureDependency,
            positionFrameworkDependency,
            documentDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(WorkforcePlanningReadinessState state) =>
        state is WorkforcePlanningReadinessState.Ready or WorkforcePlanningReadinessState.NotRequired;

    private static void ValidateState(WorkforcePlanningReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(WorkforcePlanningReadinessCreateRequest request)
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
