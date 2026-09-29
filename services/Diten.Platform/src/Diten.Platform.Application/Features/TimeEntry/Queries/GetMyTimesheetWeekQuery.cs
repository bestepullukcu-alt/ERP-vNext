using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Queries;

/// <summary>MOD-0280-FU01 — the caller's own week: days, targets, holidays, entries, flags, status. Writes nothing
/// except what MOD-0023 has already decided (the pull finalizer, D7).</summary>
public sealed record GetMyTimesheetWeekQuery(string WeekKey, string CorrelationId) : IRequest<Response<TimesheetWeekDto>>;
