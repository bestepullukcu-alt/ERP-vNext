using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using Xunit;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>MOD-0181 DL-1 pure rules (pack §17 "Birim"): the DL-1 transitions, the reason bound, the window, the blind rule, the key format.</summary>
public sealed class CycleCountRulesTests
{
    [Theory]
    [InlineData(CycleCountStatus.Draft, true, true, true)]
    [InlineData(CycleCountStatus.Released, false, true, false)]
    [InlineData(CycleCountStatus.Counting, false, true, false)]
    [InlineData(CycleCountStatus.Submitted, false, false, false)]
    [InlineData(CycleCountStatus.Closed, false, false, false)]
    [InlineData(CycleCountStatus.Cancelled, false, false, false)]
    public void The_DL1_transitions_follow_pack_8_3(CycleCountStatus status, bool draft, bool canCancel, bool cancelDeletes)
    {
        var count = new CycleCount { Status = status };
        Assert.Equal(draft, CycleCountRules.IsDraft(count));
        Assert.Equal(canCancel, CycleCountRules.CanCancel(count));
        Assert.Equal(cancelDeletes, CycleCountRules.CancelDeletes(count));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData(" sebep ", "sebep")]
    public void A_reason_is_trimmed_and_required(string? raw, string? expected) => Assert.Equal(expected, CycleCountRules.Reason(raw));

    [Fact]
    public void A_reason_over_500_characters_is_refused() => Assert.Null(CycleCountRules.Reason(new string('x', 501)));

    [Fact]
    public void The_snapshot_window_is_ten_minutes()
    {
        var at = new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);
        Assert.Equal(at.AddMinutes(-10), CycleCountRules.WindowStart(at));
    }

    [Theory]
    [InlineData(false, CycleCountStatus.Released, false, true)]
    [InlineData(true, CycleCountStatus.Released, true, false)]
    [InlineData(true, CycleCountStatus.Counting, true, false)]
    [InlineData(true, CycleCountStatus.Submitted, false, false)]
    [InlineData(true, CycleCountStatus.Submitted, true, true)]
    public void A_blind_count_hides_the_expected_quantity_before_submit_whatever_the_keys(bool blind, CycleCountStatus status, bool readsVariances, bool shows)
        => Assert.Equal(shows, CycleCountReads.ShowsExpected(new CycleCount { Blind = blind, Status = status }, readsVariances));

    [Theory]
    [InlineData("k", "k", "idempotencyKey must be 8-100 characters.")]
    [InlineData("12345678", "12345679", "The Idempotency-Key header must equal body idempotencyKey.")]
    [InlineData(null, "12345678", "Both the Idempotency-Key header and body idempotencyKey are required.")]
    [InlineData("12345678", "12345678", null)]
    public void The_key_rule_is_draft_rule_5(string? header, string? body, string? error) => Assert.Equal(error, CycleCountSupport.KeyError(header, body, out _));

    // ── DL-2 (pack §8.3, §8.4, §12) ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("0", true, 0)]
    [InlineData("12", true, 12)]
    [InlineData("1.123456", true, 1.123456)]
    [InlineData("1.1234567", false, 0)]
    [InlineData("-1", false, 0)]
    [InlineData("+1", false, 0)]
    [InlineData("1e3", false, 0)]
    [InlineData(" 1", false, 0)]
    [InlineData("", false, 0)]
    [InlineData(null, false, 0)]
    public void A_counted_quantity_is_a_decimal_string_of_at_most_six_fraction_digits(string? raw, bool ok, double value)
    {
        Assert.Equal(ok, CycleCountRules.TryParseCounted(raw, out var parsed));
        if (ok) Assert.Equal((decimal)value, parsed);
    }

    [Theory]
    [InlineData(CycleCountLineOrigin.Snapshot, true)]
    [InlineData(CycleCountLineOrigin.Recount, true)]
    [InlineData(CycleCountLineOrigin.Found, false)]
    public void Submit_needs_the_Snapshot_and_Recount_lines_counted(CycleCountLineOrigin origin, bool mustBeCounted)
    {
        Assert.Equal(mustBeCounted, CycleCountRules.MustBeCounted(new CycleCountLine { Origin = origin }));
        Assert.Equal(mustBeCounted, CycleCountRules.IsUncounted(new CycleCountLine { Origin = origin }));
        Assert.False(CycleCountRules.IsUncounted(new CycleCountLine { Origin = origin, CountedQuantity = 0m }));
    }

    private static readonly DateTimeOffset SnapshotAt = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid InSnapshot = Guid.NewGuid();

    private static CycleCountKeyMovement Move(int minutes, decimal delta, Guid? id = null) => new(id ?? Guid.NewGuid(), SnapshotAt.AddMinutes(minutes), delta);

    [Fact]
    public void Variance_counts_the_movements_after_the_snapshot_up_to_the_entry_and_never_a_snapshot_movement()
    {
        // a receipt already in the snapshot (+5), +3 in after it, −1 out after the entry (in the balance, not in the variance)
        var v = CycleCountVarianceRules.Compute(5m, 8m, SnapshotAt.AddMinutes(10), [InSnapshot],
            [Move(-2, 5m, InSnapshot), Move(5, 3m), Move(20, -1m)], balanceToday: 7m);
        Assert.Equal(3m, v.MovementsSinceSnapshot);
        Assert.Equal(8m, v.ExpectedAtCount);
        Assert.Equal(0m, v.VarianceQuantity);
        Assert.True(v.MovementDuringCount);
        Assert.False(v.SnapshotDrift);
        Assert.Equal(CycleCountLineStatus.AwaitingApproval, v.Status);
    }

    [Theory]
    [InlineData(4.5, -0.5)]
    [InlineData(0, -5)]
    [InlineData(5.000001, 0.000001)]
    public void Variance_is_counted_minus_expected_at_count(double counted, double variance)
        => Assert.Equal((decimal)variance, CycleCountVarianceRules.Compute(5m, (decimal)counted, SnapshotAt.AddMinutes(1), [], [], 5m).VarianceQuantity);

    [Fact]
    public void Only_variance_zero_without_a_flag_or_a_drift_is_NoVariance()
    {
        Assert.Equal(CycleCountLineStatus.NoVariance, CycleCountVarianceRules.Compute(5m, 5m, SnapshotAt.AddMinutes(1), [], [], 5m).Status);
        // a found line: expected 0, nothing moved, counted 0 → NoVariance; counted 2 → awaits
        Assert.Equal(CycleCountLineStatus.NoVariance, CycleCountVarianceRules.Compute(0m, 0m, SnapshotAt.AddMinutes(1), [], [], 0m).Status);
        Assert.Equal(CycleCountLineStatus.AwaitingApproval, CycleCountVarianceRules.Compute(0m, 2m, SnapshotAt.AddMinutes(1), [], [], 0m).Status);
        // drift at variance 0 (r4, 02/F-1): the balance moved without a movement the window saw
        var drift = CycleCountVarianceRules.Compute(5m, 5m, SnapshotAt.AddMinutes(1), [], [], 7m);
        Assert.Equal(0m, drift.VarianceQuantity);
        Assert.True(drift.SnapshotDrift);
        Assert.Equal(CycleCountLineStatus.AwaitingApproval, drift.Status);
    }

    [Theory]
    [InlineData("W/\"3\"", 3)]
    [InlineData("\"3\"", 3)]
    [InlineData("3", -1)]
    public void IfMatch_is_a_weak_or_strong_etag(string header, int version) => Assert.Equal(version, CycleCountSupport.ExpectedVersion(header));
}
