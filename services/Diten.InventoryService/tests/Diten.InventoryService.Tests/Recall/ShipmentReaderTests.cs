using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Recall.Trace;
using Diten.InventoryService.Infrastructure.Recall;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>The MVP-6 answers the double can give for one shipment (SHIPMENT-BUNDLE getShipment through the Gateway).</summary>
public enum ShipmentDoubleBehaviour
{
    Ok,
    Unauthorized,
    Forbidden,
    NotFound,
    Timeout,
    ServerError
}

/// <summary>
/// The MVP-6 test double (WP-0177-R2): answers <c>GET api/shipment-bundle/shipments/{id}</c> per shipment id (default 200 with a
/// <c>shipToReference</c> derived from the id), counts every request (the no-cache proof), records the company header and the
/// highest number of reads in flight (the 8-parallel bound). A timeout waits longer than the reader's 2 s.
/// </summary>
public sealed class ShipmentBundleDouble : HttpMessageHandler
{
    private int _inFlight;
    private int _maxInFlight;
    private int _requests;

    public ConcurrentDictionary<string, ShipmentDoubleBehaviour> Behaviour { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ShipmentDoubleBehaviour Default { get; set; } = ShipmentDoubleBehaviour.Ok;

    public TimeSpan Latency { get; set; } = TimeSpan.Zero;

    public ConcurrentQueue<(string Path, string? LegalEntity, string? Authorization)> Seen { get; } = new();

    public int Requests => Volatile.Read(ref _requests);

    public int MaxInFlight => Volatile.Read(ref _maxInFlight);

    public static string ShipTo(string id) => "CUST-" + id[..8].ToUpperInvariant() + "/ADDR-1";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requests);
        var now = Interlocked.Increment(ref _inFlight);
        int seen;
        while ((seen = Volatile.Read(ref _maxInFlight)) < now && Interlocked.CompareExchange(ref _maxInFlight, now, seen) != seen)
        {
        }

        try
        {
            var path = request.RequestUri!.AbsolutePath;
            Seen.Enqueue((path, request.Headers.TryGetValues("X-Legal-Entity-Id", out var le) ? le.Single() : null,
                request.Headers.Authorization?.ToString()));
            var id = path[(path.LastIndexOf('/') + 1)..];
            if (Latency > TimeSpan.Zero) await Task.Delay(Latency, cancellationToken);
            var behaviour = Behaviour.TryGetValue(id, out var b) ? b : Default;
            switch (behaviour)
            {
                case ShipmentDoubleBehaviour.Timeout:
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                case ShipmentDoubleBehaviour.Unauthorized:
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                case ShipmentDoubleBehaviour.Forbidden:
                    return new HttpResponseMessage(HttpStatusCode.Forbidden);
                case ShipmentDoubleBehaviour.NotFound:
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                case ShipmentDoubleBehaviour.ServerError:
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                default:
                    // the SHIPMENT-BUNDLE body: lines carry quantities the recall must never use (pack §8.6)
                    var body = $$"""
                        {"shipmentId":"{{id}}","status":"Delivered","actualDeliverAt":"2026-09-20T10:00:00Z","shipToReference":"{{ShipTo(id)}}",
                         "lines":[{"lineNo":1,"quantity":999999,"uomId":"EA"}]}
                        """;
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            }
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }
}

/// <summary>WP-0177-R2 item 3 — the MVP-6 shipment reader (pack §8.6): the degrade table, no cache, 2 s per read, at most 8 in flight, the company header.</summary>
public sealed class ShipmentReaderTests
{
    private static readonly Guid Company = Guid.Parse("1e000000-0000-0000-0000-0000000000c1");

    private static (ShipmentBundleHttpReader Reader, ShipmentBundleDouble Double) Reader()
    {
        var handler = new ShipmentBundleDouble();
        var tenant = new TenantContext();
        tenant.SetScope(Guid.NewGuid(), Company, Guid.NewGuid());
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://gateway.test/"), Timeout = TimeSpan.FromSeconds(60) }; // the client bound is far away: only the reader's own 2 s can end a read early
        return (new ShipmentBundleHttpReader(http, tenant, NullLogger<ShipmentBundleHttpReader>.Instance), handler);
    }

