using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using System.Text.RegularExpressions;

namespace Diten.HumanCapitalService.Application.Features.CompetencySkills;

public static class CompetencySkillsGuard
{
    public const string OwnerKey = "hcm.competency-skills";
    public const string ReadPermission = "hcm.competency-skills.read";
    public const string ManagePermission = "hcm.competency-skills.manage";
    public const string EvaluatePermission = "hcm.competency-skills.evaluate";
    public const string AuditReadPermission = "hcm.competency-skills.audit.read";

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
    // words such as "taxonomy" (contains "tax") or "scorecard" (contains "score") are NOT
    // falsely rejected, while real markers ("tax", "ssn", "salary", ...) still match. Tested
    // against the RAW value. Underscore is a regex word character, so snake_case markers match.
    private static readonly Regex ForbiddenMarkerRegex = new(
        @"\b(" + string.Join("|", ForbiddenMarkers.Select(Regex.Escape)) + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static Response<Guid> RequireTenant(ITenantContext tenantContext) =>
        tenantContext.TenantId is { } tenantId && tenantId != Guid.Empty
            ? Response<Guid>.Success(tenantId)
            : Response<Guid>.Fail("Tenant context is required.", 401);

    public static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    public static IReadOnlyList<string> Validate(CompetencySkillsReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.CompetencySkillsReadinessVersion < 1)
        {
            errors.Add("CompetencySkillsReadinessVersion must be greater than zero.");
        }

        ValidateState(request.CompetencySkillsReadinessState, nameof(request.CompetencySkillsReadinessState), errors);
        ValidateState(request.AssessmentWorkflowBoundaryState, nameof(request.AssessmentWorkflowBoundaryState), errors);
        ValidateState(request.CompetencyFrameworkDependencyState, nameof(request.CompetencyFrameworkDependencyState), errors);
        ValidateState(request.SkillTaxonomyDependencyState, nameof(request.SkillTaxonomyDependencyState), errors);
        ValidateState(request.SkillScoringBoundaryState, nameof(request.SkillScoringBoundaryState), errors);
        ValidateState(request.RatingBoundaryState, nameof(request.RatingBoundaryState), errors);
        ValidateState(request.CalibrationBoundaryState, nameof(request.CalibrationBoundaryState), errors);
        ValidateState(request.RankingBoundaryState, nameof(request.RankingBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.ManagerAssessmentUxBoundaryState, nameof(request.ManagerAssessmentUxBoundaryState), errors);
        ValidateState(request.EmployeeAssessmentUxBoundaryState, nameof(request.EmployeeAssessmentUxBoundaryState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.CompetencySkillsReadinessState == CompetencySkillsReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.AssessmentWorkflowBoundaryState == CompetencySkillsReadinessState.Ready)
        {
            errors.Add("Competency skills assessment workflow cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.SkillScoringBoundaryState == CompetencySkillsReadinessState.Ready)
        {
            errors.Add("Skill scoring cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.RatingBoundaryState == CompetencySkillsReadinessState.Ready)
        {
            errors.Add("Rating behavior cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.CalibrationBoundaryState == CompetencySkillsReadinessState.Ready)
        {
            errors.Add("Calibration behavior cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.RankingBoundaryState == CompetencySkillsReadinessState.Ready)
        {
            errors.Add("Ranking behavior cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == CompetencySkillsReadinessState.Ready)
        {
            errors.Add("Automated decision behavior cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ManagerAssessmentUxBoundaryState == CompetencySkillsReadinessState.Ready)
        {
            errors.Add("Manager assessment UX cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.EmployeeAssessmentUxBoundaryState == CompetencySkillsReadinessState.Ready)
        {
            errors.Add("Employee assessment UX cannot be marked Ready in the first metadata-only slice.");
        }

        foreach (var pair in request.DependencyStates)
        {
            RequireText(pair.Key, nameof(request.DependencyStates), 80, errors);
            ValidateState(pair.Value, $"{nameof(request.DependencyStates)}.{pair.Key}", errors);
        }

        if (ForbiddenValues(request).Any(ContainsForbiddenMarker))
        {
            errors.Add("Competency skills readiness metadata cannot contain assessment workflow bodies, scores, ratings, calibration outcomes, rankings, automated decision outputs, free-text assessment notes, appraisal narrative, attachments, document payloads, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static CompetencySkillsReadinessState ResolveFailClosedReadinessState(CompetencySkillsReadinessCreateRequest request)
    {
        if (request.CompetencySkillsReadinessState != CompetencySkillsReadinessState.Ready)
        {
            return request.CompetencySkillsReadinessState;
        }

        return ArePreconditionsReady(
            request.AssessmentWorkflowBoundaryState,
            request.CompetencyFrameworkDependencyState,
            request.SkillTaxonomyDependencyState,
            request.SkillScoringBoundaryState,
            request.RatingBoundaryState,
            request.CalibrationBoundaryState,
            request.RankingBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.ManagerAssessmentUxBoundaryState,
            request.EmployeeAssessmentUxBoundaryState,
            request.DocumentDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.DependencyStates)
            ? CompetencySkillsReadinessState.Ready
            : CompetencySkillsReadinessState.Deferred;
    }

    public static void ApplyEvaluation(CompetencySkillsReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.CompetencySkillsReadinessState = ArePreconditionsReady(
            entity.AssessmentWorkflowBoundaryState,
            entity.CompetencyFrameworkDependencyState,
            entity.SkillTaxonomyDependencyState,
            entity.SkillScoringBoundaryState,
            entity.RatingBoundaryState,
            entity.CalibrationBoundaryState,
            entity.RankingBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.ManagerAssessmentUxBoundaryState,
            entity.EmployeeAssessmentUxBoundaryState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates)
            ? CompetencySkillsReadinessState.Ready
            : CompetencySkillsReadinessState.Deferred;

        entity.DeferredReason = entity.CompetencySkillsReadinessState == CompetencySkillsReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Competency skills readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        CompetencySkillsReadinessState assessmentWorkflow,
        CompetencySkillsReadinessState competencyFrameworkDependency,
        CompetencySkillsReadinessState skillTaxonomyDependency,
        CompetencySkillsReadinessState skillScoring,
        CompetencySkillsReadinessState rating,
        CompetencySkillsReadinessState calibration,
        CompetencySkillsReadinessState ranking,
        CompetencySkillsReadinessState automatedDecision,
        CompetencySkillsReadinessState managerAssessmentUx,
        CompetencySkillsReadinessState employeeAssessmentUx,
        CompetencySkillsReadinessState documentDependency,
        CompetencySkillsReadinessState notificationDependency,
        CompetencySkillsReadinessState consent,
        CompetencySkillsReadinessState dataMinimization,
        CompetencySkillsReadinessState retention,
        CompetencySkillsReadinessState evidence,
        IReadOnlyDictionary<string, CompetencySkillsReadinessState> dependencyStates)
    {
        var required = new[]
        {
            assessmentWorkflow,
            competencyFrameworkDependency,
            skillTaxonomyDependency,
            skillScoring,
            rating,
            calibration,
            ranking,
            automatedDecision,
            managerAssessmentUx,
            employeeAssessmentUx,
            documentDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(CompetencySkillsReadinessState state) =>
        state is CompetencySkillsReadinessState.Ready or CompetencySkillsReadinessState.NotRequired;

    private static void ValidateState(CompetencySkillsReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(CompetencySkillsReadinessCreateRequest request)
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

    private static bool ContainsForbiddenMarker(string value)
    {
        return !string.IsNullOrEmpty(value) && ForbiddenMarkerRegex.IsMatch(value);
    }

    private static string Normalize(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        var index = 0;

        foreach (var current in value)
        {
            if (char.IsLetterOrDigit(current) || current == '_')
            {
                buffer[index++] = char.ToLowerInvariant(current);
            }
        }

        return new string(buffer[..index]);
    }
}
