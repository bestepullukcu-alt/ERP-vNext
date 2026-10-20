using Diten.InventoryService.Infrastructure.Middleware;
using Diten.InventoryService.Tests.Recall;
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
