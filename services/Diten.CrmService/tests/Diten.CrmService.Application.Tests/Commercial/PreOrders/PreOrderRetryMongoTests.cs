using System.Reflection;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Persistence.Repositories.Commercial;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.CrmService.Application.Tests.Commercial.PreOrders;

/// <summary>
/// WP-VW-W6 W6-BE-c1 — on a real mongod (the lane's own port; skipped without CRM_TEST_MONGO_URI, never 27017): the retry
/// scheduler's due query finds exactly the failed transmissions whose retry is due, across tenants, oldest first, served
/// by its index; the retry time round-trips as a plain number.
/// </summary>
public sealed class PreOrderRetryMongoTests
{
    [Fact]
    public async Task M_C1_the_due_query_finds_only_due_failed_transmissions_across_tenants_and_uses_its_index()
    {
        var uri = Environment.GetEnvironmentVariable("CRM_TEST_MONGO_URI");
        if (string.IsNullOrWhiteSpace(uri) || uri.Contains(":27017", StringComparison.Ordinal)) return; // never the dev database
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var client = new MongoClient(uri);
        var name = "W6_BEC1_" + Guid.NewGuid().ToString("N");
        try
        {
            var db = client.GetDatabase(name);
            typeof(Diten.CrmService.Persistence.DependencyInjection).GetMethod("TryEnsureIndexes", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [db]);
            var orders = new PreOrderRepository(db);
            var now = new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

            PreOrder Order(Guid tenant, string status, DateTimeOffset? next, bool deleted = false) => new()
            {
                TenantId = tenant, Code = "MG-ORD-" + Guid.NewGuid().ToString("N")[..6], Status = status, IsDeleted = deleted,
                Transmission = new PreOrderTransmission { LastStatus = "failed", NextAttemptAtTicks = next?.UtcTicks }
            };

            var older = Order(PreOrderWorld.Tenant, PreOrderStatuses.TransmissionFailed, now.AddMinutes(-30));
            var dueOther = Order(PreOrderWorld.OtherTenant, PreOrderStatuses.TransmissionFailed, now.AddMinutes(-1));
            var future = Order(PreOrderWorld.Tenant, PreOrderStatuses.TransmissionFailed, now.AddMinutes(10));
            var spent = Order(PreOrderWorld.Tenant, PreOrderStatuses.TransmissionFailed, null);
            var sent = Order(PreOrderWorld.Tenant, PreOrderStatuses.Transmitted, now.AddMinutes(-5));
            var deleted = Order(PreOrderWorld.Tenant, PreOrderStatuses.TransmissionFailed, now.AddMinutes(-5), deleted: true);
            foreach (var o in new[] { older, dueOther, future, spent, sent, deleted })
            {
                await orders.InsertAsync(o, default);
            }

            var due = await orders.ListDueTransmissionRetriesAsync(now.UtcTicks, 50, default);
            Assert.Equal(new[] { (PreOrderWorld.Tenant, older.Id), (PreOrderWorld.OtherTenant, dueOther.Id) }, due);

            var stored = await orders.GetByIdAsync(PreOrderWorld.Tenant, older.Id, default);
            Assert.Equal(now.AddMinutes(-30).UtcTicks, stored!.Transmission!.NextAttemptAtTicks);
            var raw = await db.GetCollection<BsonDocument>(PreOrderRepository.CollectionName).Find(new BsonDocument("_id", older.Id.ToString("D"))).FirstOrDefaultAsync()
                      ?? await db.GetCollection<BsonDocument>(PreOrderRepository.CollectionName).Find(FilterDefinition<BsonDocument>.Empty).FirstAsync();
            Assert.Equal(BsonType.Int64, raw["Transmission"]["NextAttemptAtTicks"].BsonType);

            // W6-BE-c2 (FU-W6-APPROVED-BACKLOG) — the approved orders that never had an attempt, decided before the grace
            PreOrder Approved(DateTimeOffset decided, bool tried = false) => new()
            {
                TenantId = PreOrderWorld.Tenant, Code = "MG-ORD-" + Guid.NewGuid().ToString("N")[..6], Status = PreOrderStatuses.Approved,
                Approval = new PreOrderApproval { Decision = PreOrderDecisions.Approve, DecidedAt = decided },
                Transmission = tried ? new PreOrderTransmission { LastStatus = "failed" } : null
            };
            var old = Approved(now.AddMinutes(-10));
            var fresh = Approved(now.AddSeconds(-30));
            var tried = Approved(now.AddMinutes(-10), tried: true);
            foreach (var o in new[] { old, fresh, tried })
            {
                await orders.InsertAsync(o, default);
            }

            Assert.Equal(new[] { (PreOrderWorld.Tenant, old.Id) }, await orders.ListApprovedUntransmittedAsync(now.AddMinutes(-2), 50, default));

            var indexes = await (await db.GetCollection<BsonDocument>(PreOrderRepository.CollectionName).Indexes.ListAsync()).ToListAsync();
            Assert.Contains(indexes, i => i["name"] == "ix_commercial_pre_orders_status_next_attempt");
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }
}
