using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.WorkingCalendar.Commands;

/// <summary>Archive is terminal and there is no delete — history stays readable.</summary>
public sealed record ArchiveWorkingCalendarCommand(
    Guid Id,
    int ExpectedVersion,
    bool IsPlatformActor) : IRequest<Response<NoContent>>;
