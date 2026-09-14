using System.Text.RegularExpressions;
using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.SelfService;

public static class SelfServiceGuard
{
    public const string OwnerKey = "hcm.self-service";
    public const string ReadPermission = "hcm.self-service.read";
    public const string ManagePermission = "hcm.self-service.manage";
    public const string EvaluatePermission = "hcm.self-service.evaluate";
    public const string AuditReadPermission = "hcm.self-service.audit.read";

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
    // "self-service" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(SelfServiceReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.SelfServiceReadinessVersion < 1)
        {
            errors.Add("SelfServiceReadinessVersion must be greater than zero.");
        }

        ValidateState(request.SelfServiceReadinessState, nameof(request.SelfServiceReadinessState), errors);
        ValidateState(request.RequestIntakeBoundaryState, nameof(request.RequestIntakeBoundaryState), errors);
        ValidateState(request.ApprovalRoutingBoundaryState, nameof(request.ApprovalRoutingBoundaryState), errors);
        ValidateState(request.InboxDeliveryBoundaryState, nameof(request.InboxDeliveryBoundaryState), errors);
        ValidateState(request.ProfileSelfUpdateBoundaryState, nameof(request.ProfileSelfUpdateBoundaryState), errors);
        ValidateState(request.DelegationScopeBoundaryState, nameof(request.DelegationScopeBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.IdentityDirectoryDependencyState, nameof(request.IdentityDirectoryDependencyState), errors);
        ValidateState(request.HcmCapabilityDependencyState, nameof(request.HcmCapabilityDependencyState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.SelfServiceReadinessState == SelfServiceReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.RequestIntakeBoundaryState == SelfServiceReadinessState.Ready)
        {
            errors.Add("Request intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ApprovalRoutingBoundaryState == SelfServiceReadinessState.Ready)
        {
            errors.Add("Approval routing cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.InboxDeliveryBoundaryState == SelfServiceReadinessState.Ready)
        {
            errors.Add("Inbox delivery cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ProfileSelfUpdateBoundaryState == SelfServiceReadinessState.Ready)
        {
            errors.Add("Profile self-update cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.DelegationScopeBoundaryState == SelfServiceReadinessState.Ready)
        {
            errors.Add("Delegation scope cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == SelfServiceReadinessState.Ready)
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
            errors.Add("Employee and manager self-service readiness metadata cannot contain self-service request bodies, form submissions, approval decisions, manager notes, employee statements, free-text notes, narrative, attachments, document payloads, scores, ratings, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static SelfServiceReadinessState ResolveFailClosedReadinessState(SelfServiceReadinessCreateRequest request)
    {
        if (request.SelfServiceReadinessState != SelfServiceReadinessState.Ready)
        {
            return request.SelfServiceReadinessState;
        }

        return ArePreconditionsReady(
            request.RequestIntakeBoundaryState,
            request.ApprovalRoutingBoundaryState,
            request.InboxDeliveryBoundaryState,
            request.ProfileSelfUpdateBoundaryState,
            request.DelegationScopeBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.IdentityDirectoryDependencyState,
            request.HcmCapabilityDependencyState,
            request.DocumentDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.DependencyStates)
            ? SelfServiceReadinessState.Ready
            : SelfServiceReadinessState.Deferred;
    }

    public static void ApplyEvaluation(SelfServiceReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.SelfServiceReadinessState = ArePreconditionsReady(
            entity.RequestIntakeBoundaryState,
            entity.ApprovalRoutingBoundaryState,
            entity.InboxDeliveryBoundaryState,
            entity.ProfileSelfUpdateBoundaryState,
            entity.DelegationScopeBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.IdentityDirectoryDependencyState,
            entity.HcmCapabilityDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates)
            ? SelfServiceReadinessState.Ready
            : SelfServiceReadinessState.Deferred;

        entity.DeferredReason = entity.SelfServiceReadinessState == SelfServiceReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Employee and manager self-service readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        SelfServiceReadinessState requestIntake,
        SelfServiceReadinessState approvalRouting,
        SelfServiceReadinessState inboxDelivery,
        SelfServiceReadinessState profileSelfUpdate,
        SelfServiceReadinessState delegationScope,
        SelfServiceReadinessState automatedDecision,
        SelfServiceReadinessState identityDirectoryDependency,
        SelfServiceReadinessState hcmCapabilityDependency,
        SelfServiceReadinessState documentDependency,
        SelfServiceReadinessState notificationDependency,
        SelfServiceReadinessState consent,
        SelfServiceReadinessState dataMinimization,
        SelfServiceReadinessState retention,
        SelfServiceReadinessState evidence,
        IReadOnlyDictionary<string, SelfServiceReadinessState> dependencyStates)
    {
        var required = new[]
        {
            requestIntake,
            approvalRouting,
            inboxDelivery,
            profileSelfUpdate,
            delegationScope,
            automatedDecision,
            identityDirectoryDependency,
            hcmCapabilityDependency,
            documentDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(SelfServiceReadinessState state) =>
        state is SelfServiceReadinessState.Ready or SelfServiceReadinessState.NotRequired;

    private static void ValidateState(SelfServiceReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(SelfServiceReadinessCreateRequest request)
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
