using Diten.Platform.API.Configuration;
using Diten.Platform.API.Services.BusinessReferenceData;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Infrastructure.Persistence.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

[Collection(VerifiedMarketOperationalEnvironmentCollection.Name)]
public sealed class VerifiedMarketOperationalEligibilityTests
{
    [Fact]
    public void Enabled_DefaultsFalse() => Assert.False(new VerifiedMarketOperationalProvisioningOptions().Enabled);

    [Fact]
    public async Task DevelopmentExactProcessValues_IssuesFactsBoundOpaqueAuthorization()
    {
        var options = Valid();
        using var environment = TemporaryProcessEnvironment.Apply(ProcessValues(options));
        var sut = Create(options);

        var decision = await sut.EvaluateAsync();

        Assert.True(decision.IsEligible);
        Assert.NotNull(decision.Authorization);
        Assert.NotNull(decision.Facts);
        Assert.Equal(Guid.Parse(VerifiedMarketOperationalProvisioningOptions.LockedReferenceTenantId), decision.Facts.ReferenceTenantId);
        Assert.Equal("actor", decision.Facts.ActorId);
        Assert.Equal("market-run", decision.Facts.IdempotencyNamespace);
        Assert.True(sut.IsAuthorized(decision.Authorization, decision.Facts));
        Assert.False(sut.IsAuthorized(decision.Authorization, decision.Facts with { CatalogFingerprint = new string('0', 64) }));
        Assert.False(sut.IsAuthorized(new Forged(), decision.Facts));
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", false)]
    public async Task NonDevelopmentOrDisabled_RejectsBeforeArtifactRead(string environmentName, bool enabled)
    {
        var options = Valid();
        options.Enabled = enabled;
        options.CatalogPath = Path.Combine(Path.GetTempPath(), "missing", VerifiedMarketOperationalProvisioningOptions.LockedCatalogFileName);
        using var environment = TemporaryProcessEnvironment.Apply(ProcessValues(options));

        var decision = await Create(options, environmentName).EvaluateAsync();

        Assert.False(decision.IsEligible);
        Assert.Null(decision.Authorization);
    }

    [Theory]
    [InlineData(VerifiedMarketOperationalProvisioningOptions.EnabledEnvironmentKey)]
    [InlineData(VerifiedMarketOperationalProvisioningOptions.CatalogPathEnvironmentKey)]
    [InlineData(VerifiedMarketOperationalProvisioningOptions.CatalogVersionEnvironmentKey)]
    [InlineData(VerifiedMarketOperationalProvisioningOptions.CatalogFingerprintEnvironmentKey)]
    [InlineData(VerifiedMarketOperationalProvisioningOptions.ActorIdEnvironmentKey)]
    [InlineData(VerifiedMarketOperationalProvisioningOptions.IdempotencyNamespaceEnvironmentKey)]
    [InlineData(VerifiedMarketOperationalProvisioningOptions.CatalogLoadEnabledEnvironmentKey)]
    [InlineData(VerifiedMarketOperationalProvisioningOptions.ReferenceTenantEnvironmentKey)]
    public async Task MissingProcessValue_IsFailClosedBeforeAuthorization(string missingKey)
    {
        var options = Valid();
        var process = ProcessValues(options);
        process[missingKey] = null;
        using var environment = TemporaryProcessEnvironment.Apply(process);

        var decision = await Create(options).EvaluateAsync();

        Assert.False(decision.IsEligible);
        Assert.Null(decision.Authorization);
    }

    [Fact]
    public async Task ResolvedValueThatDoesNotMatchProcessSource_IsFailClosed()
    {
        var options = Valid();
        using var environment = TemporaryProcessEnvironment.Apply(ProcessValues(options));
        var configuration = Configuration(options);
        configuration[$"{VerifiedMarketOperationalProvisioningOptions.SectionName}:ActorId"] = "appsettings-actor";

        var decision = await Create(options, configuration: configuration).EvaluateAsync();

        Assert.False(decision.IsEligible);
        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_CONFIGURATION_INVALID", decision.ReasonCode);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("namespace")]
    [InlineData("path")]
    [InlineData("version")]
    [InlineData("hash")]
    public async Task InvalidRequiredFact_IsFailClosed(string field)
    {
        var options = Valid();
        if (field == "actor") options.ActorId = " ";
        if (field == "namespace") options.IdempotencyNamespace = " ";
        if (field == "path") options.CatalogPath += ".wrong";
        if (field == "version") options.ExpectedCatalogVersion = "wrong";
        if (field == "hash") options.ExpectedCatalogFingerprint = new string('0', 64);
        using var environment = TemporaryProcessEnvironment.Apply(ProcessValues(options));

        var decision = await Create(options).EvaluateAsync();

        Assert.False(decision.IsEligible);
        Assert.Null(decision.Authorization);
    }

    [Fact]
    public async Task NonLockedReferenceTenant_IsFailClosed()
    {
        var options = Valid();
        using var environment = TemporaryProcessEnvironment.Apply(ProcessValues(options));

        var decision = await Create(options, tenant: Guid.NewGuid()).EvaluateAsync();

        Assert.False(decision.IsEligible);
        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_CONFIGURATION_INVALID", decision.ReasonCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" market-run")]
    [InlineData("market-run ")]
    [InlineData("market:run")]
    [InlineData("market/run")]
    public async Task NamespaceThatCouldAliasOrCollide_IsFailClosed(string value)
    {
        var options = Valid();
        options.IdempotencyNamespace = value;
        using var environment = TemporaryProcessEnvironment.Apply(ProcessValues(options));

        var decision = await Create(options).EvaluateAsync();

        Assert.False(decision.IsEligible);
        Assert.Null(decision.Authorization);
    }

    [Fact]
    public async Task Cancellation_Propagates()
    {
        var options = Valid();
        using var environment = TemporaryProcessEnvironment.Apply(ProcessValues(options));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(options).EvaluateAsync(cts.Token));
    }

    internal static VerifiedMarketOperationalProvisioningOptions Valid() => new()
    {
        Enabled = true,
        CatalogPath = BusinessReferenceDataTestHarness.GetSeedPath(VerifiedMarketOperationalProvisioningOptions.LockedCatalogFileName),
        ExpectedCatalogVersion = VerifiedMarketOperationalProvisioningOptions.LockedCatalogVersion,
        ExpectedCatalogFingerprint = VerifiedMarketOperationalProvisioningOptions.LockedCatalogFingerprint,
        ActorId = "actor",
        IdempotencyNamespace = "market-run"
    };

    internal static Dictionary<string, string?> ProcessValues(VerifiedMarketOperationalProvisioningOptions options) => new(StringComparer.Ordinal)
    {
        [VerifiedMarketOperationalProvisioningOptions.EnabledEnvironmentKey] = options.Enabled ? "true" : "false",
        [VerifiedMarketOperationalProvisioningOptions.CatalogPathEnvironmentKey] = options.CatalogPath,
        [VerifiedMarketOperationalProvisioningOptions.CatalogVersionEnvironmentKey] = options.ExpectedCatalogVersion,
        [VerifiedMarketOperationalProvisioningOptions.CatalogFingerprintEnvironmentKey] = options.ExpectedCatalogFingerprint,
        [VerifiedMarketOperationalProvisioningOptions.ActorIdEnvironmentKey] = options.ActorId,
        [VerifiedMarketOperationalProvisioningOptions.IdempotencyNamespaceEnvironmentKey] = options.IdempotencyNamespace,
        [VerifiedMarketOperationalProvisioningOptions.CatalogLoadEnabledEnvironmentKey] = "false",
        [VerifiedMarketOperationalProvisioningOptions.ReferenceTenantEnvironmentKey] = VerifiedMarketOperationalProvisioningOptions.LockedReferenceTenantId
    };

    internal static IConfigurationRoot Configuration(VerifiedMarketOperationalProvisioningOptions options) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{VerifiedMarketOperationalProvisioningOptions.SectionName}:Enabled"] = options.Enabled.ToString(),
                [$"{VerifiedMarketOperationalProvisioningOptions.SectionName}:CatalogPath"] = options.CatalogPath,
                [$"{VerifiedMarketOperationalProvisioningOptions.SectionName}:ExpectedCatalogVersion"] = options.ExpectedCatalogVersion,
                [$"{VerifiedMarketOperationalProvisioningOptions.SectionName}:ExpectedCatalogFingerprint"] = options.ExpectedCatalogFingerprint,
                [$"{VerifiedMarketOperationalProvisioningOptions.SectionName}:ActorId"] = options.ActorId,
                [$"{VerifiedMarketOperationalProvisioningOptions.SectionName}:IdempotencyNamespace"] = options.IdempotencyNamespace,
                ["BusinessReferenceData:CatalogLoad:Enabled"] = "false",
                [$"{BusinessReferenceDataProviderOptions.SectionName}:ReferenceTenantId"] = VerifiedMarketOperationalProvisioningOptions.LockedReferenceTenantId
            })
            .Build();

