using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.WorkingCalendar.Commands;

/// <summary>Archives one day. An archived day is invisible to the provider but still readable in history.</summary>
public sealed record ArchiveWorkingCalendarDayCommand(
    Guid CalendarId,
    Guid DayId,
    int ExpectedVersion,
    bool IsPlatformActor) : IRequest<Response<NoContent>>;
