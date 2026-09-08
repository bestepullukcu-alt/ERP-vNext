using System.Text.RegularExpressions;
using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.Succession;

public static class SuccessionGuard
{
    public const string OwnerKey = "hcm.succession";
    public const string ReadPermission = "hcm.succession.read";
    public const string ManagePermission = "hcm.succession.manage";
    public const string EvaluatePermission = "hcm.succession.evaluate";
    public const string AuditReadPermission = "hcm.succession.audit.read";

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
    // "succession" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(SuccessionReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.SuccessionReadinessVersion < 1)
        {
            errors.Add("SuccessionReadinessVersion must be greater than zero.");
        }

        ValidateState(request.SuccessionReadinessState, nameof(request.SuccessionReadinessState), errors);
        ValidateState(request.SuccessionPoolBoundaryState, nameof(request.SuccessionPoolBoundaryState), errors);
        ValidateState(request.HighPotentialIdentificationBoundaryState, nameof(request.HighPotentialIdentificationBoundaryState), errors);
        ValidateState(request.NominationWorkflowBoundaryState, nameof(request.NominationWorkflowBoundaryState), errors);
        ValidateState(request.ReadinessAssessmentBoundaryState, nameof(request.ReadinessAssessmentBoundaryState), errors);
        ValidateState(request.TalentReviewBoundaryState, nameof(request.TalentReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.TalentProfileDependencyState, nameof(request.TalentProfileDependencyState), errors);
        ValidateState(request.PositionFrameworkDependencyState, nameof(request.PositionFrameworkDependencyState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.SuccessionReadinessState == SuccessionReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.SuccessionPoolBoundaryState == SuccessionReadinessState.Ready)
        {
            errors.Add("Succession pool cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.HighPotentialIdentificationBoundaryState == SuccessionReadinessState.Ready)
        {
            errors.Add("High-potential identification cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.NominationWorkflowBoundaryState == SuccessionReadinessState.Ready)
        {
            errors.Add("Nomination workflow cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ReadinessAssessmentBoundaryState == SuccessionReadinessState.Ready)
        {
            errors.Add("Readiness assessment cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.TalentReviewBoundaryState == SuccessionReadinessState.Ready)
        {
            errors.Add("Talent review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == SuccessionReadinessState.Ready)
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
            errors.Add("Succession readiness metadata cannot contain assessment workflow bodies, scores, ratings, calibration outcomes, rankings, automated decision outputs, free-text assessment notes, appraisal narrative, attachments, document payloads, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static SuccessionReadinessState ResolveFailClosedReadinessState(SuccessionReadinessCreateRequest request)
    {
        if (request.SuccessionReadinessState != SuccessionReadinessState.Ready)
        {
            return request.SuccessionReadinessState;
        }

        return ArePreconditionsReady(
            request.SuccessionPoolBoundaryState,
            request.HighPotentialIdentificationBoundaryState,
            request.NominationWorkflowBoundaryState,
            request.ReadinessAssessmentBoundaryState,
            request.TalentReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.TalentProfileDependencyState,
            request.PositionFrameworkDependencyState,
            request.DocumentDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.DependencyStates)
            ? SuccessionReadinessState.Ready
            : SuccessionReadinessState.Deferred;
    }

    public static void ApplyEvaluation(SuccessionReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.SuccessionReadinessState = ArePreconditionsReady(
            entity.SuccessionPoolBoundaryState,
            entity.HighPotentialIdentificationBoundaryState,
            entity.NominationWorkflowBoundaryState,
            entity.ReadinessAssessmentBoundaryState,
            entity.TalentReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentProfileDependencyState,
            entity.PositionFrameworkDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates)
            ? SuccessionReadinessState.Ready
            : SuccessionReadinessState.Deferred;

        entity.DeferredReason = entity.SuccessionReadinessState == SuccessionReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Succession readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        SuccessionReadinessState successionPool,
        SuccessionReadinessState highPotentialIdentification,
        SuccessionReadinessState nominationWorkflow,
        SuccessionReadinessState readinessAssessment,
        SuccessionReadinessState talentReview,
        SuccessionReadinessState automatedDecision,
        SuccessionReadinessState talentProfileDependency,
        SuccessionReadinessState positionFrameworkDependency,
        SuccessionReadinessState documentDependency,
        SuccessionReadinessState notificationDependency,
        SuccessionReadinessState consent,
        SuccessionReadinessState dataMinimization,
        SuccessionReadinessState retention,
        SuccessionReadinessState evidence,
        IReadOnlyDictionary<string, SuccessionReadinessState> dependencyStates)
    {
        var required = new[]
        {
            successionPool,
            highPotentialIdentification,
            nominationWorkflow,
            readinessAssessment,
            talentReview,
            automatedDecision,
            talentProfileDependency,
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

    private static bool IsSatisfied(SuccessionReadinessState state) =>
        state is SuccessionReadinessState.Ready or SuccessionReadinessState.NotRequired;

    private static void ValidateState(SuccessionReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(SuccessionReadinessCreateRequest request)
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
