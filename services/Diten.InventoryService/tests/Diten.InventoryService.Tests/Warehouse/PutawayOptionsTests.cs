using Xunit;
using static Diten.InventoryService.Tests.Warehouse.PutawayScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-2 — the <c>Warehouse:Putaway</c> options are validated when the host starts (pack §8.3; AC-46 opening, AC-67 (2)):
/// <c>InFlightLeaseSeconds ≥ PostingTimeoutSeconds + 30</c>, <c>LongPendingMinutes &gt; 0</c> and <c>LongPendingMinutes × 60 &gt; InFlightLeaseSeconds</c>
/// — a wrong setting stops the host instead of running with it.
/// </summary>
public sealed class PutawayOptionsTests
{
    private static Exception? Start(params (string Key, string Value)[] settings)
    {
        using var factory = new ConfiguredWarehouseApiFactory(settings.ToDictionary(s => "Warehouse:Putaway:" + s.Key, s => s.Value));
        return Record.Exception(() => factory.CreateClient().Dispose());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    public void AC46_long_pending_zero_or_not_beyond_the_lease_stops_the_host(string minutes)
    {
        var error = Start(("LongPendingMinutes", minutes));
        Assert.NotNull(error);
        Assert.Contains("LongPendingMinutes", error.ToString());
    }

    [Fact]
    public void AC46_long_pending_three_minutes_starts()
        => Assert.Null(Start(("LongPendingMinutes", "3")));

    [Fact]
    public void AC67_2_a_lease_shorter_than_the_posting_limit_plus_30_stops_the_host()
    {
        var error = Start(("PostingTimeoutSeconds", "60"), ("InFlightLeaseSeconds", "89"));
        Assert.NotNull(error);
        Assert.Contains("InFlightLeaseSeconds", error.ToString());
        Assert.Null(Start(("PostingTimeoutSeconds", "60"), ("InFlightLeaseSeconds", "90")));
    }
}

/// <summary>
/// AC-67 (1) — a ledger slower than <c>PostingTimeoutSeconds</c> (1 s here): the confirmation stays Pending, the answer is 503
/// (<c>PostingTimeout</c>) and the flight lock holds until <c>InFlightUntilUtc</c> — a retry in that time is 409 IN_FLIGHT.
/// </summary>
public sealed class PutawayPostingTimeoutTests : IClassFixture<ShortPostingTimeoutApiFactory>
{
    private readonly ShortPostingTimeoutApiFactory _factory;

    public PutawayPostingTimeoutTests(ShortPostingTimeoutApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AC67_1_the_posting_time_limit_leaves_the_confirmation_pending_and_locked()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(20);
        var task = await s.TaskAsync(20);
        _factory.PostingDelay[s.Tenant] = TimeSpan.FromSeconds(10);
        try
        {
            var answer = await Expect(s.ConfirmAsync(task, 20, s.A01), 503);
            Assert.Equal("PostingTimeout", answer.GetProperty("error").GetProperty("details").GetProperty("reason").GetString());
            var pending = DetailsTask(answer);
            Assert.Equal("Pending", ConfirmationStatus(pending, 1));
            Assert.False(string.IsNullOrEmpty(Confirmation(pending, 1).GetProperty("inFlightUntilUtc").GetString()));
            Assert.Equal("WMS_CONFIRMATION_IN_FLIGHT", await ErrorCode(s.RetryAsync(TaskId(task), 1), 409));
            Assert.Empty(s.TaskMovements(TaskId(task)));
        }
        finally
        {
            _factory.PostingDelay.TryRemove(s.Tenant, out _);
        }

        _factory.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal("Posted", ConfirmationStatus(await Expect(s.RetryAsync(TaskId(task), 1), 200), 1));
    }
}
