using System.Security.Cryptography;
using System.Text.Json;
using Diten.Platform.API.Configuration;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Infrastructure.Persistence.Settings;
using Microsoft.Extensions.Options;

namespace Diten.Platform.API.Services.BusinessReferenceData;

public sealed class DevelopmentBusinessReferenceDataVerifiedMarketOperationalEligibility : IBusinessReferenceDataVerifiedMarketOperationalEligibility
{
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IOptions<VerifiedMarketOperationalProvisioningOptions> _options;
    private readonly IOptions<BusinessReferenceDataProviderOptions> _provider;
    private readonly object _issuer = new();

    public DevelopmentBusinessReferenceDataVerifiedMarketOperationalEligibility(
        IHostEnvironment environment,
        IConfiguration configuration,
        IOptions<VerifiedMarketOperationalProvisioningOptions> options,
        IOptions<BusinessReferenceDataProviderOptions> provider)
    {
        _environment = environment;
        _configuration = configuration;
        _options = options;
        _provider = provider;
    }

    public async Task<VerifiedMarketOperationalEligibilityDecision> EvaluateAsync(CancellationToken ct = default)
    {
        if (!_environment.IsDevelopment())
        {
            return Denied("VERIFIED_MARKET_OPERATIONAL_ENVIRONMENT_NOT_ALLOWED");
        }

        if (!HasExactProcessValue(VerifiedMarketOperationalProvisioningOptions.EnabledEnvironmentKey, "true")
            || !HasExactProcessValue(VerifiedMarketOperationalProvisioningOptions.CatalogLoadEnabledEnvironmentKey, "false")
            || !HasExactProcessValue(
                VerifiedMarketOperationalProvisioningOptions.ReferenceTenantEnvironmentKey,
                VerifiedMarketOperationalProvisioningOptions.LockedReferenceTenantId))
        {
            return Denied("VERIFIED_MARKET_OPERATIONAL_PROCESS_PROVENANCE_REQUIRED");
        }

        VerifiedMarketOperationalProvisioningOptions options;
        try
        {
            options = _options.Value;
        }
        catch
        {
            return Denied("VERIFIED_MARKET_OPERATIONAL_CONFIGURATION_INVALID");
        }

        var provider = BusinessReferenceDataProviderOptionsResolver.Resolve(_provider);
        if (!provider.IsValid
            || provider.ReferenceTenantId != Guid.Parse(VerifiedMarketOperationalProvisioningOptions.LockedReferenceTenantId)
            || !options.Enabled
            || !IsExactProcessResolvedValue(VerifiedMarketOperationalProvisioningOptions.CatalogPathEnvironmentKey, $"{VerifiedMarketOperationalProvisioningOptions.SectionName}:CatalogPath")
            || !IsExactProcessResolvedValue(VerifiedMarketOperationalProvisioningOptions.CatalogVersionEnvironmentKey, $"{VerifiedMarketOperationalProvisioningOptions.SectionName}:ExpectedCatalogVersion")
            || !IsExactProcessResolvedValue(VerifiedMarketOperationalProvisioningOptions.CatalogFingerprintEnvironmentKey, $"{VerifiedMarketOperationalProvisioningOptions.SectionName}:ExpectedCatalogFingerprint")
            || !IsExactProcessResolvedValue(VerifiedMarketOperationalProvisioningOptions.ActorIdEnvironmentKey, $"{VerifiedMarketOperationalProvisioningOptions.SectionName}:ActorId")
            || !IsExactProcessResolvedValue(VerifiedMarketOperationalProvisioningOptions.IdempotencyNamespaceEnvironmentKey, $"{VerifiedMarketOperationalProvisioningOptions.SectionName}:IdempotencyNamespace")
            || !IsExactProcessResolvedValue(VerifiedMarketOperationalProvisioningOptions.CatalogLoadEnabledEnvironmentKey, "BusinessReferenceData:CatalogLoad:Enabled")
            || !IsExactProcessResolvedValue(VerifiedMarketOperationalProvisioningOptions.ReferenceTenantEnvironmentKey, $"{BusinessReferenceDataProviderOptions.SectionName}:ReferenceTenantId")
            || string.IsNullOrWhiteSpace(options.ActorId)
            || !IsValidNamespace(options.IdempotencyNamespace)
            || options.ExpectedCatalogVersion != VerifiedMarketOperationalProvisioningOptions.LockedCatalogVersion
            || !string.Equals(options.ExpectedCatalogFingerprint, VerifiedMarketOperationalProvisioningOptions.LockedCatalogFingerprint, StringComparison.Ordinal))
        {
            return Denied("VERIFIED_MARKET_OPERATIONAL_CONFIGURATION_INVALID");
        }

        try
        {
            var path = Path.GetFullPath(options.CatalogPath);
            if (!File.Exists(path)
                || !string.Equals(Path.GetFileName(path), VerifiedMarketOperationalProvisioningOptions.LockedCatalogFileName, StringComparison.Ordinal))
            {
                return Denied("VERIFIED_MARKET_OPERATIONAL_ARTIFACT_INVALID");
            }

            var bytes = await File.ReadAllBytesAsync(path, ct);
            var fingerprint = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            using var document = JsonDocument.Parse(bytes);
            if (!string.Equals(fingerprint, VerifiedMarketOperationalProvisioningOptions.LockedCatalogFingerprint, StringComparison.Ordinal)
                || !document.RootElement.TryGetProperty("catalog_version", out var version)
                || !string.Equals(version.GetString(), VerifiedMarketOperationalProvisioningOptions.LockedCatalogVersion, StringComparison.Ordinal))
            {
                return Denied("VERIFIED_MARKET_OPERATIONAL_ARTIFACT_INVALID");
            }

            var facts = new VerifiedMarketOperationalFacts(
                path,
                options.ExpectedCatalogVersion,
                fingerprint,
                provider.ReferenceTenantId,
                options.ActorId.Trim(),
                options.IdempotencyNamespace.Trim());
            return new VerifiedMarketOperationalEligibilityDecision(
                true,
                "VERIFIED_MARKET_OPERATIONAL_ELIGIBLE",
                facts,
                new Authorization(_issuer, facts));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return Denied("VERIFIED_MARKET_OPERATIONAL_ARTIFACT_INVALID");
        }
    }

    public bool IsAuthorized(
        IBusinessReferenceDataVerifiedMarketOperationalAuthorization authorization,
        VerifiedMarketOperationalFacts facts) =>
        authorization is Authorization candidate
        && ReferenceEquals(candidate.Issuer, _issuer)
        && candidate.Facts == facts;

    private bool HasExactProcessValue(string environmentKey, string expected) =>
        string.Equals(
            Environment.GetEnvironmentVariable(environmentKey, EnvironmentVariableTarget.Process),
            expected,
            StringComparison.Ordinal);

    private bool IsExactProcessResolvedValue(string environmentKey, string configurationKey)
    {
        var processValue = Environment.GetEnvironmentVariable(environmentKey, EnvironmentVariableTarget.Process);
        return !string.IsNullOrEmpty(processValue)
               && string.Equals(_configuration[configurationKey], processValue, StringComparison.Ordinal);
    }

    private static bool IsValidNamespace(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value == value.Trim()
        && value.Length <= 128
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    private static VerifiedMarketOperationalEligibilityDecision Denied(string code) => new(false, code);

    private sealed record Authorization(object Issuer, VerifiedMarketOperationalFacts Facts)
        : IBusinessReferenceDataVerifiedMarketOperationalAuthorization;
}
