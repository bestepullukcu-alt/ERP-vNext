using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.DevelopmentPlans;

public static class DevelopmentPlanGuard
{
    public const string OwnerKey = "hcm.development-plans";
    public const string ReadPermission = "hcm.development-plans.read";
    public const string ManagePermission = "hcm.development-plans.manage";
    public const string EvaluatePermission = "hcm.development-plans.evaluate";
    public const string AuditReadPermission = "hcm.development-plans.audit.read";

    private static readonly string[] ForbiddenMarkers =
    [
        "workflowbody",
        "workflow_body",
        "planbody",
        "plan_body",
        "developmentnotes",
        "development_notes",
        "goalassignmentpayload",
        "goal_assignment_payload",
        "learningassignmentpayload",
        "learning_assignment_payload",
        "coachingnarrative",
        "coaching_narrative",
        "coachingpayload",
        "coaching_payload",
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
        "skillGapScoring",
        "skillgapscoring",
        "skill_gap_scoring",
        "rating",
        "rank",
        "ranking",
        "recommendation",
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

    public static IReadOnlyList<string> Validate(DevelopmentPlanReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.DevelopmentPlanReadinessVersion < 1)
        {
            errors.Add("DevelopmentPlanReadinessVersion must be greater than zero.");
        }

        ValidateState(request.DevelopmentPlanReadinessState, nameof(request.DevelopmentPlanReadinessState), errors);
        ValidateState(request.DevelopmentPlanWorkflowBoundaryState, nameof(request.DevelopmentPlanWorkflowBoundaryState), errors);
        ValidateState(request.GoalAssignmentBoundaryState, nameof(request.GoalAssignmentBoundaryState), errors);
        ValidateState(request.LearningAssignmentBoundaryState, nameof(request.LearningAssignmentBoundaryState), errors);
        ValidateState(request.SkillGapScoringBoundaryState, nameof(request.SkillGapScoringBoundaryState), errors);
        ValidateState(request.RatingBoundaryState, nameof(request.RatingBoundaryState), errors);
        ValidateState(request.RecommendationBoundaryState, nameof(request.RecommendationBoundaryState), errors);
        ValidateState(request.RankingBoundaryState, nameof(request.RankingBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.ManagerActionUxBoundaryState, nameof(request.ManagerActionUxBoundaryState), errors);
        ValidateState(request.CoachingActionBoundaryState, nameof(request.CoachingActionBoundaryState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.DevelopmentPlanReadinessState == DevelopmentPlanReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.DevelopmentPlanWorkflowBoundaryState == DevelopmentPlanReadinessState.Ready)
        {
            errors.Add("Development plan workflow cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.GoalAssignmentBoundaryState == DevelopmentPlanReadinessState.Ready)
        {
            errors.Add("Goal assignment execution cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.LearningAssignmentBoundaryState == DevelopmentPlanReadinessState.Ready)
        {
            errors.Add("Learning assignment execution cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.SkillGapScoringBoundaryState == DevelopmentPlanReadinessState.Ready)
        {
            errors.Add("Skill gap scoring cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.RatingBoundaryState == DevelopmentPlanReadinessState.Ready)
        {
            errors.Add("Rating behavior cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.RecommendationBoundaryState == DevelopmentPlanReadinessState.Ready)
        {
            errors.Add("Recommendation behavior cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.RankingBoundaryState == DevelopmentPlanReadinessState.Ready)
        {
            errors.Add("Ranking behavior cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == DevelopmentPlanReadinessState.Ready)
        {
            errors.Add("Automated decision behavior cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ManagerActionUxBoundaryState == DevelopmentPlanReadinessState.Ready)
        {
            errors.Add("Manager action UX cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.CoachingActionBoundaryState == DevelopmentPlanReadinessState.Ready)
        {
            errors.Add("Coaching action cannot be marked Ready in the first metadata-only slice.");
        }

        foreach (var pair in request.DependencyStates)
        {
            RequireText(pair.Key, nameof(request.DependencyStates), 80, errors);
            ValidateState(pair.Value, $"{nameof(request.DependencyStates)}.{pair.Key}", errors);
        }

        if (ForbiddenValues(request).Any(ContainsForbiddenMarker))
        {
            errors.Add("Development plan readiness metadata cannot contain workflow bodies, goal or learning assignment payloads, coaching action payloads, scores, ratings, recommendation outcomes, rankings, automated decision outputs, free-text development notes, coaching narrative, attachments, document payloads, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static DevelopmentPlanReadinessState ResolveFailClosedReadinessState(DevelopmentPlanReadinessCreateRequest request)
    {
        if (request.DevelopmentPlanReadinessState != DevelopmentPlanReadinessState.Ready)
        {
            return request.DevelopmentPlanReadinessState;
        }

        return ArePreconditionsReady(
            request.DevelopmentPlanWorkflowBoundaryState,
            request.GoalAssignmentBoundaryState,
            request.LearningAssignmentBoundaryState,
            request.SkillGapScoringBoundaryState,
            request.RatingBoundaryState,
            request.RecommendationBoundaryState,
            request.RankingBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.ManagerActionUxBoundaryState,
            request.CoachingActionBoundaryState,
            request.DocumentDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.DependencyStates)
            ? DevelopmentPlanReadinessState.Ready
            : DevelopmentPlanReadinessState.Deferred;
    }

    public static void ApplyEvaluation(DevelopmentPlanReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.DevelopmentPlanReadinessState = ArePreconditionsReady(
            entity.DevelopmentPlanWorkflowBoundaryState,
            entity.GoalAssignmentBoundaryState,
            entity.LearningAssignmentBoundaryState,
            entity.SkillGapScoringBoundaryState,
            entity.RatingBoundaryState,
            entity.RecommendationBoundaryState,
            entity.RankingBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.ManagerActionUxBoundaryState,
            entity.CoachingActionBoundaryState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates)
            ? DevelopmentPlanReadinessState.Ready
            : DevelopmentPlanReadinessState.Deferred;

        entity.DeferredReason = entity.DevelopmentPlanReadinessState == DevelopmentPlanReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Development plan readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        DevelopmentPlanReadinessState developmentPlanWorkflow,
        DevelopmentPlanReadinessState goalAssignment,
        DevelopmentPlanReadinessState learningAssignment,
        DevelopmentPlanReadinessState skillGapScoring,
        DevelopmentPlanReadinessState rating,
        DevelopmentPlanReadinessState recommendation,
        DevelopmentPlanReadinessState ranking,
        DevelopmentPlanReadinessState automatedDecision,
        DevelopmentPlanReadinessState managerActionUx,
        DevelopmentPlanReadinessState coachingAction,
        DevelopmentPlanReadinessState documentDependency,
        DevelopmentPlanReadinessState notificationDependency,
        DevelopmentPlanReadinessState consent,
        DevelopmentPlanReadinessState dataMinimization,
        DevelopmentPlanReadinessState retention,
        DevelopmentPlanReadinessState evidence,
        IReadOnlyDictionary<string, DevelopmentPlanReadinessState> dependencyStates)
    {
        var required = new[]
        {
            developmentPlanWorkflow,
            goalAssignment,
            learningAssignment,
            skillGapScoring,
            rating,
            recommendation,
            ranking,
            automatedDecision,
            managerActionUx,
            coachingAction,
            documentDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(DevelopmentPlanReadinessState state) =>
        state is DevelopmentPlanReadinessState.Ready or DevelopmentPlanReadinessState.NotRequired;

    private static void ValidateState(DevelopmentPlanReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(DevelopmentPlanReadinessCreateRequest request)
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
