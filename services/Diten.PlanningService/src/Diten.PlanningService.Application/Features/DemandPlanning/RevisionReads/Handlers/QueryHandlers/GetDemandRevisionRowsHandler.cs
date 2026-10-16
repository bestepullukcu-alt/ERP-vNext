using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

public sealed class GetDemandRevisionRowsHandler(
    IInternalPublishedSnapshotReader reader)
    : IRequestHandler<GetDemandRevisionRowsQuery, Response<RevisionRowPageView>>
{
    public async Task<Response<RevisionRowPageView>> Handle(
        GetDemandRevisionRowsQuery query, CancellationToken cancellationToken)
    {
        var result = await reader.ReadPageAsync(query.TenantId,
            query.LegalEntityId, query.RevisionId, query.ActorId,
            query.PageSize, query.Cursor, cancellationToken);
        if (result.Outcome != SnapshotReadOutcome.Found || result.Data is null)
            return RevisionReadFailure.FromSnapshot<RevisionRowPageView>(result.Outcome);
        return Response<RevisionRowPageView>.Success(
            RevisionReadViewMapper.Page(result.Data.Status,
                result.Data.Rows, result.Data.NextCursor));
    }
}
