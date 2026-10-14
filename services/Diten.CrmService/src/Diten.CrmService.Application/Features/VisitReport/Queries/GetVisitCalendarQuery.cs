using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitReport.Queries;

/// <summary>
/// The Day/Week EXECUTION calendar read (D-CALENDAR-UI = A): the FU01 <c>PlannedVisit</c> atoms in the [<paramref
/// name="From"/>, <paramref name="To"/>] window (optionally narrowed to one <paramref name="ResourceId"/>), JOINED with
/// each visit's FU02 report state (none / draft / submitted / amended). Read-only; the join lives in the handler and
/// mutates nothing. The window is required so the read is bounded.
/// <para>WP-VW-W1 — <paramref name="WorkStatus"/> is an optional comma-separated filter over the derived work status
/// (<c>missed,expired</c>); an unknown code is refused (400 visit_report_work_status_invalid). Absent ⇒ no filter.</para>
/// </summary>
public sealed record GetVisitCalendarQuery(
    string? From,
    string? To,
    string? ResourceId = null,
    string? WorkStatus = null) : IRequest<Response<VisitCalendarDto>>;
