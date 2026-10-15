using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R6 — what the İzleme screen draws for a lot with on-hand stock in TWO companies of the tenant (CT-R8): the caller's company shows
/// its own Stock line; the other company's balance is never a node (no warehouse, no quantity), only the one OTHER_COMPANY_ACTIVITY count.
/// R6 sabotage (d) — the balance read without its company filter — turns this red. (R2's CT-R8 test covers movements; this one the stock.)
/// </summary>
public sealed class RecallTraceCompanyStockTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallTraceCompanyStockTests(RecallApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Another_companys_stock_of_the_lot_is_not_a_trace_row_only_its_activity_is_a_count()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var otherCompany = Guid.NewGuid();
        var balances = w.S.Factory.Database.GetCollection<BsonDocument>("inventory_balances");
        var own = balances.Find(Builders<BsonDocument>.Filter.Eq("LotId", w.Fg)).ToList();
        Assert.NotEmpty(own); // the finished good has stock in this company
        foreach (var row in own)
        {
            var clone = row.DeepClone().AsBsonDocument;
            clone["_id"] = row["_id"].IsString ? Guid.NewGuid().ToString("D") : new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
            clone["LegalEntityId"] = RecallTraceWorld.SameShape(row["LegalEntityId"], otherCompany);
            clone["OnHand"] = new Decimal128(4242.5m);
            if (clone.Contains("Available")) clone["Available"] = new Decimal128(4242.5m);
            balances.InsertOne(clone);
        }

        w.CloneTransaction(w.Shipment, d => d["LegalEntityId"] = RecallTraceWorld.SameShape(d["LegalEntityId"], otherCompany)); // its activity there

        var (status, _, raw) = await w.S.SendAsync(HttpMethod.Get, $"/api/recall/trace?rootLotId={w.Fg}&direction=Both", w.Token);
        Assert.Equal(200, status);
        var text = await raw.Content.ReadAsStringAsync();
        var trace = JsonDocument.Parse(text).RootElement;
        var stock = RecallTraceWorld.Nodes(trace).Where(n => n.GetProperty("kind").GetString() == "Stock").ToList();
        Assert.Equal(own.Count, stock.Count);                                  // only this company's lines
        Assert.DoesNotContain("4242.5", text, StringComparison.Ordinal);       // the other company's quantity never travels
        Assert.DoesNotContain(otherCompany.ToString("D"), text, StringComparison.OrdinalIgnoreCase);
        Assert.True(Assert.Single(RecallTraceWorld.Gaps(trace, "OTHER_COMPANY_ACTIVITY")).GetProperty("count").GetInt64() >= 1);
    }
}
