using System.Text.RegularExpressions;
using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.HeadcountBudget;

public static class HeadcountBudgetGuard
{
    public const string OwnerKey = "hcm.headcount-budget";
    public const string ReadPermission = "hcm.headcount-budget.read";
    public const string ManagePermission = "hcm.headcount-budget.manage";
    public const string EvaluatePermission = "hcm.headcount-budget.evaluate";
    public const string AuditReadPermission = "hcm.headcount-budget.audit.read";

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
    // "headcount-budget" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(HeadcountBudgetReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.HeadcountBudgetReadinessVersion < 1)
        {
            errors.Add("HeadcountBudgetReadinessVersion must be greater than zero.");
        }

        ValidateState(request.HeadcountBudgetReadinessState, nameof(request.HeadcountBudgetReadinessState), errors);
        ValidateState(request.HeadcountRequisitionBoundaryState, nameof(request.HeadcountRequisitionBoundaryState), errors);
        ValidateState(request.PositionBudgetBoundaryState, nameof(request.PositionBudgetBoundaryState), errors);
        ValidateState(request.BudgetAllocationBoundaryState, nameof(request.BudgetAllocationBoundaryState), errors);
        ValidateState(request.BudgetApprovalBoundaryState, nameof(request.BudgetApprovalBoundaryState), errors);
        ValidateState(request.BudgetReconciliationBoundaryState, nameof(request.BudgetReconciliationBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.OrganizationStructureDependencyState, nameof(request.OrganizationStructureDependencyState), errors);
        ValidateState(request.PositionFrameworkDependencyState, nameof(request.PositionFrameworkDependencyState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.HeadcountBudgetReadinessState == HeadcountBudgetReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.HeadcountRequisitionBoundaryState == HeadcountBudgetReadinessState.Ready)
        {
            errors.Add("Headcount requisition cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.PositionBudgetBoundaryState == HeadcountBudgetReadinessState.Ready)
        {
            errors.Add("Position budget cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.BudgetAllocationBoundaryState == HeadcountBudgetReadinessState.Ready)
        {
            errors.Add("Budget allocation cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.BudgetApprovalBoundaryState == HeadcountBudgetReadinessState.Ready)
        {
            errors.Add("Budget approval cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.BudgetReconciliationBoundaryState == HeadcountBudgetReadinessState.Ready)
        {
            errors.Add("Budget reconciliation cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == HeadcountBudgetReadinessState.Ready)
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
            errors.Add("Headcount and position budget readiness metadata cannot contain budget amounts, headcount cost figures, scores, ratings, calibration outcomes, rankings, automated decision outputs, free-text planning notes, narrative, attachments, document payloads, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static HeadcountBudgetReadinessState ResolveFailClosedReadinessState(HeadcountBudgetReadinessCreateRequest request)
    {
        if (request.HeadcountBudgetReadinessState != HeadcountBudgetReadinessState.Ready)
        {
            return request.HeadcountBudgetReadinessState;
        }

        return ArePreconditionsReady(
            request.HeadcountRequisitionBoundaryState,
            request.PositionBudgetBoundaryState,
            request.BudgetAllocationBoundaryState,
            request.BudgetApprovalBoundaryState,
            request.BudgetReconciliationBoundaryState,
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
            ? HeadcountBudgetReadinessState.Ready
            : HeadcountBudgetReadinessState.Deferred;
    }

    public static void ApplyEvaluation(HeadcountBudgetReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.HeadcountBudgetReadinessState = ArePreconditionsReady(
            entity.HeadcountRequisitionBoundaryState,
            entity.PositionBudgetBoundaryState,
            entity.BudgetAllocationBoundaryState,
            entity.BudgetApprovalBoundaryState,
            entity.BudgetReconciliationBoundaryState,
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
            ? HeadcountBudgetReadinessState.Ready
            : HeadcountBudgetReadinessState.Deferred;

        entity.DeferredReason = entity.HeadcountBudgetReadinessState == HeadcountBudgetReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Headcount and position budget readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        HeadcountBudgetReadinessState headcountRequisition,
        HeadcountBudgetReadinessState positionBudget,
        HeadcountBudgetReadinessState budgetAllocation,
        HeadcountBudgetReadinessState budgetApproval,
        HeadcountBudgetReadinessState budgetReconciliation,
        HeadcountBudgetReadinessState automatedDecision,
        HeadcountBudgetReadinessState organizationStructureDependency,
        HeadcountBudgetReadinessState positionFrameworkDependency,
        HeadcountBudgetReadinessState documentDependency,
        HeadcountBudgetReadinessState notificationDependency,
        HeadcountBudgetReadinessState consent,
        HeadcountBudgetReadinessState dataMinimization,
        HeadcountBudgetReadinessState retention,
        HeadcountBudgetReadinessState evidence,
        IReadOnlyDictionary<string, HeadcountBudgetReadinessState> dependencyStates)
    {
        var required = new[]
        {
            headcountRequisition,
            positionBudget,
            budgetAllocation,
            budgetApproval,
            budgetReconciliation,
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

    private static bool IsSatisfied(HeadcountBudgetReadinessState state) =>
        state is HeadcountBudgetReadinessState.Ready or HeadcountBudgetReadinessState.NotRequired;

    private static void ValidateState(HeadcountBudgetReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(HeadcountBudgetReadinessCreateRequest request)
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
