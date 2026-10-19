using System.Text.RegularExpressions;
using Diten.SupplyChainService.Domain.Features.Claims;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
namespace Diten.SupplyChainService.Tests.Claims;

/// <summary>
/// MOD-0187 §16 (evidence audit 2026-10-19), two criteria that had no test:
/// "Settled creates no payment / AP / AR record" — measured on the real repository: after a claim is approved, settled and
/// closed, its own database holds only the module's claims_* collections and nothing that looks like a finance record.
/// "Correlation unchanged through all claim events" — only 2 of 7 event types were asserted; here every arrow of the frozen
/// lifecycle runs and every outbox envelope carries the shipment root.
/// </summary>
public sealed class ClaimSettlementBoundaryTests
{
    private static readonly Regex Finance = new("payment|payable|receivable|invoice|ledger|journal|settlement|(^|_)(ap|ar)(_|$)", RegexOptions.IgnoreCase);

    [Fact]
    public async Task Settled_and_closed_write_only_claim_collections_and_no_finance_record()
    {
        var f = await ClaimDbFixture.Create(databaseName: "diten_test_claims_settlement_boundary");
        var c = await f.CreateClaim("create"); Assert.Equal(201, c.StatusCode);
        Assert.Equal(200, (await f.Transition(c.ClaimId, ClaimStatus.Investigating, "investigate")).StatusCode);
        Assert.Equal(200, (await f.Transition(c.ClaimId, ClaimStatus.Approved, "approve", "250")).StatusCode);
        Assert.Equal(200, (await f.Transition(c.ClaimId, ClaimStatus.Settled, "settle")).StatusCode);
        Assert.Equal(200, (await f.Transition(c.ClaimId, ClaimStatus.Closed, "close")).StatusCode);

        var names = await (await f.Db.ListCollectionNamesAsync()).ToListAsync();
        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.StartsWith("claims", name, StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => Finance.IsMatch(name));
        // And inside the module's own collections, nothing but the claim, its receipts, audit and events for this scope.
        Assert.Equal(new long[] { 1, 5, 5, 5 }, await f.Counts());
    }

    [Fact]
    public async Task Every_claim_event_type_carries_the_shipment_root()
    {
        var paths = new[]
        {
            new[] { ClaimStatus.Investigating, ClaimStatus.Approved, ClaimStatus.Settled, ClaimStatus.Closed },
            new[] { ClaimStatus.Investigating, ClaimStatus.Rejected, ClaimStatus.Closed },
            new[] { ClaimStatus.Withdrawn }
        };
        var types = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var f = await ClaimDbFixture.Create();
            var c = await f.CreateClaim("create"); Assert.Equal(201, c.StatusCode);
            foreach (var target in path)
                Assert.Equal(200, (await f.Transition(c.ClaimId, target, target.ToString(), target == ClaimStatus.Approved ? "250" : null)).StatusCode);
            var docs = await f.Db.GetCollection<BsonDocument>("claims_outbox").Find(f.Filter()).ToListAsync();
            Assert.Equal(path.Length + 1, docs.Count);
            foreach (var d in docs)
            {
                var envelope = d["Envelope"].AsBsonDocument;
                Assert.Equal(f.Root.ToString(), envelope["correlationId"].AsString);
                types.Add(envelope["eventType"].AsString);
            }
        }
        Assert.Equal(new[] { "ClaimApproved", "ClaimClosed", "ClaimInvestigating", "ClaimOpened", "ClaimRejected", "ClaimSettled", "ClaimWithdrawn" },
            types.Order(StringComparer.Ordinal).ToArray());
    }
}
