using System.Text.RegularExpressions;
using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.EarlyWarningSignals;

public static class EarlyWarningSignalsGuard
{
    public const string OwnerKey = "tep.early-warning-signals";
    public const string ReadPermission = "tep.early-warning-signals.read";
    public const string ManagePermission = "tep.early-warning-signals.manage";
    public const string EvaluatePermission = "tep.early-warning-signals.evaluate";
    public const string AuditReadPermission = "tep.early-warning-signals.audit.read";

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
    // "early-warning-signals" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(EarlyWarningSignalsReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.EarlyWarningSignalsReadinessVersion < 1)
        {
            errors.Add("EarlyWarningSignalsReadinessVersion must be greater than zero.");
        }

        ValidateState(request.EarlyWarningSignalsReadinessState, nameof(request.EarlyWarningSignalsReadinessState), errors);
        ValidateState(request.SignalCatalogBoundaryState, nameof(request.SignalCatalogBoundaryState), errors);
        ValidateState(request.PatternDetectionBoundaryState, nameof(request.PatternDetectionBoundaryState), errors);
        ValidateState(request.CrossCompanyCorrelationBoundaryState, nameof(request.CrossCompanyCorrelationBoundaryState), errors);
        ValidateState(request.AlertRoutingBoundaryState, nameof(request.AlertRoutingBoundaryState), errors);
        ValidateState(request.SignalReviewBoundaryState, nameof(request.SignalReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.TalentDataSourceDependencyState, nameof(request.TalentDataSourceDependencyState), errors);
        ValidateState(request.RiskIndicatorSourceDependencyState, nameof(request.RiskIndicatorSourceDependencyState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.EarlyWarningSignalsReadinessState == EarlyWarningSignalsReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.SignalCatalogBoundaryState == EarlyWarningSignalsReadinessState.Ready)
        {
            errors.Add("Signal catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.PatternDetectionBoundaryState == EarlyWarningSignalsReadinessState.Ready)
        {
            errors.Add("Pattern detection cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.CrossCompanyCorrelationBoundaryState == EarlyWarningSignalsReadinessState.Ready)
        {
            errors.Add("Cross-company correlation cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AlertRoutingBoundaryState == EarlyWarningSignalsReadinessState.Ready)
        {
            errors.Add("Alert routing cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.SignalReviewBoundaryState == EarlyWarningSignalsReadinessState.Ready)
        {
            errors.Add("Signal review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == EarlyWarningSignalsReadinessState.Ready)
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
            errors.Add("Early warning signals readiness metadata cannot contain signal scores, computed risk ratings, cross-company alert content, candidate/applicant PII, raw signal or pattern payloads, individual attributions, free-text notes, narrative, attachments, document payloads, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static EarlyWarningSignalsReadinessState ResolveFailClosedReadinessState(EarlyWarningSignalsReadinessCreateRequest request)
    {
        if (request.EarlyWarningSignalsReadinessState != EarlyWarningSignalsReadinessState.Ready)
        {
            return request.EarlyWarningSignalsReadinessState;
        }

        return ArePreconditionsReady(
            request.SignalCatalogBoundaryState,
            request.PatternDetectionBoundaryState,
            request.CrossCompanyCorrelationBoundaryState,
            request.AlertRoutingBoundaryState,
            request.SignalReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.TalentDataSourceDependencyState,
            request.RiskIndicatorSourceDependencyState,
            request.DocumentDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.DependencyStates)
            ? EarlyWarningSignalsReadinessState.Ready
            : EarlyWarningSignalsReadinessState.Deferred;
    }

    public static void ApplyEvaluation(EarlyWarningSignalsReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.EarlyWarningSignalsReadinessState = ArePreconditionsReady(
            entity.SignalCatalogBoundaryState,
            entity.PatternDetectionBoundaryState,
            entity.CrossCompanyCorrelationBoundaryState,
            entity.AlertRoutingBoundaryState,
            entity.SignalReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.RiskIndicatorSourceDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates)
            ? EarlyWarningSignalsReadinessState.Ready
            : EarlyWarningSignalsReadinessState.Deferred;

        entity.DeferredReason = entity.EarlyWarningSignalsReadinessState == EarlyWarningSignalsReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Early warning signals readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        EarlyWarningSignalsReadinessState signalCatalog,
        EarlyWarningSignalsReadinessState patternDetection,
        EarlyWarningSignalsReadinessState crossCompanyCorrelation,
        EarlyWarningSignalsReadinessState alertRouting,
        EarlyWarningSignalsReadinessState signalReview,
        EarlyWarningSignalsReadinessState automatedDecision,
        EarlyWarningSignalsReadinessState talentDataSourceDependency,
        EarlyWarningSignalsReadinessState riskIndicatorSourceDependency,
        EarlyWarningSignalsReadinessState documentDependency,
        EarlyWarningSignalsReadinessState notificationDependency,
        EarlyWarningSignalsReadinessState consent,
        EarlyWarningSignalsReadinessState dataMinimization,
        EarlyWarningSignalsReadinessState retention,
        EarlyWarningSignalsReadinessState evidence,
        IReadOnlyDictionary<string, EarlyWarningSignalsReadinessState> dependencyStates)
    {
        var required = new[]
        {
            signalCatalog,
            patternDetection,
            crossCompanyCorrelation,
            alertRouting,
            signalReview,
            automatedDecision,
            talentDataSourceDependency,
            riskIndicatorSourceDependency,
            documentDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(EarlyWarningSignalsReadinessState state) =>
        state is EarlyWarningSignalsReadinessState.Ready or EarlyWarningSignalsReadinessState.NotRequired;

    private static void ValidateState(EarlyWarningSignalsReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(EarlyWarningSignalsReadinessCreateRequest request)
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
