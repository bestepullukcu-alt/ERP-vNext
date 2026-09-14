using System.Text.RegularExpressions;
using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.TalentDataFoundation;

public static class TalentDataFoundationGuard
{
    public const string OwnerKey = "tep.talent-data-foundation";
    public const string ReadPermission = "tep.talent-data-foundation.read";
    public const string ManagePermission = "tep.talent-data-foundation.manage";
    public const string EvaluatePermission = "tep.talent-data-foundation.evaluate";
    public const string AuditReadPermission = "tep.talent-data-foundation.audit.read";

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
    // "talent-data-foundation" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(TalentDataFoundationReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.TalentDataFoundationReadinessVersion < 1)
        {
            errors.Add("TalentDataFoundationReadinessVersion must be greater than zero.");
        }

        ValidateState(request.TalentDataFoundationReadinessState, nameof(request.TalentDataFoundationReadinessState), errors);
        ValidateState(request.TalentEntityCatalogBoundaryState, nameof(request.TalentEntityCatalogBoundaryState), errors);
        ValidateState(request.DataIngestionBoundaryState, nameof(request.DataIngestionBoundaryState), errors);
        ValidateState(request.IdentityResolutionBoundaryState, nameof(request.IdentityResolutionBoundaryState), errors);
        ValidateState(request.DataQualityBoundaryState, nameof(request.DataQualityBoundaryState), errors);
        ValidateState(request.LineageTrackingBoundaryState, nameof(request.LineageTrackingBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.HcmFoundationDependencyState, nameof(request.HcmFoundationDependencyState), errors);
        ValidateState(request.ConsentPolicyDependencyState, nameof(request.ConsentPolicyDependencyState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.TalentDataFoundationReadinessState == TalentDataFoundationReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.TalentEntityCatalogBoundaryState == TalentDataFoundationReadinessState.Ready)
        {
            errors.Add("Talent entity catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.DataIngestionBoundaryState == TalentDataFoundationReadinessState.Ready)
        {
            errors.Add("Data ingestion cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.IdentityResolutionBoundaryState == TalentDataFoundationReadinessState.Ready)
        {
            errors.Add("Identity resolution cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.DataQualityBoundaryState == TalentDataFoundationReadinessState.Ready)
        {
            errors.Add("Data quality cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.LineageTrackingBoundaryState == TalentDataFoundationReadinessState.Ready)
        {
            errors.Add("Lineage tracking cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == TalentDataFoundationReadinessState.Ready)
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
            errors.Add("Talent data foundation readiness metadata cannot contain raw talent/candidate records, resume/CV content, profile payloads, PII-bearing dataset rows, free-text notes, narrative, attachments, document payloads, scores, ratings, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static TalentDataFoundationReadinessState ResolveFailClosedReadinessState(TalentDataFoundationReadinessCreateRequest request)
    {
        if (request.TalentDataFoundationReadinessState != TalentDataFoundationReadinessState.Ready)
        {
            return request.TalentDataFoundationReadinessState;
        }

        return ArePreconditionsReady(
            request.TalentEntityCatalogBoundaryState,
            request.DataIngestionBoundaryState,
            request.IdentityResolutionBoundaryState,
            request.DataQualityBoundaryState,
            request.LineageTrackingBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.HcmFoundationDependencyState,
            request.ConsentPolicyDependencyState,
            request.DocumentDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.DependencyStates)
            ? TalentDataFoundationReadinessState.Ready
            : TalentDataFoundationReadinessState.Deferred;
    }

    public static void ApplyEvaluation(TalentDataFoundationReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.TalentDataFoundationReadinessState = ArePreconditionsReady(
            entity.TalentEntityCatalogBoundaryState,
            entity.DataIngestionBoundaryState,
            entity.IdentityResolutionBoundaryState,
            entity.DataQualityBoundaryState,
            entity.LineageTrackingBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.HcmFoundationDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates)
            ? TalentDataFoundationReadinessState.Ready
            : TalentDataFoundationReadinessState.Deferred;

        entity.DeferredReason = entity.TalentDataFoundationReadinessState == TalentDataFoundationReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Talent data foundation readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        TalentDataFoundationReadinessState talentEntityCatalog,
        TalentDataFoundationReadinessState dataIngestion,
        TalentDataFoundationReadinessState identityResolution,
        TalentDataFoundationReadinessState dataQuality,
        TalentDataFoundationReadinessState lineageTracking,
        TalentDataFoundationReadinessState automatedDecision,
        TalentDataFoundationReadinessState hcmFoundationDependency,
        TalentDataFoundationReadinessState consentPolicyDependency,
        TalentDataFoundationReadinessState documentDependency,
        TalentDataFoundationReadinessState notificationDependency,
        TalentDataFoundationReadinessState consent,
        TalentDataFoundationReadinessState dataMinimization,
        TalentDataFoundationReadinessState retention,
        TalentDataFoundationReadinessState evidence,
        IReadOnlyDictionary<string, TalentDataFoundationReadinessState> dependencyStates)
    {
        var required = new[]
        {
            talentEntityCatalog,
            dataIngestion,
            identityResolution,
            dataQuality,
            lineageTracking,
            automatedDecision,
            hcmFoundationDependency,
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

    private static bool IsSatisfied(TalentDataFoundationReadinessState state) =>
        state is TalentDataFoundationReadinessState.Ready or TalentDataFoundationReadinessState.NotRequired;

    private static void ValidateState(TalentDataFoundationReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(TalentDataFoundationReadinessCreateRequest request)
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
