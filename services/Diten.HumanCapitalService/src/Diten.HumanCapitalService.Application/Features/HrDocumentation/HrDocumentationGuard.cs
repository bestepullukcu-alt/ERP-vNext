using System.Text.RegularExpressions;
using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.HrDocumentation;

public static class HrDocumentationGuard
{
    public const string OwnerKey = "hcm.hr-documentation";
    public const string ReadPermission = "hcm.hr-documentation.read";
    public const string ManagePermission = "hcm.hr-documentation.manage";
    public const string EvaluatePermission = "hcm.hr-documentation.evaluate";
    public const string AuditReadPermission = "hcm.hr-documentation.audit.read";

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
    // "hr-documentation" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(HrDocumentationReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.HrDocumentationReadinessVersion < 1)
        {
            errors.Add("HrDocumentationReadinessVersion must be greater than zero.");
        }

        ValidateState(request.HrDocumentationReadinessState, nameof(request.HrDocumentationReadinessState), errors);
        ValidateState(request.DocumentWorkspaceBoundaryState, nameof(request.DocumentWorkspaceBoundaryState), errors);
        ValidateState(request.EvidenceLinkBoundaryState, nameof(request.EvidenceLinkBoundaryState), errors);
        ValidateState(request.DocumentClassificationBoundaryState, nameof(request.DocumentClassificationBoundaryState), errors);
        ValidateState(request.LegalHoldBoundaryState, nameof(request.LegalHoldBoundaryState), errors);
        ValidateState(request.DispositionScheduleBoundaryState, nameof(request.DispositionScheduleBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.DocumentRepositoryDependencyState, nameof(request.DocumentRepositoryDependencyState), errors);
        ValidateState(request.EvidenceStoreDependencyState, nameof(request.EvidenceStoreDependencyState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.HrDocumentationReadinessState == HrDocumentationReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.DocumentWorkspaceBoundaryState == HrDocumentationReadinessState.Ready)
        {
            errors.Add("Document workspace cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.EvidenceLinkBoundaryState == HrDocumentationReadinessState.Ready)
        {
            errors.Add("Evidence link cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.DocumentClassificationBoundaryState == HrDocumentationReadinessState.Ready)
        {
            errors.Add("Document classification cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.LegalHoldBoundaryState == HrDocumentationReadinessState.Ready)
        {
            errors.Add("Legal hold cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.DispositionScheduleBoundaryState == HrDocumentationReadinessState.Ready)
        {
            errors.Add("Disposition schedule cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == HrDocumentationReadinessState.Ready)
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
            errors.Add("HR documentation and evidence readiness metadata cannot contain document payloads, evidence file content, attachments, controlled document bodies, exported document data, scores, ratings, calibration outcomes, rankings, model output, automated decision outputs, free-text documentation notes, narrative, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static HrDocumentationReadinessState ResolveFailClosedReadinessState(HrDocumentationReadinessCreateRequest request)
    {
        if (request.HrDocumentationReadinessState != HrDocumentationReadinessState.Ready)
        {
            return request.HrDocumentationReadinessState;
        }

        return ArePreconditionsReady(
            request.DocumentWorkspaceBoundaryState,
            request.EvidenceLinkBoundaryState,
            request.DocumentClassificationBoundaryState,
            request.LegalHoldBoundaryState,
            request.DispositionScheduleBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.DocumentRepositoryDependencyState,
            request.EvidenceStoreDependencyState,
            request.DocumentDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.DependencyStates)
            ? HrDocumentationReadinessState.Ready
            : HrDocumentationReadinessState.Deferred;
    }

    public static void ApplyEvaluation(HrDocumentationReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.HrDocumentationReadinessState = ArePreconditionsReady(
            entity.DocumentWorkspaceBoundaryState,
            entity.EvidenceLinkBoundaryState,
            entity.DocumentClassificationBoundaryState,
            entity.LegalHoldBoundaryState,
            entity.DispositionScheduleBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.DocumentRepositoryDependencyState,
            entity.EvidenceStoreDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates)
            ? HrDocumentationReadinessState.Ready
            : HrDocumentationReadinessState.Deferred;

        entity.DeferredReason = entity.HrDocumentationReadinessState == HrDocumentationReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "HR documentation and evidence readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        HrDocumentationReadinessState documentWorkspace,
        HrDocumentationReadinessState evidenceLink,
        HrDocumentationReadinessState documentClassification,
        HrDocumentationReadinessState legalHold,
        HrDocumentationReadinessState dispositionSchedule,
        HrDocumentationReadinessState automatedDecision,
        HrDocumentationReadinessState documentRepositoryDependency,
        HrDocumentationReadinessState evidenceStoreDependency,
        HrDocumentationReadinessState documentDependency,
        HrDocumentationReadinessState notificationDependency,
        HrDocumentationReadinessState consent,
        HrDocumentationReadinessState dataMinimization,
        HrDocumentationReadinessState retention,
        HrDocumentationReadinessState evidence,
        IReadOnlyDictionary<string, HrDocumentationReadinessState> dependencyStates)
    {
        var required = new[]
        {
            documentWorkspace,
            evidenceLink,
            documentClassification,
            legalHold,
            dispositionSchedule,
            automatedDecision,
            documentRepositoryDependency,
            evidenceStoreDependency,
            documentDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(HrDocumentationReadinessState state) =>
        state is HrDocumentationReadinessState.Ready or HrDocumentationReadinessState.NotRequired;

    private static void ValidateState(HrDocumentationReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(HrDocumentationReadinessCreateRequest request)
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
