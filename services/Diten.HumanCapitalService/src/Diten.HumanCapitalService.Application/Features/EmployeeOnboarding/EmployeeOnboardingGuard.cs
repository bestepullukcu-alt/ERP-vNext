using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using System.Text.RegularExpressions;

namespace Diten.HumanCapitalService.Application.Features.EmployeeOnboarding;

public static class EmployeeOnboardingGuard
{
    public const string OwnerKey = "hcm.employee-onboarding";
    public const string ReadPermission = "hcm.employee-onboarding.read";
    public const string ManagePermission = "hcm.employee-onboarding.manage";
    public const string EvaluatePermission = "hcm.employee-onboarding.evaluate";
    public const string AuditReadPermission = "hcm.employee-onboarding.audit.read";

    private static readonly string[] ForbiddenMarkers =
    [
        "taskbody",
        "task_body",
        "checklistpayload",
        "checklist_payload",
        "actionpayload",
        "action_payload",
        "managernote",
        "manager_note",
        "hrnote",
        "hr_note",
        "employeenote",
        "employee_note",
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
        "provisioningsecret",
        "provisioning_secret",
        "activationcode",
        "activation_code",
        "devicesecret",
        "device_secret",
        "credential",
        "access_token",
        "refresh_token",
        "secret",
        "password",
        "payroll",
        "bank",
        "tax",
        "payslip",
        "salaryamount",
        "salary_amount",
        "salary",
        "wage",
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
    // words such as "taxonomy" (contains "tax") or "scorecard" (contains "score") are NOT
    // falsely rejected, while real markers ("tax", "ssn", "salary", ...) still match. Tested
    // against the RAW value. Underscore is a regex word character, so snake_case markers match.
    private static readonly Regex ForbiddenMarkerRegex = new(
        @"\b(" + string.Join("|", ForbiddenMarkers.Select(Regex.Escape)) + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static Response<Guid> RequireTenant(ITenantContext tenantContext) =>
        tenantContext.TenantId is { } tenantId && tenantId != Guid.Empty
            ? Response<Guid>.Success(tenantId)
            : Response<Guid>.Fail("Tenant context is required.", 401);

    public static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    public static IReadOnlyList<string> Validate(EmployeeOnboardingReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.OnboardingReadinessVersion < 1)
        {
            errors.Add("OnboardingReadinessVersion must be greater than zero.");
        }

        ValidateState(request.OnboardingReadinessState, nameof(request.OnboardingReadinessState), errors);
        ValidateState(request.LifecycleBoundaryState, nameof(request.LifecycleBoundaryState), errors);
        ValidateState(request.ChecklistBoundaryState, nameof(request.ChecklistBoundaryState), errors);
        ValidateState(request.ManagerActionBoundaryState, nameof(request.ManagerActionBoundaryState), errors);
        ValidateState(request.EmployeeActionBoundaryState, nameof(request.EmployeeActionBoundaryState), errors);
        ValidateState(request.CandidateTransitionBoundaryState, nameof(request.CandidateTransitionBoundaryState), errors);
        ValidateState(request.IdentityProvisioningBoundaryState, nameof(request.IdentityProvisioningBoundaryState), errors);
        ValidateState(request.AccessProvisioningBoundaryState, nameof(request.AccessProvisioningBoundaryState), errors);
        ValidateState(request.DeviceEquipmentProvisioningBoundaryState, nameof(request.DeviceEquipmentProvisioningBoundaryState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.OnboardingReadinessState == EmployeeOnboardingReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.LifecycleBoundaryState == EmployeeOnboardingReadinessState.Ready)
        {
            errors.Add("Employee onboarding workflow execution cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ChecklistBoundaryState == EmployeeOnboardingReadinessState.Ready)
        {
            errors.Add("Checklist and task execution cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ManagerActionBoundaryState == EmployeeOnboardingReadinessState.Ready)
        {
            errors.Add("Manager action workflow cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.EmployeeActionBoundaryState == EmployeeOnboardingReadinessState.Ready)
        {
            errors.Add("Employee action workflow cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.IdentityProvisioningBoundaryState == EmployeeOnboardingReadinessState.Ready)
        {
            errors.Add("Identity/account provisioning cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AccessProvisioningBoundaryState == EmployeeOnboardingReadinessState.Ready)
        {
            errors.Add("Access provisioning cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.DeviceEquipmentProvisioningBoundaryState == EmployeeOnboardingReadinessState.Ready)
        {
            errors.Add("Device/equipment provisioning cannot be marked Ready in the first metadata-only slice.");
        }

        foreach (var pair in request.DependencyStates)
        {
            RequireText(pair.Key, nameof(request.DependencyStates), 80, errors);
            ValidateState(pair.Value, $"{nameof(request.DependencyStates)}.{pair.Key}", errors);
        }

        if (ForbiddenValues(request).Any(ContainsForbiddenMarker))
        {
            errors.Add("Employee onboarding readiness metadata cannot contain task bodies, checklist payloads, action payloads, free-text narrative, attachments, document payloads, provisioning secrets, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static EmployeeOnboardingReadinessState ResolveFailClosedReadinessState(EmployeeOnboardingReadinessCreateRequest request)
    {
        if (request.OnboardingReadinessState != EmployeeOnboardingReadinessState.Ready)
        {
            return request.OnboardingReadinessState;
        }

        return ArePreconditionsReady(
            request.LifecycleBoundaryState,
            request.ChecklistBoundaryState,
            request.ManagerActionBoundaryState,
            request.EmployeeActionBoundaryState,
            request.CandidateTransitionBoundaryState,
            request.IdentityProvisioningBoundaryState,
            request.AccessProvisioningBoundaryState,
            request.DeviceEquipmentProvisioningBoundaryState,
            request.DocumentDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.DependencyStates)
            ? EmployeeOnboardingReadinessState.Ready
            : EmployeeOnboardingReadinessState.Deferred;
    }

    public static void ApplyEvaluation(EmployeeOnboardingReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.OnboardingReadinessState = ArePreconditionsReady(
            entity.LifecycleBoundaryState,
            entity.ChecklistBoundaryState,
            entity.ManagerActionBoundaryState,
            entity.EmployeeActionBoundaryState,
            entity.CandidateTransitionBoundaryState,
            entity.IdentityProvisioningBoundaryState,
            entity.AccessProvisioningBoundaryState,
            entity.DeviceEquipmentProvisioningBoundaryState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates)
            ? EmployeeOnboardingReadinessState.Ready
            : EmployeeOnboardingReadinessState.Deferred;

        entity.DeferredReason = entity.OnboardingReadinessState == EmployeeOnboardingReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Employee onboarding readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        EmployeeOnboardingReadinessState lifecycle,
        EmployeeOnboardingReadinessState checklist,
        EmployeeOnboardingReadinessState managerAction,
        EmployeeOnboardingReadinessState employeeAction,
        EmployeeOnboardingReadinessState candidateTransition,
        EmployeeOnboardingReadinessState identityProvisioning,
        EmployeeOnboardingReadinessState accessProvisioning,
        EmployeeOnboardingReadinessState deviceEquipmentProvisioning,
        EmployeeOnboardingReadinessState documentDependency,
        EmployeeOnboardingReadinessState notificationDependency,
        EmployeeOnboardingReadinessState consent,
        EmployeeOnboardingReadinessState dataMinimization,
        EmployeeOnboardingReadinessState retention,
        EmployeeOnboardingReadinessState evidence,
        IReadOnlyDictionary<string, EmployeeOnboardingReadinessState> dependencyStates)
    {
        var required = new[]
        {
            lifecycle,
            checklist,
            managerAction,
            employeeAction,
            candidateTransition,
            identityProvisioning,
            accessProvisioning,
            deviceEquipmentProvisioning,
            documentDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(EmployeeOnboardingReadinessState state) =>
        state is EmployeeOnboardingReadinessState.Ready or EmployeeOnboardingReadinessState.NotRequired;

    private static void ValidateState(EmployeeOnboardingReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(EmployeeOnboardingReadinessCreateRequest request)
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
        return !string.IsNullOrEmpty(value) && ForbiddenMarkerRegex.IsMatch(value);
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
