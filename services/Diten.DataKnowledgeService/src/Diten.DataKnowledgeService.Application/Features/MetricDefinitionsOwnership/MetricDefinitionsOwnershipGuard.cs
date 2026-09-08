using System.Text.RegularExpressions;
using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Application.Features.MetricDefinitionsOwnership;

public static class MetricDefinitionsOwnershipGuard
{
    public const string OwnerKey = "dki.metric-definitions-ownership";
    public const string ReadPermission = "dki.metric-definitions-ownership.read";
    public const string ManagePermission = "dki.metric-definitions-ownership.manage";
    public const string EvaluatePermission = "dki.metric-definitions-ownership.evaluate";
    public const string AuditReadPermission = "dki.metric-definitions-ownership.audit.read";

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
    // "metric-definitions-ownership" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(MetricDefinitionsOwnershipReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.MetricDefinitionsOwnershipReadinessVersion < 1)
        {
            errors.Add("MetricDefinitionsOwnershipReadinessVersion must be greater than zero.");
        }

        ValidateState(request.MetricDefinitionsOwnershipReadinessState, nameof(request.MetricDefinitionsOwnershipReadinessState), errors);
        ValidateState(request.DefinitionCatalogBoundaryState, nameof(request.DefinitionCatalogBoundaryState), errors);
        ValidateState(request.OwnershipAssignmentIntakeBoundaryState, nameof(request.OwnershipAssignmentIntakeBoundaryState), errors);
        ValidateState(request.StewardshipScopeBoundaryState, nameof(request.StewardshipScopeBoundaryState), errors);
        ValidateState(request.ApprovalControlBoundaryState, nameof(request.ApprovalControlBoundaryState), errors);
        ValidateState(request.DefinitionReviewBoundaryState, nameof(request.DefinitionReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.MetricSemanticRegistrySourceDependencyState, nameof(request.MetricSemanticRegistrySourceDependencyState), errors);
        ValidateState(request.KpiCatalogSourceDependencyState, nameof(request.KpiCatalogSourceDependencyState), errors);
        ValidateState(request.DataContractRegistryDependencyState, nameof(request.DataContractRegistryDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.StewardshipPreconditionState, nameof(request.StewardshipPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.ApprovalPolicyState, nameof(request.ApprovalPolicyState), errors);

        if (request.MetricDefinitionsOwnershipReadinessState == MetricDefinitionsOwnershipReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.DefinitionCatalogBoundaryState == MetricDefinitionsOwnershipReadinessState.Ready)
        {
            errors.Add("Definition catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.OwnershipAssignmentIntakeBoundaryState == MetricDefinitionsOwnershipReadinessState.Ready)
        {
            errors.Add("Ownership assignment intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.StewardshipScopeBoundaryState == MetricDefinitionsOwnershipReadinessState.Ready)
        {
            errors.Add("Stewardship scope cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ApprovalControlBoundaryState == MetricDefinitionsOwnershipReadinessState.Ready)
        {
            errors.Add("Approval control cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.DefinitionReviewBoundaryState == MetricDefinitionsOwnershipReadinessState.Ready)
        {
            errors.Add("Definition review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == MetricDefinitionsOwnershipReadinessState.Ready)
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
            errors.Add("Metric definitions and ownership readiness metadata cannot contain metric formula/definition/expression bodies, calculation logic, metric or measure values, dataset rows, owner/steward PII or personal contact details, individual attributions, free-text notes, narrative, attachments, document payloads, ratings, scores, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static MetricDefinitionsOwnershipReadinessState ResolveFailClosedReadinessState(MetricDefinitionsOwnershipReadinessCreateRequest request)
    {
        if (request.MetricDefinitionsOwnershipReadinessState != MetricDefinitionsOwnershipReadinessState.Ready)
        {
            return request.MetricDefinitionsOwnershipReadinessState;
        }

        return ArePreconditionsReady(
            request.DefinitionCatalogBoundaryState,
            request.OwnershipAssignmentIntakeBoundaryState,
            request.StewardshipScopeBoundaryState,
            request.ApprovalControlBoundaryState,
            request.DefinitionReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.MetricSemanticRegistrySourceDependencyState,
            request.KpiCatalogSourceDependencyState,
            request.DataContractRegistryDependencyState,
            request.NotificationDependencyState,
            request.StewardshipPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.ApprovalPolicyState,
            request.DependencyStates)
            ? MetricDefinitionsOwnershipReadinessState.Ready
            : MetricDefinitionsOwnershipReadinessState.Deferred;
    }

    public static void ApplyEvaluation(MetricDefinitionsOwnershipReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.MetricDefinitionsOwnershipReadinessState = ArePreconditionsReady(
            entity.DefinitionCatalogBoundaryState,
            entity.OwnershipAssignmentIntakeBoundaryState,
            entity.StewardshipScopeBoundaryState,
            entity.ApprovalControlBoundaryState,
            entity.DefinitionReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.MetricSemanticRegistrySourceDependencyState,
            entity.KpiCatalogSourceDependencyState,
            entity.DataContractRegistryDependencyState,
            entity.NotificationDependencyState,
            entity.StewardshipPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.ApprovalPolicyState,
            entity.DependencyStates)
            ? MetricDefinitionsOwnershipReadinessState.Ready
            : MetricDefinitionsOwnershipReadinessState.Deferred;

        entity.DeferredReason = entity.MetricDefinitionsOwnershipReadinessState == MetricDefinitionsOwnershipReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Metric definitions and ownership readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        MetricDefinitionsOwnershipReadinessState definitionCatalog,
        MetricDefinitionsOwnershipReadinessState ownershipAssignmentIntake,
        MetricDefinitionsOwnershipReadinessState stewardshipScope,
        MetricDefinitionsOwnershipReadinessState approvalControl,
        MetricDefinitionsOwnershipReadinessState definitionReview,
        MetricDefinitionsOwnershipReadinessState automatedDecision,
        MetricDefinitionsOwnershipReadinessState metricSemanticRegistrySourceDependency,
        MetricDefinitionsOwnershipReadinessState kpiCatalogSourceDependency,
        MetricDefinitionsOwnershipReadinessState dataContractRegistryDependency,
        MetricDefinitionsOwnershipReadinessState notificationDependency,
        MetricDefinitionsOwnershipReadinessState consent,
        MetricDefinitionsOwnershipReadinessState dataMinimization,
        MetricDefinitionsOwnershipReadinessState retention,
        MetricDefinitionsOwnershipReadinessState evidence,
        IReadOnlyDictionary<string, MetricDefinitionsOwnershipReadinessState> dependencyStates)
    {
        var required = new[]
        {
            definitionCatalog,
            ownershipAssignmentIntake,
            stewardshipScope,
            approvalControl,
            definitionReview,
            automatedDecision,
            metricSemanticRegistrySourceDependency,
            kpiCatalogSourceDependency,
            dataContractRegistryDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(MetricDefinitionsOwnershipReadinessState state) =>
        state is MetricDefinitionsOwnershipReadinessState.Ready or MetricDefinitionsOwnershipReadinessState.NotRequired;

    private static void ValidateState(MetricDefinitionsOwnershipReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(MetricDefinitionsOwnershipReadinessCreateRequest request)
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
