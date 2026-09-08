using System.Text.RegularExpressions;
using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Application.Features.EtlEltPipelines;

public static class EtlEltPipelinesGuard
{
    public const string OwnerKey = "dki.etl-elt-pipelines";
    public const string ReadPermission = "dki.etl-elt-pipelines.read";
    public const string ManagePermission = "dki.etl-elt-pipelines.manage";
    public const string EvaluatePermission = "dki.etl-elt-pipelines.evaluate";
    public const string AuditReadPermission = "dki.etl-elt-pipelines.audit.read";

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
    // "etl-elt-pipelines" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(EtlEltPipelinesReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.EtlEltPipelinesReadinessVersion < 1)
        {
            errors.Add("EtlEltPipelinesReadinessVersion must be greater than zero.");
        }

        ValidateState(request.EtlEltPipelinesReadinessState, nameof(request.EtlEltPipelinesReadinessState), errors);
        ValidateState(request.PipelineDefinitionCatalogBoundaryState, nameof(request.PipelineDefinitionCatalogBoundaryState), errors);
        ValidateState(request.ExtractIntakeBoundaryState, nameof(request.ExtractIntakeBoundaryState), errors);
        ValidateState(request.TransformScopeBoundaryState, nameof(request.TransformScopeBoundaryState), errors);
        ValidateState(request.LoadControlBoundaryState, nameof(request.LoadControlBoundaryState), errors);
        ValidateState(request.OrchestrationReviewBoundaryState, nameof(request.OrchestrationReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.LakehouseSourceDependencyState, nameof(request.LakehouseSourceDependencyState), errors);
        ValidateState(request.JobOrchestrationDependencyState, nameof(request.JobOrchestrationDependencyState), errors);
        ValidateState(request.DataContractRegistryDependencyState, nameof(request.DataContractRegistryDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.StewardshipPreconditionState, nameof(request.StewardshipPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.MonitoringPolicyState, nameof(request.MonitoringPolicyState), errors);

        if (request.EtlEltPipelinesReadinessState == EtlEltPipelinesReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.PipelineDefinitionCatalogBoundaryState == EtlEltPipelinesReadinessState.Ready)
        {
            errors.Add("Pipeline definition catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ExtractIntakeBoundaryState == EtlEltPipelinesReadinessState.Ready)
        {
            errors.Add("Extract intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.TransformScopeBoundaryState == EtlEltPipelinesReadinessState.Ready)
        {
            errors.Add("Transform scope cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.LoadControlBoundaryState == EtlEltPipelinesReadinessState.Ready)
        {
            errors.Add("Load control cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.OrchestrationReviewBoundaryState == EtlEltPipelinesReadinessState.Ready)
        {
            errors.Add("Orchestration review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == EtlEltPipelinesReadinessState.Ready)
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
            errors.Add("ETL/ELT pipelines readiness metadata cannot contain extracted/transformed/loaded records or row values, raw source or staging data, query results, transformation logic/expression bodies, connection strings or pipeline credentials, job run payloads, schedules with embedded secrets, candidate/individual PII or contact details, free-text notes, narrative, attachments, document payloads, ratings, scores, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static EtlEltPipelinesReadinessState ResolveFailClosedReadinessState(EtlEltPipelinesReadinessCreateRequest request)
    {
        if (request.EtlEltPipelinesReadinessState != EtlEltPipelinesReadinessState.Ready)
        {
            return request.EtlEltPipelinesReadinessState;
        }

        return ArePreconditionsReady(
            request.PipelineDefinitionCatalogBoundaryState,
            request.ExtractIntakeBoundaryState,
            request.TransformScopeBoundaryState,
            request.LoadControlBoundaryState,
            request.OrchestrationReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.LakehouseSourceDependencyState,
            request.JobOrchestrationDependencyState,
            request.DataContractRegistryDependencyState,
            request.NotificationDependencyState,
            request.StewardshipPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.MonitoringPolicyState,
            request.DependencyStates)
            ? EtlEltPipelinesReadinessState.Ready
            : EtlEltPipelinesReadinessState.Deferred;
    }

    public static void ApplyEvaluation(EtlEltPipelinesReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.EtlEltPipelinesReadinessState = ArePreconditionsReady(
            entity.PipelineDefinitionCatalogBoundaryState,
            entity.ExtractIntakeBoundaryState,
            entity.TransformScopeBoundaryState,
            entity.LoadControlBoundaryState,
            entity.OrchestrationReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.LakehouseSourceDependencyState,
            entity.JobOrchestrationDependencyState,
            entity.DataContractRegistryDependencyState,
            entity.NotificationDependencyState,
            entity.StewardshipPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.MonitoringPolicyState,
            entity.DependencyStates)
            ? EtlEltPipelinesReadinessState.Ready
            : EtlEltPipelinesReadinessState.Deferred;

        entity.DeferredReason = entity.EtlEltPipelinesReadinessState == EtlEltPipelinesReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "ETL/ELT pipelines readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        EtlEltPipelinesReadinessState pipelineDefinitionCatalog,
        EtlEltPipelinesReadinessState extractIntake,
        EtlEltPipelinesReadinessState transformScope,
        EtlEltPipelinesReadinessState loadControl,
        EtlEltPipelinesReadinessState orchestrationReview,
        EtlEltPipelinesReadinessState automatedDecision,
        EtlEltPipelinesReadinessState lakehouseSourceDependency,
        EtlEltPipelinesReadinessState jobOrchestrationDependency,
        EtlEltPipelinesReadinessState dataContractRegistryDependency,
        EtlEltPipelinesReadinessState notificationDependency,
        EtlEltPipelinesReadinessState consent,
        EtlEltPipelinesReadinessState dataMinimization,
        EtlEltPipelinesReadinessState retention,
        EtlEltPipelinesReadinessState evidence,
        IReadOnlyDictionary<string, EtlEltPipelinesReadinessState> dependencyStates)
    {
        var required = new[]
        {
            pipelineDefinitionCatalog,
            extractIntake,
            transformScope,
            loadControl,
            orchestrationReview,
            automatedDecision,
            lakehouseSourceDependency,
            jobOrchestrationDependency,
            dataContractRegistryDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(EtlEltPipelinesReadinessState state) =>
        state is EtlEltPipelinesReadinessState.Ready or EtlEltPipelinesReadinessState.NotRequired;

    private static void ValidateState(EtlEltPipelinesReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(EtlEltPipelinesReadinessCreateRequest request)
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
