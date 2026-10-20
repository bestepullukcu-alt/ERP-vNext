using Diten.InventoryService.Infrastructure.Middleware;
using Diten.InventoryService.Tests.Recall;
using MongoDB.Bson;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// FU-INT1-02 (owner decision G1-D3 = A, 2026-10-20 — WP-MVP1-FIX-LIVE3) — the production <see cref="CorrelationIdMiddleware"/> on the real
/// host: a value that is not one UUID is never refused; a fresh UUID carries the request (2xx, response header); the supplied text appears
/// only in ONE warning log line, cut to <see cref="CorrelationIdMiddleware.LoggedLength"/> characters and escaped; a valid UUID passes as is
/// and logs nothing. (The host's own log sink: <c>RecallApiFactory.Logs</c>.)
/// </summary>
public sealed class CorrelationReplacementTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public CorrelationReplacementTests(RecallApiFactory factory) => _factory = factory;

    private List<string> Replaced(string id) => _factory.Logs.Lines.Where(l => l.Contains("inventory.correlation.replaced", StringComparison.Ordinal) && l.Contains(id, StringComparison.Ordinal)).ToList();

    [Fact]
    public async Task A_non_uuid_value_goes_on_with_a_fresh_uuid_and_is_only_logged()
    {
        var s = new RecallScenario(_factory).Ledger;
        using var response = await s.PostAsync(s.Receipt("1"), configure: r => r.Headers.TryAddWithoutValidation("X-Correlation-Id", "req-1"));

        Assert.Equal(201, (int)response.StatusCode);
        var minted = response.Headers.GetValues("X-Correlation-Id").Single();
        Assert.True(Guid.TryParse(minted, out var id) && id != Guid.Empty);
        var line = Assert.Single(Replaced(minted));
        Assert.StartsWith("Warning ", line, StringComparison.Ordinal);
        Assert.Contains("\"req-1\"", line, StringComparison.Ordinal);
        Assert.Equal(minted, Assert.Single(s.Persisted("inventory_transactions"))["CorrelationId"].AsString);
    }

    [Fact]
    public async Task A_valid_uuid_passes_unchanged_and_logs_nothing()
    {
        var s = new RecallScenario(_factory).Ledger;
        var mine = Guid.NewGuid().ToString();
        using var response = await s.PostAsync(s.Receipt("1"), configure: r => r.Headers.TryAddWithoutValidation("X-Correlation-Id", mine));

        Assert.Equal(201, (int)response.StatusCode);
        Assert.Equal(mine, response.Headers.GetValues("X-Correlation-Id").Single());
        Assert.Empty(Replaced(mine));
    }

    /// <summary>
    /// The EMPTY UUID is not an id (CT hold, 2026-10-20): accepted, every caller sending it would share one correlation across their movements,
    /// audit rows and outbox events — the NAMES1 name match (correlation + person) and the audit trail would mix. It is replaced like any other
    /// invalid value.
    /// </summary>
    [Fact]
    public async Task The_empty_uuid_is_replaced_by_a_fresh_uuid_in_every_record_and_logged_once()
    {
        const string zero = "00000000-0000-0000-0000-000000000000";
        var s = new RecallScenario(_factory).Ledger;
        using var response = await s.PostAsync(s.Receipt("1"), configure: r => r.Headers.TryAddWithoutValidation("X-Correlation-Id", zero));

        Assert.Equal(201, (int)response.StatusCode);
        var minted = response.Headers.GetValues("X-Correlation-Id").Single();
        Assert.True(Guid.TryParse(minted, out var id) && id != Guid.Empty, minted);
        foreach (var collection in new[] { "inventory_transactions", "inventory_audit", "inventory_outbox" })
        {
            var row = Assert.Single(s.Persisted(collection));
            Assert.Equal(minted, row["CorrelationId"].AsString);
            Assert.DoesNotContain(zero, row.ToJson(), StringComparison.Ordinal);
        }

        var line = Assert.Single(Replaced(minted));
        Assert.Contains("\"" + zero + "\"", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// Several values of the header (even two valid UUIDs) name no single id: a fresh UUID carries the request; the values are logged joined
    /// by a comma (73 characters here → cut to the first <see cref="CorrelationIdMiddleware.LoggedLength"/>).
    /// </summary>
    [Fact]
    public async Task Several_values_are_replaced_by_a_fresh_uuid_and_logged_once()
    {
        var first = Guid.NewGuid().ToString();
        var second = Guid.NewGuid().ToString();
        var s = new RecallScenario(_factory).Ledger;
        using var response = await s.PostAsync(s.Receipt("1"), configure: r => r.Headers.TryAddWithoutValidation("X-Correlation-Id", new[] { first, second }));

        Assert.Equal(201, (int)response.StatusCode);
        var minted = response.Headers.GetValues("X-Correlation-Id").Single();
        Assert.True(Guid.TryParse(minted, out var id) && id != Guid.Empty, minted);
        Assert.NotEqual(first, minted);
        Assert.NotEqual(second, minted);
        Assert.Equal(minted, Assert.Single(s.Persisted("inventory_transactions"))["CorrelationId"].AsString);
        Assert.Equal(minted, Assert.Single(s.Persisted("inventory_audit"))["CorrelationId"].AsString);
        var line = Assert.Single(Replaced(minted));
        var joined = first + "," + second;
        Assert.Contains("\"" + joined[..CorrelationIdMiddleware.LoggedLength] + "\"...(+" + (joined.Length - CorrelationIdMiddleware.LoggedLength) + ")", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_overlong_value_is_cut_and_escaped_in_the_log()
    {
        var s = new RecallScenario(_factory).Ledger;
        var supplied = "x\u0007y" + new string('z', 300);
        using var response = await s.PostAsync(s.Receipt("1"), configure: r => r.Headers.TryAddWithoutValidation("X-Correlation-Id", supplied));

        Assert.Equal(201, (int)response.StatusCode);
        var minted = response.Headers.GetValues("X-Correlation-Id").Single();
        var line = Assert.Single(Replaced(minted));
        Assert.Contains("\"x\\u0007y" + new string('z', CorrelationIdMiddleware.LoggedLength - 3) + "\"...(+" + (supplied.Length - CorrelationIdMiddleware.LoggedLength) + ")", line, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('z', CorrelationIdMiddleware.LoggedLength), line, StringComparison.Ordinal);
        Assert.DoesNotContain("\u0007", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("")]
    public void The_empty_uuid_and_blank_values_are_not_ids(string value)
        => Assert.StartsWith("\"", CorrelationIdMiddleware.ForLog(value), StringComparison.Ordinal);
}
