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

    [Theory]
    [InlineData("W/\"3\"", 3)]
    [InlineData("\"3\"", 3)]
    [InlineData("3", -1)]
    public void IfMatch_is_a_weak_or_strong_etag(string header, int version) => Assert.Equal(version, CycleCountSupport.ExpectedVersion(header));
}
