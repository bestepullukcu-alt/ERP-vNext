using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.LearningTraining;

public static class LearningTrainingGuard
{
    public const string OwnerKey = "hcm.learning-training";
    public const string ReadPermission = "hcm.learning-training.read";
    public const string ManagePermission = "hcm.learning-training.manage";
    public const string EvaluatePermission = "hcm.learning-training.evaluate";
    public const string AuditReadPermission = "hcm.learning-training.audit.read";

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

    public static Response<Guid> RequireTenant(ITenantContext tenantContext) =>
        tenantContext.TenantId is { } tenantId && tenantId != Guid.Empty
            ? Response<Guid>.Success(tenantId)
            : Response<Guid>.Fail("Tenant context is required.", 401);

    public static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    public static IReadOnlyList<string> Validate(LearningTrainingReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.LearningTrainingReadinessVersion < 1)
        {
            errors.Add("LearningTrainingReadinessVersion must be greater than zero.");
        }

        ValidateState(request.LearningTrainingReadinessState, nameof(request.LearningTrainingReadinessState), errors);
        ValidateState(request.CourseCatalogBoundaryState, nameof(request.CourseCatalogBoundaryState), errors);
        ValidateState(request.EnrollmentWorkflowBoundaryState, nameof(request.EnrollmentWorkflowBoundaryState), errors);
        ValidateState(request.CompletionTrackingBoundaryState, nameof(request.CompletionTrackingBoundaryState), errors);
        ValidateState(request.CertificationBoundaryState, nameof(request.CertificationBoundaryState), errors);
        ValidateState(request.AssessmentScoringBoundaryState, nameof(request.AssessmentScoringBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.LearningContentDependencyState, nameof(request.LearningContentDependencyState), errors);
        ValidateState(request.SkillTaxonomyDependencyState, nameof(request.SkillTaxonomyDependencyState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.LearningTrainingReadinessState == LearningTrainingReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.CourseCatalogBoundaryState == LearningTrainingReadinessState.Ready)
        {
            errors.Add("Course catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.EnrollmentWorkflowBoundaryState == LearningTrainingReadinessState.Ready)
        {
            errors.Add("Enrollment workflow cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.CompletionTrackingBoundaryState == LearningTrainingReadinessState.Ready)
        {
            errors.Add("Completion tracking cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.CertificationBoundaryState == LearningTrainingReadinessState.Ready)
        {
            errors.Add("Certification behavior cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AssessmentScoringBoundaryState == LearningTrainingReadinessState.Ready)
        {
            errors.Add("Assessment scoring cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == LearningTrainingReadinessState.Ready)
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
            errors.Add("Learning training readiness metadata cannot contain assessment workflow bodies, scores, ratings, calibration outcomes, rankings, automated decision outputs, free-text assessment notes, appraisal narrative, attachments, document payloads, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static LearningTrainingReadinessState ResolveFailClosedReadinessState(LearningTrainingReadinessCreateRequest request)
    {
        if (request.LearningTrainingReadinessState != LearningTrainingReadinessState.Ready)
        {
            return request.LearningTrainingReadinessState;
        }

        return ArePreconditionsReady(
            request.CourseCatalogBoundaryState,
            request.EnrollmentWorkflowBoundaryState,
            request.CompletionTrackingBoundaryState,
            request.CertificationBoundaryState,
            request.AssessmentScoringBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.LearningContentDependencyState,
            request.SkillTaxonomyDependencyState,
            request.DocumentDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.DependencyStates)
            ? LearningTrainingReadinessState.Ready
            : LearningTrainingReadinessState.Deferred;
    }

    public static void ApplyEvaluation(LearningTrainingReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.LearningTrainingReadinessState = ArePreconditionsReady(
            entity.CourseCatalogBoundaryState,
            entity.EnrollmentWorkflowBoundaryState,
            entity.CompletionTrackingBoundaryState,
            entity.CertificationBoundaryState,
            entity.AssessmentScoringBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.LearningContentDependencyState,
            entity.SkillTaxonomyDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates)
            ? LearningTrainingReadinessState.Ready
            : LearningTrainingReadinessState.Deferred;

        entity.DeferredReason = entity.LearningTrainingReadinessState == LearningTrainingReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Learning training readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        LearningTrainingReadinessState courseCatalog,
        LearningTrainingReadinessState enrollmentWorkflow,
        LearningTrainingReadinessState completionTracking,
        LearningTrainingReadinessState certification,
        LearningTrainingReadinessState assessmentScoring,
        LearningTrainingReadinessState automatedDecision,
        LearningTrainingReadinessState learningContentDependency,
        LearningTrainingReadinessState skillTaxonomyDependency,
        LearningTrainingReadinessState documentDependency,
        LearningTrainingReadinessState notificationDependency,
        LearningTrainingReadinessState consent,
        LearningTrainingReadinessState dataMinimization,
        LearningTrainingReadinessState retention,
        LearningTrainingReadinessState evidence,
        IReadOnlyDictionary<string, LearningTrainingReadinessState> dependencyStates)
    {
        var required = new[]
        {
            courseCatalog,
            enrollmentWorkflow,
            completionTracking,
            certification,
            assessmentScoring,
            automatedDecision,
            learningContentDependency,
            skillTaxonomyDependency,
            documentDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(LearningTrainingReadinessState state) =>
        state is LearningTrainingReadinessState.Ready or LearningTrainingReadinessState.NotRequired;

    private static void ValidateState(LearningTrainingReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(LearningTrainingReadinessCreateRequest request)
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
        var normalized = Normalize(value);
        return ForbiddenMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal));
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
