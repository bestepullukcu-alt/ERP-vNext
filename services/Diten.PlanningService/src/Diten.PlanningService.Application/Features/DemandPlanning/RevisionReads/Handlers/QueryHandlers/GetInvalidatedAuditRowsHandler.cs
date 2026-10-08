using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

public sealed class GetInvalidatedAuditRowsHandler(
    IInternalInvalidatedHistoryReader reader)
    : IRequestHandler<GetInvalidatedAuditRowsQuery,
        Response<InvalidatedAuditRowPageView>>
{
    public async Task<Response<InvalidatedAuditRowPageView>> Handle(
        GetInvalidatedAuditRowsQuery query, CancellationToken cancellationToken)
    {
        var result = await reader.ReadPageAsync(query.TenantId,
            query.LegalEntityId, query.RevisionId, query.ActorId,
            query.PageSize, query.Cursor, cancellationToken);
        if (result.Outcome != SnapshotReadOutcome.Found || result.Data is null)
            return RevisionReadFailure.FromSnapshot<InvalidatedAuditRowPageView>(result.Outcome);
        return Response<InvalidatedAuditRowPageView>.Success(
            new InvalidatedAuditRowPageView(result.Data.Warning,
                result.Data.Status.State.ToString(),
                RevisionReadViewMapper.Page(result.Data.Status,
                    result.Data.Rows, result.Data.NextCursor)));
    }
}
