using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.TepShell;

public static class TepShellGuard
{
    private static readonly string[] ForbiddenMarkers =
    [
        "candidate",
        "talentprofile",
        "talent_profile",
        "association",
        "consentrecord",
        "consent_record",
        "referenceexchange",
        "reference_exchange",
        "reputation",
        "risk",
        "analytics",
        "rawpayload",
        "raw_payload",
        "providerpayload",
        "provider_payload",
        "credential",
        "token",
        "secret",
        "password",
        "payslip",
        "payroll",
        "bank",
        "tax",
        "biometric",
        "geolocation",
        "nationalid",
        "national_id",
        "dateofbirth",
        "dob",
        "homeaddress",
        "home_address"
    ];

    public static Response<Guid> RequireTenant(ITenantContext tenantContext) =>
        tenantContext.TenantId is { } tenantId
            ? Response<Guid>.Success(tenantId)
            : Response<Guid>.Fail("Tenant context is required.", 401);

    public static IReadOnlyList<string> ValidateRequest(TepShellMetadataRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            errors.Add("Code is required.");
        }
        else if (ContainsForbiddenMarker(request.Code))
        {
            errors.Add("Code contains a forbidden TEP runtime marker.");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors.Add("DisplayName is required.");
        }
        else if (ContainsForbiddenMarker(request.DisplayName))
        {
            errors.Add("DisplayName contains a forbidden TEP runtime marker.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceContractVersion))
        {
            errors.Add("SourceContractVersion is required.");
        }
        else if (ContainsForbiddenMarker(request.SourceContractVersion))
        {
            errors.Add("SourceContractVersion contains a forbidden TEP runtime marker.");
        }

        if (request.ShellVersion <= 0)
        {
            errors.Add("ShellVersion must be greater than zero.");
        }

        foreach (var dependency in request.DependencyStates)
        {
            if (string.IsNullOrWhiteSpace(dependency.DependencyKey))
            {
                errors.Add("DependencyKey is required.");
            }
            else if (ContainsForbiddenMarker(dependency.DependencyKey))
            {
                errors.Add("DependencyKey contains a forbidden TEP runtime marker.");
            }

            if (!string.IsNullOrWhiteSpace(dependency.Reason) && ContainsForbiddenMarker(dependency.Reason))
            {
                errors.Add("Dependency reason contains a forbidden TEP runtime marker.");
            }
        }

        return errors;
    }

    public static Response<NoContent> ValidateDependencyState(TepShellMetadataRequest request)
    {
        if (request.ShellState is TepShellState.Active
            && (request.HcmFoundationState is not TepShellDependencyStatus.Available
                || request.PrivacyLegalState is not TepShellDependencyStatus.Available
                || request.ConsentBoundaryState is not TepShellDependencyStatus.Available
                || request.VisibilityBoundaryState is not TepShellDependencyStatus.Available))
        {
            return Response<NoContent>.Fail("Active shell metadata requires available HCM, privacy/legal, consent, and visibility boundaries.", 404);
        }

        return Response<NoContent>.Success(204);
    }

    private static bool ContainsForbiddenMarker(string value)
    {
        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return ForbiddenMarkers.Any(marker => normalized.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
