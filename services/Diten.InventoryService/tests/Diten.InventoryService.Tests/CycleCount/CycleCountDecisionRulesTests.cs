using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using Diten.InventoryService.Domain.Enums;
using Xunit;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>MOD-0181 DL-3 pure rules (pack §8.5, §8.6, §8.3, §2.4, §12; AC-21, AC-29, AC-47, AC-49) — the production rules, no I/O.</summary>
public sealed class CycleCountDecisionRulesTests
{
    private static CycleCountLine Line(decimal expectedAtCount, decimal variance, bool movement = false, bool drift = false, bool serial = false) => new()
    {
        Id = Guid.NewGuid(), CountId = Guid.NewGuid(), WarehouseId = Guid.NewGuid(), LocationId = Guid.NewGuid(), ItemId = Guid.NewGuid(), SkuId = Guid.NewGuid(),
        SkuLevel = SkuLevel.Lsku, BaseUomId = "EA", ExpectedQuantity = expectedAtCount, ExpectedAtCount = expectedAtCount, VarianceQuantity = variance,
        CountedQuantity = expectedAtCount + variance, CountedAt = new DateTimeOffset(2026, 11, 10, 8, 30, 15, 123, TimeSpan.Zero), MovementDuringCount = movement,
        SnapshotDrift = drift, SerialControlled = serial, Status = CycleCountLineStatus.AwaitingApproval
    };

    [Theory]
    [InlineData(10, -1, "2", "15", true)]   // 1 ≤ 2 and 10 % ≤ 15 %
    [InlineData(10, -2, "2", "50", true)]   // DL-3 CT DÜŞÜK-1: |v| exactly the absolute — inside
    [InlineData(10, -2, "5", "20", true)]   // DL-3 CT DÜŞÜK-1: v % exactly the percent — inside
    [InlineData(10, -3, "2", "50", false)]  // above the absolute
    [InlineData(10, -2, "2", "15", false)]  // 20 % above the percent
    [InlineData(0, 1, "5", "100", false)]   // expected 0: the percent is never met
    [InlineData(10, -1, "0", "0", false)]   // the default tolerance takes nothing
    public void AC21_the_tolerance_needs_the_absolute_AND_the_percent(decimal atCount, decimal variance, string absolute, string percent, bool within)
        => Assert.Equal(within, CycleCountDecisionRules.WithinTolerance(Line(atCount, variance),
            new CycleCountPolicy(decimal.Parse(absolute, System.Globalization.CultureInfo.InvariantCulture), decimal.Parse(percent, System.Globalization.CultureInfo.InvariantCulture), 2, 2000)));

    [Fact]
    public void AC21_a_flagged_drifting_or_serial_line_never_takes_the_tolerance()
    {
        var wide = new CycleCountPolicy(100m, 100m, 2, 2000);
        Assert.True(CycleCountDecisionRules.WithinTolerance(Line(10, -1), wide));
        Assert.False(CycleCountDecisionRules.WithinTolerance(Line(10, -1, movement: true), wide));
        Assert.False(CycleCountDecisionRules.WithinTolerance(Line(10, -1, drift: true), wide));
        Assert.False(CycleCountDecisionRules.WithinTolerance(Line(10, -1, serial: true), wide));
    }

