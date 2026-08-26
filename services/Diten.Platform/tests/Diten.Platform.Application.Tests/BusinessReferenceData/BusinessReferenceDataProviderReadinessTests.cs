using Diten.Platform.API.Configuration;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataProviderReadinessTests
{
    [Fact]
    public async Task DisabledProvisioningWithCompleteDurableState_IsHealthy()
    {
        var referenceTenantId = Guid.NewGuid();
        var consumerTenantId = Guid.NewGuid();
        var facts = CreateDisabledProvisioningFacts(referenceTenantId, consumerTenantId);
        var repository = CompleteRepository(referenceTenantId, consumerTenantId, facts);
        using var provider = BuildProvider(repository.Object);
        var sut = CreateSut(provider, enabled: false, enumerationEnabled: false, consumerTenantId: consumerTenantId);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        repository.Verify(value => value.GetVerifiedPublicationAsync(
            It.IsAny<string>(), facts.CatalogVersion, facts.CatalogFingerprint, It.IsAny<CancellationToken>()), Times.Exactly(2));
        repository.Verify(value => value.GetActiveTenantAssignmentAsync(
            consumerTenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task DisabledProvisioningWithIncompleteDurableState_IsUnhealthy()
    {
        var referenceTenantId = Guid.NewGuid();
        var consumerTenantId = Guid.NewGuid();
        var facts = CreateDisabledProvisioningFacts(referenceTenantId, consumerTenantId);
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>();
        repository.Setup(value => value.GetRequiredReferenceTenantId()).Returns(referenceTenantId);
        repository.Setup(value => value.GetVerifiedPublicationAsync(
                It.IsAny<string>(), facts.CatalogVersion, facts.CatalogFingerprint, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BusinessReferenceDataVerifiedPublication?)null);
        using var provider = BuildProvider(repository.Object);
        var sut = CreateSut(provider, enabled: false, enumerationEnabled: false, consumerTenantId: consumerTenantId);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        repository.Verify(value => value.GetActiveTenantAssignmentAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvalidProviderConfiguration_IsUnhealthyWithNoExceptionLeak()
    {
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>(MockBehavior.Strict);
        repository.Setup(value => value.GetRequiredReferenceTenantId())
            .Throws(new InvalidOperationException("REFERENCE_PROVIDER_CONFIGURATION_INVALID"));
        using var provider = BuildProvider(repository.Object);
        var sut = CreateSut(provider, enabled: false, enumerationEnabled: false);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Business reference data provider configuration or state is invalid.", result.Description);
    }

    [Fact]
    public async Task EnabledButIneligible_IsUnhealthyWithoutCatalogMutation()
    {
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>();
        repository.Setup(value => value.GetRequiredReferenceTenantId()).Returns(Guid.NewGuid());
        var eligibility = new Mock<IBusinessReferenceDataVerifiedGskuOperationalEligibility>();
        eligibility.Setup(value => value.EvaluateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedGskuOperationalEligibilityDecision(
                false,
                "VERIFIED_GSKU_OPERATIONAL_ARTIFACT_INVALID"));
        using var provider = BuildProvider(repository.Object, eligibility.Object);
        var sut = CreateSut(provider, enabled: true, enumerationEnabled: false);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        repository.Verify(value => value.GetVerifiedPublicationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnabledWithIncompleteVerifiedState_IsUnhealthyAndReadOnly()
    {
        var referenceTenantId = Guid.NewGuid();
        var facts = new VerifiedGskuOperationalFacts(
            "locked.json",
            "1.0.0",
            new string('a', 64),
            referenceTenantId,
            Guid.NewGuid(),
            "actor",
            "run",
            ["pack-applicability", "uom"]);
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>();
        repository.Setup(value => value.GetRequiredReferenceTenantId()).Returns(referenceTenantId);
        repository.Setup(value => value.GetVerifiedPublicationAsync(
                It.IsAny<string>(), facts.CatalogVersion, facts.CatalogFingerprint, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BusinessReferenceDataVerifiedPublication?)null);
        var eligibility = new Mock<IBusinessReferenceDataVerifiedGskuOperationalEligibility>();
        eligibility.Setup(value => value.EvaluateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedGskuOperationalEligibilityDecision(
                true,
                "VERIFIED_GSKU_OPERATIONAL_ELIGIBLE",
                facts,
                Mock.Of<IBusinessReferenceDataVerifiedGskuOperationalAuthorization>()));
        using var provider = BuildProvider(repository.Object, eligibility.Object);
        var sut = CreateSut(provider, enabled: true, enumerationEnabled: false);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        repository.Verify(value => value.EnsureActiveTenantAssignmentAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(value => value.CreateTenantAssignmentAsync(
            It.IsAny<Diten.Platform.Domain.Entities.BusinessReferenceDataTenantAssignment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnabledEligibleWithoutAuthorization_IsUnhealthyWithoutStateRead()
    {
        var referenceTenantId = Guid.NewGuid();
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>();
        repository.Setup(value => value.GetRequiredReferenceTenantId()).Returns(referenceTenantId);
        var eligibility = new Mock<IBusinessReferenceDataVerifiedGskuOperationalEligibility>();
        eligibility.Setup(value => value.EvaluateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedGskuOperationalEligibilityDecision(
                true,
                "VERIFIED_GSKU_OPERATIONAL_ELIGIBLE",
                new VerifiedGskuOperationalFacts(
                    "locked.json",
                    "1.0.0",
                    new string('a', 64),
                    referenceTenantId,
                    Guid.NewGuid(),
                    "actor",
                    "run",
                    ["pack-applicability", "uom"])));
        using var provider = BuildProvider(repository.Object, eligibility.Object);
        var sut = CreateSut(provider, enabled: true, enumerationEnabled: false);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        repository.Verify(value => value.GetVerifiedPublicationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(value => value.GetActiveTenantAssignmentAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnumerationOnlyWithCompleteVerifiedState_IsHealthyAndReadOnly()
    {
        var referenceTenantId = Guid.NewGuid();
        var consumerTenantId = Guid.NewGuid();
        var facts = CreateEnumerationFacts(referenceTenantId, consumerTenantId);
        var repository = CompleteRepository(referenceTenantId, consumerTenantId, facts);
        var eligibility = new Mock<IBusinessReferenceDataVerifiedGskuOperationalEligibility>(MockBehavior.Strict);
        eligibility.Setup(value => value.EvaluateEnumerationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedGskuEnumerationEligibilityDecision(
                true,
                "VERIFIED_GSKU_ENUMERATION_ELIGIBLE",
                facts));
        using var provider = BuildProvider(repository.Object, eligibility.Object);
        var sut = CreateSut(provider, enabled: false, enumerationEnabled: true);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        eligibility.Verify(value => value.EvaluateAsync(It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(value => value.GetVerifiedPublicationAsync(
            It.IsAny<string>(), facts.CatalogVersion, facts.CatalogFingerprint, It.IsAny<CancellationToken>()), Times.Exactly(2));
        repository.Verify(value => value.GetActiveTenantAssignmentAsync(
            consumerTenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        repository.Verify(value => value.EnsureActiveTenantAssignmentAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(value => value.CreateTenantAssignmentAsync(
            It.IsAny<BusinessReferenceDataTenantAssignment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnumerationOnlyWithIncompleteState_IsUnhealthy()
    {
        var referenceTenantId = Guid.NewGuid();
        var facts = CreateEnumerationFacts(referenceTenantId, Guid.NewGuid());
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>();
        repository.Setup(value => value.GetRequiredReferenceTenantId()).Returns(referenceTenantId);
        repository.Setup(value => value.GetVerifiedPublicationAsync(
                It.IsAny<string>(), facts.CatalogVersion, facts.CatalogFingerprint, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BusinessReferenceDataVerifiedPublication?)null);
        var eligibility = new Mock<IBusinessReferenceDataVerifiedGskuOperationalEligibility>();
        eligibility.Setup(value => value.EvaluateEnumerationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedGskuEnumerationEligibilityDecision(
                true,
                "VERIFIED_GSKU_ENUMERATION_ELIGIBLE",
                facts));
        using var provider = BuildProvider(repository.Object, eligibility.Object);
        var sut = CreateSut(provider, enabled: false, enumerationEnabled: true);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        repository.Verify(value => value.GetActiveTenantAssignmentAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BothEnabledWithMatchingDecisions_RequiresVerifiedStateAndIsHealthy()
    {
        var referenceTenantId = Guid.NewGuid();
        var consumerTenantId = Guid.NewGuid();
        var enumerationFacts = CreateEnumerationFacts(referenceTenantId, consumerTenantId);
        var operationalFacts = new VerifiedGskuOperationalFacts(
            enumerationFacts.CatalogPath,
            enumerationFacts.CatalogVersion,
            enumerationFacts.CatalogFingerprint,
            referenceTenantId,
            consumerTenantId,
            "actor",
            "run",
            enumerationFacts.RequiredSetCodes.ToArray());
        var repository = CompleteRepository(referenceTenantId, consumerTenantId, enumerationFacts);
        var eligibility = new Mock<IBusinessReferenceDataVerifiedGskuOperationalEligibility>();
        eligibility.Setup(value => value.EvaluateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedGskuOperationalEligibilityDecision(
                true,
                "VERIFIED_GSKU_OPERATIONAL_ELIGIBLE",
                operationalFacts,
                Mock.Of<IBusinessReferenceDataVerifiedGskuOperationalAuthorization>()));
        eligibility.Setup(value => value.EvaluateEnumerationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedGskuEnumerationEligibilityDecision(
                true,
                "VERIFIED_GSKU_ENUMERATION_ELIGIBLE",
                enumerationFacts));
        using var provider = BuildProvider(repository.Object, eligibility.Object);
        var sut = CreateSut(provider, enabled: true, enumerationEnabled: true);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        eligibility.VerifyAll();
    }

    private static ServiceProvider BuildProvider(
        IBusinessReferenceDataStewardshipRepository repository,
        IBusinessReferenceDataVerifiedGskuOperationalEligibility? eligibility = null)
    {
        var services = new ServiceCollection()
            .AddSingleton(repository)
            .AddSingleton<ITenantContext, TenantContext>();
        if (eligibility is not null)
        {
            services.AddSingleton(eligibility);
        }

        return services.BuildServiceProvider();
    }

    private static BusinessReferenceDataProviderReadinessHealthCheck CreateSut(
        ServiceProvider provider,
        bool enabled,
        bool enumerationEnabled,
        Guid? consumerTenantId = null) => new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new VerifiedGskuOperationalProvisioningOptions
            {
                Enabled = enabled,
                EnumerationEnabled = enumerationEnabled,
                ConsumerTenantId = consumerTenantId,
                ExpectedCatalogVersion = VerifiedGskuOperationalProvisioningOptions.LockedCatalogVersion,
                ExpectedCatalogFingerprint = VerifiedGskuOperationalProvisioningOptions.LockedCatalogFingerprint
            }));

    private static VerifiedGskuEnumerationFacts CreateEnumerationFacts(
        Guid referenceTenantId,
        Guid consumerTenantId) => new(
            "locked.json",
            "1.0.0",
            new string('a', 64),
            referenceTenantId,
            consumerTenantId,
            ["pack-applicability", "uom"]);

    private static VerifiedGskuEnumerationFacts CreateDisabledProvisioningFacts(
        Guid referenceTenantId,
        Guid consumerTenantId) => new(
            "disabled-runner",
            VerifiedGskuOperationalProvisioningOptions.LockedCatalogVersion,
            VerifiedGskuOperationalProvisioningOptions.LockedCatalogFingerprint,
            referenceTenantId,
            consumerTenantId,
            ["pack-applicability", "uom"]);

    private static Mock<IBusinessReferenceDataStewardshipRepository> CompleteRepository(
        Guid referenceTenantId,
        Guid consumerTenantId,
        VerifiedGskuEnumerationFacts facts)
    {
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>();
        repository.Setup(value => value.GetRequiredReferenceTenantId()).Returns(referenceTenantId);
        repository.Setup(value => value.GetVerifiedPublicationAsync(
                It.IsAny<string>(), facts.CatalogVersion, facts.CatalogFingerprint, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BusinessReferenceDataVerifiedPublication(
                new BusinessReferenceDataSet
                {
                    TenantId = referenceTenantId,
                    SetCode = "verified",
                    Name = "Verified",
                    ScopeType = "global"
                },
                new BusinessReferenceDataVersion { TenantId = referenceTenantId },
                new BusinessReferenceDataPublishOperation
                {
                    TenantId = referenceTenantId,
                    BusinessReferenceDataSetId = Guid.NewGuid(),
                    BusinessReferenceDataVersionId = Guid.NewGuid(),
                    IdempotencyKey = "verified",
                    ExpectedSetVersion = 1,
                    ExpectedTargetVersionToken = "verified"
                }));
        repository.Setup(value => value.GetActiveTenantAssignmentAsync(
                consumerTenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BusinessReferenceDataTenantAssignment
            {
                TenantId = referenceTenantId,
                ConsumerTenantId = consumerTenantId,
                SetCode = "verified",
                CreatedBy = "readiness-test"
            });
        return repository;
    }
}
