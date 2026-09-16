using System.Text.RegularExpressions;
using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport;

public static class IndustrySkillPassportGuard
{
    public const string OwnerKey = "tep.industry-skill-passport";
    public const string ReadPermission = "tep.industry-skill-passport.read";
    public const string ManagePermission = "tep.industry-skill-passport.manage";
    public const string EvaluatePermission = "tep.industry-skill-passport.evaluate";
    public const string AuditReadPermission = "tep.industry-skill-passport.audit.read";

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
    // "industry-skill-passport" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(IndustrySkillPassportReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.IndustrySkillPassportReadinessVersion < 1)
        {
            errors.Add("IndustrySkillPassportReadinessVersion must be greater than zero.");
        }

        ValidateState(request.IndustrySkillPassportReadinessState, nameof(request.IndustrySkillPassportReadinessState), errors);
        ValidateState(request.SkillClaimCatalogBoundaryState, nameof(request.SkillClaimCatalogBoundaryState), errors);
        ValidateState(request.AttestationIntakeBoundaryState, nameof(request.AttestationIntakeBoundaryState), errors);
        ValidateState(request.VerificationScopeBoundaryState, nameof(request.VerificationScopeBoundaryState), errors);
        ValidateState(request.VisibilityControlBoundaryState, nameof(request.VisibilityControlBoundaryState), errors);
        ValidateState(request.PassportReviewBoundaryState, nameof(request.PassportReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.TalentDataSourceDependencyState, nameof(request.TalentDataSourceDependencyState), errors);
        ValidateState(request.ConsentPolicyDependencyState, nameof(request.ConsentPolicyDependencyState), errors);
        ValidateState(request.CertificationSourceDependencyState, nameof(request.CertificationSourceDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.VerificationPolicyState, nameof(request.VerificationPolicyState), errors);

        if (request.IndustrySkillPassportReadinessState == IndustrySkillPassportReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.SkillClaimCatalogBoundaryState == IndustrySkillPassportReadinessState.Ready)
        {
            errors.Add("Skill claim catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AttestationIntakeBoundaryState == IndustrySkillPassportReadinessState.Ready)
        {
            errors.Add("Credential intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.VerificationScopeBoundaryState == IndustrySkillPassportReadinessState.Ready)
        {
            errors.Add("Verification scope cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.VisibilityControlBoundaryState == IndustrySkillPassportReadinessState.Ready)
        {
            errors.Add("Visibility control cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.PassportReviewBoundaryState == IndustrySkillPassportReadinessState.Ready)
        {
            errors.Add("Passport review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == IndustrySkillPassportReadinessState.Ready)
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
            errors.Add("Industry skill passport readiness metadata cannot contain skill claim rosters, candidate/individual PII or contact details, resume/CV or profile content, certificate/credential/license content or numbers, verification evidence or attestation content, proficiency or ranking scores, free-text notes, narrative, attachments, document payloads, ratings, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static IndustrySkillPassportReadinessState ResolveFailClosedReadinessState(IndustrySkillPassportReadinessCreateRequest request)
    {
        if (request.IndustrySkillPassportReadinessState != IndustrySkillPassportReadinessState.Ready)
        {
            return request.IndustrySkillPassportReadinessState;
        }

        return ArePreconditionsReady(
            request.SkillClaimCatalogBoundaryState,
            request.AttestationIntakeBoundaryState,
            request.VerificationScopeBoundaryState,
            request.VisibilityControlBoundaryState,
            request.PassportReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.TalentDataSourceDependencyState,
            request.ConsentPolicyDependencyState,
            request.CertificationSourceDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.VerificationPolicyState,
            request.DependencyStates)
            ? IndustrySkillPassportReadinessState.Ready
            : IndustrySkillPassportReadinessState.Deferred;
    }

    public static void ApplyEvaluation(IndustrySkillPassportReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.IndustrySkillPassportReadinessState = ArePreconditionsReady(
            entity.SkillClaimCatalogBoundaryState,
            entity.AttestationIntakeBoundaryState,
            entity.VerificationScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.PassportReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.CertificationSourceDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.VerificationPolicyState,
            entity.DependencyStates)
            ? IndustrySkillPassportReadinessState.Ready
            : IndustrySkillPassportReadinessState.Deferred;

        entity.DeferredReason = entity.IndustrySkillPassportReadinessState == IndustrySkillPassportReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Industry skill passport readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        IndustrySkillPassportReadinessState skillClaimCatalog,
        IndustrySkillPassportReadinessState attestationIntake,
        IndustrySkillPassportReadinessState verificationScope,
        IndustrySkillPassportReadinessState visibilityControl,
        IndustrySkillPassportReadinessState passportReview,
        IndustrySkillPassportReadinessState automatedDecision,
        IndustrySkillPassportReadinessState talentDataSourceDependency,
        IndustrySkillPassportReadinessState consentPolicyDependency,
        IndustrySkillPassportReadinessState certificationSourceDependency,
        IndustrySkillPassportReadinessState notificationDependency,
        IndustrySkillPassportReadinessState consent,
        IndustrySkillPassportReadinessState dataMinimization,
        IndustrySkillPassportReadinessState retention,
        IndustrySkillPassportReadinessState evidence,
        IReadOnlyDictionary<string, IndustrySkillPassportReadinessState> dependencyStates)
    {
        var required = new[]
        {
            skillClaimCatalog,
            attestationIntake,
            verificationScope,
            visibilityControl,
            passportReview,
            automatedDecision,
            talentDataSourceDependency,
            consentPolicyDependency,
            certificationSourceDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(IndustrySkillPassportReadinessState state) =>
        state is IndustrySkillPassportReadinessState.Ready or IndustrySkillPassportReadinessState.NotRequired;

    private static void ValidateState(IndustrySkillPassportReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(IndustrySkillPassportReadinessCreateRequest request)
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
