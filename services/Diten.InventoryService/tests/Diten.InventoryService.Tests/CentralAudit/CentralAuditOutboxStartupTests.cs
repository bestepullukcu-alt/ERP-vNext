using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.CentralAudit;

/*
 * FU-BL501-OUTBOX-RACE — ON A FRESH DATABASE THE OUTBOX EXISTS BEFORE THE FIRST REQUEST.
 *
 * Reported by MVP-6 (2026-10-22): SupplyChain's first write on a fresh database answered 500. The command's transaction
 * created central_audit_outbox implicitly (its enlisted insert) while the dispatcher's background createIndexes created the
 * same namespace outside it; the transaction's commit then fails ("Collection namespace … is already in use").
 *
 * The real Inventory host on its own replica set, pointed at a DEDICATED database dropped before the host starts (the race
 * needs an empty database; the suite's shared DB-010 database already has the collection). The background dispatcher is
 * off, so only what AddCentralAudit runs before the host serves requests can have created the collection:
 *   host started                                  → the outbox collection and both indexes are there
 *   first enlisted transaction + the dispatcher's
 *   createIndexes at the same moment              → the transaction commits and its row is there
 */
public sealed class CentralAuditOutboxStartupTests
{
    [Fact]
    public async Task On_a_fresh_database_the_outbox_and_its_indexes_exist_once_the_host_has_started()
    {
        using var factory = new FreshOutboxDatabaseFactory("diten_inventory_outbox_start_tests");
        _ = factory.Services;

        var names = await (await factory.Database.ListCollectionNamesAsync()).ToListAsync();
        Assert.Contains(CentralAuditOutboxRecord.CollectionName, names);
        var indexes = await (await factory.Database.GetCollection<BsonDocument>(CentralAuditOutboxRecord.CollectionName)
            .Indexes.ListAsync()).ToListAsync();
        var indexNames = indexes.Select(i => i["name"].AsString).ToList();
        Assert.Contains("ix_central_audit_outbox_status_next_attempt", indexNames);
        Assert.Contains("ix_central_audit_outbox_tenant_created", indexNames);
    }

    /// <summary>
    /// The reported interleaving, made deterministic: the first command's transaction enlists its row, the dispatcher's
    /// index step runs before that transaction commits. Bare StartTransaction / Commit — no WithTransactionAsync retry to
    /// hide the conflict.
    /// </summary>
    [Fact]
    public async Task The_first_enlisted_transaction_on_a_fresh_database_commits_while_the_dispatcher_creates_the_indexes()
    {
        using var factory = new FreshOutboxDatabaseFactory("diten_inventory_outbox_race_tests");
        _ = factory.Services;
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ICentralAuditOutboxStore>();
        var row = new CentralAuditOutboxRecord
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            SourceService = "Diten.InventoryService",
            RequestType = "FirstCommandOnAFreshDatabase",
            WrittenInCommandTransaction = true,
            Status = CentralAuditOutboxStatus.AwaitingCommand,
            OccurredAtUtc = DateTime.UtcNow,
            NextAttemptAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };

        using var session = await scope.ServiceProvider.GetRequiredService<IMongoClient>().StartSessionAsync();
        session.StartTransaction();
        await store.InsertInTransactionAsync(session, row, CancellationToken.None);
        await store.CreateIndexesAsync(CancellationToken.None);
        await session.CommitTransactionAsync();

        var outbox = factory.Database.GetCollection<CentralAuditOutboxRecord>(CentralAuditOutboxRecord.CollectionName);
        Assert.Equal(row.Id, Assert.Single(await outbox.Find(x => x.Id == row.Id).ToListAsync()).Id);
    }

    /// <summary>The real host on a dedicated, freshly dropped database; the background dispatcher off.</summary>
    private sealed class FreshOutboxDatabaseFactory : InventoryApiFactory
    {
        private readonly string _databaseName;

        public FreshOutboxDatabaseFactory(string databaseName)
        {
            _databaseName = databaseName;
            Client = new MongoClient(TestSettings.MongoConnectionString);
            Client.DropDatabase(databaseName);
        }

        public IMongoClient Client { get; }

        public new IMongoDatabase Database => Client.GetDatabase(_databaseName);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("CentralAudit:DispatcherEnabled", "false");
            builder.ConfigureTestServices(services =>
                services.Replace(ServiceDescriptor.Scoped<IMongoDatabase>(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(_databaseName))));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                Client.DropDatabase(_databaseName);
            }
        }
    }
}
