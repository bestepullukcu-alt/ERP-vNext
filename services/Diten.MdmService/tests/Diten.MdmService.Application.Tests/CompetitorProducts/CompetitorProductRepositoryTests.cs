using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Persistence.Configurations;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests.CompetitorProducts;

/// <summary>WP-VW-W4 (W4-BE-b2) — the Mongo repositories on a throw-away database (<c>MdmTestMongo</c>, the run's own replica set):
/// the class maps round-trip prices / history / the merge trace, Guid FKs are stored as standard UUIDs (queryable), reads
/// and writes are tenant-scoped, a replace is a compare-and-swap.</summary>
public sealed class CompetitorProductRepositoryTests
{
    private static readonly Guid Tenant = CompetitorHarness.Tenant;
    private static readonly Guid OtherTenant = CompetitorHarness.OtherTenant;

    [Fact]
    public async Task The_record_round_trips_is_tenant_scoped_and_replaces_only_on_the_expected_version()
    {
        CompetitorProductClassMaps.Register();
        var settings = MongoClientSettings.FromConnectionString(MdmTestMongo.ConnectionString); // main-sync 2026-10-14: shared Mongo (MdmTestMongo.SharedPort) default removed (BL-595), owner approved
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        var client = new MongoClient(settings);
        var name = $"diten_mdm_competitor_tests_{Guid.NewGuid():N}";
        var database = client.GetDatabase(name);
        try
        {
            var products = new CompetitorProductRepository(database);
            var record = new CompetitorProduct
            {
                TenantId = Tenant, Name = "Vastarel MR", Company = "Servier", NormalizedKey = "servier|vastarelmr",
                Status = CompetitorProductStatuses.Active, Aliases = ["Vastrel"], ActiveIngredients = ["trimetazidine"],
                CompetesWithGlobalProductIds = [CompetitorHarness.Tutukon],
                CountryPrices = [new CompetitorCountryPrice { MarketCode = "TR", BoxPrice = 123.4567m, CurrencyCode = "TRY", ReimbursementStatusCode = "reimbursed", ValidFrom = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc) }],
                CountryPriceHistory = [new CompetitorPriceHistoryEntry { MarketCode = "TR", BoxPrice = 100m, CurrencyCode = "TRY", ReimbursementStatusCode = "unknown", ValidFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), ValidTo = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), Reason = "changed" }],
                MergedFrom = [new CompetitorMergeTrace { SourceId = Guid.NewGuid(), SourceName = "Vastrel", MergedAt = DateTime.UtcNow }]
            };
            await products.InsertAsync(record, default);

            Assert.Null(await products.GetAsync(OtherTenant, record.Id, default));
            Assert.Null(await products.GetAsync(Guid.Empty, record.Id, default));
            Assert.Empty(await products.ListAsync(OtherTenant, default));
            var loaded = (await products.GetAsync(Tenant, record.Id, default))!;
            Assert.Equal(123.4567m, Assert.Single(loaded.CountryPrices).BoxPrice);
            Assert.Equal(new DateTime(2026, 7, 1), Assert.Single(loaded.CountryPriceHistory).ValidTo);
            Assert.Equal("Vastrel", Assert.Single(loaded.MergedFrom).SourceName);

            // a Guid FK is a standard UUID: a raw query by it finds the record (the class-map GUID trap)
            var raw = database.GetCollection<BsonDocument>(CompetitorProductRepository.CollectionName);
            Assert.Equal(1, await raw.CountDocumentsAsync(new BsonDocument("CompetesWithGlobalProductIds",
                new BsonBinaryData(CompetitorHarness.Tutukon, GuidRepresentation.Standard))));

            var stale = (await products.GetAsync(Tenant, record.Id, default))!;
            loaded.Status = CompetitorProductStatuses.Inactive;
            Assert.True(await products.ReplaceAsync(loaded, loaded.Version, default));
            stale.Name = "stale";
            Assert.False(await products.ReplaceAsync(stale, stale.Version, default));
            var foreign = (await products.GetAsync(Tenant, record.Id, default))!;
            foreign.TenantId = OtherTenant;
            Assert.False(await products.ReplaceAsync(foreign, foreign.Version, default));
            var stored = (await products.GetAsync(Tenant, record.Id, default))!;
            Assert.Equal((CompetitorProductStatuses.Inactive, "Vastarel MR", 1), (stored.Status, stored.Name, stored.Version));

            var decisions = new CompetitorProposalDecisionRepository(database);
            var decision = new CompetitorProposalDecision { TenantId = Tenant, ProposalId = record.Id, Action = CompetitorDecisionActions.Merge, TargetId = Guid.NewGuid(), MoveMarketCodes = ["TR"] };
            await decisions.InsertAsync(decision, default);
            Assert.Equal(decision.Id, (await decisions.GetByProposalAsync(Tenant, record.Id, default))!.Id);
            Assert.Null(await decisions.GetByProposalAsync(OtherTenant, record.Id, default));
            Assert.Single(await decisions.ListByProposalsAsync(Tenant, [record.Id], default));
            Assert.Equal(["TR"], (await decisions.GetAsync(Tenant, decision.Id, default))!.MoveMarketCodes);
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }
}