    [Fact]
    public void AC22_the_counter_and_the_submitter_are_self_approvers()
    {
        var counter = Guid.NewGuid();
        var submitter = Guid.NewGuid();
        var line = Line(10, -1);
        line.CountedBy = counter;
        var count = new CycleCount { SubmittedBy = submitter };
        Assert.True(CycleCountDecisionRules.IsSelfApproval(count, line, counter));
        Assert.True(CycleCountDecisionRules.IsSelfApproval(count, line, submitter));
        Assert.False(CycleCountDecisionRules.IsSelfApproval(count, line, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(2, MovementType.CountGain, "COUNT_GAIN")]
    [InlineData(-2, MovementType.CountLoss, "COUNT_LOSS")]
    public void AC29_0181_builds_only_count_movements_with_the_lines_key_and_the_first_attempts_dates(decimal variance, MovementType type, string wire)
    {
        var line = Line(10, variance);
        var now = new DateTimeOffset(2026, 11, 11, 23, 59, 59, TimeSpan.Zero);
        var adjustment = CycleCountDecisionRules.FirstAdjustment(line, now)!;
        Assert.Equal(type, adjustment.MovementType);
        Assert.Equal(2m, adjustment.Quantity);
        Assert.Equal("cycle-count:" + line.Id.ToString("D"), adjustment.IdempotencyKey);
        Assert.True(adjustment.IdempotencyKey.Length <= 128);
        Assert.Equal(new DateTimeOffset(2026, 11, 10, 8, 30, 15, TimeSpan.Zero), adjustment.TransactionDate); // CountedAt, to the second
        Assert.Equal(new DateTimeOffset(2026, 11, 11, 0, 0, 0, TimeSpan.Zero), adjustment.PostingDate);    // the first attempt's UTC day

        line.Adjustment = adjustment;
        var request = CycleCountPostingEngine.Request(new CycleCount { Id = line.CountId, Number = "GMG-CNT-000001" }, line);
        Assert.Equal(wire, request.MovementType);
        Assert.Equal("MOD-0181", request.SourceModule);
        Assert.Equal("CYCLE_COUNT", request.SourceType);
        Assert.Equal("CYCLE_COUNT_VARIANCE", request.MovementReason);
        Assert.Equal(line.Id.ToString("D"), request.SourceLineId);
        Assert.Equal(line.CountId.ToString("D"), request.SourceDocumentId);
        Assert.Equal("2026-11-10T08:30:15Z", request.TransactionDate);
        Assert.Equal("2026-11-11T00:00:00Z", request.PostingDate);
        Assert.Equal(type == MovementType.CountGain ? line.LocationId.ToString("D") : null, request.ToLocationId);
        Assert.Equal(type == MovementType.CountLoss ? line.LocationId.ToString("D") : null, request.FromLocationId);
        Assert.Null(CycleCountDecisionRules.FirstAdjustment(Line(10, 0, movement: true), now)); // a flagged variance-0 line has nothing to post
    }

    [Fact]
    public void AC29_any_other_movement_type_is_refused_before_the_ledger_is_called()
    {
        var line = Line(10, 2);
        line.Adjustment = CycleCountDecisionRules.FirstAdjustment(line, DateTimeOffset.UtcNow)!;
        line.Adjustment.MovementType = MovementType.LocationTransfer;
        Assert.Throws<InvalidOperationException>(() => CycleCountPostingEngine.Request(new CycleCount { Number = "GMG-CNT-000001" }, line));
    }

    [Fact]
    public void S8_3_the_last_state_of_a_line()
    {
        var approved = Line(10, 2);
        approved.Status = CycleCountLineStatus.Approved;
        approved.Adjustment = CycleCountDecisionRules.FirstAdjustment(approved, DateTimeOffset.UtcNow);
        Assert.False(CycleCountRules.IsFinal(approved));
        approved.Adjustment!.Status = CycleCountAdjustmentStatus.Refused;
        Assert.False(CycleCountRules.IsFinal(approved));
        approved.Adjustment.Status = CycleCountAdjustmentStatus.Posted;
        Assert.True(CycleCountRules.IsFinal(approved));
        var flagged = Line(10, 0, movement: true);
        flagged.Status = CycleCountLineStatus.Approved;
        Assert.True(CycleCountRules.IsFinal(flagged)); // nothing to post
    }

    [Theory]
    [InlineData("1.5", "100", 0, 1, null)]
    [InlineData("-1", "0", 2, 2000, "toleranceAbsolute")]
    [InlineData("0", "150", 2, 2000, "tolerancePercent")]
    [InlineData("0", "0", -1, 2000, "maxRecounts")]
    [InlineData("0", "0", 11, 2000, "maxRecounts")]
    [InlineData("0", "0", 2, 0, "maxLinesPerCount")]
    [InlineData("0", "0", 2, 5001, "maxLinesPerCount")]
    public void AC49_the_settings_bounds(string absolute, string percent, int recounts, int lines, string? field)
        => Assert.Equal(field, CycleCountDecisionRules.SettingsError(decimal.Parse(absolute, System.Globalization.CultureInfo.InvariantCulture), decimal.Parse(percent, System.Globalization.CultureInfo.InvariantCulture), recounts, lines));

    [Fact]
    public void AC47_a_movement_is_a_count_correction_only_when_both_directions_hold()
    {
        var line = Line(10, 2);
        line.Status = CycleCountLineStatus.Approved;
        line.Adjustment = CycleCountDecisionRules.FirstAdjustment(line, new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero))!;
        var tx = Guid.NewGuid();
        CycleCountMovementFacts Facts(DateTimeOffset at, string module = "MOD-0181", string? key = null) => new(tx, key ?? line.Adjustment!.IdempotencyKey, module,
            line.CountId.ToString("D"), line.Id.ToString("D"), at);

        // Pending: CreatedAt ≥ FirstAttemptAt — the committed correction of a Pending line is a count's; an older one is not
        Assert.True(CycleCountDecisionRules.IsCountMovement(Facts(line.Adjustment.FirstAttemptAt.AddSeconds(1)), line));
        Assert.False(CycleCountDecisionRules.IsCountMovement(Facts(line.Adjustment.FirstAttemptAt.AddSeconds(-1)), line));
        // Posted: only the movement whose id the line holds
        line.Adjustment.Status = CycleCountAdjustmentStatus.Posted;
        line.Adjustment.TransactionId = tx;
        Assert.True(CycleCountDecisionRules.IsCountMovement(Facts(DateTimeOffset.UtcNow), line));
        line.Adjustment.TransactionId = Guid.NewGuid();
        Assert.False(CycleCountDecisionRules.IsCountMovement(Facts(DateTimeOffset.UtcNow), line));
        // IntegrityError / a MOD-0181 declaration with another key / another module: never
        line.Adjustment.TransactionId = tx;
        Assert.False(CycleCountDecisionRules.IsCountMovement(Facts(DateTimeOffset.UtcNow, module: "MOD-0173"), line));
        Assert.False(CycleCountDecisionRules.IsCountMovement(Facts(DateTimeOffset.UtcNow, key: "manual-1"), line));
        line.Adjustment.Status = CycleCountAdjustmentStatus.IntegrityError;
        Assert.False(CycleCountDecisionRules.IsCountMovement(Facts(DateTimeOffset.UtcNow), line));
        Assert.False(CycleCountDecisionRules.IsCountMovement(Facts(DateTimeOffset.UtcNow), null));
    }
}
