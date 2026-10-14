using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>AC12 audit + correlation · AC13 DEC-INV-19 hook.</summary>
public sealed class AuditCorrelationTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public AuditCorrelationTests(InventoryApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Every_posting_has_an_audit_record_and_a_Pending_outbox_event_with_the_response_correlation_id()
    {
        var s = new InventoryScenario(_factory);

        using var response = await s.PostAsync(s.Receipt("100"));
        var body = await s.ReadAsync(response);
        var correlation = response.Headers.GetValues("X-Correlation-Id").Single();
        Assert.True(Guid.TryParse(correlation, out _));

        var transactionId = body.GetProperty("transactionId").GetString();
        var transaction = Assert.Single(s.Persisted("inventory_transactions"));
        var audit = Assert.Single(s.Persisted("inventory_audit"));
        var outbox = Assert.Single(s.Persisted("inventory_outbox"));

        Assert.Equal(correlation, transaction["CorrelationId"].AsString);
        Assert.Equal(correlation, audit["CorrelationId"].AsString);
        Assert.Equal(correlation, outbox["CorrelationId"].AsString);

        Assert.Equal("InventoryMovementPosted", audit["Action"].AsString);
        Assert.Equal(transactionId, audit["TransactionId"].AsString);
        Assert.Equal(s.User.ToString(), audit["ActorUserId"].AsString);
        Assert.Equal(body.GetProperty("transactionNumber").GetString(), audit["TransactionNumber"].AsString);

        Assert.Equal("InventoryMovementPosted", outbox["EventName"].AsString);
        Assert.Equal("Pending", outbox["Status"].AsString);
        Assert.Equal(transactionId, outbox["AggregateId"].AsString);
        Assert.Equal(body.GetProperty("transactionNumber").GetString(), outbox["Payload"]["TransactionNumber"].AsString);
    }

    [Fact]
    public async Task A_caller_supplied_correlation_uuid_is_echoed_and_persisted()
    {
        var s = new InventoryScenario(_factory);
        var mine = Guid.NewGuid().ToString();

        using var response = await s.PostAsync(s.Receipt("1"), configure: r => r.Headers.Add("X-Correlation-Id", mine));

        Assert.Equal(mine, response.Headers.GetValues("X-Correlation-Id").Single());
        Assert.Equal(mine, Assert.Single(s.Persisted("inventory_audit"))["CorrelationId"].AsString);
        Assert.Equal(mine, Assert.Single(s.Persisted("inventory_outbox"))["CorrelationId"].AsString);
    }

    [Fact]
    public async Task A_non_uuid_correlation_id_is_400_and_the_error_carries_the_echoed_id()
    {
        var s = new InventoryScenario(_factory);

        using var response = await s.PostAsync(s.Receipt("1"), configure: r => r.Headers.Add("X-Correlation-Id", "req-1"));
        var json = await s.ReadAsync(response);

        Assert.Equal(400, (int)response.StatusCode);
        Assert.Equal(response.Headers.GetValues("X-Correlation-Id").Single(), json.GetProperty("error").GetProperty("correlationId").GetString());
        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    [Fact]
    public async Task Errors_carry_the_same_correlation_id_as_the_header()
    {
        var s = new InventoryScenario(_factory);

        using var response = await s.PostAsync(s.Issue("1"));
        var json = await s.ReadAsync(response);

        Assert.Equal(422, (int)response.StatusCode);
        Assert.Equal(response.Headers.GetValues("X-Correlation-Id").Single(), json.GetProperty("error").GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task A_replay_writes_no_second_audit_or_outbox_row()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("1");
        await s.PostExpectingAsync(body, 201);
        await s.PostExpectingAsync(body, 200);

        Assert.Single(s.Persisted("inventory_audit"));
        Assert.Single(s.Persisted("inventory_outbox"));
    }

    [Fact]
    public async Task Reversal_is_audited_as_a_reversal()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("1");
        var id = (await s.PostExpectingAsync(body, 201)).GetProperty("transactionId").GetString()!;
        await s.PostExpectingAsync(s.Reversal(body, id), 201);

        Assert.Contains(s.Persisted("inventory_audit"), a => a["Action"].AsString == "InventoryMovementReversed");
    }

    [Fact]
    public async Task SourceSystem_and_ExternalRef_are_persisted_DEC_INV_19()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("1");
        body["sourceSystem"] = "SAP-ECC-TR";
        body["sourceDocumentId"] = "GRN-42";
        body["sourceLineId"] = "7";

        await s.PostExpectingAsync(body, 201);

        var stored = Assert.Single(s.Persisted("inventory_transactions"));
        Assert.Equal("SAP-ECC-TR", stored["SourceSystem"].AsString);
        Assert.True(stored.Contains("ExternalRef"), "ExternalRef must exist on the document (nullable, DEC-INV-19).");
        Assert.Equal("SAP-ECC-TR", Assert.Single(s.Persisted("inventory_audit"))["SourceSystem"].AsString);

        // The frozen TransactionItem exposes the source reference but not sourceSystem — returned where exposed.
        var history = await s.ReadAsync(await s.GetAsync($"/api/inventory/transactions?itemId={s.Item}"));
        var item = Assert.Single(history.GetProperty("items").EnumerateArray().ToList());
        Assert.Equal("GRN-42", item.GetProperty("sourceDocumentId").GetString());
        Assert.Equal("7", item.GetProperty("sourceLineId").GetString());
        Assert.False(item.TryGetProperty("sourceSystem", out _));
    }
}
