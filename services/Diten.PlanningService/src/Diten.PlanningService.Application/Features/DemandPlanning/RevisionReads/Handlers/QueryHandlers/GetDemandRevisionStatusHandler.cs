using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

public sealed class GetDemandRevisionStatusHandler(
    IInternalAuthoritativeRevisionStatusReader reader, TimeProvider clock)
    : IRequestHandler<GetDemandRevisionStatusQuery, Response<RevisionStatusView>>
{
    public async Task<Response<RevisionStatusView>> Handle(
        GetDemandRevisionStatusQuery query, CancellationToken cancellationToken)
    {
        var result = await reader.ReadAsync(query.TenantId, query.LegalEntityId,
            query.RevisionId, query.ActorId, cancellationToken);
        if (result.Outcome != AuthoritativeStatusOutcome.Found || result.Status is null)
            return RevisionReadFailure.FromStatus<RevisionStatusView>(result.Outcome);
        return Response<RevisionStatusView>.Success(
            RevisionReadViewMapper.Status(result.Status, clock.GetUtcNow()));
    }
}
