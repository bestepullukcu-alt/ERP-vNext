using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.InventoryService.Tests.Trace;

/// <summary>
/// B-3 / CT-D8 — Expired only by the system: the sweeper expires every lot whose expiry date is before today, in ANY
/// status, with a <c>SystemExpired</c> history row from the system actor; nothing else sets or leaves Expired; a non-Draft
/// lot — Expired included — is still edited with a reason. The periodic sweeper is off in tests; it is driven here.
/// The sweeper reads every tenant: every class that creates lots with a past expiry date or drives the sweeper is in
/// <see cref="LotExpiryCollection"/> (WP-0174-P2 adds <see cref="LedgerLotExpiryTests"/>), so they run one at a time.
/// </summary>
[Collection(LotExpiryCollection.Name)]
public sealed class LotExpiryTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public LotExpiryTests(TraceApiFactory factory) => _factory = factory;

    [Fact]
    public async Task The_sweeper_expires_due_lots_in_any_status_and_writes_a_system_row()
    {
        var s = new TraceScenario(_factory);
        var past = new Dictionary<string, object?> { ["manufacturingDate"] = "2024-06-15", ["expiryDate"] = "2026-06-30" };
        var (draft, _, _) = await s.CreateLotAsync(extra: past);
        var (released, releasedTag, _) = await s.CreateLotAsync(extra: past);
        var (blocked, blockedTag, _) = await s.CreateLotAsync(extra: past);
        var today = DateTimeOffset.UtcNow.UtcDateTime.Date.ToString("yyyy-MM-dd");
        var (dueToday, _, _) = await s.CreateLotAsync(extra: new() { ["expiryDate"] = today });

        await s.Expect(s.StatusAsync(released, "Released", "QA serbest bırakma onayı", releasedTag), 200);
        await s.Expect(s.StatusAsync(blocked, "Released", "QA serbest bırakma onayı", blockedTag), 200);
        await s.Expect(s.StatusAsync(blocked, "Blocked", "Sapma DEV-2026-018 incelemesi", await s.ETagOf(blocked), ack: true), 200);

        Assert.True(await SweepAsync() >= 3);

        foreach (var id in new[] { draft, released, blocked })
        {
            var lot = await s.Expect(s.GetAsync($"/api/trace/lots/{id}"), 200);
            Assert.Equal("Expired", lot.GetProperty("status").GetString());
            Assert.Equal("Expired", lot.GetProperty("expiryState").GetString());
            var row = Assert.Single(await s.History(id), h => h.GetProperty("action").GetString() == "SystemExpired");
            Assert.Equal("Expired", row.GetProperty("newValue").GetString());
            Assert.Equal(Guid.Empty.ToString("D"), row.GetProperty("actorUserId").GetString());
            Assert.Equal("system-expiry", row.GetProperty("source").GetString());
        }

        Assert.Equal("Draft", (await s.Expect(s.GetAsync($"/api/trace/lots/{dueToday}"), 200)).GetProperty("status").GetString());

        // Idempotent: a second pass finds nothing of this tenant and writes no second row.
        await SweepAsync();
        Assert.Single(await s.History(draft), h => h.GetProperty("action").GetString() == "SystemExpired");
    }

    [Fact]
    public async Task An_Expired_lot_never_leaves_Expired_by_hand_but_is_edited_with_a_reason()
    {
        var s = new TraceScenario(_factory);
        var (id, _, _) = await s.CreateLotAsync(extra: new() { ["manufacturingDate"] = "2024-06-15", ["expiryDate"] = "2026-06-30" });
        await SweepAsync();
        var etag = await s.ETagOf(id);

        await s.ExpectError(s.StatusAsync(id, "Released", "Elle serbest bırakma denemesi", etag), 422, "LOT_STATUS_TRANSITION_INVALID");
        await s.ExpectError(s.StatusAsync(id, "Blocked", "Elle bloke etme denemesi", etag, ack: true), 422, "LOT_STATUS_TRANSITION_INVALID");
        await s.ExpectError(s.PatchAsync(id, new { coaReference = "AR-1" }, etag), 400, "REASON_REQUIRED");
        await s.Expect(s.PatchAsync(id, new { coaReference = "AR-1", reason = "Arşiv düzeltmesi yapıldı" }, etag), 200);
        Assert.Equal("Expired", (await s.Expect(s.GetAsync($"/api/trace/lots/{id}"), 200)).GetProperty("status").GetString());
    }

    private async Task<int> SweepAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ILotExpirySweeper>().SweepAsync(DateTimeOffset.UtcNow);
    }
}
