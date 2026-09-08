using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.EmploymentChanges;

public static class EmploymentChangeGuard
{
    public const string OwnerKey = "hcm.employment-changes";
    public const string ReadPermission = "hcm.employment-changes.read";
    public const string ManagePermission = "hcm.employment-changes.manage";
    public const string EvaluatePermission = "hcm.employment-changes.evaluate";
    public const string AuditReadPermission = "hcm.employment-changes.audit.read";

    private static readonly string[] ForbiddenMarkers =
    [
        "workflowbody",
        "workflow_body",
        "approvalbody",
        "approval_body",
        "approvaldecision",
        "approval_decision",
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

    public static Response<Guid> RequireTenant(ITenantContext tenantContext) =>
        tenantContext.TenantId is { } tenantId && tenantId != Guid.Empty
            ? Response<Guid>.Success(tenantId)
            : Response<Guid>.Fail("Tenant context is required.", 401);

    public static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    public static IReadOnlyList<string> Validate(EmploymentChangeReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.EmploymentChangeReadinessVersion < 1)
        {
            errors.Add("EmploymentChangeReadinessVersion must be greater than zero.");
        }

        ValidateState(request.EmploymentChangeReadinessState, nameof(request.EmploymentChangeReadinessState), errors);
        ValidateState(request.ChangeLifecycleBoundaryState, nameof(request.ChangeLifecycleBoundaryState), errors);
        ValidateState(request.TransferBoundaryState, nameof(request.TransferBoundaryState), errors);
        ValidateState(request.PromotionBoundaryState, nameof(request.PromotionBoundaryState), errors);
        ValidateState(request.ApprovalBoundaryState, nameof(request.ApprovalBoundaryState), errors);
        ValidateState(request.PositionAssignmentBoundaryState, nameof(request.PositionAssignmentBoundaryState), errors);
        ValidateState(request.EmployeeActionBoundaryState, nameof(request.EmployeeActionBoundaryState), errors);
        ValidateState(request.ManagerActionBoundaryState, nameof(request.ManagerActionBoundaryState), errors);
        ValidateState(request.CompensationDataBoundaryState, nameof(request.CompensationDataBoundaryState), errors);
        ValidateState(request.BenefitsDataBoundaryState, nameof(request.BenefitsDataBoundaryState), errors);
        ValidateState(request.PayrollDataBoundaryState, nameof(request.PayrollDataBoundaryState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.EmploymentChangeReadinessState == EmploymentChangeReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.ChangeLifecycleBoundaryState == EmploymentChangeReadinessState.Ready)
        {
            errors.Add("Employment change workflow execution cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.TransferBoundaryState == EmploymentChangeReadinessState.Ready)
        {
            errors.Add("Transfer workflow cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.PromotionBoundaryState == EmploymentChangeReadinessState.Ready)
        {
            errors.Add("Promotion workflow cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ApprovalBoundaryState == EmploymentChangeReadinessState.Ready)
        {
            errors.Add("Transfer/promotion approval workflow cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.PositionAssignmentBoundaryState == EmploymentChangeReadinessState.Ready)
        {
            errors.Add("Position assignment mutation cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.EmployeeActionBoundaryState == EmploymentChangeReadinessState.Ready)
        {
            errors.Add("Employee action UX cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ManagerActionBoundaryState == EmploymentChangeReadinessState.Ready)
        {
            errors.Add("Manager action UX cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.CompensationDataBoundaryState == EmploymentChangeReadinessState.Ready)
        {
            errors.Add("Compensation payload persistence cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.BenefitsDataBoundaryState == EmploymentChangeReadinessState.Ready)
        {
            errors.Add("Benefits payload persistence cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.PayrollDataBoundaryState == EmploymentChangeReadinessState.Ready)
        {
            errors.Add("Payroll payload persistence cannot be marked Ready in the first metadata-only slice.");
        }

        foreach (var pair in request.DependencyStates)
        {
            RequireText(pair.Key, nameof(request.DependencyStates), 80, errors);
            ValidateState(pair.Value, $"{nameof(request.DependencyStates)}.{pair.Key}", errors);
        }

        if (ForbiddenValues(request).Any(ContainsForbiddenMarker))
        {
            errors.Add("Employment change readiness metadata cannot contain workflow bodies, approval decisions, position mutation payloads, employee/manager notes, free-text narrative, attachments, document payloads, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static EmploymentChangeReadinessState ResolveFailClosedReadinessState(EmploymentChangeReadinessCreateRequest request)
    {
        if (request.EmploymentChangeReadinessState != EmploymentChangeReadinessState.Ready)
        {
            return request.EmploymentChangeReadinessState;
        }

        return ArePreconditionsReady(
            request.ChangeLifecycleBoundaryState,
            request.TransferBoundaryState,
            request.PromotionBoundaryState,
            request.ApprovalBoundaryState,
            request.PositionAssignmentBoundaryState,
            request.EmployeeActionBoundaryState,
            request.ManagerActionBoundaryState,
            request.CompensationDataBoundaryState,
            request.BenefitsDataBoundaryState,
            request.PayrollDataBoundaryState,
            request.DocumentDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.DependencyStates)
            ? EmploymentChangeReadinessState.Ready
            : EmploymentChangeReadinessState.Deferred;
    }

    public static void ApplyEvaluation(EmploymentChangeReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.EmploymentChangeReadinessState = ArePreconditionsReady(
            entity.ChangeLifecycleBoundaryState,
            entity.TransferBoundaryState,
            entity.PromotionBoundaryState,
            entity.ApprovalBoundaryState,
            entity.PositionAssignmentBoundaryState,
            entity.EmployeeActionBoundaryState,
            entity.ManagerActionBoundaryState,
            entity.CompensationDataBoundaryState,
            entity.BenefitsDataBoundaryState,
            entity.PayrollDataBoundaryState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates)
            ? EmploymentChangeReadinessState.Ready
            : EmploymentChangeReadinessState.Deferred;

        entity.DeferredReason = entity.EmploymentChangeReadinessState == EmploymentChangeReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Employment change readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        EmploymentChangeReadinessState lifecycle,
        EmploymentChangeReadinessState transfer,
        EmploymentChangeReadinessState promotion,
        EmploymentChangeReadinessState approval,
        EmploymentChangeReadinessState positionAssignment,
        EmploymentChangeReadinessState employeeAction,
        EmploymentChangeReadinessState managerAction,
        EmploymentChangeReadinessState compensation,
        EmploymentChangeReadinessState benefits,
        EmploymentChangeReadinessState payroll,
        EmploymentChangeReadinessState documentDependency,
        EmploymentChangeReadinessState notificationDependency,
        EmploymentChangeReadinessState consent,
        EmploymentChangeReadinessState dataMinimization,
        EmploymentChangeReadinessState retention,
        EmploymentChangeReadinessState evidence,
        IReadOnlyDictionary<string, EmploymentChangeReadinessState> dependencyStates)
    {
        var required = new[]
        {
            lifecycle,
            transfer,
            promotion,
            approval,
            positionAssignment,
            employeeAction,
            managerAction,
            compensation,
            benefits,
            payroll,
            documentDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(EmploymentChangeReadinessState state) =>
        state is EmploymentChangeReadinessState.Ready or EmploymentChangeReadinessState.NotRequired;

    private static void ValidateState(EmploymentChangeReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(EmploymentChangeReadinessCreateRequest request)
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

    private static bool ContainsForbiddenMarker(string value)
    {
        var normalized = Normalize(value);
        return ForbiddenMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal));
    }

    private static string Normalize(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        var index = 0;

        foreach (var current in value)
        {
            if (char.IsLetterOrDigit(current) || current == '_')
            {
                buffer[index++] = char.ToLowerInvariant(current);
            }
        }

        return new string(buffer[..index]);
    }
}
