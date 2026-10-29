using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

/// <summary>
/// MOD-0048-FU01 §21.5 AC-R3 — what the production catalog loader does when a Treasury set is missing, on a fresh
/// tenant of the shared test database (nothing published before = the clean-database precondition):
/// (a) the catalog is absent → the lookup verification reports <c>published_set_not_found</c> and the load does not stop;
/// (b) the published-version read the consumable endpoint relies on finds nothing;
/// (d) published once, then a load without the catalog → the last published version stays.
/// (c) — a Treasury consumer answering 503 without writing — belongs to the Treasury service suites, not Platform.
/// </summary>
[Trait("Category", "Mongo")]
public sealed class TreasuryReferenceCatalogLoadMongoTests
{
    private const string ActorId = "trm-17-test-loader";

    private static async Task<string> CatalogWithoutTreasurySetsAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"brd-trm17-empty-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, """{ "catalog_version": "trm-17-empty", "module": "BusinessReferenceData", "sets": [] }""");
        return path;
    }

    [Fact]
    public async Task Missing_catalog_is_reported_as_published_set_not_found_and_the_load_continues()
    {
        await using var harness = await BusinessReferenceDataTestHarness.CreateSharedAsync();
        var empty = await CatalogWithoutTreasurySetsAsync();
        try
        {
            var summary = await harness.CreateLoader().LoadFromFileAsync(
                empty,
                harness.ReferenceTenantId,
                ActorId,
                [TreasuryReferenceCatalogTests.PaymentMethod, TreasuryReferenceCatalogTests.CurrencyMinorUnits]);

            Assert.Empty(summary.BlockedConflicts);
            foreach (var code in new[] { TreasuryReferenceCatalogTests.PaymentMethod, TreasuryReferenceCatalogTests.CurrencyMinorUnits })
            {
                var result = Assert.Single(summary.LookupResults, r => r.SetCode == code);
                Assert.Equal("published_set_not_found", result.Status);
            }

            using var _ = Diten.Platform.Application.Contracts.TenantScope.Begin(harness.TenantContext, harness.ReferenceTenantId);
            Assert.Empty(await harness.Repository.GetPublishedVersionsBySetCodeAsync(TreasuryReferenceCatalogTests.PaymentMethod, default));
            Assert.Empty(await harness.Repository.GetPublishedVersionsBySetCodeAsync(TreasuryReferenceCatalogTests.CurrencyMinorUnits, default));
        }
        finally
        {
            File.Delete(empty);
        }
    }

    [Fact]
    public async Task Published_once_then_loaded_without_the_catalog_keeps_the_last_published_version()
    {
        await using var harness = await BusinessReferenceDataTestHarness.CreateSharedAsync();
        var loader = harness.CreateLoader();

        var first = await loader.LoadFromFileAsync(
            TreasuryReferenceCatalogTests.CatalogPath(),
            harness.ReferenceTenantId,
            ActorId,
            [TreasuryReferenceCatalogTests.PaymentMethod]);
        Assert.Empty(first.BlockedConflicts);
        Assert.Equal("ok", Assert.Single(first.LookupResults).Status);

        var empty = await CatalogWithoutTreasurySetsAsync();
        try
        {
            var restart = await loader.LoadFromFileAsync(
                empty,
                harness.ReferenceTenantId,
                ActorId,
                [TreasuryReferenceCatalogTests.PaymentMethod]);

            var result = Assert.Single(restart.LookupResults);
            Assert.True(result.SetFound);
            Assert.NotEqual("published_set_not_found", result.Status);

            using var _ = Diten.Platform.Application.Contracts.TenantScope.Begin(harness.TenantContext, harness.ReferenceTenantId);
            var version = Assert.Single(await harness.Repository.GetPublishedVersionsBySetCodeAsync(TreasuryReferenceCatalogTests.PaymentMethod, default));
            Assert.Equal(
                ["BANK_TRANSFER", "SEPA_CREDIT_TRANSFER", "SWIFT_TRANSFER", "DOMESTIC_TRANSFER", "CHEQUE", "CASH"],
                version.Values.OrderBy(v => v.SortOrder).Select(v => v.ValueCode));
        }
        finally
        {
            File.Delete(empty);
        }
    }
}
