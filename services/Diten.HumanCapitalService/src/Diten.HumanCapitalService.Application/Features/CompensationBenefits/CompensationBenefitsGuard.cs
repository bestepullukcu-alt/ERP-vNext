using System.Text.RegularExpressions;
using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.CompensationBenefits;

public static class CompensationBenefitsGuard
{
    public const string OwnerKey = "hcm.compensation-benefits";
    public const string ReadPermission = "hcm.compensation-benefits.read";
    public const string ManagePermission = "hcm.compensation-benefits.manage";
    public const string EvaluatePermission = "hcm.compensation-benefits.evaluate";
    public const string AuditReadPermission = "hcm.compensation-benefits.audit.read";

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
    // "compensation-benefits" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(CompensationBenefitsReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.CompensationBenefitsReadinessVersion < 1)
        {
            errors.Add("CompensationBenefitsReadinessVersion must be greater than zero.");
        }

        ValidateState(request.CompensationBenefitsReadinessState, nameof(request.CompensationBenefitsReadinessState), errors);
        ValidateState(request.CompensationPlanBoundaryState, nameof(request.CompensationPlanBoundaryState), errors);
        ValidateState(request.BenefitProgramBoundaryState, nameof(request.BenefitProgramBoundaryState), errors);
        ValidateState(request.PayGradeMappingBoundaryState, nameof(request.PayGradeMappingBoundaryState), errors);
        ValidateState(request.BenefitEnrollmentBoundaryState, nameof(request.BenefitEnrollmentBoundaryState), errors);
        ValidateState(request.CompensationReviewBoundaryState, nameof(request.CompensationReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.CompensationSourceDependencyState, nameof(request.CompensationSourceDependencyState), errors);
        ValidateState(request.BenefitProviderSourceDependencyState, nameof(request.BenefitProviderSourceDependencyState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.CompensationBenefitsReadinessState == CompensationBenefitsReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.CompensationPlanBoundaryState == CompensationBenefitsReadinessState.Ready)
        {
            errors.Add("Compensation plan cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.BenefitProgramBoundaryState == CompensationBenefitsReadinessState.Ready)
        {
            errors.Add("Benefit program cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.PayGradeMappingBoundaryState == CompensationBenefitsReadinessState.Ready)
        {
            errors.Add("Pay grade mapping cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.BenefitEnrollmentBoundaryState == CompensationBenefitsReadinessState.Ready)
        {
            errors.Add("Benefit enrollment cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.CompensationReviewBoundaryState == CompensationBenefitsReadinessState.Ready)
        {
            errors.Add("Compensation review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == CompensationBenefitsReadinessState.Ready)
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
            errors.Add("Compensation and benefits readiness metadata cannot contain salary or compensation amounts, pay figures, wage data, bonus/allowance amounts, benefit elections, payroll data, bank/tax details, scores, ratings, model output, automated decision outputs, free-text notes, narrative, attachments, document payloads, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static CompensationBenefitsReadinessState ResolveFailClosedReadinessState(CompensationBenefitsReadinessCreateRequest request)
    {
        if (request.CompensationBenefitsReadinessState != CompensationBenefitsReadinessState.Ready)
        {
            return request.CompensationBenefitsReadinessState;
        }

        return ArePreconditionsReady(
            request.CompensationPlanBoundaryState,
            request.BenefitProgramBoundaryState,
            request.PayGradeMappingBoundaryState,
            request.BenefitEnrollmentBoundaryState,
            request.CompensationReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.CompensationSourceDependencyState,
            request.BenefitProviderSourceDependencyState,
            request.DocumentDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.DependencyStates)
            ? CompensationBenefitsReadinessState.Ready
            : CompensationBenefitsReadinessState.Deferred;
    }

    public static void ApplyEvaluation(CompensationBenefitsReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.CompensationBenefitsReadinessState = ArePreconditionsReady(
            entity.CompensationPlanBoundaryState,
            entity.BenefitProgramBoundaryState,
            entity.PayGradeMappingBoundaryState,
            entity.BenefitEnrollmentBoundaryState,
            entity.CompensationReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.CompensationSourceDependencyState,
            entity.BenefitProviderSourceDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates)
            ? CompensationBenefitsReadinessState.Ready
            : CompensationBenefitsReadinessState.Deferred;

        entity.DeferredReason = entity.CompensationBenefitsReadinessState == CompensationBenefitsReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Compensation and benefits readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        CompensationBenefitsReadinessState compensationPlan,
        CompensationBenefitsReadinessState benefitProgram,
        CompensationBenefitsReadinessState payGradeMapping,
        CompensationBenefitsReadinessState benefitEnrollment,
        CompensationBenefitsReadinessState compensationReview,
        CompensationBenefitsReadinessState automatedDecision,
        CompensationBenefitsReadinessState compensationSourceDependency,
        CompensationBenefitsReadinessState benefitProviderSourceDependency,
        CompensationBenefitsReadinessState documentDependency,
        CompensationBenefitsReadinessState notificationDependency,
        CompensationBenefitsReadinessState consent,
        CompensationBenefitsReadinessState dataMinimization,
        CompensationBenefitsReadinessState retention,
        CompensationBenefitsReadinessState evidence,
        IReadOnlyDictionary<string, CompensationBenefitsReadinessState> dependencyStates)
    {
        var required = new[]
        {
            compensationPlan,
            benefitProgram,
            payGradeMapping,
            benefitEnrollment,
            compensationReview,
            automatedDecision,
            compensationSourceDependency,
            benefitProviderSourceDependency,
            documentDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(CompensationBenefitsReadinessState state) =>
        state is CompensationBenefitsReadinessState.Ready or CompensationBenefitsReadinessState.NotRequired;

    private static void ValidateState(CompensationBenefitsReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(CompensationBenefitsReadinessCreateRequest request)
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
