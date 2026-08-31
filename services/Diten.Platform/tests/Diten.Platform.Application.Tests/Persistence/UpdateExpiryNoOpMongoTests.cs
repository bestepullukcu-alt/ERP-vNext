using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class UpdateExpiryNoOpMongoTests
{
    [Fact]
    public async Task ExactSameExpiry_LeavesEveryMongoParticipantByteIdentical()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = mongo.CreateDatabase();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var expiry = DateTimeOffset.UtcNow.AddDays(2);
        var entitlement = Entitlement(tenantId, expiry);
        var otherEntitlement = Entitlement(otherTenantId, expiry.AddDays(1));
        var entitlements = database.GetCollection<TenantModuleEntitlement>("tenant_module_entitlements");
        await entitlements.InsertManyAsync([entitlement, otherEntitlement]);

        foreach (var collection in new[] { EntitlementStateVersionRepository.CollectionName, "quota_usages", "quota_events", "outbox_events", "audit_outbox" })
            await database.GetCollection<BsonDocument>(collection).InsertOneAsync(new BsonDocument
            {
                { "tenantId", tenantId.ToString() }, { "sentinel", collection }, { "value", 7 }
            });

        var before = await SnapshotAsync(database);
        var context = new PlatformDbContext(mongo.Client, database);
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        var repository = new TenantModuleEntitlementRepository(context, tenantContext);
        var executor = new CountingExecutor();
        var versions = new Mock<IEntitlementStateVersionRepository>(MockBehavior.Strict);
        var events = new Mock<ITransactionalIntegrationEventWriter>(MockBehavior.Strict);
        var audit = new Mock<ITransactionalAuditOutboxWriter>(MockBehavior.Strict);
        var currentUser = new Mock<ICurrentUserContext>();
        var handler = new UpdateTenantModuleEntitlementExpiryCommandHandler(
            repository, executor, versions.Object, events.Object, audit.Object, currentUser.Object);

        var result = await handler.Handle(
            new UpdateTenantModuleEntitlementExpiryCommand(
                tenantId,
                entitlement.Id,
                new UpdateTenantModuleEntitlementExpiryRequest(expiry, entitlement.Reason, entitlement.RowVersion)),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(0, executor.InvocationCount);
        Assert.Equal(before, await SnapshotAsync(database));
        Assert.Equal(0, await database.GetCollection<BsonDocument>("transaction_receipts")
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    private static TenantModuleEntitlement Entitlement(Guid tenantId, DateTimeOffset expiry) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, ModuleCode = "PPM", Source = EntitlementSource.ManualOverride,
        IsEnabled = true, ExpiryDateUtc = expiry, Reason = "unchanged", RowVersion = Guid.NewGuid().ToByteArray()
    };

    private static async Task<string> SnapshotAsync(IMongoDatabase database)
    {
        var snapshots = new List<string>();
        foreach (var collection in new[]
                 {
                     "tenant_module_entitlements", EntitlementStateVersionRepository.CollectionName, "quota_usages", "quota_events",
                     "outbox_events", "audit_outbox"
                 })
        {
            var documents = await database.GetCollection<BsonDocument>(collection)
                .Find(FilterDefinition<BsonDocument>.Empty)
                .Sort(Builders<BsonDocument>.Sort.Ascending("_id"))
                .ToListAsync();
            snapshots.Add(collection + ":" + Convert.ToHexString(documents.SelectMany(x => x.ToBson()).ToArray()));
        }

        return string.Join("|", snapshots);
    }

    private sealed class CountingExecutor : IPlatformTransactionExecutor
    {
        public int InvocationCount { get; private set; }
        public Task<T> ExecuteAsync<T>(Func<IPlatformTransactionSession, CancellationToken, Task<T>> body, CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            throw new Xunit.Sdk.XunitException("A no-op must not start a transaction.");
        }
    }
}
