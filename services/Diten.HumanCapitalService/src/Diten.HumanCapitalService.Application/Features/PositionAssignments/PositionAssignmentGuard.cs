using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.SensitiveAccess;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using System.Text.RegularExpressions;

namespace Diten.HumanCapitalService.Application.Features.PositionAssignments;

public static class PositionAssignmentGuard
{
    public const string OwnerKey = "hcm.position-assignments";
    public const string ReadPermission = "hcm.position-assignments.read";
    public const string ManagePermission = "hcm.position-assignments.manage";
    public const string ArchivePermission = "hcm.position-assignments.archive";
    public const string ReferenceLinkManagePermission = "hcm.position-assignments.reference-link.manage";

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
        "home_address"
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

    public static IReadOnlyList<string> Validate(PositionAssignmentCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.EmployeeProjectionId == Guid.Empty)
        {
            errors.Add("EmployeeProjectionId is required.");
        }

        if (request.PersonReferenceId == Guid.Empty)
        {
            errors.Add("PersonReferenceId is required.");
        }

        if (request.EffectiveFrom == default)
        {
            errors.Add("EffectiveFrom is required.");
        }

        if (request.EffectiveTo is { } effectiveTo && effectiveTo <= request.EffectiveFrom)
        {
            errors.Add("EffectiveTo must be after EffectiveFrom.");
        }

        if (request.AssignmentVersion < 1)
        {
            errors.Add("AssignmentVersion must be greater than zero.");
        }

        if (!Enum.IsDefined(request.AssignmentState))
        {
            errors.Add("AssignmentState is not supported.");
        }

        if (request.AssignmentState == AssignmentOverlayState.Archived)
        {
            errors.Add("Archived state is controlled by archive operation.");
        }

        if (ForbiddenValues(request).Any(value => ContainsForbiddenMarker(value)))
        {
            errors.Add("Assignment overlay metadata cannot contain raw payload, credential, payroll, bank, tax, payslip, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static async Task<Response<AssignmentValidationDecision>> ValidateReferencesAsync(
        Guid tenantId,
        PositionAssignmentCreateRequest request,
        IEmployeeProjectionRepository employeeRepository,
        IPositionAssignmentReferenceValidator referenceValidator,
        CancellationToken ct)
    {
        var employee = await employeeRepository.GetByIdAsync(tenantId, request.EmployeeProjectionId, ct);
        if (employee is null)
        {
            return Response<AssignmentValidationDecision>.Fail("Employee projection anchor was not found.", 404);
        }

        if (request.ManagerEmployeeProjectionId is { } managerId)
        {
            var manager = await employeeRepository.GetByIdAsync(tenantId, managerId, ct);
            if (manager is null)
            {
                return Response<AssignmentValidationDecision>.Fail("Manager employee projection anchor was not found.", 404);
            }
        }

        var pssResults = new List<ReferenceValidationResult>
        {
            await referenceValidator.ValidatePersonReferenceAsync(tenantId, request.PersonReferenceId, ct)
        };

        if (request.OrganizationUnitId is { } organizationUnitId)
        {
            pssResults.Add(await referenceValidator.ValidateOrganizationUnitAsync(tenantId, organizationUnitId, ct));
        }

        if (request.PositionId is { } positionId)
        {
            pssResults.Add(await referenceValidator.ValidatePositionAsync(tenantId, positionId, ct));
        }

        if (pssResults.Any(result => result.IsAvailable && !result.IsValid))
        {
            return Response<AssignmentValidationDecision>.Fail("Referenced person, organization unit, or position was not found for this tenant.", 404);
        }

        if (pssResults.Any(result => !result.IsAvailable))
        {
            return request.AssignmentState is AssignmentOverlayState.Validated or AssignmentOverlayState.Active
                ? Response<AssignmentValidationDecision>.Fail("PSS reference validation contract is unavailable; Validated or Active assignment state is fail-closed.", 404)
                : Response<AssignmentValidationDecision>.Success(AssignmentValidationDecision.Deferred("PSS reference validation deferred or unavailable."));
        }

        return Response<AssignmentValidationDecision>.Success(AssignmentValidationDecision.Validated());
    }

    public static async Task<AssignmentSensitiveAccessDecisionState> EvaluateSensitiveAccessAsync(
        Guid tenantId,
        EmployeeProfileProjection employee,
        ISensitiveAccessDataScopeEvaluator dataScopeEvaluator,
        CancellationToken ct)
    {
        var dataScope = await dataScopeEvaluator.EvaluateAsync(tenantId, employee, ct);
        if (!dataScope.IsAvailable)
        {
            return AssignmentSensitiveAccessDecisionState.Deferred;
        }

        return dataScope.IsInScope
            ? AssignmentSensitiveAccessDecisionState.Allowed
            : AssignmentSensitiveAccessDecisionState.Denied;
    }

    public static bool IsSensitiveAccessAllowedForMutation(AssignmentSensitiveAccessDecisionState state) =>
        state == AssignmentSensitiveAccessDecisionState.Allowed;

    public static bool IsBroadReadAllowed(EmployeeProfileProjection employee, AssignmentSensitiveAccessDecisionState state) =>
        employee.VisibilityClassification == EmployeeVisibilityClassification.StandardHr
        && state == AssignmentSensitiveAccessDecisionState.Allowed;

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

    private static IEnumerable<string> ForbiddenValues(PositionAssignmentCreateRequest request)
    {
        yield return request.Code;
        yield return request.SourceContractVersion;
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

public sealed record AssignmentValidationDecision(
    AssignmentReferenceValidationState ReferenceState,
    string? DeferredReason)
{
    public static AssignmentValidationDecision Validated() =>
        new(AssignmentReferenceValidationState.Validated, null);

    public static AssignmentValidationDecision Deferred(string reason) =>
        new(AssignmentReferenceValidationState.Deferred, reason);
}
