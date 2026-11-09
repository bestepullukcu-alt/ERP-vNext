using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Domain.Enums;
using Xunit;

namespace Diten.InventoryService.Tests.Domain;

/// <summary>
/// MOD-0173-FU03 AC-7 (§8.2 "Geriye uyum") — an unscoped reservation request keeps its 1.5.0 idempotency fingerprint byte for byte. The two
/// vectors were produced by the code BEFORE this change (84d1fbc05, <c>ReservationFingerprint.Compute</c> run on the same inputs in a detached
/// worktree of that commit) and pinned here; the scope members enter the fingerprint only when sent.
/// </summary>
public sealed class ReservationFingerprintVectorTests
{
    private static readonly Guid Item = Guid.Parse("b1f2c3d4-0000-4000-8000-000000000001");
    private static readonly Guid Sku = Guid.Parse("c3d4e5f6-0000-4000-8000-000000000002");

    private static ParsedReservation Full(string? warehouseId = null, string? lotId = null)
        => new("rsv-vector-1", Item, Sku, SkuLevel.Lsku, 30.000m, "SALES_ORDER", "MOD-0172", "SO-900", "1", 10,
            new DateTimeOffset(2026, 9, 16, 0, 0, 0, TimeSpan.Zero), warehouseId, lotId);

    private static ParsedReservation Minimal() => new("rsv-vector-2", Item, Sku, SkuLevel.Gsku, 5m, "PICK", "MOD-0178", null, null, null, null);

    /// <summary>S-2: blank warehouseId / lotId added to the fingerprint unconditionally → both vectors change, red.</summary>
    [Fact]
    public void Unscoped_request_matches_1_5_0_vector()
    {
        Assert.Equal("2DEFBEEF541CAC1D4BC92408E279CBA5DCFD0E743FE59DD0AB1F0EE60483F316", ReservationFingerprint.Compute(Full()));
        Assert.Equal("7563150F37EF91A378B35CF72D5998E26277B53F6ACD9BC98F0DEFBE6D342AEC", ReservationFingerprint.Compute(Minimal()));
    }

    [Fact]
    public void The_scope_enters_the_fingerprint_only_when_sent()
    {
        var unscoped = ReservationFingerprint.Compute(Full());
        var warehouse = ReservationFingerprint.Compute(Full("3f2b5a1e-0000-4000-8000-000000000201"));
        var lot = ReservationFingerprint.Compute(Full("3f2b5a1e-0000-4000-8000-000000000201", "3f2b5a1e-0000-4000-8000-000000000101"));
        var otherLot = ReservationFingerprint.Compute(Full("3f2b5a1e-0000-4000-8000-000000000201", "3f2b5a1e-0000-4000-8000-000000000102"));
        Assert.Equal(4, new[] { unscoped, warehouse, lot, otherLot }.Distinct(StringComparer.Ordinal).Count());
    }
}
