using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Queries;

/// <summary>MOD-0280-FU01 D2 — the caller's timer: running segment, switch state, last night's midnight closes. A read
/// may close an orphaned or past-midnight segment first (an audited system command), never anything else.</summary>
public sealed record GetMyTimerQuery(string CorrelationId) : IRequest<Response<TimerDto>>;
