using System.Text.RegularExpressions;
using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.IndustryTalentPool;

public static class IndustryTalentPoolGuard
{
    public const string OwnerKey = "tep.industry-talent-pool";
    public const string ReadPermission = "tep.industry-talent-pool.read";
    public const string ManagePermission = "tep.industry-talent-pool.manage";
    public const string EvaluatePermission = "tep.industry-talent-pool.evaluate";
    public const string AuditReadPermission = "tep.industry-talent-pool.audit.read";

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
    // "industry-talent-pool" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(IndustryTalentPoolReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.IndustryTalentPoolReadinessVersion < 1)
        {
            errors.Add("IndustryTalentPoolReadinessVersion must be greater than zero.");
        }

        ValidateState(request.IndustryTalentPoolReadinessState, nameof(request.IndustryTalentPoolReadinessState), errors);
        ValidateState(request.PoolMembershipCatalogBoundaryState, nameof(request.PoolMembershipCatalogBoundaryState), errors);
        ValidateState(request.CandidateInclusionIntakeBoundaryState, nameof(request.CandidateInclusionIntakeBoundaryState), errors);
        ValidateState(request.EligibilityScopeBoundaryState, nameof(request.EligibilityScopeBoundaryState), errors);
        ValidateState(request.VisibilityControlBoundaryState, nameof(request.VisibilityControlBoundaryState), errors);
        ValidateState(request.PoolCurationReviewBoundaryState, nameof(request.PoolCurationReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.TalentDataSourceDependencyState, nameof(request.TalentDataSourceDependencyState), errors);
        ValidateState(request.ConsentPolicyDependencyState, nameof(request.ConsentPolicyDependencyState), errors);
        ValidateState(request.ReputationSourceDependencyState, nameof(request.ReputationSourceDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EligibilityPolicyState, nameof(request.EligibilityPolicyState), errors);

        if (request.IndustryTalentPoolReadinessState == IndustryTalentPoolReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.PoolMembershipCatalogBoundaryState == IndustryTalentPoolReadinessState.Ready)
        {
            errors.Add("Pool membership catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.CandidateInclusionIntakeBoundaryState == IndustryTalentPoolReadinessState.Ready)
        {
            errors.Add("Candidate inclusion intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.EligibilityScopeBoundaryState == IndustryTalentPoolReadinessState.Ready)
        {
            errors.Add("Eligibility scope cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.VisibilityControlBoundaryState == IndustryTalentPoolReadinessState.Ready)
        {
            errors.Add("Visibility control cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.PoolCurationReviewBoundaryState == IndustryTalentPoolReadinessState.Ready)
        {
            errors.Add("Pool curation review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == IndustryTalentPoolReadinessState.Ready)
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
            errors.Add("Industry talent pool readiness metadata cannot contain pool membership rosters, candidate/individual PII or contact details, resume/CV or profile content, eligibility or ranking scores, reputation scores, free-text notes, narrative, attachments, document payloads, ratings, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static IndustryTalentPoolReadinessState ResolveFailClosedReadinessState(IndustryTalentPoolReadinessCreateRequest request)
    {
        if (request.IndustryTalentPoolReadinessState != IndustryTalentPoolReadinessState.Ready)
        {
            return request.IndustryTalentPoolReadinessState;
        }

        return ArePreconditionsReady(
            request.PoolMembershipCatalogBoundaryState,
            request.CandidateInclusionIntakeBoundaryState,
            request.EligibilityScopeBoundaryState,
            request.VisibilityControlBoundaryState,
            request.PoolCurationReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.TalentDataSourceDependencyState,
            request.ConsentPolicyDependencyState,
            request.ReputationSourceDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EligibilityPolicyState,
            request.DependencyStates)
            ? IndustryTalentPoolReadinessState.Ready
            : IndustryTalentPoolReadinessState.Deferred;
    }

    public static void ApplyEvaluation(IndustryTalentPoolReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.IndustryTalentPoolReadinessState = ArePreconditionsReady(
            entity.PoolMembershipCatalogBoundaryState,
            entity.CandidateInclusionIntakeBoundaryState,
            entity.EligibilityScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.PoolCurationReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.ReputationSourceDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EligibilityPolicyState,
            entity.DependencyStates)
            ? IndustryTalentPoolReadinessState.Ready
            : IndustryTalentPoolReadinessState.Deferred;

        entity.DeferredReason = entity.IndustryTalentPoolReadinessState == IndustryTalentPoolReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Industry talent pool readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        IndustryTalentPoolReadinessState poolMembershipCatalog,
        IndustryTalentPoolReadinessState candidateInclusionIntake,
        IndustryTalentPoolReadinessState eligibilityScope,
        IndustryTalentPoolReadinessState visibilityControl,
        IndustryTalentPoolReadinessState poolCurationReview,
        IndustryTalentPoolReadinessState automatedDecision,
        IndustryTalentPoolReadinessState talentDataSourceDependency,
        IndustryTalentPoolReadinessState consentPolicyDependency,
        IndustryTalentPoolReadinessState reputationSourceDependency,
        IndustryTalentPoolReadinessState notificationDependency,
        IndustryTalentPoolReadinessState consent,
        IndustryTalentPoolReadinessState dataMinimization,
        IndustryTalentPoolReadinessState retention,
        IndustryTalentPoolReadinessState evidence,
        IReadOnlyDictionary<string, IndustryTalentPoolReadinessState> dependencyStates)
    {
        var required = new[]
        {
            poolMembershipCatalog,
            candidateInclusionIntake,
            eligibilityScope,
            visibilityControl,
            poolCurationReview,
            automatedDecision,
            talentDataSourceDependency,
            consentPolicyDependency,
            reputationSourceDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(IndustryTalentPoolReadinessState state) =>
        state is IndustryTalentPoolReadinessState.Ready or IndustryTalentPoolReadinessState.NotRequired;

    private static void ValidateState(IndustryTalentPoolReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(IndustryTalentPoolReadinessCreateRequest request)
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
