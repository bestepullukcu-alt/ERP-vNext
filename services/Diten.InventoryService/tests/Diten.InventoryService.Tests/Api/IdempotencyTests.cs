using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// AC4 / pack §13.1-2. GUARD UNDER TEST: PostMovementHandler's replay decision + the unique
/// (TenantId, LegalEntityId, IdempotencyKey) index. Sabotage target of D4.
/// </summary>
public sealed class IdempotencyTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public IdempotencyTests(InventoryApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Same_key_twice_writes_once_and_replays_the_original_result_with_200()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("100");

        var first = await s.PostExpectingAsync(body, 201);
        var second = await s.PostExpectingAsync(body, 200);

        Assert.True(second.GetProperty("idempotentReplay").GetBoolean());
        Assert.Equal(first.GetProperty("transactionId").GetString(), second.GetProperty("transactionId").GetString());
        Assert.Equal(first.GetProperty("transactionNumber").GetString(), second.GetProperty("transactionNumber").GetString());
        Assert.Equal("100.000", second.GetProperty("balanceSnapshot").GetProperty("onHand").GetString());

        Assert.Single(s.Persisted("inventory_transactions"));
        Assert.Single(s.Persisted("inventory_outbox"));
        Assert.Single(s.Persisted("inventory_audit"));
        var row = Assert.Single(s.Persisted("inventory_balances"));
        Assert.Equal(100m, row["OnHand"].AsDecimal);
        Assert.Equal(1, row["Version"].AsInt32);
    }

    [Fact]
    public async Task Replay_returns_the_ORIGINAL_snapshot_even_after_later_movements()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("100");
        await s.PostExpectingAsync(body, 201);
        await s.PostExpectingAsync(s.Issue("30"), 201);

        var replay = await s.PostExpectingAsync(body, 200);

        Assert.Equal("100.000", replay.GetProperty("balanceSnapshot").GetProperty("onHand").GetString());
        Assert.Equal(70m, await s.OnHandAsync());
    }

    [Fact]
    public async Task Same_key_with_a_different_payload_is_400_IDEMPOTENCY_KEY_REUSED_and_writes_nothing()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("100");
        await s.PostExpectingAsync(body, 201);

        var changed = new Dictionary<string, object?>(body) { ["quantity"] = "101" };
        var refused = await s.PostExpectingAsync(changed, 400);

        Assert.Equal("IDEMPOTENCY_KEY_REUSED", refused.GetProperty("error").GetProperty("code").GetString());
        Assert.Single(s.Persisted("inventory_transactions"));
        Assert.Single(s.Persisted("inventory_outbox"));
        Assert.Equal(100m, await s.OnHandAsync());
    }

    [Fact]
    public async Task Equivalent_spellings_of_the_same_movement_are_a_replay_not_a_reuse()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("100");
        await s.PostExpectingAsync(body, 201);

        // "100" vs "100.000" and an explicit default status are the same movement.
        var same = new Dictionary<string, object?>(body) { ["quantity"] = "100.000", ["toStockStatus"] = "AVAILABLE" };
        var replay = await s.PostExpectingAsync(same, 200);

        Assert.True(replay.GetProperty("idempotentReplay").GetBoolean());
        Assert.Single(s.Persisted("inventory_transactions"));
    }

    [Fact]
    public async Task Concurrent_duplicates_of_one_key_commit_exactly_once()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("100");

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => s.PostAsync(body)));
        var statuses = responses.Select(r => (int)r.StatusCode).OrderBy(x => x).ToList();

        Assert.Equal(1, statuses.Count(x => x == 201));
        Assert.Equal(5, statuses.Count(x => x == 200));
        Assert.Single(s.Persisted("inventory_transactions"));
        Assert.Single(s.Persisted("inventory_outbox"));
        Assert.Equal(100m, await s.OnHandAsync());
    }

    [Fact]
    public async Task The_same_key_in_another_tenant_is_an_independent_movement()
    {
        var a = new InventoryScenario(_factory);
        var b = new InventoryScenario(_factory);
        var bodyA = a.Receipt("100");
        var bodyB = b.Receipt("100");
        bodyB["idempotencyKey"] = bodyA["idempotencyKey"];

        await a.PostExpectingAsync(bodyA, 201);
        var inB = await b.PostExpectingAsync(bodyB, 201);

        Assert.False(inB.GetProperty("idempotentReplay").GetBoolean());
    }

    [Theory]
    [InlineData(null, "Idempotency-Key header missing")]
    [InlineData("other-key", "header differs from body")]
    public async Task Header_and_body_keys_must_both_be_present_and_equal(string? header, string because)
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("100");

        using var response = await s.PostAsync(body, idempotencyKeyHeader: header ?? string.Empty);
        var json = await s.ReadAsync(response);

        Assert.True(400 == (int)response.StatusCode, because);
        Assert.Equal("INVALID_REQUEST", json.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    [Fact]
    public async Task Key_longer_than_128_characters_is_refused()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("100");
        body["idempotencyKey"] = new string('k', 129);

        var refused = await s.PostExpectingAsync(body, 400);

        Assert.Equal("INVALID_REQUEST", refused.GetProperty("error").GetProperty("code").GetString());
    }
}
