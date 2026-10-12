using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Queries;

/// <summary>MOD-0280-FU01 T2a — the tasks the caller can add a time row for: open tasks they hold, and tasks they recorded
/// time on in the edit window and can still read. Filtered by title, at most <see cref="TimeEntryLimits.TaskOptionsMax"/>.
/// READ ONLY.</summary>
public sealed record GetTimeEntryTaskOptionsQuery(string? Search, string CorrelationId)
    : IRequest<Response<IReadOnlyList<TimeEntryTaskOptionDto>>>;
