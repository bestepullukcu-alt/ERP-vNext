using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.WorkAggregation.Calendar;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// WP-TASK-CALENDAR-ENGINE-01 (E) — the Task Center's calendar feed. Read-only; the writes a calendar makes (plan,
/// unplan) go through the existing work-item action endpoint and the task endpoints.
/// </summary>
[ApiController]
[Route("api/v1/work")]
[Authorize]
public sealed class WorkCalendarController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public WorkCalendarController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    /// <summary>
    /// The caller's own planned work, own meetings, working windows and day types for a local-date range
    /// (<c>from</c>/<c>to</c> as YYYY-MM-DD, at most 42 days; a wider or inverted range answers
    /// <c>400 WORK_CALENDAR_RANGE_INVALID</c>).
    /// </summary>
    [HttpGet("calendar")]
    [LoginOnly("The caller's own planned work and own meeting invitations; the caller cannot name another subject, the same posture as work-items/mine.")]
    public async Task<IActionResult> GetCalendar(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new GetMyWorkCalendarQuery(from, to, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
            ? HttpContext.TraceIdentifier
            : _correlationContext.CorrelationId!;
}
