using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>AC8 / pack §13.9 / ASSUMPTION-0173-05.</summary>
public sealed class ReversalTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public ReversalTests(InventoryApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Reversal_restores_the_balance_with_a_new_transaction_and_leaves_the_original_untouched()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        var issueBody = s.Issue("20");
        var issue = await s.PostExpectingAsync(issueBody, 201);
        var issueId = issue.GetProperty("transactionId").GetString()!;
        var before = Raw(s, issueId);

        var reversal = await s.PostExpectingAsync(s.Reversal(issueBody, issueId), 201);

        Assert.NotEqual(issueId, reversal.GetProperty("transactionId").GetString());
        Assert.Equal(100m, await s.OnHandAsync());
        Assert.Equal(before.ToBson(), Raw(s, issueId).ToBson());

        var stored = Raw(s, reversal.GetProperty("transactionId").GetString()!);
        Assert.Equal(issueId, stored["ReversesTransactionId"].AsString);
        Assert.Equal(s.Warehouse, stored["ToWarehouseId"].AsString);
        Assert.Equal(BsonNull.Value, stored["FromLocationId"]);
    }

    [Fact]
    public async Task A_second_reversal_of_the_same_original_is_422_ALREADY_REVERSED()
    {
        var s = new InventoryScenario(_factory);
        var receiptBody = s.Receipt("100");
        var receipt = await s.PostExpectingAsync(receiptBody, 201);
        var id = receipt.GetProperty("transactionId").GetString()!;
        await s.PostExpectingAsync(s.Reversal(receiptBody, id), 201);

        var second = await s.PostExpectingAsync(s.Reversal(receiptBody, id), 422);

        Assert.Equal("ALREADY_REVERSED", second.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(2, s.Persisted("inventory_transactions").Count);
    }

    [Fact]
    public async Task Parallel_reversals_of_one_original_commit_exactly_once()
    {
        var s = new InventoryScenario(_factory);
        var receiptBody = s.Receipt("100");
        var id = (await s.PostExpectingAsync(receiptBody, 201)).GetProperty("transactionId").GetString()!;

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => s.PostAsync(s.Reversal(receiptBody, id))));

        Assert.Equal(1, results.Count(r => (int)r.StatusCode == 201));
        Assert.Equal(3, results.Count(r => (int)r.StatusCode == 422));
        Assert.Equal(0m, await s.OnHandAsync());
    }

    [Fact]
    public async Task Reversing_a_receipt_whose_stock_was_already_issued_is_422_INSUFFICIENT_STOCK()
    {
        var s = new InventoryScenario(_factory);
        var receiptBody = s.Receipt("100");
        var id = (await s.PostExpectingAsync(receiptBody, 201)).GetProperty("transactionId").GetString()!;
        await s.PostExpectingAsync(s.Issue("30"), 201);

        var refused = await s.PostExpectingAsync(s.Reversal(receiptBody, id), 422);

        Assert.Equal("INSUFFICIENT_STOCK", refused.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(70m, await s.OnHandAsync());
    }

    [Fact]
    public async Task A_reversal_cannot_itself_be_reversed()
    {
        var s = new InventoryScenario(_factory);
        var receiptBody = s.Receipt("100");
        var id = (await s.PostExpectingAsync(receiptBody, 201)).GetProperty("transactionId").GetString()!;
        var reversalBody = s.Reversal(receiptBody, id);
        var reversalId = (await s.PostExpectingAsync(reversalBody, 201)).GetProperty("transactionId").GetString()!;

        var refused = await s.PostExpectingAsync(s.Reversal(reversalBody, reversalId), 400);

        Assert.Equal("INVALID_REQUEST", refused.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Reversal_quantity_must_equal_the_original()
    {
        var s = new InventoryScenario(_factory);
        var receiptBody = s.Receipt("100");
        var id = (await s.PostExpectingAsync(receiptBody, 201)).GetProperty("transactionId").GetString()!;
        var wrong = s.Reversal(receiptBody, id);
        wrong["quantity"] = "99";

        await s.PostExpectingAsync(wrong, 400);
        Assert.Equal(100m, await s.OnHandAsync());
    }

    [Fact]
    public async Task Another_tenants_transaction_cannot_be_reversed()
    {
        var a = new InventoryScenario(_factory);
        var b = new InventoryScenario(_factory);
        var receiptBody = a.Receipt("100");
        var id = (await a.PostExpectingAsync(receiptBody, 201)).GetProperty("transactionId").GetString()!;

        var refused = await b.PostExpectingAsync(b.Reversal(receiptBody, id), 400);

        Assert.Equal("INVALID_REQUEST", refused.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(100m, await a.OnHandAsync());
    }

    [Fact]
    public async Task Reversal_must_not_name_its_own_sides()
    {
        var s = new InventoryScenario(_factory);
        var receiptBody = s.Receipt("100");
        var id = (await s.PostExpectingAsync(receiptBody, 201)).GetProperty("transactionId").GetString()!;
        var body = s.Reversal(receiptBody, id);
        body["fromWarehouseId"] = s.Warehouse;
        body["fromLocationId"] = s.LocationA;

        await s.PostExpectingAsync(body, 400);
    }

    private static BsonDocument Raw(InventoryScenario s, string id)
        => s.Factory.Collection("inventory_transactions").Find(new BsonDocument("_id", id)).Single();
}