    private static DevelopmentBusinessReferenceDataVerifiedMarketOperationalEligibility Create(
        VerifiedMarketOperationalProvisioningOptions options,
        string environmentName = "Development",
        Guid? tenant = null,
        IConfiguration? configuration = null)
    {
        var host = new Mock<IHostEnvironment>();
        host.SetupGet(x => x.EnvironmentName).Returns(environmentName);
        return new DevelopmentBusinessReferenceDataVerifiedMarketOperationalEligibility(
            host.Object,
            configuration ?? Configuration(options),
            Options.Create(options),
            Options.Create(new BusinessReferenceDataProviderOptions
            {
                ReferenceTenantId = tenant ?? Guid.Parse(VerifiedMarketOperationalProvisioningOptions.LockedReferenceTenantId)
            }));
    }

    private sealed class Forged : IBusinessReferenceDataVerifiedMarketOperationalAuthorization;
}

internal sealed class TemporaryProcessEnvironment : IDisposable
{
    private readonly Dictionary<string, string?> _originals;

    private TemporaryProcessEnvironment(IReadOnlyDictionary<string, string?> values)
    {
        _originals = values.Keys.ToDictionary(
            key => key,
            key => Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process),
            StringComparer.Ordinal);
        foreach (var pair in values)
        {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value, EnvironmentVariableTarget.Process);
        }
    }

    public static TemporaryProcessEnvironment Apply(IReadOnlyDictionary<string, string?> values) => new(values);

    public void Dispose()
    {
        foreach (var pair in _originals)
        {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value, EnvironmentVariableTarget.Process);
        }
    }
}
