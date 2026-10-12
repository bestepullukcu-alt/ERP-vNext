using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>
/// MOD-0280-FU01 D2 / §12 — start the caller's timer. The person is always the caller (D11); the instant is the server's.
///
/// <para>The checks, in the pack's order: the timer is on for the caller's legal entity (D12 — manual entry never
/// depends on it); the task is held by the caller (a task that does not exist and one held by somebody else are ONE
/// answer, so the endpoint cannot probe for task ids) and InProgress; or the category exists and is active.</para>
///
/// <para>The task is never touched: a timer control writes no <c>TaskTransition</c> and changes no lifecycle.</para>
/// </summary>
public sealed class StartTimerHandler : IRequestHandler<StartTimerCommand, Response<TimerMutationDto>>
{
    private readonly ITimerService _timer;
    private readonly ITimerReadModel _read;
    private readonly ITimeEntryTaskGateway _tasks;
    private readonly IWorkCategoryRepository _categories;
    private readonly ICurrentUserContext _currentUser;

    public StartTimerHandler(
        ITimerService timer,
        ITimerReadModel read,
        ITimeEntryTaskGateway tasks,
        IWorkCategoryRepository categories,
        ICurrentUserContext currentUser)
    {
        _timer = timer;
        _read = read;
        _tasks = tasks;
        _categories = categories;
        _currentUser = currentUser;
    }

    public async Task<Response<TimerMutationDto>> Handle(StartTimerCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = _currentUser.UserId;

        if (!await _timer.IsEnabledForAsync(userId, ct))
        {
            return Fail("The timer is not switched on for your legal entity; enter time manually.", 409,
                TimeEntryReasonCodes.TimerDisabledForLegalEntity, request);
        }

        TimerTarget target;
        if (request.Request.TaskItemId is { } taskId)
        {
            var facts = await _tasks.TaskFactsAsync([taskId], ct);
            if (!facts.TryGetValue(taskId, out var task) || task.HolderUserId != userId)
            {
                return Fail("You do not hold this task.", 409, TimeEntryReasonCodes.TimerTaskNotHeld, request);
            }

            if (!task.IsInProgress)
            {
                return Fail("Start the task before timing it.", 409, TimeEntryReasonCodes.TimerTaskNotInProgress, request);
            }

            target = new TimerTarget(taskId, null);
        }
        else
        {
            var code = request.Request.CategoryCode!.Trim().ToUpperInvariant();
            var category = await _categories.GetByCodeAsync(code, ct);
            if (category is null)
            {
                return Fail("Unknown work category.", 400, TimeEntryReasonCodes.TargetInvalid, request);
            }

            if (!category.IsActive)
            {
                return Fail("This work category is no longer active.", 400, TimeEntryReasonCodes.CategoryInactive, request);
            }

            target = new TimerTarget(null, code);
        }

        var outcome = await _timer.StartAsync(userId, target, TimerStartSource.TimerControl, startTransitionId: null, ct);
        if (outcome.ReasonCode is not null)
        {
            return Fail("Another start for you landed at the same moment; reload and retry.", 409, outcome.ReasonCode, request);
        }

        return Response<TimerMutationDto>.Success(
            new TimerMutationDto(await _read.ReadAsync(userId, ct), outcome.StoppedSegmentId), correlationId: request.CorrelationId);
    }

    private static Response<TimerMutationDto> Fail(string message, int status, string code, StartTimerCommand request)
        => Response<TimerMutationDto>.Fail(message, status, code, request.CorrelationId);
}
