using System.Security.Cryptography;
using System.Text.Json;
using Diten.Platform.API.Configuration;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Infrastructure.Persistence.Settings;
using Microsoft.Extensions.Options;

namespace Diten.Platform.API.Services.BusinessReferenceData;

public sealed class DevelopmentBusinessReferenceDataVerifiedMarketOperationalEligibility : IBusinessReferenceDataVerifiedMarketOperationalEligibility
{
    private readonly IHostEnvironment _environment; private readonly IOptions<VerifiedMarketOperationalProvisioningOptions> _options; private readonly IOptions<BusinessReferenceDataProviderOptions> _provider; private readonly object _issuer = new();
    public DevelopmentBusinessReferenceDataVerifiedMarketOperationalEligibility(IHostEnvironment environment, IOptions<VerifiedMarketOperationalProvisioningOptions> options, IOptions<BusinessReferenceDataProviderOptions> provider) => (_environment, _options, _provider) = (environment, options, provider);
    public async Task<VerifiedMarketOperationalEligibilityDecision> EvaluateAsync(CancellationToken ct = default)
    {
        if (!_environment.IsDevelopment()) return Denied("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE");
        VerifiedMarketOperationalProvisioningOptions o; try { o = _options.Value; } catch { return Denied("VERIFIED_MARKET_OPERATIONAL_CONFIGURATION_INVALID"); }
        var provider = BusinessReferenceDataProviderOptionsResolver.Resolve(_provider);
        if (!o.Enabled || !provider.IsValid || string.IsNullOrWhiteSpace(o.ActorId) || !IsValidNamespace(o.IdempotencyNamespace) || string.IsNullOrWhiteSpace(o.CatalogPath) || o.ExpectedCatalogVersion != VerifiedMarketOperationalProvisioningOptions.LockedCatalogVersion || !string.Equals(o.ExpectedCatalogFingerprint, VerifiedMarketOperationalProvisioningOptions.LockedCatalogFingerprint, StringComparison.OrdinalIgnoreCase)) return Denied("VERIFIED_MARKET_OPERATIONAL_CONFIGURATION_INVALID");
        try { var path = Path.GetFullPath(o.CatalogPath); var bytes = await File.ReadAllBytesAsync(path, ct); if (!File.Exists(path) || Path.GetFileName(path) != VerifiedMarketOperationalProvisioningOptions.LockedCatalogFileName || !string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), VerifiedMarketOperationalProvisioningOptions.LockedCatalogFingerprint, StringComparison.OrdinalIgnoreCase) || JsonDocument.Parse(bytes).RootElement.GetProperty("catalog_version").GetString() != VerifiedMarketOperationalProvisioningOptions.LockedCatalogVersion) return Denied("VERIFIED_MARKET_OPERATIONAL_ARTIFACT_INVALID"); var facts = new VerifiedMarketOperationalFacts(path, o.ExpectedCatalogVersion, o.ExpectedCatalogFingerprint.ToLowerInvariant(), provider.ReferenceTenantId, o.ActorId.Trim(), o.IdempotencyNamespace.Trim()); return new(true, "VERIFIED_MARKET_OPERATIONAL_ELIGIBLE", facts, new Authorization(_issuer, facts)); } catch (OperationCanceledException) { throw; } catch { return Denied("VERIFIED_MARKET_OPERATIONAL_ARTIFACT_INVALID"); }
    }
    public bool IsAuthorized(IBusinessReferenceDataVerifiedMarketOperationalAuthorization authorization, VerifiedMarketOperationalFacts facts) => authorization is Authorization a && ReferenceEquals(a.Issuer, _issuer) && a.Facts == facts;
    private static bool IsValidNamespace(string value) => !string.IsNullOrWhiteSpace(value)
        && value == value.Trim()
        && value.Length <= 128
        && value.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or '.');
    private static VerifiedMarketOperationalEligibilityDecision Denied(string code) => new(false, code);
    private sealed record Authorization(object Issuer, VerifiedMarketOperationalFacts Facts) : IBusinessReferenceDataVerifiedMarketOperationalAuthorization;
}
