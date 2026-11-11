using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Rules;
using Xunit;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-2 — the pure rules of <see cref="PutawayTaskRules"/> and the menu of <see cref="PutawayTaskReads.Actions"/> measured directly:
/// the integration flag's priority (AC-46: IntegrityError &gt; PendingLong &gt; Pending &gt; unresolved Refused &gt; None), the result write's
/// one rule (A)–(E) (AC-64), the transaction-2 guard <c>Confirmed + q ≤ Planned</c> (AC-67 (5)) and the state menu (AC-43 / AC-11).
/// </summary>
public sealed class PutawayTaskRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan LongPending = TimeSpan.FromMinutes(30);

    private static TaskConfirmation C(int seq, ConfirmationStatus status, DateTimeOffset? at = null, ConfirmationResolution? resolution = null) => new()
    {
        Seq = seq, Quantity = 10, Status = status, ConfirmedAtUtc = at ?? Now, Resolution = resolution, PostingKey = $"wms:put:x:{seq}"
    };

    private static WarehouseTask T(params TaskConfirmation[] confirmations)
    {
        var task = new WarehouseTask { PlannedQuantity = 60, Status = WarehouseTaskStatus.InProgress };
        task.Confirmations.AddRange(confirmations);
        return task;
    }

    [Fact]
    public void AC46_flag_priority_integrity_error_beats_long_pending_beats_pending_beats_refused()
    {
        var old = Now - TimeSpan.FromMinutes(31);
        Assert.Equal(IntegrationFlag.PostingIntegrityError, PutawayTaskRules.Flag(T(C(1, ConfirmationStatus.IntegrityError), C(2, ConfirmationStatus.Pending, old)), Now, LongPending));
        Assert.Equal(IntegrationFlag.PostingIntegrityError, PutawayTaskRules.Flag(T(C(1, ConfirmationStatus.IntegrityError), C(2, ConfirmationStatus.Pending)), Now, LongPending));
        Assert.Equal(IntegrationFlag.PostingPendingLong, PutawayTaskRules.Flag(T(C(1, ConfirmationStatus.Refused), C(2, ConfirmationStatus.Pending, old)), Now, LongPending));
        Assert.Equal(IntegrationFlag.PostingPending, PutawayTaskRules.Flag(T(C(1, ConfirmationStatus.Refused), C(2, ConfirmationStatus.Pending)), Now, LongPending));
        Assert.Equal(IntegrationFlag.PostingRefused, PutawayTaskRules.Flag(T(C(1, ConfirmationStatus.Refused), C(2, ConfirmationStatus.Posted)), Now, LongPending));
        Assert.Equal(IntegrationFlag.None, PutawayTaskRules.Flag(T(C(1, ConfirmationStatus.Posted)), Now, LongPending));
    }

    [Theory]
    [InlineData(ConfirmationResolution.SupersededByNewConfirmation)]
    [InlineData(ConfirmationResolution.ReturnedToSource)]
    public void AC46_a_resolved_refused_raises_no_flag_but_another_unresolved_one_does(ConfirmationResolution resolution)
    {
        Assert.Equal(IntegrationFlag.None, PutawayTaskRules.Flag(T(C(1, ConfirmationStatus.Refused, resolution: resolution), C(2, ConfirmationStatus.Posted)), Now, LongPending));
        Assert.Equal(IntegrationFlag.PostingRefused,
            PutawayTaskRules.Flag(T(C(1, ConfirmationStatus.Refused, resolution: resolution), C(2, ConfirmationStatus.Refused)), Now, LongPending));
    }

    [Theory]
    [InlineData(20, 40, 60, true)]
    [InlineData(40, 20, 60, true)]
    [InlineData(41, 20, 60, false)]
    [InlineData(60, 0.000001, 60, false)]
    public void AC67_5_the_transaction_2_guard_confirmed_plus_q_within_planned(decimal confirmed, decimal q, decimal planned, bool expected)
        => Assert.Equal(expected, PutawayTaskRules.CanIncrement(confirmed, q, planned));

    [Fact]
    public void AC67_5_applying_a_posted_result_that_would_overshoot_is_not_applied()
    {
        var c = C(2, ConfirmationStatus.Pending);
        c.Quantity = 30;
        var task = T(C(1, ConfirmationStatus.Posted), c);
        task.ConfirmedQuantity = 40;
        var (applied, _, _) = PutawayTaskRules.ApplyPosted(task, c, Guid.NewGuid().ToString("D"), "INV-1", clearLock: true);
        Assert.False(applied);
        Assert.Equal(40m, task.ConfirmedQuantity);
        Assert.Equal(ConfirmationStatus.Pending, c.Status);
    }

    [Fact]
    public void AC64_the_one_rule_A_to_E()
    {
        var mine = Guid.NewGuid();
        var other = Guid.NewGuid();
        var txn = Guid.NewGuid().ToString("D");
        TaskConfirmation Locked(Guid owner, DateTimeOffset until) => new() { Status = ConfirmationStatus.Pending, AttemptId = owner, InFlightUntilUtc = until };

        // (A) my lock → write; the reconciler writes only when the lock is empty or expired
        Assert.Equal(ResultDecision.Write, PutawayTaskRules.Decide(Locked(mine, Now.AddMinutes(1)), mine, Now, AttemptOutcome.Posted, txn));
        Assert.Equal(ResultDecision.Write, PutawayTaskRules.Decide(Locked(other, Now.AddSeconds(-1)), null, Now, AttemptOutcome.Posted, txn));
        // (C1) another live attempt / (C2) another, expired attempt
        Assert.Equal(ResultDecision.InFlightElsewhere, PutawayTaskRules.Decide(Locked(other, Now.AddMinutes(1)), mine, Now, AttemptOutcome.Posted, txn));
        Assert.Equal(ResultDecision.InFlightElsewhere, PutawayTaskRules.Decide(Locked(other, Now.AddMinutes(1)), null, Now, AttemptOutcome.Posted, txn));
        Assert.Equal(ResultDecision.OwnedByExpiredAttempt, PutawayTaskRules.Decide(Locked(other, Now.AddSeconds(-1)), mine, Now, AttemptOutcome.Posted, txn));
        // (B) already Posted with this attempt's transaction
        Assert.Equal(ResultDecision.AlreadyDone,
            PutawayTaskRules.Decide(new TaskConfirmation { Status = ConfirmationStatus.Posted, TransactionId = txn }, mine, Now, AttemptOutcome.Posted, txn));
        // (D) Posted here but Refused there → diverged; already IntegrityError → no write
        Assert.Equal(ResultDecision.MarkDiverged, PutawayTaskRules.Decide(new TaskConfirmation { Status = ConfirmationStatus.Refused }, mine, Now, AttemptOutcome.Posted, txn));
        Assert.Equal(ResultDecision.AlreadyIntegrityError,
            PutawayTaskRules.Decide(new TaskConfirmation { Status = ConfirmationStatus.IntegrityError }, mine, Now, AttemptOutcome.Posted, txn));
        // (E) a Refused result meeting a Posted confirmation
        Assert.Equal(ResultDecision.NotPending,
            PutawayTaskRules.Decide(new TaskConfirmation { Status = ConfirmationStatus.Posted, TransactionId = txn }, mine, Now, AttemptOutcome.Refused, null));
    }

    [Fact]
    public void AC43_menu_cancel_without_posted_close_short_with_posted_retry_only_while_pending()
    {
        var fresh = new WarehouseTask { PlannedQuantity = 60, Status = WarehouseTaskStatus.Open };
        var (allowed, disabled) = PutawayTaskReads.Actions(fresh);
        Assert.Contains("Cancel", allowed);
        Assert.Contains(disabled, d => d is { Action: "CloseShort", ReasonCode: "NoPostedConfirmation" });

        var posted = T(C(1, ConfirmationStatus.Posted));
        (allowed, disabled) = PutawayTaskReads.Actions(posted);
        Assert.Contains("CloseShort", allowed);
        Assert.Contains(disabled, d => d is { Action: "Cancel", ReasonCode: "HasPostedConfirmations" });

        var pending = T(C(1, ConfirmationStatus.Pending));
        (allowed, disabled) = PutawayTaskReads.Actions(pending);
        Assert.Equal(["Retry"], allowed);
        Assert.All(disabled.Where(d => d.Action is "Confirm" or "Update" or "Claim" or "Cancel" or "CloseShort"), d => Assert.Equal("ConfirmationPending", d.ReasonCode));

        var integrity = T(C(1, ConfirmationStatus.IntegrityError));
        (allowed, _) = PutawayTaskReads.Actions(integrity);
        Assert.Equal(["ResolveIntegrity"], allowed);

        var serial = T(C(1, ConfirmationStatus.Posted));
        serial.SerialControlled = true;
        (_, disabled) = PutawayTaskReads.Actions(serial);
        Assert.Contains(disabled, d => d is { Action: "CloseShort", ReasonCode: "SerialControlled" });
    }
}
