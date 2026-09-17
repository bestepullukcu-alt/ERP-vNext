using System.Text.RegularExpressions;
using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.HrCaseManagement;

public static class HrCaseManagementGuard
{
    public const string OwnerKey = "hcm.hr-case-management";
    public const string ReadPermission = "hcm.hr-case-management.read";
    public const string ManagePermission = "hcm.hr-case-management.manage";
    public const string EvaluatePermission = "hcm.hr-case-management.evaluate";
    public const string AuditReadPermission = "hcm.hr-case-management.audit.read";

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
    // "hr-case-management" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(HrCaseManagementReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.HrCaseManagementReadinessVersion < 1)
        {
            errors.Add("HrCaseManagementReadinessVersion must be greater than zero.");
        }

        ValidateState(request.HrCaseManagementReadinessState, nameof(request.HrCaseManagementReadinessState), errors);
        ValidateState(request.CaseIntakeBoundaryState, nameof(request.CaseIntakeBoundaryState), errors);
        ValidateState(request.CaseTriageBoundaryState, nameof(request.CaseTriageBoundaryState), errors);
        ValidateState(request.InvestigationTrackingBoundaryState, nameof(request.InvestigationTrackingBoundaryState), errors);
        ValidateState(request.DisciplinaryActionBoundaryState, nameof(request.DisciplinaryActionBoundaryState), errors);
        ValidateState(request.ResolutionClosureBoundaryState, nameof(request.ResolutionClosureBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.EmployeeRecordDependencyState, nameof(request.EmployeeRecordDependencyState), errors);
        ValidateState(request.SensitiveAccessPolicyDependencyState, nameof(request.SensitiveAccessPolicyDependencyState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.HrCaseManagementReadinessState == HrCaseManagementReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.CaseIntakeBoundaryState == HrCaseManagementReadinessState.Ready)
        {
            errors.Add("Case intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.CaseTriageBoundaryState == HrCaseManagementReadinessState.Ready)
        {
            errors.Add("Case triage cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.InvestigationTrackingBoundaryState == HrCaseManagementReadinessState.Ready)
        {
            errors.Add("Investigation tracking cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.DisciplinaryActionBoundaryState == HrCaseManagementReadinessState.Ready)
        {
            errors.Add("Disciplinary action cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ResolutionClosureBoundaryState == HrCaseManagementReadinessState.Ready)
        {
            errors.Add("Resolution closure cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == HrCaseManagementReadinessState.Ready)
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
            errors.Add("Employee relations and HR case readiness metadata cannot contain case bodies, grievance/disciplinary/investigation content, allegations, findings, statements, witness testimony, manager notes, employee statements, free-text notes, narrative, attachments, document payloads, scores, ratings, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static HrCaseManagementReadinessState ResolveFailClosedReadinessState(HrCaseManagementReadinessCreateRequest request)
    {
        if (request.HrCaseManagementReadinessState != HrCaseManagementReadinessState.Ready)
        {
            return request.HrCaseManagementReadinessState;
        }

        return ArePreconditionsReady(
            request.CaseIntakeBoundaryState,
            request.CaseTriageBoundaryState,
            request.InvestigationTrackingBoundaryState,
            request.DisciplinaryActionBoundaryState,
            request.ResolutionClosureBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.EmployeeRecordDependencyState,
            request.SensitiveAccessPolicyDependencyState,
            request.DocumentDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.DependencyStates)
            ? HrCaseManagementReadinessState.Ready
            : HrCaseManagementReadinessState.Deferred;
    }

    public static void ApplyEvaluation(HrCaseManagementReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.HrCaseManagementReadinessState = ArePreconditionsReady(
            entity.CaseIntakeBoundaryState,
            entity.CaseTriageBoundaryState,
            entity.InvestigationTrackingBoundaryState,
            entity.DisciplinaryActionBoundaryState,
            entity.ResolutionClosureBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.EmployeeRecordDependencyState,
            entity.SensitiveAccessPolicyDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates)
            ? HrCaseManagementReadinessState.Ready
            : HrCaseManagementReadinessState.Deferred;

        entity.DeferredReason = entity.HrCaseManagementReadinessState == HrCaseManagementReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Employee relations and HR case readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        HrCaseManagementReadinessState caseIntake,
        HrCaseManagementReadinessState caseTriage,
        HrCaseManagementReadinessState investigationTracking,
        HrCaseManagementReadinessState disciplinaryAction,
        HrCaseManagementReadinessState resolutionClosure,
        HrCaseManagementReadinessState automatedDecision,
        HrCaseManagementReadinessState employeeRecordDependency,
        HrCaseManagementReadinessState sensitiveAccessPolicyDependency,
        HrCaseManagementReadinessState documentDependency,
        HrCaseManagementReadinessState notificationDependency,
        HrCaseManagementReadinessState consent,
        HrCaseManagementReadinessState dataMinimization,
        HrCaseManagementReadinessState retention,
        HrCaseManagementReadinessState evidence,
        IReadOnlyDictionary<string, HrCaseManagementReadinessState> dependencyStates)
    {
        var required = new[]
        {
            caseIntake,
            caseTriage,
            investigationTracking,
            disciplinaryAction,
            resolutionClosure,
            automatedDecision,
            employeeRecordDependency,
            sensitiveAccessPolicyDependency,
            documentDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(HrCaseManagementReadinessState state) =>
        state is HrCaseManagementReadinessState.Ready or HrCaseManagementReadinessState.NotRequired;

    private static void ValidateState(HrCaseManagementReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(HrCaseManagementReadinessCreateRequest request)
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
