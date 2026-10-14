using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// AC-R7 (pack ASSUMPTION-0173-16): an expired reservation no longer counts as reserved — measured separately for the
/// in-transaction path (a decrease / create on the SKU) and for the sweeper. These are the only tests that create
/// short-lived reservations; they live in one class so xUnit runs them sequentially and the sweeper test cannot
/// expire the in-transaction test's reservation first. The hosted sweeper is disabled in tests (TestSettings).
/// </summary>
public sealed class ReservationExpiryTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public ReservationExpiryTests(InventoryApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Reads_stop_counting_a_due_reservation_immediately()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        await s.ReserveExpectingAsync(s.Reservation("30", expiresAt: Soon()), 201);
        Assert.Equal((100m, 30m, 70m), await s.AvailabilityAsync());

        await Task.Delay(TimeSpan.FromSeconds(2.5));

        Assert.Equal((100m, 0m, 100m), await s.AvailabilityAsync());
    }

    [Fact]
    public async Task In_transaction_path_a_decrease_expires_due_reservations_of_the_sku_first()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        var created = await s.ReserveExpectingAsync(s.Reservation("100", expiresAt: Soon()), 201);
        var id = created.GetProperty("reservationId").GetString()!;

        // Before expiry the stock is fully reserved.
        await s.PostExpectingAsync(s.IssueFor("1", "MOD-OTHER", "DO-1", "1"), 422);
        await Task.Delay(TimeSpan.FromSeconds(2.5));

        using var issue = await s.PostAsync(s.IssueFor("40", "MOD-OTHER", "DO-2", "1"));
        Assert.Equal(201, (int)issue.StatusCode);

        var reservation = Reservation(s, id);
        Assert.Equal("Expired", reservation["Status"].AsString);
        var expired = Assert.Single(s.Persisted("inventory_audit"), a => a.Contains("ReservationId") && a["Action"].AsString == "InventoryReservationExpired");
        Assert.Equal(issue.Headers.GetValues("X-Correlation-Id").Single(), expired["CorrelationId"].AsString);
        Assert.Equal(s.User.ToString(), expired["ActorUserId"].AsString);
        Assert.Equal(0m, Assert.Single(s.Persisted("inventory_sku_positions"))["Reserved"].AsDecimal);
        Assert.Equal((60m, 0m, 60m), await s.AvailabilityAsync());
    }

    [Fact]
    public async Task In_transaction_path_a_new_reservation_expires_due_ones_first()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        await s.ReserveExpectingAsync(s.Reservation("100", expiresAt: Soon()), 201);
        await Task.Delay(TimeSpan.FromSeconds(2.5));

        var created = await s.ReserveExpectingAsync(s.Reservation("100"), 201);

        Assert.Equal("0.000", created.GetProperty("availableAfter").GetString());
        Assert.Contains(s.Persisted("inventory_reservations"), r => r["Status"].AsString == "Expired");
    }

    [Fact]
    public async Task Releasing_a_due_reservation_is_409_it_has_expired()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        var created = await s.ReserveExpectingAsync(s.Reservation("30", expiresAt: Soon()), 201);
        await Task.Delay(TimeSpan.FromSeconds(2.5));

        using var release = await s.ReleaseAsync(created.GetProperty("reservationId").GetString()!, created.GetProperty("etag").GetString());

        Assert.Equal(409, (int)release.StatusCode);
        Assert.Equal("Expired", Reservation(s, created.GetProperty("reservationId").GetString()!)["Status"].AsString);
    }

    [Fact]
    public async Task Sweeper_path_expires_due_reservations_nobody_touches_with_the_system_actor()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        var created = await s.ReserveExpectingAsync(s.Reservation("30", expiresAt: Soon()), 201);
        var keep = await s.ReserveExpectingAsync(s.Reservation("20", expiresAt: "2099-01-01T00:00:00Z"), 201);
        await Task.Delay(TimeSpan.FromSeconds(2.5));

        using (var scope = _factory.Services.CreateScope())
        {
            var swept = await scope.ServiceProvider.GetRequiredService<IReservationExpirySweeper>().SweepAsync(DateTimeOffset.UtcNow);
            Assert.True(swept >= 1, "the sweeper expired nothing");
        }

        Assert.Equal("Expired", Reservation(s, created.GetProperty("reservationId").GetString()!)["Status"].AsString);
        Assert.Equal("Active", Reservation(s, keep.GetProperty("reservationId").GetString()!)["Status"].AsString);
        var audit = Assert.Single(s.Persisted("inventory_audit"), a => a.Contains("ReservationId") && a["Action"].AsString == "InventoryReservationExpired");
        Assert.Equal(Guid.Empty.ToString(), audit["ActorUserId"].AsString);
        Assert.Contains(s.Persisted("inventory_outbox"), o => o["EventName"].AsString == "InventoryReservationChanged" && o["Payload"]["Change"].AsString == "Expired");
        Assert.Equal(20m, Assert.Single(s.Persisted("inventory_sku_positions"))["Reserved"].AsDecimal);
        Assert.Equal((100m, 20m, 80m), await s.AvailabilityAsync());
    }

    private static string Soon() => DateTimeOffset.UtcNow.AddSeconds(1.5).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static BsonDocument Reservation(InventoryScenario s, string id)
        => s.Factory.Collection("inventory_reservations").Find(new BsonDocument("_id", id)).Single();
}
