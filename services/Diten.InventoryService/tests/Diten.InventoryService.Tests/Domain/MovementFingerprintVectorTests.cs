using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Xunit;

namespace Diten.InventoryService.Tests.Domain;

/// <summary>
/// MOD-0173-FU03 AC-25 (§8.3, §8.3.1) — a movement without <c>reservationId</c> and without <c>restoreReservation</c> (or with it false)
/// keeps its idempotency fingerprint byte for byte. The vectors were produced by the code BEFORE this change (0f32e805c — the
/// <c>RequestFingerprint</c> of 1.5.0, unchanged by 1.6.0 — run on the same requests in a detached worktree of that commit) and pinned here.
/// </summary>
public sealed class MovementFingerprintVectorTests
{
    private static PostMovementCommand Issue(string? reservationId = null) => new()
    {
        HeaderIdempotencyKey = "mv-vector-1", IdempotencyKey = "mv-vector-1", MovementType = "GOODS_ISSUE_SALES",
        ItemId = "b1f2c3d4-0000-4000-8000-000000000001", SkuId = "c3d4e5f6-0000-4000-8000-000000000002", SkuLevel = "Lsku",
        FromWarehouseId = "3f2b5a1e-0000-4000-8000-000000000201", FromLocationId = "3f2b5a1e-0000-4000-8000-000000000301",
        Quantity = "30", UomId = "EA", LotId = "3f2b5a1e-0000-4000-8000-000000000101", SourceModule = "MOD-0183", SourceType = "SHIPMENT",
        SourceDocumentId = "SH-1", SourceLineId = "1", TransactionDate = "2026-09-30T10:00:00Z", PostingDate = "2026-09-30T10:00:00Z",
        ReservationId = reservationId
    };

    private static PostMovementCommand Reversal(bool? restore = null) => new()
    {
        HeaderIdempotencyKey = "mv-vector-2", IdempotencyKey = "mv-vector-2", MovementType = "REVERSAL",
        ItemId = "b1f2c3d4-0000-4000-8000-000000000001", SkuId = "c3d4e5f6-0000-4000-8000-000000000002", SkuLevel = "Lsku",
        Quantity = "30", UomId = "EA", SourceModule = "MOD-0173", SourceType = "REVERSAL",
        SourceDocumentId = "7c1d2e3f-0000-4000-8000-000000000009", TransactionDate = "2026-09-30T10:00:00Z", PostingDate = "2026-09-30T10:00:00Z",
        RestoreReservation = restore
    };

    private static string Fingerprint(PostMovementCommand command)
    {
        var (movement, errors) = MovementRequestParser.Parse(command);
        Assert.True(movement is not null, string.Join("; ", errors.Select(e => e.Message)));
        return RequestFingerprint.Compute(movement!);
    }

    /// <summary>S-17: a blank reservationId added to the fingerprint unconditionally → the issue vector changes, red.</summary>
    [Fact]
    public void Request_without_reservation_id_matches_1_5_0_vector()
    {
        Assert.Equal("8F0C21E9526BDA23BFE8E4CFF4A93EE25A53E9A16E712FB7B002D47AD7BA6641", Fingerprint(Issue()));
        Assert.NotEqual(Fingerprint(Issue()), Fingerprint(Issue("3f2b5a1e-0000-4000-8000-000000000401")));
        Assert.NotEqual(Fingerprint(Issue("3f2b5a1e-0000-4000-8000-000000000401")), Fingerprint(Issue("3f2b5a1e-0000-4000-8000-000000000402")));
    }

    /// <summary>S-17b: restoreReservation (false included) added to the fingerprint unconditionally → the REVERSAL vector changes, red.</summary>
    [Fact]
    public void Reversal_without_restore_flag_matches_1_5_0_vector()
    {
        const string vector = "DF8922AA5E14EE8E14C50AB3F2EF85603CFD8A09A0A6393E0C1510C4BB804100";
        Assert.Equal(vector, Fingerprint(Reversal()));
        Assert.Equal(vector, Fingerprint(Reversal(false)));
        Assert.NotEqual(vector, Fingerprint(Reversal(true)));
    }
}
