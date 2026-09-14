using System.Text.RegularExpressions;
using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedCertificationRegistry;

public static class VerifiedCertificationRegistryGuard
{
    public const string OwnerKey = "tep.verified-certification-registry";
    public const string ReadPermission = "tep.verified-certification-registry.read";
    public const string ManagePermission = "tep.verified-certification-registry.manage";
    public const string EvaluatePermission = "tep.verified-certification-registry.evaluate";
    public const string AuditReadPermission = "tep.verified-certification-registry.audit.read";

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
    // "verified-certification-registry" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(VerifiedCertificationRegistryReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.VerifiedCertificationRegistryReadinessVersion < 1)
        {
            errors.Add("VerifiedCertificationRegistryReadinessVersion must be greater than zero.");
        }

        ValidateState(request.VerifiedCertificationRegistryReadinessState, nameof(request.VerifiedCertificationRegistryReadinessState), errors);
        ValidateState(request.CertificationCatalogBoundaryState, nameof(request.CertificationCatalogBoundaryState), errors);
        ValidateState(request.VerificationIntakeBoundaryState, nameof(request.VerificationIntakeBoundaryState), errors);
        ValidateState(request.IssuerBindingScopeBoundaryState, nameof(request.IssuerBindingScopeBoundaryState), errors);
        ValidateState(request.VisibilityControlBoundaryState, nameof(request.VisibilityControlBoundaryState), errors);
        ValidateState(request.RegistryReviewBoundaryState, nameof(request.RegistryReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.TalentDataSourceDependencyState, nameof(request.TalentDataSourceDependencyState), errors);
        ValidateState(request.ConsentPolicyDependencyState, nameof(request.ConsentPolicyDependencyState), errors);
        ValidateState(request.SkillPassportSourceDependencyState, nameof(request.SkillPassportSourceDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.PublicationPolicyState, nameof(request.PublicationPolicyState), errors);

        if (request.VerifiedCertificationRegistryReadinessState == VerifiedCertificationRegistryReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.CertificationCatalogBoundaryState == VerifiedCertificationRegistryReadinessState.Ready)
        {
            errors.Add("Certification catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.VerificationIntakeBoundaryState == VerifiedCertificationRegistryReadinessState.Ready)
        {
            errors.Add("Verification intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.IssuerBindingScopeBoundaryState == VerifiedCertificationRegistryReadinessState.Ready)
        {
            errors.Add("Issuer binding scope cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.VisibilityControlBoundaryState == VerifiedCertificationRegistryReadinessState.Ready)
        {
            errors.Add("Visibility control cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.RegistryReviewBoundaryState == VerifiedCertificationRegistryReadinessState.Ready)
        {
            errors.Add("Registry review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == VerifiedCertificationRegistryReadinessState.Ready)
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
            errors.Add("Verified certification registry readiness metadata cannot contain certificate/credential/license identifiers or numbers, verification evidence or attestation content, holder/individual PII or contact details, issued-certificate rosters or holder lists, free-text verification narrative or notes, proficiency or ranking scores, free-text notes, narrative, attachments, document payloads, ratings, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static VerifiedCertificationRegistryReadinessState ResolveFailClosedReadinessState(VerifiedCertificationRegistryReadinessCreateRequest request)
    {
        if (request.VerifiedCertificationRegistryReadinessState != VerifiedCertificationRegistryReadinessState.Ready)
        {
            return request.VerifiedCertificationRegistryReadinessState;
        }

        return ArePreconditionsReady(
            request.CertificationCatalogBoundaryState,
            request.VerificationIntakeBoundaryState,
            request.IssuerBindingScopeBoundaryState,
            request.VisibilityControlBoundaryState,
            request.RegistryReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.TalentDataSourceDependencyState,
            request.ConsentPolicyDependencyState,
            request.SkillPassportSourceDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.PublicationPolicyState,
            request.DependencyStates)
            ? VerifiedCertificationRegistryReadinessState.Ready
            : VerifiedCertificationRegistryReadinessState.Deferred;
    }

    public static void ApplyEvaluation(VerifiedCertificationRegistryReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.VerifiedCertificationRegistryReadinessState = ArePreconditionsReady(
            entity.CertificationCatalogBoundaryState,
            entity.VerificationIntakeBoundaryState,
            entity.IssuerBindingScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.RegistryReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.SkillPassportSourceDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates)
            ? VerifiedCertificationRegistryReadinessState.Ready
            : VerifiedCertificationRegistryReadinessState.Deferred;

        entity.DeferredReason = entity.VerifiedCertificationRegistryReadinessState == VerifiedCertificationRegistryReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Verified certification registry readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        VerifiedCertificationRegistryReadinessState certificationCatalog,
        VerifiedCertificationRegistryReadinessState verificationIntake,
        VerifiedCertificationRegistryReadinessState issuerBindingScope,
        VerifiedCertificationRegistryReadinessState visibilityControl,
        VerifiedCertificationRegistryReadinessState registryReview,
        VerifiedCertificationRegistryReadinessState automatedDecision,
        VerifiedCertificationRegistryReadinessState talentDataSourceDependency,
        VerifiedCertificationRegistryReadinessState consentPolicyDependency,
        VerifiedCertificationRegistryReadinessState skillPassportSourceDependency,
        VerifiedCertificationRegistryReadinessState notificationDependency,
        VerifiedCertificationRegistryReadinessState consent,
        VerifiedCertificationRegistryReadinessState dataMinimization,
        VerifiedCertificationRegistryReadinessState retention,
        VerifiedCertificationRegistryReadinessState evidence,
        IReadOnlyDictionary<string, VerifiedCertificationRegistryReadinessState> dependencyStates)
    {
        var required = new[]
        {
            certificationCatalog,
            verificationIntake,
            issuerBindingScope,
            visibilityControl,
            registryReview,
            automatedDecision,
            talentDataSourceDependency,
            consentPolicyDependency,
            skillPassportSourceDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(VerifiedCertificationRegistryReadinessState state) =>
        state is VerifiedCertificationRegistryReadinessState.Ready or VerifiedCertificationRegistryReadinessState.NotRequired;

    private static void ValidateState(VerifiedCertificationRegistryReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(VerifiedCertificationRegistryReadinessCreateRequest request)
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