    private static string NewId() => Guid.NewGuid().ToString("D");

    [Theory]
    [InlineData(ShipmentDoubleBehaviour.Ok, ShipmentReadOutcome.Ok)]
    [InlineData(ShipmentDoubleBehaviour.Unauthorized, ShipmentReadOutcome.Forbidden)]
    [InlineData(ShipmentDoubleBehaviour.Forbidden, ShipmentReadOutcome.Forbidden)]
    [InlineData(ShipmentDoubleBehaviour.NotFound, ShipmentReadOutcome.NotFound)]
    [InlineData(ShipmentDoubleBehaviour.ServerError, ShipmentReadOutcome.Unavailable)]
    [InlineData(ShipmentDoubleBehaviour.Timeout, ShipmentReadOutcome.Unavailable)]
    public async Task Every_answer_maps_to_a_read_outcome_never_an_exception(ShipmentDoubleBehaviour behaviour, ShipmentReadOutcome expected)
    {
        var (reader, handler) = Reader();
        handler.Default = behaviour;
        var id = NewId();
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var read = (await reader.ReadAsync([id]))[id];

        Assert.Equal(expected, read.Read);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"a read took {clock.Elapsed} — the 2 s bound did not hold");
        if (expected == ShipmentReadOutcome.Ok)
        {
            Assert.Equal(("Delivered", ShipmentBundleDouble.ShipTo(id)), (read.Status, read.ShipToReference));
            Assert.Equal(new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero), read.ActualDeliverAt);
        }
        else
        {
            Assert.Null(read.ShipToReference);
        }
    }

    [Fact]
    public async Task Reads_go_through_the_gateway_path_with_the_callers_company()
    {
        var (reader, handler) = Reader();
        var id = NewId();

        await reader.ReadAsync([id]);

        var seen = Assert.Single(handler.Seen);
        Assert.Equal($"/api/shipment-bundle/shipments/{id}", seen.Path);
        Assert.Equal(Company.ToString("D"), seen.LegalEntity);
    }

    [Fact]
    public async Task No_cache_every_trace_reads_again()
    {
        var (reader, handler) = Reader();
        var id = NewId();

        await reader.ReadAsync([id]);
        await reader.ReadAsync([id]);
        await reader.ReadAsync([id, id]);

        Assert.Equal(3, handler.Requests);
    }

    [Fact]
    public async Task At_most_eight_reads_are_in_flight()
    {
        var (reader, handler) = Reader();
        handler.Latency = TimeSpan.FromMilliseconds(150);
        var ids = Enumerable.Range(0, 30).Select(_ => NewId()).ToList();

        var reads = await reader.ReadAsync(ids);

        Assert.Equal(30, reads.Count);
        Assert.All(reads.Values, r => Assert.Equal(ShipmentReadOutcome.Ok, r.Read));
        Assert.Equal(ShipmentBundleHttpReader.MaxParallel, handler.MaxInFlight);
    }

    [Fact]
    public async Task One_slow_shipment_does_not_fail_the_others()
    {
        var (reader, handler) = Reader();
        var slow = NewId();
        var fine = NewId();
        handler.Behaviour[slow] = ShipmentDoubleBehaviour.Timeout;

        var reads = await reader.ReadAsync([slow, fine]);

        Assert.Equal(ShipmentReadOutcome.Unavailable, reads[slow].Read);
        Assert.Equal(ShipmentReadOutcome.Ok, reads[fine].Read);
    }

    [Fact]
    public async Task A_document_id_that_is_not_a_shipment_uuid_is_NotFound_without_a_call()
    {
        var (reader, handler) = Reader();

        var reads = await reader.ReadAsync(["SO-LEGACY-1"]);

        Assert.Equal(ShipmentReadOutcome.NotFound, reads["SO-LEGACY-1"].Read);
        Assert.Equal(0, handler.Requests);
    }
}
