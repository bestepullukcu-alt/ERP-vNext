using Diten.Platform.API.Configuration;
using Diten.Platform.API.Services.BusinessReferenceData;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Infrastructure.Persistence.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class VerifiedMarketOperationalEligibilityTests
{
    [Fact] public void Enabled_DefaultsFalse() => Assert.False(new VerifiedMarketOperationalProvisioningOptions().Enabled);

    [Fact]
    public async Task DevelopmentExactArtifact_IssuesFactsBoundOpaqueAuthorization()
    {
        var tenant = Guid.NewGuid(); var sut = Create(Valid(), tenant: tenant);
        var decision = await sut.EvaluateAsync();
        Assert.True(decision.IsEligible); Assert.NotNull(decision.Authorization); Assert.NotNull(decision.Facts);
        Assert.Equal(tenant, decision.Facts.ReferenceTenantId); Assert.Equal("actor", decision.Facts.ActorId);
        Assert.Equal("market-run", decision.Facts.IdempotencyNamespace);
        Assert.True(sut.IsAuthorized(decision.Authorization, decision.Facts));
        Assert.False(sut.IsAuthorized(decision.Authorization, decision.Facts with { CatalogFingerprint = new string('0', 64) }));
        Assert.False(sut.IsAuthorized(new Forged(), decision.Facts));
    }

    [Theory]
    [InlineData("Production", true)] [InlineData("Staging", true)] [InlineData("Development", false)]
    public async Task NonDevelopmentOrDisabled_RejectsBeforeArtifactRead(string environment, bool enabled)
    {
        var options = Valid(); options.Enabled = enabled; options.CatalogPath = "missing.json";
        var decision = await Create(options, environment).EvaluateAsync();
        Assert.False(decision.IsEligible); Assert.Null(decision.Authorization);
    }

    [Theory]
    [InlineData("actor")] [InlineData("namespace")] [InlineData("path")] [InlineData("version")] [InlineData("hash")]
    public async Task InvalidRequiredFact_IsFailClosed(string field)
    {
        var options = Valid();
        if (field == "actor") options.ActorId = " "; if (field == "namespace") options.IdempotencyNamespace = " ";
        if (field == "path") options.CatalogPath += ".wrong"; if (field == "version") options.ExpectedCatalogVersion = "wrong";
        if (field == "hash") options.ExpectedCatalogFingerprint = new string('0', 64);
        var decision = await Create(options).EvaluateAsync();
        Assert.False(decision.IsEligible); Assert.Null(decision.Authorization);
    }

    [Fact]
    public async Task MissingReferenceTenant_IsFailClosed()
    {
        var decision = await Create(Valid(), configured:false).EvaluateAsync();
        Assert.False(decision.IsEligible); Assert.Equal("VERIFIED_MARKET_OPERATIONAL_CONFIGURATION_INVALID", decision.ReasonCode);
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

        var decision = await Create(options).EvaluateAsync();

        Assert.False(decision.IsEligible);
        Assert.Null(decision.Authorization);
    }

    [Fact]
    public async Task Cancellation_Propagates()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(Valid()).EvaluateAsync(cts.Token));
    }

    private static VerifiedMarketOperationalProvisioningOptions Valid() => new() { Enabled=true, CatalogPath=BusinessReferenceDataTestHarness.GetSeedPath("mod-0290-market-reference.json"), ExpectedCatalogVersion=VerifiedMarketOperationalProvisioningOptions.LockedCatalogVersion, ExpectedCatalogFingerprint=VerifiedMarketOperationalProvisioningOptions.LockedCatalogFingerprint, ActorId=" actor ", IdempotencyNamespace="market-run" };
    private static DevelopmentBusinessReferenceDataVerifiedMarketOperationalEligibility Create(VerifiedMarketOperationalProvisioningOptions options, string environment="Development", Guid? tenant=null, bool configured=true) { var host=new Mock<IHostEnvironment>(); host.SetupGet(x=>x.EnvironmentName).Returns(environment); return new(host.Object, Options.Create(options), Options.Create(new BusinessReferenceDataProviderOptions { ReferenceTenantId=configured ? tenant ?? Guid.NewGuid() : null })); }
    private sealed class Forged : IBusinessReferenceDataVerifiedMarketOperationalAuthorization;
}
