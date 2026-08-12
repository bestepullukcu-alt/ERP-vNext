using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation.Queries;
using MediatR;

namespace Diten.Platform.Application.Features.WorkAggregation.Handlers.QueryHandlers;

/// <summary>
/// BL-023 — reads the org chart through the SAME descent everything else uses
/// (<see cref="ITaskTeamResolver"/> → <see cref="ITaskAssignmentScopeResolver"/>), so the control the user sees
/// and the list they get can never disagree about whether they have a team.
/// </summary>
public sealed class GetMyTeamAvailabilityHandler
    : IRequestHandler<GetMyTeamAvailabilityQuery, Response<TeamAvailabilityDto>>
{
    private readonly ITaskTeamResolver _team;

    public GetMyTeamAvailabilityHandler(ITaskTeamResolver team) => _team = team;

    public async Task<Response<TeamAvailabilityDto>> Handle(
        GetMyTeamAvailabilityQuery request, CancellationToken ct)
    {
        var team = await _team.ResolveTeamAsync(ct);

        return Response<TeamAvailabilityDto>.Success(
            new TeamAvailabilityDto(team.HasTeam, team.UserIds.Count),
            correlationId: request.CorrelationId);
    }
}
