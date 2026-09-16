using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.PositionAssignments;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using System.Text.RegularExpressions;

namespace Diten.HumanCapitalService.Application.Features.OffboardingCases;

public static class OffboardingCaseGuard
{
    public const string OwnerKey = "hcm.offboarding";
    public const string ReadPermission = "hcm.offboarding.read";
    public const string ManagePermission = "hcm.offboarding.manage";
    public const string ReviewPermission = "hcm.offboarding.review";
    public const string ArchivePermission = "hcm.offboarding.archive";
    public const string HandoffManagePermission = "hcm.offboarding.handoff.manage";

    private static readonly string[] ForbiddenMarkers =
    [
        "rawpayload",
        "raw_payload",
        "providerresponse",
        "provider_response",
        "credential",
        "access_token",
        "refresh_token",
        "secret",
        "password",
        "payroll",
        "timeattendance",
        "time_attendance",
        "bank",
        "tax",
        "payslip",
        "biometric",
        "geolocation",
        "nationalid",
        "national_id",
        "dateofbirth",
        "dob",
        "homeaddress",
        "home_address",
        "candidateidentity",
        "candidate_identity",
        "talentidentity",
        "talent_identity"
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

    public static IReadOnlyList<string> Validate(OffboardingCaseCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.ExitReasonCode, nameof(request.ExitReasonCode), 64, errors);
        RequireText(request.ExitTypeCode, nameof(request.ExitTypeCode), 64, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.EmployeeProjectionId == Guid.Empty)
        {
            errors.Add("EmployeeProjectionId is required.");
        }

        if (request.PlannedExitDate == default)
        {
            errors.Add("PlannedExitDate is required.");
        }

        if (request.NoticeDate is { } noticeDate && noticeDate > request.PlannedExitDate)
        {
            errors.Add("NoticeDate must not be after PlannedExitDate.");
        }

        if (request.ActualExitDate is { } actualExitDate && request.NoticeDate is { } notice && actualExitDate < notice)
        {
            errors.Add("ActualExitDate must not be before NoticeDate.");
        }

        if (request.OffboardingVersion < 1)
        {
            errors.Add("OffboardingVersion must be greater than zero.");
        }

        if (!Enum.IsDefined(request.OffboardingState))
        {
            errors.Add("OffboardingState is not supported.");
        }

        if (!Enum.IsDefined(request.ChecklistState))
        {
            errors.Add("ChecklistState is not supported.");
        }

        if (!Enum.IsDefined(request.DependencyDecisionState))
        {
            errors.Add("DependencyDecisionState is not supported.");
        }

        if (!Enum.IsDefined(request.TepHandoffState))
        {
            errors.Add("TepHandoffState is not supported.");
        }

        if (request.OffboardingState == OffboardingState.Archived)
        {
            errors.Add("Archived state is controlled by archive operation.");
        }

        if (request.TepHandoffState == OffboardingTepHandoffState.Transferred)
        {
            errors.Add("Transferred TEP handoff state is not authorized in the first local metadata slice.");
        }

        if (request.OffboardingState == OffboardingState.Completed && request.ActualExitDate is null)
        {
            errors.Add("ActualExitDate is required before Completed state.");
        }

        if (request.OffboardingState == OffboardingState.Completed
            && request.ChecklistState != OffboardingChecklistState.Completed)
        {
            errors.Add("ChecklistState must be Completed before Completed state.");
        }

        if (request.OffboardingState == OffboardingState.Completed
            && request.DependencyDecisionState == OffboardingDependencyDecisionState.Unavailable)
        {
            errors.Add("Unavailable dependency metadata cannot complete offboarding.");
        }

        if (request.OffboardingState == OffboardingState.TepHandoffReady
            && request.TepHandoffState is OffboardingTepHandoffState.NotRequired or OffboardingTepHandoffState.Transferred)
        {
            errors.Add("TepHandoffReady requires Planned, Ready, or Deferred local handoff metadata.");
        }

        if (ForbiddenValues(request).Any(value => ContainsForbiddenMarker(value)))
        {
            errors.Add("Offboarding metadata cannot contain raw payload, credential, payroll, time/attendance, bank, tax, payslip, biometric, geolocation, national ID, DOB, home address, TEP identity, or PII-heavy markers.");
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateReview(OffboardingCaseReviewRequest request)
    {
        var errors = new List<string>();
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (!Enum.IsDefined(request.OffboardingState))
        {
            errors.Add("OffboardingState is not supported.");
        }

        if (!Enum.IsDefined(request.DependencyDecisionState))
        {
            errors.Add("DependencyDecisionState is not supported.");
        }

        if (request.OffboardingState is OffboardingState.Archived or OffboardingState.Completed)
        {
            errors.Add("Review operation cannot archive or complete an offboarding case.");
        }

        if (request.OffboardingVersion < 1)
        {
            errors.Add("OffboardingVersion must be greater than zero.");
        }

        if (ContainsForbiddenMarker(request.SourceContractVersion))
        {
            errors.Add("Review metadata cannot contain forbidden raw or sensitive markers.");
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateReviewTransition(OffboardingCaseReviewRequest request, OffboardingCase entity)
    {
        var errors = new List<string>();

        if (request.OffboardingState == OffboardingState.TepHandoffReady
            && entity.TepHandoffState is not (OffboardingTepHandoffState.Planned or OffboardingTepHandoffState.Ready or OffboardingTepHandoffState.Deferred))
        {
            errors.Add("TepHandoffReady requires existing Planned, Ready, or Deferred local handoff metadata.");
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateHandoff(OffboardingCaseHandoffRequest request)
    {
        var errors = new List<string>();
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (!Enum.IsDefined(request.TepHandoffState))
        {
            errors.Add("TepHandoffState is not supported.");
        }

        if (request.TepHandoffState == OffboardingTepHandoffState.Transferred)
        {
            errors.Add("Transferred TEP handoff state is not authorized in the first local metadata slice.");
        }

        if (request.OffboardingVersion < 1)
        {
            errors.Add("OffboardingVersion must be greater than zero.");
        }

        if (ForbiddenValues(request).Any(value => ContainsForbiddenMarker(value)))
        {
            errors.Add("TEP handoff metadata cannot contain raw payload, credential, payroll, time/attendance, TEP identity, or PII-heavy markers.");
        }

        return errors;
    }

    public static async Task<Response<EmployeeProfileProjection>> ValidateEmployeeAnchorAsync(
        Guid tenantId,
        Guid employeeProjectionId,
        IEmployeeProjectionRepository employeeRepository,
        IReadOnlyCollection<Guid> legalEntityIds,
        CancellationToken ct)
    {
        var employee = await employeeRepository.GetByIdAsync(tenantId, legalEntityIds, employeeProjectionId, ct);
        return employee is null
            ? Response<EmployeeProfileProjection>.Fail("Employee projection anchor was not found.", 404)
            : Response<EmployeeProfileProjection>.Success(employee);
    }

    public static async Task<Response<AssignmentContextDecision>> ValidateAssignmentContextAsync(
        Guid tenantId,
        Guid? assignmentOverlayId,
        IPositionAssignmentOverlayRepository assignmentRepository,
        IReadOnlyCollection<Guid> legalEntityIds,
        CancellationToken ct)
    {
        if (assignmentOverlayId is null)
        {
            return Response<AssignmentContextDecision>.Success(AssignmentContextDecision.Deferred("Assignment overlay context was not supplied."));
        }

        var assignment = await assignmentRepository.GetByIdAsync(tenantId, legalEntityIds, assignmentOverlayId.Value, ct);
        return assignment is null
            ? Response<AssignmentContextDecision>.Fail("Assignment overlay context was not found.", 404)
            : Response<AssignmentContextDecision>.Success(AssignmentContextDecision.Validated());
    }

    public static async Task<OffboardingSensitiveAccessDecisionState> EvaluateSensitiveAccessAsync(
        Guid tenantId,
        EmployeeProfileProjection employee,
        ISensitiveAccessDataScopeEvaluator dataScopeEvaluator,
        CancellationToken ct)
    {
        var dataScope = await dataScopeEvaluator.EvaluateAsync(tenantId, employee, ct);
        if (!dataScope.IsAvailable)
        {
            return OffboardingSensitiveAccessDecisionState.Deferred;
        }

        return dataScope.IsInScope
            ? OffboardingSensitiveAccessDecisionState.Allowed
            : OffboardingSensitiveAccessDecisionState.Denied;
    }

    public static bool IsSensitiveAccessAllowedForMutation(OffboardingSensitiveAccessDecisionState state) =>
        state == OffboardingSensitiveAccessDecisionState.Allowed;

    public static bool IsBroadReadAllowed(EmployeeProfileProjection employee, OffboardingSensitiveAccessDecisionState state) =>
        employee.VisibilityClassification == EmployeeVisibilityClassification.StandardHr
        && state == OffboardingSensitiveAccessDecisionState.Allowed;

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

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

    private static IEnumerable<string> ForbiddenValues(OffboardingCaseCreateRequest request)
    {
        yield return request.Code;
        yield return request.ExitReasonCode;
        yield return request.ExitTypeCode;
        yield return request.SourceContractVersion;
        yield return request.TepHandoffReferenceKey ?? string.Empty;
    }

    private static IEnumerable<string> ForbiddenValues(OffboardingCaseHandoffRequest request)
    {
        yield return request.SourceContractVersion;
        yield return request.TepHandoffReferenceKey ?? string.Empty;
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

public sealed record AssignmentContextDecision(bool IsValidated, string? DeferredReason)
{
    public static AssignmentContextDecision Validated() => new(true, null);

    public static AssignmentContextDecision Deferred(string reason) => new(false, reason);
}
