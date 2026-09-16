using System.Text.RegularExpressions;
using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.AssociationOperations;

public static class AssociationOperationsGuard
{
    public const string OwnerKey = "tep.association-operations";
    public const string ReadPermission = "tep.association-operations.read";
    public const string ManagePermission = "tep.association-operations.manage";
    public const string EvaluatePermission = "tep.association-operations.evaluate";
    public const string AuditReadPermission = "tep.association-operations.audit.read";

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
    // "association-operations" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(AssociationOperationsReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.AssociationOperationsReadinessVersion < 1)
        {
            errors.Add("AssociationOperationsReadinessVersion must be greater than zero.");
        }

        ValidateState(request.AssociationOperationsReadinessState, nameof(request.AssociationOperationsReadinessState), errors);
        ValidateState(request.MembershipCatalogBoundaryState, nameof(request.MembershipCatalogBoundaryState), errors);
        ValidateState(request.ServiceBindingIntakeBoundaryState, nameof(request.ServiceBindingIntakeBoundaryState), errors);
        ValidateState(request.ProgramScopeBoundaryState, nameof(request.ProgramScopeBoundaryState), errors);
        ValidateState(request.VisibilityControlBoundaryState, nameof(request.VisibilityControlBoundaryState), errors);
        ValidateState(request.OperationsReviewBoundaryState, nameof(request.OperationsReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.MemberRegistrySourceDependencyState, nameof(request.MemberRegistrySourceDependencyState), errors);
        ValidateState(request.SectorTrendSourceDependencyState, nameof(request.SectorTrendSourceDependencyState), errors);
        ValidateState(request.DataGovernancePolicyDependencyState, nameof(request.DataGovernancePolicyDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.PublicationPolicyState, nameof(request.PublicationPolicyState), errors);

        if (request.AssociationOperationsReadinessState == AssociationOperationsReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.MembershipCatalogBoundaryState == AssociationOperationsReadinessState.Ready)
        {
            errors.Add("Membership catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ServiceBindingIntakeBoundaryState == AssociationOperationsReadinessState.Ready)
        {
            errors.Add("Service binding intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ProgramScopeBoundaryState == AssociationOperationsReadinessState.Ready)
        {
            errors.Add("Program scope cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.VisibilityControlBoundaryState == AssociationOperationsReadinessState.Ready)
        {
            errors.Add("Visibility control cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.OperationsReviewBoundaryState == AssociationOperationsReadinessState.Ready)
        {
            errors.Add("Operations review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == AssociationOperationsReadinessState.Ready)
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
            errors.Add("Association operations readiness metadata cannot contain real membership transactions, dues/fee/payment values or amounts, member counts or rosters, per-member operational data, query results, individual/member PII or contact details, workforce or company rosters, free-text notes, narrative, attachments, document payloads, ratings, scores, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static AssociationOperationsReadinessState ResolveFailClosedReadinessState(AssociationOperationsReadinessCreateRequest request)
    {
        if (request.AssociationOperationsReadinessState != AssociationOperationsReadinessState.Ready)
        {
            return request.AssociationOperationsReadinessState;
        }

        return ArePreconditionsReady(
            request.MembershipCatalogBoundaryState,
            request.ServiceBindingIntakeBoundaryState,
            request.ProgramScopeBoundaryState,
            request.VisibilityControlBoundaryState,
            request.OperationsReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.MemberRegistrySourceDependencyState,
            request.SectorTrendSourceDependencyState,
            request.DataGovernancePolicyDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.PublicationPolicyState,
            request.DependencyStates)
            ? AssociationOperationsReadinessState.Ready
            : AssociationOperationsReadinessState.Deferred;
    }

    public static void ApplyEvaluation(AssociationOperationsReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.AssociationOperationsReadinessState = ArePreconditionsReady(
            entity.MembershipCatalogBoundaryState,
            entity.ServiceBindingIntakeBoundaryState,
            entity.ProgramScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.OperationsReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.MemberRegistrySourceDependencyState,
            entity.SectorTrendSourceDependencyState,
            entity.DataGovernancePolicyDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates)
            ? AssociationOperationsReadinessState.Ready
            : AssociationOperationsReadinessState.Deferred;

        entity.DeferredReason = entity.AssociationOperationsReadinessState == AssociationOperationsReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Association operations readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        AssociationOperationsReadinessState membershipCatalog,
        AssociationOperationsReadinessState serviceBindingIntake,
        AssociationOperationsReadinessState programScope,
        AssociationOperationsReadinessState visibilityControl,
        AssociationOperationsReadinessState operationsReview,
        AssociationOperationsReadinessState automatedDecision,
        AssociationOperationsReadinessState memberRegistrySourceDependency,
        AssociationOperationsReadinessState sectorTrendSourceDependency,
        AssociationOperationsReadinessState dataGovernancePolicyDependency,
        AssociationOperationsReadinessState notificationDependency,
        AssociationOperationsReadinessState consent,
        AssociationOperationsReadinessState dataMinimization,
        AssociationOperationsReadinessState retention,
        AssociationOperationsReadinessState evidence,
        IReadOnlyDictionary<string, AssociationOperationsReadinessState> dependencyStates)
    {
        var required = new[]
        {
            membershipCatalog,
            serviceBindingIntake,
            programScope,
            visibilityControl,
            operationsReview,
            automatedDecision,
            memberRegistrySourceDependency,
            sectorTrendSourceDependency,
            dataGovernancePolicyDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(AssociationOperationsReadinessState state) =>
        state is AssociationOperationsReadinessState.Ready or AssociationOperationsReadinessState.NotRequired;

    private static void ValidateState(AssociationOperationsReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(AssociationOperationsReadinessCreateRequest request)
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
