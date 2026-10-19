using Diten.Platform.API.Configuration;
using Diten.Platform.API.Services.BusinessReferenceData;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
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
    /// <summary>WP-PLT-MARKET-REFTENANT-01 — the Development reference tenant (appsettings.Development.json
    /// BusinessReferenceData:Provider:ReferenceTenantId), the tenant the tests run the Market path under.</summary>
    internal const string DevReferenceTenantId = "0b7d0048-0290-4000-8000-000000000001";

    /// <summary>The Development GSKU consumer tenant (BusinessReferenceData:VerifiedGskuOperationalProvisioning:ConsumerTenantId).</summary>
    internal const string GskuConsumerTenantId = "97c59330-dbc4-4665-b29c-0c26dbb5cc93";

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
        Assert.Equal(Guid.Parse(DevReferenceTenantId), decision.Facts.ReferenceTenantId); // the configured tenant, not a lock
        Assert.Equal(VerifiedMarketOperationalProvisioningOptions.LockedActorId, decision.Facts.ActorId);
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

    // (b) The process names a reference tenant the configuration does not: refused as missing provenance (the one match).
    [Fact]
    public async Task ProcessReferenceTenantDifferentFromConfiguration_IsRefused()
    {
        var options = Valid();
        var process = ProcessValues(options);
        process[VerifiedMarketOperationalProvisioningOptions.ReferenceTenantEnvironmentKey] = Guid.NewGuid().ToString();
        using var environment = TemporaryProcessEnvironment.Apply(process);

        var decision = await Create(options).EvaluateAsync();

        Assert.False(decision.IsEligible);
        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_PROCESS_PROVENANCE_REQUIRED", decision.ReasonCode);
        Assert.Null(decision.Authorization);
    }

    // (c) The platform system tenant as the reference tenant, named consistently by configuration and process: refused.
    // (d) The GSKU consumer tenant as the reference tenant: refused with the same reason (MOD-0048-FU01: consumer ≠ reference).
    [Theory]
    [InlineData("system")]
    [InlineData("gsku-consumer")]
    public async Task SystemOrGskuConsumerTenantAsReference_IsRefusedAsReferenceTenantInvalid(string which)
    {
        var tenant = which == "system" ? SystemTenantRules.PlatformSystemTenantId.ToString() : GskuConsumerTenantId;
        var options = Valid();
        using var environment = TemporaryProcessEnvironment.Apply(ProcessValues(options, tenant));

        var decision = await Create(options, tenant: tenant).EvaluateAsync();

        Assert.False(decision.IsEligible);
        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_REFERENCE_TENANT_INVALID", decision.ReasonCode);
        Assert.Null(decision.Facts);
        Assert.Null(decision.Authorization);
    }

    // An empty reference tenant (configured and named as such) is refused with the same reason.
    [Fact]
    public async Task EmptyReferenceTenant_IsRefusedAsReferenceTenantInvalid()
    {
        var options = Valid();
        var empty = Guid.Empty.ToString();
        using var environment = TemporaryProcessEnvironment.Apply(ProcessValues(options, empty));

        var decision = await Create(options, tenant: empty).EvaluateAsync();

        Assert.False(decision.IsEligible);
        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_REFERENCE_TENANT_INVALID", decision.ReasonCode);
    }

    // (e) Production with EVERY process value exact: refused by the environment, by that reason, before anything else.
    [Fact]
    public async Task Production_WithEveryProcessValueExact_IsRefusedAsEnvironmentNotAllowed()
    {
        var options = Valid();
        using var environment = TemporaryProcessEnvironment.Apply(ProcessValues(options));

        var decision = await Create(options, "Production").EvaluateAsync();

        Assert.False(decision.IsEligible);
        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_ENVIRONMENT_NOT_ALLOWED", decision.ReasonCode);
        Assert.Null(decision.Authorization);
    }

    // (f) The actor lock is unchanged: the canonical Platform Administrator, nothing else.
    [Fact]
    public void ActorLock_IsTheCanonicalPlatformAdministrator()
    {
        Assert.Equal("11111111-1111-1111-1111-111111111111", VerifiedMarketOperationalProvisioningOptions.LockedActorId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-a-guid")]
    [InlineData("42b66b40-f47f-4d7b-9a90-15a654333d48")]
    public async Task NonCanonicalActor_IsFailClosedBeforeAuthorization(string actorId)
    {
        var options = Valid();
        options.ActorId = actorId;
        using var environment = TemporaryProcessEnvironment.Apply(ProcessValues(options));

        var decision = await Create(options).EvaluateAsync();

        Assert.False(decision.IsEligible);
        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_CONFIGURATION_INVALID", decision.ReasonCode);
        Assert.Null(decision.Facts);
        Assert.Null(decision.Authorization);
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
        ActorId = VerifiedMarketOperationalProvisioningOptions.LockedActorId,
        IdempotencyNamespace = "market-run"
    };

    internal static Dictionary<string, string?> ProcessValues(
        VerifiedMarketOperationalProvisioningOptions options,
        string referenceTenantId = DevReferenceTenantId) => new(StringComparer.Ordinal)
    {
        [VerifiedMarketOperationalProvisioningOptions.EnabledEnvironmentKey] = options.Enabled ? "true" : "false",
        [VerifiedMarketOperationalProvisioningOptions.CatalogPathEnvironmentKey] = options.CatalogPath,
        [VerifiedMarketOperationalProvisioningOptions.CatalogVersionEnvironmentKey] = options.ExpectedCatalogVersion,
        [VerifiedMarketOperationalProvisioningOptions.CatalogFingerprintEnvironmentKey] = options.ExpectedCatalogFingerprint,
        [VerifiedMarketOperationalProvisioningOptions.ActorIdEnvironmentKey] = options.ActorId,
        [VerifiedMarketOperationalProvisioningOptions.IdempotencyNamespaceEnvironmentKey] = options.IdempotencyNamespace,
        [VerifiedMarketOperationalProvisioningOptions.CatalogLoadEnabledEnvironmentKey] = "false",
        [VerifiedMarketOperationalProvisioningOptions.ReferenceTenantEnvironmentKey] = referenceTenantId
    };

    internal static IConfigurationRoot Configuration(
        VerifiedMarketOperationalProvisioningOptions options,
        string referenceTenantId = DevReferenceTenantId) =>
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
                [$"{BusinessReferenceDataProviderOptions.SectionName}:ReferenceTenantId"] = referenceTenantId
            })
            .Build();

    /// <param name="tenant">The reference tenant, the SAME value in the configuration and the resolved provider options (as
    /// the host binds both from one configuration).</param>
    private static DevelopmentBusinessReferenceDataVerifiedMarketOperationalEligibility Create(
        VerifiedMarketOperationalProvisioningOptions options,
        string environmentName = "Development",
        string tenant = DevReferenceTenantId,
        IConfiguration? configuration = null)
    {
        var host = new Mock<IHostEnvironment>();
        host.SetupGet(x => x.EnvironmentName).Returns(environmentName);
        return new DevelopmentBusinessReferenceDataVerifiedMarketOperationalEligibility(
            host.Object,
            configuration ?? Configuration(options, tenant),
            Options.Create(options),
            Options.Create(new BusinessReferenceDataProviderOptions { ReferenceTenantId = Guid.Parse(tenant) }),
            Options.Create(new VerifiedGskuOperationalProvisioningOptions { ConsumerTenantId = Guid.Parse(GskuConsumerTenantId) }));
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
