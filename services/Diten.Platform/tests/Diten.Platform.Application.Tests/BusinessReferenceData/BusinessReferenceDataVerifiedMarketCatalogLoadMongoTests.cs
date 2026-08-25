using Diten.Platform.Domain.Entities;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataVerifiedMarketCatalogLoadMongoTests : IAsyncLifetime
{
    private BusinessReferenceDataTestHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BusinessReferenceDataTestHarness.CreateAsync();

    public Task DisposeAsync() => _harness.DisposeAsync().AsTask();

    [Fact]
    public async Task ExplicitTestFixture_LoadsOnlyUnderReferenceTenantAndReadsBackVerifiedPublication()
    {
        var path = await VerifiedMarketTestFixture.CreateAsync();
        try
        {
            var summary = await _harness.CreateLoader()
                .LoadVerifiedMarketCatalogFromFileAsync(path, "verified-market-test");

            Assert.Empty(summary.BlockedConflicts);
            Assert.Equal(1, summary.SetsLoaded);
            Assert.Equal(3, summary.ValuesInserted);
            var set = Assert.Single(await _harness.Database
                .GetCollection<BusinessReferenceDataSet>("business_reference_data_sets")
                .Find(x => x.TenantId == _harness.ReferenceTenantId && x.SetCode == "market")
                .ToListAsync());
            Assert.NotNull(set.PublishedVersionId);
            Assert.Equal(0, await _harness.Database
                .GetCollection<BusinessReferenceDataSet>("business_reference_data_sets")
                .CountDocumentsAsync(x => x.TenantId != _harness.ReferenceTenantId && x.SetCode == "market"));

            var publication = await _harness.Repository.GetVerifiedPublicationAsync("market");
            Assert.NotNull(publication);
            Assert.Equal(set.PublishedVersionId, publication.Version.BusinessReferenceDataVersionId);
            Assert.True(publication.Version.IsImmutable);
            Assert.Equal(BusinessReferenceDataVersionStatus.Published, publication.Version.Status);
            Assert.Equal(BusinessReferenceDataPublishOperationState.COMPLETED, publication.Operation.OperationState);
            Assert.Equal(BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED, publication.Operation.PublishCheckpoint);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GenericLoader_CannotTurnMarketRowsIntoVerifiedProof()
    {
        var path = await VerifiedMarketTestFixture.CreateAsync();
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _harness.CreateLoader()
                .LoadFromFileAsync(path, Guid.NewGuid(), "legacy-pss-012", ["market"]));

            Assert.Equal("VERIFIED_MARKET_CATALOG_CONTRACT_REQUIRED", exception.Message);
            Assert.Null(await _harness.Repository.GetVerifiedPublicationAsync("market"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("duplicate")]
    [InlineData("lowercase")]
    public async Task MalformedActiveFixture_IsRejectedBeforeAnyCatalogWrite(string mutation)
    {
        var path = await VerifiedMarketTestFixture.CreateAsync(mutation);
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _harness.CreateLoader()
                .LoadVerifiedMarketCatalogFromFileAsync(path, "verified-market-test"));

            Assert.Equal("market_catalog_active_values_invalid", exception.Message);
            Assert.Equal(0, await _harness.Database
                .GetCollection<BusinessReferenceDataSet>("business_reference_data_sets")
                .CountDocumentsAsync(FilterDefinition<BusinessReferenceDataSet>.Empty));
        }
        finally
        {
            File.Delete(path);
        }
    }
}

internal static class VerifiedMarketTestFixture
{
    public static Task<string> CreateAsync(string? mutation = null, string catalogVersion = "market-test-fixture-v1")
    {
        var values = mutation switch
        {
            "empty" => """
                [{"value_code":"TR","display_name":"Turkiye","is_active":false,"sort_order":10,"attributes":{}}]
                """,
            "duplicate" => """
                [{"value_code":"TR","display_name":"Turkiye","is_active":true,"sort_order":10,"attributes":{}},
                 {"value_code":"TR","display_name":"Duplicate","is_active":true,"sort_order":20,"attributes":{}}]
                """,
            "lowercase" => """
                [{"value_code":"tr","display_name":"Turkiye","is_active":true,"sort_order":10,"attributes":{}}]
                """,
            "retire-tr" => """
                [{"value_code":"TR","display_name":"Turkiye","is_active":false,"sort_order":10,"attributes":{}},
                 {"value_code":"US","display_name":"United States","is_active":true,"sort_order":20,"attributes":{}}]
                """,
            "rename-tr" => """
                [{"value_code":"TR","display_name":"Changed Meaning","is_active":true,"sort_order":10,"attributes":{}},
                 {"value_code":"US","display_name":"United States","is_active":true,"sort_order":20,"attributes":{}}]
                """,
            _ => """
                [{"value_code":"TR","display_name":"Turkiye","is_active":true,"sort_order":10,"attributes":{}},
                 {"value_code":"US","display_name":"United States","is_active":true,"sort_order":20,"attributes":{}},
                 {"value_code":"DE","display_name":"Germany","is_active":false,"sort_order":30,"attributes":{}}]
                """
        };
        var payload = $$"""
            {
              "catalog_version": "{{catalogVersion}}",
              "module": "BusinessReferenceData",
              "note": "TEST FIXTURE ONLY - not an operational market artifact",
              "sets": [{
                "set_code": "market",
                "set_name": "Market",
                "scope_type": "global",
                "status": "Active",
                "description": "Explicit verified-market test fixture",
                "attribute_definitions": [],
                "values": {{values}}
              }]
            }
            """;
        var path = Path.Combine(Path.GetTempPath(), $"verified-market-test-{Guid.NewGuid():N}.json");
        return WriteAsync(path, payload);
    }

    private static async Task<string> WriteAsync(string path, string payload)
    {
        await File.WriteAllTextAsync(path, payload);
        return path;
    }
}
