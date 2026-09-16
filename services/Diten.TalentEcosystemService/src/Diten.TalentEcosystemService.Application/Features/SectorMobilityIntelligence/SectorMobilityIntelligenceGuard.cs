using System.Text.RegularExpressions;
using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence;

public static class SectorMobilityIntelligenceGuard
{
    public const string OwnerKey = "tep.sector-mobility-intelligence";
    public const string ReadPermission = "tep.sector-mobility-intelligence.read";
    public const string ManagePermission = "tep.sector-mobility-intelligence.manage";
    public const string EvaluatePermission = "tep.sector-mobility-intelligence.evaluate";
    public const string AuditReadPermission = "tep.sector-mobility-intelligence.audit.read";

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
    // "sector-mobility-intelligence" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(SectorMobilityIntelligenceReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.SectorMobilityIntelligenceReadinessVersion < 1)
        {
            errors.Add("SectorMobilityIntelligenceReadinessVersion must be greater than zero.");
        }

        ValidateState(request.SectorMobilityIntelligenceReadinessState, nameof(request.SectorMobilityIntelligenceReadinessState), errors);
        ValidateState(request.MobilityCatalogBoundaryState, nameof(request.MobilityCatalogBoundaryState), errors);
        ValidateState(request.FlowBindingIntakeBoundaryState, nameof(request.FlowBindingIntakeBoundaryState), errors);
        ValidateState(request.CorridorScopeBoundaryState, nameof(request.CorridorScopeBoundaryState), errors);
        ValidateState(request.VisibilityControlBoundaryState, nameof(request.VisibilityControlBoundaryState), errors);
        ValidateState(request.MobilityReviewBoundaryState, nameof(request.MobilityReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.SectorTrendSourceDependencyState, nameof(request.SectorTrendSourceDependencyState), errors);
        ValidateState(request.WorkforceAnalyticsSourceDependencyState, nameof(request.WorkforceAnalyticsSourceDependencyState), errors);
        ValidateState(request.SkillsTaxonomySourceDependencyState, nameof(request.SkillsTaxonomySourceDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.PublicationPolicyState, nameof(request.PublicationPolicyState), errors);

        if (request.SectorMobilityIntelligenceReadinessState == SectorMobilityIntelligenceReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.MobilityCatalogBoundaryState == SectorMobilityIntelligenceReadinessState.Ready)
        {
            errors.Add("Mobility catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.FlowBindingIntakeBoundaryState == SectorMobilityIntelligenceReadinessState.Ready)
        {
            errors.Add("Flow binding intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.CorridorScopeBoundaryState == SectorMobilityIntelligenceReadinessState.Ready)
        {
            errors.Add("Corridor scope cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.VisibilityControlBoundaryState == SectorMobilityIntelligenceReadinessState.Ready)
        {
            errors.Add("Visibility control cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.MobilityReviewBoundaryState == SectorMobilityIntelligenceReadinessState.Ready)
        {
            errors.Add("Mobility review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == SectorMobilityIntelligenceReadinessState.Ready)
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
            errors.Add("Sector mobility intelligence readiness metadata cannot contain real mobility scores or flow output values, corridor/transition cell values or distributions, inflow/outflow quantities or rates, per-individual or per-role mobility data, query results, individual/participant PII or contact details, workforce or company rosters, free-text notes, narrative, attachments, document payloads, ratings, scores, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static SectorMobilityIntelligenceReadinessState ResolveFailClosedReadinessState(SectorMobilityIntelligenceReadinessCreateRequest request)
    {
        if (request.SectorMobilityIntelligenceReadinessState != SectorMobilityIntelligenceReadinessState.Ready)
        {
            return request.SectorMobilityIntelligenceReadinessState;
        }

        return ArePreconditionsReady(
            request.MobilityCatalogBoundaryState,
            request.FlowBindingIntakeBoundaryState,
            request.CorridorScopeBoundaryState,
            request.VisibilityControlBoundaryState,
            request.MobilityReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.SectorTrendSourceDependencyState,
            request.WorkforceAnalyticsSourceDependencyState,
            request.SkillsTaxonomySourceDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.PublicationPolicyState,
            request.DependencyStates)
            ? SectorMobilityIntelligenceReadinessState.Ready
            : SectorMobilityIntelligenceReadinessState.Deferred;
    }

    public static void ApplyEvaluation(SectorMobilityIntelligenceReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.SectorMobilityIntelligenceReadinessState = ArePreconditionsReady(
            entity.MobilityCatalogBoundaryState,
            entity.FlowBindingIntakeBoundaryState,
            entity.CorridorScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.MobilityReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SectorTrendSourceDependencyState,
            entity.WorkforceAnalyticsSourceDependencyState,
            entity.SkillsTaxonomySourceDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates)
            ? SectorMobilityIntelligenceReadinessState.Ready
            : SectorMobilityIntelligenceReadinessState.Deferred;

        entity.DeferredReason = entity.SectorMobilityIntelligenceReadinessState == SectorMobilityIntelligenceReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Sector mobility intelligence readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        SectorMobilityIntelligenceReadinessState mobilityCatalog,
        SectorMobilityIntelligenceReadinessState flowBindingIntake,
        SectorMobilityIntelligenceReadinessState corridorScope,
        SectorMobilityIntelligenceReadinessState visibilityControl,
        SectorMobilityIntelligenceReadinessState mobilityReview,
        SectorMobilityIntelligenceReadinessState automatedDecision,
        SectorMobilityIntelligenceReadinessState sectorTrendSourceDependency,
        SectorMobilityIntelligenceReadinessState workforceAnalyticsSourceDependency,
        SectorMobilityIntelligenceReadinessState skillsTaxonomySourceDependency,
        SectorMobilityIntelligenceReadinessState notificationDependency,
        SectorMobilityIntelligenceReadinessState consent,
        SectorMobilityIntelligenceReadinessState dataMinimization,
        SectorMobilityIntelligenceReadinessState retention,
        SectorMobilityIntelligenceReadinessState evidence,
        IReadOnlyDictionary<string, SectorMobilityIntelligenceReadinessState> dependencyStates)
    {
        var required = new[]
        {
            mobilityCatalog,
            flowBindingIntake,
            corridorScope,
            visibilityControl,
            mobilityReview,
            automatedDecision,
            sectorTrendSourceDependency,
            workforceAnalyticsSourceDependency,
            skillsTaxonomySourceDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(SectorMobilityIntelligenceReadinessState state) =>
        state is SectorMobilityIntelligenceReadinessState.Ready or SectorMobilityIntelligenceReadinessState.NotRequired;

    private static void ValidateState(SectorMobilityIntelligenceReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(SectorMobilityIntelligenceReadinessCreateRequest request)
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
