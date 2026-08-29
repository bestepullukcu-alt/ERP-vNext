using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.WorkingCalendar.Queries;
using MediatR;

namespace Diten.Platform.Application.Features.WorkingCalendar.Handlers.QueryHandlers;

/// <summary>
/// Serves the vocabulary both UIs render. The tenant slice is not a filtered view of the full contract at render
/// time — it is a different payload, so the override form structurally cannot offer country scope or country-layer
/// day types even if someone edits the page JS.
/// </summary>
public sealed class GetWorkingCalendarContractHandler
    : IRequestHandler<GetWorkingCalendarContractQuery, Response<object>>
{
    public Task<Response<object>> Handle(GetWorkingCalendarContractQuery request, CancellationToken ct)
    {
        object payload = request.TenantSlice
            ? WorkingCalendarValidation.BuildOverrideContract()
            : WorkingCalendarValidation.BuildContract();

        return Task.FromResult(Response<object>.Success(payload, 200));
    }
}
