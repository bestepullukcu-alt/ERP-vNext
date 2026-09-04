using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitReport.Queries;

/// <summary>
/// The Day/Week EXECUTION calendar read (D-CALENDAR-UI = A): the FU01 <c>PlannedVisit</c> atoms in the [<paramref
/// name="From"/>, <paramref name="To"/>] window (optionally narrowed to one <paramref name="ResourceId"/>), JOINED with
/// each visit's FU02 report state (none / draft / submitted / amended). Read-only; the join lives in the handler and
/// mutates nothing. The window is required so the read is bounded.
/// </summary>
public sealed record GetVisitCalendarQuery(
    string? From,
    string? To,
    string? ResourceId = null) : IRequest<Response<VisitCalendarDto>>;
