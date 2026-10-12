using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>AC2 opening golden flow (incl. host restart) · AC3 issue flow with the first transaction byte-identical.</summary>
public sealed class OpeningAndIssueFlowTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public OpeningAndIssueFlowTests(InventoryApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Opening_balance_100_posts_one_transaction_and_a_100_projection_row_that_survives_a_host_restart()
    {
        var s = new InventoryScenario(_factory);

        var posted = await s.PostExpectingAsync(s.Receipt("100"), 201);

        Assert.False(posted.GetProperty("idempotentReplay").GetBoolean());
        Assert.Matches(@"^INV-2026-\d{7}$", posted.GetProperty("transactionNumber").GetString());
        var snapshot = posted.GetProperty("balanceSnapshot");
        Assert.Equal("100.000", snapshot.GetProperty("onHand").GetString());
        Assert.Equal("100.000", snapshot.GetProperty("available").GetString());
        Assert.Equal(s.LegalEntity.ToString(), snapshot.GetProperty("legalEntityId").GetString());

        Assert.Single(s.Persisted("inventory_transactions"));
        var row = Assert.Single(s.Persisted("inventory_balances"));
        Assert.Equal(100m, row["OnHand"].AsDecimal);
        Assert.Equal(100m, row["Available"].AsDecimal);

        // Restart: a brand-new host on the same database must read the same truth.
        using var restarted = new InventoryApiFactory();
        var afterRestart = new ScenarioView(restarted, s);
        Assert.Equal("100.000", (await afterRestart.BalanceRowAsync()).GetProperty("onHand").GetString());
        var availability = await afterRestart.AvailabilityAsync();
        Assert.Equal("100.000", availability.GetProperty("onHand").GetString());
        Assert.Equal("100.000", availability.GetProperty("available").GetString());
    }

    [Fact]
    public async Task Issue_20_appends_a_second_transaction_leaves_80_and_never_touches_the_first_document()
    {
        var s = new InventoryScenario(_factory);
        var first = await s.PostExpectingAsync(s.Receipt("100"), 201);
        var firstId = first.GetProperty("transactionId").GetString()!;

        var before = RawTransaction(s, firstId);

        var issued = await s.PostExpectingAsync(s.Issue("20"), 201);
        Assert.Equal("80.000", issued.GetProperty("balanceSnapshot").GetProperty("onHand").GetString());

        var after = RawTransaction(s, firstId);
        Assert.Equal(before.ToBson(), after.ToBson());

        Assert.Equal(2, s.Persisted("inventory_transactions").Count);
        Assert.Equal(80m, await s.OnHandAsync());
    }

    private static BsonDocument RawTransaction(InventoryScenario s, string id)
        => s.Factory.Collection("inventory_transactions").Find(new BsonDocument("_id", id)).Single();

    /// <summary>Reads a scenario's data through another host (same token, same tenant).</summary>
    private sealed class ScenarioView
    {
        private readonly HttpClient _client;
        private readonly InventoryScenario _scenario;

        public ScenarioView(InventoryApiFactory factory, InventoryScenario scenario)
        {
            _client = factory.CreateContractClient();
            _scenario = scenario;
        }

        public async Task<System.Text.Json.JsonElement> BalanceRowAsync()
        {
            var json = await GetAsync($"/api/inventory/balance?itemId={_scenario.Item}");
            return Assert.Single(json.GetProperty("rows").EnumerateArray().ToList());
        }

        public Task<System.Text.Json.JsonElement> AvailabilityAsync() => GetAsync($"/api/inventory/availability?itemId={_scenario.Item}");

        private async Task<System.Text.Json.JsonElement> GetAsync(string url)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _scenario.Token);
            using var response = await _client.SendAsync(request);
            Assert.Equal(200, (int)response.StatusCode);
            return System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
        }
    }
}
