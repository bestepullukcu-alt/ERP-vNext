using System.Text.RegularExpressions;
using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators;

public static class HiringRiskIndicatorsGuard
{
    public const string OwnerKey = "tep.hiring-risk-indicators";
    public const string ReadPermission = "tep.hiring-risk-indicators.read";
    public const string ManagePermission = "tep.hiring-risk-indicators.manage";
    public const string EvaluatePermission = "tep.hiring-risk-indicators.evaluate";
    public const string AuditReadPermission = "tep.hiring-risk-indicators.audit.read";

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
    // "hiring-risk-indicators" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(HiringRiskIndicatorsReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.HiringRiskIndicatorsReadinessVersion < 1)
        {
            errors.Add("HiringRiskIndicatorsReadinessVersion must be greater than zero.");
        }

        ValidateState(request.HiringRiskIndicatorsReadinessState, nameof(request.HiringRiskIndicatorsReadinessState), errors);
        ValidateState(request.RiskIndicatorCatalogBoundaryState, nameof(request.RiskIndicatorCatalogBoundaryState), errors);
        ValidateState(request.RiskSignalIntakeBoundaryState, nameof(request.RiskSignalIntakeBoundaryState), errors);
        ValidateState(request.RiskAssessmentBoundaryState, nameof(request.RiskAssessmentBoundaryState), errors);
        ValidateState(request.MitigationTrackingBoundaryState, nameof(request.MitigationTrackingBoundaryState), errors);
        ValidateState(request.IndicatorReviewBoundaryState, nameof(request.IndicatorReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.TalentDataSourceDependencyState, nameof(request.TalentDataSourceDependencyState), errors);
        ValidateState(request.ConsentPolicyDependencyState, nameof(request.ConsentPolicyDependencyState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.HiringRiskIndicatorsReadinessState == HiringRiskIndicatorsReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.RiskIndicatorCatalogBoundaryState == HiringRiskIndicatorsReadinessState.Ready)
        {
            errors.Add("Risk indicator catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.RiskSignalIntakeBoundaryState == HiringRiskIndicatorsReadinessState.Ready)
        {
            errors.Add("Risk signal intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.RiskAssessmentBoundaryState == HiringRiskIndicatorsReadinessState.Ready)
        {
            errors.Add("Risk assessment cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.MitigationTrackingBoundaryState == HiringRiskIndicatorsReadinessState.Ready)
        {
            errors.Add("Mitigation tracking cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.IndicatorReviewBoundaryState == HiringRiskIndicatorsReadinessState.Ready)
        {
            errors.Add("Indicator review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == HiringRiskIndicatorsReadinessState.Ready)
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
            errors.Add("Hiring risk indicators readiness metadata cannot contain risk scores, computed risk ratings, candidate/applicant PII, background-check or credit content, adverse-action decisions, raw signal payloads, free-text notes, narrative, attachments, document payloads, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static HiringRiskIndicatorsReadinessState ResolveFailClosedReadinessState(HiringRiskIndicatorsReadinessCreateRequest request)
    {
        if (request.HiringRiskIndicatorsReadinessState != HiringRiskIndicatorsReadinessState.Ready)
        {
            return request.HiringRiskIndicatorsReadinessState;
        }

        return ArePreconditionsReady(
            request.RiskIndicatorCatalogBoundaryState,
            request.RiskSignalIntakeBoundaryState,
            request.RiskAssessmentBoundaryState,
            request.MitigationTrackingBoundaryState,
            request.IndicatorReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.TalentDataSourceDependencyState,
            request.ConsentPolicyDependencyState,
            request.DocumentDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.DependencyStates)
            ? HiringRiskIndicatorsReadinessState.Ready
            : HiringRiskIndicatorsReadinessState.Deferred;
    }

    public static void ApplyEvaluation(HiringRiskIndicatorsReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.HiringRiskIndicatorsReadinessState = ArePreconditionsReady(
            entity.RiskIndicatorCatalogBoundaryState,
            entity.RiskSignalIntakeBoundaryState,
            entity.RiskAssessmentBoundaryState,
            entity.MitigationTrackingBoundaryState,
            entity.IndicatorReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates)
            ? HiringRiskIndicatorsReadinessState.Ready
            : HiringRiskIndicatorsReadinessState.Deferred;

        entity.DeferredReason = entity.HiringRiskIndicatorsReadinessState == HiringRiskIndicatorsReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Hiring risk indicators readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        HiringRiskIndicatorsReadinessState riskIndicatorCatalog,
        HiringRiskIndicatorsReadinessState riskSignalIntake,
        HiringRiskIndicatorsReadinessState riskAssessment,
        HiringRiskIndicatorsReadinessState mitigationTracking,
        HiringRiskIndicatorsReadinessState indicatorReview,
        HiringRiskIndicatorsReadinessState automatedDecision,
        HiringRiskIndicatorsReadinessState talentDataSourceDependency,
        HiringRiskIndicatorsReadinessState consentPolicyDependency,
        HiringRiskIndicatorsReadinessState documentDependency,
        HiringRiskIndicatorsReadinessState notificationDependency,
        HiringRiskIndicatorsReadinessState consent,
        HiringRiskIndicatorsReadinessState dataMinimization,
        HiringRiskIndicatorsReadinessState retention,
        HiringRiskIndicatorsReadinessState evidence,
        IReadOnlyDictionary<string, HiringRiskIndicatorsReadinessState> dependencyStates)
    {
        var required = new[]
        {
            riskIndicatorCatalog,
            riskSignalIntake,
            riskAssessment,
            mitigationTracking,
            indicatorReview,
            automatedDecision,
            talentDataSourceDependency,
            consentPolicyDependency,
            documentDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(HiringRiskIndicatorsReadinessState state) =>
        state is HiringRiskIndicatorsReadinessState.Ready or HiringRiskIndicatorsReadinessState.NotRequired;

    private static void ValidateState(HiringRiskIndicatorsReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(HiringRiskIndicatorsReadinessCreateRequest request)
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
