using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.WorkingCalendar.Queries;

/// <summary><paramref name="CountryLayer"/> selects the owning surface. The tenant surface first resolves its own
/// override and may then expose an ACTIVE country row as a read-only inherited detail; draft/archived country rows
/// remain hidden, and all mutations retain their separate own-override-only guard.</summary>
public sealed record GetWorkingCalendarByIdQuery(Guid Id, bool CountryLayer) : IRequest<Response<WorkingCalendarDto>>;
