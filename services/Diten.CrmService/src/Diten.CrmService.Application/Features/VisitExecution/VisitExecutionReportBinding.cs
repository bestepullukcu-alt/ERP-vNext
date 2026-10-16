using Diten.CrmService.Domain.Entities;
using ExecutionEntity = Diten.CrmService.Domain.Entities.VisitExecution;

namespace Diten.CrmService.Application.Features.VisitExecution;

/// <summary>
/// WP-VW-W3 (BE-a, §2.2) — how the W1 / W2 report and plan commands read the visit's execution, in ONE place:
/// <list type="bullet">
/// <item>a started (or finished) visit takes no <c>missed</c> / <c>rescheduled</c> outcome and no plan cancel →
/// 409 <c>visit_execution_started</c>;</item>
/// <item>a started but unfinished visit is not submitted as completed → 409 <c>visit_execution_not_finished</c>;</item>
/// <item>a finished visit's report <c>ExecutedAt</c> is the SERVER <c>startedAt</c>; the client's value is ignored.</item>
/// </list>
/// </summary>
public static class VisitExecutionReportBinding
{
    public static (string Message, string Code)? RefuseNotDone(ExecutionEntity? execution, string outcome)
        => execution is not null
           && (string.Equals(outcome, VisitExecutionOutcome.Missed, StringComparison.Ordinal)
               || string.Equals(outcome, VisitExecutionOutcome.Rescheduled, StringComparison.Ordinal))
            ? ("The visit has been started; it cannot be recorded as not done or rescheduled.", VisitExecutionErrorCodes.ExecutionStarted)
            : null;

    public static (string Message, string Code)? RefuseCancel(ExecutionEntity? execution)
        => execution is not null
            ? ("The visit has been started; it cannot be cancelled.", VisitExecutionErrorCodes.ExecutionStarted)
            : null;

    public static (string Message, string Code)? RefuseSubmit(ExecutionEntity? execution)
        => execution is not null && execution.IsStarted()
            ? ("Finish the visit before submitting its report.", VisitExecutionErrorCodes.ExecutionNotFinished)
            : null;

    /// <summary>The report's execution instant: the server start of a finished execution, else <paramref name="fallback"/>.</summary>
    public static DateTimeOffset ExecutedAt(ExecutionEntity? execution, DateTimeOffset fallback)
        => execution is not null && execution.IsFinished() ? execution.StartedAt : fallback;
}
