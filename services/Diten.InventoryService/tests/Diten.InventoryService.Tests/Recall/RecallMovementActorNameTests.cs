using Diten.InventoryService.Persistence.Recall;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-MVP1-FU-CLEAN2 (FU-0173-MOV-ACTORNAME) — a quarantine movement posted IN THE BACKGROUND for a recall (the KR-B sweeper: no token, a new
/// correlation, so the central audit queue has no name for it) names the approver: the approval stores the approver's token name on the
/// quarantine request (<c>RequestedByName</c>), the sweeper hands it to the posting, the posting stores it as <c>CreatedByName</c>, and the
/// ledger answers it as <c>createdBy.name</c>. Real approval, real sweeper, real MOD-0175 handler and posting, the suite's own Mongo.
/// </summary>
public sealed class RecallMovementActorNameTests : IClassFixture<RecallApiFactory>
{
    private const string Approver = "Zeynep Kaya";
    private readonly RecallApiFactory _factory;

    public RecallMovementActorNameTests(RecallApiFactory factory) => _factory = factory;

    private static readonly DateTimeOffset Later = DateTimeOffset.UtcNow + RecallQuarantineSweeper.PendingAge + TimeSpan.FromMinutes(1);

    private static List<BsonDocument> QuarantineMoves(RecallScenario s)
        => s.Persisted("inventory_transactions").Where(d => d.Contains("ToStockStatus") && d["ToStockStatus"].ToString() is "Quarantine" or "QUARANTINE" or "2").ToList();

    private async Task<(RecallScenario S, string Lot, BsonDocument Request)> PendingAsync(string? approverName)
    {
        var s = new RecallScenario(_factory);
        var lot = await s.LotAsync();
        await s.Lots.PostOkAsync(s.Lots.Receipt("6", lot, movementType: "GOODS_RECEIPT_PO"));
        _factory.QuarantineCrash[lot] = "Before";                       // the in-request dispatch fails: the sweeper sends it later
        var (caseId, _) = await RecallR3.ApproveAsync(s, lot, approverName: approverName);
        var request = Assert.Single(RecallR3.Requests(s, caseId), r => r["LotId"].AsString == lot);
        Assert.Equal("Pending", request["Status"].AsString);
        _factory.QuarantineCrash.TryRemove(lot, out _);
        Assert.Empty(QuarantineMoves(s));
        return (s, lot, request);
    }

    [Fact]
    public async Task A_background_recall_quarantine_movement_names_the_approver_from_the_stored_name()
    {
        var (s, _, request) = await PendingAsync(Approver);
        Assert.Equal(Approver, request["RequestedByName"].AsString);    // stored at approval (the token name)

        Assert.Equal(1, await _factory.Services.GetRequiredService<RecallQuarantineSweeper>().SweepAsync(Later, CancellationToken.None, [s.Tenant]));

        var move = Assert.Single(QuarantineMoves(s));
        Assert.Equal(s.Qa2.ToString(), move["CreatedBy"].ToString());
        Assert.Equal(Approver, move["CreatedByName"].AsString);
        // the background posting's correlation has no queue name: the name can ONLY come from the stored one
        var queue = s.Persisted("central_audit_outbox").Where(d => d.Contains("CorrelationId") && d["CorrelationId"].ToString() == move["CorrelationId"].ToString()
                                                                    && d.Contains("ActorDisplayName") && d["ActorDisplayName"].IsString).ToList();
        Assert.Empty(queue);

        var (status, page, _) = await s.SendAsync(HttpMethod.Get, $"/api/inventory/transactions?itemId={move["ItemId"]}&pageSize=50",
            s.Mint(s.Qa2, TestTokens.AllPermissions));
        Assert.Equal(200, status);
        var row = Assert.Single(page.GetProperty("items").EnumerateArray(), r => r.GetProperty("transactionId").GetString() == move["_id"].ToString());
        Assert.Equal((s.Qa2.ToString("D"), Approver), (row.GetProperty("createdBy").GetProperty("userId").GetString(), row.GetProperty("createdBy").GetProperty("name").GetString()));
    }

    [Fact]
    public async Task An_unnamed_approver_leaves_the_background_movement_without_a_name_never_invented()
    {
        var (s, _, request) = await PendingAsync(null);
        Assert.True(!request.Contains("RequestedByName") || request["RequestedByName"].IsBsonNull);

        await _factory.Services.GetRequiredService<RecallQuarantineSweeper>().SweepAsync(Later, CancellationToken.None, [s.Tenant]);

        var move = Assert.Single(QuarantineMoves(s));
        Assert.True(!move.Contains("CreatedByName") || move["CreatedByName"].IsBsonNull);
    }

    [Fact]
    public async Task Each_tenants_background_movement_carries_only_its_own_approvers_name()
    {
        var (s1, _, _) = await PendingAsync("Ayşe Demir");
        var (s2, _, _) = await PendingAsync("Mehmet Öz");
        Assert.NotEqual((s1.Tenant, s1.Ledger.LegalEntity), (s2.Tenant, s2.Ledger.LegalEntity));

        Assert.Equal(2, await _factory.Services.GetRequiredService<RecallQuarantineSweeper>().SweepAsync(Later, CancellationToken.None, [s1.Tenant, s2.Tenant]));

        var m1 = Assert.Single(QuarantineMoves(s1));
        var m2 = Assert.Single(QuarantineMoves(s2));
        Assert.Equal(("Ayşe Demir", s1.Ledger.LegalEntity.ToString()), (m1["CreatedByName"].AsString, m1["LegalEntityId"].ToString()));
        Assert.Equal(("Mehmet Öz", s2.Ledger.LegalEntity.ToString()), (m2["CreatedByName"].AsString, m2["LegalEntityId"].ToString()));
    }
}
