using System.Text.RegularExpressions;
using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Application.Features.BaselineExperimentMeasurement;

public static class BaselineExperimentMeasurementGuard
{
    public const string OwnerKey = "dki.baseline-experiment-measurement";
    public const string ReadPermission = "dki.baseline-experiment-measurement.read";
    public const string ManagePermission = "dki.baseline-experiment-measurement.manage";
    public const string EvaluatePermission = "dki.baseline-experiment-measurement.evaluate";
    public const string AuditReadPermission = "dki.baseline-experiment-measurement.audit.read";

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
    // "baseline-experiment-measurement" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(BaselineExperimentMeasurementReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.BaselineExperimentMeasurementReadinessVersion < 1)
        {
            errors.Add("BaselineExperimentMeasurementReadinessVersion must be greater than zero.");
        }

        ValidateState(request.BaselineExperimentMeasurementReadinessState, nameof(request.BaselineExperimentMeasurementReadinessState), errors);
        ValidateState(request.BaselineCatalogBoundaryState, nameof(request.BaselineCatalogBoundaryState), errors);
        ValidateState(request.ExperimentDesignIntakeBoundaryState, nameof(request.ExperimentDesignIntakeBoundaryState), errors);
        ValidateState(request.MeasurementBindingScopeBoundaryState, nameof(request.MeasurementBindingScopeBoundaryState), errors);
        ValidateState(request.ResultPublicationControlBoundaryState, nameof(request.ResultPublicationControlBoundaryState), errors);
        ValidateState(request.ExperimentReviewBoundaryState, nameof(request.ExperimentReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.MetricSemanticRegistrySourceDependencyState, nameof(request.MetricSemanticRegistrySourceDependencyState), errors);
        ValidateState(request.ScorecardSourceDependencyState, nameof(request.ScorecardSourceDependencyState), errors);
        ValidateState(request.DataContractRegistryDependencyState, nameof(request.DataContractRegistryDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.StewardshipPreconditionState, nameof(request.StewardshipPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.MeasurementPolicyState, nameof(request.MeasurementPolicyState), errors);

        if (request.BaselineExperimentMeasurementReadinessState == BaselineExperimentMeasurementReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.BaselineCatalogBoundaryState == BaselineExperimentMeasurementReadinessState.Ready)
        {
            errors.Add("Baseline catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ExperimentDesignIntakeBoundaryState == BaselineExperimentMeasurementReadinessState.Ready)
        {
            errors.Add("Experiment design intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.MeasurementBindingScopeBoundaryState == BaselineExperimentMeasurementReadinessState.Ready)
        {
            errors.Add("Measurement binding scope cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ResultPublicationControlBoundaryState == BaselineExperimentMeasurementReadinessState.Ready)
        {
            errors.Add("Result publication control cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ExperimentReviewBoundaryState == BaselineExperimentMeasurementReadinessState.Ready)
        {
            errors.Add("Experiment review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == BaselineExperimentMeasurementReadinessState.Ready)
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
            errors.Add("Baseline and experiment measurement readiness metadata cannot contain measurement results or metric values, baseline/experiment sample or observation rows, statistical values (p-values, effect sizes, confidence intervals), variant assignments, raw event or telemetry data, subject/participant PII or personal contact details, free-text notes, narrative, attachments, document payloads, ratings, scores, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static BaselineExperimentMeasurementReadinessState ResolveFailClosedReadinessState(BaselineExperimentMeasurementReadinessCreateRequest request)
    {
        if (request.BaselineExperimentMeasurementReadinessState != BaselineExperimentMeasurementReadinessState.Ready)
        {
            return request.BaselineExperimentMeasurementReadinessState;
        }

        return ArePreconditionsReady(
            request.BaselineCatalogBoundaryState,
            request.ExperimentDesignIntakeBoundaryState,
            request.MeasurementBindingScopeBoundaryState,
            request.ResultPublicationControlBoundaryState,
            request.ExperimentReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.MetricSemanticRegistrySourceDependencyState,
            request.ScorecardSourceDependencyState,
            request.DataContractRegistryDependencyState,
            request.NotificationDependencyState,
            request.StewardshipPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.MeasurementPolicyState,
            request.DependencyStates)
            ? BaselineExperimentMeasurementReadinessState.Ready
            : BaselineExperimentMeasurementReadinessState.Deferred;
    }

    public static void ApplyEvaluation(BaselineExperimentMeasurementReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.BaselineExperimentMeasurementReadinessState = ArePreconditionsReady(
            entity.BaselineCatalogBoundaryState,
            entity.ExperimentDesignIntakeBoundaryState,
            entity.MeasurementBindingScopeBoundaryState,
            entity.ResultPublicationControlBoundaryState,
            entity.ExperimentReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.MetricSemanticRegistrySourceDependencyState,
            entity.ScorecardSourceDependencyState,
            entity.DataContractRegistryDependencyState,
            entity.NotificationDependencyState,
            entity.StewardshipPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.MeasurementPolicyState,
            entity.DependencyStates)
            ? BaselineExperimentMeasurementReadinessState.Ready
            : BaselineExperimentMeasurementReadinessState.Deferred;

        entity.DeferredReason = entity.BaselineExperimentMeasurementReadinessState == BaselineExperimentMeasurementReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Baseline and experiment measurement readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        BaselineExperimentMeasurementReadinessState baselineCatalog,
        BaselineExperimentMeasurementReadinessState experimentDesignIntake,
        BaselineExperimentMeasurementReadinessState measurementBindingScope,
        BaselineExperimentMeasurementReadinessState resultPublicationControl,
        BaselineExperimentMeasurementReadinessState experimentReview,
        BaselineExperimentMeasurementReadinessState automatedDecision,
        BaselineExperimentMeasurementReadinessState metricSemanticRegistrySourceDependency,
        BaselineExperimentMeasurementReadinessState scorecardSourceDependency,
        BaselineExperimentMeasurementReadinessState dataContractRegistryDependency,
        BaselineExperimentMeasurementReadinessState notificationDependency,
        BaselineExperimentMeasurementReadinessState consent,
        BaselineExperimentMeasurementReadinessState dataMinimization,
        BaselineExperimentMeasurementReadinessState retention,
        BaselineExperimentMeasurementReadinessState evidence,
        IReadOnlyDictionary<string, BaselineExperimentMeasurementReadinessState> dependencyStates)
    {
        var required = new[]
        {
            baselineCatalog,
            experimentDesignIntake,
            measurementBindingScope,
            resultPublicationControl,
            experimentReview,
            automatedDecision,
            metricSemanticRegistrySourceDependency,
            scorecardSourceDependency,
            dataContractRegistryDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(BaselineExperimentMeasurementReadinessState state) =>
        state is BaselineExperimentMeasurementReadinessState.Ready or BaselineExperimentMeasurementReadinessState.NotRequired;

    private static void ValidateState(BaselineExperimentMeasurementReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(BaselineExperimentMeasurementReadinessCreateRequest request)
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
