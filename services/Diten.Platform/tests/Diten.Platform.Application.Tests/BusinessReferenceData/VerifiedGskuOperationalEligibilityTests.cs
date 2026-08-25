using Diten.Platform.API.Configuration;
using Diten.Platform.API.Services.BusinessReferenceData;
using Diten.Platform.Infrastructure.Persistence.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class VerifiedGskuOperationalEligibilityTests
{
    [Fact]
    public void Flags_DefaultToFalse()
    {
        var options = new VerifiedGskuOperationalProvisioningOptions();

        Assert.False(options.Enabled);
        Assert.False(options.EnumerationEnabled);
    }

    [Fact]
    public async Task BothDisabled_AreFailClosedWithoutReadingCatalog()
    {
        var sut = CreateSut(new VerifiedGskuOperationalProvisioningOptions
        {
            Enabled = false,
            EnumerationEnabled = false,
            CatalogPath = "does-not-exist.json"
        });

        var provisioning = await sut.EvaluateAsync();
        var enumeration = await sut.EvaluateEnumerationAsync();

        Assert.False(provisioning.IsEligible);
        Assert.Equal("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE", provisioning.ReasonCode);
        Assert.Null(provisioning.Authorization);
        Assert.False(enumeration.IsEligible);
        Assert.Equal("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE", enumeration.ReasonCode);
        Assert.Null(enumeration.Facts);
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public async Task NonDevelopment_IsFailClosedEvenWhenBothFlagsAreEnabled(string environmentName)
    {
        var sut = CreateSut(
            new VerifiedGskuOperationalProvisioningOptions
            {
                Enabled = true,
                EnumerationEnabled = true
            },
            environmentName);

        var provisioning = await sut.EvaluateAsync();
        var enumeration = await sut.EvaluateEnumerationAsync();

        Assert.False(provisioning.IsEligible);
        Assert.Equal("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE", provisioning.ReasonCode);
        Assert.Null(provisioning.Authorization);
        Assert.False(enumeration.IsEligible);
        Assert.Equal("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE", enumeration.ReasonCode);
    }

    [Fact]
    public async Task ExactLockedArtifact_IssuesAuthorizationBoundToAllFacts()
    {
        var referenceTenantId = Guid.NewGuid();
        var consumerTenantId = Guid.NewGuid();
        var catalogPath = FindLockedCatalog();
        var sut = CreateSut(new VerifiedGskuOperationalProvisioningOptions
        {
            Enabled = true,
            CatalogPath = catalogPath,
            ExpectedCatalogVersion = VerifiedGskuOperationalProvisioningOptions.LockedCatalogVersion,
            ExpectedCatalogFingerprint = VerifiedGskuOperationalProvisioningOptions.LockedCatalogFingerprint,
            ConsumerTenantId = consumerTenantId,
            ActorId = " pilot-actor ",
            IdempotencyNamespace = " pilot-run "
        }, providerTenantId: referenceTenantId);

        var decision = await sut.EvaluateAsync();

        Assert.True(decision.IsEligible);
        Assert.Equal("VERIFIED_GSKU_OPERATIONAL_ELIGIBLE", decision.ReasonCode);
        Assert.NotNull(decision.Facts);
        Assert.NotNull(decision.Authorization);
        Assert.Equal(referenceTenantId, decision.Facts.ReferenceTenantId);
        Assert.Equal(consumerTenantId, decision.Facts.ConsumerTenantId);
        Assert.Equal(["pack-applicability", "uom"], decision.Facts.RequiredSetCodes);
        Assert.True(sut.IsAuthorized(decision.Authorization, decision.Facts));
        Assert.False(sut.IsAuthorized(
            decision.Authorization,
            decision.Facts with { ConsumerTenantId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task EnumerationOnly_IssuesReadFactsWithoutMutationAuthorization()
    {
        var referenceTenantId = Guid.NewGuid();
        var consumerTenantId = Guid.NewGuid();
        var sut = CreateSut(new VerifiedGskuOperationalProvisioningOptions
        {
            Enabled = false,
            EnumerationEnabled = true,
            CatalogPath = FindLockedCatalog(),
            ExpectedCatalogVersion = VerifiedGskuOperationalProvisioningOptions.LockedCatalogVersion,
            ExpectedCatalogFingerprint = VerifiedGskuOperationalProvisioningOptions.LockedCatalogFingerprint,
            ConsumerTenantId = consumerTenantId
        }, providerTenantId: referenceTenantId);

        var provisioning = await sut.EvaluateAsync();
        var enumeration = await sut.EvaluateEnumerationAsync();

        Assert.False(provisioning.IsEligible);
        Assert.Null(provisioning.Authorization);
        Assert.True(enumeration.IsEligible);
        Assert.Equal("VERIFIED_GSKU_ENUMERATION_ELIGIBLE", enumeration.ReasonCode);
        Assert.NotNull(enumeration.Facts);
        Assert.Equal(referenceTenantId, enumeration.Facts.ReferenceTenantId);
        Assert.Equal(consumerTenantId, enumeration.Facts.ConsumerTenantId);
        Assert.Equal(["pack-applicability", "uom"], enumeration.Facts.RequiredSetCodes);
    }

    [Fact]
    public async Task ProvisioningOnly_IssuesMutationAuthorizationButDeniesEnumeration()
    {
        var sut = CreateSut(ValidOptions(enabled: true, enumerationEnabled: false));

        var provisioning = await sut.EvaluateAsync();
        var enumeration = await sut.EvaluateEnumerationAsync();

        Assert.True(provisioning.IsEligible);
        Assert.NotNull(provisioning.Authorization);
        Assert.False(enumeration.IsEligible);
        Assert.Equal("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE", enumeration.ReasonCode);
        Assert.Null(enumeration.Facts);
    }

    [Fact]
    public async Task BothEnabled_IssueIndependentMutationAndEnumerationDecisions()
    {
        var sut = CreateSut(ValidOptions(enabled: true, enumerationEnabled: true));

        var provisioning = await sut.EvaluateAsync();
        var enumeration = await sut.EvaluateEnumerationAsync();

        Assert.True(provisioning.IsEligible);
        Assert.NotNull(provisioning.Authorization);
        Assert.True(enumeration.IsEligible);
        Assert.NotNull(enumeration.Facts);
        Assert.Equal(provisioning.Facts!.CatalogVersion, enumeration.Facts.CatalogVersion);
        Assert.Equal(provisioning.Facts.CatalogFingerprint, enumeration.Facts.CatalogFingerprint);
        Assert.Equal(provisioning.Facts.ReferenceTenantId, enumeration.Facts.ReferenceTenantId);
        Assert.Equal(provisioning.Facts.ConsumerTenantId, enumeration.Facts.ConsumerTenantId);
    }

    [Fact]
    public async Task ProviderTenantMissing_IsStableConfigurationFailure()
    {
        var sut = CreateSut(new VerifiedGskuOperationalProvisioningOptions
        {
            Enabled = true,
            CatalogPath = FindLockedCatalog(),
            ExpectedCatalogVersion = VerifiedGskuOperationalProvisioningOptions.LockedCatalogVersion,
            ExpectedCatalogFingerprint = VerifiedGskuOperationalProvisioningOptions.LockedCatalogFingerprint,
            ConsumerTenantId = Guid.NewGuid(),
            ActorId = "actor",
            IdempotencyNamespace = "run"
        }, providerConfigured: false);

        var decision = await sut.EvaluateAsync();

        Assert.False(decision.IsEligible);
        Assert.Equal("REFERENCE_PROVIDER_CONFIGURATION_INVALID", decision.ReasonCode);
    }

    [Fact]
    public async Task EnumerationWithMissingProvider_IsStableConfigurationFailure()
    {
        var options = ValidOptions(enabled: false, enumerationEnabled: true);
        var sut = CreateSut(options, providerConfigured: false);

        var decision = await sut.EvaluateEnumerationAsync();

        Assert.False(decision.IsEligible);
        Assert.Equal("REFERENCE_PROVIDER_CONFIGURATION_INVALID", decision.ReasonCode);
        Assert.Null(decision.Facts);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"catalog_version\":1}")]
    public async Task DriftedArtifactShape_IsStableArtifactFailure(string payload)
    {
        var directory = Directory.CreateTempSubdirectory("verified-gsku-eligibility-");
        try
        {
            var path = Path.Combine(
                directory.FullName,
                VerifiedGskuOperationalProvisioningOptions.LockedCatalogFileName);
            await File.WriteAllTextAsync(path, payload);
            var options = ValidOptions(enabled: true, enumerationEnabled: true);
            options.CatalogPath = path;
            var sut = CreateSut(options);

            var provisioning = await sut.EvaluateAsync();
            var enumeration = await sut.EvaluateEnumerationAsync();

            Assert.False(provisioning.IsEligible);
            Assert.Equal("VERIFIED_GSKU_OPERATIONAL_ARTIFACT_INVALID", provisioning.ReasonCode);
            Assert.Null(provisioning.Authorization);
            Assert.False(enumeration.IsEligible);
            Assert.Equal("VERIFIED_GSKU_OPERATIONAL_ARTIFACT_INVALID", enumeration.ReasonCode);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static DevelopmentBusinessReferenceDataVerifiedGskuOperationalEligibility CreateSut(
        VerifiedGskuOperationalProvisioningOptions options,
        string environmentName = "Development",
        Guid? providerTenantId = null,
        bool providerConfigured = true)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(environmentName);
        return new DevelopmentBusinessReferenceDataVerifiedGskuOperationalEligibility(
            environment.Object,
            Options.Create(options),
            Options.Create(new BusinessReferenceDataProviderOptions
            {
                ReferenceTenantId = providerConfigured ? providerTenantId ?? Guid.NewGuid() : null
            }));
    }

    private static string FindLockedCatalog()
        => BusinessReferenceDataTestHarness.GetArtifactPath();

    private static VerifiedGskuOperationalProvisioningOptions ValidOptions(
        bool enabled,
        bool enumerationEnabled) => new()
        {
            Enabled = enabled,
            EnumerationEnabled = enumerationEnabled,
            CatalogPath = FindLockedCatalog(),
            ExpectedCatalogVersion = VerifiedGskuOperationalProvisioningOptions.LockedCatalogVersion,
            ExpectedCatalogFingerprint = VerifiedGskuOperationalProvisioningOptions.LockedCatalogFingerprint,
            ConsumerTenantId = Guid.NewGuid(),
            ActorId = "actor",
            IdempotencyNamespace = "run"
        };
}
