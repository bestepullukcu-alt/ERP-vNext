using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Observability;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>
/// MOD-0280-FU01 §5.1 item 1 — time entry's side of the MOD-0024 hook. Decides cheaply whether the transition matters to
/// any timer (a start by the holder, or a running segment on the task) and only then sends the audited
/// <see cref="ApplyTaskTransitionToTimerCommand"/> — so a task edit that concerns no timer writes nothing and audits
/// nothing.
///
/// <para><b>Never throws into the task write</b> (the contract says so): the task has already committed. A failure is
/// logged and the next timer or week read reconciles (pack §13 "Hook failed").</para>
///
/// <para>Resolves its collaborators from the request scope at call time rather than taking them in the constructor: the
/// task repository that calls it is itself a dependency of the task gateway the timer reads through, and a constructor
/// chain would be a cycle.</para>
/// </summary>
public sealed class TaskTransitionTimerObserver : ITaskTransitionObserver
{
    private readonly IServiceProvider _services;
    private readonly ILogger<TaskTransitionTimerObserver> _logger;

    public TaskTransitionTimerObserver(IServiceProvider services, ILogger<TaskTransitionTimerObserver> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task OnTransitionRecordedAsync(TaskTransitionObservation observation, CancellationToken ct = default)
    {
        try
        {
            var segments = _services.GetRequiredService<ITimerSegmentRepository>();
            var relevant = TaskTransitionTimerRules.StartsRun(observation)
                           || (await segments.ListRunningByTaskAsync(observation.TaskItemId, ct))
                               .Any(segment => TaskTransitionTimerRules.EndsRunFor(observation, segment.UserId));
            if (!relevant)
            {
                return;
            }

            var correlation = _services.GetService<ICorrelationContext>()?.CorrelationId;
            var response = await _services.GetRequiredService<IMediator>().Send(
                new ApplyTaskTransitionToTimerCommand(observation, correlation ?? Guid.NewGuid().ToString()), CancellationToken.None);
            if (!response.IsSuccessful)
            {
                _logger.LogWarning(
                    "time-entry.timer.hook refused TaskId={TaskId} TransitionId={TransitionId} Reason={Reason}",
                    observation.TaskItemId, observation.TransitionId, response.ReasonCode);
            }
        }
        catch (Exception ex)
        {
            // Including cancellation: the task write it follows has committed and must not be reported as failed.
            _logger.LogWarning(ex,
                "time-entry.timer.hook failed TaskId={TaskId} TransitionId={TransitionId}; the next read reconciles.",
                observation.TaskItemId, observation.TransitionId);
        }
    }
}
