using System.Text.RegularExpressions;
using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.SkillsGapHeatmap;

public static class SkillsGapHeatmapGuard
{
    public const string OwnerKey = "tep.skills-gap-heatmap";
    public const string ReadPermission = "tep.skills-gap-heatmap.read";
    public const string ManagePermission = "tep.skills-gap-heatmap.manage";
    public const string EvaluatePermission = "tep.skills-gap-heatmap.evaluate";
    public const string AuditReadPermission = "tep.skills-gap-heatmap.audit.read";

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
    // "skills-gap-heatmap" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(SkillsGapHeatmapReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.SkillsGapHeatmapReadinessVersion < 1)
        {
            errors.Add("SkillsGapHeatmapReadinessVersion must be greater than zero.");
        }

        ValidateState(request.SkillsGapHeatmapReadinessState, nameof(request.SkillsGapHeatmapReadinessState), errors);
        ValidateState(request.GapCatalogBoundaryState, nameof(request.GapCatalogBoundaryState), errors);
        ValidateState(request.HeatmapBindingIntakeBoundaryState, nameof(request.HeatmapBindingIntakeBoundaryState), errors);
        ValidateState(request.SeverityScopeBoundaryState, nameof(request.SeverityScopeBoundaryState), errors);
        ValidateState(request.VisibilityControlBoundaryState, nameof(request.VisibilityControlBoundaryState), errors);
        ValidateState(request.GapReviewBoundaryState, nameof(request.GapReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.SkillsTaxonomySourceDependencyState, nameof(request.SkillsTaxonomySourceDependencyState), errors);
        ValidateState(request.WorkforceAnalyticsSourceDependencyState, nameof(request.WorkforceAnalyticsSourceDependencyState), errors);
        ValidateState(request.TalentDemandForecastSourceDependencyState, nameof(request.TalentDemandForecastSourceDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.PublicationPolicyState, nameof(request.PublicationPolicyState), errors);

        if (request.SkillsGapHeatmapReadinessState == SkillsGapHeatmapReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.GapCatalogBoundaryState == SkillsGapHeatmapReadinessState.Ready)
        {
            errors.Add("Gap catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.HeatmapBindingIntakeBoundaryState == SkillsGapHeatmapReadinessState.Ready)
        {
            errors.Add("Heatmap binding intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.SeverityScopeBoundaryState == SkillsGapHeatmapReadinessState.Ready)
        {
            errors.Add("Severity scope cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.VisibilityControlBoundaryState == SkillsGapHeatmapReadinessState.Ready)
        {
            errors.Add("Visibility control cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.GapReviewBoundaryState == SkillsGapHeatmapReadinessState.Ready)
        {
            errors.Add("Gap review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == SkillsGapHeatmapReadinessState.Ready)
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
            errors.Add("Skills gap heatmap readiness metadata cannot contain real gap scores or heatmap output values, severity/intensity cell values or distributions, skills coverage or shortfall quantities, per-individual or per-role gap data, query results, individual/participant PII or contact details, workforce or company rosters, free-text notes, narrative, attachments, document payloads, ratings, scores, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static SkillsGapHeatmapReadinessState ResolveFailClosedReadinessState(SkillsGapHeatmapReadinessCreateRequest request)
    {
        if (request.SkillsGapHeatmapReadinessState != SkillsGapHeatmapReadinessState.Ready)
        {
            return request.SkillsGapHeatmapReadinessState;
        }

        return ArePreconditionsReady(
            request.GapCatalogBoundaryState,
            request.HeatmapBindingIntakeBoundaryState,
            request.SeverityScopeBoundaryState,
            request.VisibilityControlBoundaryState,
            request.GapReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.SkillsTaxonomySourceDependencyState,
            request.WorkforceAnalyticsSourceDependencyState,
            request.TalentDemandForecastSourceDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.PublicationPolicyState,
            request.DependencyStates)
            ? SkillsGapHeatmapReadinessState.Ready
            : SkillsGapHeatmapReadinessState.Deferred;
    }

    public static void ApplyEvaluation(SkillsGapHeatmapReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.SkillsGapHeatmapReadinessState = ArePreconditionsReady(
            entity.GapCatalogBoundaryState,
            entity.HeatmapBindingIntakeBoundaryState,
            entity.SeverityScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.GapReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SkillsTaxonomySourceDependencyState,
            entity.WorkforceAnalyticsSourceDependencyState,
            entity.TalentDemandForecastSourceDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates)
            ? SkillsGapHeatmapReadinessState.Ready
            : SkillsGapHeatmapReadinessState.Deferred;

        entity.DeferredReason = entity.SkillsGapHeatmapReadinessState == SkillsGapHeatmapReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Skills gap heatmap readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        SkillsGapHeatmapReadinessState gapCatalog,
        SkillsGapHeatmapReadinessState heatmapBindingIntake,
        SkillsGapHeatmapReadinessState severityScope,
        SkillsGapHeatmapReadinessState visibilityControl,
        SkillsGapHeatmapReadinessState gapReview,
        SkillsGapHeatmapReadinessState automatedDecision,
        SkillsGapHeatmapReadinessState skillsTaxonomySourceDependency,
        SkillsGapHeatmapReadinessState workforceAnalyticsSourceDependency,
        SkillsGapHeatmapReadinessState talentDemandForecastSourceDependency,
        SkillsGapHeatmapReadinessState notificationDependency,
        SkillsGapHeatmapReadinessState consent,
        SkillsGapHeatmapReadinessState dataMinimization,
        SkillsGapHeatmapReadinessState retention,
        SkillsGapHeatmapReadinessState evidence,
        IReadOnlyDictionary<string, SkillsGapHeatmapReadinessState> dependencyStates)
    {
        var required = new[]
        {
            gapCatalog,
            heatmapBindingIntake,
            severityScope,
            visibilityControl,
            gapReview,
            automatedDecision,
            skillsTaxonomySourceDependency,
            workforceAnalyticsSourceDependency,
            talentDemandForecastSourceDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(SkillsGapHeatmapReadinessState state) =>
        state is SkillsGapHeatmapReadinessState.Ready or SkillsGapHeatmapReadinessState.NotRequired;

    private static void ValidateState(SkillsGapHeatmapReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(SkillsGapHeatmapReadinessCreateRequest request)
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
