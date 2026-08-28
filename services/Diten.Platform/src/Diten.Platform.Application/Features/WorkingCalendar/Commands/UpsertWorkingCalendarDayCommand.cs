using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.WorkingCalendar.Commands;

/// <summary>Adds or edits one embedded day. The whole aggregate is replaced under its version token, so days and the
/// calendar can never end up with two different concurrency behaviours.</summary>
public sealed record UpsertWorkingCalendarDayCommand(
    Guid CalendarId,
    WorkingCalendarDayInput Day,
    int ExpectedVersion,
    bool IsPlatformActor) : IRequest<Response<Guid>>;
