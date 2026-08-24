using Diten.Platform.API.Services.BusinessReferenceData;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Moq;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class VerifiedMarketOperationalProvisioningRunnerTests
{
    [Fact]
    public async Task Run_UsesOnlyAuthorizedOverloadAndRequiresVerifiedReadback()
    {
        var facts = Facts(); var authorization = new Authorization(); var eligibility = Eligible(facts, authorization);
        var loader = new Mock<IBusinessReferenceDataCatalogLoaderService>(MockBehavior.Strict);
        loader.Setup(x => x.LoadVerifiedMarketCatalogFromFileAsync(facts.CatalogPath, facts.ActorId, facts.IdempotencyNamespace, authorization, facts, It.IsAny<CancellationToken>())).ReturnsAsync(new BusinessReferenceDataCatalogLoadSummary());
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>();
        repository.Setup(x => x.GetVerifiedPublicationAsync("market", facts.CatalogVersion, facts.CatalogFingerprint, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessReferenceDataVerifiedMarketResolveContractTests.Publication(
                facts.ReferenceTenantId,
                Guid.NewGuid(),
                []));
        var sut = new VerifiedMarketOperationalProvisioningRunner(eligibility.Object, loader.Object, repository.Object, new TenantContext());
        await sut.RunAsync();
        loader.VerifyAll();
        loader.Verify(x => x.LoadVerifiedMarketCatalogFromFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MissingVerifiedReadback_IsFalseSuccess()
    {
        var facts=Facts(); var authorization=new Authorization(); var eligibility=Eligible(facts,authorization);
        var loader=new Mock<IBusinessReferenceDataCatalogLoaderService>(); loader.Setup(x=>x.LoadVerifiedMarketCatalogFromFileAsync(facts.CatalogPath,facts.ActorId,facts.IdempotencyNamespace,authorization,facts,It.IsAny<CancellationToken>())).ReturnsAsync(new BusinessReferenceDataCatalogLoadSummary());
        var repository=new Mock<IBusinessReferenceDataStewardshipRepository>();
        var sut=new VerifiedMarketOperationalProvisioningRunner(eligibility.Object,loader.Object,repository.Object,new TenantContext());
        var ex=await Assert.ThrowsAsync<InvalidOperationException>(()=>sut.RunAsync()); Assert.Equal("REFERENCE_PUBLICATION_NOT_VERIFIED",ex.Message);
    }

    [Fact]
    public async Task Cancellation_Propagates()
    {
        var facts=Facts(); var authorization=new Authorization(); var eligibility=Eligible(facts,authorization);
        var loader=new Mock<IBusinessReferenceDataCatalogLoaderService>(); loader.Setup(x=>x.LoadVerifiedMarketCatalogFromFileAsync(facts.CatalogPath,facts.ActorId,facts.IdempotencyNamespace,authorization,facts,It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
        await Assert.ThrowsAsync<OperationCanceledException>(()=>new VerifiedMarketOperationalProvisioningRunner(eligibility.Object,loader.Object,new Mock<IBusinessReferenceDataStewardshipRepository>().Object,new TenantContext()).RunAsync());
    }

    [Fact] public void Runner_IsNotHostedService() => Assert.False(typeof(IHostedService).IsAssignableFrom(typeof(VerifiedMarketOperationalProvisioningRunner)));

    [Fact]
    public void CommandLine_RequiresExactArgumentAndDevelopmentEnvironment()
    {
        Assert.True(VerifiedMarketOperationalCommandLine.IsRequested([VerifiedMarketOperationalCommandLine.RunArgument]));
        Assert.False(VerifiedMarketOperationalCommandLine.IsRequested(["--RUN-VERIFIED-MARKET-PROVISIONING"]));
        Assert.False(VerifiedMarketOperationalCommandLine.IsRequested(["--run-verified-market-provisioning=true"]));

        var development = new Mock<IHostEnvironment>();
        development.SetupGet(x => x.EnvironmentName).Returns(Environments.Development);
        VerifiedMarketOperationalCommandLine.EnsureDevelopment(development.Object);

        var production = new Mock<IHostEnvironment>();
        production.SetupGet(x => x.EnvironmentName).Returns(Environments.Production);
        var exception = Assert.Throws<InvalidOperationException>(() =>
            VerifiedMarketOperationalCommandLine.EnsureDevelopment(production.Object));
        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_ENVIRONMENT_NOT_ALLOWED", exception.Message);
    }

    private static Mock<IBusinessReferenceDataVerifiedMarketOperationalEligibility> Eligible(VerifiedMarketOperationalFacts facts, Authorization authorization) { var m=new Mock<IBusinessReferenceDataVerifiedMarketOperationalEligibility>(); m.Setup(x=>x.EvaluateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new VerifiedMarketOperationalEligibilityDecision(true,"ok",facts,authorization)); m.Setup(x=>x.IsAuthorized(authorization,facts)).Returns(true); return m; }
    private static VerifiedMarketOperationalFacts Facts()=>new("market.json","UNSD-M49-2026-08-08",new string('a',64),Guid.NewGuid(),"actor","market-run");
    private sealed class Authorization : IBusinessReferenceDataVerifiedMarketOperationalAuthorization;
}
