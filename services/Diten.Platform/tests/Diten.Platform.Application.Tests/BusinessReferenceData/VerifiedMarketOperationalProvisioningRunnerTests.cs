using Diten.Platform.API.Configuration;
using Diten.Platform.API.Services.BusinessReferenceData;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class VerifiedMarketOperationalProvisioningRunnerTests
{
    [Fact]
    public async Task FreshTarget_UsesOnlyAuthorizedOverloadAndRequiresExactCompletion()
    {
        var facts = Facts();
        var authorization = new Authorization();
        var eligibility = Eligible(facts, authorization);
        var preflight = new Mock<IVerifiedMarketOperationalPreflight>(MockBehavior.Strict);
        preflight.Setup(x => x.VerifyBeforeWriteAsync(facts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedMarketOperationalTargetPreflightResult(
                VerifiedMarketOperationalTargetDisposition.Fresh,
                "VERIFIED_MARKET_OPERATIONAL_TARGET_FRESH"));
        preflight.Setup(x => x.VerifyCompletionAsync(facts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedMarketOperationalTargetPreflightResult(
                VerifiedMarketOperationalTargetDisposition.ExactReplay,
                "VERIFIED_MARKET_OPERATIONAL_TARGET_EXACT_REPLAY"));
        var loader = new Mock<IBusinessReferenceDataCatalogLoaderService>(MockBehavior.Strict);
        loader.Setup(x => x.LoadVerifiedMarketCatalogFromFileAsync(
                facts.CatalogPath,
                facts.ActorId,
                facts.IdempotencyNamespace,
                authorization,
                facts,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BusinessReferenceDataCatalogLoadSummary());
        var repository = VerifiedPublicationRepository(facts);

        await Runner(eligibility.Object, preflight.Object, loader.Object, repository.Object).RunAsync();

        preflight.VerifyAll();
        loader.VerifyAll();
        loader.Verify(x => x.LoadVerifiedMarketCatalogFromFileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.VerifyAll();
    }

    [Fact]
    public async Task ManualReconciliationPreflight_BlocksBeforeLoader()
    {
        var facts = Facts();
        var authorization = new Authorization();
        var preflight = new Mock<IVerifiedMarketOperationalPreflight>();
        preflight.Setup(x => x.VerifyBeforeWriteAsync(facts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedMarketOperationalTargetPreflightResult(
                VerifiedMarketOperationalTargetDisposition.ManualReconciliationRequired,
                "VERIFIED_MARKET_OPERATIONAL_TARGET_PREEXISTING"));
        var loader = new Mock<IBusinessReferenceDataCatalogLoaderService>(MockBehavior.Strict);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Runner(
                Eligible(facts, authorization).Object,
                preflight.Object,
                loader.Object,
                new Mock<IBusinessReferenceDataStewardshipRepository>(MockBehavior.Strict).Object).RunAsync());

        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_TARGET_PREEXISTING", exception.Message);
        loader.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LoaderFailure_IsManualReconciliationAndNeverAutoRetries()
    {
        var facts = Facts();
        var authorization = new Authorization();
        var preflight = new Mock<IVerifiedMarketOperationalPreflight>();
        preflight.Setup(x => x.VerifyBeforeWriteAsync(facts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedMarketOperationalTargetPreflightResult(
                VerifiedMarketOperationalTargetDisposition.Fresh,
                "fresh"));
        var loader = new Mock<IBusinessReferenceDataCatalogLoaderService>();
        loader.Setup(x => x.LoadVerifiedMarketCatalogFromFileAsync(
                facts.CatalogPath,
                facts.ActorId,
                facts.IdempotencyNamespace,
                authorization,
                facts,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit append failed"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Runner(
                Eligible(facts, authorization).Object,
                preflight.Object,
                loader.Object,
                new Mock<IBusinessReferenceDataStewardshipRepository>().Object).RunAsync());

        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_MANUAL_RECONCILIATION_REQUIRED", exception.Message);
        Assert.Equal("audit append failed", exception.InnerException?.Message);
        loader.Verify(x => x.LoadVerifiedMarketCatalogFromFileAsync(
            facts.CatalogPath,
            facts.ActorId,
            facts.IdempotencyNamespace,
            authorization,
            facts,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompletionThatIsNotExactReplay_IsFalseSuccess()
    {
        var facts = Facts();
        var authorization = new Authorization();
        var preflight = new Mock<IVerifiedMarketOperationalPreflight>();
        preflight.SetupSequence(x => x.VerifyBeforeWriteAsync(facts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedMarketOperationalTargetPreflightResult(VerifiedMarketOperationalTargetDisposition.Fresh, "fresh"));
        preflight.Setup(x => x.VerifyCompletionAsync(facts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedMarketOperationalTargetPreflightResult(
                VerifiedMarketOperationalTargetDisposition.ManualReconciliationRequired,
                "audit proof missing"));
        var loader = new Mock<IBusinessReferenceDataCatalogLoaderService>();
        loader.Setup(x => x.LoadVerifiedMarketCatalogFromFileAsync(
                facts.CatalogPath,
                facts.ActorId,
                facts.IdempotencyNamespace,
                authorization,
                facts,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BusinessReferenceDataCatalogLoadSummary());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Runner(
                Eligible(facts, authorization).Object,
                preflight.Object,
                loader.Object,
                new Mock<IBusinessReferenceDataStewardshipRepository>().Object).RunAsync());

        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_MANUAL_RECONCILIATION_REQUIRED", exception.Message);
    }

    [Fact]
    public async Task ExactReplay_SkipsLoaderAndRequiresVerifiedPublicationReadback()
    {
        var facts = Facts();
        var authorization = new Authorization();
        var preflight = new Mock<IVerifiedMarketOperationalPreflight>();
        preflight.Setup(x => x.VerifyBeforeWriteAsync(facts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedMarketOperationalTargetPreflightResult(
                VerifiedMarketOperationalTargetDisposition.ExactReplay,
                "exact"));
        preflight.Setup(x => x.VerifyCompletionAsync(facts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedMarketOperationalTargetPreflightResult(
                VerifiedMarketOperationalTargetDisposition.ExactReplay,
                "exact"));
        var loader = new Mock<IBusinessReferenceDataCatalogLoaderService>(MockBehavior.Strict);
        var repository = VerifiedPublicationRepository(facts);

        await Runner(Eligible(facts, authorization).Object, preflight.Object, loader.Object, repository.Object).RunAsync();

        loader.VerifyNoOtherCalls();
        repository.VerifyAll();
    }

    [Fact]
    public async Task MissingVerifiedPublicationReadback_IsFalseSuccess()
    {
        var facts = Facts();
        var authorization = new Authorization();
        var preflight = new Mock<IVerifiedMarketOperationalPreflight>();
        preflight.Setup(x => x.VerifyBeforeWriteAsync(facts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedMarketOperationalTargetPreflightResult(VerifiedMarketOperationalTargetDisposition.ExactReplay, "exact"));
        preflight.Setup(x => x.VerifyCompletionAsync(facts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedMarketOperationalTargetPreflightResult(VerifiedMarketOperationalTargetDisposition.ExactReplay, "exact"));
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Runner(
                Eligible(facts, authorization).Object,
                preflight.Object,
                new Mock<IBusinessReferenceDataCatalogLoaderService>(MockBehavior.Strict).Object,
                repository.Object).RunAsync());

        Assert.Equal("REFERENCE_PUBLICATION_NOT_VERIFIED", exception.Message);
    }

    [Fact]
    public async Task Cancellation_PropagatesWithoutWrapping()
    {
        var facts = Facts();
        var authorization = new Authorization();
        var preflight = new Mock<IVerifiedMarketOperationalPreflight>();
        preflight.Setup(x => x.VerifyBeforeWriteAsync(facts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedMarketOperationalTargetPreflightResult(VerifiedMarketOperationalTargetDisposition.Fresh, "fresh"));
        var loader = new Mock<IBusinessReferenceDataCatalogLoaderService>();
        loader.Setup(x => x.LoadVerifiedMarketCatalogFromFileAsync(
                facts.CatalogPath,
                facts.ActorId,
                facts.IdempotencyNamespace,
                authorization,
                facts,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Runner(
                Eligible(facts, authorization).Object,
                preflight.Object,
                loader.Object,
                new Mock<IBusinessReferenceDataStewardshipRepository>().Object).RunAsync(cts.Token));
    }

    [Fact]
    public void Runner_IsNotHostedService() =>
        Assert.False(typeof(IHostedService).IsAssignableFrom(typeof(VerifiedMarketOperationalProvisioningRunner)));

    [Theory]
    [InlineData(VerifiedMarketOperationalCommandClassification.Exact, "--run-verified-market-provisioning")]
    [InlineData(VerifiedMarketOperationalCommandClassification.Invalid, "--RUN-VERIFIED-MARKET-PROVISIONING")]
    [InlineData(VerifiedMarketOperationalCommandClassification.Invalid, "--run-verified-market-provisioning=true")]
    [InlineData(VerifiedMarketOperationalCommandClassification.NotRequested, "--urls=http://127.0.0.1:5057")]
    public void CommandLine_ClassifiesExactAndNearMatchArguments(
        VerifiedMarketOperationalCommandClassification expected,
        string argument) =>
        Assert.Equal(expected, VerifiedMarketOperationalCommandLine.Classify([argument]));

    [Fact]
    public void CommandLine_DuplicateOrConflictingArgumentsAreInvalid()
    {
        Assert.Equal(
            VerifiedMarketOperationalCommandClassification.Invalid,
            VerifiedMarketOperationalCommandLine.Classify([
                VerifiedMarketOperationalCommandLine.RunArgument,
                VerifiedMarketOperationalCommandLine.RunArgument]));
        Assert.Equal(
            VerifiedMarketOperationalCommandClassification.Invalid,
            VerifiedMarketOperationalCommandLine.Classify([
                VerifiedMarketOperationalCommandLine.RunArgument,
                "--urls=http://127.0.0.1:5057"]));
    }

    private static VerifiedMarketOperationalProvisioningRunner Runner(
        IBusinessReferenceDataVerifiedMarketOperationalEligibility eligibility,
        IVerifiedMarketOperationalPreflight preflight,
        IBusinessReferenceDataCatalogLoaderService loader,
        IBusinessReferenceDataStewardshipRepository repository) =>
        new(
            eligibility,
            preflight,
            loader,
            repository,
            new TenantContext(),
            new VerifiedMarketOperationalGovernanceAuditAdapter(
                new Mock<IAuditService>(MockBehavior.Strict).Object,
                preflight));

    private static Mock<IBusinessReferenceDataStewardshipRepository> VerifiedPublicationRepository(
        VerifiedMarketOperationalFacts facts)
    {
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>(MockBehavior.Strict);
        repository.Setup(x => x.GetVerifiedPublicationAsync(
                "market",
                facts.CatalogVersion,
                facts.CatalogFingerprint,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessReferenceDataVerifiedMarketResolveContractTests.Publication(
                facts.ReferenceTenantId,
                Guid.NewGuid(),
                []));
        return repository;
    }

    private static Mock<IBusinessReferenceDataVerifiedMarketOperationalEligibility> Eligible(
        VerifiedMarketOperationalFacts facts,
        Authorization authorization)
    {
        var mock = new Mock<IBusinessReferenceDataVerifiedMarketOperationalEligibility>();
        mock.Setup(x => x.EvaluateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedMarketOperationalEligibilityDecision(true, "ok", facts, authorization));
        mock.Setup(x => x.IsAuthorized(authorization, facts)).Returns(true);
        return mock;
    }

    private static VerifiedMarketOperationalFacts Facts() => new(
        BusinessReferenceDataTestHarness.GetSeedPath(VerifiedMarketOperationalProvisioningOptions.LockedCatalogFileName),
        VerifiedMarketOperationalProvisioningOptions.LockedCatalogVersion,
        VerifiedMarketOperationalProvisioningOptions.LockedCatalogFingerprint,
        Guid.Parse(VerifiedMarketOperationalProvisioningOptions.LockedReferenceTenantId),
        "actor",
        "market-run");

    private sealed class Authorization : IBusinessReferenceDataVerifiedMarketOperationalAuthorization;
}
