using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.EmployeeProjections;

internal static partial class EmployeeProjectionGuards
{
    public static Guid RequireTenant(ITenantContext tenantContext)
    {
        if (tenantContext.TenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        return tenantId;
    }

    public static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    public static IReadOnlyList<string> ValidateRequest(EmployeeProjectionCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 160, errors);
        RequireText(request.ExternalEmployeeReference, nameof(request.ExternalEmployeeReference), 160, errors);
        RequireText(request.EmploymentRecordReferenceKey, nameof(request.EmploymentRecordReferenceKey), 160, errors);
        RequireText(request.EmploymentStatusCode, nameof(request.EmploymentStatusCode), 64, errors);
        RequireText(request.WorkerTypeCode, nameof(request.WorkerTypeCode), 64, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 64, errors);

        if (request.HrisSourceProfileId == Guid.Empty)
        {
            errors.Add("HrisSourceProfileId is required.");
        }

        if (request.PersonReferenceId == Guid.Empty)
        {
            errors.Add("PersonReferenceId is required.");
        }

        if (request.VisibilityClassification is null)
        {
            errors.Add("VisibilityClassification is required.");
        }

        if (request.ProjectionVersion < 1)
        {
            errors.Add("ProjectionVersion must be greater than zero.");
        }

        foreach (var value in EnumerateStringValues(request))
        {
            if (LooksForbidden(value))
            {
                errors.Add("Request contains forbidden raw, secret, payroll, provider, or PII-heavy markers.");
                break;
            }
        }

        if (request.ExtensionData is { Count: > 0 })
        {
            foreach (var extension in request.ExtensionData)
            {
                if (LooksForbidden(extension.Key) || LooksForbidden(JsonValue(extension.Value)))
                {
                    errors.Add("Request contains forbidden extra fields.");
                    break;
                }
            }
        }

        return errors;
    }

    public static async Task<Response<EmployeeProjectionState>> ResolveReferenceStateAsync(
        Guid tenantId,
        EmployeeProjectionCreateRequest request,
        IEmployeeProjectionReferenceValidator referenceValidator,
        CancellationToken ct)
    {
        var hris = await referenceValidator.ValidateHrisSourceProfileAsync(tenantId, request.HrisSourceProfileId, ct);
        var person = await referenceValidator.ValidatePersonReferenceAsync(tenantId, request.PersonReferenceId, ct);

        if ((hris.IsAvailable && !hris.IsValid) || (person.IsAvailable && !person.IsValid))
        {
            return Response<EmployeeProjectionState>.Fail("Referenced HRIS source or person reference was not found for this tenant.", 404);
        }

        var hasUnavailableContract = !hris.IsAvailable || !person.IsAvailable;
        if (hasUnavailableContract)
        {
            return request.ProjectionState == EmployeeProjectionState.Validated
                ? Response<EmployeeProjectionState>.Fail("Reference validation contract is unavailable; Validated state is fail-closed.", 404)
                : Response<EmployeeProjectionState>.Success(EmployeeProjectionState.Deferred);
        }

        return Response<EmployeeProjectionState>.Success(request.ProjectionState);
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

    private static IEnumerable<string?> EnumerateStringValues(EmployeeProjectionCreateRequest request)
    {
        yield return request.Code;
        yield return request.DisplayName;
        yield return request.ExternalEmployeeReference;
        yield return request.EmploymentRecordReferenceKey;
        yield return request.EmploymentStatusCode;
        yield return request.WorkerTypeCode;
        yield return request.SourceContractVersion;
    }

    private static bool LooksForbidden(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        return LooksLikeRawPayload(candidate)
               || SecretPattern().IsMatch(candidate)
               || PiiHeavyPattern().IsMatch(candidate)
               || PayrollProviderPattern().IsMatch(candidate)
               || JwtPattern().IsMatch(candidate)
               || PemPattern().IsMatch(candidate);
    }

    private static string JsonValue(JsonElement element) =>
        element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.GetRawText();

    private static bool LooksLikeRawPayload(string candidate) =>
        (candidate.StartsWith('{') && candidate.EndsWith('}'))
        || (candidate.StartsWith('[') && candidate.EndsWith(']'))
        || candidate.Contains("<worker>", StringComparison.OrdinalIgnoreCase)
        || candidate.Contains("<employee>", StringComparison.OrdinalIgnoreCase)
        || candidate.Contains("<person>", StringComparison.OrdinalIgnoreCase)
        || candidate.Contains("<payroll>", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex("(password|passwd|secret|token|apikey|api_key|client_secret|refresh_token|access_token|credential|bearer\\s+)", RegexOptions.IgnoreCase)]
    private static partial Regex SecretPattern();

    [GeneratedRegex("(date_of_birth|dob|birthdate|national_id|ssn|social_security|passport|home_address|street_address|bank|iban|swift|tax|payslip|salary|compensation|biometric|fingerprint|faceprint|geolocation|latitude|longitude)", RegexOptions.IgnoreCase)]
    private static partial Regex PiiHeavyPattern();

    [GeneratedRegex("(payroll|time_attendance|attendance|timesheet|provider_adapter|adapter_client|webhook|background_sync|candidate|talent_profile|requisition|onboarding|offboarding|benefits|performance|compensation)", RegexOptions.IgnoreCase)]
    private static partial Regex PayrollProviderPattern();

    [GeneratedRegex("^[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+$")]
    private static partial Regex JwtPattern();

    [GeneratedRegex("-----BEGIN [A-Z ]+PRIVATE KEY-----", RegexOptions.IgnoreCase)]
    private static partial Regex PemPattern();
}
