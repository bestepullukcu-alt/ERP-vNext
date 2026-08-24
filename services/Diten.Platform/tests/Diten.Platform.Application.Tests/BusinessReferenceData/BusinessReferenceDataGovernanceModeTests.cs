using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataGovernanceModeTests : IAsyncLifetime
{
    private BusinessReferenceDataTestHarness _harness = null!;

    public async Task InitializeAsync()
    {
        _harness = await BusinessReferenceDataTestHarness.CreateAsync();
    }

    public Task DisposeAsync() => _harness.DisposeAsync().AsTask();

    [Fact]
    public async Task DisabledMockAndFailClosed_RejectBeforeCreatingOrAdvancingOperation()
    {
        var originalMode = Environment.GetEnvironmentVariable("BusinessReferenceData__GovernanceMode");
        try
        {
            foreach (var mode in new[] { "Disabled", "Mock", "FailClosed" })
            {
                Environment.SetEnvironmentVariable("BusinessReferenceData__GovernanceMode", mode);
                var eligibility = new RuntimeBusinessReferenceDataPublicationEligibility();
                var decision = eligibility.Evaluate();

                Assert.False(decision.IsEligible);
                Assert.Equal(mode, decision.GovernanceMode);
                Assert.Equal("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE", decision.ReasonCode);

                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    _harness.CreatePublishService(eligibility: eligibility).PublishVerifiedAsync(
                        Guid.NewGuid(),
                        "test-publisher",
                        Guid.NewGuid().ToString(),
                        $"governance-{mode}",
                        "Immediate",
                        null,
                        "target-token",
                        false,
                        null));
                Assert.Equal("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE", exception.Message);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("BusinessReferenceData__GovernanceMode", originalMode);
        }

        var operations = _harness.Database
            .GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations");
        Assert.Equal(0, await operations.CountDocumentsAsync(FilterDefinition<BusinessReferenceDataPublishOperation>.Empty));
    }

    [Fact]
    public async Task ExplicitTestOnlyEligibility_IsTheOnlyPositiveSeam()
    {
        var summary = await _harness.CreateLoader(eligibility: new EligiblePublicationForTests()).LoadVerifiedGskuCatalogFromFileAsync(
            BusinessReferenceDataTestHarness.GetArtifactPath(),
            "test-publisher",
            ["pack-applicability", "uom"]);

        Assert.Empty(summary.BlockedConflicts);
        Assert.Equal(2, summary.SetsLoaded);
        var completed = await _harness.Database
            .GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations")
            .CountDocumentsAsync(x => x.OperationState == BusinessReferenceDataPublishOperationState.COMPLETED);
        Assert.Equal(2, completed);
    }
}
