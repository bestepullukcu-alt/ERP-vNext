using Diten.Platform.Domain.Entities.ESignature;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>MOD-0022 E3 — the pure sample rules (pack §17 "sample status projection", V21, V22).</summary>
public sealed class ESignatureSampleRulesTests
{
    [Theory]
    [InlineData(new[] { ESignatureSampleEventKind.Submitted }, ESignatureSampleStatus.Pending)]
    [InlineData(new[] { ESignatureSampleEventKind.Submitted, ESignatureSampleEventKind.Approved }, ESignatureSampleStatus.Active)]
    [InlineData(new[] { ESignatureSampleEventKind.Submitted, ESignatureSampleEventKind.Activated }, ESignatureSampleStatus.Active)]
    [InlineData(new[] { ESignatureSampleEventKind.Submitted, ESignatureSampleEventKind.Rejected }, ESignatureSampleStatus.Rejected)]
    [InlineData(new[] { ESignatureSampleEventKind.Submitted, ESignatureSampleEventKind.Approved, ESignatureSampleEventKind.Deactivated }, ESignatureSampleStatus.Inactive)]
    [InlineData(new[] { ESignatureSampleEventKind.Submitted, ESignatureSampleEventKind.Rejected, ESignatureSampleEventKind.Withdrawn }, ESignatureSampleStatus.Withdrawn)]
    public void Status_is_the_projection_of_the_last_event_by_sequence(ESignatureSampleEventKind[] events, ESignatureSampleStatus expected)
    {
        // Given out of order on purpose: the sequence, not the list order, decides.
        var rows = events.Select((e, i) => Event(i, e)).Reverse().ToList();
        Assert.Equal(expected, ESignatureSampleRules.Project(rows));
    }

    [Fact]
    public void A_sample_without_events_is_pending() => Assert.Equal(ESignatureSampleStatus.Pending, ESignatureSampleRules.Project([]));

    [Theory]
    [InlineData(ESignatureSampleStatus.Pending, 0, true)]
    [InlineData(ESignatureSampleStatus.Rejected, 0, true)]
    [InlineData(ESignatureSampleStatus.Inactive, 0, true)]
    [InlineData(ESignatureSampleStatus.Active, 0, false)]
    [InlineData(ESignatureSampleStatus.Inactive, 1, false)]
    [InlineData(ESignatureSampleStatus.Withdrawn, 0, false)]
    public void Withdraw_needs_unused_and_not_active(ESignatureSampleStatus status, long used, bool allowed) =>
        Assert.Equal(allowed, ESignatureSampleRules.CanWithdraw(status, used));

    [Fact]
    public void The_owner_deciding_their_own_sample_is_a_self_decision()
    {
        var sample = Sample("owner-1");
        Assert.True(ESignatureSampleRules.IsSelfDecision(sample, "owner-1"));
        Assert.False(ESignatureSampleRules.IsSelfDecision(sample, "approver-2"));
    }

    private static ESignatureSampleEvent Event(int sequence, ESignatureSampleEventKind kind) => new()
    {
        TenantId = Guid.NewGuid(), SampleId = Guid.Empty, OwnerUserId = "o", Sequence = sequence, Event = kind, By = "b", ByName = "B",
        AtUtc = DateTime.UtcNow
    };

    private static ESignatureSample Sample(string owner) => new()
    {
        TenantId = Guid.NewGuid(), SampleNumber = "IMZ-2026-0001", OwnerUserId = owner, OwnerLogin = owner, OwnerName = owner,
        Kind = ESignatureSampleKind.Drawn, Format = ESignatureSampleFormat.Png, ContentId = "c", StorageProvider = "p", StoragePath = "s",
        ImageHash = "h", CreatedAtUtc = DateTime.UtcNow, TicketRef = "t", IdempotencyKey = "k-12345678"
    };
}
