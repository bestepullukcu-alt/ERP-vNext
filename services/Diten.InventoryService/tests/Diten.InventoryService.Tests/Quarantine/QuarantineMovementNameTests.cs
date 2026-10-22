using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using Xunit;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// WP-MVP1-NAMES1 — the hold's movement trail names who posted each movement AT THE TIME of the posting (<c>MovementLink.byUserName</c>, an
/// existing optional QUARANTINE member): the central audit queue's display name of the posting correlation and the posting user, this company
/// only; none recorded → absent (never the id, never the live label). One read for the whole trail (the production reader, counted).
/// </summary>
public sealed class QuarantineMovementNameTests : IClassFixture<QuarantineApiFactory>
{
    private const string Ayse = "Ayşe Demir";
    private readonly QuarantineApiFactory _factory;

    public QuarantineMovementNameTests(QuarantineApiFactory factory) => _factory = factory;

    private BsonDocument Mine(QuarantineScenario s) => new() { ["TenantId"] = s.Tenant.ToString("D"), ["ActorId"] = s.Ledger.User.ToString("D") };

    [Fact]
    public async Task The_trail_names_the_poster_at_the_time_of_the_posting_this_company_only()
    {
        var s = new QuarantineScenario(_factory);
        var tx = await s.ReceiveAsync("40", await s.ReleasedLotAsync(), "QUARANTINE");
        var queue = _factory.Collection("central_audit_outbox");

        var unnamed = await s.HoldBySourceAsync(tx);                                           // the posting token had no name
        Assert.False(unnamed.GetProperty("movements")[0].TryGetProperty("byUserName", out _));

        Assert.True(queue.UpdateMany(Mine(s), new BsonDocument("$set", new BsonDocument("ActorDisplayName", Ayse))).ModifiedCount > 0);
        var reads = _factory.AuditNameReads.Count(r => r.Tenant == s.Tenant);
        var named = await s.HoldBySourceAsync(tx);
        Assert.Equal(Ayse, named.GetProperty("movements").EnumerateArray().Single(m => m.GetProperty("transactionId").GetString() == tx)
            .GetProperty("byUserName").GetString());
        Assert.Contains(_factory.AuditNameReads.Where(r => r.Tenant == s.Tenant).Skip(reads), r => r.Company == s.Ledger.LegalEntity);

        // the same row moved to another company of the tenant → no name
        queue.UpdateMany(Mine(s), new BsonDocument("$set", new BsonDocument("CompanyId", Guid.NewGuid().ToString("D"))));
        var other = await s.HoldBySourceAsync(tx);
        Assert.False(other.GetProperty("movements")[0].TryGetProperty("byUserName", out _));
    }
}
