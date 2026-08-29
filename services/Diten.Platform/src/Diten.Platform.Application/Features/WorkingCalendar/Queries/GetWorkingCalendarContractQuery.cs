using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.WorkingCalendar.Queries;

/// <summary>Feeds every dropdown on both surfaces — there is no hardcoded vocabulary anywhere in the views or JS.
/// The tenant slice omits country scope and the country-layer day types.</summary>
public sealed record GetWorkingCalendarContractQuery(bool TenantSlice) : IRequest<Response<object>>;
