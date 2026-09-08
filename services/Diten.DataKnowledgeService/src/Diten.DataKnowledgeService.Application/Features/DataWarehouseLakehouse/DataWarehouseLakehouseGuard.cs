using System.Text.RegularExpressions;
using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse;

public static class DataWarehouseLakehouseGuard
{
    public const string OwnerKey = "dki.data-warehouse-lakehouse";
    public const string ReadPermission = "dki.data-warehouse-lakehouse.read";
    public const string ManagePermission = "dki.data-warehouse-lakehouse.manage";
    public const string EvaluatePermission = "dki.data-warehouse-lakehouse.evaluate";
    public const string AuditReadPermission = "dki.data-warehouse-lakehouse.audit.read";

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
    // "data-warehouse-lakehouse" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(DataWarehouseLakehouseReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.DataWarehouseLakehouseReadinessVersion < 1)
        {
            errors.Add("DataWarehouseLakehouseReadinessVersion must be greater than zero.");
        }

        ValidateState(request.DataWarehouseLakehouseReadinessState, nameof(request.DataWarehouseLakehouseReadinessState), errors);
        ValidateState(request.StorageLayerCatalogBoundaryState, nameof(request.StorageLayerCatalogBoundaryState), errors);
        ValidateState(request.IngestionIntakeBoundaryState, nameof(request.IngestionIntakeBoundaryState), errors);
        ValidateState(request.PartitioningScopeBoundaryState, nameof(request.PartitioningScopeBoundaryState), errors);
        ValidateState(request.LineageControlBoundaryState, nameof(request.LineageControlBoundaryState), errors);
        ValidateState(request.WarehouseReviewBoundaryState, nameof(request.WarehouseReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.VaultDependencyState, nameof(request.VaultDependencyState), errors);
        ValidateState(request.LoggingMonitoringDependencyState, nameof(request.LoggingMonitoringDependencyState), errors);
        ValidateState(request.DataContractRegistryDependencyState, nameof(request.DataContractRegistryDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.StewardshipPreconditionState, nameof(request.StewardshipPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.StorageTierPolicyState, nameof(request.StorageTierPolicyState), errors);

        if (request.DataWarehouseLakehouseReadinessState == DataWarehouseLakehouseReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.StorageLayerCatalogBoundaryState == DataWarehouseLakehouseReadinessState.Ready)
        {
            errors.Add("Storage layer catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.IngestionIntakeBoundaryState == DataWarehouseLakehouseReadinessState.Ready)
        {
            errors.Add("Ingestion intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.PartitioningScopeBoundaryState == DataWarehouseLakehouseReadinessState.Ready)
        {
            errors.Add("Partitioning scope cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.LineageControlBoundaryState == DataWarehouseLakehouseReadinessState.Ready)
        {
            errors.Add("Lineage control cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.WarehouseReviewBoundaryState == DataWarehouseLakehouseReadinessState.Ready)
        {
            errors.Add("Warehouse review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == DataWarehouseLakehouseReadinessState.Ready)
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
            errors.Add("Data warehouse and lakehouse readiness metadata cannot contain dataset/table/warehouse rows or column values, raw ingested records, query results, connection strings or storage credentials, partition data, lineage payloads, candidate/individual PII or contact details, free-text notes, narrative, attachments, document payloads, ratings, scores, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static DataWarehouseLakehouseReadinessState ResolveFailClosedReadinessState(DataWarehouseLakehouseReadinessCreateRequest request)
    {
        if (request.DataWarehouseLakehouseReadinessState != DataWarehouseLakehouseReadinessState.Ready)
        {
            return request.DataWarehouseLakehouseReadinessState;
        }

        return ArePreconditionsReady(
            request.StorageLayerCatalogBoundaryState,
            request.IngestionIntakeBoundaryState,
            request.PartitioningScopeBoundaryState,
            request.LineageControlBoundaryState,
            request.WarehouseReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.VaultDependencyState,
            request.LoggingMonitoringDependencyState,
            request.DataContractRegistryDependencyState,
            request.NotificationDependencyState,
            request.StewardshipPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.StorageTierPolicyState,
            request.DependencyStates)
            ? DataWarehouseLakehouseReadinessState.Ready
            : DataWarehouseLakehouseReadinessState.Deferred;
    }

    public static void ApplyEvaluation(DataWarehouseLakehouseReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.DataWarehouseLakehouseReadinessState = ArePreconditionsReady(
            entity.StorageLayerCatalogBoundaryState,
            entity.IngestionIntakeBoundaryState,
            entity.PartitioningScopeBoundaryState,
            entity.LineageControlBoundaryState,
            entity.WarehouseReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.VaultDependencyState,
            entity.LoggingMonitoringDependencyState,
            entity.DataContractRegistryDependencyState,
            entity.NotificationDependencyState,
            entity.StewardshipPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.StorageTierPolicyState,
            entity.DependencyStates)
            ? DataWarehouseLakehouseReadinessState.Ready
            : DataWarehouseLakehouseReadinessState.Deferred;

        entity.DeferredReason = entity.DataWarehouseLakehouseReadinessState == DataWarehouseLakehouseReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Data warehouse and lakehouse readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        DataWarehouseLakehouseReadinessState storageLayerCatalog,
        DataWarehouseLakehouseReadinessState ingestionIntake,
        DataWarehouseLakehouseReadinessState partitioningScope,
        DataWarehouseLakehouseReadinessState lineageControl,
        DataWarehouseLakehouseReadinessState warehouseReview,
        DataWarehouseLakehouseReadinessState automatedDecision,
        DataWarehouseLakehouseReadinessState vaultDependency,
        DataWarehouseLakehouseReadinessState loggingMonitoringDependency,
        DataWarehouseLakehouseReadinessState dataContractRegistryDependency,
        DataWarehouseLakehouseReadinessState notificationDependency,
        DataWarehouseLakehouseReadinessState consent,
        DataWarehouseLakehouseReadinessState dataMinimization,
        DataWarehouseLakehouseReadinessState retention,
        DataWarehouseLakehouseReadinessState evidence,
        IReadOnlyDictionary<string, DataWarehouseLakehouseReadinessState> dependencyStates)
    {
        var required = new[]
        {
            storageLayerCatalog,
            ingestionIntake,
            partitioningScope,
            lineageControl,
            warehouseReview,
            automatedDecision,
            vaultDependency,
            loggingMonitoringDependency,
            dataContractRegistryDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(DataWarehouseLakehouseReadinessState state) =>
        state is DataWarehouseLakehouseReadinessState.Ready or DataWarehouseLakehouseReadinessState.NotRequired;

    private static void ValidateState(DataWarehouseLakehouseReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(DataWarehouseLakehouseReadinessCreateRequest request)
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
