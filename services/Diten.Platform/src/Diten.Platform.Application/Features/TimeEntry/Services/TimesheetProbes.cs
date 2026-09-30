namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>
/// Test seams at the two points where a crash between two writes matters (the <c>IPlatformTransactionFaultProbe</c>
/// pattern). Production registers the no-op below; a test registers one that throws or interleaves, to prove the order
/// of the writes is safe to retry. Neither seam carries data out or changes a decision.
/// </summary>
public interface ITimesheetSubmissionProbe
{
    /// <summary>F1 — between "MOD-0023 instance started" and "week written as Submitted".</summary>
    Task AfterApprovalStartedAsync(Guid weekId, CancellationToken ct);

    /// <summary>F13 — between "withdraw read the decision as pending" and "withdraw cancels the instance".</summary>
    Task BeforeWithdrawCancelAsync(Guid weekId, CancellationToken ct);

    /// <summary>CT acceptance (T1b v3) — just before a save/submit recomputes the week's timer drafts.</summary>
    Task BeforeWeekDraftRecomputeAsync(Guid userId, string weekKey, CancellationToken ct);
}

/// <summary>F9 — between "task total computed" and "task total written".</summary>
public interface ITimesheetFinalizationProbe
{
    Task BeforeTaskTotalWriteAsync(Guid taskItemId, CancellationToken ct);
}

public sealed class NoOpTimesheetProbe : ITimesheetSubmissionProbe, ITimesheetFinalizationProbe
{
    public Task AfterApprovalStartedAsync(Guid weekId, CancellationToken ct) => Task.CompletedTask;

    public Task BeforeWithdrawCancelAsync(Guid weekId, CancellationToken ct) => Task.CompletedTask;

    public Task BeforeWeekDraftRecomputeAsync(Guid userId, string weekKey, CancellationToken ct) => Task.CompletedTask;

    public Task BeforeTaskTotalWriteAsync(Guid taskItemId, CancellationToken ct) => Task.CompletedTask;
}
