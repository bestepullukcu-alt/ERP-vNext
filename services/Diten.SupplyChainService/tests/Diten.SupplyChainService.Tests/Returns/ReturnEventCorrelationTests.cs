using System.Text.RegularExpressions;
using Diten.SupplyChainService.Domain.Features.Returns;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
namespace Diten.SupplyChainService.Tests.Returns;

/// <summary>
/// MOD-0186 §16 (evidence audit 2026-10-19). "Correlation unchanged through reverse lifecycle events": no test read the
/// outbox envelope — here every arrow of the frozen lifecycle runs and every envelope carries the shipment root, across
/// all eight event types. "No stock collection / projection / movement SoR": the module's own database holds only its
/// return_* collections — no balance, movement or stock record is created by a return that is received and dispositioned.
/// </summary>
public sealed class ReturnEventCorrelationTests
{
    private static readonly Regex Stock = new("stock|balance|movement|on.?hand|inventory_(item|level|ledger)|ledger", RegexOptions.IgnoreCase);

    [Fact]
    public async Task Every_return_event_type_carries_the_shipment_root()
    {
        var paths = new[]
        {
            new[] { ReturnStatus.Authorized, ReturnStatus.InTransit, ReturnStatus.Received, ReturnStatus.Dispositioned, ReturnStatus.Closed },
            new[] { ReturnStatus.Rejected },
            new[] { ReturnStatus.Authorized, ReturnStatus.Cancelled }
        };
        var types = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var f = await ReturnTestFixture.New();
            var c = await f.Create("1"); Assert.Equal(201, c.StatusCode);
            foreach (var target in path)
                Assert.Equal(200, (await f.Transition(c.ReturnId, target, target.ToString(),
                    inventory: target == ReturnStatus.Received ? "opaque-no-lookup" : null,
                    disposition: target == ReturnStatus.Dispositioned ? " " : null)).StatusCode);
            var docs = await f.Db.GetCollection<BsonDocument>("returns_outbox").Find(f.Filter).ToListAsync();
            Assert.Equal(path.Length + 1, docs.Count);
            foreach (var d in docs)
            {
                Assert.Equal(f.Root.ToString(), d["Envelope"]["correlationId"].AsString);
                types.Add(d["Envelope"]["eventType"].AsString);
            }
        }
        Assert.Equal(new[] { "ReturnAuthorized", "ReturnCancelled", "ReturnClosed", "ReturnDispositioned", "ReturnInTransit",
            "ReturnReceived", "ReturnRejected", "ReturnRequested" }, types.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task A_received_and_dispositioned_return_creates_no_stock_record()
    {
        var f = await ReturnTestFixture.New("diten_returns_stock_boundary");
        var c = await f.Create("1"); Assert.Equal(201, c.StatusCode);
        foreach (var target in new[] { ReturnStatus.Authorized, ReturnStatus.InTransit, ReturnStatus.Received, ReturnStatus.Dispositioned })
            Assert.Equal(200, (await f.Transition(c.ReturnId, target, target.ToString(),
                inventory: target == ReturnStatus.Received ? "opaque-no-lookup" : null,
                disposition: target == ReturnStatus.Dispositioned ? " " : null)).StatusCode);
        var names = await (await f.Db.ListCollectionNamesAsync()).ToListAsync();
        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.StartsWith("return", name, StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => Stock.IsMatch(name));
    }
}
