using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Diten.SupplyChainService.Tests.Common;

namespace Diten.SupplyChainService.Tests.CapacityPlans;

/// <summary>
/// MOD-0192:199 — "UUID correlation is preserved from HTTP request through response, audit and lifecycle
/// event." Nothing asserted the chain: the four CapacityPlans suites that mention CorrelationId all drive the
/// repository directly and none of them compares a value across the hops. The chain exists in production
/// (CapacityContextMiddleware.cs:34-38 reads and echoes the header, CapacityRepository.cs:40/45/49 writes it
/// to receipt, audit and outbox), so these two cases measure it end to end over the real pipeline.
/// </summary>
[Collection(SandopCapacityHttpCollection.Name)]
public sealed class CapacityCorrelationTests
{
    private const string Plans = "/api/supply-chain/capacity-plans";
    private readonly SandopCapacityHttpHost _host;
    public CapacityCorrelationTests(SandopCapacityHttpHost host) => _host = host;

    private async Task<long> Rows(string collection, Guid correlation) =>
        await _host.Database.GetCollection<BsonDocument>(collection)
            .CountDocumentsAsync(new BsonDocument("CorrelationId", correlation.ToString()));

    [Fact(Skip = "MOD-0192:199/:200 - needs the Program.cs capacity branch, which is hash-pinned as Accepted composition in MOD-0192:273 and MOD-0190:261 and needs integration-owner authorization. Measured green on 2026-10-12 with that branch in place; see MOD-0192:199. Un-skip with the branch, not before.")]
    public async Task Correlation_survives_request_response_audit_and_event()
    {
        var correlation = Guid.NewGuid();
        var reply = await _host.Send("POST", Plans, SandopCapacityHttpHost.CapacityPlan(),
            _host.Token(), "k-" + Guid.NewGuid(), correlation: correlation.ToString());

        Assert.Equal(201, reply.Status);

        // hop 1 -> 2: the request's id comes back on the response, not a fresh one.
        Assert.Equal(correlation.ToString(), reply.Correlation);

        // hop 3: the audit row the write produced carries the same id.
        Assert.Equal(1, await Rows("capacity_audit", correlation));

        // hop 4: so does the lifecycle event, and it is a real typed event rather than an empty row.
        var events = await _host.Database.GetCollection<BsonDocument>("capacity_outbox")
            .Find(new BsonDocument("CorrelationId", correlation.ToString())).ToListAsync();
        Assert.Single(events);
        Assert.False(string.IsNullOrWhiteSpace(events[0]["EventType"].AsString));
        Assert.Equal("Pending", events[0]["Status"].AsString);

        // and the receipt, which is the same id written on a third path.
        Assert.Equal(1, await Rows("capacity_receipts", correlation));
    }

    [Fact(Skip = "MOD-0192:199/:200 - needs the Program.cs capacity branch, which is hash-pinned as Accepted composition in MOD-0192:273 and MOD-0190:261 and needs integration-owner authorization. Measured green on 2026-10-12 with that branch in place; see MOD-0192:199. Un-skip with the branch, not before.")]
    public async Task Malformed_correlation_is_rejected_before_any_write()
    {
        var reply = await _host.Send("POST", Plans, SandopCapacityHttpHost.CapacityPlan(),
            _host.Token(), "k-" + Guid.NewGuid(), correlation: "not-a-uuid");

        Assert.Equal(400, reply.Status);
        Assert.Equal("INVALID_CORRELATION_ID", reply.Code);

        // The rejection happens in the middleware, so the response still carries a correlation id - a minted
        // one - and nothing was written under the string the caller sent.
        Assert.False(string.IsNullOrWhiteSpace(reply.Correlation));
        foreach (var collection in new[] { "capacity_audit", "capacity_outbox", "capacity_receipts" })
            Assert.Equal(0, await _host.Database.GetCollection<BsonDocument>(collection)
                .CountDocumentsAsync(new BsonDocument("CorrelationId", "not-a-uuid")));
    }
}
