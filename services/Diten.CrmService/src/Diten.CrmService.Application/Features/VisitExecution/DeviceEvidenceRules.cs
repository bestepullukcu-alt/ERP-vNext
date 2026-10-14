using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.VisitExecution;

/// <summary>
/// WP-VW-W3E (BE-b, §2.5, K-W3E-3 / -4 / -10) — the device declaration of a start, in ONE call (just before the photo):
/// <list type="bullet">
/// <item>a malformed declaration (an unknown platform / provider / verdict, an app version over 40, a token over 8 KB) is
/// a 400 <c>visit_capture_invalid</c> like any bad capture field — never a "fraud" refusal;</item>
/// <item>the declaration is RECORDED in every anti-fraud mode (<see cref="VisitExecutionDevice"/>, <c>Verified=false</c>:
/// nothing is verified server-side until W3E-ATTEST; the token itself is never stored — only <c>TokenProvided</c>);</item>
/// <item>flags only in <c>flag</c> mode: a mock location → <c>mock_location</c>; an integrity verdict <c>failed</c> →
/// <c>device_integrity_failed</c>; the Web, no integrity verdict or verdict <c>unavailable</c> → <c>device_unverified</c>;</item>
/// <item>no outcome refuses the start.</item>
/// </list>
/// </summary>
public static class DeviceEvidenceRules
{
    public sealed record Outcome(VisitExecutionDevice Device, IReadOnlyList<string> Flags, VisitExecutionRules.Failure? Refusal);

    public static Outcome Evaluate(VisitDeviceEvidenceInput? input, string? antiFraudMode)
    {
        var platform = VisitExecutionRules.Normalize(input?.ClientPlatform);
        var provider = VisitExecutionRules.Normalize(input?.IntegrityProvider);
        var verdict = VisitExecutionRules.Normalize(input?.IntegrityVerdict);
        var appVersion = string.IsNullOrWhiteSpace(input?.AppVersion) ? null : input!.AppVersion!.Trim();

        string? problem = null;
        if (platform is not null && !VisitClientPlatforms.All.Contains(platform, StringComparer.Ordinal))
            problem = $"clientPlatform must be one of: {string.Join(", ", VisitClientPlatforms.All)}.";
        else if (provider is not null && !VisitIntegrityProviders.All.Contains(provider, StringComparer.Ordinal))
            problem = $"integrityProvider must be one of: {string.Join(", ", VisitIntegrityProviders.All)}.";
        else if (verdict is not null && !VisitIntegrityVerdicts.All.Contains(verdict, StringComparer.Ordinal))
            problem = $"integrityVerdict must be one of: {string.Join(", ", VisitIntegrityVerdicts.All)}.";
        else if (appVersion is { Length: > VisitExecutionLimits.MaxAppVersionLength })
            problem = $"appVersion is at most {VisitExecutionLimits.MaxAppVersionLength} characters.";
        else if (input?.IntegrityTokenBytes is > VisitExecutionLimits.MaxIntegrityTokenBytes)
            problem = $"integrityToken is at most {VisitExecutionLimits.MaxIntegrityTokenBytes} bytes.";

        var device = new VisitExecutionDevice
        {
            Platform = platform,
            AppVersion = appVersion,
            MockLocation = input?.MockLocationDetected,
            IntegrityProvider = provider,
            IntegrityVerdict = verdict,
            TokenProvided = input?.IntegrityTokenBytes is > 0,
            Verified = false // W3E-ATTEST verifies the token server-side; until then a declaration is only a declaration
        };
        if (problem is not null)
        {
            return new Outcome(device, Array.Empty<string>(), VisitExecutionRules.Capture(problem));
        }

        if (!string.Equals(antiFraudMode, VisitAntiFraudModes.Flag, StringComparison.Ordinal))
        {
            return new Outcome(device, Array.Empty<string>(), null);
        }

        var flags = new List<string>();
        if (device.MockLocation == true) flags.Add(VisitExecutionFlags.MockLocation);
        if (verdict == VisitIntegrityVerdicts.Failed) flags.Add(VisitExecutionFlags.DeviceIntegrityFailed);
        if (platform == VisitClientPlatforms.Web || verdict is null || verdict == VisitIntegrityVerdicts.Unavailable)
        {
            flags.Add(VisitExecutionFlags.DeviceUnverified);
        }

        return new Outcome(device, flags, null);
    }

    public static VisitExecutionDeviceDto? ToDto(VisitExecutionDevice? d)
        => d is null ? null : new VisitExecutionDeviceDto(d.Platform, d.AppVersion, d.MockLocation, d.IntegrityProvider, d.IntegrityVerdict, d.TokenProvided, d.Verified);
}
