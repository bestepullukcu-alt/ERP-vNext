using System.Text.RegularExpressions;
using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry;

public static class MetricSemanticRegistryGuard
{
    public const string OwnerKey = "dki.metric-semantic-registry";
    public const string ReadPermission = "dki.metric-semantic-registry.read";
    public const string ManagePermission = "dki.metric-semantic-registry.manage";
    public const string EvaluatePermission = "dki.metric-semantic-registry.evaluate";
    public const string AuditReadPermission = "dki.metric-semantic-registry.audit.read";

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
    // "metric-semantic-registry" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(MetricSemanticRegistryReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.MetricSemanticRegistryReadinessVersion < 1)
        {
            errors.Add("MetricSemanticRegistryReadinessVersion must be greater than zero.");
        }

        ValidateState(request.MetricSemanticRegistryReadinessState, nameof(request.MetricSemanticRegistryReadinessState), errors);
        ValidateState(request.MetricIdentityCatalogBoundaryState, nameof(request.MetricIdentityCatalogBoundaryState), errors);
        ValidateState(request.SemanticEntityIntakeBoundaryState, nameof(request.SemanticEntityIntakeBoundaryState), errors);
        ValidateState(request.DimensionMeasureScopeBoundaryState, nameof(request.DimensionMeasureScopeBoundaryState), errors);
        ValidateState(request.SemanticBindingControlBoundaryState, nameof(request.SemanticBindingControlBoundaryState), errors);
        ValidateState(request.RegistryReviewBoundaryState, nameof(request.RegistryReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.DataSourceRegistryDependencyState, nameof(request.DataSourceRegistryDependencyState), errors);
        ValidateState(request.DataGovernancePolicyDependencyState, nameof(request.DataGovernancePolicyDependencyState), errors);
        ValidateState(request.SemanticContractSourceDependencyState, nameof(request.SemanticContractSourceDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.StewardshipPreconditionState, nameof(request.StewardshipPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.VersioningPolicyState, nameof(request.VersioningPolicyState), errors);

        if (request.MetricSemanticRegistryReadinessState == MetricSemanticRegistryReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.MetricIdentityCatalogBoundaryState == MetricSemanticRegistryReadinessState.Ready)
        {
            errors.Add("Metric identity catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.SemanticEntityIntakeBoundaryState == MetricSemanticRegistryReadinessState.Ready)
        {
            errors.Add("Semantic entity intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.DimensionMeasureScopeBoundaryState == MetricSemanticRegistryReadinessState.Ready)
        {
            errors.Add("Dimension and measure scope cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.SemanticBindingControlBoundaryState == MetricSemanticRegistryReadinessState.Ready)
        {
            errors.Add("Semantic binding control cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.RegistryReviewBoundaryState == MetricSemanticRegistryReadinessState.Ready)
        {
            errors.Add("Registry review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == MetricSemanticRegistryReadinessState.Ready)
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
            errors.Add("Metric and semantic registry readiness metadata cannot contain metric or query values, measure results, semantic rule/expression bodies, raw dataset or warehouse rows, candidate/individual PII or contact details, free-text notes, narrative, attachments, document payloads, ratings, scores, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static MetricSemanticRegistryReadinessState ResolveFailClosedReadinessState(MetricSemanticRegistryReadinessCreateRequest request)
    {
        if (request.MetricSemanticRegistryReadinessState != MetricSemanticRegistryReadinessState.Ready)
        {
            return request.MetricSemanticRegistryReadinessState;
        }

        return ArePreconditionsReady(
            request.MetricIdentityCatalogBoundaryState,
            request.SemanticEntityIntakeBoundaryState,
            request.DimensionMeasureScopeBoundaryState,
            request.SemanticBindingControlBoundaryState,
            request.RegistryReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.DataSourceRegistryDependencyState,
            request.DataGovernancePolicyDependencyState,
            request.SemanticContractSourceDependencyState,
            request.NotificationDependencyState,
            request.StewardshipPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.VersioningPolicyState,
            request.DependencyStates)
            ? MetricSemanticRegistryReadinessState.Ready
            : MetricSemanticRegistryReadinessState.Deferred;
    }

    public static void ApplyEvaluation(MetricSemanticRegistryReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.MetricSemanticRegistryReadinessState = ArePreconditionsReady(
            entity.MetricIdentityCatalogBoundaryState,
            entity.SemanticEntityIntakeBoundaryState,
            entity.DimensionMeasureScopeBoundaryState,
            entity.SemanticBindingControlBoundaryState,
            entity.RegistryReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.DataSourceRegistryDependencyState,
            entity.DataGovernancePolicyDependencyState,
            entity.SemanticContractSourceDependencyState,
            entity.NotificationDependencyState,
            entity.StewardshipPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.VersioningPolicyState,
            entity.DependencyStates)
            ? MetricSemanticRegistryReadinessState.Ready
            : MetricSemanticRegistryReadinessState.Deferred;

        entity.DeferredReason = entity.MetricSemanticRegistryReadinessState == MetricSemanticRegistryReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Metric and semantic registry readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        MetricSemanticRegistryReadinessState metricIdentityCatalog,
        MetricSemanticRegistryReadinessState semanticEntityIntake,
        MetricSemanticRegistryReadinessState dimensionMeasureScope,
        MetricSemanticRegistryReadinessState semanticBindingControl,
        MetricSemanticRegistryReadinessState registryReview,
        MetricSemanticRegistryReadinessState automatedDecision,
        MetricSemanticRegistryReadinessState dataSourceRegistryDependency,
        MetricSemanticRegistryReadinessState dataGovernancePolicyDependency,
        MetricSemanticRegistryReadinessState semanticContractSourceDependency,
        MetricSemanticRegistryReadinessState notificationDependency,
        MetricSemanticRegistryReadinessState consent,
        MetricSemanticRegistryReadinessState dataMinimization,
        MetricSemanticRegistryReadinessState retention,
        MetricSemanticRegistryReadinessState evidence,
        IReadOnlyDictionary<string, MetricSemanticRegistryReadinessState> dependencyStates)
    {
        var required = new[]
        {
            metricIdentityCatalog,
            semanticEntityIntake,
            dimensionMeasureScope,
            semanticBindingControl,
            registryReview,
            automatedDecision,
            dataSourceRegistryDependency,
            dataGovernancePolicyDependency,
            semanticContractSourceDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(MetricSemanticRegistryReadinessState state) =>
        state is MetricSemanticRegistryReadinessState.Ready or MetricSemanticRegistryReadinessState.NotRequired;

    private static void ValidateState(MetricSemanticRegistryReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(MetricSemanticRegistryReadinessCreateRequest request)
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
