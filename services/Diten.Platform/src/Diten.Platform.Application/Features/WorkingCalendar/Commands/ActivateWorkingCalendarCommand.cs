using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.WorkingCalendar.Commands;

/// <summary>Makes a calendar authoritative. For the country layer this changes the working-day answer for every
/// tenant in that country, which is why it sits behind its own permission.</summary>
public sealed record ActivateWorkingCalendarCommand(
    Guid Id,
    int ExpectedVersion,
    bool IsPlatformActor) : IRequest<Response<NoContent>>;
