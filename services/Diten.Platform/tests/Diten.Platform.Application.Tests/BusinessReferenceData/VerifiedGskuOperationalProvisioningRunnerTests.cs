using Diten.Platform.API.Services.BusinessReferenceData;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class VerifiedGskuOperationalProvisioningRunnerTests
{
    [Fact]
    public async Task EnumerationOnly_DoesNotResolveLoaderOrMutateState_AndStartsOnce()
    {
        var eligibility = new Mock<IBusinessReferenceDataVerifiedGskuOperationalEligibility>();
        eligibility
            .Setup(value => value.EvaluateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedGskuOperationalEligibilityDecision(
                false,
                "REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE"));
        eligibility
            .Setup(value => value.EvaluateEnumerationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedGskuEnumerationEligibilityDecision(
                true,
                "VERIFIED_GSKU_ENUMERATION_ELIGIBLE",
                new VerifiedGskuEnumerationFacts(
                    "locked-catalog.json",
                    "1.0.0",
                    new string('a', 64),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    ["pack-applicability", "uom"])));
        using var provider = new ServiceCollection()
            .AddSingleton(eligibility.Object)
            .BuildServiceProvider();
        var sut = new VerifiedGskuOperationalProvisioningRunner(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<VerifiedGskuOperationalProvisioningRunner>.Instance);

        await Task.WhenAll(
            sut.StartAsync(CancellationToken.None),
            sut.StartAsync(CancellationToken.None));

        eligibility.Verify(
            value => value.EvaluateAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        eligibility.Verify(
            value => value.EvaluateEnumerationAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LoaderConflict_FailsBeforePublicationOrAssignmentReadback()
    {
        var facts = CreateFacts();
        var authorization = new TestAuthorization();
        var eligibility = new Mock<IBusinessReferenceDataVerifiedGskuOperationalEligibility>();
        eligibility
            .Setup(value => value.EvaluateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedGskuOperationalEligibilityDecision(
                true,
                "VERIFIED_GSKU_OPERATIONAL_ELIGIBLE",
                facts,
                authorization));
        var loader = new Mock<IBusinessReferenceDataCatalogLoaderService>();
        var summary = new BusinessReferenceDataCatalogLoadSummary();
        summary.BlockedConflicts.Add("uom");
        loader
            .Setup(value => value.LoadVerifiedGskuCatalogFromFileAsync(
                facts.CatalogPath,
                facts.ActorId,
                facts.IdempotencyNamespace,
                facts.RequiredSetCodes,
                authorization,
                facts,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);
        using var provider = new ServiceCollection()
            .AddSingleton(eligibility.Object)
            .AddSingleton(loader.Object)
            .BuildServiceProvider();
        var sut = new VerifiedGskuOperationalProvisioningRunner(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<VerifiedGskuOperationalProvisioningRunner>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.StartAsync(CancellationToken.None));

        Assert.Equal("REFERENCE_CONTRACT_MISMATCH", exception.Message);
        loader.VerifyAll();
    }

    private static VerifiedGskuOperationalFacts CreateFacts() => new(
        "locked-catalog.json",
        "1.0.0",
        new string('a', 64),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "actor",
        "run",
        ["pack-applicability", "uom"]);

    private sealed class TestAuthorization : IBusinessReferenceDataVerifiedGskuOperationalAuthorization;
}
