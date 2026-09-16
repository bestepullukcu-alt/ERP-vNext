using System.Text.RegularExpressions;
using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.RestrictedIntegrityRegistry;

public static class RestrictedIntegrityRegistryGuard
{
    public const string OwnerKey = "tep.restricted-integrity-registry";
    public const string ReadPermission = "tep.restricted-integrity-registry.read";
    public const string ManagePermission = "tep.restricted-integrity-registry.manage";
    public const string EvaluatePermission = "tep.restricted-integrity-registry.evaluate";
    public const string AuditReadPermission = "tep.restricted-integrity-registry.audit.read";

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
    // "restricted-integrity-registry" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(RestrictedIntegrityRegistryReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.RestrictedIntegrityRegistryReadinessVersion < 1)
        {
            errors.Add("RestrictedIntegrityRegistryReadinessVersion must be greater than zero.");
        }

        ValidateState(request.RestrictedIntegrityRegistryReadinessState, nameof(request.RestrictedIntegrityRegistryReadinessState), errors);
        ValidateState(request.IntegrityCaseCatalogBoundaryState, nameof(request.IntegrityCaseCatalogBoundaryState), errors);
        ValidateState(request.RestrictionScopeBoundaryState, nameof(request.RestrictionScopeBoundaryState), errors);
        ValidateState(request.EvidenceChainBoundaryState, nameof(request.EvidenceChainBoundaryState), errors);
        ValidateState(request.DisclosureControlBoundaryState, nameof(request.DisclosureControlBoundaryState), errors);
        ValidateState(request.CaseReviewBoundaryState, nameof(request.CaseReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.EarlyWarningSourceDependencyState, nameof(request.EarlyWarningSourceDependencyState), errors);
        ValidateState(request.ConsentPolicyDependencyState, nameof(request.ConsentPolicyDependencyState), errors);
        ValidateState(request.LegalHoldDependencyState, nameof(request.LegalHoldDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.RestrictedIntegrityRegistryReadinessState == RestrictedIntegrityRegistryReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.IntegrityCaseCatalogBoundaryState == RestrictedIntegrityRegistryReadinessState.Ready)
        {
            errors.Add("Integrity case catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.RestrictionScopeBoundaryState == RestrictedIntegrityRegistryReadinessState.Ready)
        {
            errors.Add("Restriction scope cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.EvidenceChainBoundaryState == RestrictedIntegrityRegistryReadinessState.Ready)
        {
            errors.Add("Evidence chain cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.DisclosureControlBoundaryState == RestrictedIntegrityRegistryReadinessState.Ready)
        {
            errors.Add("Disclosure control cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.CaseReviewBoundaryState == RestrictedIntegrityRegistryReadinessState.Ready)
        {
            errors.Add("Case review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == RestrictedIntegrityRegistryReadinessState.Ready)
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
            errors.Add("Restricted integrity registry readiness metadata cannot contain restricted-integrity case bodies, allegations, findings, evidence content, disclosure/adverse-action decisions, candidate/individual PII or attributions, free-text notes, narrative, attachments, document payloads, scores, ratings, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static RestrictedIntegrityRegistryReadinessState ResolveFailClosedReadinessState(RestrictedIntegrityRegistryReadinessCreateRequest request)
    {
        if (request.RestrictedIntegrityRegistryReadinessState != RestrictedIntegrityRegistryReadinessState.Ready)
        {
            return request.RestrictedIntegrityRegistryReadinessState;
        }

        return ArePreconditionsReady(
            request.IntegrityCaseCatalogBoundaryState,
            request.RestrictionScopeBoundaryState,
            request.EvidenceChainBoundaryState,
            request.DisclosureControlBoundaryState,
            request.CaseReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.EarlyWarningSourceDependencyState,
            request.ConsentPolicyDependencyState,
            request.LegalHoldDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.DependencyStates)
            ? RestrictedIntegrityRegistryReadinessState.Ready
            : RestrictedIntegrityRegistryReadinessState.Deferred;
    }

    public static void ApplyEvaluation(RestrictedIntegrityRegistryReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.RestrictedIntegrityRegistryReadinessState = ArePreconditionsReady(
            entity.IntegrityCaseCatalogBoundaryState,
            entity.RestrictionScopeBoundaryState,
            entity.EvidenceChainBoundaryState,
            entity.DisclosureControlBoundaryState,
            entity.CaseReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.EarlyWarningSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.LegalHoldDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates)
            ? RestrictedIntegrityRegistryReadinessState.Ready
            : RestrictedIntegrityRegistryReadinessState.Deferred;

        entity.DeferredReason = entity.RestrictedIntegrityRegistryReadinessState == RestrictedIntegrityRegistryReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Restricted integrity registry readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        RestrictedIntegrityRegistryReadinessState integrityCaseCatalog,
        RestrictedIntegrityRegistryReadinessState restrictionScope,
        RestrictedIntegrityRegistryReadinessState evidenceChain,
        RestrictedIntegrityRegistryReadinessState disclosureControl,
        RestrictedIntegrityRegistryReadinessState caseReview,
        RestrictedIntegrityRegistryReadinessState automatedDecision,
        RestrictedIntegrityRegistryReadinessState earlyWarningSourceDependency,
        RestrictedIntegrityRegistryReadinessState consentPolicyDependency,
        RestrictedIntegrityRegistryReadinessState legalHoldDependency,
        RestrictedIntegrityRegistryReadinessState notificationDependency,
        RestrictedIntegrityRegistryReadinessState consent,
        RestrictedIntegrityRegistryReadinessState dataMinimization,
        RestrictedIntegrityRegistryReadinessState retention,
        RestrictedIntegrityRegistryReadinessState evidence,
        IReadOnlyDictionary<string, RestrictedIntegrityRegistryReadinessState> dependencyStates)
    {
        var required = new[]
        {
            integrityCaseCatalog,
            restrictionScope,
            evidenceChain,
            disclosureControl,
            caseReview,
            automatedDecision,
            earlyWarningSourceDependency,
            consentPolicyDependency,
            legalHoldDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(RestrictedIntegrityRegistryReadinessState state) =>
        state is RestrictedIntegrityRegistryReadinessState.Ready or RestrictedIntegrityRegistryReadinessState.NotRequired;

    private static void ValidateState(RestrictedIntegrityRegistryReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(RestrictedIntegrityRegistryReadinessCreateRequest request)
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
