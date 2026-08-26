using System.Security.Cryptography;
using System.Text.Json;
using Diten.Platform.API.Configuration;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Infrastructure.Persistence.Settings;
using Microsoft.Extensions.Options;

namespace Diten.Platform.API.Services.BusinessReferenceData;

public sealed class DevelopmentBusinessReferenceDataVerifiedGskuOperationalEligibility
    : IBusinessReferenceDataVerifiedGskuOperationalEligibility
{
    private static readonly string[] RequiredSets = ["pack-applicability", "uom"];
    private readonly IHostEnvironment _environment;
    private readonly IOptions<VerifiedGskuOperationalProvisioningOptions> _options;
    private readonly IOptions<BusinessReferenceDataProviderOptions> _providerOptions;
    private readonly object _issuer = new();

    public DevelopmentBusinessReferenceDataVerifiedGskuOperationalEligibility(
        IHostEnvironment environment,
        IOptions<VerifiedGskuOperationalProvisioningOptions> options,
        IOptions<BusinessReferenceDataProviderOptions> providerOptions)
    {
        _environment = environment;
        _options = options;
        _providerOptions = providerOptions;
    }

    public async Task<VerifiedGskuOperationalEligibilityDecision> EvaluateAsync(CancellationToken ct = default)
    {
        var options = ResolveOptions();
        if (options is null)
        {
            return Denied("VERIFIED_GSKU_OPERATIONAL_CONFIGURATION_INVALID");
        }

        if (!options.Enabled)
        {
            return Denied("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE");
        }

        if (!_environment.IsDevelopment())
        {
            return Denied("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE");
        }

        var validation = await ValidateCommonAsync(options, ct);
        if (!validation.IsValid || validation.Facts is null)
        {
            return Denied(validation.ReasonCode);
        }

        if (string.IsNullOrWhiteSpace(options.ActorId)
            || string.IsNullOrWhiteSpace(options.IdempotencyNamespace))
        {
            return Denied("VERIFIED_GSKU_OPERATIONAL_CONFIGURATION_INVALID");
        }

        var common = validation.Facts;
        var facts = new VerifiedGskuOperationalFacts(
            common.CatalogPath,
            common.CatalogVersion,
            common.CatalogFingerprint,
            common.ReferenceTenantId,
            common.ConsumerTenantId,
            options.ActorId.Trim(),
            options.IdempotencyNamespace.Trim(),
            common.RequiredSetCodes);
        return new VerifiedGskuOperationalEligibilityDecision(
            true,
            "VERIFIED_GSKU_OPERATIONAL_ELIGIBLE",
            facts,
            new Authorization(_issuer, facts));
    }

    public async Task<VerifiedGskuEnumerationEligibilityDecision> EvaluateEnumerationAsync(
        CancellationToken ct = default)
    {
        var options = ResolveOptions();
        if (options is null)
        {
            return EnumerationDenied("VERIFIED_GSKU_OPERATIONAL_CONFIGURATION_INVALID");
        }

        if (!options.EnumerationEnabled || !_environment.IsDevelopment())
        {
            return EnumerationDenied("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE");
        }

        var validation = await ValidateCommonAsync(options, ct);
        return validation.IsValid && validation.Facts is not null
            ? new VerifiedGskuEnumerationEligibilityDecision(
                true,
                "VERIFIED_GSKU_ENUMERATION_ELIGIBLE",
                validation.Facts)
            : EnumerationDenied(validation.ReasonCode);
    }

    public bool IsAuthorized(
        IBusinessReferenceDataVerifiedGskuOperationalAuthorization authorization,
        VerifiedGskuOperationalFacts facts) =>
        authorization is Authorization issued
        && ReferenceEquals(issued.Issuer, _issuer)
        && issued.Facts == facts
        && issued.Facts.RequiredSetCodes.SequenceEqual(facts.RequiredSetCodes, StringComparer.Ordinal);

    private static VerifiedGskuOperationalEligibilityDecision Denied(string reasonCode) => new(false, reasonCode);

    private static VerifiedGskuEnumerationEligibilityDecision EnumerationDenied(string reasonCode) =>
        new(false, reasonCode);

    private VerifiedGskuOperationalProvisioningOptions? ResolveOptions()
    {
        try
        {
            return _options.Value;
        }
        catch (Exception exception) when (exception is OptionsValidationException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private async Task<CommonValidation> ValidateCommonAsync(
        VerifiedGskuOperationalProvisioningOptions options,
        CancellationToken ct)
    {
        var provider = BusinessReferenceDataProviderOptionsResolver.Resolve(_providerOptions);
        if (!provider.IsValid)
        {
            return new CommonValidation(false, BusinessReferenceDataProviderOptionsResolution.InvalidReasonCode);
        }

        if (!options.ConsumerTenantId.HasValue
            || options.ConsumerTenantId.Value == Guid.Empty
            || options.ConsumerTenantId.Value == provider.ReferenceTenantId
            || string.IsNullOrWhiteSpace(options.CatalogPath)
            || string.IsNullOrWhiteSpace(options.ExpectedCatalogVersion)
            || string.IsNullOrWhiteSpace(options.ExpectedCatalogFingerprint))
        {
            return new CommonValidation(false, "VERIFIED_GSKU_OPERATIONAL_CONFIGURATION_INVALID");
        }

        try
        {
            var fullPath = Path.GetFullPath(options.CatalogPath);
            if (!File.Exists(fullPath)
                || !string.Equals(Path.GetFileName(fullPath), VerifiedGskuOperationalProvisioningOptions.LockedCatalogFileName, StringComparison.Ordinal)
                || !string.Equals(options.ExpectedCatalogVersion, VerifiedGskuOperationalProvisioningOptions.LockedCatalogVersion, StringComparison.Ordinal)
                || !string.Equals(options.ExpectedCatalogFingerprint, VerifiedGskuOperationalProvisioningOptions.LockedCatalogFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                return new CommonValidation(false, "VERIFIED_GSKU_OPERATIONAL_ARTIFACT_INVALID");
            }

            var payload = await File.ReadAllBytesAsync(fullPath, ct);
            var fingerprint = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
            if (!string.Equals(fingerprint, VerifiedGskuOperationalProvisioningOptions.LockedCatalogFingerprint, StringComparison.Ordinal))
            {
                return new CommonValidation(false, "VERIFIED_GSKU_OPERATIONAL_ARTIFACT_INVALID");
            }

            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("catalog_version", out var catalogVersionElement)
                || catalogVersionElement.ValueKind != JsonValueKind.String
                || !string.Equals(
                    catalogVersionElement.GetString(),
                    VerifiedGskuOperationalProvisioningOptions.LockedCatalogVersion,
                    StringComparison.Ordinal))
            {
                return new CommonValidation(false, "VERIFIED_GSKU_OPERATIONAL_ARTIFACT_INVALID");
            }

            return new CommonValidation(
                true,
                "VERIFIED_GSKU_OPERATIONAL_CONFIGURATION_VALID",
                new VerifiedGskuEnumerationFacts(
                    fullPath,
                    VerifiedGskuOperationalProvisioningOptions.LockedCatalogVersion,
                    fingerprint,
                    provider.ReferenceTenantId,
                    options.ConsumerTenantId.Value,
                    RequiredSets));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or CryptographicException)
        {
            return new CommonValidation(false, "VERIFIED_GSKU_OPERATIONAL_ARTIFACT_INVALID");
        }
    }

    private sealed record Authorization(object Issuer, VerifiedGskuOperationalFacts Facts)
        : IBusinessReferenceDataVerifiedGskuOperationalAuthorization;

    private sealed record CommonValidation(
        bool IsValid,
        string ReasonCode,
        VerifiedGskuEnumerationFacts? Facts = null);
}
