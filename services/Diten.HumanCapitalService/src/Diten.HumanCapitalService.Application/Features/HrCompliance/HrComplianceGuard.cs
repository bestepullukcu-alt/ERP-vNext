using System.Text.RegularExpressions;
using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.HrCompliance;

public static class HrComplianceGuard
{
    public const string OwnerKey = "hcm.hr-compliance";
    public const string ReadPermission = "hcm.hr-compliance.read";
    public const string ManagePermission = "hcm.hr-compliance.manage";
    public const string EvaluatePermission = "hcm.hr-compliance.evaluate";
    public const string AuditReadPermission = "hcm.hr-compliance.audit.read";

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
    // "hr-compliance" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(HrComplianceReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.HrComplianceReadinessVersion < 1)
        {
            errors.Add("HrComplianceReadinessVersion must be greater than zero.");
        }

        ValidateState(request.HrComplianceReadinessState, nameof(request.HrComplianceReadinessState), errors);
        ValidateState(request.ObligationCatalogBoundaryState, nameof(request.ObligationCatalogBoundaryState), errors);
        ValidateState(request.ControlMappingBoundaryState, nameof(request.ControlMappingBoundaryState), errors);
        ValidateState(request.StatutoryReportDefinitionBoundaryState, nameof(request.StatutoryReportDefinitionBoundaryState), errors);
        ValidateState(request.FilingScheduleBoundaryState, nameof(request.FilingScheduleBoundaryState), errors);
        ValidateState(request.AttestationClosureBoundaryState, nameof(request.AttestationClosureBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.RegulatorySourceDependencyState, nameof(request.RegulatorySourceDependencyState), errors);
        ValidateState(request.HcmDataSourceDependencyState, nameof(request.HcmDataSourceDependencyState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.HrComplianceReadinessState == HrComplianceReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.ObligationCatalogBoundaryState == HrComplianceReadinessState.Ready)
        {
            errors.Add("Obligation catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ControlMappingBoundaryState == HrComplianceReadinessState.Ready)
        {
            errors.Add("Control mapping cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.StatutoryReportDefinitionBoundaryState == HrComplianceReadinessState.Ready)
        {
            errors.Add("Statutory report definition cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.FilingScheduleBoundaryState == HrComplianceReadinessState.Ready)
        {
            errors.Add("Filing schedule cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AttestationClosureBoundaryState == HrComplianceReadinessState.Ready)
        {
            errors.Add("Attestation closure cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == HrComplianceReadinessState.Ready)
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
            errors.Add("HR compliance and statutory reporting readiness metadata cannot contain report bodies, statutory filing content, generated report data, exported datasets, findings, free-text notes, narrative, attachments, document payloads, scores, ratings, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static HrComplianceReadinessState ResolveFailClosedReadinessState(HrComplianceReadinessCreateRequest request)
    {
        if (request.HrComplianceReadinessState != HrComplianceReadinessState.Ready)
        {
            return request.HrComplianceReadinessState;
        }

        return ArePreconditionsReady(
            request.ObligationCatalogBoundaryState,
            request.ControlMappingBoundaryState,
            request.StatutoryReportDefinitionBoundaryState,
            request.FilingScheduleBoundaryState,
            request.AttestationClosureBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.RegulatorySourceDependencyState,
            request.HcmDataSourceDependencyState,
            request.DocumentDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.DependencyStates)
            ? HrComplianceReadinessState.Ready
            : HrComplianceReadinessState.Deferred;
    }

    public static void ApplyEvaluation(HrComplianceReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.HrComplianceReadinessState = ArePreconditionsReady(
            entity.ObligationCatalogBoundaryState,
            entity.ControlMappingBoundaryState,
            entity.StatutoryReportDefinitionBoundaryState,
            entity.FilingScheduleBoundaryState,
            entity.AttestationClosureBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.RegulatorySourceDependencyState,
            entity.HcmDataSourceDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates)
            ? HrComplianceReadinessState.Ready
            : HrComplianceReadinessState.Deferred;

        entity.DeferredReason = entity.HrComplianceReadinessState == HrComplianceReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "HR compliance and statutory reporting readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        HrComplianceReadinessState obligationCatalog,
        HrComplianceReadinessState controlMapping,
        HrComplianceReadinessState statutoryReportDefinition,
        HrComplianceReadinessState filingSchedule,
        HrComplianceReadinessState attestationClosure,
        HrComplianceReadinessState automatedDecision,
        HrComplianceReadinessState regulatorySourceDependency,
        HrComplianceReadinessState hcmDataSourceDependency,
        HrComplianceReadinessState documentDependency,
        HrComplianceReadinessState notificationDependency,
        HrComplianceReadinessState consent,
        HrComplianceReadinessState dataMinimization,
        HrComplianceReadinessState retention,
        HrComplianceReadinessState evidence,
        IReadOnlyDictionary<string, HrComplianceReadinessState> dependencyStates)
    {
        var required = new[]
        {
            obligationCatalog,
            controlMapping,
            statutoryReportDefinition,
            filingSchedule,
            attestationClosure,
            automatedDecision,
            regulatorySourceDependency,
            hcmDataSourceDependency,
            documentDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(HrComplianceReadinessState state) =>
        state is HrComplianceReadinessState.Ready or HrComplianceReadinessState.NotRequired;

    private static void ValidateState(HrComplianceReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(HrComplianceReadinessCreateRequest request)
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
