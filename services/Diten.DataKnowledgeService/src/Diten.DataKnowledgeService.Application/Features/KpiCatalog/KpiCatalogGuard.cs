using System.Text.RegularExpressions;
using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Application.Features.KpiCatalog;

public static class KpiCatalogGuard
{
    public const string OwnerKey = "dki.kpi-catalog";
    public const string ReadPermission = "dki.kpi-catalog.read";
    public const string ManagePermission = "dki.kpi-catalog.manage";
    public const string EvaluatePermission = "dki.kpi-catalog.evaluate";
    public const string AuditReadPermission = "dki.kpi-catalog.audit.read";

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
    // "kpi-catalog" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(KpiCatalogReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.KpiCatalogReadinessVersion < 1)
        {
            errors.Add("KpiCatalogReadinessVersion must be greater than zero.");
        }

        ValidateState(request.KpiCatalogReadinessState, nameof(request.KpiCatalogReadinessState), errors);
        ValidateState(request.KpiIdentityCatalogBoundaryState, nameof(request.KpiIdentityCatalogBoundaryState), errors);
        ValidateState(request.DefinitionBindingIntakeBoundaryState, nameof(request.DefinitionBindingIntakeBoundaryState), errors);
        ValidateState(request.OwnershipScopeBoundaryState, nameof(request.OwnershipScopeBoundaryState), errors);
        ValidateState(request.PublicationControlBoundaryState, nameof(request.PublicationControlBoundaryState), errors);
        ValidateState(request.CatalogReviewBoundaryState, nameof(request.CatalogReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.MetricSemanticRegistrySourceDependencyState, nameof(request.MetricSemanticRegistrySourceDependencyState), errors);
        ValidateState(request.DataDictionaryDependencyState, nameof(request.DataDictionaryDependencyState), errors);
        ValidateState(request.DataContractRegistryDependencyState, nameof(request.DataContractRegistryDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.StewardshipPreconditionState, nameof(request.StewardshipPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.PublicationPolicyState, nameof(request.PublicationPolicyState), errors);

        if (request.KpiCatalogReadinessState == KpiCatalogReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.KpiIdentityCatalogBoundaryState == KpiCatalogReadinessState.Ready)
        {
            errors.Add("KPI identity catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.DefinitionBindingIntakeBoundaryState == KpiCatalogReadinessState.Ready)
        {
            errors.Add("Definition binding intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.OwnershipScopeBoundaryState == KpiCatalogReadinessState.Ready)
        {
            errors.Add("Ownership scope cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.PublicationControlBoundaryState == KpiCatalogReadinessState.Ready)
        {
            errors.Add("Publication control cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.CatalogReviewBoundaryState == KpiCatalogReadinessState.Ready)
        {
            errors.Add("Catalog review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == KpiCatalogReadinessState.Ready)
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
            errors.Add("KPI catalog readiness metadata cannot contain KPI values or measure results, metric query expressions, target/threshold numbers, formula/definition bodies, dataset rows, individual attributions, candidate/individual PII or contact details, free-text notes, narrative, attachments, document payloads, ratings, scores, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static KpiCatalogReadinessState ResolveFailClosedReadinessState(KpiCatalogReadinessCreateRequest request)
    {
        if (request.KpiCatalogReadinessState != KpiCatalogReadinessState.Ready)
        {
            return request.KpiCatalogReadinessState;
        }

        return ArePreconditionsReady(
            request.KpiIdentityCatalogBoundaryState,
            request.DefinitionBindingIntakeBoundaryState,
            request.OwnershipScopeBoundaryState,
            request.PublicationControlBoundaryState,
            request.CatalogReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.MetricSemanticRegistrySourceDependencyState,
            request.DataDictionaryDependencyState,
            request.DataContractRegistryDependencyState,
            request.NotificationDependencyState,
            request.StewardshipPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.PublicationPolicyState,
            request.DependencyStates)
            ? KpiCatalogReadinessState.Ready
            : KpiCatalogReadinessState.Deferred;
    }

    public static void ApplyEvaluation(KpiCatalogReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.KpiCatalogReadinessState = ArePreconditionsReady(
            entity.KpiIdentityCatalogBoundaryState,
            entity.DefinitionBindingIntakeBoundaryState,
            entity.OwnershipScopeBoundaryState,
            entity.PublicationControlBoundaryState,
            entity.CatalogReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.MetricSemanticRegistrySourceDependencyState,
            entity.DataDictionaryDependencyState,
            entity.DataContractRegistryDependencyState,
            entity.NotificationDependencyState,
            entity.StewardshipPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates)
            ? KpiCatalogReadinessState.Ready
            : KpiCatalogReadinessState.Deferred;

        entity.DeferredReason = entity.KpiCatalogReadinessState == KpiCatalogReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "KPI catalog readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        KpiCatalogReadinessState kpiIdentityCatalog,
        KpiCatalogReadinessState definitionBindingIntake,
        KpiCatalogReadinessState ownershipScope,
        KpiCatalogReadinessState publicationControl,
        KpiCatalogReadinessState catalogReview,
        KpiCatalogReadinessState automatedDecision,
        KpiCatalogReadinessState metricSemanticRegistrySourceDependency,
        KpiCatalogReadinessState dataDictionaryDependency,
        KpiCatalogReadinessState dataContractRegistryDependency,
        KpiCatalogReadinessState notificationDependency,
        KpiCatalogReadinessState consent,
        KpiCatalogReadinessState dataMinimization,
        KpiCatalogReadinessState retention,
        KpiCatalogReadinessState evidence,
        IReadOnlyDictionary<string, KpiCatalogReadinessState> dependencyStates)
    {
        var required = new[]
        {
            kpiIdentityCatalog,
            definitionBindingIntake,
            ownershipScope,
            publicationControl,
            catalogReview,
            automatedDecision,
            metricSemanticRegistrySourceDependency,
            dataDictionaryDependency,
            dataContractRegistryDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(KpiCatalogReadinessState state) =>
        state is KpiCatalogReadinessState.Ready or KpiCatalogReadinessState.NotRequired;

    private static void ValidateState(KpiCatalogReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(KpiCatalogReadinessCreateRequest request)
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
