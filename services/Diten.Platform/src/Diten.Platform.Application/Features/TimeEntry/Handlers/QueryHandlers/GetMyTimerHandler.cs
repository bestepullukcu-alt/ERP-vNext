using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Queries;
using Diten.Platform.Application.Features.TimeEntry.Services;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.QueryHandlers;

/// <summary>
/// MOD-0280-FU01 D2 — the caller's own timer, never anyone else's (D11: there is no "who is running a timer" read).
/// Before reading, a segment its midnight has passed, or whose task left the caller, is closed — the pack's read-time
/// path, through the audited system commands (D3, §13). Nothing else is written.
/// </summary>
public sealed class GetMyTimerHandler : IRequestHandler<GetMyTimerQuery, Response<TimerDto>>
{
    private readonly ITimerReadModel _read;
    private readonly ICurrentUserContext _currentUser;

    public GetMyTimerHandler(ITimerReadModel read, ICurrentUserContext currentUser)
    {
        _read = read;
        _currentUser = currentUser;
    }

    public async Task<Response<TimerDto>> Handle(GetMyTimerQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = _currentUser.UserId;
        await _read.ReconcileAsync(userId, request.CorrelationId, ct);
        return Response<TimerDto>.Success(await _read.ReadAsync(userId, ct), correlationId: request.CorrelationId);
    }
}
