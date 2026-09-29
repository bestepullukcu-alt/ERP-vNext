using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeEntry.Queries;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.QueryHandlers;

public sealed class ResolveReopenTargetHandler : IRequestHandler<ResolveReopenTargetQuery, Response<Guid?>>
{
    private readonly ITimesheetWeekRepository _weeks;

    public ResolveReopenTargetHandler(ITimesheetWeekRepository weeks) => _weeks = weeks;

    public async Task<Response<Guid?>> Handle(ResolveReopenTargetQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.UserId == Guid.Empty || !WeekCalendar.TryParse(request.WeekKey, out var monday))
        {
            // The command itself answers the malformed request with its own code.
            return Response<Guid?>.Success((Guid?)null, correlationId: request.CorrelationId);
        }

        var revisions = await _weeks.ListRevisionsAsync(request.UserId, WeekCalendar.KeyOf(monday), ct);
        var target = revisions.FirstOrDefault(r => r.IsOpen) ?? revisions.LastOrDefault();
        return Response<Guid?>.Success(target?.Id, correlationId: request.CorrelationId);
    }
}
